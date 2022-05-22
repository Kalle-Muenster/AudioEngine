using System;
using Stepflow.Controller;
using System.Collections.Generic;
#if X86_64
using Preci = System.Double;
using ControlledPreci = Stepflow.Controlled.Float64;
using MonoPreciFrame = Stepflow.Audio.FrameTypes.AuPCMf64bit1ch;
#elif X86_32
using Preci = System.Single;
using ControlledPreci = Stepflow.Controlled.Float32;
using MonoPreciFrame = Stepflow.Audio.FrameTypes.AuPCMf32bit1ch;
#endif

namespace Stepflow.Audio.Elements
{
    public class ThreBandEQ : FxPlug
    {
        public class Insert
            : InsertEffect<ThreBandEQ>, IInsert
        {
            public override bool ByPass { get { return fxImpl().ByPass; } set { fxImpl().ByPass = value; } }

            public Insert() : base() {
                impl = new ThreBandEQ( this );
                FxType = EffectType.Filter;
            }

            public override List<IParameter<Preci>> GetParameters() {
                return impl.allparams;
            }

            public ThreBandEQ Fx() { return impl; }
        }

        public class Send
            : SendEffect<ThreBandEQ>
            , ISend
        {
            public Send() : base() {
                impl = new ThreBandEQ( this );
                FxType = EffectType.Filter;
            }

            public override List<IParameter<Preci>> GetParameters() {
                return impl.allparams;
            }

            public ThreBandEQ Fx() { return impl; }
        }

        public enum PARAMETERS {
            IN,LO,MI,HI, LowSplit, HighSplit
        }

        private ModulationPointer ingain, logain, migain, higain, losplit, hisplit;

        protected List<IParameter<Preci>> allparams;
        
        public ControllerBase[] chn;

        public override Array Parameters
        {
            get { return Enum.GetValues(typeof(PARAMETERS)); }
        }

        public override IElmPtr<Preci> this[int parameter] {
            get { return this[(PARAMETERS)parameter]; }
            set { this[(PARAMETERS)parameter] = value; }
        }

        public IElmPtr<Preci> this[PARAMETERS parameter]
        {
            get { switch( parameter ) {
                    case PARAMETERS.IN: return ingain;
                    case PARAMETERS.LO: return logain;
                    case PARAMETERS.MI: return migain;
                    case PARAMETERS.HI: return higain;
                    case PARAMETERS.LowSplit: return losplit;
                    case PARAMETERS.HighSplit: return hisplit;
                    default:  throw new Exception("in,lo,mi,hi...");
                }
            }

            set { switch( parameter ) {
                  case PARAMETERS.IN: value.pointer = ingain.pointer; break;
                  case PARAMETERS.LO: value.pointer = logain.pointer; break;
                  case PARAMETERS.MI: value.pointer = migain.pointer; break;
                  case PARAMETERS.HI: value.pointer = higain.pointer; break;
                  case PARAMETERS.LowSplit: value.pointer = losplit.pointer; break;
                  case PARAMETERS.HighSplit: value.pointer = hisplit.pointer; break; 
                  default:  throw new Exception("in,lo,mi,hi...");
                }
            }
        }

        public Preci InGain {
            get{ unsafe{ return *(Preci*)ingain.pointer.ToPointer(); } }
            set { unsafe{ *(Preci*)ingain.pointer.ToPointer() = value; } }
        }
        public Preci LoGain {
            get{ unsafe{ return *(Preci*)logain.pointer.ToPointer(); } }
            set { unsafe{ *(Preci*)logain.pointer.ToPointer() = value; } }
        }
        public Preci MiGain {
            get{ unsafe{ return *(Preci*)migain.pointer.ToPointer(); } }
            set { unsafe{ *(Preci*)migain.pointer.ToPointer() = value; } }
        }
        public Preci HiGain {
            get{ unsafe{ return *(Preci*)higain.pointer.ToPointer(); } }
            set { unsafe{ *(Preci*)higain.pointer.ToPointer() = value; } }
        }

        public override bool ByPass
        {
            get { unsafe { return (*(byte*)chn[0].GetPin( EQ3BandFilterPins.ByPass ).ToPointer() & 0x01) != 0; } }
            set
            {
                unsafe
                {
                    AudioFrameType t = (elm.render as ElementarTrack).frameType();
                    int setto = *(byte*)chn[0].GetPin( EQ3BandFilterPins.ByPass ).ToPointer();
                    if ( value ) setto |= 1;
                    else setto &= ~1;
                    for( int i = 0; i < t.ChannelCount; ++i ) {
                        *(byte*)chn[i].GetPin( EQ3BandFilterPins.ByPass ).ToPointer() = (byte)setto;
                    }
                }
            }
        }

        public Compress Compression
        {
            get
            {
                unsafe
                {
                    return (Compress)(*(byte*)chn[0].GetPin(EQ3BandFilterPins.ByPass).ToPointer() & (byte)Compress.PRE);
                }
            }
            set
            {
                AudioFrameType t = (elm.render as ElementarTrack).frameType();
                for (int c = 0; c < t.ChannelCount; ++c) unsafe
                    {
                        byte* setting = (byte*)chn[c].GetPin(EQ3BandFilterPins.ByPass).ToPointer();
                        byte bypass = (byte)(*setting & 0x01);
                        *(Compress*)setting = (Compress)( bypass | (byte)value );
                    }
            }
        }

        public ThreBandEQ( Insert element ) : base(element) {
            elm.FxType = EffectType.Filter;
            elm.Add<ElementName>( GetType().Name );
        }

        public ThreBandEQ( Send element ) : base(element) {
            elm.FxType = EffectType.Filter;
            elm.Add<ElementName>( GetType().Name );
        }

        public override Element Init( Element attach, object[] initializations )
        {
            if( attach is Track ) {
                PcmFormat fmt = new PcmFormat();
                if( attach is MixTrack ) {
                    MixTrack spur = attach as MixTrack;
                    if (initializations.Length > 0) {
                        if (initializations[0] is PcmFormat) {
                            fmt = (PcmFormat)initializations[0];
                        } else if (initializations.Length > 1) {
                            if (initializations[0] is AudioFrameType && ((initializations[1] is int) || (initializations[1] is uint)))
                                if (initializations[1] is int) fmt = ((AudioFrameType)initializations[0]).CreateFormatStruct((int)initializations[1]);
                                else fmt = ((AudioFrameType)initializations[0]).CreateFormatStruct((int)(uint)initializations[1]);
                        } else fmt = spur.frameType().CreateFormatStruct((int)initializations[0]);
                    } else try {
                        AudioFrameType typ = elm.render.track().frameType();
                        int rate = (int)elm.render.track().sampleRate();
                        fmt = typ.CreateFormatStruct(rate);
                    } catch(Exception ex) {
                        MessageLogger.logErrorSchlimm("missing audio format... getting format from rendertrack result: {0}",ex.Message);
                        return elm;
                    }
                } chn = setupFxControllers( fmt.FrameType, fmt.SampleRate, ControlMode.Filter3Band4Pole );

                ingain  = elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, chn[0].GetPin((int)EQ3BandFilterPins.InpGain),     Element.GANZ );
                logain  = elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, chn[0].GetPin((int)EQ3BandFilterPins.loPass.Gain), Element.GANZ );
                migain  = elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, chn[0].GetPin((int)EQ3BandFilterPins.miBand.Gain), Element.GANZ );
                higain  = elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, chn[0].GetPin((int)EQ3BandFilterPins.hiPass.Gain), Element.GANZ );
                losplit = elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, chn[0].GetPin((int)EQ3BandFilterPins.loSplit), Element.GANZ );
                hisplit = elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, chn[0].GetPin((int)EQ3BandFilterPins.hiSplit), Element.GANZ );

                for( int i = 1; i < fmt.NumChannels; ++i ) {
                    chn[i].SetPin((int)EQ3BandFilterPins.loPass.Gain, logain.pointer);
                    chn[i].SetPin((int)EQ3BandFilterPins.miBand.Gain, migain.pointer);
                    chn[i].SetPin((int)EQ3BandFilterPins.hiPass.Gain, higain.pointer);
                    chn[i].SetPin((int)EQ3BandFilterPins.InpGain, ingain.pointer);
                    chn[i].SetPin((int)EQ3BandFilterPins.loSplit, losplit.pointer);
                    chn[i].SetPin((int)EQ3BandFilterPins.hiSplit, hisplit.pointer);
                }
                allparams = new List<IParameter<Preci>> { ingain, logain, migain, higain, losplit, hisplit };
                return elm.Init( attach );
            } else {
                MessageLogger.logErrorSchlimm( "attach Track Element" );
            } return elm;
        } 

        protected ControllerBase[] setupFxControllers( AudioFrameType forType, uint samplerate, ControlMode mode )
        {
            output = forType.CreateEmptyFrame();
            ControllerBase[] setup = new ControllerBase[forType.ChannelCount];
            for( int i=0; i < setup.Length; ++i ) {
                switch( forType.BitDepth ) {
                    case 16: { Controlled.Int16 ctrl = new Controlled.Int16();
                        ctrl.SetUp( Int16.MaxValue / 2, Int16.MaxValue, (Int16.MaxValue / 3), 0, mode );
                        setup[i] = ctrl;
                    } break;
                    case 24: { Controlled.Int24 ctrl = new Controlled.Int24();
                        ctrl.SetUp( Int24.MinValue / 2, Int24.MaxValue, (Int24.MaxValue / 3), Int24.DB0Value, mode);
                        setup[i] = ctrl; 
                    } break;
                    case 32: { Controlled.Float32 ctrl = new Controlled.Float32();
                        ctrl.SetUp( 0.5f, 1.0f, 1.0f/3.0f, 0.0f, mode);
                        setup[i] = ctrl; 
                    } break;
                    case 64: { Controlled.Float64 ctrl = new Controlled.Float64();
                        ctrl.SetUp( 0.5, 1.0, 1.0/3.0, 0.0, mode);
                        setup[i] = ctrl; 
                    } break;
                }
            } return setup;
        }

        public override IAudioFrame DoFrame( IAudioFrame frame /* dry input... */ )
        {
            int channels = output.FrameType.ChannelCount;
            int bitdepth = output.FrameType.BitDepth;

            for(int i = 0; i < channels; ++i ) {
                switch( bitdepth ) {
                    case 16: { (chn[i] as Controlled.Int16).VAL = (short)frame.get_Channel(i);
                               output.set_Channel( i, (chn[i] as Controlled.Int16).VAL ); } break;
                    case 24: { (chn[i] as Controlled.Int24).VAL = (Int24)frame.get_Channel(i);
                               output.set_Channel( i, (chn[i] as Controlled.Int24).VAL ); } break;
                    case 32: { (chn[i] as Controlled.Float32).VAL = (float)frame.get_Channel(i);
                               output.set_Channel( i, (chn[i] as Controlled.Float32).VAL ); } break;
                    case 64: { (chn[i] as Controlled.Float64).VAL = (double)frame.get_Channel(i);
                               output.set_Channel( i, (chn[i] as Controlled.Float64).VAL ); } break;
                }
            } return output; // wet output...
        }

        public override void ApplyOn( ref IAudioFrame frame )
        {
            elm.ApplyOn( ref frame );
        }
    }
}

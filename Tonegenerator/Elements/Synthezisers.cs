using System;
using System.Collections.Generic;
using Stepflow.Controller;
using Stepflow.Audio.FileIO;
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



    /*
 
     XML approach how setting up an osc could look like

     <osc frq="300" amp="0.75">
        <pan val="Neutral"/>
        <amp>
            <evp AT="0.1" AL="0.9" DT="0.5" SL="0.3"/> <!== implies RT=0.6 ==!>
        </amp>
    
        <frm val="0"/>
     </osc>

    tonscript approach how defining a master + atached track which plays (.ply) a sine osc
    at runtime could look like: 

    

    mix[
    trk[1
    osc[sin
      frq[300]
      evp[0.1 0.9 0.5 0.3]
      pan[]
      amp[]
    ]
    ]
    ].ply

    and how accessing elements on the playing audio track during playback would look like:

    mix.trk(1).osc(sin).amp = 0.85

    for notation using tonescript this would mean each command needs timeing information (due to lines are executed 'at once' when entered - means when interpreter parses a file instead everything would take place as fast as commands can be rendered.)

    tmc[0004520
    
    ]


     */

    public class OSC : ElementarTrack
    {
        public new const uint ElementCode = (uint)ToneScriptParser.ToneToken.osc;
        public const int FRQ = AMP + 1;

        public  ControlledPreci      osc;
        private ModulationValue     _frq;
        public  MonoPreciFrame       frm;

        public ModulationValue frq {
            get { return _frq; }
            set { if ( value != _frq ) {
                    value.usage = PARAMETER.Frequency;
                   _frq = Set<ModulationParameter>(FRQ,value) as ModulationValue;
                }
            }
        }

        public OSC() : base() {
            osc = new ControlledPreci();
           _frq = new ModulationValue();
            frm = new MonoPreciFrame(0);
        }

        public override Element Init( Element synth, object[] parameter )
        {
            amp.Init( this, PARAMETER.Volume );
            pan.Init( this, PARAMETER.Panorama );
            frq.Init( this, PARAMETER.Frequency, NULL );

            if ( parameter.Length < 2 )
                MessageLogger.logErrorSchlimm( "OSC initialization missing form and frequency parameter" );

            FORM form = (FORM)parameter[0];
            Preci[] frqVals = parameter[1] as Preci[];

            if( parameter.Length > 2 ) {
                if ( parameter[2] is Panorama[] ) {
                    Panorama[] panVals = parameter[2] as Panorama[];
                    object arg = (panVals.Length > 1) ? (object)panVals : (object)panVals[0];
                    pan = new LinearPanChange().Init( this, arg ) as LinearPanChange;
                }
            }
            osc.SetUp( -GANZ, GANZ, frqVals[0], NULL, (ControlMode)form );
            osc.SetCheckAtGet();
            osc.Active = true;
            if ( frqVals.Length > 1 )
                frq.value = frqVals[1];

            return Init( synth );
        }

        public override AudioFrameType frameType()
        {
            return MonoPreciFrame.type;
        }

        public override void AddEnvelop( PARAMETER target, Preci[][] ADSR, uint framecount )
        {
            switch (target)
            {
                case PARAMETER.Volume: {
                        EVP evp = new EVP().Init(this, target, ADSR, framecount) as EVP;
                       _amp = Set<ModulationParameter>(AMP,evp);
                    } break;
                case PARAMETER.PanStero: {
                        EVP evp = new EVP().Init(this, target, ADSR, framecount) as EVP;
                        pan.sides = evp; } break;
                case PARAMETER.PanFront: {
                        EVP evp = new EVP().Init(this, target, ADSR, framecount) as EVP;
                        pan.front = evp; } break;
                case PARAMETER.WaveForm: {
                        osc.SetPin(PulsFormer.FORM, ref Add<ModulationParameter,EVP>(this, target, ADSR, framecount).value );
                    } break;
                case PARAMETER.Frequency: {
                        osc.SetPin(PulsFormer.MOV, ref Add<ModulationParameter, EVP>(this, target, ADSR, framecount).value);
                    } break;
            }
        }

        public override void AddLFO( PARAMETER target, Preci from, Preci to, Preci frequency, ControlMode form, uint samplerate )
        {
            Add<LFO>( target, from, to, frequency, form, (Preci)samplerate );
        }


        public override void FillFrame( ref IAudioFrame outputFrame )
        {
            outputFrame.Clear();
            Panorama p = pan;
            switch (outputFrame.FrameType.BitDepth ) {
                case 8:  outputFrame.Mix((sbyte)((osc.VAL * amp) * 127), p); break;
                case 16: outputFrame.Mix((Int16)((osc.VAL * amp) * Int16.MaxValue), p); break;
                case 24: outputFrame.Mix((Int24)((osc.VAL * amp) * Int24.MaxValue), p); break;
                case 32: outputFrame.Mix((float) (osc.VAL * amp), p); break;
                case 64: outputFrame.Mix((double)(osc.VAL * amp), p); break;
            }           
            if( Has<InsertEffect>() )
                DoInsertsChain( ref outputFrame );
        }
        public override void MixInto( ref IAudioFrame outputFrame, float drywet )
        {
            outputFrame.Mix( PullFrame().Convert( outputFrame.FrameType ).Pan( pan ), drywet );
        }

        protected override IAudioFrame pullFrame()
        {
            frm.SetChannel( 0, osc.VAL * amp );
            return frm;
        }

        public override void PushFrame( IAudioOutStream outputStream )
        {
            IAudioFrame fr = outputStream.GetFrameType().CreateEmptyFrame();
            FillFrame( ref fr );
            outputStream.WriteFrame( fr );
        }

        protected override void update()
        {
            amp.Update( phasys );
            pan.Update( phasys );
            if ( frq.type == MODULATOR.StaticValue ) {
                osc.MOV += frq;
            } else frq.Update( phasys );
            if( Has<LFO>() )
                UpdateAll<LFO>( phasys );
        }

        public override uint GetElementCode()
        {
            return ElementCode | ( osc.GetModeCode() << 32 );
        }

        public override uint sampleRate()
        {
            return track().element().render.sampleRate();
        }
    }

    public class SourceSample : ElementarTrack
    {
        public new const uint ElementCode = 5652823;
        private AudioStreamBuffer        sample;

        public class SampleLength : ElementLength
        {
            public SourceSample          source;
            public Preci                 offset { get { return Get<ModulationParameter>(0); } }
            public Preci                 endset { get { return Get<ModulationParameter>(1); } }
            public ModulationValue       number { get { return Get<ModulationParameter,ModulationValue>(2); } }
            override public uint         frames { get{ return source.sample.FrameCount; } set{ /*TODO: implement addjusting endcut */ } }
            public bool                  looped;
            public uint                  slices;
            public bool                  change;

            private uint _offset, _endset;

 
            public SampleLength() : base()
            {
                source = null;
                looped = false;
                change = false;
                slices = 1;
                _offset = _endset = 0;
            }
            public override Element Init( Element attach )
            {
                if( attach is SourceSample )
                    source = attach as SourceSample;
                Get<ModulationParameter>(0).Init(this);
                Get<ModulationParameter>(1).Init(this);
                Get<ModulationParameter>(2).Init(this);
                return base.Init( attach );
            }
            public override Element Init( Element attach, object[] initializations )
            {
                if( attach is SourceSample )
                    source = attach as SourceSample;
                looped = (bool)initializations[0];
                _offset = (uint)initializations[1];
                Set<ModulationParameter>(0,(new ModulationValue()).Init(this, new object[] { GANZ }) as ModulationValue);
                _endset = (uint)initializations[2];
                Set<ModulationParameter>(1,(new ModulationValue()).Init(this, new object[] { GANZ }) as ModulationValue);
                slices = (uint)initializations[3];
                Set<ModulationParameter>(2,(new ModulationValue()).Init(this, new object[] { GANZ }) as ModulationValue);
                return base.Init( attach );
            }
        }
        public SampleLength length;

        internal override List<ToneScriptParser.Token> accepts { get {
            return new List<ToneScriptParser.Token>() {
                new ToneScriptParser.Token(ToneScriptParser.ScopeType.Elementar,ToneScriptParser.ToneToken.wav),
                new ToneScriptParser.Token(ToneScriptParser.ScopeType.Modulator,ToneScriptParser.ToneToken.va4)
                                                        };
            }
        }

        public SourceSample() : base()
        {
            length = Get<ElementLength,SampleLength>(0);
        }

        /// <summary> SourceSample (ElementarTrack) </summary>
        /// Elementary element for realizing sample data based sound generators
        /// <param name="file">path to a file which provides sample data to be loaded
        /// supported are only files which deliver uncompressed pcm data., as well as
        /// .ton script files which define synthetization parameters (short files will
        /// be loaded, parsed and rendered to a buffer before playback begins. larger
        /// files may be progressed during playback on demand via using a file reader
        /// </param><param name="timing">filename can be followed by up to 3 numeric
        /// values which can be given as start offset, endcut offset (for samples),
        /// or slicen lengths and slicen counts (for setting up wavetable synthesys) 
        /// </param><returns></returns>

        public override Element Init( Element attach, object[] parameter )
        {
            MonoPreciFrame frm = new MonoPreciFrame();
            string file = parameter[0] as string;
            if( file.EndsWith(".wav") || file.EndsWith(".au") || file.EndsWith(".snd") ) {
                WaveFileReader loader = new WaveFileReader( parameter[0] as string );
                sample = new AudioStreamBuffer( loader.ReadAll().convert( frm.FrameType ) );
            } else if ( file.EndsWith(".ton") ) {
                Track track = render;
                while( !(track is MasterTrack) )
                    track = track.render;
                uint rate = (track as MasterTrack).format.SampleRate;
                PcmFormat fmt = frm.FrameType.CreateFormatStruct( (int)rate );
                ToneScriptParser parse = new ToneScriptParser(fmt,false,false);
                parse.loadScript(file);
                parse.parseInput();
                MasterTrack scriptmaster = parse.OutputMixer;
                sample = new AudioStreamBuffer( ref fmt, scriptmaster.FrameCount, true );
                scriptmaster.AttachOutputStream( sample );
                ToneGenerator.render( scriptmaster );
            } Init( this ); 
            if ( parameter.Length > 1 ) {
                uint[] len = (uint[])parameter[1];
                if ( len.Length > 1 ) {
                    if ( len.Length > 2 ) {
                        length.Init(this, new object[] { true, len[0], len[1], len[2], 0u });
                    } else {
                        length.Init(this, new object[] { true, len[0], len[1], 0u, 0u  });
                    }
                } length.Init( this, new object[] { false, len[0], 0u, 0u, 0u });
            } length.Init( this ); 
            return this;
        }
        protected override IAudioFrame pullFrame()
        {
            return sample.ReadFrame().Amp( amp );
        }
        public override void FillFrame( ref IAudioFrame frame )
        {
            frame.Clear();
            Preci currentFrame = ((MonoPreciFrame)pullFrame())[0];
            switch( sample.GetFrameType().BitDepth ) {
                case 8:  frame.Mix( (sbyte)( currentFrame * 127), pan ); break;
                case 16: frame.Mix( (short)( currentFrame * Int16.MaxValue ), pan ); break;
                case 24: frame.Mix( (Int24)( currentFrame * Int24.MaxValue ), pan ); break;
                case 32: frame.Mix( (float) currentFrame, pan ); break;
                case 64: frame.Mix( (double) currentFrame, pan ); break;
            }
        }
        override public void PushFrame( IAudioOutStream destination )
        {
            destination.WriteFrame( pullFrame().Convert( destination.GetFrameType() ).Pan( pan ) );
        }
        public override void MixInto( ref IAudioFrame frame, float drywet )
        {
            frame.Mix( pullFrame().Convert( frame.FrameType ).Pan( pan ), drywet );
        }

        public override AudioFrameType frameType()
        {
            return sample.GetFrameType();
        }

        public override uint GetElementCode()
        {
            return ElementCode;
        }

        protected override void update()
        {
            if ( length.slices > 1 ) {
                uint aaa = (uint)( length.number * length.slices );
                uint bbb = (uint)( (GANZ-length.number) * length.slices );
                sample = sample.getOrigin().getRange( aaa * length, bbb * length ) as AudioStreamBuffer;
            }
            if ( sample.AvailableFrames() == 0 ) {
                sample.SeekRead( 0 );
            }
        }

        public override uint sampleRate()
        {
            return sample.Format.SampleRate;
        }
    }
}

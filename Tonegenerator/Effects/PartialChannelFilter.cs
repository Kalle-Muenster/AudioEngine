using System;
using Stepflow.Controller;
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
    [Flags] public enum Compress { POST = 0x80, OFF = 0x02, PRE = 0x82 };

    /// <summary> PartialChannelFilter (InsertEffect)
    /// Defines an InsertEffect to be at best placed
    /// on a TrackMaster output track. It sets up fx
    /// controllers as frequency split to make .1 and
    /// center channels gain just partial frequencies
    /// when output format defines surround channels
    /// (.1 channel gains just lower sub band fequency
    /// and center channel mostly mid band frequencies) 
    /// - additionally it offers applying soft compression
    // (3:2 @ +half) to either the input before band
    // split or to the output after filtering is done
    /// </summary>
    public class PartialChannelFilter : InsertEffect
    {
        private AudioFrameType  constallation;
        private uint               samplerate;
        private ControllerBase[]    SplitFreq;
        private IntPtr[][]          SplitGain;

        public override bool ByPass {
            get { unsafe { return (0x01 & *(byte*)SplitFreq[0].GetPin(EQ3BandFilterPins.ByPass).ToPointer()) == 0x01; } }
            set { unsafe { *(byte*)SplitFreq[0].GetPin(EQ3BandFilterPins.ByPass).ToPointer() |= (byte)(value ? 0x01 : 0x00); } }
        }

        public void setCompression( Compress mode )
        {
            int pchs = constallation.ChannelCount - 4;
            unsafe { for(int c = 0; c < pchs; ++c ) {
                byte* setting = (byte*)SplitFreq[c].GetPin(EQ3BandFilterPins.ByPass).ToPointer();
                byte bypass = (byte)(*setting & 0x01);
                    *(Compress*) setting = mode;
                    *setting |= bypass;
                }
            }
        }


        public PartialChannelFilter() : base() {
            FxType = EffectType.Master|EffectType.Surround|EffectType.Filter;
        }

        /// <sumary> Init(trackelement,parameters[])
        /// Initialization function called for attaching a PartialChannelFilter to
        /// an Audio Track's fx insert chain. First parameter (Element where to attach to)
        /// must be a MixTrack element or at least of some type which derives from the
        /// MixTrack Element class </sumary><parameter>
        /// <param name="MixTrack"> track element where to attach to </param>
        /// <param type="object[]" name="parameter"> A parameter object[] array. assumed to
        /// deliver information about the audio format which the track elemet uses as either:
        /// one parameter: <param name="PcmFormat"> format </param> or:
        /// two parameter: <param name="AudioFrameType"> first paramete r</param> and
        ///                <param name="SampleRate"> second parameter </param></parameter>
        public override Element Init( Element audiotrack, object[] parameter )
        {
            if ( parameter[0] is PcmFormat ) {
                PcmFormat fmt = (PcmFormat)parameter[0];
                constallation = fmt.FrameType;
                samplerate = fmt.SampleRate;
            } else {
                constallation = (AudioFrameType)parameter[0];
                samplerate = (uint)parameter[1];
            }

            // Prepare EQ filter to be applied as band split for partial channels
            // like .1 (sub-woof), center, or upper rears which shall not gain the
            // full spectrum of genrated signal but just partial frequency ranges:
            int partialChannels = constallation.ChannelCount - 4;
            partialChannels = partialChannels > 0 ? partialChannels : 0;
            SplitFreq = new ControllerBase[partialChannels];
            for ( int i = 0; i < partialChannels; ++i  ) {
            switch ( constallation.BitDepth ) {
                case 16: { 
                     Controlled.Int16 eq = new Controlled.Int16();
                     eq.SetUp( Int16.MaxValue / 2, Int16.MaxValue, Int16.MaxValue / 3, 0, ControlMode.Filter3Band4Pole );
                     eq.SetCheckAtSet();
                     SplitFreq[i] = eq; } break;
                case 24: { 
                     Controlled.Int24 eq = new Controlled.Int24();
                     eq.SetUp( Int24.MaxValue / 2, Int24.MaxValue, Int24.MaxValue / 3, Int24.DB0Value, ControlMode.Filter3Band4Pole );
                     eq.SetCheckAtSet();
                     SplitFreq[i] = eq; } break;
                case 32: { 
                     Controlled.Float32 eq = new Controlled.Float32();
                     eq.SetUp( 0.5f, 1.0f, 1.0f/3.0f, 0.0f, ControlMode.Filter3Band4Pole );
                     eq.SetCheckAtSet();
                     SplitFreq[i] = eq; } break;
                case 64: { 
                     Controlled.Float64 eq = new Controlled.Float64();
                     eq.SetUp( 0.5, 1.0, 1.0/3.0, 0.0, ControlMode.Filter3Band4Pole );
                     eq.SetCheckAtSet();
                     SplitFreq[i] = eq; } break;
                }
            } SplitGain = new IntPtr[partialChannels][];
            for ( int c = constallation.ChannelCount-1; c >= 4; --c ) unsafe { int C = c-4;
                if ( c == (constallation.ChannelCount-2) ) {
                    SplitGain[C] = new IntPtr[3] {
                        SplitFreq[C].GetPin( (int)EQ3BandFilterPins.loPass.Gain ),
                        SplitFreq[C].GetPin( (int)EQ3BandFilterPins.miBand.Gain ),
                        SplitFreq[C].GetPin( (int)EQ3BandFilterPins.hiPass.Gain ) };
                    *(Preci*)SplitGain[C][0].ToPointer() = NULL;
                    *(Preci*)SplitGain[C][1].ToPointer() = GANZ;
                    *(Preci*)SplitGain[C][2].ToPointer() = GANZ/(Preci)5;
                    *(Preci*)SplitFreq[C].GetPin( (int)EQ3BandFilterPins.InpGain ).ToPointer() = GANZ/(Preci)3;
                } else if ( c == (constallation.ChannelCount-1) ) {
                    SplitGain[C] = new IntPtr[3] {
                        SplitFreq[C].GetPin( (int)EQ3BandFilterPins.loPass.Gain ),
                        SplitFreq[C].GetPin( (int)EQ3BandFilterPins.miBand.Gain ),
                        SplitFreq[C].GetPin( (int)EQ3BandFilterPins.hiPass.Gain ) };
                    *(Preci*)SplitGain[C][0].ToPointer() = GANZ;
                    *(Preci*)SplitGain[C][1].ToPointer() = NULL;
                    *(Preci*)SplitGain[C][2].ToPointer() = NULL;
                    *(Preci*)SplitFreq[C].GetPin( (int)EQ3BandFilterPins.InpGain ).ToPointer() = HALB;
                } else {
                    SplitGain[C] = new IntPtr[3] {
                        SplitFreq[C].GetPin( (int)EQ3BandFilterPins.loPass.Gain ),
                        SplitFreq[C].GetPin( (int)EQ3BandFilterPins.miBand.Gain ),
                        SplitFreq[C].GetPin( (int)EQ3BandFilterPins.hiPass.Gain ) };
                    *(Preci*)SplitGain[C][0].ToPointer() = NULL;
                    *(Preci*)SplitGain[C][1].ToPointer() = GANZ/(Preci)5;
                    *(Preci*)SplitGain[C][2].ToPointer() = GANZ;
                    *(Preci*)SplitFreq[C].GetPin( (int)EQ3BandFilterPins.InpGain ).ToPointer() = GANZ/(Preci)4;
                } SplitFreq[C].Active = true;
            }

            setCompression( Compress.OFF );
            return Init( audiotrack );
        }

        public override IAudioFrame DoFrame( IAudioFrame outputFrame )
        {
            if ( constallation.ChannelCount > 4 )
                ApplyOn( ref outputFrame );
            return outputFrame;
        }

        public override void ApplyOn( ref IAudioFrame frame /* in: dry, out: wet */ )
        {
            // if the outputstream defines any partial channels, apply filters to these to make them
            // contain just defined split parts of the downmixed signals actual frequency spectre
            if ( constallation.ChannelCount > 4 ) { 
                switch( constallation.BitDepth ) {
                    case 16: { for ( int c = 4; c < constallation.ChannelCount; ++c ) {
                            Controlled.Int16 chn = SplitFreq[c-4] as Controlled.Int16;
                            chn.VAL = (Int16)frame.get_Channel(c);
                            frame.set_Channel( c, chn.VAL );
                        } break; }
                    case 24: { for ( int c = 4; c < constallation.ChannelCount; ++c ) {
                            Controlled.Int24 chn = SplitFreq[c-4] as Controlled.Int24;
                            chn.VAL = (Int24)frame.get_Channel(c);
                            frame.set_Channel( c, chn.VAL );
                        } break; }
                    case 32: { for ( int c = 4; c < constallation.ChannelCount; ++c ) {
                            Controlled.Float32 chn = SplitFreq[c-4] as Controlled.Float32;
                            chn.VAL = (float)frame.get_Channel(c);
                            frame.set_Channel( c, chn.VAL );
                        } break; }
                    case 64: { for ( int c = 4; c < constallation.ChannelCount; ++c ) {
                            Controlled.Float64 chn = SplitFreq[c-4] as Controlled.Float64;
                            chn.VAL = (double)frame.get_Channel(c);
                            frame.set_Channel( c, chn.VAL );
                        } break; }
                }
            }
        }
    }
}

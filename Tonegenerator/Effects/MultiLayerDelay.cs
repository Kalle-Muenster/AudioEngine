using System;
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
    public class MultiLayerDelay : FxPlug
    {
        public enum PARAMETERS
        {
            Delay /*seconds*/, Repeat /*x-times*/, Decay /*seconds*/, Panorama /*per each repeat*/
        }

        public enum PanParameter
        {
            AllDelays, Delay1, Delay2, Delay3, Delay4
        }

        public override bool     ByPass { get; set; }
        private AudioStreamBuffer[] buf;
        private IAudioFrame      reduce;
        private Panorama[]       pansen;
        private Panorama.Axis[]  axtens;
        public ElementLength     length;

        public ModulationPointer  count;
        public ModulationPointer  delay;
        public ModulationPointer  decay;

        public override Array Parameters
        {
            get { return Enum.GetValues( typeof(PARAMETERS) ); }
        }

        public IElmPtr<Preci> this[PARAMETERS parameter]
        {
            get
            {
                switch (parameter)
                {
                    case PARAMETERS.Delay: return delay;
                    case PARAMETERS.Repeat: return count;
                    case PARAMETERS.Decay: return decay;
                    default: throw new Exception("just Delay, Repeat, Decay...");
                }
            }

            set
            {
                switch (parameter)
                {
                    case PARAMETERS.Delay: value.pointer = delay.pointer; break;
                    case PARAMETERS.Repeat: value.pointer = count.pointer; break;
                    case PARAMETERS.Decay: value.pointer = decay.pointer; break;
                    default: throw new Exception("just Delay, Repeat, Decay...");
                }
            }
        }

        public IElmPtr<Panorama> this[PanParameter delayline]
        {
            get { return elm.Get<PanoramaValue>((int)delayline-1).elmptr(); }
            set { value.pointer = elm.Get<PanoramaValue>((int)delayline-1).elmptr().pointer; }
        }


        public override IElmPtr<Preci> this[int parameter]
        {
            get { if (parameter < 3) {
                    return this[(PARAMETERS)parameter];
                } else  {
                    parameter -= 3;
                    int delayline = parameter / 2;
                    int side = parameter % 2;
                    return side == 0
                         ? (this[(PanParameter)delayline] as PanoramaValue).sides.elmptr()
                         : (this[(PanParameter)delayline] as PanoramaValue).front.elmptr();
                }
            } set {
                if (parameter < 3) {
                    this[(PARAMETERS)parameter] = value;
                } else {
                    parameter -= 3;
                    int delayline = parameter / 2;
                    int side = parameter % 2;
                    if (side == 0) (this[(PanParameter)delayline] as PanoramaValue).sides = value.elementar() as ModulationParameter;
                    else (this[(PanParameter)delayline] as PanoramaValue).front = value.elementar() as ModulationParameter;
                }
            }
        }

        protected MultiLayerDelay(Insert element) : base(element)
        {
            elm.FxType = EffectType.Delay;
            elm.Add<ElementName>( GetType().Name );
        }

        protected MultiLayerDelay(Send element) : base(element)
        {
            elm.FxType = EffectType.Delay;
            elm.Add<ElementName>( GetType().Name );
        }

        public class Insert
            : InsertEffect<MultiLayerDelay>
        {
            public Insert()
            {
                impl = new MultiLayerDelay( this );
            }

            protected override void update()
            {
                impl.update();
                base.update();
            }
        }

        public class Send
            : SendEffect<MultiLayerDelay>
        {
            public Send()
            {
                impl = new MultiLayerDelay( this );
            }

            protected override void update()
            {
                impl.update();
                base.update();
            }
        }

        public override void ApplyOn( ref IAudioFrame frame )
        {
            if ( !ByPass ) elm.ApplyOn( ref frame );
        }



        public override IAudioFrame DoFrame( IAudioFrame /* 100% dry */ input )
        {
            if( ByPass ) {
                output.Set( input );
            return output; }

            int cascades = (int)count.actual;
            float reductio = 1.0f / cascades;
            float level = 1.0f;

            output.Clear();
            for ( int i = 0; i < cascades; ++i )
            { reduce.Set( input );
                AudioStreamBuffer buffer = buf[i]; 
                if( buffer.CanStream( StreamDirection.OUTPUT ) == 0 ) {
                    buffer.Seek( StreamDirection.READ, 0 );
                } input = buffer.ReadFrame();
                output.Add( input.Pan( pansen[i % 4], axtens[i % 4] ) ); 
                if( buffer.CanStream( StreamDirection.OUTPUT ) == 0 ) {
                    buffer.Seek( StreamDirection.WRITE, 0 );
                } buffer.WriteFrame( reduce.Amp( level ) );
            level -= reductio; }

            return /* 100% wet */ output;
        }

        public override Element Init( Element attach, params object[] initialize )
        {
            PcmFormat format;
            if ( attach is Effectroutes ) {
                // initialize as a send effect 
                format = (attach as Effectroutes).master.format;
            } else {
                // initialize as an insert effect
                format = (attach as MixTrack).frameType().CreateFormatStruct(
                    (int)(attach as MixTrack).master.format.SampleRate
                );
            }

            // test values actually hardcoded
            pansen = new Panorama[4] { new Panorama(0.25f), new Panorama(1.0f), new Panorama(0.0f), new Panorama(0.75f) };
            axtens = new Panorama.Axis[4] { Panorama.Axis.LeftRight, Panorama.Axis.LeftRight, Panorama.Axis.LeftRight, Panorama.Axis.LeftRight };

            // initilize length parameters
            Preci duration = (Preci)initialize[0];
            delay = elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, duration );
            delay.pointer = IntPtr.Zero;
            length = elm.Add<ElementLength>((uint)(duration * format.SampleRate));

            if ( initialize[1] is Preci ) { 
                duration = (Preci)initialize[1];
                decay = elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, duration );
                decay.pointer = IntPtr.Zero;
                count = elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, (Preci)(uint)(duration / delay) );
                count.pointer = IntPtr.Zero;
            } else if ( initialize[1] is UInt32 ) {
                count = elm.Add<ModulationParameter, ModulationPointer>( PARAMETER.FxPara, (UInt32)initialize[1] );
                count.pointer = IntPtr.Zero;
                duration = count * delay;
                decay = elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, duration );
                decay.pointer = IntPtr.Zero;
            } int cascadas = (int)count;

            // create the delay buffers
            reduce = format.CreateEmptyFrame();
            output = format.CreateEmptyFrame();
            buf = new AudioStreamBuffer[cascadas];
            for ( int i = 0; i < cascadas; ++i ) {
                Elementar<AudioStreamBuffer> elmbuf = elm.Add<Elementar<AudioStreamBuffer>>(
                    new AudioStreamBuffer( ref format, length.frames, false ) );
                elmbuf.entity.Seek( StreamDirection.READ, 0 );
                elmbuf.entity.Seek( StreamDirection.WRITE, 0 );
                for ( int l = 0; l < length.frames; ++l )
                    elmbuf.entity.WriteFrame( output );
                buf[i] = elmbuf.entity;
            } return elm.Init( attach );
        }

        protected void update()
        {
            MasterTrack mixer = elm.render.master;
            uint hasChanged = (uint)( delay * mixer.sampleRate() );
            uint cycleCount = (uint)count.actual;
            uint frameCount = length.frames;

            // if delay length parameter has changed value since the last
            // processed frame, addjust/reallocate delay buffers accordingly
            if( frameCount != hasChanged ) {
                if( hasChanged < frameCount ) {
                    for( int i = 0; i < cycleCount; ++i )
                        buf[i].AddOffset(
                            -(int)(frameCount - hasChanged) );
                } else {
                    for( int i = 0; i < cycleCount; ++i )
                        buf[i].append(
                            new AudioBuffer( 
                                mixer.frameType(),
                                mixer.sampleRate(),
                                hasChanged - frameCount )
                        );
                } if ( mixer.istime ) {
                    for( int i = 0; i < cycleCount; ++i )
                        buf[i].Compact();
                } length.frames = hasChanged;
            }

            // if repeat count (times the delay returns) was changed
            // then change number of delay buffers the instance uses
            hasChanged = (uint)(decay / delay);
            if( cycleCount != hasChanged ) {
                count.value = hasChanged;
            }
        }
    }
}

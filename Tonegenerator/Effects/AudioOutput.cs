#if DOT47 && X86_32
using System;
using System.Collections.Generic;

using Stepflow.TaskAssist;
using Stepflow.Audio.Elements;
using Stepflow.Audio.FrameTypes;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

using System.Runtime.InteropServices;


namespace Stepflow.Audio
{
    [StructLayout(LayoutKind.Explicit,Size = 4)]
    public struct Converter
    {
        [FieldOffset(0)] public AuPCMs16bit2ch frame;
        [FieldOffset(0)] public short L;
        [FieldOffset(0)] public byte byte0;
        [FieldOffset(1)] public byte byte1;
        [FieldOffset(2)] public short R; 
        [FieldOffset(2)] public byte byte2; 
        [FieldOffset(3)] public byte byte3; 
    }



    public class XnaAudioOutput : IAudioOutStream, IAudioStream
    {
        private DynamicSoundEffectInstance output;
        private byte[]                     outbuf;
        private PcmFormat                  format;
        private Converter                  inputs;
        private uint                       writen;
        private uint                       length;
        private int                        sprate;
        private static bool                Initialized = false;

        public XnaAudioOutput(int samplerate,uint framecount)
        {
            sprate = samplerate;
            length = framecount;
            inputs = new Converter();
            inputs.frame = new AuPCMs16bit2ch(0);
            writen = 0;
            if (!Initialized) {
                FrameworkDispatcher.Update();
                Initialized = true;
            }
            format = AuPCMs16bit2ch.type.CreateFormatStruct(samplerate);
            output = new DynamicSoundEffectInstance(samplerate, AudioChannels.Stereo);
            outbuf = new byte[framecount * format.BlockAlign];
            Volume = 0.5f;
            output.Pause();
        }

        public bool TrySubmitToOutputDevice()
        {
            int fillstate = output.PendingBufferCount;
            if( fillstate < 32 ) {
                if( writen > 0 ) {
                    output.SubmitBuffer( outbuf, 0, (int)writen );
                    writen = 0; return true;
                } else if ( fillstate == 0 ) {
                    Consola.StdStream.Err.WriteLine("buffer underrun");
                }
            } return false;
        }

        public float Volume {
            get { return output.Volume; }
            set { output.Volume = value; }
        }

        public Panorama Panorama {
            get { return new Panorama( output.Pan ); }
            set { output.Pan = value.LR;  }
        }

        public float Pitch {
            get { return output.Pitch; }
            set { output.Pitch = value; }
        }

        public bool Play {
            get { return output.State == SoundState.Playing; }
            set { if ( value != Play ) {
                    if( value ) { output.Play(); }
                    else { output.Pause(); } 
                  }
            }
        }

        public bool Pause {
            get { return !Play; }
            set { Play = !value; }
        }

        public uint CanStream( StreamDirection whereWhatWhyWhearin ) {
            if( Direction.In( whereWhatWhyWhearin ) )
                return length - (writen / 4);
            else return 0;
        }

        public uint WriteAudio( Audio data )
        {
            uint frmswrite = CanStream( Direction );
            frmswrite = frmswrite < data.FrameCount
                      ? frmswrite : data.FrameCount; 
            frmswrite = stream().Write( data, (int)frmswrite, 0 );
            Play = frmswrite > 0;
            return frmswrite;
        }

        public IAudioOutStream stream() {
            return this;
        }

        PcmFormat IAudioStream.GetFormat() {
            return inputs.frame.FrameType.CreateFormatStruct( sprate );
        }

        public AudioFrameType GetFrameType() {
            return inputs.frame.FrameType;
        }

        uint IAudioOutStream.WrittenBytes() {
            return writen * 4;
        }

        uint IAudioOutStream.WrittenFrames() {
            return writen;
        }

        public TimeSpan WrittenTime() {
            return output.GetSampleDuration((int)(writen * 4));
        }

        private void flushWriteHead()
        {
            outbuf[writen++] = inputs.byte0;
            outbuf[writen++] = inputs.byte1;
            outbuf[writen++] = inputs.byte2;
            outbuf[writen++] = inputs.byte3;
        } 

        public uint WriteFrame( short sample ) {
            inputs.L = sample;
            inputs.R = sample;
            flushWriteHead();
            return 4;
        }

        public uint WriteFrame( float sample ) {
            inputs.L = (short)(sample * Int16.MaxValue);
            inputs.R = inputs.L;
            flushWriteHead();
            return 4;
        }

        uint IAudioOutStream.WriteFrame( float sample, Panorama mixer ) {
            return WriteFrame( (AuPCMs16bit2ch)inputs.frame.Mix( (short)(sample * Int16.MaxValue), mixer ) );
        }

        uint IAudioOutStream.WriteFrame( short sample, Panorama mixer ) {
            return WriteFrame( (AuPCMs16bit2ch)inputs.frame.Mix( sample, mixer ) );
        }

        uint IAudioOutStream.WriteFrame( double sample, Panorama mixer ) {
            return WriteFrame( (AuPCMs16bit2ch)inputs.frame.Mix( (short)(sample * Int16.MaxValue), mixer ) );
        }

        void IAudioStream.Seek( StreamDirection A_0, uint A_1 ) {}
        
        StreamDirection IAudioStream.CanSeek() { return StreamDirection.NONE; }
        

        uint IAudioOutStream.Write( Audio data, int countFs, int FsOffsetSrc ) {
            if( data.GetFrameType().Code != AuPCMs16bit2ch.type.Code )
                data.convert( AuPCMs16bit2ch.type );
            return stream().Write( data.GetRaw(), countFs * 4, FsOffsetSrc * 4 );
        }

        uint IAudioOutStream.WriteAudio( Audio data ) {
            return stream().Write( data, (int)data.FrameCount, 0 );
        }

        public uint WriteFrame( AuPCMs16bit2ch frame )
        {
            inputs.frame = frame;
            flushWriteHead();
            return 4;
        }

        public uint WriteFrame( IAudioFrame frame ) {
            IAudioFrame conv = frame.Convert( AuPCMs16bit2ch.type.Code );
            if( conv is AuPCMs16bit2ch ) {
                inputs.frame = (AuPCMs16bit2ch)frame.Convert(AuPCMs16bit2ch.type.Code);
                flushWriteHead();
                return 4;
            } else return 0;
        }

        public uint WriteSample( short sample ) {
            inputs.L = sample;
            outbuf[writen++] = inputs.byte0;
            outbuf[writen++] = inputs.byte1;
            return 2;
        }

        public uint WriteSample( float sample ) {
            return WriteSample( (short)(sample*Int16.MaxValue) );
        }

        public uint Write( IntPtr rawData, int countBytes, int offsetSrcBytes )
        {
            uint writable = CanStream( Direction );
            if( writable == 0 ) return 0;
            int countFs = countBytes/4; 
            if( countFs > writable ) countFs = (int)writable;
            unsafe { uint* src = ((uint*)rawData.ToPointer())+(offsetSrcBytes/4);
                fixed( void* ptBuf = &outbuf[0] ) { uint* end = ((uint*)ptBuf)+countFs; 
                    for( uint* dst = (uint*)ptBuf; dst != end; ++dst )
                        *dst = *src++;
            } } return writen = (uint)(countFs*4);
        }

        uint IAudioStream.GetPosition( StreamDirection direction )
        {
            return direction.In( StreamDirection.WRITE )
                 ? writen : (uint)output.PendingBufferCount;
        }

        public StreamDirection Direction
        {
            get { return StreamDirection.OUTPUT; }
        }

    }

    internal struct MessDatenStruct
    {
        public TimeSpan AverageRendertime;
        public TimeSpan AverageIdleTime;
        public uint     PerLoopChunkSize;
    }

    internal class RenderPosition : Element
    {
        private uint pos;
        protected override void update() {
            ++pos;
            base.update();
        }

        public void Reset() { pos = 0; }
        public static implicit operator uint(RenderPosition cast)
        { return cast.pos; }
    }


    public abstract class Renderer
    {
        protected MasterTrack                     sourceage;
        protected IAudioStream                    outputage;

        public void AttachRenderSource( MasterTrack source )
        {
            sourceage = source;
            sourceage.Add<RenderPosition>().Reset();
            if ( outputage != null ) {
                sourceage.AttachOutputStream( outputage );
            }
        }

        public void AttachRenderTarget( IAudioStream target )
        {
            outputage = target;
            if ( sourceage != null ) {
                sourceage.AttachOutputStream( outputage );
            }
        }

        abstract protected void renderLoop();
        abstract public void Start();
        abstract public int Render( MixTrack source );
    } 

    public class SynchronRenderer : Renderer
    {
        protected override void renderLoop()
        {
            RenderPosition position = sourceage.Get<RenderPosition>();
            uint framecount = sourceage.FrameCount;
            while( position < sourceage.FrameCount ) {
                sourceage.Update();
            }
        }

        public override void Start()
        {
            renderLoop();
        }

        public override int Render( MixTrack source )
        {
            if ( outputage == null) {
                throw new Exception("No render target for output set");
            }
            if ( source is MasterTrack ) {
                sourceage = source as MasterTrack;
            } else {
                if ( sourceage != null ) {
                    sourceage.DetachTracks();
                } else {
                    sourceage = new MasterTrack( source.frameType().CreateFormatStruct( (int)outputage.GetFormat().SampleRate ) );
                } sourceage.AddTrack( source );
            }
            sourceage.AttachOutputStream( outputage );
            renderLoop();
            return (int)source.FrameCount;
        }
    }

    public class RealtimeRenderer
        : Renderer
        , ITaskAsistableVehicle<SteadyAction>
    {
        private TaskAssist<SteadyAction,Action> realtimer;                  
        private MessDatenStruct                 messdaten;
        private volatile bool                   intheloop;

        static RealtimeRenderer()
        {
            SteadyAction.SetDefaultThradingMode( true );
            TaskAssist<SteadyAction,Action>.Init( 30 );
        }

        public RealtimeRenderer()
        {
            intheloop = false;
            realtimer = new TaskAssist<SteadyAction,Action>( this, renderLoop, 30 );
            messdaten = new MessDatenStruct();
            messdaten.PerLoopChunkSize = 44100 / 30;
        }

        public override void Start()
        {
            task().StartAssist();
        }

        int ITaskAsistableVehicle<SteadyAction>.StartAssist()
        {
            return realtimer.GetAssistence( realtimer.action );
        }

        int ITaskAsistableVehicle<SteadyAction>.StoptAssist()
        {
            return realtimer.ReleaseAssist( realtimer.action );
        }

        public ITaskAsistableVehicle<SteadyAction> task() {
            return this;
        }

        public ITaskAssistor<SteadyAction> assist() {
            return realtimer;
        }

        protected override void renderLoop()
        {
            if( intheloop ) return;
            intheloop = true;
            uint writable = outputage.CanStream( StreamDirection.WRITE );
            if ( writable > messdaten.PerLoopChunkSize ) {
                 writable = messdaten.PerLoopChunkSize; 
            } do { sourceage.Update();
            } while( --writable > 0 );
            intheloop = false;
        }

        private void addtrack( MixTrack additional )
        {
            List<MixTrack> playings = new List<MixTrack>();
            IEnumerator<MixTrack> track = sourceage.All<MixTrack>();
            while( track.MoveNext() ) {
                playings.Add( track.Current );
            } sourceage.DetachTracks();
            if ( additional is MasterTrack ) {
                 sourceage = additional as MasterTrack;
            } track = playings.GetEnumerator();
            while ( track.MoveNext() ) {
                sourceage.AddTrack( track.Current );
            } if ( additional != sourceage ) {
                sourceage.AddTrack( additional );
            } sourceage.Enable();
        }

        public override int Render( MixTrack source )
        {
            if ( outputage == null ) {
                throw new Exception("No render target for output set");
            }
            if ( sourceage == null ) {
                sourceage = new MasterTrack( source.frameType().CreateFormatStruct( (int)outputage.GetFormat().SampleRate ) );
                sourceage.AttachOutputStream( outputage );
            }
            if ( intheloop ) {
                System.Threading.Tasks.Task spaeter = new System.Threading.Tasks.Task( ()=>{
                    while( intheloop ) System.Threading.Thread.Sleep(5);
                    intheloop = true; addtrack( source ); intheloop = false;
                } ); 
            } else addtrack( source );

            uint renderframes = sourceage.FrameCount;
            if ( !intheloop ) Start();
            
            return (int)renderframes;
        }
    }

}
#else
using System;
using System.Collections.Generic;
using Stepflow.Audio;
using Stepflow.Audio.FileIO;
using Stepflow.Audio.FrameTypes;
using Stepflow.Audio.Elements;
using System.Threading.Tasks;
using Stepflow.TaskAssist;
using System.Runtime.InteropServices;
using Stepflow;


namespace Stepflow.Audio
{
    [StructLayout(LayoutKind.Explicit, Size = 4)]
    public struct Converter
    {
        [FieldOffset(0)] public AuPCMs16bit2ch frame;
        [FieldOffset(0)] public short L;
        [FieldOffset(0)] public byte byte0;
        [FieldOffset(1)] public byte byte1;
        [FieldOffset(2)] public short R;
        [FieldOffset(2)] public byte byte2;
        [FieldOffset(3)] public byte byte3;
    }

    internal struct MessDatenStruct
    {
        public TimeSpan AverageRendertime;
        public TimeSpan AverageIdleTime;
        public uint PerLoopChunkSize;
    }

    internal class RenderPosition : Element
    {
        private uint pos;
        protected override void update()
        {
            ++pos;
            base.update();
        }

        public void Reset() { pos = 0; }
        public static implicit operator uint(RenderPosition cast)
        { return cast.pos; }
    }


    public abstract class Renderer
    {
        protected MasterTrack  sourceage;
        protected IAudioStream outputage;

        public void AttachRenderSource(MasterTrack source)
        {
            sourceage = source;
            sourceage.Add<RenderPosition>().Reset();
            if (outputage != null)
            {
                sourceage.AttachOutputStream(outputage);
            }
        }

        public void AttachRenderTarget(IAudioStream target)
        {
            outputage = target;
            if (sourceage != null)
            {
                sourceage.AttachOutputStream(outputage);
            }
        }

        abstract protected void renderLoop();
        abstract public void Start();
        abstract public int Render(MixTrack source);
    }

    public class SynchronRenderer : Renderer
    {
        protected override void renderLoop()
        {
            RenderPosition position = sourceage.Get<RenderPosition>();
            uint framecount = sourceage.FrameCount;
            while (position < sourceage.FrameCount)
            {
                sourceage.Update();
            }
        }

        public override void Start()
        {
            renderLoop();
        }

        public override int Render(MixTrack source)
        {
            if (outputage == null)
            {
                throw new Exception("No render target for output set");
            }
            if (source is MasterTrack)
            {
                sourceage = source as MasterTrack;
            }
            else
            {
                if (sourceage != null)
                {
                    sourceage.DetachTracks();
                }
                else
                {
                    sourceage = new MasterTrack(source.frameType().CreateFormatStruct((int)outputage.GetFormat().SampleRate));
                }
                sourceage.AddTrack(source);
            }
            sourceage.AttachOutputStream(outputage);
            renderLoop();
            return (int)source.FrameCount;
        }
    }

    public class RealtimeRenderer
        : Renderer
        , ITaskAsistableVehicle<Action,Action>
    {
        private TaskAssist<SteadyAction,Action,Action> realtimer;
        private MessDatenStruct messdaten;



        static RealtimeRenderer()
        {
            TaskAssist<SteadyAction,Action,Action>.Init(30);
        }

        public RealtimeRenderer()
        {
            realtimer = new TaskAssist<SteadyAction,Action,Action>(this, renderLoop, 30);
            messdaten = new MessDatenStruct();
            messdaten.PerLoopChunkSize = 44100 / 30;
        }

        public override void Start()
        {
            realtimer.action = renderLoop;
            task().StartAssist();
        }

        
        public ITaskAsistableVehicle<Action,Action> task()
        {
            return this;
        }

        int IAsistableVehicle<IActionDriver<Action,ILapFinish<Action>,Action>,ILapFinish<Action>>.StartAssist()
        {
             return realtimer.assist.GetAssistence( realtimer.action );
        }

        int IAsistableVehicle<IActionDriver<Action,ILapFinish<Action>,Action>,ILapFinish<Action>>.StoptAssist()
        {
             return realtimer.assist.ReleaseAssist( realtimer.action );
        }

        ITaskAssistor<Action,Action> ITaskAsistableVehicle<Action,Action>.assist
        {
            get { return realtimer; }
            set { realtimer = value as TaskAssist<SteadyAction,Action,Action>; }
        }

        protected override void renderLoop()
        {
            uint writable = outputage.CanStream( StreamDirection.OUTPUT );
            if (writable > messdaten.PerLoopChunkSize) {
                writable = messdaten.PerLoopChunkSize;
            } do {
                sourceage.Update();
            } while (--writable > 0);
        }

        public override int Render(MixTrack source)
        {
            if (outputage == null)
            {
                throw new Exception("No render target for output set");
            }
            if (source is MasterTrack)
            {
                sourceage = source as MasterTrack;
            }
            else
            {
                if (sourceage != null)
                {
                    sourceage.DetachTracks();
                }
                else
                {
                    sourceage = new MasterTrack(source.frameType().CreateFormatStruct((int)outputage.GetFormat().SampleRate));
                }
                sourceage.AddTrack(source);
            }
            sourceage.AttachOutputStream(outputage);
            renderLoop();
            return (int)source.FrameCount;
        }
    }

}


#endif

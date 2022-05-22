using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#if X86_64
using Ticks = System.UInt64;
using Preci = System.Double;
using ControlledPreci = Stepflow.Controlled.Float64;
using MonoPreciFrame = Stepflow.Audio.FrameTypes.AuPCMf64bit1ch;
#elif X86_32
#endif

namespace Stepflow.Audio.Elements
{
    public struct tuctdef
    {
        public enum Tackts : byte { 
            ThreeQuaters = 3, FourQuaters = 4, SixQuaters = 6,
        }
        private byte   segmentCount;   // quater notes per bar
        private byte   metronomBeat;   // metronome beats per bar
        private uint   metronomTime;   // cpu ticks per metronome beat
        private uint   segmentTicks;   // cpu ticks per quater note
        private uint   segmentAudio;   // audio frames per quater note

        public tuctdef( Tackts tackt, int tempo, uint samplerate )
        {
            segmentCount = 4;
            metronomBeat = 16;
            double timebeat = (1.0 / ((double)tempo / 60.0)) * System.Diagnostics.Stopwatch.Frequency;
            metronomTime = (uint)(timebeat / (metronomBeat / segmentCount) );
            segmentTicks = (uint) timebeat;
            segmentAudio = (uint)(timebeat / ((1.0 / samplerate) * System.Diagnostics.Stopwatch.Frequency) );
            Segments = tackt;
        }

        /// <summary> SetBPM(bpm): change metronom speed to given tempo </summary>
        /// <param name="bpm"></param>
        public void SetBPM( int beatsPerMinute )
        {
            metronomTime = (uint)( ((1.0 / ((double)beatsPerMinute / 60.0) ) * System.Diagnostics.Stopwatch.Frequency) / (metronomBeat / segmentCount) );
        }

        /// Get or Set count on quaters per bar
        public Tackts Segments {
            get { return (Tackts)segmentCount; }
            set { if (value != Segments) {
                byte changed = (byte)value;
                metronomBeat = (byte)(metronomBeat + ((changed - segmentCount) * 4));
                segmentCount = changed;
            } }
        }

        /// Get metronome beats per bar
        public int MetronomeBeats {
            get { return metronomBeat; }
        }

        /// duration of one metronome beat as cpu ticks
        public int MetronomeTime {
            get { return (int)metronomTime; }
            set { if ( value != metronomTime ) {
                double change = (double)value / metronomTime;
                SegmentTicks = (int)(change *segmentTicks);
            } }
        }

        /// cpu ticks duration of a quater note
        public int SegmentTicks {
            get { return (int)segmentTicks; }
            set { if (value != segmentTicks) {
                double change = segmentTicks / (double)value;
                segmentAudio = (uint)(change * segmentAudio);
            } }
        }

        /// count on frames mastered per quater note
        public int SegmentFrames {
            get { return (int)segmentAudio; }
            set { segmentAudio = (uint)value; }
        }



        public int timeLine( uint framecount )
        {
            return (int)Math.Ceiling( ( ((float)framecount / segmentAudio) / segmentCount) );
        }
    }




    public interface ISegment
    {
        Sequence.Flags flg { get; }
        Sequence       seq { get; }
    }

    public interface ISegment<I> : ISegment where I : struct
    {
        I pos{ get; set; }
        I len{ get; }
        ISequence<ISegment<I>,I> sub(I index);
        
    }
    public interface ISequence<S> : ISegment where S : ISegment
    {

    }
    public interface ISequence<S,G> : ISegment where S : ISegment<G> where G : struct
    {
        List<G> get();
    }

    public interface ISegment<G,S,I> : ISegment<G> where G : struct where S : ISequence<ISegment<I>,I> where I : struct
    {
        S seq { get; }
        I idx { get; set; }
    }

    public interface ISquesnce<G,S,I> : ISegment<G> where S : ISegment<G,ISequence<ISegment<I>,I>,I> where G : struct where I : struct
    {

    }




    public class Sequence : Element, ISequence<ISegment<UInt16>,UInt16>
    {
        [Flags]
        public enum Props : ushort {
            Plain=0, Position=0x1, Loop=0x2, Start=0x4, Tempo=0x8, Stop=0x10,
            Segments=0x20, Control=0x40, FlowControl=Loop|Start|Stop|Tempo|Control,
            Seekable=Position|Control, GrainControl=Segments|Control, SnapTo=0x80,
        }
        public enum Grain : byte
        {
            Plan = 0,
            CPUTicks = TypeCode.UInt64,
            Timecode = TypeCode.Float64,
            ByteCount = TypeCode.Int32,
            FrameCount = TypeCode.UInt32,
            NullFrames = TypeCode.UInt24,
            Metronome = TypeCode.Byte,
            Segments = TypeCode.Int16
        }

        [StructLayout(LayoutKind.Explicit,Size = 4)]
        public struct Flags
        {
            [FieldOffset(0)] private uint value;
            [FieldOffset(0)] public Grain grain;
            [FieldOffset(1)] public Props props;
            [FieldOffset(2)] public Grain relat;
            [FieldOffset(3)] private byte depth;

            public Grain SnapTo {
                get{ return props.HasFlag( Props.SnapTo )
                          ? relat : Grain.Plan; }
                set{ if(value == Grain.Plan)
                        props &= Props.SnapTo;
                   else props |= Props.SnapTo;
                        relat = value; }
            }

            public void AddFlags(Flags add)
            {
                value |= add.value;
            }

            public void RemFlags(Flags rem)
            {
                value &= rem.value;
            }

            public bool AnyFlags(Flags any)
            {
                return (value & any.value) != 0; 
            }

            public bool HasFlags(Flags all)
            {
                return (value & all.value) == all.value;
            }

            public Flags(Props properties,Grain resolution,Grain relation) {
                value = 0;
                grain = resolution;
                props = properties;
                relat = relation;
                depth = 0;
            }
            public Flags(Props prop) : this(prop,Grain.FrameCount,Grain.CPUTicks) {}
            public Flags(Grain segmentation, Grain granularity)
                : this( Props.Position|Props.Segments, segmentation, granularity ) {}
        }

        public Flags              flg;
        public UInt16             pos;
        public tuctdef            bar;
        public AudioStreamBuffer  src;
        public ISegment<ushort>[] elm;

        public override Element Init( Element attach, object[] initializations )
        {
            src = initializations[0] as AudioStreamBuffer;
            bar = new tuctdef(tuctdef.Tackts.FourQuaters, 136, 44100);
            elm = new ISegment<ushort>[ bar.timeLine( src.FrameCount ) ];
            
            return Init( attach );
        }

        public ushort len { get{ return (ushort)elm.Length; } }

        Flags ISegment.flg {get { return flg; } }

        public Sequence seq { get { return this; } }

        public List<ushort> get()
        {
            List<ushort> list = sub(pos).get();
            list.Add(pos);
            return list;
        }

        public ISequence<ISegment<ushort>,ushort> sub(ushort index)
        {
            return elm[index] as ISequence<ISegment<ushort>, ushort>;
        }
    }



    public class Sequence<S,G> : Element where S : Segment<G> where G : struct
    {

        public G pos { get; }
    }

    public class Segment<G> : Element, ISegment<G> where G : struct
    {
        public Sequence.Flags flg { get {
            return (attached as Sequence).flg; 
        } }

        public Segment() {
        }

        virtual public G pos { get{ return new G(); } }
        virtual public G len { get{ return new G(); } }

        G ISegment<G>.pos { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }

        public Sequence seq {  get { throw new NotImplementedException(); } }

        public ISequence<ISegment<G>, G> sub(G index)
        {
            throw new NotImplementedException();
        }
    }

    /*
    public class TrackLength : Segment
    {
        protected IAudioInStream source;

        public TrackLength()
        {
            source = null;
        }

        public override Element Init( Element attach, object[] initializations )
        {
            source = initializations[0] as IAudioInStream;
            return base.Init( attach );
        }

        public override uint framecount { 
            get { return (source as Audio).FrameCount; }
        }
        public override uint position {
            get{ return source.GetPosition(source.GetDirection()); }
        }
        public override bool looping {
            get { return (source as Audio).Chunk.Current == (source as Audio).Chunk.Next(); }
        }
    }

    public class TrackPosition : Segment
    {
        protected Audio source;

        public TrackLength()
        {
            source = new AudioBuffer(0);
        }

        public override uint framecount { 
            get { return source.FrameCount; }
        }
    }
    */
}

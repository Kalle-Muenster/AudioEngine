using System.Collections.Generic;
using Stepflow.Audio.FileIO;
#if X86_64
using Preci = System.Double;
using ControlledPreci = Stepflow.Controlled.Float64;
using MonoPreciFrame = Stepflow.Audio.FrameTypes.AuPCMf64bit1ch;
#elif X86_32
using Preci = System.Single;
using MonoPreciFrame = Stepflow.Audio.FrameTypes.AuPCMf32bit1ch;
#endif

namespace Stepflow.Audio.Elements
{
    public interface ITrack : IElement<ElementarTrack>
    {
        ITrack track();
        void MixInto(ref IAudioFrame frame, float drywet);
        void FillFrame(ref IAudioFrame frame);
        IAudioFrame PullFrame();
        void PushFrame(IAudioOutStream into);
        AudioFrameType frameType();
        uint           sampleRate();

        Preci    Level { get; set; }
        Panorama Panorama { get; set; }
        string   Name { get; set; }
        int      Number { get; }
    }

    public class Track : Element
    {
        // dummy implementation of the ITrack mixing interface
        // to make possible 'Track' not to be handled abstract
        public virtual IAudioFrame PullFrame() { return null; }
        public virtual void MixInto( ref IAudioFrame frame, float drywet ) {}
        public virtual void PushFrame( IAudioOutStream into ) {}
        public virtual void FillFrame( ref IAudioFrame frame ) {}

        public virtual MasterTrack master
        {
            get { ITrack check = track();
                if ( check is MasterTrack ) {
                    return check as MasterTrack;
               } if( check is MixTrack ) { 
                    MasterTrack create = new MasterTrack( Find<Elementar<PcmFormat>>().entity );
                    create.AddTrack( check as MixTrack );
                    return create;
                } else MessageLogger.logErrorSchlimm(
                    "cannot directly render tracks of type '{0}'", check.GetType() );
                return null;
            }
        }

        public int AddInsertEffect<FX>( FX instance ) where FX : InsertEffect
        {
            if( !elements.ContainsKey( typeof(List<InsertEffect>) ) ) {
                if( elements.ContainsKey( typeof(InsertEffect) ) ) {
                    elements.Add( typeof(List<InsertEffect>), new List<InsertEffect>(2){ elements[typeof(InsertEffect)] as InsertEffect } );
                    elements.Remove( typeof(InsertEffect) );
                } else {
                    elements.Add( typeof(List<InsertEffect>), new List<InsertEffect>(1) );
                }
            } List<InsertEffect> list = elements[typeof(List<InsertEffect>)] as List<InsertEffect>;
            int idx = list.Count;
            list.Add( instance );
            return idx;
        }

        protected void DoInsertsChain( ref IAudioFrame currentFrame )
        {
            if( Num<InsertEffect>() > 0 ) {
                IEnumerator<InsertEffect> enamurator = All<InsertEffect>();
                while( enamurator.MoveNext() ) {
                    enamurator.Current.ApplyOn( ref currentFrame );
                }
            }
        }
    }

    public abstract class ElementarTrack : Track, ITrack
    {
        public const int AMP = 0;
        public const int PAN = 0;

        virtual public ElementarTrack element() { return this; }

        protected ModulationParameter _amp = null;
        public ModulationParameter amp {
            get { return _amp; }
            set { if ( value != _amp ) { 
                    _amp = Set<ModulationParameter>( AMP, value );
                } 
            }
        }

        protected PanoramaParameter _pan = null;
        public PanoramaParameter pan {
            get { return _pan; }
            set { if( value != _pan ) {
                    _pan = Set<PanoramaParameter>( PAN, value );
                }
            }
        }

        public ElementarTrack() : base() {
            amp = new ModulationParameter();
            pan = new PanoramaParameter();
        }

        // maybe named property can be removed if turns out never used ( due to calling Get<ElementName>() individually always would be possible anyway)
        virtual public string Name {
            get { return Has<ElementName>() ? Get<ElementName>() : track().Number.ToString(); }
            set { if ( Has<ElementName>() ) Get<ElementName>().entity = value;
                  else Add<ElementName>( value );
            }
        }

        virtual public int Number {
            get { return Idx<Track>( this ); }
        } 

        public override ITrack track() { return this; }

        public abstract AudioFrameType frameType();
        public abstract uint          sampleRate();
        protected abstract IAudioFrame pullFrame();
        public override IAudioFrame PullFrame() {
            IAudioFrame nextframe = pullFrame();
            if ( Has<InsertEffect>() ) DoInsertsChain( ref nextframe );
            nextframe = nextframe.Pan( pan ).Amp( (float)amp.actual );

            // TODO try to get these moved into the Mixtrack.update()
            pan.Update(!phasys);
            amp.Update(!phasys);
            if (Has<LFO>())
                UpdateAll<LFO>(!phasys);
            if (Has<EVP>())
                UpdateAll<EVP>(!phasys);


            return nextframe;
        }



        public virtual void AddLFO(PARAMETER target, Preci min, Preci max, Preci frq, ControlMode form, uint samplerate)
        {
            Add<LFO>( target, min, max, frq, form, samplerate );
        }
        public virtual void AddEnvelop( PARAMETER target, Preci[][] ADSR, uint length )
        {
            Add<EVP>( target, ADSR, length );
        }

        public Preci Level { get { return amp.actual; } set { amp.actual = value; } }
        public Panorama Panorama { get { return pan.actual; } set { pan.actual = value; } }

    }

    public class MixTrack : ElementarTrack
    {
        protected IAudioFrame         current;
        protected uint                samplerate;

        public override MasterTrack master {
            get{ return render as MasterTrack; }
        }

        public override AudioFrameType frameType() {
            return current.FrameType;
        }
        public override uint sampleRate() {
            return samplerate;
        }
        public void SetMaster( MasterTrack mixer )
        {
            attached = mixer;
        }
        virtual public uint FrameCount { get { return Get<ElementLength>().frames; } }
        public void DoSubTracksMix( ref IAudioFrame target )
        {
            IEnumerator<MixTrack> it = All<MixTrack>();
            while( it.MoveNext() ) {
                it.Current.MixInto( ref target, 0.5f );
            } 
        }
        //protected override void update()
        //{
        //    base.update();
        //    if (Has<LFO>())
        //        UpdateAll<LFO>(phasys);
        //    if (Has<EVP>())
        //        UpdateAll<EVP>(phasys);
        //}

        protected override IAudioFrame pullFrame() {
            if ( Has<Elementar<IAudioInStream>>() )
                current = GetElementar<IAudioInStream>().ReadFrame();
            else current.Clear();
            return current;
        }

        public PcmFormat GetTargetFormat()
        {
            if( current == null )
                current = master.GetTargetFormat().CreateEmptyFrame();
            return current.FrameType.CreateFormatStruct( (int)samplerate );
        }

        virtual public void SetTargetFormat( PcmFormat format )
        {
            current = format.CreateEmptyFrame();
            samplerate = format.SampleRate;
        }

        public int AddInsertEffect<FX>(params object[] initparams) where FX : InsertEffect, new()
        {
            if( initparams.Length == 0 ) {
                initparams = new object[] { Find<Elementar<PcmFormat>>().entity };
            }
            int insertidx = Num<InsertEffect>();
            Add<InsertEffect, FX>(initparams);
            return insertidx;
        }

        public PcmFormat TargetFormat
        {
            get{ return GetTargetFormat(); }
            set{ SetTargetFormat(value); }
        }

        public MixTrack() : base()
        {
            amp = new ModulationValue();
            pan = new PanoramaValue();
        }

        public FXtype Fx<FXtype>() where FXtype : InsertEffect, new()
        {
            FXtype f;
            if ( ( f = Get<InsertEffect,FXtype>() ) == null ) {
                f = Add<InsertEffect,FXtype>( frameType(), sampleRate() );
            } return f;
        }
    }

    public interface ITrackMixer
    {
    //    ModulationValue     volume { get; }
        ElementLength       length { get; }
        int                 tracks { get; }
        PcmFormat           format { get; set; }
        MixTrack GetTrack( int trackIdx );
        int AddTrack( MixTrack track );
        MixTrack this[int track] { get; set; }
        void Update();
    }

    public class ElementCost : Element
    {
        private System.Diagnostics.Stopwatch timer;
        private ulong                        value;
        private Preci                        frame;

        public ElementCost() : base() {
            timer = new System.Diagnostics.Stopwatch();
            value = 0;
        }

        public override Element Init( Element attach, params object[] inits )
        {
            frame = (Preci)System.Diagnostics.Stopwatch.Frequency / (Preci)inits[0];
            return base.Init( attach );
        }

        public void start() {
            timer.Start();
        }

        public void stop() {
            value = (uint)timer.ElapsedTicks;
            timer.Stop();
        }

        protected override void update() {
            if (timer.IsRunning) stop();
        }

        public Preci perFrame() {
           return (Preci)value / frame;
        }

    }

    public class ElementName : Elementar<string>
    {
        public enum Scope : byte {
            Dominant, Recessive
        }
        public Scope behave;

        public ElementName() { entity = ""; behave = Scope.Recessive; }
        public ElementName( Element named ) : base( named ) { }
    }

    public class MasterTrack : MixTrack, ITrackMixer
    {
        private PROGRESS              action;
        public  Effectroutes          routes;
        public  IAudioStream          output;
        private Elementar<PcmFormat> _format;
        private BarrierFlags          inputs;
        private ElementLength         length;
        
        

        public bool istime {
            get { return Get<ElementCost>().perFrame() < 0.1; }
        }

        public PcmFormat format {
            get { return _format.entity; }
            set { _format.entity = value; }
        }

        ElementLength  ITrackMixer.length { get { return length; } }
        public int                 tracks { get { return Num<MixTrack>(); } }
        public bool                active { get { return action.HasFlag(PROGRESS.Play); } }

        public MasterTrack( PcmFormat inputformat )
        {
            action = PROGRESS.Init;
           _format = Add<Elementar<PcmFormat>>();
            format = inputformat;
            inputs = Add<BarrierFlags>();
            inputs.Clear = false;
            length = Add<ElementLength>( 0u );
        }

        public MixTrack this[int trackIdx]
        {
            get { return Get<MixTrack>( trackIdx ); }
            set { Set<MixTrack>( trackIdx, value ); }
        }

        public MixTrack GetTrack( int trackIdx )
        {
            return Get<MixTrack>( trackIdx );
        }

        public int AddTrack( MixTrack newTrack )
        {
            newTrack.TargetFormat = format;
            int trackCount = Num<MixTrack>();
            newTrack.SetMaster( this );
            Add( newTrack );
            inputs.Clear = false;
            return trackCount;
        }

        public void DetachTracks()
        {
            int trackCount = Num<MixTrack>();
            for( int i = 0; i < trackCount; ++i ) {
                Rem<MixTrack>(i);
            } inputs.Clear = false;
        }

        public void Enable()
        {
            if( output != null ) {
                Init( this, output );
            } else {
                Init( this, TargetFormat );
            }
        }

        public int AddSendEffect<FX>( params object[] parameters ) where FX : SendEffect, new()
        {
            if ( inputs.Clear ) {
                return routes.AddSendEffect<FX>( parameters );
            } else {
                MessageLogger.logInfoWichtig( "Adding send FX route is disabled until master initialized" );
                return -1;
            }
        }

        public SendFX GetSendEffect<SendFX>() where SendFX : SendEffect, new()
        {
            return routes.Get<SendEffect,SendFX>();
        }

        public override Element Init( Element attach, params object[] initializations )
        { 
            if( initializations[0] is IAudioStream ) {
                output = initializations[0] as IAudioStream;
                PcmFormat setFormat = output.GetFormat();
                TargetFormat = setFormat;
            } else {
                output = null;
                PcmFormat setFormat = (PcmFormat)initializations[0];
                TargetFormat = setFormat;
            } current = format.CreateEmptyFrame();
            if ( Has<Effectroutes>() ) {
                routes = Get<Effectroutes>();
            } else {
                routes = Add<Effectroutes>();
            }
            IEnumerator<MixTrack> trks = All<MixTrack>();
            while( trks.MoveNext() ) {
                length.frames = trks.Current.FrameCount > length.frames
                              ? trks.Current.FrameCount : length.frames; 
            }
            inputs.Clear = true;
            return Init( this );
        }

        public void AttachOutputStream( IAudioStream audiostream )
        {
            Init( this, audiostream );
        }

        public override Element Init( Element attach )
        {
            if( attach == null || attach == this ) {
                attached = this;
            } return attached;
        }

        public void DoRouteReturns( ref IAudioFrame target )
        {
            if( routes.Has<SendEffect>() ) {
                routes.DoEffectReturn( ref target );
            }
        }

        public override IAudioFrame PullFrame()
        {
            action |= PROGRESS.Play;
            if (Has<ElementCost>()) Get<ElementCost>().start();
                    IAudioFrame mixmax = pullFrame();
            DoSubTracksMix( ref mixmax );
            DoRouteReturns( ref mixmax );
            DoInsertsChain( ref mixmax );
            if ( output == null )
                Update( phasys );
            return current;
        }

        public override void SetTargetFormat( PcmFormat format )
        {
            base.SetTargetFormat( format );
            if ( format.NumChannels > 4 ) {
                if ( !Has<PartialChannelFilter>() )
                    Add<InsertEffect,PartialChannelFilter>( current.FrameType, samplerate );
            } else if( Has<PartialChannelFilter>() ) {
                Rem<InsertEffect>( Idx<InsertEffect>( Get<PartialChannelFilter>() ) );
            }
        }

        protected override void update()
        {
            if( output != null )
            if( output.Direction.In( StreamDirection.OUTPUT ) )
              ( output as IAudioOutStream ).WriteFrame( PullFrame() );
            if( Has<ElementCost>() ) Get<ElementCost>().stop();
        }

        public void Update()
        {
            Update( phasys );
        }
    }

    public class AudioSource : MixTrack
    {
        public new const uint ElementCode = 4478273;
        public IAudioInStream    src;
        protected AudioFrameType typ;
        private bool converse;
        public override uint GetElementCode()
        {
            return ElementCode;
        }
        public override uint FrameCount
        {
            get { return src.AvailableFrames(); }
        }

        public void SetEnvelop( PARAMETER usageArg, Preci[][] ADSRparameter, uint envelopLength )
        {
            Add<EVP>( usageArg, ADSRparameter, envelopLength );
        }
        public override Element Init( Element attach, object[] parameter )
        {
            src = null;
            if ( FILE.LoadOnBeginn == (FILE)parameter[1] ) {
                WaveFileReader loader = new WaveFileReader( parameter[0] as string );
                src = new AudioStreamBuffer( loader.ReadAll() );
            } else {
                if ( parameter[0] != null ) {
                    if ( parameter[0] is string )  {
                        src = new WaveFileReader(parameter[0] as string);
                    } else if (parameter[0] is Consola.StdStreams) {
                        src = new AudioFromStdIn( parameter[0] as Consola.StdStreams );
                    }
                }
            }
            if ( parameter.Length >= 3 ) {
                TargetFormat = (PcmFormat)parameter[2];
            } else TargetFormat = src.GetFormat();
            return Init( attach );
        }

        public override void SetTargetFormat( PcmFormat fmt )
        {
            current = fmt.CreateEmptyFrame();
            typ = fmt.FrameType;
            if ( src.GetFrameType().Code != typ.Code )
                 converse = true;
            else converse = false;
        }

        public override void MixInto( ref IAudioFrame frame, float drywet )
        {
            frame.Mix( PullFrame(), drywet );
        }

        protected override IAudioFrame pullFrame()
        {
            current = converse ? src.ReadFrame().Convert( typ )
                               : src.ReadFrame();
            if (Has<MixTrack>()) DoSubTracksMix( ref current );
            current.Amp( amp );
            current.Pan( pan );
            if (Has<InsertEffect>()) DoInsertsChain( ref current );
            Update( phasys );
            return current;
        }

        public override void PushFrame( IAudioOutStream into )
        {
            into.WriteFrame( PullFrame() );
        }

        protected override void update()
        {
            pan.Update( phasys );
            amp.Update( phasys );
            if( Has<LFO>() ) {
                UpdateAll<LFO>( phasys );
            }
        }
    }


}

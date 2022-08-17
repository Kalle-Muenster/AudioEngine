using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using IFrame = Stepflow.Audio.IAudioFrame;
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
    [Flags]
    public enum EffectMode : uint
    {
        Insert = 0x00000000,
        SendFx = 0x80000000,
        MASKEN = 0x80000000
    }

    [Flags]
    public enum EffectType : ushort
    {
        NoType     = 0x0000,
        Additiv    = 0x0001,
        Subtractiv = 0x0002,
        Delay      = 0x0101,
        Reverb     = 0x0201,
        Filter     = 0x0402,
        Fatten     = 0x0801,
        Master     = 0x1000,
        Dynamics   = 0x2002,
        Voice      = 0x4000,
        Surround   = 0x8000,
        MASKEN     = 0x7f00
    }

    public interface IEffect
    {
        Preci Level { get; set; }
        bool ByPass { get; set; }
        IFrame DoFrame( IFrame input );
        void ApplyOn( ref IFrame output );
        AudioFrameType frameType();
        uint           sampleRate();
    }

    public class Effect : Element, IEffect
    {
        public new const uint ElementCode = 0x00ff0000;
        internal protected EffectMode FxMode;
        internal protected EffectType FxType;

        virtual public bool ByPass { get; set; }

        protected ModulationValue mix;

        public virtual string Name { get { return string.Format( "{0} {1}", FxMode, FxType ); } }
        public virtual IFrame DoFrame( IFrame frame ) { return frame; }
        public virtual void   ApplyOn( ref IFrame frame ) {}

        public virtual List<IParameter<Preci>> GetParameters() {
            return new List<IParameter<Preci>>(0);
        }

        public override uint GetElementCode() {
            return (uint)FxMode | (uint)FxType | ElementCode;
        }

        public Effect() : base() {
            FxType = EffectType.NoType;
        }

        public Preci Level {
            get { return mix; }
            set { mix.value = value; }
        }

        AudioFrameType IEffect.frameType() { return track().element().frameType(); }
        uint IEffect.sampleRate() { return track().element().sampleRate(); }
    }

    public class SendEffect<FX> : SendEffect where FX : FxPlug
    {
        protected FX impl;

        public SendEffect() {
            FxMode = EffectMode.SendFx;
            mix = new ModulationValue().Init( this, PARAMETER.FxSend ) as ModulationValue;
            mix.value = 0.5f;
        }

        public override Element Init( Element attach, params object[] initializations ) {
            return impl.Init( attach, initializations );
        }

        public override string Name {
            get { return string.Format("{0} {1} {2}", impl.Name, FxType, FxMode); }
        }

        /// <summary>
        /// On a FxPlug.SendEffect instance, DoFrame(frame) regularly is called by the effect instance
        /// itself, subsequentially to ApplyOn(frame) calls done by the mixer where it's attached to.
        /// So it shouldn't be called from any other elements then the SendEffect itself and should
        /// not be overridden by SendEffect implementations 
        /// </summary>
        /// <param name="frame"></param>
        /// <returns></returns>
        public sealed override IFrame DoFrame( IFrame frame ) {
            return impl.DoFrame( frame ).Amp( Return ).Pan( Panorama );
        }

        /// <summary>
        /// On a FxPlug.SendEffect instance, ApplyOn(frame) regularly is called by that mixer where it 
        /// is attached to one of thats fx return routes. so it in most cases will receive as 
        /// 'frame' parameter that mixers actuall master frame currently in render progress.
        /// </summary><param name="frame"></param>
        public sealed override void ApplyOn( ref IFrame frame ) {
            //if (!ByPass) frame.Add( impl.DoFrame( insent ).Amp( Return ).Pan( Panorama ) );
            if (!ByPass) frame.Add( DoFrame( insent ) );
        }

        public FX fxImpl() { return impl; }

        public override bool ByPass {
            get { return impl.ByPass; }
            set { impl.ByPass = value; }
        }
    }

    public class InsertEffect<FX> : InsertEffect where FX : FxPlug
    {
        protected FX impl;

        public InsertEffect() {
            FxMode = EffectMode.Insert;
            mix = new ModulationValue().Init( this, PARAMETER.FxSend ) as ModulationValue;
            mix.value = 0;
        }

        public override Element Init( Element attach, params object[] initializations ) {
            return impl.Init( attach, initializations );
        }

        public override string Name {
            get { return string.Format("{0} {1} {2}", impl.Name, FxType, FxMode); }
        }

        public override IFrame DoFrame( IFrame frame ) {
            impl.output.Set( frame );
            return impl.DoFrame( impl.output.Amp( mix ) );
        }

        public override void ApplyOn( ref IFrame frame ) {
            if (!ByPass) frame.Add( DoFrame( frame ) );
        }

        public FX fxImpl() { return impl; }

        public override bool ByPass {
            get { return impl.ByPass; }
            set { impl.ByPass = value; }
        }
    }

    /// <summary> FxPlug (abstract base class for effect instance which supports being 'Insert' fx
    /// and also supports being 'Send' effect) for supporting both automaticly, the fx instance
    /// need to provide two slightly different implementations for receiving and returning audio
    /// frames from the tracks where attached to. (this is done by inheriting from different base
    /// classes where these derive from individually) So FxPlug class implementations provide code
    /// which both effect types can use for calculating effect same way. the two variants therefore
    /// won't need to implement anything anymore due to the track interfaces already are implement
    /// within the two different base classes they're derived from </summary>
    public abstract class FxPlug : IEffect
    {
        abstract public IElmPtr<Preci> this[int parameter] { get; set; }
        public string Name {
            get { return nam;  }
            set { if ( value != nam ) { elm.Get<ElementName>().entity = nam = value; } }
        }

        abstract public Array Parameters { get; }

        protected Effect elm;
        protected string nam;
        public    IFrame output;
        
        public Effect fxImpl()
        {
            return elm;
        }


        public FxPlug( Effect element ) {
            elm = element;
        }


        abstract public Element Init( Element attach, params object[] initialize );
        abstract public IFrame  DoFrame( IFrame frame );
        abstract public void    ApplyOn( ref IFrame frame );
        abstract public bool    ByPass { get; set; }
        public Preci Level  {
            get { return elm.Level; }
            set { elm.Level = value; }
        }

        public AudioFrameType frameType() { return output.FrameType; }
        uint IEffect.sampleRate() { return elm.track().element().sampleRate(); }

    }




    /// <summary>
    /// InsertEffect - base class for component elements which when attached to a track element, will be
    /// processed by the track during rendering as part of it's insert chain. in a track's fx chain, each
    /// InsertFX element will make the track to provide an individual additional dry/wet parameter for it
    /// </summary>
    public interface IInsert
    {
        IInsert         fxInsrt();
        ModulationValue DryWet { get; set; }
    }
    public class InsertEffect : Effect, IInsert
    {
        public virtual ModulationValue DryWet {
            get { return mix; }
            set { if (value != mix)
                 mix = Set<ModulationParameter>( 0, value ) as ModulationValue;
            }
        }
        //public override bool ByPass { get; set; }
        public IInsert fxInsrt() { return this; }
    }




    /// <summary> SendEffect - base class for elements which when attached to a track will gain their input signals
    /// not from that track where attached to, but instead from all these input tracks which also are attached to the track.
    /// So SendEffect won't cause effect directly to theses input tracks where signal comes from, but instead causes effect
    /// to the output track where the SendEffect is attached to. so effected frame is mixed sum of all input tracks (via
    /// adjusting dry/wet level of the coresponding send route endpoint which automatically will be inserted to each of the 
    /// tracks insert chains)... Attaching a SendEffect to a track so enables that track can adjust Return level ratio
    /// by which the fx will be mixed into it. when the track where send fx is attached to is the mater output of a Track mixer 
    /// It enables each of the input-tracks connected to that mixer to make possible each can addjust send-levels for the
    /// SendEffect then individually - and the output track of the the mixer will gain effected signal from all mixtracks
    /// adjusted by each's dry/wet parameter for that send Fx attached to these tracks master mix's output track  
    /// </summary>
    public interface ISend 
    {
        ISend           fxSend();
        ModulationValue Return { get; set; }
        Panorama        Panorama { get; set; }
        bool            ByPass { get; set; }

        void Send( IFrame frame );
        void Clear();
    }
    public class SendEffect : Effect, ISend
    {
        protected IFrame insent;

        //public virtual bool ByPass { get; set; }
        public override Element Init( Element attach )
        {
            PcmFormat format = attach.render.master.output.GetFormat();
            insent = format.CreateEmptyFrame();
            Return.Add<PanoramaParameter>();
            return base.Init( attach );
        }

        public override Element Init( Element attach, params object[] inits )
        {
            return Init( attach );
        }

        public ISend fxSend() { return this; }


        public virtual ModulationValue Return {
            get { return mix; }
            set { if ( value != mix ) {
                    PanoramaParameter pan = mix.Get<PanoramaParameter>();
                    pan.Init( value );
                    value.Add( pan );
                    mix = Set<ModulationParameter>(0, value) as ModulationValue;
                }
            }
        }
        public Panorama Panorama
        {
            get { return mix.Get<PanoramaParameter>(); }
            set { mix.Get<PanoramaParameter>().actual = value; }
        }

        public void Send( IFrame frame )
        {
            insent.Add( frame );
        }

        void ISend.Clear()
        {
            insent.Clear();
        }
    }

    



    /// <summary> TrackEffectSend-Endpoint (entry-point - InsertEffect derived)
    /// Defines send endpoints which are entry points to the (master attached) fx send routes
    /// to be handled (by that track where this enpoint is placed on) within
    /// its insert chain but with not causing effect to the track itself but
    /// to the output mixers master track instead (the where this is attached 
    /// won't know about that this thingy indeed is a send route entrypoint
    /// and not a 'regular' insert fx... but the track will recognize it as
    /// such an insert FX which it got to procceed during its render cycle.
    /// it will write itselfs current frame into it (output variable) but 
    /// without receivig any effected changes then froom this like it would
    /// gain when it's processing other regular insert fx's )</summary>
    /// <parameters><attachto> 0: MixTrack Element </attachto></parameter>
    public class EffectSend<SendFx> : InsertEffect, IEffect where SendFx : SendEffect
    {
        public const int         SNT = 0; 
        protected int            returnRoute;        
        public override string   Name { get { return fxinst().Name; } }
        protected uint           rate;
        protected AudioFrameType type;
        // output into the send route whee this endpoint (entrypoint from this point of view) belongs to / leads to 
        private IFrame           output;

        public EffectSend() : base()
        {
            returnRoute = -1;
            FxMode = EffectMode.SendFx;
            mix = Get<ModulationValue>();
        }

        public override Element Init( Element attach, params object[] initArgs )
        {
            Init( attach );  
            attached.Add<ModulationParameter>( DryWet.Init( attached, PARAMETER.FxSend, NULL ) );
            returnRoute = (int)initArgs[0];
            PcmFormat format = render.master.format;
            rate = format.SampleRate;
            type = format.FrameType;
            FxType = (EffectType)(fxinst().GetElementCode() & 0x0000ff00);
            output = format.CreateEmptyFrame();
            return this;
        }

        public Preci Send {
            get { return DryWet; }
            set { DryWet.actual = value; }
        }

        public override ITrack track()
        {
            return attached as MixTrack;
        }
        protected SendFx fxinst()
        {
            return track().element().render.master.Get<Effectroutes>().Get<SendEffect>( returnRoute ) as SendFx;
        }

        public override void ApplyOn( ref IFrame frame )
        {
            if( !ByPass ) fxinst().Send( DoFrame( frame ) );
        }

        public override IFrame DoFrame( IFrame frame )
        {
            output.Set( frame );
            return output.Amp( mix ).Convert( type );
        }
    }

    /// <summary> EffectRoute (ElementarTrack Element)
    /// Defines an fx chain which provides individual send and return endpoints
    /// which can be placed to different track elements each independantly</summary>
    public class Effectroutes : ElementarTrack
    {
        public new const uint ElementCode = 4543570;
        public override uint GetElementCode() { return ElementCode; }

        public override MasterTrack master { get{ return render as MasterTrack; } }
        private IFrame              fxsumm;

        ///<summary>
        ///<param name="attachto">
        /// 0: MasterTrack (or any other MasterTrack derived mixer element)
        ///</param>
        ///</summary>
        public override Element Init( Element attach )
        {
            if( attach is MasterTrack ) {
                base.Init( attach );
                fxsumm = Find<Elementar<PcmFormat>>().entity.CreateEmptyFrame();
            } else throw new Exception( "attach to MasterTrack" );
            return this;
        }

        protected override IFrame pullFrame()
        {
            fxsumm.Clear();
            if ( Has<SendEffect>() ) {
                IEnumerator<SendEffect> fx = All<SendEffect>();
                while ( fx.MoveNext() ) fx.Current.ApplyOn( ref fxsumm );
            } return fxsumm;
        }

        public void DoEffectReturn( ref IFrame targetFrame )
        {
            targetFrame.Add( pullFrame() );
            ForAll( (SendEffect route) => {
                route.fxSend().Clear(); 
            } );
        }

        public int AddSendEffect<FX>( params object[] args ) where FX : SendEffect, new()
        {
            int fxCount = Num<SendEffect>();
            Add<SendEffect,FX>( args[0], args[1] );
            for( int i=0; i < master.tracks; ++i ) {
                master[i].Add<InsertEffect,EffectSend<FX>>( fxCount );
            } return fxCount;
        }

        public override AudioFrameType frameType()
        {
            return fxsumm.FrameType;
        }

        public override uint sampleRate()
        {
            return master.format.SampleRate;
        }
    }
}

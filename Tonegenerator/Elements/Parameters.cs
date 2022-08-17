using System;
using System.Collections.Generic;
using Stepflow.Controller;
using Stepflow.Audio.FrameTypes;
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


    public interface IParameter<T>
    {
        T actual { get; set; }
        T value { get; set; }
        IElmPtr<T> elmptr();

        string   Name { get; set; }
        MODULATOR type { get; }
        PARAMETER usage { get; }
        IParameter<T> elementar();
        void Update( bool phase );
    }

    public class ModulationFrame : Element, IParameter<AuPCMf32bit4ch>
    {
        public AuPCMf32bit4ch frame;
        public ModFrame value;

        public string Name {
            get { return Has<ElementName>() 
                       ? Get<ElementName>().entity
                       : string.Empty; }
            set { if( Has<ElementName>() ) {
                    Get<ElementName>().entity = value;
                } else { Add<ElementName>( value ); }
            }
        }

        AuPCMf32bit4ch IParameter<AuPCMf32bit4ch>.value { get { return frame; } set { frame = value; } }
        public AuPCMf32bit4ch actual { get { return frame; } set { frame = value; } }

        public MODULATOR type { get; set; }

        public PARAMETER usage { get; set; }

        IElmPtr<AuPCMf32bit4ch> IParameter<AuPCMf32bit4ch>.elmptr() {
            unsafe { fixed (void* p = &frame) { return Add<ElmPtr<AuPCMf32bit4ch>>(new IntPtr(p)); } }
        }

        public IParameter<AuPCMf32bit4ch> elementar()
        {
            return this;
        }
        public static implicit operator AuPCMf32bit4ch(ModulationFrame cast)
        {
            return cast.frame;
        }

        public static implicit operator ElmPtr<AuPCMf32bit4ch>(ModulationFrame cast)
        {
            return cast.elementar().elmptr() as ElmPtr<AuPCMf32bit4ch>;
        }

        public class ModFrame : Controlled.Float32
        {
            private float modeFunction( ref float FLeft, ref float FRight, ref float RLeft, ref float RRight )
            {
                float dazwichen = (FLeft + FRight) * 0.25f;
                FRight = (FLeft - dazwichen);
                FLeft = dazwichen;
                return dazwichen;
            }

            public ModFrame( ref AuPCMf32bit4ch frame ) : base()
            {
                AttachedDelegate += modeFunction;
                LetPoint( ControllerVariable.VAL, frame.GetPointer(0) );
                LetPoint( ControllerVariable.MIN, frame.GetPointer(1) );
                LetPoint( ControllerVariable.MAX, frame.GetPointer(2) );
                LetPoint( ControllerVariable.MOV, frame.GetPointer(3) );
            }
        }

        public ModulationFrame() : base()
        {
            frame = new AuPCMf32bit4ch();
            value = new ModFrame( ref frame );
            value.SetUp( frame[0], frame[1], frame[2], frame[3], ControlMode.None );
        }

    }

    public class ModulationParameter : Element, IParameter<Preci>
    {
        public new const uint ElementCode = (uint)Constants.PreciTypeCode;

        public MODULATOR type = MODULATOR.Constant;
        public PARAMETER usage = PARAMETER.None;

        public override uint GetElementCode() {
            return ElementCode;
        }

        public string Name {
            get { return Has<ElementName>()
                       ? Get<ElementName>().entity
                       : string.Empty; }
            set { if( Has<ElementName>() ) {
                    Get<ElementName>().entity = value;
                } else { Add<ElementName>( value ); }
            }
        }

        virtual public IParameter<Preci> elementar() { return this; } 
        virtual public Preci actual { get { return GANZ; } set { elementar().value = value; } }
        Preci IParameter<Preci>.value { get { return GetElementar<Preci>(); } set { Get<Elementar<Preci>>().entity = value; } }
        virtual public IElmPtr<Preci> elmptr() {
            return Has<ElmPtr>() ? Get<ElmPtr>() : Add<ElmPtr>();
        }
        MODULATOR IParameter<Preci>.type {get { return type; } }
        PARAMETER IParameter<Preci>.usage { get { return usage; } }
        public static implicit operator ElmPtr( ModulationParameter cast ) {
            return cast.elementar().elmptr() as ElmPtr;
        }
        public static implicit operator Preci( ModulationParameter cast ) {
            return cast.actual;
        }
#if X86_64
        public static implicit operator float( ModulationParameter cast ) {
            return (float)cast.actual;
        }
#endif
    }
    public class ModulationValue : ModulationParameter
    {
        public Preci value = 1;
        public override Preci actual { get { return this.value; } set { this.value = value; } }

        public ModulationValue() : base() {
            type = MODULATOR.StaticValue;
            value = 1;
        }

        public override Element Init( Element attach, params object[] initializations )
        {
            for(int i = 0; i < initializations.Length; ++i) {
                if (initializations[i] is PARAMETER)  usage = (PARAMETER)initializations[i];
                else if (initializations[i] is Preci) value = (Preci)initializations[i];
            } return Init( attach );
        }

        private unsafe IntPtr makePointer() {
            fixed (Preci* p = &value) {
                return Add<ElmPtr>( new IntPtr(p) ).entity;
            }
        }

        public override IElmPtr<Preci> elmptr()
        {
            return this is ModulationPointer ? this as ModulationPointer : Has<ElmPtr>() ? Get<ElmPtr>() : Add<ElmPtr>( makePointer() );
        }

        public static explicit operator IntPtr( ModulationValue cast )
        {
            if (!cast.Has<ElmPtr>()) return cast.makePointer();
            else return cast.elementar().elmptr().pointer;
        } 
        public static implicit operator Preci( ModulationValue cast )
        {
            return cast.value;
        }
#if X86_64
        public static implicit operator float( ModulationValue cast ) {
            return (float)cast.value;
        }
#endif
    }

    public interface IElmPtr<T> : IParameter<T>
    {
        IntPtr pointer { get; set; }
        void SetProportion( T proper );
        void SetTarget( ref T variable );
        void SetTarget( IntPtr ptr );
    }

    public unsafe class ElmPtr<T> : Elementar<IntPtr>, IElmPtr<T> where T : struct // ModulationValue //, IParameter<Preci>
    {
        public ElmPtr() : base() {
            entity = IntPtr.Zero;
        }

        public IntPtr pointer {
            get { return entity; }
            set { entity = value;
                attached.changed();
            }
        }

        public string Name {
            get { return Has<ElementName>()
                       ? Get<ElementName>().entity
                       : string.Empty; }
            set { if( Has<ElementName>() ) {
                    Get<ElementName>().entity = value;
                } else { Add<ElementName>( value ); }
            }
        }

        MODULATOR IParameter<T>.type { get { return MODULATOR.Relative; } }
        PARAMETER IParameter<T>.usage { get { return elementar().usage; } }
        public IParameter<T> elementar() { return attached as IParameter<T>; }

        public virtual void SetTarget(ref T variable) { MessageLogger.logErrorSchlimm("must implement by derivation"); }
#if X86_64
        public void SetTarget(ref float variable)
        {
            fixed (float* pt = &variable)
                pointer = new IntPtr(pt);
        }
#endif
        public void SetTarget(IntPtr ptr)
        {
            pointer = ptr;
        }

        public void SetProportion(T proper)
        {
            value = proper;
        }

        T IParameter<T>.actual { get; set; }

        virtual public IElmPtr<T> elmptr() { return Get<ElmPtr<T>>(); }

        public T value { get { return elementar().actual; } set { elementar().actual = value; } }

        public static implicit operator T(ElmPtr<T> cast)
        {
            return cast.elementar().actual;
        }

    }

    public unsafe class ElmPtr : ElmPtr<Preci>
    {

        public unsafe override void SetTarget( ref Preci variable )
        {
            fixed (Preci* pt = &variable)
                pointer = new IntPtr(pt);
        }

        public override Element Init( Element attach, object[] initializations )
        {
            ModulationParameter at;

            if (attach is ModulationParameter)
                at = attach as ModulationParameter;
            else throw new Exception("nö");

            if (!(at is ModulationValue)) {
                at.Get<Elementar<Preci>>().entity = 0;
            }

            for (int i = 0; i < initializations.Length; ++i) {
                if (initializations[i] is PARAMETER) at.usage = (PARAMETER)initializations[i];
                else if (initializations[i] is IntPtr) entity = (IntPtr)initializations[i];
                else if (initializations[i] is Preci) value = (Preci)initializations[i];
                else if ((i > 2) && (initializations[i] is float))
                    *(Preci*)pointer.ToPointer() = (Preci)(float)initializations[i];
            }
            if ( pointer == IntPtr.Zero ) {
                if (at is ModulationValue) {
                    fixed (void* p = &(at as ModulationValue).value) entity = new IntPtr(p);
                } else {
                    fixed (void* p = &at.Get<Elementar<Preci>>().entity) entity = new IntPtr(p);
                }
            } return Init( attach );
        }

        public Preci actual {
            get { return entity != IntPtr.Zero ? (value * *(float*)entity.ToPointer()) : value; }
            set { this.value = value; }
        }

        public static implicit operator Preci( ElmPtr cast ) {
            return cast.actual;
        }

#if X86_64
        public static implicit operator float(ElmPtr cast) {
            return (float)cast.elementar().actual;
        }
#endif
    }

    public class ModulationPointer : ModulationValue, IElmPtr<Preci>
    {
        public IntPtr pointer;
        public ModulationPointer() : base()
        {
            type    = MODULATOR.Relative;
            pointer = IntPtr.Zero;
            value   = GANZ;
        }

        IntPtr IElmPtr<Preci>.pointer { get { return this.pointer; } set { this.pointer = value; changed(); } }

        public override Preci actual {
            get { unsafe { return pointer != IntPtr.Zero ? (value * *(float*)pointer.ToPointer()) : value; } }
            set { this.value = value; }
        }

        public override IParameter<Preci> elementar() { return this; }

        public override Element Init(Element attach, object[] initializations)
        {
            for( int i = 0; i < initializations.Length; ++i ) unsafe {
                if (initializations[i] is PARAMETER) usage = (PARAMETER)initializations[i];
                else if (initializations[i] is IntPtr) pointer = (IntPtr)initializations[i];
                else if (initializations[i] is Preci) value = (Preci)initializations[i];
                else if ((i > 2) && (initializations[i] is float))
                    *(Preci*)pointer.ToPointer() = (Preci)(float)initializations[i];
            }

            if (pointer == IntPtr.Zero) unsafe {
                fixed (void* p = &value) pointer = new IntPtr(p);
            }

            return Init( attach );
        }

        unsafe void IElmPtr<Preci>.SetTarget(ref Preci variable)
        {
            fixed (Preci* pt = &variable)
                pointer = new IntPtr(pt);
        }

        unsafe public void SetTarget( ref float variable )
        {
            fixed (float* pt = &variable)
                pointer = new IntPtr(pt);
        }

        public void SetTarget(IntPtr ptr)
        {
            pointer = ptr;
        }

        public void SetProportion(Preci proper)
        {
            value = proper;
        }

        public static implicit operator Preci(ModulationPointer cast)
        {
            return cast.actual;
        }

#if X86_64
        public static implicit operator float(ModulationPointer cast)
        {
            return (float)cast.actual;
        }
#endif
    }

    public class PanoramaParameter :  Element, IParameter<Panorama>
    {
        public new const uint ElementCode = 5128528;
        public static readonly ModulationValue NeutralAxis = new ModulationValue().Init(null,HALB) as ModulationValue;
        
        virtual public ModulationParameter sides { get { return NeutralAxis; } set { ErrorReport( "partial value parameters", "default element instances" ); } }
        virtual public ModulationParameter front { get { return NeutralAxis; } set { ErrorReport( "partial value parameters", "default element instances" ); } }


        public MODULATOR type = MODULATOR.Constant;
        public PARAMETER usage = PARAMETER.Panorama;

        public override uint GetElementCode() {
            return ElementCode;
        }

        public string Name {
            get { return Has<ElementName>() ? Get<ElementName>() : track().Name + ".Panorama"; }
            set { if( Has<ElementName>() ) {
                    Get<ElementName>().entity = value;
                } else { Add<ElementName>( value ); }
            }
        }

        public IParameter<Panorama> elementar() { return this; }
        public virtual Panorama actual { get { return Panorama.Neutral; } set { elementar().value = value; } }
        public virtual IElmPtr<Panorama> elmptr() {
            if ( !Has<ElmPtr<Panorama>>() ) return makePointer();
            else return Get<ElmPtr<Panorama>>(); }

        private unsafe ElmPtr<Panorama> makePointer() {
            elementar().value = actual;
            fixed (Panorama* p = &Get<Elementar<Panorama>>().entity)
                return Add<ElmPtr<Panorama>>(new IntPtr(p));
        }

        Panorama IParameter<Panorama>.value {
            get { return GetElementar<Panorama>(); }
            set { Get<Elementar<Panorama>>().entity = value; }
        } 
        MODULATOR IParameter<Panorama>.type {get { return type; } }
        PARAMETER IParameter<Panorama>.usage { get { return usage; } }

        public static implicit operator ElmPtr<Panorama>( PanoramaParameter cast )
        {
            return cast.elmptr() as ElmPtr<Panorama>;
        }
        public static implicit operator Panorama( PanoramaParameter cast ) {
            return cast.actual;
        }
    }

    public class PanoramaValue : PanoramaParameter 
    {
        public Panorama value;
        public PanoramaValue() : base() {
            type  = MODULATOR.StaticValue;
            value = Panorama.Neutral;
        }
        
        public override ModulationParameter sides { get { return Get<ModulationParameter>(0); } set { Set(0, value); } }
        public override ModulationParameter front { get { return Get<ModulationParameter>(1); } set { Set(1, value); } }

        public IElmPtr<Preci> this[PARAMETER index]
        {
            get { return (index == PARAMETER.PanFront ? front : sides) as ModulationPointer; }
            set { value.pointer = Get<ModulationParameter,ModulationPointer>(index == PARAMETER.PanFront ? 1 : 0).pointer; }
        } 

        public override Element Init(Element attach,object[] parameter)
        {
            switch ( parameter.Length ) {
                case 4:
                    { ModulationPointer ptinit = attach.Add<ModulationParameter,ModulationPointer>(PARAMETER.PanFront);
                      ptinit.SetTarget( ref value.FR );
                      ptinit.value = (Preci)parameter[3];
                    } goto case 3;
                case 3:
                    { ModulationPointer ptinit = attach.Add<ModulationParameter,ModulationPointer>(PARAMETER.PanStero);
                      ptinit.SetTarget( ref value.LR );
                      ptinit.value = (Preci)parameter[2];
                    } goto case 2;
                case 2: if (parameter[1] is float) value.FR = (float)parameter[1]; goto case 1;
                case 1: if (parameter[0] is float) value.LR = (float)parameter[0];
                   else if (parameter[0] is Panorama) value = (Panorama)parameter[0]; goto default;
                default:
                return Init( attach );
            }
        }

        public override Panorama actual { get { return value; } set { this.value = value; } }
    }

    public class ElementLength : Element, IParameter<UInt32>
    {
        virtual public UInt32 frames { get; set; }
        public UInt32 value { get { return frames; } set { frames = value; } }
        public MODULATOR type { get { return MODULATOR.Constant; } }

        public virtual UInt32 actual { get { return frames; } set { Get<Elementar<UInt32>>().entity = value; } }
        IElmPtr<UInt32> IParameter<UInt32>.elmptr() { return Get<ElmPtr<UInt32>>(); }

        public PARAMETER usage { get { return PARAMETER.Duration; } }

        public ElementLength() : base() { frames = 0; }

        public string Name {
            get { return Has<ElementName>() ? Get<ElementName>().entity : track().Name + ".Length"; }
            set { if( Has<ElementName>() ) Get<ElementName>().entity = value; else Add<ElementName>( value ); }
        }


        public override Element Init( Element attach, object[] initializations )
        {
            for( int i = 0; i< initializations.Length; ++i ) {
                if ( initializations[i] is UInt32 ) frames = (uint)initializations[i];
            } return Init( attach );
        }

        public IParameter<UInt32> elementar()
        {
            return this;
        }

        public static implicit operator UInt32(ElementLength cast)
        {
            return cast.frames;
        }
    }



    public class BarrierFlags : Element, IParameter<UInt32>
    {
        public enum State { Clear = 0, Block = 1 }
        public uint  bits;
        private uint mask;

        UInt32 IParameter<UInt32>.value { get { return bits; } set { if (bits != value) { attached.changed(); bits = value; } } }
        IElmPtr<UInt32> IParameter<UInt32>.elmptr() { return Get<ElmPtr<UInt32>>(); }

        MODULATOR IParameter<UInt32>.type { get { return MODULATOR.StaticValue; } }

        public BarrierFlags() : base() {
            bits = 0xffffffffu;
            mask = 0xffffffffu;
            usage = PARAMETER.Segments;
        }

        public string Name {
            get { return Has<ElementName>()
                       ? Get<ElementName>().entity
                       : string.Empty; }
            set { if( Has<ElementName>() ) {
                    Get<ElementName>().entity = value;
                } else { Add<ElementName>( value ); }
            }
        }

        public uint actual {
            get{ return elementar().value; }
            set { elementar().value = value; }
        }

        public bool Clear {
            get { return bits == 0; }
            set { bits = value ? 0 : 0xffffffffu; }
        }

        public PARAMETER usage;
        PARAMETER IParameter<UInt32>.usage { get { return usage; } }

        public State this[ int idx ] {
            get{ return (State)((bits & (0x00000001u << idx)) >> idx); }
            set{ if( value == State.Clear )
                    bits &= ~(0x00000001u << idx);
               else bits |= (0x00000001u << idx); }
        }

        public int Count() {
            return (bits & mask) == 0 
                 ? 0 : Count( State.Block );
        }

        public int Count( State states ) {
            uint mums = mask;
            uint bums = bits; 
            int val = 0;
            int add = (int)states;
            int not = add == 0 ? 1 : 0;
            while( (mums & 1u) > 0 ) {
                val += ((bums & mums) & 1u ) > 0 ? add : not ;
                bums >>= 1;
                mums >>= 1;
            } return val;
        }

        public int Active {
            get { int num=0; while( (mask & (1u << num++)) > 0 ) ; return num-1; }
            set { mask = 0; while( value-- > 0 ) mask = (mask<<1)|1u; }
        }

        public IParameter<uint> elementar() { return this; }

        public static implicit operator State( BarrierFlags cast ) {
            return (cast.bits & cast.mask) == 0 ? State.Clear : State.Block;
        }

    }

    public class EVP : ModulationValue
    {
        public new const uint ElementCode = 5264965;
        private ElementLength count;
        private Preci[][]     param;
        private Preci         delta;
        private Preci         timeR;
        private Preci         nextL;
        private bool          isbig;
        private int           index;

        internal override List<ToneScriptParser.Token> accepts {
            get{ return new List<ToneScriptParser.Token>() {
                new ToneScriptParser.Token(ToneScriptParser.ScopeType.Modulator,ToneScriptParser.ToneToken.typ),
                new ToneScriptParser.Token(ToneScriptParser.ScopeType.Modulator,ToneScriptParser.ToneToken.va4),
                new ToneScriptParser.Token(ToneScriptParser.ScopeType.Modulator,ToneScriptParser.ToneToken.seq),
            }; }
        }

        public EVP() : base() {
            this.value = 1;
        }

        /// <summary> Can atach to just Track (or Track derived) elements.
        /// expects parameters:
        /// 0: usage (PARAMETER enum)
        /// 1: ADSR[2][2] (2x2 Preci values)
        /// 2: (opt.) length (framesCount UInt32 or Sequence Element) - defaults to the owning Track's Sequence length when ommitted
        /// </summary>
        /// <param name="track"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public override Element Init( Element track, object[] parameter )
        {
            type  = MODULATOR.Relative | MODULATOR.EVP;
            string error = "";

            if( !(parameter[0] is PARAMETER) ) error = "Missing first arg PARAMETER";
            if( !(parameter[1] is Preci[][]) ) error += ", Missing second arg ADSR[][]";
            if( parameter.Length > 2 ) {
                if( parameter[2] is ElementLength ) {
                    count = parameter[2] as ElementLength;
                } else if ( error.Length == 0 ) {
                    count = new ElementLength().Init( this, parameter[2] ) as ElementLength;
                }
            } else {
                count = track.Find<ElementLength>();
            } if (count == null) error = ", Cannot determine (opt. third arg) ElementLength";
 
            if ( error.Length > 0 ) {
                MessageLogger.logErrorSchlimm("EVP.Init(args[]): "+error);
                return null;
            }

            usage = (PARAMETER)parameter[0];
            param = parameter[1] as Preci[][];
            Add( count );

            if ( usage == PARAMETER.Panorama ) {
                usage = PARAMETER.PanStero;
            }

            switch (usage) {
                case PARAMETER.PanFront: {
                (track as ElementarTrack).pan.actual = (track as ElementarTrack).pan.actual;
                (track as ElementarTrack).pan.front = this;
                } break;
                case PARAMETER.PanStero: {
                (track as ElementarTrack).pan.actual = (track as ElementarTrack).pan.actual;
                (track as ElementarTrack).pan.sides = this;
                } break;
                case PARAMETER.Volume: {
                (track as ElementarTrack).amp = this;
                } break;
            }

            this.value = 0;
            SetAccessors( () => { return this.value; },
                (Preci setter) => { this.value = setter; }
            );
            Prepare();
            return Init( track );
        }

        private void SetAccessors( GetValFunc getter, SetValFunc setter )
        {
            valueGetter = getter;
            valueSetter = setter;
        }

        private void Prepare()
        {
            index = 0;
            nextL = param[index][0];
            isbig = actual > nextL;
            delta = param[index][1];
            timeR = GANZ - (delta + param[1][1]);
            delta = (nextL / (delta * count.frames));
            if( delta == Preci.NaN )
                delta = nextL;
            param[1][1] = (param[1][0] - param[0][0]) / (param[1][1] * count.frames);
            if( param[1][1] == Preci.NaN )
                param[1][1] = param[1][0] - param[0][0];
        }

        protected override void update()
        {
            actual += delta;

            if( isbig ? value <= nextL : value > nextL ) {
                actual = nextL;
                if ( index == 0 ) {
                    ++index;
                    nextL = param[index][0];
                    delta = param[index][1];
                    param[index][1] = -(
                        param[index][0] / (timeR * count.frames)
                                         );
                    param[index][0] = 0;
                } else if( index == 1 ) {
                    nextL = param[index][0];
                    delta = param[index][1];
                } isbig = value > nextL;
            }
        }

        private delegate Preci GetValFunc();
        private delegate void SetValFunc(Preci setter);

        private GetValFunc valueGetter;
        private SetValFunc valueSetter;

        public override Preci actual {
            get { return valueGetter(); }
            set { valueSetter(value); }
        }

        private unsafe IntPtr makePointer()
        {
           fixed (Preci* ptr = &this.value)
           {
                return new IntPtr(ptr);
           }
        }

        public override void changed()
        {
            if ( Has<ElmPtr>() ) unsafe {
                IntPtr p = Get<ElmPtr>().pointer;
                if ( p == makePointer()) {
                    SetAccessors( ()=>{ return value; },
                         (Preci set)=>{ value = set; }
                    );
                } else {
                    SetAccessors( ()=> { return value =
                    *(Preci*)Get<ElmPtr>().pointer.ToPointer();
                                          },
                          (Preci set)=>{ value =
                    *(Preci*)Get<ElmPtr>().pointer.ToPointer()=
                                     set; }
                    );
                }
            } else SetAccessors( () => { return value; },
                        (Preci set) => { value = set; }
            );
        }

        public override IElmPtr<Preci> elmptr()
        {
            if (Has<ElmPtr>()) return Get<ElmPtr>();
            else return Add<ElmPtr>( makePointer() );
        }

        public override uint GetElementCode() {
            return ElementCode;
        }

        public static implicit operator ElmPtr<Preci>(EVP cast)
        {
            return cast.elmptr() as ElmPtr<Preci>;
        }

        public static implicit operator Preci(EVP cast) {
            return cast.value;
        }
    }

    public class LFO : ModulationParameter, IElmPtr<Preci>
    {
        public new const uint ElementCode = 7300716;
        public ControlledPreci mod;
        public override Preci actual {
            get { return mod; }
        }

        public IntPtr pointer { get { return mod.GetTarget(); } set { mod.SetTarget( value ); } }

        public LFO() : base() {}

        /// <summary>
        /// expected parameters are:
        /// 0: modulation target
        /// 1: min value
        /// 2: max value
        /// 3: frq in Hz
        /// 4: wave form
        /// 5: samplefrq
        /// </summary>
        /// <param name="attach"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        public override Element Init( Element attach, params object[] parameter )
        {
            if( attach is ElementarTrack ) {
                mod = new ControlledPreci();
                usage  = (PARAMETER)parameter[0];
                ControlMode  form =  (ControlMode)parameter[4];
                Preci  min = (Preci)parameter[1];
                Preci  max = (Preci)parameter[2];
                Preci  frq = (Preci)parameter[3];         
                Preci rate = (Preci)parameter[5];
                Preci  val = 0;
                if ((usage & PARAMETER.Panorama) != PARAMETER.None) {
                    attach.Set<PanoramaParameter>(0,new PanoramaValue().Init(attach) as PanoramaValue);
                }
                if ( attach is OSC ) {
                OSC osc = attach as OSC;
                switch( usage ) {
                    case PARAMETER.WaveForm:
                    switch( osc.osc.Mode ) {
                        case ControlMode.Pulse: {
                            mod.LetPoint( ControllerVariable.VAL, osc.osc.GetPin( PulsFormer.FORM ) );
                        } break;
                        case ControlMode.Stack: {
                            mod.LetPoint( ControllerVariable.VAL, osc.osc.GetPin( RampFormer.FORM ) );
                        } break;
                        case ControlMode.Sinus: {
                            mod.LetPoint( ControllerVariable.VAL, osc.osc.GetPin( SineFormer.FORM ) );
                        } break;
                        case ControlMode.Cycle: {
                        //    mod.LetPoint( ControllerVariable.VAL, osc.osc.GetPin( RampFormer.FORM ) );
                        } break;
                        case ControlMode.Delegate: {
                            mod.LetPoint( ControllerVariable.VAL, osc.osc.GetPin( RampFormer.FORM ) );
                        } break;
                        default: {
                            Element.ErrorReport( "LFO Modulator Form ", osc.osc.Mode+" is unknown ControlMode" );
                            return this; } 
                    } break;
                    case PARAMETER.Frequency: {
                         val = osc.osc.MOV;
                         mod.LetPoint( ControllerVariable.VAL, osc.osc.GetPointer( ControllerVariable.MOV ) );
                    } break;
                    case PARAMETER.Volume: {
                         osc.amp = this;
                    } break;
                    case PARAMETER.PanStero: {
                         osc.pan.sides = this;
                    } break;
                    case PARAMETER.PanFront: {
                         osc.pan.front = this;
                    } break;
                }
              } else if ( attach is SourceSample ) {
                    SourceSample wav = attach as SourceSample;
                    switch( usage ) {
                        case PARAMETER.WaveForm: {
                            wav.length.Set<ModulationParameter>(2,this);
                        } break;
                        case PARAMETER.StartPoint: {
                            wav.length.Set<ModulationParameter>(0,this);
                        } break;
                        case PARAMETER.Volume: {
                            wav.amp = this;
                        } break;
                        case PARAMETER.PanStero: {
                            wav.pan.sides = this;
                        } break;
                        case PARAMETER.PanFront: {
                            wav.pan.front = this;
                        } break;
                    }
              }
             
                mod.SetUp( min, max, ToneScriptParser.correctFreqForFORM( (max-min) / (rate / frq), (FORM)form ), val, form );
                mod.SetCheckAtGet();
                mod.Active = true;
            } else {
                ErrorReport( "ModulationLFO", "must attach to Elements of type 'ElementarTrack'" );
            }
            return Init( attach );
        }



        protected override void update()
        {
            mod.Check();
        }

        public override uint GetElementCode()
        {
            return ElementCode;
        }

        public void SetProportion(Preci proper)
        {
            mod.VAL = proper;
        }

        public void SetTarget( ref Preci variable )
        {
            mod.SetTarget( ref variable );
        }

        public void SetTarget( IntPtr ptr )
        {
            mod.SetTarget( ptr );
        }
        public override IParameter<Preci> elementar()
        {
            return this;
        }
        public override IElmPtr<Preci> elmptr()
        {
            return this;
        }
    }

    public class LinearPanChange : PanoramaValue
    {
        protected Panorama change = new Panorama();

        public LinearPanChange() : base() {}

        public override Element Init( Element attach, params object[] initializations )
        {
            type = MODULATOR.LinearChange;
            int argidx = 0;
            if( initializations[argidx] is PARAMETER)
                ++argidx;
            Panorama[] array = initializations[argidx++] as Panorama[];
            uint len = 0;
            if (initializations.Length > argidx)
                len = (uint)initializations[argidx];
            
            base.Init( attach, array[0].LR, array[0].FR, GANZ, GANZ );
            value = array[0];
            change = array[1];
            return Init( attach );
        }
        protected override void update() {
            value.addStep( change );
        }
    }

    public class LinearValueChange : ModulationValue
    {
        protected Preci change = 0;
        public LinearValueChange() : base() {}

        public override Element Init(Element attach, object[] initializations)
        {
            type = MODULATOR.LinearChange;
            int argidx = 0;
            if ( initializations[argidx] is PARAMETER )
                usage = (PARAMETER)initializations[argidx++];
            Preci[] parameter = initializations[argidx++] as Preci[];
            value = parameter[0];
            change = parameter[1];
            return Init(attach);
        }
        protected override void update()
        {
            value += change;
        }
    }
}

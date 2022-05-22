using System;
using System.Collections;
using System.Collections.Generic;
using Tokens = System.Collections.Generic.List<Stepflow.Audio.Elements.ToneScriptParser.Token>;
using Stepflow;
#if X86_64
using Preci = System.Double;
using Archi = System.UInt64;
using ControlledPreci = Stepflow.Controlled.Float64;
using MonoPreciFrame = Stepflow.Audio.FrameTypes.AuPCMf64bit1ch;
#elif X86_32
using Preci = System.Single;
using Archi = System.UInt32;
using ControlledPreci = Stepflow.Controlled.Float32;
using MonoPreciFrame = Stepflow.Audio.FrameTypes.AuPCMf32bit1ch;
#endif

namespace Stepflow.Audio.Elements
{
    internal static class Constants
    {
#if X86_64
        public const TypeCode PreciTypeCode = TypeCode.Float64;
        public const int      SignShiftZwei = 62;
        public const Archi    PreciSignMask = 0x8000000000000000u;
#else
        public const TypeCode PreciTypeCode = TypeCode.Float32;
        public const int      SignShiftZwei = 30;
        public const Archi    PreciSignMask = 0x80000000u;
#endif
    }

    public enum FORM : uint
    {
        sin = (uint)ControlMode.Sinus,
        saw = (uint)ControlMode.Cycle,
        pls = (uint)ControlMode.Pulse,
        tri = (uint)ControlMode.Stack
    };

    public enum FILE
    {
        ReadOnDemand,
        LoadOnBeginn
    };

    public enum PARAMETER : ushort
    {
        None = 0,
        Frequency,
        Volume,
        FxSend,
        FxPara,
        PanStero,
        PanFront,
        Panorama,
        WaveForm,
        Duration,
        PitchRate,
        LoopTime,
        StartPoint,
        Segments,
        Metronome,
    }

    public enum MODULATOR : ushort
    {
        Absolute     = 0,
        Constant     = Absolute,
        StaticValue  = 0xC000,
        LinearChange = 0x4,
        CurvedChange = 0x8,
        LFO          = 0x10,
        EVP          = 0x20,
        Relative     = 0x8000,
    }

    public enum PROGRESS : sbyte
    {
        Back = -0x80,
        None =  0x00,
        Init =  0x00,
        Play =  0x01,
    }

    public interface IElement { E element<E>() where E : Element; }
    public interface IElement<E> : IElement where E : IElement { E element(); }

    // base class for compositing functional 'clusters' which will add functionallity (which the unit provides)
    // dynamically on demand, as soon calls may depend on these. 
    // not was declared (or instanciated fits better here - functionalities the compiled unit provides 
    // can be instanciated later on for a 'cluster', on demand, even if not meantioned for instanciation
    // when the cluster was designed.... with this, a clusters will add anything it can find in the compilation
    // unit which it needs to return some kind of result without causing greater errors or even crashes at runtime 
    // (this means: if one tries to acces some (vermeintliche) member variable on an element but which not yet is
    // defined on it, the element will then instanciate and attach a varable of that reqested type at runtime and
    // will be able proceeding some sutable return value so... requesting things which not already are 'there' won't
    // cause error, but would add some new variable of accessed type in a default state then, (without notifying anything)
    // as if it would have been there before already.... (does not mean everything to be handled
    // like this. more efficently in any case would be still to implement - before making usage at runtime- 
    // specialized element classes which can handle distinct tasks 'out of the box' without having to look up new things 
    // at runtime and then attaching overrides for these.
    // In any case, these clusters won't nessesarly conduct to some kind of predictable tree-baum structure.. there no
    // strict hirachical rules apply which could be used for predicting conducted structure of elements at runtime 
    // (in many cases elements ARE tree-baum order structured and accesible, but this isn no mansatory due to
    // the connectioning mechanisim,.. it shold be seen more like a 'chaotically ordred structure' best fitting
    // distinct individual purposes - like huge storage malls are not meant and used for 'being ordered' but for 
    // 'storing things' at least.

    // this model doesnt distingueses between 'objects' and 'components', or 'sites' and 'components' like other models
    // do, but knows only 'elements' - these can conect against each other without any more rules then: an 'element' can connect any variabl of type 'element' or of types derived from 'element'
    // - when some diferent type may be needed missing at runtime - or lazy developer missed implementing a distinct spezialized 'element' providing a type, 
    // such missing type then can be 'elementar' added instantly. makes any accessible data at pointed addresses doing fine being added 'elementar' element and can be added to clusters needing a type.
    // - element are allowed to depend on set a initialization parameters for being able functioning correctly and doing distinct tasks, but must ensure every symbols (functions, variables anything) are accesible always ... even without having initialization parameters passed - functions and fields then shall return 0, default constructed objects, or anything else and at best not nullptrs atleast
    // the parameters for initialization are passed shortly after constructin always nameless, always typless as an object[] arrry - which also is allowd to come in null, or even not passed at all
    // so Initializer functions of elements 'MUST' ensure type checking any arguments the initializer maybe 'could' gets passed. so initializer always should rather assume no parameter are passed in - and not that expected (or even any) parameters are pased   
    // for correctly functioning - but they never can use individual function signatures forthese. 
    // - element classes only can be attached which type is 'instanciable' via defaut constructor... so no abstract base class types can be defined for carying abstract functionality definitions. - the type must be constructable - if abstract base functionality is wanted it cannot come as (= 0) virtual calls, but functions then  atlast must implement empty body and (if functions declare return type the function must return a variable of that declared type for real:  virtual Elementar<int> funcname(){ return GetElementar<int>(); }  would be minimal variant valid in somer abstractelement base  base  
    // the only way to depend on parameters is via implementing Init(Element attach, object[] args) ... (Element attach parameter is always mandatory,.. an element broght up to a cluster via 'attaching' 
    // it to some ewlement.  - (these parameter must have passed an element object -if there are no other elements wgich could attach to, an elements this pointer also sohould do initialization working fine.

    // (an element can connect to itself for sure, this does not interferes the rule) - 
    // 
    public abstract class Element : IElement
    {
        internal static void ErrorReport( string what, string why ) {
            MessageLogger.logErrorSchlimm( "# Error: Assigning {0} is disabled on {1}", what, why );
        }
        internal static void InfoReport( string what, string why ) {
            MessageLogger.logInfoWichtig( "# Log: {0} info: {1}", what, why );
        }
        public const uint ElementCode = 0;

        internal static readonly Tokens Nothing = new Tokens(0);
        internal static readonly object[] NoArgs = Array.Empty<object>();

        public const Preci ZWEI = 2;
        public const Preci GANZ = 1;
        public const Preci HALB = GANZ/ZWEI;
        public const Preci NULL = 0;

        public static Preci SYMETRIC( Preci bal ) { return (bal+bal) - GANZ; }
        public static Preci BALLANCE( Preci sym ) { return  sym*HALB + HALB; }
        public static Preci ANTIPROP( Preci prop ) {
            unsafe { Archi s = *(Archi*)&prop & Constants.PreciSignMask;
                 prop = GANZ - prop;
              s |= *(Archi*)&prop;
            return *(Preci*)&s + (Preci)((s & Constants.PreciSignMask) >> Constants.SignShiftZwei);
                   }
        }

        E IElement.element<E>() { return this as E; }



        protected bool      phasys;
        protected Element   attached;
        protected Hashtable elements;

        public MixTrack render {
            get { Element find = this;
                while ( find != find.attached ) {
                    if (find.attached == null) break;
                        find = find.attached;
               } return find as MixTrack;
            }
        }

        virtual public ITrack track() {
               Element find = this;
             do { if ( find is ElementarTrack) break;
                  else find = find.attached;
              } while( find != find.attached );
                return find as ElementarTrack;
        }

        internal virtual Tokens accepts {
            get { return Nothing; }
        }
        
        public virtual uint GetElementCode() {
            return ElementCode;
        }

        public bool IsProgress
        {
            get { return render.master.active; }
        }

        protected virtual void update() {}
        public virtual void changed() {}

        public void Update( bool currentPhasys ) {
            if (currentPhasys == phasys) {
                update();
                phasys = !phasys;
            }
        }

        public Element()
        {
            phasys = false;
            attached = null;
            elements = new Hashtable();
        }
        public virtual Element Init( Element attach )
        {
            attached = attach;
            phasys = attached == null ? false : attached.phasys;
            return this;
        }
        public virtual Element Init( Element at, params object[] it )
        {
            return Init( at );
        }

        public virtual Element Tryit( Element tryout )
        {
            if( attached == null ) {
                return Init( tryout );
            } return this;
        }

        public E Add<B,E>( params object[] parameter ) where E : B, new() where B : Element, new()
        {
            Type b = typeof(B);
            if( elements.ContainsKey(b) ) { 
                List<B> list = elements[b] as List<B>;
                list.Add( new E().Init( this, parameter ) as E );
                return list[list.Count-1] as E;
            } else if (elements.ContainsKey( typeof(E) ) ) {
                List<B> neu = new List<B>();
                neu.AddRange( elements[typeof(E)] as List<E> );
                neu.Add( new E().Init( this, parameter ) as E );
                elements.Remove( typeof(E) );
                elements.Add( b, neu );
                return neu[neu.Count-1] as E;
            } else { E neu = new E().Init(this, parameter) as E;
                elements.Add(b, new List<B>(1) {
                    neu } );
                return neu;
            }
        }
        public int Idx<B,E>(int number) where E : B where B : Element
        {
            if( Has<B>() ) {
                List<B> list = elements[typeof(B)] as List<B>;
                for (int i = 0; i < list.Count; ++i)
                    if (list[i] is E) if(--number < 0) return i;
            } return -1;
        }
        public int Idx<B,E>() where E : B where B : Element
        {
            return Idx<B,E>(0);
        }
        public int Idx<B>( Element E, int number ) where B : Element
        {
            if (number == 0) { 
                if (elements.ContainsKey(E.GetType()))
                    return 0;
            }
            if( elements.ContainsKey(typeof(B)) ) {
                List<B> list = elements[typeof(B)] as List<B>;
                for (int i = 0; i < list.Count; ++i)
                    if (list[i] == E) if (--number < 0) return i;
            } return -1;
        }
        public int Idx<B>(Element E) where B : Element
        {
            return Idx<B>(E,0);
        }

        public E Get<B,E>() where E : B, new() where B :Element, new()
        {
            int idx = Idx<B,E>(0);
            if( idx < 0 ) { MessageLogger.logErrorSchlimm(
                "Element not has {0} of type {1} attached",
                                       typeof(B), typeof(E) );
                return null;
            } else return (elements[typeof(B)] as List<B>)[idx] as E;
        }

        public E Get<B,E>( int idx ) where E : B, new() where B : Element, new()
        {
            if( !Has<B>() ) {
                List<B> list = new List<B>( idx + 1 );
                for (int i = 0; i < idx; ++i) list.Add( new B() );
                list.Add( new E() );
                elements.Add( typeof(B), list );
            } return (elements[typeof(B)] as List<B>)[idx] as E;
        }

        public E Add<E>( params object[] parameter ) where E : Element, new()
        {
            if ( !elements.ContainsKey( typeof(E) ) ) {
                return Get<E>().Init( this, parameter ) as E;
            } else {
                E neu = new E();
                neu.Init( this, parameter );
                (elements[typeof(E)] as List<E>).Add( neu );
                return neu;
            }
        }

        public E Add<E>( E instance ) where E : Element
        {
            Type e = typeof(E);
            if ( elements.ContainsKey(e) ) {
                List<E> list = elements[e] as List<E>;
                if ( !list.Contains(instance) )
                    list.Add( instance );
            } else elements.Add( e, new List<E> { instance } );
            return instance;
        }

        internal E Add<E>( E instance, Type B ) where E : Element
        {
            Type lt = typeof(List<>).MakeGenericType( new Type[]{B} );
            if ( (!elements.ContainsKey(B)) ) {
                object bList = lt.GetConstructor( Type.EmptyTypes ).Invoke( new object[0] );
                if( elements.ContainsKey( typeof(E) ) ) {
                    List<E> eList = elements[typeof(E)] as List<E>;
                    object[] objar = new object[eList.Count];
                    for (int i = 0; i < objar.Length; ++i)
                    { objar[i] = eList[i]; }
                    lt.GetMethod( "Add" ).Invoke( bList, objar );
                    elements.Remove( typeof(E) );
                } elements.Add( B, bList );
            } lt.GetMethod( "Add" ).Invoke( elements[B], new object[]{instance} );
            return instance;
        }

        public E Get<E>() where E : Element, new()
        { Type t = typeof(E);
            if ( !elements.ContainsKey(t) ) {
                elements.Add( t, new List<E>(){ new E() } );
            } return (elements[t] as List<E>)[0];
        }

        public E Get<E>( int idx ) where E : Element, new()
        {
            Type e = typeof(E);
            List<E> get;
            ++idx;
            if ( !elements.ContainsKey( e ) ) {
                get = new List<E>( idx );
                get.Add( new E() );
                elements.Add( e, get );
            } else get = elements[e] as List<E>;
            for ( int i = get.Count; i < idx; ++i ) {
                get.Add( new E() );
            } return get[ idx-1 ];
        }

        public E Set<E>( int idx, E elm ) where E : Element
        {
            Type e = typeof(E);
            if ( !elements.ContainsKey(e) ) {
                if (idx == 0) {
                    elements.Add( e, new List<E>(){elm} );
                    return elm; } 
            } else {
                List<E> list = elements[e] as List<E>;
                if( list.Count > idx ) {
                    return ( list[idx] = elm );
                } else if( list.Count == idx ) {
                    list.Add( elm );
                    return elm;
                }
            } ErrorReport(
                "instance to element '" + idx + "'",
                "no element '" + idx + "'exists at all"
                             );
            return elm;
        }

        public bool HasElementar<T>()
        {
            return Has<Elementar<T>>() ? true
                 : elements.ContainsKey( typeof(Elementar<T>) );
        }

        public T GetElementar<T>()
        {
            return Get<Elementar<T>>().entity;
        }

        public T GetElementar<T>(int idx)
        {
            return Get<Elementar<T>>(idx).entity;
        }

        public void SetElementar<T>(int idx, T val)
        {
            if ( !Has<Elementar<T>>(idx) ) {
                Get<Elementar<T>>(idx).entity = val;

            }
        }
        public bool Has( Type T )
        {
            if ( T.IsSubclassOf( typeof(Element) ) ) {
                return elements.ContainsKey(T);
            } else if ( !T.IsGenericType ) {
                return Has( typeof(Elementar<>).MakeGenericType(new Type[]{T}) );
            } else return elements.ContainsKey( T.GetElementType() );
        }
        public bool Has<E>() where E : Element
        {
            return elements.ContainsKey( typeof(E) );
        }

        public bool Has<E>( int idx ) where E : Element
        {
            if( elements.ContainsKey( typeof(E) ) ) {
                return (elements[typeof(E)] as List<E>).Count > idx;
            } return false;
        }

        public int Num<E>() where E : Element
        {
            if( elements.ContainsKey( typeof(E) ) ) {
                return (elements[typeof(E)] as List<E>).Count;
            } return 0;
        }

        public IEnumerator<E> All<E>() where E : Element
        {
            if( elements.ContainsKey( typeof(E) ) ) {
                return (elements[typeof(E)] as List<E>).GetEnumerator();
            } return Array.Empty<E>().GetEnumerator() as IEnumerator<E>; 
        }

        public void ForAll<E>( Action<E> perform ) where E : Element
        {
            IEnumerator<E> it = All<E>();
            while ( it.MoveNext() ) {
                perform( it.Current );
            }
        }

        public void ForAllIn<E,F>( F ins, Action<E,F> perform ) where E : Element
        {
            IEnumerator<E> it = All<E>();
            while ( it.MoveNext() ) {
                perform( it.Current, ins );
            }
        }

        public void UpdateSub<E>( bool currentPhasys ) where E : Element
        {
            IEnumerator<E> e = All<E>();
            while ( e.MoveNext() ) {
                e.Current.UpdateSub<E>( currentPhasys );
                e.Current.Update( currentPhasys );
            }
        }

        public void UpdateAll<E>( bool currentPhasys ) where E : Element
        {
            IEnumerator<E> e = All<E>();
            while ( e.MoveNext() ) {
                e.Current.Update( currentPhasys );
            }
        }

        public void Rem<E>( int idx ) where E : Element
        { 
            int count = Num<E>();
            if( count > 0 ) {
                if (count == 1 || idx < 0)
                    elements.Remove( typeof(E) );
                else ( elements[typeof(E)] as List<E>
                      ).RemoveAt( idx );
            }
        }

        public E Deep<E>( int depth ) where E : Element, new()
        {
            if( depth < 0 ) return null;
            if( Has<E>() ) return Get<E>();
            else if ( depth > 0 )
            foreach( var hash in elements.Keys ) {
                    --depth; E findi;
                object obj = elements[hash].GetType();
                if( ( findi = (obj as Element)?.Deep<E>(depth) ) != null )  return findi;    
                Type oT = obj.GetType();
                if ( oT.HasElementType ) {
                    IEnumerator it = typeof(List<>).MakeGenericType(
                          new Type[]{ oT.GetGenericArguments()[0] } )
                                    .GetMethod( "GetEnumerator" )
                                    .Invoke( obj, NoArgs )
                    as IEnumerator;
                    while( it.MoveNext() )
                        if( ( findi = (it.Current as Element).Deep<E>( depth ) ) != null )
                            return findi;
                }
            } return null;
        }

        // depth parameter = 1: (default)
        // searches directly self attached components first, if not finds an E proceeds upward the hirachy till master, searching directly atached elements to each higher element on its way upward till master
        // if not finds no E on neither these nor on master itself, it will go on searching the own cluster downward 2    
        // depth parameter > 1 will invoke deep search on all side branches attached to the master track
        // (would search element clusters of all other tracks attached to the master mixer before looking up own track's element cluster)
        // depth parameter = 0 will lookup upwards till master output before searching downwards the own cluster by detail - but won't crawl through other maybe also master attached mixtracks before)
         
        public E Find<E>( int depth ) where E : Element, new()
        {
            if( Has<E>() ) {
                return Get<E>();
            } else if (attached != this) {
                return attached.Find<E>( depth-1 );
            } else {
                return Deep<E>( depth );
            } 
        }

        public E Find<E>() where E : Element, new()
        {
            return Find<E>( 1 );
        }
    }

    public class Elementar<T> : Element
    {
        public T entity;
        public Elementar() : base() {}
        public Elementar( Element at ) : this() {
            Tryit( at );
        }
        public override Element Init( Element at, params object[] it ) {
            if( it.Length > 0 )
                entity = (T)it[0];
            return Init( at );
        }

        public static implicit operator T( Elementar<T> cast ) {
            return cast.entity;
        } 
    }

}

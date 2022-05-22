using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Consola;
using Std = Consola.StdStream;
#if X86_64
using Preci = System.Double;
using ControlledPreci = Stepflow.Controlled.Float64;
using MonoPreciFrame = Stepflow.Audio.FrameTypes.AuPCMf64bit1ch;
#elif X86_32
using Preci = System.Single;
#endif

namespace Stepflow.Audio.Elements
{

    public interface IToneScriptSource
    {
        IToneScriptSource script();
        string NextElement { get; }
        bool loade( string filenameOrContentAsString, Element attachTo );
    }

    public class ScriptElement : Element
    {
        public override uint GetElementCode()
        {
            return (uint)GetHashCode();
        }
    }

    public class ToneScriptString : ScriptElement, IToneScriptSource
    {
        public IToneScriptSource script() { return this; }
        protected Queue<string> tontoken = null;
        string IToneScriptSource.NextElement {
            get { if( tontoken.Count > 0 )
                    return tontoken.Dequeue().Trim();
                else return null;
            }
        }
        bool IToneScriptSource.loade( string contentstring, Element attachTo )
        {
            if( contentstring.EndsWith(".ton") 
              | contentstring.EndsWith(".log") )
                return false;
            string[] split = contentstring.Split('\n',';');
            if( split.Length > 0 ) {
                tontoken = new Queue<string>( split );
                Init( attachTo );
                return true;
            } else return false;
        }
    }
    public class ToneScriptStream : ToneScriptString, IToneScriptSource
    {
        protected System.IO.StreamReader tonstream = null;
        string IToneScriptSource.NextElement { 
            get{ if( tontoken.Count > 0 ) {
                    return tontoken.Dequeue().Trim(); 
                 } else {
                    if( !tonstream.BaseStream.CanRead )
                        return null;
                    string line = tonstream.ReadLine().Trim();
                    if ( line == null ) tonstream.Close();
                    if ( line.Contains(";") ) {
                        string[] split = line.Split(';');
                        foreach( string elmtok in split ) {
                            tontoken.Enqueue( elmtok ); }
                        line = tontoken.Dequeue().Trim();
                    } return line;
                }
            }
        }
        bool IToneScriptSource.loade( string filename, Element attachTo ) {
            if( filename.EndsWith(".ton")
              | filename.EndsWith(".log") ) {
                System.IO.FileInfo f = new System.IO.FileInfo( filename );
                if( f.Exists ) {
                    tonstream = f.OpenText();
                    tontoken = new Queue<string>();
                    Init( attachTo );
                    return true; }
            } return false;
        }
    }

    public class ScriptSource : MixTrack
    {
        protected string            nam;
        public ElementLength frameCount;
        public List<ElementarTrack> osc;
        protected IAudioFrame       mix;

        public static Element Create( string from, Element attachTo )
        {
            if( from.EndsWith(".wav")
              | from.EndsWith(".au")
              | from.EndsWith(".snd")
              | from.EndsWith(".pam") ) {
                return new AudioSource().Init( attachTo,
                    new object[] { from, FILE.LoadOnBeginn }
                ) as Element;
            } else if ( from.EndsWith(".ton")
                      | from.EndsWith(".log") ) {
                ToneScriptStream stream = new ToneScriptStream();
                if( stream.script().loade( from, attachTo ) )
                    return stream;
            } else {
                ToneScriptString tone = new ToneScriptString();
                if( tone.script().loade( from, attachTo ) )
                    return tone;
            } return null;
        }

        public ScriptSource() : base()
        {
            osc = new List<ElementarTrack>();
        }

        public override uint FrameCount {
            get{ return frameCount; }
        }

        public override Element Init( Element attach, object[] parameter )
        {
            string initerrors = "ScriptSource.Init() takes exactly 3 parameters";
            if ( parameter.Length < 3 ) { MessageLogger.logErrorSchlimm( initerrors ); return null; }

            if (parameter[0] is PcmFormat) SetTargetFormat( (PcmFormat)parameter[0] );
            else { MessageLogger.logErrorSchlimm( initerrors+"... first parameter must be PcmFormat defining track format" ); return null; }

            if (parameter[1] is string) nam = (string)parameter[1];
            else { MessageLogger.logErrorSchlimm( initerrors + "... second parameter must be String defining track name"); return null; }

            if (parameter[2] is uint) frameCount = Add<ElementLength>( (uint)parameter[2] );
            else { MessageLogger.logErrorSchlimm(initerrors + "... third parameter must be UInt32 defining track length"); return null; }

            return Init( this );
        }

        public override void SetTargetFormat( PcmFormat format )
        {
            mix = format.FrameType.CreateEmptyFrame();
            current = format.FrameType.CreateEmptyFrame();
            samplerate = format.SampleRate;
        }

        public override void MixInto( ref IAudioFrame frame, float drywet )
        {
            frame.Mix( PullFrame(), drywet );
        }

        protected override IAudioFrame pullFrame()
        {
            FillFrame( ref mix );
            return mix;
        }

        public override void FillFrame( ref IAudioFrame frame )
        {
            frame.Clear();
            int count = osc.Count;
            float perchan = 1.0f/count;
            for( int i=0; i<count; ++i ) {
                osc[i].FillFrame( ref current );
                frame.Mix( current.Amp( perchan ), 0.5f );
            } if( Has<MixTrack>() )
                DoSubTracksMix( ref frame );
            Update( phasys );
        }

        public override void PushFrame( IAudioOutStream into )
        {
            into.WriteFrame( PullFrame() );
        }

        public override string Name {
            get { return nam; }
            set { nam = value; }
        }

        protected override void update()
        {
            foreach( OSC o in osc ) {
                o.Update( phasys );
            }
        }
    }

    public class ToneScriptParser 
    {
        [Flags]
        public enum ScopeType : byte {
            OpenScope, Master=0x1, Track=0x2, Elementar=0x4,
            Effect=0x8, Modulator=0x10, Sequence=0x20
        }

        [Flags]
        public enum ToneToken : ulong {
            // typ's: (lower 32bit)
            val=0x0000000000000000,
            wav=0x0000000000000100,
            osc=0x0000000000000200,
            mod=0x0000000000000400,
            pan=0x0000000000000C00,
            efx=0x0000000000001000,
            seq=0x0000000000002000,
            mix=0x0000000000004000,
            typ=0x0000000000008000,
            sum=0x0000000000010000,

            // arg's: value (higher 32bit) of type (lower 32bit)
            sin=osc|(FORM.sin<<32),
            pls=osc|(FORM.pls<<32),
            tri=osc|(FORM.tri<<32),
            saw=osc|(FORM.saw<<32),
            amp=typ|(PARAMETER.Volume<<32),
            vol=amp,
            frm=typ|(PARAMETER.WaveForm<<32),
            frq=typ|(PARAMETER.Frequency<<32),
            lfo=mod|(LFO.ElementCode<<32),
            evp=mod|(EVP.ElementCode<<32),
            a2b=mod|(LinearValueChange.ElementCode<<32),
            ctr=mod|(ModulationValue.ElementCode<<32),
            va1=val|(Constants.PreciTypeCode<<32)|(1<<24),
            va2=val|(Constants.PreciTypeCode<<32)|(2<<24),
            va4=val|(Constants.PreciTypeCode<<32)|(4<<24),
            va8=val|(Constants.PreciTypeCode<<32)|(8<<24),
        }

        [Flags]
        public enum OperatorType : ushort
        {
            Define  = 0x1,
            Attach  = 0x2,
            Values  = 0x4,
            Finish  = 0x8
        }

        [StructLayout(LayoutKind.Explicit,Size = 8)]
        public struct Token
        {
            private Token( ulong rawvalue ) : this() {
                token = (ToneToken)rawvalue;
            }

            public static readonly Token      Empty = new Token( 0xffffffffffffffff );
            [FieldOffset(0)] public ScopeType scope;
            [FieldOffset(0)] public ToneToken token;
            [FieldOffset(1)] public UInt24    param;
            [FieldOffset(4)] public UInt32    value;

            public Token( ScopeType scopetype, ToneToken tonetoken ) : this()
            {
                token = tonetoken;
                scope = scopetype;
            }
            public static OperatorType operator +(Scope This, Token That)
            {
                if( This < That.scope ) {
                    if ( This.CanScope.HasFlag( That.scope ) ) {
                        return OperatorType.Define;
                    } else {
                        return OperatorType.Attach;
                    }
                } else if (This.CanClose) {
                    return OperatorType.Finish;
                } else {
                    return OperatorType.Values;
                }
            }
            public static implicit operator bool(Token cast)
            {
                return cast != Token.Empty;
            }
            public static bool operator ==(Token aaa,Token bbb)
            {
                return aaa.token == bbb.token;
            }
            public static bool operator !=(Token aaa,Token bbb)
            {
                return aaa.token != bbb.token;
            }
            public override bool Equals( object obj )
            {
                return obj is Token ? this == (Token)obj : false;
            }
        }

        public class Scope
        {
            private static uint      id;
            static              Scope(){ id = 0; }
            static uint    generatore(){ return id++; }
            public readonly uint scpid;
            public ScopeType      type;
            public Element     element;
            private Stack<Scope> stack;
            private Scope       within;
            private List<uint>  holden;

            public Scope( Element createNew, Scope withIn, ScopeType newScope )
            {
                scpid = generatore();
                element = createNew;
                type = newScope;
                holden = new List<uint>();
                if( this ) (within = withIn).holden.Add( scpid );
                else { stack = new Stack<Scope>();
                    within = withIn == null 
                           ? this : withIn; }
                stack.Push( this );
            }
            
            public Stack<Scope> Stack {
                get{ return stack; }
            } 

            public static implicit operator bool( Scope cast ) {
                return cast.scpid > 0;
            }
            public static implicit operator ScopeType( Scope cast ) {
                return cast.type | cast.within.CanScope;
            }
            public bool CanClose {
                get{ return holden.Count == 0; }
            }
            public ScopeType CanScope {
                get { return (ScopeType)((uint)type<<1); }
            }
            public Scope Origin {
                get { return within; }
            }
            public bool Exists( Scope relation )
            {
                if( !holden.Contains( relation.scpid ) )
                    return within.Exists( relation );
                else return true;
            }
        }

        public bool                  logging;
        protected IToneScriptSource  sources;
        protected MixTrack           created;
        public PcmFormat             format;
        private bool                 verbose;
        private Stack<Scope>         scopses;
        private Dictionary<char,int> perform;
        private MasterTrack          mainmix;
        private string               iserror;
        
        internal ToneScriptParser(bool l, bool v)
        {
            iserror = null;
            sources = null;
            created = null;
            logging = l;
            verbose = v;
            perform = new Dictionary<char,int>();
        }

        public ToneScriptParser( MasterTrack mixer, bool enableLogging, bool verboseLogging )
            : this(enableLogging,verboseLogging) {
            scopses = new Scope( mainmix = mixer, null, ScopeType.Master ).Stack;
            format = mainmix.format;
        }

        public ToneScriptParser( PcmFormat targetFormat, bool enableLogging, bool verboseLogging )
            : this(enableLogging,verboseLogging) {
            format = targetFormat;
            scopses = new Scope( mainmix = new MasterTrack(targetFormat), null, ScopeType.Master ).Stack;
        }

        public MasterTrack OutputMixer
        {
            get { return mainmix; }
            set { mainmix = value; }
        }

        private Scope InScope {
            get{ return scopses.Peek(); }
            set{ scopses.Push( value ); }
        } 

        public void SetOutputFormat( PcmFormat fmt )
        {
            format = fmt;
        }

        public void EnableLogging( bool shouldBeEnabled )
        {
            if( !shouldBeEnabled )
                VerboseLogging( shouldBeEnabled );
            logging = shouldBeEnabled;
        }

        public void VerboseLogging( bool shouldVerboseLoggingEnabled )
        {
            if( shouldVerboseLoggingEnabled )
                EnableLogging( shouldVerboseLoggingEnabled );
            verbose = shouldVerboseLoggingEnabled;
        }

        public MixTrack CreatedTrack
        {
            get{ return created; }
        }

        public bool loadScript( string scriptsource )
        {
            Element creating = ScriptSource.Create( scriptsource, mainmix );
            if( creating is AudioSource ) {
                created = creating as AudioSource;
                sources = null;
            } else if ( creating is ScriptElement ) {
                sources = creating as IToneScriptSource;
                created = null;
                parseInput();
            } return created != null;
        }

        public ToneToken ElementarTrackType( string line )
        {
            if( line.Contains("wav") ) {
                return ToneToken.wav;
            } else return (ToneToken)ToneToken.Parse(
                typeof(ToneToken), line.Substring(0,3) );
        }

        public Element parseLine( string line )
        {
            Token type = DeclaresType( line );
            switch( type.scope ) {
                case ScopeType.OpenScope: break;
                case ScopeType.Master: break;
                case ScopeType.Track: break;
                case ScopeType.Elementar:
                    switch( InScope.type ) {
                        case ScopeType.Master: {
                            new Scope( InScope.element.Add<MixTrack,ScriptSource>(), InScope, ScopeType.Track );
                        return parseLine( line ); }
                        case ScopeType.Track: {
                            Initializer newElm = null;
                            switch ( type.token ) {
                                case ToneToken.wav: newElm = TryGetParameter<SourceSample>( line, null ); break;
                                case ToneToken.osc:
                                case ToneToken.sin:
                                case ToneToken.pls:
                                case ToneToken.tri:
                                case ToneToken.saw: newElm = TryGetParameter<OSC>( line, null ); break;
                            } new Scope( newElm.Create( InScope.element ), InScope, ScopeType.Elementar );
                        break; }
                    } break;
                case ScopeType.Modulator: break;
                case ScopeType.Effect: break;
                case ScopeType.Sequence: break;
            } return created;
        }

        public string DeclaresName( string line )
        {
            if( line.Contains("[") ) {
                int namIdx = line.IndexOf('[')+1;
                return line.Substring(namIdx, line.IndexOf(']') - namIdx);
            } else if( line.Contains("nam") ) {
                return line.Split('=')[1].Trim();
            } else return "";
        }

        public static Preci correctFreqForFORM( Preci freq, FORM form )
        {
            if (form == FORM.tri || (int)form == 8)
                return (freq + freq);
            return freq;
        }

        public abstract class Initializer
        {
            public Dictionary<Initializer,Type> inits;
            public List<object> parameter; 
            public Dictionary<Delegate,object[]> custom;
            public abstract Element Create(Element attachto);
        } 

        public class Initializer<E> : Initializer where E : Element, new()
        {
            public E element;
            
            public Initializer()
            {
                custom = new Dictionary<Delegate,object[]>(0);
                parameter = new List<object>(0);
                inits = new Dictionary<Initializer,Type>(0);
                element = new E();
            }
            public Initializer(params object[] parameters) : this()
            {
                parameter.AddRange(parameters);
            }

            public override Element Create( Element attach )
            {
                if ( parameter.Count > 0 ) {
                    element.Init( attach, parameter.ToArray() );
                } else element.Init( attach );
                foreach( KeyValuePair<Initializer,Type> it in inits ) {
                    element.Add( it.Key.Create( element ), it.Value );
                } return element as E;
            }
        }

        public bool parseLFO<ET>( string line, PARAMETER target, Initializer<ET> init) where ET : Element, new()
        {
            string[] args = null;
            if ( line.StartsWith("~lfo(") ) {
                args = line.Substring(5,line.IndexOf(')')-5).Split(',');
            } else if ( line.StartsWith("~lfo=") ){
                args = new string[] { line.Substring(5) };
            } if ( args != null ) {
                ControlMode frm = ControlMode.PingPong; // todo make FORM parse-bar !

                Preci[] vals;
                switch( args.Length ) {
                    case 1: vals = new Preci[] { -Element.GANZ, Element.GANZ, Preci.Parse(args[0]) }; break;
                    case 2: vals = new Preci[] { -Preci.Parse(args[0]), Preci.Parse(args[0]), Preci.Parse(args[1]) }; break;
                    case 3: vals = new Preci[] { Preci.Parse(args[0]), Preci.Parse(args[1]), Preci.Parse(args[2]) }; break;
                    default: throw new IndexOutOfRangeException("lfo(min,max,frq)");
                }

                Delegate addlfo = new Action<PARAMETER,Preci,Preci,Preci,ControlMode,uint>( (init.element as ElementarTrack).AddLFO );
                init.custom.Add( addlfo, new object[] { target, vals[0], vals[1], vals[2], frm, "Find SampleRate" } );
                return true;
            } return false;
        }

        public Initializer<T> TryGetParameter<T>( string line, Initializer<T> initiator ) where T : Element, new()
        {
            int foundArgs = 0;
            if( initiator == null ) {
                initiator = new Initializer<T>();
                foundArgs = 1;
            }
            foreach( Token required in initiator.element.accepts )
            {
            if( required.token.HasFlag( ToneToken.frm ) ) { FORM form;
                if( Enum.TryParse<FORM>( line.Substring(0,3), out form ) ) {
                    initiator.parameter.Add( form );
                    ++foundArgs;
                    if( parseLFO<T>( line.Substring(3).Trim(), PARAMETER.WaveForm, initiator ) )
                        ++foundArgs;
                    line = ReadNextParameter();
                }
            } else if ( required.token.HasFlag(ToneToken.frq) ) {
                if (line.StartsWith("frq")) {
                    string frqline = line.Substring(3).Trim();
                    if( frqline.StartsWith("=") ) {
                        Preci[] frqprm = readSeveralFloats(frqline.Substring(1).Trim())[0];
                        initiator.parameter.Add(frqline);
                        ++foundArgs;
                        line = ReadNextParameter();
                    }
                }
            } else if ( required.token.HasFlag( ToneToken.wav ) ) {
                string fileline = null; 
                if( line.Contains("wav") ) {
                    fileline = line.Substring(line.IndexOf("wav") + 1);
                } else if ( line.Contains("nam") ) {
                    fileline = line.Substring(line.IndexOf("nam") + 1);
                } if (fileline != null) {
                    initiator.parameter.Add(fileline);
                    ++foundArgs;
                    line = ReadNextParameter();
                }
            } else if( required.token.HasFlag( ToneToken.pan ) ) {
                if ( line.StartsWith("pan") ) {
                string panline = line.Substring(3).Trim(); 
                    if ( panline.StartsWith("=") ) {
                        Preci[][] values = readSeveralFloats( panline.Substring(1) );
                        if ( values[0][0] == values[1][0] && values[0][1] == values[1][1] ) {
                            if ( values[0][0] != Element.HALB || values[0][1] != Element.HALB ) {
                                initiator.inits.Add( new Initializer<PanoramaValue>(values), typeof(List<PanoramaParameter>) );
                            } else initiator.inits.Add( new Initializer<PanoramaParameter>(),typeof(List<PanoramaParameter>) );
                        } else {
                            initiator.inits.Add( new Initializer<PanoramaValue>(values), typeof(List<PanoramaParameter>) );
                        } ++foundArgs;
                        line = ReadNextParameter();
                    }
                } 
            } else if ( required.token.HasFlag( ToneToken.amp ) ) {
                if ( line.StartsWith("amp") || line.StartsWith("vol") ) {
                    string ampline = line.Substring(3).Trim(); 
                    if( ampline.StartsWith("=") ) {
                        Preci[][] values = readSeveralFloats( Element.GANZ, ampline.Substring(1) );
                        if( values[0][0] == values[1][0] && values[0][1] == values[1][1] ) {
                            if( values[0][0] == values[0][1] ) {
                                if( values[0][0] == Element.GANZ ) {
                                    initiator.inits.Add( new Initializer<ModulationParameter>(), typeof(List<ModulationParameter>) );
                                } else {
                                    initiator.inits.Add( new Initializer<ModulationValue>(values[0][0]), typeof(List<ModulationParameter>) );
                                }
                            } else initiator.inits.Add( new Initializer<LinearValueChange>( values[0][0], values[0][1] ), typeof(List<ModulationParameter>) );
                        } else initiator.inits.Add( new Initializer<EVP>( PARAMETER.Volume, values, "Find ElementLength"), typeof(List<ModulationParameter>) );
                        ++foundArgs;
                        line = ReadNextParameter();
                    } else if ( parseLFO<T>( ampline, PARAMETER.Volume, initiator ) ) {
                        ++foundArgs;
                        line = ReadNextParameter();
                    }
                }
              }
            } if ( foundArgs == 0 ) {
                return TryGetParameter<T>( ReadNextParameter(), initiator );
            } return initiator;
        }
        
        public string GetErrorMessage()
        {
            return iserror;
        }

        public bool Errors { get { return iserror != null; } set { iserror = null; } }

        public bool parseInput()
        {
            // Synthetization parameters will be requested by stdin...
            // ..or read from .ton file when --tonescript=filename.ton
            // or parsed from string containing parameters directly.
            ControlMode currentLFO = 0;
            Panorama[]  currentPan;
            Preci[]     currentFrq;
            FORM        currentFrm = 0;
            OSC         currentOSC = null;

            uint intput;
            string stringput;
            ScriptSource synth = new ScriptSource();

            if ( sources == null )
                Std.Out.WriteLine( "# filename{0}: ", verbose ? " for wave output" : "" );
            stringput = ReadNextParameter();
            if( !stringput.EndsWith(".wav") )
                 stringput += ".wav";

            if ( sources == null )
                Std.Out.WriteLine( "# duration{0}: ", verbose ? " of wave in ms" : "" );
            while ( !UInt32.TryParse( ReadNextParameter(), out intput ) )
                if ( sources == null ) Std.Err.WriteLine( "gib zahlen ein du blödmann!" );
            intput = (uint)( ((Preci)format.SampleRate / 1000.0) * intput );

            synth.Init( mainmix, new object[] { format, stringput, intput, } );

            if (sources==null) Std.Out.WriteLine("# oscillators: ");
            while( !UInt32.TryParse( ReadNextParameter(), out intput ) )
                if (sources==null) Std.Err.WriteLine( "gib zahlen ein du blödmann!" );
            
            currentPan = new Panorama[2];

            for( int i = 0; i < intput; ++i )
            { // prepare each oscillator and regarding arrays holding frequency and
              // panorama values which may change over time axis by given parameters....
                string oscNum = (i+1).ToString();
                if( sources == null ) Std.Out.WriteLine(
                    string.Format( "# osc[{0}] {1}:", oscNum, verbose
                                 ? "waveform <sin|saw|pls|tri> | <file|script>":"waveform" )
                                                      );
                currentLFO = 0;
                currentOSC = new OSC();
                Type trackType = typeof(OSC);
                string[] oscarg = new string[1];
                oscarg[0] = ReadNextParameter();
                if( oscarg[0].Contains("~") ) {
                    oscarg = oscarg[0].Split('~');
                        // TODO: add a switch which checks against file|script argument 
                        //  like:  audio~file=dindong.wav 
                        //    or:  audio~script=dongbong.ton
                        // which could be given instead of a FORM parameter
                        // and which sets trackType = typeof(AudioSource)
                        // if path indeed can be resolved, add AudioTrack element to the synth element and initialize it for loading the file
                        //   like: synth.Add<AudioSource>("dingdongbummel.wav/ton");
                        // else, if input was either sin, pls,tri or saw - set trackType = typeof(synth.osc[i]) ... and init as usual then 
                    currentFrm = (FORM)FORM.Parse( typeof(FORM), oscarg[0] );
                    for( int m = 1; m < oscarg.Length; ++m ) {
                        if ( oscarg[m].Contains("=") ) {
                            string[] modArg = oscarg[m].Split('=');
                            oscarg[m] = modArg[1];
                            currentLFO = ControlMode.PingPong;
                            if ( sources == null ) Std.Out.WriteLine("# Added lfo for oscillator {0}",i);
                            if( modArg[0] != "lfo" ) {
                                currentFrm = (FORM)FORM.Parse( typeof(FORM), modArg[0] );
                            }
                        }
                    }
                } else {
                    currentFrm = (FORM)FORM.Parse( typeof(FORM), oscarg[0] );
                }

                // Panorama
                if ( sources == null ) Std.Out.WriteLine( "# osc[{0}] {1}:", oscNum, verbose ? "position in field of sound <LR[xFR]>[~<LR[xFR]>]" : "panorama" );
                Preci[][] rp = readSeveralFloats( ReadNextParameter() );
                currentPan[0] = new Panorama( (float)rp[0][0], (float)rp[0][1] );
                currentPan[1] = new Panorama( (float)rp[1][0], (float)rp[1][1] );
                currentPan[1] = Panorama.MovingStep( currentPan[0], currentPan[1], synth.frameCount );
                
                // Frequency
                if ( sources == null ) Std.Out.WriteLine( "# osc[{0}] {1}frequency:",oscNum,verbose?"enter <start[~end]> ":"" );
                currentFrq = readSeveralFloats( ReadNextParameter(), 2 )[0];
                for ( int c = 0; c < currentFrq.Length; ++c ) {
                    currentFrq[c] = ( 2.0f / ( (Preci)format.SampleRate / (Preci)currentFrq[c] ) );
                } currentFrq[1] = ( currentFrq[1] - currentFrq[0] ) / (Preci)synth.frameCount;
                synth.osc.Add( synth.Add( currentOSC.Init( synth, new object[] { currentFrm, currentFrq, currentPan } ) as OSC ) );

                // Amplifier
                if ( sources == null ) { Std.Out.Write( "# osc[{0}] ",oscNum );
                    if (verbose) {
                        Std.Out.WriteLine("enter either: AbsoluteValue Vabs or LinearChange Vbeg~Vend or");
                        Std.Out.WriteLine("# ADSR Envelop: AlxAt[~SlxDt] (*where Rt implies by T-(At+Dt) where T=1.0 is total length):");
                    } else Std.Out.WriteLine("volume ( Absolute-Value, Linear-Change or ADSR-Envelop ):" );                        
                } currentOSC.AddEnvelop( PARAMETER.Volume, readSeveralFloats(1.0f, ReadNextParameter()), synth.frameCount );
                
                // Modulation
                for ( int l = 0; l < (oscarg.Length-1); ++l ) {
                    float lfoFrq = float.Parse( oscarg[l+1] );
                    ControlMode mode = currentOSC.osc.Mode;
                    currentFrm = (FORM)mode;
                    switch( mode ) {
                        case ControlMode.Pulse: {
                            currentOSC.AddLFO( PARAMETER.WaveForm, -0.9f, 0.9f, lfoFrq, currentLFO, format.SampleRate );
                        } break;
                        case ControlMode.Stack:
                        case ControlMode.Sinus: {
                            currentOSC.AddLFO( PARAMETER.WaveForm, -1, 1, lfoFrq, currentLFO, format.SampleRate );
                        } break;
                        case ControlMode.Delegate: {
                            currentOSC.AddLFO( PARAMETER.WaveForm, 0, 1, lfoFrq, currentLFO, format.SampleRate );
                        } break;
                        default: if (sources == null) {
                            iserror = string.Format( "# Error: unknown ControlMode of osc {0}", i );
                            lfoFrq = 0;
                            return false;
                        } break;
                    }
                    if( lfoFrq != 0 ) {
                        currentOSC.Get<LFO>().mod.SetCheckAtGet();
                        currentOSC.Get<LFO>().mod.SetReadOnly( true );
                        currentOSC.Get<LFO>().mod.Active = true;
                    }
                }
            } synth.TargetFormat = format;
            created = synth;
            return created != null;
        }

        public string ReadNextParameter()
        {
            string nextArg = "";
            if ( sources != null ) {
                nextArg = sources.NextElement;
                if( nextArg != null ) {
                    while ( nextArg.StartsWith("#") ) {
                        Std.Out.WriteLine( nextArg );
                        nextArg = sources.NextElement;
                    }
                    Std.Out.WriteLine( nextArg );
                } 
            } else {
                nextArg = Std.Inp.ReadLine();
                if( logging )
                    Std.Out.Log.WriteLine( nextArg );
            } return nextArg;
        }

        public Token DeclaresType( string token )
        {
            string declcheck;
            if( token[0] == '#' )
                declcheck = token.Substring(1).TrimStart().Substring(0,3);
            else declcheck = token.Substring(0,3);
            switch( declcheck.ToLower() ) {
                case "wav":
                case "osc":
                case "sin":
                case "tri":
                case "saw":
                case "pls": return new Token(ScopeType.Elementar,(ToneToken)Enum.Parse(typeof(ToneToken),declcheck));
                case "evp":
                case "lfo": return new Token(ScopeType.Modulator,(ToneToken)Enum.Parse(typeof(ToneToken),declcheck));
                default: return Token.Empty;
            }
        }

        private bool hasTokenOp( string parse, char op )
        {
            int tokidx = parse.IndexOf(op);
            if( tokidx >= 0 ) {
                perform.Add( op, tokidx );
                return true;
            } return false;
        }

        public OperatorType PerformsOperators( Token current, string parse )
        {
            switch( InScope + current ) {
                case OperatorType.Define: return OperatorType.Define;
                case OperatorType.Attach: return OperatorType.Attach;
                case OperatorType.Values: {
                } return OperatorType.Values;
                case OperatorType.Finish: return OperatorType.Finish;
            }
            return 0;
        }

        public static Preci[][] readSeveralFloats( string input )
        {
            return readSeveralFloats(0.5f,input,4);
        }
        public static Preci[][] readSeveralFloats( Preci def, string input )
        {
            if(def==1) def = Element.GANZ - Preci.Epsilon;
            if(def==0) def = Preci.Epsilon;
            if(input.Length==0)
                return new Preci[2][] { new Preci[]{def,Element.GANZ-def},new Preci[]{def,def} };
            return readSeveralFloats(def,input,4);
        }
        public static Preci[][] readSeveralFloats( string input, int max_count )
        {
            return readSeveralFloats(Element.HALB,input,max_count);
        }
        public static Preci[][] readSeveralFloats( Preci def, string input, int max_count )
        {
            char level = '\0';
            if (max_count > 1)
                level = input.Contains("~")
                ? '~' : input.Contains("x")
                ? 'x' : '\0';
            switch (level)
            {
                case '~': {
                    string[] inputs = input.Split(level);
                    if (max_count == 2) {
                        Preci[] t = readSeveralFloats(inputs[0], max_count)[0];
                        t[1] = readSeveralFloats(inputs[1], max_count)[0][1];
                        return new Preci[2][] { t, t };
                    }
                    return new Preci[2][] {
                        readSeveralFloats(inputs[0],max_count)[0],
                        readSeveralFloats(inputs[1],max_count)[0]
                    };
                } break;
                case 'x': {
                    string[] inputs = input.Split(level);
                    Preci A = readSeveralFloats(inputs[0], max_count)[0][0];
                    Preci B = readSeveralFloats(inputs[1], max_count)[0][0];
                    return (max_count == 2)
                         ? new Preci[2][] { new Preci[] { A, A }, new Preci[] { B, B } }
                         : new Preci[2][] { new Preci[] { A, B }, new Preci[] { A, B } };
                } break;
                case '\0': {
                    Preci[][] vals = new Preci[2][]{new Preci[2],new Preci[2]};
                    if (!Preci.TryParse(input.Replace('.', ','), out vals[0][0])) {
                        vals[0][0] = vals[1][0] = def;
                    } else {
                        vals[1][0] = vals[0][0];
                    }
                    vals[0][1] = max_count > 2 ? Element.GANZ-def : vals[0][0];
                    vals[1][1] = max_count > 2 ? def : vals[1][0];
                    return vals;
                }
            }
            return null;
        }
    }

    public class ScriptInterpreter : Element
    {
        public override uint GetElementCode()
        {
            return (uint)GetHashCode();
        }

        public override Element Init(Element attach)
        {
            return base.Init(attach);
        }

        public override Element Init(Element attach, object[] initializations)
        {
            return base.Init(attach, initializations);
        }

        public override string ToString()
        {
            return base.ToString();
        }

        protected override void update()
        {
            uint input = 0;
            string script = string.Empty;
            if((input = (uint)StdStream.Inp.CanRead())>0)
                script = StdStream.Inp.Read(input/2);
            base.update();

        }
    }
}

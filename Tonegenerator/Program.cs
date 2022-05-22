#define EFFECTS

using Stepflow.Audio.Elements;
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

using Std = Consola.StdStream;

namespace Stepflow.Audio
{
    class Program
    {
        static Consola.StdStreams consola = null;

        static async void waitForRenderFinished(System.Threading.Tasks.Task rendertask)
        {
            await rendertask;
        }

        static void Main( string[] args )
        {
            bool logging = false;
            string logfile = "";
            for ( int i = 0; i < args.Length; ++i ) {
                if( args[i].StartsWith("--log") ) {
                    logfile = args[i].Contains("=")
                            ? args[i].Split('=')[1]
                            :"ToneGenerator.log";
                    logging = true; break;
                }
            }  if ( logging ) {
                consola = new Consola.StdStreams( Consola.CreationFlags.TryConsole, logfile );
            } else {
                consola = new Consola.StdStreams( Consola.CreationFlags.TryConsole );
            }
            MessageLogger.SetLogWriter( consola.Out.WriteLine );
            MessageLogger.SetErrorWriter( consola.Err.WriteLine );

#if DEBUG
            MessageLogger.LogAlleInfos();
#else
            MessageLogger.LogNurSchlimmeFehler();
#endif

            PcmFormat fmt;
            fmt.BitsPerSample = 16;
            fmt.NumChannels = 2;
            fmt.SampleRate = 44100;
            fmt.Tag = PcmTag.PCMs;
            string script = null;
            string optmix = null;
            string output = null;
            foreach ( string arg in args ) {
                if (arg.Contains("help") || arg == "/?" || arg == "-h" ) {
                    ToneGenerator.showHelpScreen();
                    return;
                } 
                if( arg.StartsWith("--") ) {
                    if (arg.EndsWith("bit")) {
                        fmt.BitsPerSample = ushort.Parse(
                            arg.Replace("--","").Replace("bit","")
                                                          );
                    } else if ( arg.Contains("=") ) {
                        string[] par = arg.Split('=');
                        par[0] = par[0].Replace("--", "");
                        switch (par[0])
                        {
                            case "ton":
                            case "tonescript": {
                                script = par[1];
                            } break;
                            case "mix":
                            case "mixinput": {
                                optmix = par[1];
                                consola.closeLog();
                            } break;
                            case "out":
                            case "outputfile": {
                                output = par[1];
                            } break;
                            default: {
                                ToneGenerator.showHelpScreen();
                                consola.Err.WriteLine(
                                   "ERROR: Unknown parameter '{0}'...\n",
                                                par[0] );
                            } break;
                        }
                    } else switch( arg.Replace("--","") ) {
                        case "mono": fmt.NumChannels = 1; break;
                        case "stereo": fmt.NumChannels = 2; break;
                        case "quadro": fmt.NumChannels = 4; break;
                        case "5.1": fmt.NumChannels = 6; break;
                        case "7.1": fmt.NumChannels = 8; break;
                    } 
                } else {
                    uint frq;
                    if( uint.TryParse(arg, out frq) )
                        fmt.SampleRate = frq;
                }
            }
            fmt.BlockAlign = (ushort)((fmt.BitsPerSample>>3)*fmt.NumChannels);
            fmt.ByteRate = fmt.BlockAlign * fmt.SampleRate;
            fmt.Tag = fmt.BitsPerSample >= 32 ? PcmTag.PCMf : PcmTag.PCMs;

            
            MixTrack track = ToneGenerator.parseParameters( ref fmt, logging, script );
            ToneGenerator.parser.OutputMixer.AddTrack( track );
            
            if ( optmix != null ) {
                AudioSource mixin = new AudioSource();
                if (optmix == "" || optmix == "stdin") {
                    mixin.Init( mixin, consola, FILE.ReadOnDemand );
                } else mixin.Init( mixin, optmix, FILE.ReadOnDemand );
                consola.Out.WriteLine( "Mixing into input stream" );
                ToneGenerator.parser.OutputMixer.AddTrack( mixin );
                
            }

            bool OutputIsFileWriter = true;
            ToneGenerator.parser.OutputMixer.AttachOutputStream( new WaveFileWriter(track.Name, ref fmt) );

#if EFFECTS
            MasterTrack master = ToneGenerator.parser.OutputMixer;
            ThreBandEQ.Insert eq = ToneGenerator.parser.OutputMixer.GetTrack(0).Fx<ThreBandEQ.Insert>();
            eq.fxImpl().InGain = Element.HALB;
            eq.fxImpl().LoGain = Element.GANZ;
            eq.fxImpl().HiGain = Element.GANZ;
            eq.fxImpl().MiGain = Element.NULL;
            eq.fxImpl()[ThreBandEQ.PARAMETERS.HI] = track.Add<LFO>(PARAMETER.FxPara, (Preci)0.0, (Preci)0.25, (Preci)2.0, ControlMode.PingPong, (Preci)master.format.SampleRate);
            eq.fxImpl()[ThreBandEQ.PARAMETERS.LO] = track.Add<LFO>(PARAMETER.FxPara, (Preci)0.0, (Preci)1.0, (Preci)2.5, ControlMode.Sinus, (Preci)master.format.SampleRate);
            eq.fxImpl().Compression = Compress.POST;
            eq.DryWet.actual = (Preci)1.0;
            eq.ByPass = false;

            master.AddSendEffect<MultiLayerDelay.Send>((Preci)0.25, (Preci)2.0);
            track.Fx<EffectSend<MultiLayerDelay.Send>>().Send = (Preci)0.75;

            if (master.tracks > 1)
            {
                master.GetTrack(1).Fx<EffectSend<MultiLayerDelay.Send>>().Send = Element.GANZ;
                master.GetTrack(0).amp.actual = Element.HALB;
                consola.Out.WriteLine("tracks attached to master: {0}", master.tracks);
            }
            master.GetSendEffect<MultiLayerDelay.Send>().Return.value = (Preci)0.75;
#endif

            ToneGenerator.render( ToneGenerator.parser.OutputMixer );

            if( OutputIsFileWriter ) {
                (ToneGenerator.parser.OutputMixer.output as WaveFileWriter).Flush();
                (ToneGenerator.parser.OutputMixer.output as WaveFileWriter).Close();
            }

          /*

            Renderer renderer = new RealtimeRenderer();
            
            renderer.AttachRenderSource( ToneGenerator.parseParameters( ref fmt, logging, script ) );
            renderer.AttachRenderTarget( new XnaAudioOutput( 44100, 4096 ) );

            renderer.Start();

            
            //waitForRenderFinished( (renderer as RealtimeRenderer).task().assist().driver.Tribune() ); 
            //System.Runtime.CompilerServices.TaskAwaiter fertigerster = (renderer as RealtimeRenderer).task().assist().driver.Tribune().GetAwaiter();

            

            //fertigerster.GetResult();
            string exitFragezeichen = Consola.StdStream.Inp.ReadLine();
           */
        }
    }
}

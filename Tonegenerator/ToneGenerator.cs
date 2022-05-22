//css_ref linkage\ControlledValues.dll
//css_ref linkage\WaveFileHandling.dll
//css_ref linkage\StdStreams.dll
//css_ref linkage\Int24Types.dll
//css_co /unsafe /D:SCRIPT

#if SCRIPT
#define X86_64
#else
#define COMPILE
#endif

using System;
using Stepflow.Audio.Elements;
using Std = Consola.StdStream;
#if X86_64
using Preci = System.Double;
using ControlledPreci = Stepflow.Controlled.Float64;
#elif X86_32
using Preci = System.Single;
using ControlledPreci = Stepflow.Controlled.Float32;
#endif


namespace Stepflow
{
    namespace Audio
    {
        public class ToneGenerator
        {
            protected const Preci GANZ = (Preci)1;
            protected const Preci HALB = (Preci)1/(Preci)2;
            protected const Preci NULL = 0;

            public static ToneScriptParser parser { get; private set; }
            public static MasterTrack      master { get { return parser.OutputMixer; } }
            public static PcmFormat        format;

            public static void showHelpScreen()
            {
                Std.Out.WriteLine("\nSynopsys: ToneGenerator [SampleRate *44100*] [Options]");
                Std.Out.WriteLine("\nOptions:  (*'s = default)\n");
                Std.Out.WriteLine("    --[*16|24|32|64]bit               :    datatype of output file.");
                Std.Out.WriteLine("    --[mono|*stereo|quadro|5.1|7.1]   :    output file channel constelation.");
                Std.Out.WriteLine("    --[tonescript=<filename>]         :    parse synthetization parameters from");
                Std.Out.WriteLine("                                           file (.ton) instead of reading stdin.");
                Std.Out.WriteLine("    --[mixinput=<filename|*stdin>]    :    mix generated tone into given stream");
                Std.Out.WriteLine("                                           instead of generating just new tone.");
                Std.Out.WriteLine("    --[outputfile=<filename|*stdout>] :    override any filename which tonescript");
                Std.Out.WriteLine("                                           could contain for defining the output");
                Std.Out.WriteLine("    --log[=filename]                  :    will log any input parameters to [filename]");
                Std.Out.WriteLine("                                           (*clue: such logfile will consist from strings");
                Std.Out.WriteLine("                                            of matching syntax accepted as input paramers");
                Std.Out.WriteLine("                                            which means: [filename] later can be given as");
                Std.Out.WriteLine("                                            --tonescript parameter argument. It will");
                Std.Out.WriteLine("                                            generate again identical wave output so.\n\n");
            }

            public static MixTrack parseParameters( ref PcmFormat format, bool enableLogging, string parameters )
            {
                if (parser == null) {
                    parser = new ToneScriptParser( format, enableLogging, false);
                } else if( master.TargetFormat.FrameType.Code != format.FrameType.Code ) {
                    master.SetTargetFormat( format );
                }
                
                if( parameters != null ) {
                    parser.loadScript( parameters );
                } else {
                    parser.VerboseLogging( true );
                    parser.parseInput();
                } if ( parser.Errors ) {
                    Std.Err.WriteLine( "ERROR: {0}", parser.GetErrorMessage() );
                    return null;
                }
                
                return parser.CreatedTrack;
            }

            // render the passed audio track via master output stream 
            public static uint render( MixTrack track )
            {
                if ( track is MasterTrack ) {
                    if ( track != master ) {
                        parser.OutputMixer = track as MasterTrack;
                    }
                } else {
                    format = track.GetTargetFormat();
                    master.AddTrack( track );
                    master.AttachOutputStream( master.output );
                }
                uint renderframes = master.FrameCount;
                for ( uint frame = 0; frame < renderframes; ++frame ) {
                    master.Update();
                } return renderframes;
            }


#if SCRIPT
            public static void Main( string[] args )
            {
                parser = new ToneScriptParser( true, false );
                parser.logging = false;
                string logfile = "";
                for  (int i = 0; i < args.Length; ++i ) {
                    if( args[i].StartsWith("--log") ) {
                        logfile = args[i].Contains("=")
                                ? args[i].Split('=')[1]
                                :"ToneGenerator.log";
                        parser.EnableLogging( true ); break;
                    }
                }  if ( parser.logging ) {
                    Std.Init( CreationFlags.TryConsole, logfile );
                } else {
                    Std.Init( CreationFlags.TryConsole );
                }

                PcmFormat format = new PcmFormat();
                format.SampleRate = 44100;
                format.BitsPerSample = 16;
                format.NumChannels = 6;
#else //COMPILE#

            public static uint render(MixTrack track,bool asynchron)
            {
                if (!asynchron) return render( track );
                else { // TODO
                    MessageLogger.logInfoWichtig("asynchronuous rendering not supported yet!");
                    return 0;
                }
            }

            public static void generateTone( IAudioOutStream destination, string tonescript )
            {
                format = destination.GetFormat();

                if (parser == null)
                    parser = new ToneScriptParser( new MasterTrack( format ), true, false );

                bool shouldLoadScript = false;
                Std.Init( Consola.CreationFlags.TryConsole );
                string[] args;
                if( !tonescript.EndsWith(".ton") ) {
                    shouldLoadScript = true;
                    args = new string[3];
                } else {
                    args = new string[4];
                    args[3] = "--tonescript=" + tonescript;
                } args[0] = destination.GetFormat().SampleRate.ToString();
                args[1] = "--" + destination.GetFrameType().BitDepth.ToString() + "bit";
                args[2] = destination.GetFrameType().ChannelCount == 1 ? "--mono"
                        : destination.GetFrameType().ChannelCount == 2 ? "--stereo"
                        : destination.GetFrameType().ChannelCount == 4 ? "--quadro"
                        : destination.GetFrameType().ChannelCount == 6 ? "--5.1"
                        : destination.GetFrameType().ChannelCount == 8 ? "--7.1"
                        : "";
#endif //COMPILE


                bool verbose = false;

                for( int i = 0; i < args.Length; ++i )
                {
                    if( (args[i] == "/?") || (args[i] == "-h") || (args[i] == "/h") ) {
                        showHelpScreen(); return;
                    }
                    if( !verbose ) {
                         verbose = args[i] == "-v";
                    }
                    if( args[i].StartsWith("--") ) {
                        string currentArg = args[i].Substring(2);

                        if ( currentArg.EndsWith("bit") ) {
                            ushort.TryParse( currentArg.Substring(0, 2),
                                             out format.BitsPerSample );
                        } else
                        if ( currentArg.StartsWith("tonescript") ) {
                            System.IO.FileInfo f = new System.IO.FileInfo(
                                                 currentArg.Split('=')[1] );
                            if( !f.Exists ) {
                                string error = string.Format(
                                    "ERROR: tonescript file '{0}' can't be opened!\n",
                                                     f.FullName );
                                Std.Err.WriteLine( error );
                                showHelpScreen();
                                Std.Err.WriteLine( error );
                                return;
                            } tonescript = f.FullName;
                            shouldLoadScript = true;
                        } else
                        switch ( currentArg ) {
                            case "mono":   format.NumChannels = 1; break;
                            case "stereo": format.NumChannels = 2; break;
                            case "quadro": format.NumChannels = 4; break;
                            case "5.1":    format.NumChannels = 6; break;
                            case "7.1":    format.NumChannels = 8; break;
                            case "help":   showHelpScreen(); return;
                        default: break; }
                    } else
                    if( !UInt32.TryParse( args[i], out format.SampleRate ) ) {
                        format.SampleRate = 44100;
                    }
                }

                format.BlockAlign = (ushort)( format.NumChannels * (format.BitsPerSample >> 3) );
                format.ByteRate = format.SampleRate * format.BlockAlign;
                format.Tag = format.BitsPerSample >= 32 ? PcmTag.PCMf : PcmTag.PCMs;

                parser.SetOutputFormat( format );
                if ( shouldLoadScript )
                    parser.loadScript( tonescript );
                else
                    parser.parseInput();
                if( parser.Errors ) {
                    Std.Err.WriteLine( parser.GetErrorMessage() );
                    return;
                }

#if SCRIPT
                WaveFileWriter destination = new WaveFileWriter();
                destination.Open( parser.CreatedTrack.Get<ElementName>(), ref format );
                Std.Out.WriteLine( destination.GetFrameType().CreateEmptyFrame().GetType().ToString() );
                Std.Out.WriteLine( string.Format( "will write {3} frames {2}channels {0}kHz {1}bit audio",
                                                  format.SampleRate, format.BitsPerSample,
                                                  format.NumChannels, synth.frameCount ) );
#endif
                // do wave generation into AudioStream 'destination'
                generateWaveForm( destination, parser.CreatedTrack );
#if SCRIPT
                destination.Flush();
                destination.Close();
#endif
            }



            private static void generateWaveForm( IAudioOutStream targetstream, MixTrack sourcetrack )
            {
                if( master != sourcetrack )
                    master.AddTrack( sourcetrack );
                master.AttachOutputStream( targetstream );
                render( master );
            }
        };
    }
}
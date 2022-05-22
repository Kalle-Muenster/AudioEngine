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
	public class FormantFilter : FxPlug
	{
		public enum PARAMETERS
		{
			A, E, I, O, U     // 0=A,1=E,2=I,3=O,4=U
		}

		/*
		Public source code by alex@smartelectronix.com
		Simple example of implementation of formant filter
		Vowelnum can be 0,1,2,3,4 <=> A,E,I,O,U
		Good for spectral rich input like saw or square
		*/

		//-------------------------------------------------------------VOWEL COEFFICIENTS
		private static Preci[][] Vowels = {
			new Preci[] { 8.11044e-06f,
				8.943665402f,    -36.83889529f,    92.01697887f,    -154.337906f,    181.6233289f,
				-151.8651235f,   89.09614114f,    -35.10298511f,    8.388101016f,    -0.923313471f  ///A
			},
			new Preci[] {4.36215e-06f,
				8.90438318f,    -36.55179099f,    91.05750846f,    -152.422234f,    179.1170248f,  ///E
				-149.6496211f, 87.78352223f,    -34.60687431f,    8.282228154f,    -0.914150747f
			},
			new Preci[] { 3.33819e-06f,
				8.893102966f,    -36.49532826f,    90.96543286f,    -152.4545478f,    179.4835618f,
				-150.315433f,    88.43409371f,    -34.98612086f,    8.407803364f,    -0.932568035f  ///I
			},
			new Preci[] {1.13572e-06f,
				8.994734087f,    -37.2084849f,    93.22900521f,    -156.6929844f,    184.596544f,   ///O
				-154.3755513f,    90.49663749f,    -35.58964535f,    8.478996281f,    -0.929252233f
			},
			new Preci[] {4.09431e-07f,
				8.997322763f,    -37.20218544f,    93.11385476f,    -156.2530937f,    183.7080141f,  ///U
				-153.2631681f,    89.59539726f,    -35.12454591f,    8.338655623f,    -0.910251753f
			}
        };
		//---------------------------------------------------------------------------------

		private Preci[][][]    state;
		private AudioFrameType stype;
		private ushort         scode;
		private uint           srate;


		private FormantFilter( Effect inst ) : base(inst)
		{
			elm.Add<ElementName>( GetType().Name );
			stype = FrameTypes.AuPCMs24bit2ch.type;
			srate = 44100;
		}

		public class Insert : InsertEffect<FormantFilter>, IInsert
		{
			public Insert() : base()
			{
				impl = new FormantFilter(this);
				FxType = EffectType.Filter | EffectType.Voice;
			}
		}

		public class Send : SendEffect<FormantFilter>, ISend
		{
			public Send() : base()
			{
				impl = new FormantFilter(this);
				FxType = EffectType.Filter|EffectType.Voice;
			}
		}

		public override Element Init( Element attach, params object[] inits )
        {
			if ( inits.Length > 0 )
			if ( inits[0] is AudioFrameType ) {
				stype = (AudioFrameType)inits[0];
				if ( inits.Length > 1 ) srate = (uint)inits[1]; 
			} else if ( inits[0] is PcmFormat ) {
				PcmFormat fmt = (PcmFormat)inits[0];
				stype = fmt.FrameType;
				srate = fmt.SampleRate;    
				fmt.BitsPerSample = sizeof(Preci) * 8;
				fmt.Tag = PcmTag.PCMf;
			    scode = fmt.FrameType.Code;
			}
			output = stype.CreateEmptyFrame();

			state = new Preci[stype.ChannelCount][][];
			for (int i = 0; i < stype.ChannelCount; ++i ) {
				state[i] = new Preci[][] { new Preci[10], new Preci[10], new Preci[10], new Preci[10], new Preci[10] };
			}
			for (int i = 0; i < 5; ++i) {
				elm.Add<ModulationParameter,ModulationPointer>( PARAMETER.FxPara, (Preci)1.0 ).pointer = IntPtr.Zero;
            }
			return elm.Init(attach);
        }

		public override Array Parameters {
			get	{ int num = 0;
				ModulationPointer[] Ps = new ModulationPointer[5];
				IEnumerator<ModulationParameter> p = elm.All<ModulationParameter>();
				while (p.MoveNext()) Ps[num++] = p.Current as ModulationPointer;
			return Ps; }
		}

		public override bool ByPass { get; set; }

		public override IElmPtr<Preci> this[int parameter] {
			get { return elm.Get<ModulationParameter>(parameter).elmptr(); }
			set { elm.Get<ModulationParameter>(parameter).elmptr().SetTarget( value.pointer ); }
		}

		//---------------------------------------------------------------------------------

		public IElmPtr<Preci> GetParameter( PARAMETERS id )
        {
			return this[(int)id];
        }

		public override IAudioFrame DoFrame( IAudioFrame /*dry*/ input )
		{
			output.Set( input.Convert( scode ) );
			for ( int c = 0; c < stype.ChannelCount; ++c ) {
				Preci chanmix = 0;
				Preci channel = (Preci)output.get_Channel(c);
				for ( int v = 0; v < 5; ++v )	{
					Preci res = (
						Vowels[v][0] * channel + 
						Vowels[v][1] * state[c][v][0] +
						Vowels[v][2] * state[c][v][1] +
						Vowels[v][3] * state[c][v][2] +
						Vowels[v][4] * state[c][v][3] +
						Vowels[v][5] * state[c][v][4] +
						Vowels[v][6] * state[c][v][5] +
						Vowels[v][7] * state[c][v][6] +
						Vowels[v][8] * state[c][v][7] +
						Vowels[v][9] * state[c][v][8] +
					   Vowels[v][10] * state[c][v][9] );

					state[c][v][9] = state[c][v][8];
					state[c][v][8] = state[c][v][7];
					state[c][v][7] = state[c][v][6];
					state[c][v][6] = state[c][v][5];
					state[c][v][5] = state[c][v][4];
					state[c][v][4] = state[c][v][3];
					state[c][v][3] = state[c][v][2];
					state[c][v][2] = state[c][v][1];
					state[c][v][1] = state[c][v][0];
					state[c][v][0] = res;
					chanmix += res * this[v].actual;
				} output.set_Channel( c, chanmix );
			} return /*wet*/ output.Convert( stype );
		}

		public override void ApplyOn( ref IAudioFrame frame )
		{
			if (!ByPass) elm.ApplyOn( ref frame );
		}

	}
}


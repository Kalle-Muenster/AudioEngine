//#define Yes
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


#if Yes
namespace Stepflow.Audio.Elements
{
    public class ParameterTrigger : ModulationValue, IElmPtr<Preci>
    {
		private IntPtr ptr;
		private int    idx;

		IntPtr IElmPtr<Preci>.pointer {
			get { return ptr; }
			set { ptr = value; }
		}

        public override Element Init( Element attach ) {
		    idx = attach.Idx<ModulationParameter>( this );
            return base.Init( attach );
        }

        public override IElmPtr<Preci> elmptr() {
            return this;
        }

        public override Preci actual {
			get { unsafe { return *(Preci*)ptr.ToPointer() * value; } }
			set { unsafe { *(Preci*)ptr.ToPointer() = value / this.value;
					attached.Get<BarrierFlags>()[idx] = BarrierFlags.State.Lock;
				}
			}
		}

        void IElmPtr<Preci>.SetProportion( Preci proper ) {
			value = proper;
        }

        void IElmPtr<Preci>.SetTarget( ref Preci variable ) {
            unsafe { fixed( Preci* p = &variable ) {
				ptr = new IntPtr( p );
            } }
        }

        void IElmPtr<Preci>.SetTarget( IntPtr ptr ) {
			this.ptr = ptr;
        }
    }

	public class WhirlWah : FxPlug
	{

		public enum FxParam : byte {
			Level,   // -1 to 1
		//	Strange,      // 0 to 1	
		//	OffsetsON,    // 0 off, 1 on
			CutOffset,  // 0 to 1
			ResOffset,   // 0 to 1
		}

		public IElmPtr<Preci> this[FxParam index]
		{
			get { switch (index) {
					case FxParam.CutOffset: return elm.Get<ModulationParameter, WhirlFxPtr>();
				} return null; }
			set { switch (index) {
					case FxParam.CutOffset: elm.Get<ModulationParameter, WhirlFxPtr>().elmptr().pointer = value.pointer; break;
				} }
		}

		private const Preci MAX_CUTOFF = (Preci)0.1;
		private const Preci MAX_RESONANCE = Element.GANZ - MAX_CUTOFF;
		private const Preci MAX_AMOUNT = Element.GANZ;
		private const Preci MIN_CUTnRESnWET = Element.NULL;

		private AudioFrameType ftype;

		public Preci r, k, p, scale, ampP2, ampPn, ampPh;


		private bool _parameterChanged;
		private int inverter;

		private Preci _cutoff;
		public Preci CutOff {
			get { return _cutoff; }
			set { _parameterChanged = true;
				_cutoff = value > MIN_CUTnRESnWET
						? value < MAX_CUTOFF
						? value : MAX_CUTOFF
						: MIN_CUTnRESnWET;

				inverter = ((_cutoff >= MAX_CUTOFF - 0.01f)
						 || (_cutoff == MIN_CUTnRESnWET)
						 ) ? -inverter : inverter;

				double _2Xcutoff = _cutoff * 2;
				p = ((k = (Preci)(3.6 * _2Xcutoff - 1.6 * _2Xcutoff * _2Xcutoff - 1)) + Element.GANZ) * Element.HALB;
				scale = (Preci)Math.Pow(Math.E, (Element.GANZ - p) * 1.386249);
			}
		}

		private Preci _resonance;
		public Preci Resonance
		{
			get { return _resonance; }
			set { _parameterChanged = _resonance != value;
				_resonance = value > MIN_CUTnRESnWET
						   ? value < MAX_RESONANCE
						   ? value : MAX_RESONANCE
						   : MIN_CUTnRESnWET;
			}
		}

		private Preci _amount;
		public Preci DryWet
		{
			get { return _amount; }
			set { _amount = value > MIN_CUTnRESnWET
						  ? value < MAX_AMOUNT
						  ? value : MAX_AMOUNT
						  : MIN_CUTnRESnWET;
			}
		}

		public Preci Level
		{
			get { return _jogValue; }
			set { if ( OffsetsDisabled ) TurnTheDial( value );
				  else TurnTheDial( value, CutOff );
			}
		}

		public bool OffsetsDisabled = false;



		public class WhirlFxPtr : ModulationParameter, IElmPtr<Preci>
        {
			private bool externPtr;
			private GetValue getfunc;
			private SetValue setfunc;

			public delegate void SetValue(Preci val);
			public delegate Preci GetValue();
			public IntPtr pointer;

			public WhirlFxPtr() : base() { externPtr = false; }

			public WhirlFxPtr( GetValue get, SetValue set, IntPtr ctof )
            {
				externPtr = false;
				getfunc = get;
				setfunc = set;
				pointer = ctof;
            }

			IntPtr IElmPtr<Preci>.pointer {
				get { return pointer; }
				set { pointer = value; }
			}

            public override Preci actual {
				get { unsafe { return *(Preci*)pointer.ToPointer() * MAX_CUTOFF; } }
				set { this.value = value; }
			}

			public Preci value {
				get { unsafe { return *(Preci*)pointer.ToPointer() = (getfunc() / MAX_CUTOFF); } }
				set { unsafe { setfunc(*(Preci*)pointer.ToPointer() = value / MAX_CUTOFF); } }
			}

            public void SetProportion( Preci proper ) {
				MessageLogger.logInfoWichtig(
					"#Info: proportion ({0}) of the Cutoff parameter cannot be changed",
				MAX_CUTOFF );
            }

            public void SetTarget( ref Preci variable ) {
                unsafe { fixed (Preci* ptr = &variable)
			        pointer = new IntPtr( ptr );
                }
            }

            public override IElmPtr<Preci> elmptr() {
				return this;
            }

			public void SetTarget( IntPtr ptr ) {
				pointer = ptr;
			}
		}

		private Preci _jogValue;
		public void TurnTheDial( Preci value )
		{
			_jogValue = value;
			CutOff = CutOff - (inverter * _jogValue) / (5000 * (CutOff - 0.1f));
			value = value < 0 ? -value : value;
			DryWet = 49 * DryWet / 50 - 3 * value * (DryWet - 1) / 10;
			Resonance = 0.8f - DryWet / 2f;
			OnParameterChange();
		}
		public void TurnTheDial( Preci value, Preci cutoffOffset )
		{
			_jogValue = value;
			CutOff = (CutOff - (inverter * _jogValue) / (5000 * (CutOff - 0.1f))) + cutoffOffset / 100;
			value = value < 0 ? -value : value;
			DryWet = 49 * DryWet / 50 - 3 * value * (DryWet - 1) / 10;
			Resonance = 0.8f - DryWet / 2f;
			OnParameterChange();
		}
		public void TurnTheDial()
		{
			_jogValue = this[0].actual;
			CutOff = (CutOff - (inverter * _jogValue) / (5000 * (CutOff - 0.1f))) + this[3].actual / 100;
			_jogValue = _jogValue < 0 ? -_jogValue : _jogValue;
			DryWet = 49 * DryWet / 50 - 3 * _jogValue * (DryWet - 1) / 10;
			Resonance = (0.8f - DryWet / 2f) + 0.25f;
			OnParameterChange();
		}

		private void OnParameterChange()
		{
			if (_parameterChanged) {
				r = Resonance * scale;
				ampP2 = (Preci)((DryWet * 1.25) * (1.0 + 2.0 * Level));
				ampPn = (Preci)(1.25 - (DryWet * 1.25));
				ampPh = (Preci)((DryWet * 1.25) / (1.0 + Level));
				_parameterChanged = false;
			}
		}

		public abstract class State
		{
			public delegate Preci ChannelGet( int channel );
			public delegate void  ChannelSet( int channel, Preci value );
			public delegate ChannelGet DataGet( int variable );
			public delegate ChannelSet DataSet( int variable );

			public AudioFrameType T;
			public DataGet getY;
			public DataSet setY;
			public DataGet getO;
			public DataSet setO;

			public abstract IAudioFrame Y(int idx);
			public abstract IAudioFrame O(int idx);
		}

		public State data;

		public class Data<Au> : State
					 where Au : IAudioFrame
        {
			public Au[] yps;
			public Au[] old;

			public Data( AudioFrameType autyp ) {
				yps = new Au[5];
				old = new Au[4];
            }

            public override IAudioFrame Y(int idx)
            {
				return yps[idx];
            }

            public override IAudioFrame O(int idx)
            {
				return old[idx];
            }
        }

		public class Stato : Data<IAudioFrame> 
		{

			public Stato(AudioFrameType autyp) : base(autyp)
			{
				for ( int i = 0; i < 4; ++i ) {
					old[i] = autyp.CreateEmptyFrame();
					yps[i] = autyp.CreateEmptyFrame();
				} yps[4] = autyp.CreateEmptyFrame();

				switch ( autyp.ChannelCount ) {
#if X86_64
					case 1:	{
						getY = (int y) => { return ((FrameTypes.AuPCMf64bit1ch)yps[y]).GetChannel; };
						getO = (int o) => { return ((FrameTypes.AuPCMf64bit1ch)old[o]).GetChannel; };
					    setY = (int y) => { return ((FrameTypes.AuPCMf64bit1ch)yps[y]).SetChannel; };
						setO = (int o) => { return ((FrameTypes.AuPCMf64bit1ch)old[o]).SetChannel; };
					} break;
					case 2: {
						getY = (int y) => { return ((FrameTypes.AuPCMf64bit2ch)yps[y]).GetChannel; };
						getO = (int o) => { return ((FrameTypes.AuPCMf64bit2ch)old[o]).GetChannel; };
						setY = (int y) => { return ((FrameTypes.AuPCMf64bit2ch)yps[y]).SetChannel; };
						setO = (int o) => { return ((FrameTypes.AuPCMf64bit2ch)old[o]).SetChannel; };
					} break;
					case 4:	{
						getY = (int y) => { return ((FrameTypes.AuPCMf64bit4ch)yps[y]).GetChannel; };
						getO = (int o) => { return ((FrameTypes.AuPCMf64bit4ch)old[o]).GetChannel; };
						setY = (int y) => { return ((FrameTypes.AuPCMf64bit4ch)yps[y]).SetChannel; };
						setO = (int o) => { return ((FrameTypes.AuPCMf64bit4ch)old[o]).SetChannel; };
					} break;
					case 6:	{
						getY = (int y) => { return ((FrameTypes.AuPCMf64bit6ch)yps[y]).GetChannel; };
						getO = (int o) => { return ((FrameTypes.AuPCMf64bit6ch)old[o]).GetChannel; };
						setY = (int y) => { return ((FrameTypes.AuPCMf64bit6ch)yps[y]).SetChannel; };
						setO = (int o) => { return ((FrameTypes.AuPCMf64bit6ch)old[o]).SetChannel; };
					} break;
					case 8: {
						getY = (int y) => { return ((FrameTypes.AuPCMf64bit8ch)yps[y]).GetChannel; };
						getO = (int o) => { return ((FrameTypes.AuPCMf64bit8ch)old[o]).GetChannel; };
						setY = (int y) => { return ((FrameTypes.AuPCMf64bit8ch)yps[y]).SetChannel; };
						setO = (int o) => { return ((FrameTypes.AuPCMf64bit8ch)old[o]).SetChannel; };
					} break;
#else
					case 1:	{
						getY = (int y) => { return ((FrameTypes.AuPCMf32bit1ch)yps[y]).GetChannel; };
						getO = (int o) => { return ((FrameTypes.AuPCMf32bit1ch)old[o]).GetChannel; };
					    setY = (int y) => { return ((FrameTypes.AuPCMf32bit1ch)yps[y]).SetChannel; };
						setO = (int o) => { return ((FrameTypes.AuPCMf32bit1ch)old[o]).SetChannel; };
					} break;
					case 2: {
						getY = (int y) => { return ((FrameTypes.AuPCMf32bit2ch)yps[y]).GetChannel; };
						getO = (int o) => { return ((FrameTypes.AuPCMf32bit2ch)old[o]).GetChannel; };
						setY = (int y) => { return ((FrameTypes.AuPCMf32bit2ch)yps[y]).SetChannel; };
						setO = (int o) => { return ((FrameTypes.AuPCMf32bit2ch)old[o]).SetChannel; };
					} break;
					case 4:	{
						getY = (int y) => { return ((FrameTypes.AuPCMf32bit4ch)yps[y]).GetChannel; };
						getO = (int o) => { return ((FrameTypes.AuPCMf32bit4ch)old[o]).GetChannel; };
						setY = (int y) => { return ((FrameTypes.AuPCMf32bit4ch)yps[y]).SetChannel; };
						setO = (int o) => { return ((FrameTypes.AuPCMf32bit4ch)old[o]).SetChannel; };
					} break;
					case 6:	{
						getY = (int y) => { return ((FrameTypes.AuPCMf32bit6ch)yps[y]).GetChannel; };
						getO = (int o) => { return ((FrameTypes.AuPCMf32bit6ch)old[o]).GetChannel; };
						setY = (int y) => { return ((FrameTypes.AuPCMf32bit6ch)yps[y]).SetChannel; };
						setO = (int o) => { return ((FrameTypes.AuPCMf32bit6ch)old[o]).SetChannel; };
					} break;
					case 8: {
						getY = (int y) => { return ((FrameTypes.AuPCMf32bit8ch)yps[y]).GetChannel; };
						getO = (int o) => { return ((FrameTypes.AuPCMf32bit8ch)old[o]).GetChannel; };
						setY = (int y) => { return ((FrameTypes.AuPCMf32bit8ch)yps[y]).SetChannel; };
						setO = (int o) => { return ((FrameTypes.AuPCMf32bit8ch)old[o]).SetChannel; };
					} break;
#endif
				}
			}
		}

		private void setupTheFilterState( AudioFrameType dato )
		{ 
		   _amount = Element.NULL;
			_cutoff = 0.05f;
		   _resonance = 0.6f;
		    inverter = 1;
		    data = new Stato(dato);
			for (int ch = 0; ch < dato.ChannelCount; ++ch) {
				data.setY(2)( ch, 1 );
			}
			
		   _jogValue = Element.NULL;
		   _parameterChanged = true;
		    k = p = scale = r = Element.NULL;
		}




		public override Element Init( Element attach, params object[] inits )
        {
            unsafe { fixed (Preci* ptr = &_cutoff) {
				elm.Add<ModulationParameter,WhirlFxPtr>( new WhirlFxPtr(() => { return CutOff; }, (Preci set) => { CutOff = set; }, new IntPtr(ptr)) );
				elm.Add<ModulationParameter,WhirlFxPtr>( new WhirlFxPtr(() => { return Resonance; }, (Preci set) => { Resonance = set; }, new IntPtr(ptr)));
			    elm.Add<ModulationParameter,WhirlFxPtr>( new WhirlFxPtr(() => { return Level; }, (Preci set) => { Level = set; }, new IntPtr(ptr)));
				} }
			return elm.Init( attach );
        }

		public void update()
		{
			if ( !OffsetsDisabled )
				TurnTheDial();
			else if ( this[FxParam.CutOffset].actual != _cutoff ) {
				TurnTheDial( this[FxParam.Level].actual, this[FxParam.CutOffset].actual );
			} else
				TurnTheDial( this[FxParam.Level].actual );
		}
		

        public override Array Parameters
		{
			get { return null; }
		}

		bool _bypass = false;
        public override bool ByPass {
			get { return _bypass; }
			set { _bypass = value; }
		}

        public override IElmPtr<Preci> this[int index] {
			get { return elm.Get<ModulationParameter>( index ).elmptr(); }
			set { elm.SetElementar<Action>( index, elm.changed ); }
	    }

		private WhirlWah( Effect element ) : base( element ) {
			elm = element;
			elm.FxType = EffectType.Filter;
		}

		public class Insert : InsertEffect<WhirlWah>, IInsert {
			public Insert() : base() {
				impl = new WhirlWah( this );
            }
        }
  
		public class Send : SendEffect<WhirlWah>, ISend {
			public Send() : base() {
				impl = new WhirlWah( this );
            }
		}


		private IAudioFrame thisO, thisY, nextY;


		// multichannel filter implemention:
		//----------------------------------
		//Type : 24db resonant lowpass
		//References : CSound source code, Stilson/Smith CCRMA paper.
		//Notes : Digital approximation of Moog VCF. Fairly easy to calculate coefficients, fairly easy to process algorithm, good sound.
		public override IAudioFrame DoFrame( IAudioFrame input )
		{
			//--Inverted feed back for corner peaking
			thisY.Set( data.Y( 4 ) );
      		data.Y( 0 ).Set( input.subtractionResult( thisY.Amp( r ) ) );
			
			//Four cascaded onepole filters (bilinear transform)
			for( int i = 0; i < 4; ++i ) {
				thisO.Set( data.O( i ) );
				thisY.Set( data.Y( i ) );
				nextY.Set( data.Y( i+1 ) );
				data.Y( i+1 ).Set( thisY.Amp( p ).Add( thisO.Amp( p ).Subtract( nextY.Amp( k ) ) ) );
			}

			//Copy the new calculated values to the "old"-buffers
			for( int i = 0; i < 4; ++i ) {
				data.O( i ).Set( data.Y( i ) );
			}

			//Clipper band limited sigmoid
			int channels = ftype.ChannelCount;
			for ( int ch = 0; ch < channels; ++ch ) {
				Preci yps4 = data.getY( 4 )( ch );
				yps4 -= (Preci)( Math.Pow( yps4, 3 ) / 6 );
				data.setY( 4 )( ch, yps4 );
			} return thisO.Set( data.Y( 4 ) );
		}

		// fx return mixer:
		//----------------------------------
		public override void ApplyOn( ref IAudioFrame frame )
        {
			if( !ByPass ) {
				thisY.Set( elm.DoFrame( thisY.Set( frame ) ) );
				nextY = thisY.subtractionResult( thisY );
				thisO.Set( frame );
				frame.Set( thisO.Amp(ampPn).Add( thisY.Amp(ampP2) ).Add( nextY.Amp(ampPh) ) );
			}
        }
    }
}
#endif

using System;

using Stepflow;
using Stepflow.Controller;
#if X86_64
using Preci = System.Double;
using ControlledPreci = Stepflow.Controlled.Float64;
using MonoPreciFrame = Stepflow.Audio.FrameTypes.AuPCMf64bit1ch;
#elif X86_32
using Preci = System.Single;
using ControlledPreci = Stepflow.Controlled.Float32;
using MonoPreciFrame = Stepflow.Audio.FrameTypes.AuPCMf32bit1ch;
#endif

namespace Tonegenerator
{

    public class SawStackTest : ControlledPreci
    {
        private Preci wave;
        private Preci form;


        private Preci controlerFunction(ref Preci val,ref Preci min,ref Preci max,ref Preci mov)
        {
            val = wave;
            wave = checkMODE( ControlMode.Cycle );
            Preci factor = (Preci)(1.0 + ((2.0 + 2.0*Math.Abs(wave))*form));
            val = (Preci)((wave * factor) / ((4.0 * form) + 1.0));
            return val;
        }

        public SawStackTest()
        {
            wave = 0;
            form = 0;
        }

        public override void SetUp(Preci min, Preci max, Preci mov, Preci val, ControlMode mod)
        {
            base.SetUp( min, max, mov, val, ControlMode.None );
            AttachedDelegate = controlerFunction;
        }

        public override IntPtr GetPin( Enum pinnum )
        {
            if( pinnum.CompareTo(RampFormer.FORM) == 0 ) {
                unsafe { fixed( Preci* ptFORM = &form ) return new IntPtr(ptFORM); }
            } else if ( pinnum.CompareTo(RampFormer.WAVE) == 0 ) {
                unsafe { fixed( Preci* ptWAVE = &wave ) return new IntPtr(ptWAVE); }
            } else return base.GetPin( pinnum );
        }

    }
}

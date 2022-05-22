using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Stepflow;
using Stepflow.Audio;
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

#if Yes
namespace Stepflow.Audio.Elements
{

    public enum FlagBitState : uint { Ein, Aus }
    public enum FlagBitMasks : uint
    {
        One = 0x01, Two = 0x02, Tri = 0x04, For = 0x08,
        Fiv = 0x10, Six = 0x20, Svn = 0x40, Sgn = 0x80
    }

    public struct FlagBitShift
    {
        public const uint One = 0; public const uint Two = 1; public const uint Tri = 2; public const uint For = 3;
        public const uint Fiv = 4; public const uint Six = 5; public const uint Svn = 6; public const uint Sgn = 7;
        
        public int pos;

        public FlagBitShift(uint init) { pos = (int)init; }
        public static implicit operator FlagBitMasks(FlagBitShift cast) {
            return (FlagBitMasks)(0x01u << cast.pos);
        }
        public static implicit operator FlagBitShift(uint cast) {
            return new FlagBitShift(cast);
        }
    }

    public interface IFlagBit
    {
        public int          pos { get; }
        public FlagBitMasks bit { get; }
        public FlagBitState bin { get; set; }
    }

    [StructLayout( LayoutKind.Explicit, Size = 1 )]
    public struct FlagBitOne : IFlagBit
    {
        [FieldOffset(0)]
        public FlagBitMasks ich;

        public const FlagBitMasks bit = FlagBitMasks.One;
        public static readonly FlagBitShift idx = FlagBitShift.One;
        public FlagBitState bin { get { return (FlagBitState)( (int)(ich & bit) >> idx.pos ); } set { if (value == FlagBitState.Ein) ich |= bit; else ich &= bit; } }

        int IFlagBit.pos => idx.pos;
        FlagBitMasks IFlagBit.bit => bit;
    }

    [StructLayout(LayoutKind.Explicit, Size = 1)]
    public struct FlagBitTwo
    {
        public const ElementFlags.Flags bin = ElementFlags.Flags.Two;
        [FieldOffset(0)]
        public ElementFlags.Flags bit;
    }

    [StructLayout(LayoutKind.Explicit, Size = 1)]
    public struct FlagBit
    {
        public enum State : uint { Off, Set }
        public enum State : uint { Off, Set }

        [FieldOffset(0)]
        public ElementFlags.Flags bit;
    }
    [StructLayout(LayoutKind.Explicit, Size = 1)]
    public struct FlagBit
    {

        public enum State : uint { Off, Set }

        [FieldOffset(0)]
        public ElementFlags.Flags bit;
    }
    [StructLayout(LayoutKind.Explicit, Size = 1)]
    public struct FlagBit
    {

        public enum State : uint { Off, Set }

        [FieldOffset(0)]
        public ElementFlags.Flags bit;
    }
    [StructLayout(LayoutKind.Explicit, Size = 1)]
    public struct FlagBit
    {

        public enum State : uint { Off, Set }

        [FieldOffset(0)]
        public ElementFlags.Flags bit;
    }

    [StructLayout(LayoutKind.Explicit, Size = 1)]
    public struct FlagBits
    {
        [FieldOffset(0)]
        public FlagBit One,Two,Tri,For,Fiv,Six,Svn,Sgn;
    }

    public interface IElementFlags
    {
        UInt32 value { get; set; }
        UInt32 masks(long idx); 
        IElementFlags flags();


    }



    [StructLayout( LayoutKind.Explicit, Size = 4 )]
    public unsafe struct ElementFlags
    {



        
        [FieldOffset(0)]
        public UInt32   bits;
        [FieldOffset(0)]
        public FlagBits lower;
        [FieldOffset(1)]
        public FlagBit* lomid;
        [FieldOffset(2)]
        public FlagBit* upmid;
        [FieldOffset(3)]
        public FlagBit* upper;

        public ElementFlags(uint mask)
        {
            
        }
    }

    public class ControlFlags : ElementFlags
    {

    }

    public class Xpow2Flipper : OSC
    {
        public Preci Frequency {
            get { return d < NULL ? -d : d; }
            set { d = d < NULL ? -value : value; }
        }
        

        public sbyte Count {
            get { return _Count; }
            set { _Count = value > 3
                     ? 0 : value < 0 
                     ? 3 : value; }
        }

        public override Element Init( Element attach, params object[] parameter )
        {
            return Init(attach, parameter);
        }

        public  bool Flipping;
        private int _Count;
        private Int24 sample;
        private AuPCMs24bit4ch frame;
        private int   flip = 0;
        private Preci X = -1;
        private Preci lastX = -1;
        private Preci d = 1;
        private int   last = 0;
        private bool  Flip = false;
        private int   count = 0;
        private ushort typ;

        public override void FillFrame( ref IAudioFrame create )
        {
            create.Clear();
                                  X += d;
                 sample = (Int24)(X * X);
            if( (sample > Int24.MaxValue)
             || (sample < Int24.MinValue) ) {
                 sample = flip = last;
                 d = -d;
                 if ( Flipping ) {
                    Flip = !Flip;
                    if ( Flip == false )
                        if ( ++count == 4 )
                               count = 0;
                 }
            }

            create.Mix(
                frame.Mix( (Int24) (
                    ( X > NULL ? -GANZ : GANZ )
                  * ( sample - ( Flip ? NULL : flip ) )
                  * ( count == Count ? -GANZ : GANZ )
                     ), Panorama.Neutral )
                     .Amp( amp )
                   .Converted( typ ),
            1.0f );

            last = sample;
            lastX = X;    
        }

        protected override IAudioFrame pullFrame()
        {
            frame.Clear();
            return frame;
        }

        public override IAudioFrame PullFrame()
        {

            return pullFrame();
        }
    }
}
#endif
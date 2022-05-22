using System;
using System.Collections.Generic;
using Stepflow.Controller;
using Stepflow.Audio.FileIO;
using Stepflow.Audio.FrameTypes;
using Consola;
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
    public class AudioFromStdIn : IAudioInStream
    {
        private StdStreams      std = null;
        private AudioFrameType  typ;
        private int             srt;
        private int             pos;
        private AudioFileHeader hdr;
        private int             siz;

        public AudioFromStdIn()
        {
            pos = siz = srt = -1;
        }

        public AudioFromStdIn( CreationFlags creationFlags )
        {
            std = new StdStreams( creationFlags );
            pos = -1;
            srt = -1;
            siz = -1;
            
        }

        public AudioFromStdIn( StdStreams pimpl )
        {
            pos = -1;
            std = pimpl;
            srt = -1;
            siz = -1;
        }

        public bool Open( PcmFormat format )
        {
            if (std == null) {
                MessageLogger.logErrorSchlimm( "Consola streams not initialized!" );
                return false;
            }
            srt = (int)format.SampleRate;
            typ = format.FrameType;
            pos = 0;
            siz = int.MaxValue;
            return pos == 0;
        }

        public bool Open( AudioFileHeader waitForStreamedHeader )
        {
            if (std == null) {
                MessageLogger.logErrorSchlimm("Consola streams not initialized!");
                return false;
            }
            srt = -1;
            pos = -1;
            siz = -1;
            int available = std.Inp.CanRead();
            while ( available == 0 ) {
                System.Threading.Thread.Sleep(100);
                available = std.Inp.CanRead();
            } pos = readHeader( loadHeader( waitForStreamedHeader ) );
            return pos == 0;
        }

        public void SetConsolaPimpl( StdStreams stream_pimpl )
        {
            std = stream_pimpl;
        }

        private IntPtr loadHeader( AudioFileHeader headerType )
        {
            hdr = 0; switch( headerType ) {
                case AudioFileHeader.Wav: hdr = AudioFileHeader.WavHeaderSize; break;
                case AudioFileHeader.Snd: hdr = AudioFileHeader.SndHeaderSize; break;
                case AudioFileHeader.PB7: hdr = AudioFileHeader.PamHeaderSize; break;
            } return System.Runtime.InteropServices.Marshal.AllocCoTaskMem( (int)hdr );
        }

        private int readHeader( IntPtr raw )
        {
            int ok = -1;
            AudioFileHeader hdrsize = (AudioFileHeader)std.Inp.Read( raw, 0, (uint)hdr );
            switch( hdrsize ) {
                case AudioFileHeader.WavHeaderSize: unsafe { ok = 0;
                    WaveHeaderSp hdrstruct = *(WaveHeaderSp*)raw.ToPointer();
                    typ = hdrstruct.AudioFormat.FrameType;
                    srt = (int)hdrstruct.AudioFormat.SampleRate;
                    siz = (int)hdrstruct.DataChunk.size;
                } break;
                case AudioFileHeader.SndHeaderSize: unsafe { ok = 0;
                    SndHeader hdrstruct = SndHeader.FromRawData( raw );
                    typ = hdrstruct.FrameType;
                    srt = (int)hdrstruct.SampleRate;
                    siz = (int)hdrstruct.DataSize;
                } break;
                case AudioFileHeader.PamHeaderSize: unsafe { ok = 0;
                    PamHeader pamhdr = PamHeader.FromData( raw );
                    typ = pamhdr.FrameType;
                    srt = pamhdr.SampleRate;
                    siz = pamhdr.DataSize;
                } break;
            } System.Runtime.InteropServices.Marshal.FreeCoTaskMem( raw );
            return ok;
        }

        public uint CanStream( StreamDirection stream )
        {
            if ( stream.In( Direction ) ) {
                return siz == int.MaxValue ? uint.MaxValue : (uint)siz;
            } return 0;
        }
        public uint AvailableBytes()
        {
            return (uint)std.Inp.CanRead();
        }

        public uint AvailableFrames()
        {
            return (uint)(std.Inp.CanRead() / typ.BlockAlign);
        }

        public TimeSpan AvailableTime()
        {
            return TimeSpan.FromSeconds( (1.0 / srt) * (std.Inp.CanRead() / typ.BlockAlign) );
        }

        public StreamDirection CanSeek()
        {
            return StreamDirection.NONE;
        }

        public StreamDirection Direction
        {
            get { return StreamDirection.READ; }
        }

        public PcmFormat GetFormat()
        {
            return typ.CreateFormatStruct( srt );
        }

        public uint GetFramesReadable()
        {
            return (uint)(std.Inp.CanRead() / typ.BlockAlign);
        }

        public uint GetFramesWritable()
        {
            return 0;
        }

        public AudioFrameType GetFrameType()
        {
            return typ;
        }

        public uint GetPosition( StreamDirection read )
        {
            return (uint)pos / typ.BlockAlign;
        }

        public uint Read( Audio dst )
        {
            if (pos < 0) return 0;
            if ( (ushort)dst.TypeCode != typ.Code )
                dst.Format = typ.CreateFormatStruct( srt );
            uint readsize = std.Inp.Read( dst.GetRaw(), 0, dst.FrameCount * typ.BlockAlign );
            pos += (int)readsize;
            return readsize;
        }

        public uint Read( Audio dst, int numberFrames )
        {
            if (pos < 0) return 0;
            if ( dst.GetFrameType().Code != typ.Code )
                dst.Format = typ.CreateFormatStruct( srt );
            uint readsize = std.Inp.Read( dst.GetRaw(), 0, (uint)(numberFrames * typ.BlockAlign));
            pos += (int)readsize;
            return readsize;
        }

        public uint Read( IntPtr dstMem, int countBytes, int offsetDstBytes)
        {
            if (pos < 0) return 0;
            uint readsize = std.Inp.Read(dstMem, (uint)offsetDstBytes, (uint)countBytes);
            pos += (int)readsize;
            return readsize;
        }

        public Audio Read( int frames )
        {
            if (pos < 0) return new AudioBuffer(0);
            int done = 0;
            AudioBuffer buffer = new AudioBuffer( typ, (uint)srt, (uint)frames );
            frames *= typ.BlockAlign;
            while ( done < frames ) {
                int read = std.Inp.CanRead();
                while ( read == 0 ) {
                    System.Threading.Thread.Sleep(100);
                    read = std.Inp.CanRead();
                } read = Math.Min( read, frames );
                byte[] data = new byte[read];
                std.Inp.Read( data, 0, (uint)read );
                unsafe { fixed (void* ptr = data) {
                    byte* src = (byte*)ptr;
                    byte* dst = (byte*)buffer.GetRaw().ToPointer();
                    for ( int i = 0; i < read; ++i ) *dst++ = *src++;
                    done += read;
                } }
            } pos += done;
            return buffer;
        }

        public Audio Read()
        {
            if( pos >= 0 ) { 
            int read = std.Inp.CanRead();
            if (read >= typ.BlockAlign) {
                read -= read % typ.BlockAlign;
                AudioBuffer buffer = new AudioBuffer(typ, (uint)srt, (uint)read/typ.BlockAlign);
                byte[] data = new byte[read];
                pos += (int)std.Inp.Read( data, 0, (uint)read );
                unsafe {
                    fixed (void* ptr = data) {
                        byte* src = (byte*)ptr;
                        byte* dst = (byte*)buffer.GetRaw().ToPointer();
                        for (int i = 0; i < read; ++i) *dst++ = *src++;
                    }
                } return buffer;
            } } return new AudioBuffer(0);
        }

        public Audio ReadAll()
        {
            return Read();
        }

        public IAudioFrame ReadFrame()
        {
            IAudioFrame frame = typ.CreateEmptyFrame();
            if (pos < 0) return frame;
            pos += (int)std.Inp.Read( frame.GetRaw(), 0, typ.BlockAlign );
            return frame;
        }

        public IAudioFrame[] ReadFrames( uint count )
        {
            if (pos < 0) return null;
            IAudioFrame[] frames = new IAudioFrame[count];
            for( int i=0; i < count; ++i ) {
                IAudioFrame frame = typ.CreateEmptyFrame();
                pos += (int)std.Inp.Read( frame.GetRaw(), 0, typ.BlockAlign );
                frames[i] = frame;
            } return frames;
        }

        public void Seek( StreamDirection A_0, uint A_1 )
        {
            MessageLogger.logInfoWichtig("Cannot seek in StdIn stream");
        }
    }

    public class AudioToStdOut : IAudioOutStream
    {
        private StdStreams      std;
        private int             srt;
        private AudioFrameType  typ;
        private uint            pos;
        private AudioFileHeader hdr;
        
        public StreamDirection Direction {
            get { return StreamDirection.OUTPUT; }
        }

        public AudioToStdOut( StdStreams pimpl, PcmFormat format )
        {
            std = pimpl;
            typ = format.FrameType;
            srt = (int)format.SampleRate;
        }

        public StreamDirection CanSeek()
        {
            return StreamDirection.NONE;
        }

        public PcmFormat GetFormat()
        {
            return typ.CreateFormatStruct( srt );
        }

        public AudioFrameType GetFrameType()
        {
            return typ;
        }

        public uint GetPosition( StreamDirection direction )
        {
            if( direction.In( StreamDirection.OUTPUT ) )
                return pos / typ.BlockAlign;
            else return 0;
        }

        public void Seek( StreamDirection A_0, uint A_1 )
        {
            MessageLogger.logInfoWichtig("Cannot seek in stdout stream");
        }

        uint IAudioOutStream.Write( Audio srcBuffer, int countFs, int FsOffsetSrc )
        {
            if( srcBuffer.GetFrameType().BlockAlign == typ.BlockAlign )
                srcBuffer = srcBuffer.converted( typ );
            uint siz = (uint)(countFs * typ.BlockAlign);
            std.Out.Write( srcBuffer.GetRaw(), (int)siz, FsOffsetSrc * typ.BlockAlign );
            pos += siz;
            return siz;
        }

        public uint Write( IntPtr rawData, int countBytes, int offsetSrcBytes )
        {
            std.Out.Write( rawData, offsetSrcBytes, countBytes );
            pos += (uint)countBytes;
            return (uint)countBytes;
        }

        public uint WriteAudio( Audio buffer )
        {
            return Write( buffer.GetRaw(), (int)(buffer.FrameCount * typ.BlockAlign), 0);
        }

        public uint WriteFrame( double sample, Panorama mixer )
        {
            IAudioFrame match = typ.CreateEmptyFrame().Mix( sample, mixer );
            std.Out.Write(match.GetRaw(), 0, typ.BlockAlign);
            pos += typ.BlockAlign;
            return typ.BlockAlign;
        }

        public uint WriteFrame( float sample, Panorama mixer )
        {
            IAudioFrame match = typ.CreateEmptyFrame().Mix(sample, mixer);
            std.Out.Write(match.GetRaw(), 0, typ.BlockAlign);
            pos += typ.BlockAlign;
            return typ.BlockAlign;
        }

        public uint WriteFrame( short sample, Panorama mixer )
        {
            IAudioFrame match = typ.CreateEmptyFrame().Mix(sample, mixer);
            std.Out.Write(match.GetRaw(), 0, typ.BlockAlign);
            pos += typ.BlockAlign;
            return typ.BlockAlign;
        }

        public uint WriteFrame( float sample )
        {
            IAudioFrame match = typ.CreateEmptyFrame().Mix(sample, Panorama.Neutral);
            std.Out.Write(match.GetRaw(), 0, typ.BlockAlign);
            pos += typ.BlockAlign;
            return typ.BlockAlign;
        }

        public uint WriteFrame( short sample )
        {
            IAudioFrame match = typ.CreateEmptyFrame().Mix(sample, Panorama.Neutral);
            std.Out.Write(match.GetRaw(), 0, typ.BlockAlign);
            pos += typ.BlockAlign;
            return typ.BlockAlign;
        }

        public uint WriteFrame( IAudioFrame frame )
        {
            std.Out.Write(frame.GetRaw(), 0, typ.BlockAlign);
            pos += typ.BlockAlign;
            return typ.BlockAlign;
        }

        public uint WriteSample( float A_0 )
        {
            std.Out.Write<float>(new float[] { A_0 }, 0, 4);
            pos += 4;
            return 4;
        }

        public uint WriteSample(short A_0)
        {
            std.Out.Write<short>(new short[] { A_0 }, 0, 2);
            pos += 2;
            return 2;
        }

        public uint WrittenBytes()
        {
            return pos;
        }

        public uint WrittenFrames()
        {
            return pos / typ.BlockAlign;
        }

        public void WriteHeader( FileFormat fileformat )
        {
            switch(fileformat)
            {
                case FileFormat.WAV: break;
                case FileFormat.SND: break;
                case FileFormat.PAM: {
                    PamHeader pam = new PamHeader( typ, srt );
                    std.Out.Write( pam.ToString() );
                } break;
            }
        }

        public TimeSpan WrittenTime()
        {
            return TimeSpan.FromSeconds( (1.0 / srt) * WrittenFrames() );
        }

        public uint CanStream( StreamDirection A_0 )
        {
            if (A_0.In(StreamDirection.OUTPUT)) {
                return uint.MaxValue;
            } else return 0;
        }
    }


}

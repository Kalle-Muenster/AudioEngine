using System;
using System.Collections.Generic;
using Stepflow.Controller;
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

#if Yes
namespace Stepflow.Audio.Elements
{

    public class WaveTable : AudioSource
	{
		 public enum TABLES
		 {
			 temp0,
			 temp1,
			 temp2,
			 temp3,
			 temp4,
			 temp5,
			 temp6,
			 temp7,

		 }

		private AudioFrame32Converter[][] Table;
		private ushort[][] Waves;
		private int currentWave;
		private int currentTable;
		private int nextWave=0;
		private int nextTable =0;
		private List<int> numberOfWaves;
		private bool playing=false;
		public int NumberOfTables
		{
			get;
			private set;
		}

		public WaveTable()
		{
			NumberOfTables = 0;
			numberOfWaves = new List<int>();
			Waves = new ushort[Enum.GetNames(typeof(TABLES)).Length][];
			Table = new AudioFrame32Converter[Enum.GetNames(typeof(TABLES)).Length][];
			
			
			
			for(int i=0 ; i < Waves.Length ; i++)
			{
				LoadTable("Content\\Waves\\Abmischung_"+i.ToString()+".psm");
			}


			
			//LoadTable("Content\\Waves\\TriandSquare.txt");
			//LoadTable("Content\\Waves\\square.txt");
			

			currentWave = currentTable = 0;
			ok = true;
		}
		bool ok=false;


        public void LoadTable(string textfilename)
        {

            short sample;
            float sampleF;
            bool negativeSample = false;
            if (textfilename.EndsWith(".txt")||textfilename.EndsWith(".psm"))
            {
                List<AudioFrame32Converter> loadbuffer = new List<AudioFrame32Converter>();
                AudioFrame32Converter convert = AudioFrame32Converter.Zero;
                List<ushort> samplesbuffer = new List<ushort>();
                TextReader reader = new FileInfo(textfilename).OpenText();
                string Info = reader.ReadLine();
                if (Info.Contains("SIGNED_16"))
                {
                    if (!Info.Contains("STEREO"))
                    {
                        samplesbuffer.Add(0);
                        while (short.TryParse(reader.ReadLine(), out sample))
                        {
                            convert.signed16_0 = sample;
                            convert.signed16_1 = sample;

                            loadbuffer.Add(convert);

                            if (sample < 0)
                            {
                                if (!negativeSample)
                                    negativeSample = true;
                            }
                            else
                            {
                                if (negativeSample)
                                {
                                    samplesbuffer.Add((ushort)(loadbuffer.Count - 1));
                                    negativeSample = false;
                                }
                            }
                        }
                    }
                }

                Table[NumberOfTables] = new AudioFrame32Converter[loadbuffer.Count];
                loadbuffer.CopyTo(Table[NumberOfTables]);
                Waves[NumberOfTables] = new ushort[samplesbuffer.Count];
                samplesbuffer.CopyTo(Waves[NumberOfTables]);
                numberOfWaves.Add(samplesbuffer.Count - 1);
                //samplesbuffer.Clear();
                //loadbuffer.Clear();
                NumberOfTables++;
            }
        }
		public void Form( TABLES table, int wave )
		{
			if(ok)
			{
				if(wave < numberOfWaves[(int)table] && wave >= 0)
					nextWave = wave;
				if((int)table < NumberOfTables && table >= 0)
					nextTable = (int)table;
			}
		}
		
		public int NumberOfWavesOnTable(int tableindex)
		{
			return numberOfWaves[tableindex];
		}

		public int WaveLangth
		{
			get { return Waves[currentTable][currentWave + 1] - Waves[currentTable][currentWave]; }
		}

        public override unsafe int Read(byte[] buffer, int offset, int length)
        {
            fixed (void* pt = &buffer[0])
            {
                AudioFrame32Converter* s16Stereo = (AudioFrame32Converter*)pt;

                int c = offset / 4;
                int C = 0;

                int x = 0;

                C = Waves[currentTable][currentWave + 1] - Waves[currentTable][currentWave];
                while (C < length / 4)
                {
                    for (int i = Waves[currentTable][currentWave]; i < Waves[currentTable][currentWave + 1]; i++)
                    {
                        s16Stereo[c++] = Table[currentTable][i];
                    }

                    if (nextWave > currentWave)
                        currentWave++;
                    else if (nextWave < currentWave)
                        currentWave--;

                    if (nextTable > currentTable)
                        currentTable++;
                    else if (nextTable < currentTable)
                        currentTable--;

                    C = c + (Waves[currentTable][currentWave + 1] - Waves[currentTable][currentWave]);

                }
                c -= (offset / 4);

                return c;
            }

        }

	}
}
#endif

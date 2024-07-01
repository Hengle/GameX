using GameX.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static GameX.Platforms.Platform;
using static OpenStack.Debug;

namespace GameX.Bullfrog.Formats
{
    public unsafe class Binary_Fli : IDisposable, IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Fli(r, f));

        // logging
        static StreamWriter F;
        static void FO(string x) { F = File.CreateText("C:\\T_\\FROG\\Fli2.txt"); }
        static void FW(string x) { F.Write(x); F.Flush(); }

        #region Headers

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_Header
        {
            public const int MAGIC = 0xaf12;
            public static (string, int) Struct = ("<I4H", sizeof(X_Header));
            public uint Size;
            public ushort Type;
            public ushort NumFrames;
            public ushort Width;
            public ushort Height;
        }

        public enum ChunkType : ushort
        {
            COLOR_256 = 0x4,    // COLOR_256
            DELTA_FLC = 0x7,    // DELTA_FLC (FLI_SS2)
            BYTE_RUN = 0xF,     // BYTE_RUN
            FRAME = 0xF1FA,     // FRAME_TYPE
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_ChunkHeader
        {
            public static (string, int) Struct = ("<IH", sizeof(X_ChunkHeader));
            public uint Size;
            public ChunkType Type;
            public bool IsValid => Type == ChunkType.COLOR_256 || Type == ChunkType.DELTA_FLC || Type == ChunkType.BYTE_RUN || Type == ChunkType.FRAME;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_FrameHeader
        {
            public static (string, int) Struct = ("<5H", sizeof(X_FrameHeader));
            public ushort NumChunks;
            public ushort Delay;
            public ushort Reserved;
            public ushort WidthOverride;
            public ushort HeightOverride;
        }

        public enum OpCode : ushort
        {
            PACKETCOUNT = 0,    // PACKETCOUNT
            UNDEFINED = 1,      // UNDEFINED
            LASTPIXEL = 2,      // LASTPIXEL
            LINESKIPCOUNT = 3,  // LINESKIPCOUNT
        }

        #endregion

        public BinaryReader R;
        public byte[] Palette = new byte[256 * 3];
        public byte[] Pixels;
        public int Width;
        public int Height;
        public int NumFrames;
        public int Fps;
        public int CurFrame;

        public Binary_Fli(BinaryReader r, FileSource f)
        {
            FO("C:\\T_\\FROG\\Fli2.txt");

            // read header
            var header = r.ReadS<X_Header>();
            if (header.Type != X_Header.MAGIC) throw new FormatException("BAD MAGIC");
            Width = header.Width;
            Height = header.Height;
            NumFrames = header.NumFrames;

            // set values
            R = r;
            Fps = Path.GetFileNameWithoutExtension(f.Path).ToLowerInvariant() == "intro" ? 10 : 15;
            Pixels = new byte[Width * Height];

            // play
            Play(R);
        }

        public void Dispose() => R.Close();

        public bool HasFrames => NumFrames > 0;

        public void Play(BinaryReader r)
        {
            while (HasFrames)
            {
                if (!DecodeFrame(r)) break;
                CopyCurrentFrameToScreen();
                CurFrame++;
            }
        }

        bool DecodeFrame(BinaryReader r)
        {
            X_FrameHeader frameHeader;
            var header = r.ReadS<X_ChunkHeader>();
            do
            {
                var pos = r.BaseStream.Position;
                //FW($"{pos:x5}> {header.Type} - {header.Size}\n");
                var nextPosition = r.BaseStream.Position + (header.Size - 6);
                switch (header.Type)
                {
                    case ChunkType.COLOR_256: SetPalette(r); break; //g_System.setPalette8b3(palette_);
                    case ChunkType.DELTA_FLC: DecodeDeltaFLC(r); break;
                    case ChunkType.BYTE_RUN: DecodeByteRun(r); break;
                    case ChunkType.FRAME:
                        frameHeader = r.ReadS<X_FrameHeader>();
                        NumFrames--;
                        Log($"Frames Remaining: {NumFrames}, Chunks: {frameHeader.NumChunks}");
                        break;
                    default:
                        Log($"Unknown Type: {header.Type}");
                        r.Skip(header.Size);
                        break;
                }
                if (header.Type != ChunkType.FRAME && r.BaseStream.Position != nextPosition) r.Seek(nextPosition);
                header = r.ReadS<X_ChunkHeader>();
            }
            while (header.IsValid && header.Type != ChunkType.FRAME);
            if (header.Type == ChunkType.FRAME) r.Skip(-sizeof(X_ChunkHeader));
            return header.IsValid;
        }

        void SetPalette(BinaryReader r)
        {
            var numPackets = r.ReadUInt16();
            if (r.ReadUInt16() == 0) // special case
            {
                var data = r.ReadBytes(256 * 3);
                for (var i = 0; i < data.Length; i++) Palette[i] = (byte)((data[i] << 2) | (data[i] & 3));
                return;
            }
            r.Skip(-2);
            int palPos = 0, palPos3;
            while (numPackets-- != 0)
            {
                palPos += r.ReadByte(); palPos3 = palPos * 3;
                var change = r.ReadByte();
                var data = r.ReadBytes(change * 3);
                for (var i = 0; i < data.Length; i++) Palette[palPos3 + i] = (byte)((data[i] << 2) | (data[i] & 3));
                palPos += change;
            }
        }

        void DecodeDeltaFLC(BinaryReader r)
        {
            var linesInChunk = r.ReadUInt16();
            int curLine = 0, numPackets = 0, value;
            while (linesInChunk-- > 0)
            {
                // first process all the opcodes.
                OpCode opcode;
                do
                {
                    value = r.ReadUInt16();
                    opcode = (OpCode)((value >> 14) & 3);
                    switch (opcode)
                    {
                        case OpCode.PACKETCOUNT: numPackets = value; break;
                        case OpCode.UNDEFINED: break;
                        case OpCode.LASTPIXEL: Pixels[(curLine * Width) + (Width - 1)] = (byte)(value & 0xFF); break;
                        case OpCode.LINESKIPCOUNT: curLine += -(short)value; break;
                    }
                } while (opcode != OpCode.PACKETCOUNT);

                // now interpret the RLE data
                value = 0;
                while (numPackets-- > 0)
                {
                    value += r.ReadByte();
                    var pixels = Pixels.AsSpan((curLine * Width) + value);
                    fixed (byte* _ = pixels)
                    {
                        var count = (sbyte)r.ReadByte();
                        if (count > 0)
                        {
                            var size = count << 1;
                            Unsafe.CopyBlock(ref *_, ref r.ReadBytes(size)[0], (uint)size);
                            value += size;
                        }
                        else if (count < 0)
                        {
                            var ptr = (ushort*)_;
                            var count2 = -count;
                            var size = count2 << 1;
                            var data = r.ReadUInt16();
                            while (count2-- != 0) *ptr++ = data;
                            value += size;
                        }
                        else return; // End of cutscene?
                    }
                }
                curLine++;
            }
        }

        void DecodeByteRun(BinaryReader r)
        {
            fixed (byte* _ = Pixels)
            {
                byte* ptr = _, endPtr = _ + (Width * Height);
                while (ptr < endPtr)
                {
                    var numChunks = r.ReadByte();
                    while (numChunks-- != 0)
                    {
                        var count = (sbyte)r.ReadByte();
                        if (count > 0)
                        {
                            Unsafe.InitBlock(ref *ptr, r.ReadByte(), (uint)count); ptr += count;
                        }
                        else
                        {
                            var count2 = -count;
                            Unsafe.CopyBlock(ref *ptr, ref r.ReadBytes(count2)[0], (uint)count2); ptr += count2;
                        }
                    }
                }
            }
        }

        void CopyCurrentFrameToScreen()
        {
            //g_System.updateScreen();
            //g_System.delay(1000 / Fps);
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            return new List<MetaInfo> {
                new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "FLI" }),
                new MetaInfo("Video (FLI)", items: new List<MetaInfo> {
                    new MetaInfo($"Width: {Width}"),
                    new MetaInfo($"Height: {Height}"),
                    new MetaInfo($"NumFrames: {NumFrames}"),
                })
            };
        }
    }
}
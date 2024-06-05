using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static GameX.Formats.Binary_Snd;

namespace GameX.Volition.Formats
{
    public unsafe class PakBinary_Descent : PakBinary<PakBinary_Descent>
    {
        #region Headers

        [Flags]
        public enum PIG_Flags : byte
        {
            TRANSPARENT = 1,
            SUPER_TRANSPARENT = 2,
            NO_LIGHTING = 4,
            RLE = 8,            // A run-length encoded bitmap.
            MASK = 1 | 2 | 4 | 8,
            PAGED_OUT = 16,     // This bitmap's data is paged out.
            RLEBIG = 32,       // for bitmaps that RLE to > 255 per row (i.e. cockpits)
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct PIG_Bitmap
        {
            public static (string, int) Struct = ("<13s5bi", sizeof(PIG_Bitmap));
            public fixed byte Path[8];      // name
            public byte Frame;              // Frame
            public byte Width;              // width
            public byte Height;             // height
            public PIG_Flags Flags;         // flags
            public byte AvgColor;           // avg_color
            public int Offset;              // offset
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct PIG_Bitmap2
        {
            public static (string, int) Struct = ("<8s6bi", sizeof(PIG_Bitmap2));
            public fixed byte Path[8];      // name
            public byte Frame;              // Frame
            public byte Width;              // width
            public byte Height;             // height
            public byte WHExtra;            // hi_wh
            public PIG_Flags Flags;         // flags
            public byte AvgColor;           // avg_color
            public int Offset;              // offset
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct PIG_Sound
        {
            public static (string, int) Struct = ("<8s3i", sizeof(PIG_Sound));
            public fixed byte Path[8];      // name
            public int Length;              // length (Samples)
            public int DataLength;          // data_length
            public int Offset;              // offset
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct DHF_Record
        {
            public static (string, int) Struct = ("<13si", sizeof(DHF_Record));
            public fixed byte Path[13];     // filename, padded to 13 bytes with 0s
            public int FileSize;            // filesize in bytes
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct HOG2_Header
        {
            public static (string, int) Struct = ("<13s2i56x", sizeof(HOG2_Header));
            public int NumFiles;            // number of files
            public int Offset;              // offset to first file (end of file list)
            public fixed byte Padding[56];  // filled with FF
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct HOG2_Record
        {
            public static (string, int) Struct = ("<36s3i", sizeof(HOG2_Record));
            public fixed byte Path[36];     // null-terminated (usually is filled up with "CD")
            public int Unknown;             // always 0
            public int FileSize;            // size of file in bytes
            public int Timestamp;           // Timestamp in seconds since 1.1.1970 0:00
        }

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            const uint MAGIC_PPIG = 0x47495050;
            //const uint MAGIC_DPOG = 0x474f5044;
            const uint MAGIC_DHF = 0x00464844;
            const uint MAGIC_HOG2 = 0x00474f48;

            var magic = r.ReadUInt32();
            if ((magic & 0x00ffffff) == MAGIC_DHF) { magic = MAGIC_DHF; r.Skip(-1); }

            // read files
            List<FileSource> files;
            source.Files = files = new List<FileSource>();
            switch (magic)
            {
                case MAGIC_PPIG:
                    //case MAGIC_DPOG:
                    {
                        LoadPig(r, files, true, magic, source.Game.Id);
                        return Task.CompletedTask;
                    }
                case MAGIC_DHF:
                    {
                        while (r.BaseStream.Position < r.BaseStream.Length)
                        {
                            var record = r.ReadS<DHF_Record>();
                            files.Add(new FileSource
                            {
                                Path = UnsafeX.FixedAString(record.Path, 13),
                                Offset = r.BaseStream.Position,
                                FileSize = record.FileSize,
                            });
                            r.Skip(record.FileSize);
                        }
                    }
                    return Task.CompletedTask;
                case MAGIC_HOG2:
                    {
                        var header = r.ReadS<HOG2_Header>();
                        var offset = header.Offset;
                        for (var i = 0; i < header.NumFiles; i++)
                        {
                            var record = r.ReadS<HOG2_Record>();
                            files.Add(new FileSource
                            {
                                Path = UnsafeX.FixedAString(record.Path, 13),
                                Offset = offset,
                                FileSize = record.FileSize,
                            });
                            offset += record.FileSize;
                        }
                    }
                    return Task.CompletedTask;
                default: // PIG
                    {
                        LoadPig(r, files, false, magic, source.Game.Id);
                        return Task.CompletedTask;
                    }
            }
            throw new FormatException("BAD MAGIC");
        }

        // https://github.com/arbruijn/DesDump/blob/master/DesDump/ClassicLoader.cs
        // https://web.archive.org/web/20020213004051/http://descent-3.com/ddn/specs/hog/
        void LoadPig(BinaryReader r, List<FileSource> files, bool d2, uint magic, string gameId)
        {
            if (d2) r.Skip(4); // descent 2 pig, skip version
            else if (magic >= 65536) r.Seek(magic); // descent reg 1.4+ pig, first int is offset
            else r.Seek(0); // descent 1 sw / pre 1.4 pig: first int is count
            var numBitmaps = r.ReadInt32();
            var numSounds = d2 ? 0 : r.ReadInt32();
            var bitmapHdrSize = d2 ? sizeof(PIG_Bitmap2) : sizeof(PIG_Bitmap);
            var dataOffset = (int)(r.Tell() + numBitmaps * bitmapHdrSize + numSounds * sizeof(PIG_Sound));

            FileSource n = null, l = null;
            var fileSize = 0L;
            if (d2)
                foreach (var s in r.ReadSArray<PIG_Bitmap2>(numBitmaps))
                {
                    files.Add(n = new FileSource
                    {
                        Path = $"bmps/{UnsafeX.FixedAString(s.Path, 8)}{((s.Frame & 64) != 0 ? $".{(int)s.Frame & 63}" : "")}.bmp",
                        Offset = s.Offset + dataOffset,
                        Tag = (s.Flags, s.Width + (short)((s.WHExtra & 0x0f) << 8), s.Height + (short)((s.WHExtra & 0xf0) << 4)),
                    });
                    if (l != null) { fileSize = n.Offset - l.Offset; l.FileSize = fileSize; }
                    l = n;
                }
            else
                foreach (var s in r.ReadSArray<PIG_Bitmap>(numBitmaps))
                {
                    files.Add(n = new FileSource
                    {
                        Path = $"bmps/{UnsafeX.FixedAString(s.Path, 8)}{((s.Frame & 64) != 0 ? $".{(int)s.Frame & 63}" : "")}.bmp",
                        Offset = s.Offset + dataOffset,
                        Tag = (s.Flags, (short)s.Width, (short)s.Height),
                    });
                    if (l != null) { fileSize = n.Offset - l.Offset; l.FileSize = fileSize; }
                    l = n;
                }
             // Width + ((b.Frame & 128) != 0 ? 256 : 0);
            l.FileSize = fileSize;
            if (numSounds > 0)
                files.AddRange(r.ReadSArray<PIG_Sound>(numSounds).Select(s => new FileSource
                {
                    Path = $"snds/{UnsafeX.FixedAString(s.Path, 8)}.wav",
                    Offset = s.Offset + dataOffset,
                    FileSize = s.DataLength,
                    Tag = s,
                }));
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            r.Seek(file.Offset);
            var bytes = r.ReadBytes((int)file.FileSize);
            if (file.Tag != null)
                if (file.Tag is PIG_Sound t)
                {
                    var length = t.Length;
                    var s = new MemoryStream();
                    // write header
                    var w = new BinaryWriter(s);
                    w.WriteT(new WavHeader
                    {
                        ChunkId = WavHeader.RIFF,
                        ChunkSize = sizeof(WavFmt) + sizeof(WavData) + length,
                        Format = WavHeader.WAVE,
                    });
                    w.WriteT(new WavFmt
                    {
                        ChunkId = WavFmt.FMT_,
                        ChunkSize = sizeof(WavHeader) + sizeof(uint), // fmt size
                        AudioFormat = 1, // pcm
                        NumChannels = 1, // mono
                        SampleRate = 11025, // sample rate
                        ByteRate = 11025, // byte rate
                        BlockAlign = 1, // align
                        BitsPerSample = 8, // bits per sample
                    });
                    w.WriteT(new WavData
                    {
                        ChunkId = WavData.DATA,
                        ChunkSize = length,
                    });
                    // write data
                    w.Write(bytes);
                    s.Position = 0;
                    return Task.FromResult((Stream)s);
                }
            return Task.FromResult((Stream)new MemoryStream(bytes));
        }

        //void LoadPig1(BinaryReader r, List<FileSource> files, string gameId)
        //{
        //    var header = r.ReadS<PIG_Header>();
        //    if (!header.loadV1(r, files, gameId) &&
        //        !header.loadV2(r, files, gameId)) throw new FormatException("BAD MAGIC");
        //}

        //[StructLayout(LayoutKind.Sequential, Pack = 1)]
        //struct PIG_Header
        //{
        //    public static (string, int) Struct = ("<2i", sizeof(PIG_Header));
        //    public int NumBitmaps;
        //    public int NumSounds;

        //    public bool loadV1(BinaryReader r, List<FileSource> files, string gameId)
        //    {
        //        (int bitmaps, int sounds) max = gameId switch { "D" => (1800, 250), "D2" => (2620, 254), _ => (0, 0) };
        //        if (NumBitmaps <= max.bitmaps) r.Seek(8); // <v1.4 pig
        //        else if (NumBitmaps > 0 && NumBitmaps < r.BaseStream.Length) { r.Seek(NumBitmaps); NumBitmaps = r.ReadInt32(); NumSounds = r.ReadInt32(); } // >=v1.4 pig
        //        else return false;
        //        if (NumBitmaps >= max.bitmaps || NumSounds >= max.sounds) return false;
        //        var dataOffset = NumBitmaps * sizeof(PIG_Bitmap) + NumSounds * sizeof(PIG_Sound);

        //        FileSource n = null, l = null;
        //        foreach (var s in r.ReadSArray<PIG_Bitmap>(NumBitmaps))
        //        {
        //            files.Add(n = new FileSource
        //            {
        //                Path = $"bmps/{UnsafeX.FixedAString(s.Path, 8)}{((s.Frame & 64) != 0 ? $".{(int)s.Frame & 63}" : "")}.bmp",
        //                Offset = s.Offset + dataOffset,
        //                Tag = s,
        //            });
        //            if (l != null) l.FileSize = n.Offset - l.Offset;
        //            l = n;
        //        }
        //        //l.FileSize = n.Offset - l.Offset;

        //        //files.AddRange(r.ReadSArray<PIG_Bitmap>(NumBitmaps).Select(s => new FileSource
        //        //{
        //        //    Path = $"bmps/{UnsafeX.FixedAString(s.Path, 8)}{((s.DFlags & PIG_DFlag.ABM) != 0 ? $".{(int)s.DFlags & 63}" : "")}.bmp",
        //        //    Offset = s.Offset + dataOffset,
        //        //    Tag = s,
        //        //    //Flags = (byte)(s.Flags & PIG_Flag.MASK),
        //        //    //Tag = (s.Width + ((s.DFlags & PIG_DFlag.LARGE) != 0 ? 256 : 0), s.Height),
        //        //}));
        //        files.AddRange(r.ReadSArray<PIG_Sound>(NumSounds).Select(s => new FileSource
        //        {
        //            Path = $"snds/{UnsafeX.FixedAString(s.Path, 8)}.wav",
        //            Offset = s.Offset + dataOffset,
        //            FileSize = s.DataLength,
        //            Tag = s,
        //        }));
        //        return true;
        //    }

        //    public bool loadV2(BinaryReader r, List<FileSource> files, string gameId)
        //    {
        //        return true;
        //    }
        //}

        //static (bool shareware, bool mac) DetectPig(BinaryReader r)
        //{
        //    const int D1_SHARE_BIG_PIGSIZE = 5092871; // v1.0 - 1.4 before RLE compression
        //    const int D1_SHARE_10_PIGSIZE = 2529454; // v1.0 - 1.2
        //    const int D1_SHARE_PIGSIZE = 2509799; // v1.4
        //    const int D1_10_BIG_PIGSIZE = 7640220; // v1.0 before RLE compression
        //    const int D1_10_PIGSIZE = 4520145; // v1.0
        //    const int D1_PIGSIZE = 4920305; // v1.4 - 1.5 (Incl. OEM v1.4a)
        //    const int D1_OEM_PIGSIZE = 5039735; // v1.0
        //    const int D1_MAC_PIGSIZE = 3975533;
        //    const int D1_MAC_SHARE_PIGSIZE = 2714487;
        //    switch (r.BaseStream.Length)
        //    {
        //        case D1_SHARE_BIG_PIGSIZE:
        //        case D1_SHARE_10_PIGSIZE:
        //        case D1_SHARE_PIGSIZE: return (true, false);
        //        case D1_10_BIG_PIGSIZE:
        //        case D1_10_PIGSIZE: return (false, false);
        //        case D1_MAC_PIGSIZE:
        //        case D1_MAC_SHARE_PIGSIZE: r.Seek(r.ReadInt32()); return (false, true);
        //        case D1_PIGSIZE:
        //        case D1_OEM_PIGSIZE: r.Seek(r.ReadInt32()); return (false, false);
        //        default: Log($"Unknown size for PIG"); return (false, false);
        //    }
        //}
    }
}
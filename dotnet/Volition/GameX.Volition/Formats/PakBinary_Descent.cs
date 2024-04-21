using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static OpenStack.Debug;

namespace GameX.Volition.Formats
{
    public unsafe class PakBinary_Descent : PakBinary<PakBinary_Descent>
    {
        #region Headers

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        struct PIG_Header
        {
            public static (string, int) Struct = (">2I", sizeof(PIG_Header));
            public int NumBitmaps;
            public int NumSounds;
        }

        // https://web.archive.org/web/20020226014647/http://descent2.com/ddn/specs/hog/
        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        struct DHF_Record
        {
            public static (string, int) Struct = ("<13si", sizeof(DHF_Record));
            public fixed byte Path[13];     // filename, padded to 13 bytes with 0s
            public int FileSize;            // filesize in bytes
        }

        // https://web.archive.org/web/20020213004051/http://descent-3.com/ddn/specs/hog/
        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        struct HOG2_Header
        {
            public static (string, int) Struct = ("<13s2i56x", sizeof(HOG2_Header));
            public int NumFiles;            // number of files
            public int Offset;              // offset to first file (end of file list)
            public fixed byte Padding[56];  // filled with FF
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
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
            switch (Path.GetExtension(source.PakPath))
            {
                case ".pig":
                    {
                        var (shareware, mac) = DetectPig(r);
                        var header = r.ReadS<PIG_Header>();

                        // read files
                        var files = source.Files = new List<FileSource>();

                        //while (r.BaseStream.Positio   n < r.BaseStream.Length)
                        //{
                        //    var record = r.ReadS<HOG_Record>();
                        //    r.Skip(record.FileSize);
                        //    files.Add(new FileSource
                        //    {
                        //        Path = UnsafeX.FixedAString(record.Path, 13),
                        //        FileSize = record.FileSize,
                        //    });
                        //}
                        return Task.CompletedTask;
                    }
                case ".hog":
                    {
                        const uint MAGIC_DHF = 0x00464844;
                        const uint MAGIC_HOG2 = 0x00474f48;

                        var files = source.Files = new List<FileSource>();
                        var magic = r.ReadUInt32() & 0x00FFFFFF;
                        switch (magic)
                        {
                            case MAGIC_DHF:
                                {
                                    r.Skip(-1);
                                    // read files
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
                                    // read files
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
                            default: throw new FormatException("BAD MAGIC");
                        }
                    }
            }
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            r.Seek(file.Offset);
            return Task.FromResult((Stream)new MemoryStream(r.ReadBytes((int)file.FileSize)));
        }

        static (bool shareware, bool mac) DetectPig(BinaryReader r)
        {
            const int D1_SHARE_BIG_PIGSIZE = 5092871; // v1.0 - 1.4 before RLE compression
            const int D1_SHARE_10_PIGSIZE = 2529454; // v1.0 - 1.2
            const int D1_SHARE_PIGSIZE = 2509799; // v1.4
            const int D1_10_BIG_PIGSIZE = 7640220; // v1.0 before RLE compression
            const int D1_10_PIGSIZE = 4520145; // v1.0
            const int D1_PIGSIZE = 4920305; // v1.4 - 1.5 (Incl. OEM v1.4a)
            const int D1_OEM_PIGSIZE = 5039735; // v1.0
            const int D1_MAC_PIGSIZE = 3975533;
            const int D1_MAC_SHARE_PIGSIZE = 2714487;
            switch (r.BaseStream.Length)
            {
                case D1_SHARE_BIG_PIGSIZE:
                case D1_SHARE_10_PIGSIZE:
                case D1_SHARE_PIGSIZE: return (true, false);
                case D1_10_BIG_PIGSIZE:
                case D1_10_PIGSIZE: return (false, false);
                case D1_MAC_PIGSIZE:
                case D1_MAC_SHARE_PIGSIZE: r.Seek(r.ReadInt32()); return (false, true);
                case D1_PIGSIZE:
                case D1_OEM_PIGSIZE: r.Seek(r.ReadInt32()); return (false, false);
                default: Log($"Unknown size for PIG"); return (false, false);
            }
        }
    }
}
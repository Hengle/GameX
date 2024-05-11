using GameX.Formats;
using grendgine_collada;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    public unsafe class PakBinary_Pop3 : PakBinary<PakBinary_Pop3>
    {
        #region Header

        const uint MAGIC_SPR = 0x42465350;

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        struct SPR_Record
        {
            public static (string, int) Struct = ("<2HI", sizeof(SPR_Record));
            public ushort Width;
            public ushort Height;
            public uint Offset;
        }

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {

            var magic = r.ReadUInt32();

            // read files
            List<FileSource> files;
            source.Files = files = new List<FileSource>();
            switch (magic)
            {
                case MAGIC_SPR:
                    {
                        var count = r.ReadUInt32();
                        int i = 0;
                        FileSource n, l = null;
                        var lastOffset = r.Tell();
                        foreach (var s in r.ReadSArray<SPR_Record>((int)count))
                        {
                            files.Add(n = new FileSource
                            {
                                Path = $"sprs/spr{i++}.spr",
                                Offset = s.Offset,
                                Tag = (s.Width, s.Height),
                            });
                            if (l != null) { l.FileSize = n.Offset - (lastOffset = l.Offset); }
                            l = n;
                        }
                        l.FileSize = r.BaseStream.Length - lastOffset;
                        return Task.CompletedTask;
                    }
                default: throw new FormatException("BAD MAGIC");
            }
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            r.Seek(file.Offset);
            var bytes = r.ReadBytes((int)file.FileSize);
            return Task.FromResult((Stream)new MemoryStream(bytes));
        }
    }
}
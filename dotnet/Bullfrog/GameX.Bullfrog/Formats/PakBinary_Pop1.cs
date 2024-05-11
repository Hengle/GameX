using GameX.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    public unsafe class PakBinary_Pop1 : PakBinary<PakBinary_Pop1>
    {
        #region Header

        const uint MAGIC_SPR = 0x42465350;

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        struct SPR_Record
        {
            public uint Magic;
            public uint Dummy1;
            public uint FileCount;
            public uint ExtraFiles;
            public uint Dummy2;
            public uint Dummy3;
            public uint Dummy4;
            public uint Dummy5;
        }

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {

            var magic = r.ReadUInt32();
            var files = source.Files = new List<FileSource>();
            switch (magic)
            {
                case MAGIC_SPR:
                    {
                        var count = r.ReadUInt32();
                        var abc = r.ReadSArray<SPR_Record>((int)count);
                        return Task.CompletedTask;
                    }
                default: throw new FormatException("BAD MAGIC");
            }
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            throw new NotImplementedException();
        }
    }
}
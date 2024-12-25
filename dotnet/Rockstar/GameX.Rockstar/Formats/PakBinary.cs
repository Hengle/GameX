using GameX.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GameX.Rockstar.Formats
{
    #region PakBinary_XXX

    public unsafe class PakBinary_XXX : PakBinary<PakBinary_XXX>
    {
        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            var files = source.Files = new List<FileSource>();

            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}
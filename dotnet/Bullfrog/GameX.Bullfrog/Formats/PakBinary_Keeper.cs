using GameX.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    public unsafe class PakBinary_Keeper : PakBinary<PakBinary_Keeper>
    {
        #region Factories

        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactoryFactory(FileSource source, FamilyGame game)
            => source.Path.ToLowerInvariant() switch
            {
                _ => Path.GetExtension(source.Path).ToLowerInvariant() switch
                {
                    //".dat" => (0, Binary_Dat.Factory),
                    _ => (0, null),
                }
            };

        #endregion

        const int TEXTURE_BLOCKS_STAT_COUNT_A = 544;  // Static textures in tmapa
        const int TEXTURE_BLOCKS_STAT_COUNT_B = 544; // Static textures in tmapb

        byte[] Data;

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            const int TextureBlockSize = 32 * 32;
            Data = Rnc.Unpack(r);
            var files = source.Files = new List<FileSource>();
            var fileName = Path.GetFileName(source.PakPath);
            if (fileName.StartsWith("TMAPA"))
                for (int i = 0, o = 0; i < TEXTURE_BLOCKS_STAT_COUNT_A; i++, o += TextureBlockSize)
                    files.Add(new FileSource
                    {
                        Path = $"texs/{i}.tex",
                        Offset = o,
                        FileSize = TextureBlockSize,
                    });
            else if (fileName.StartsWith("TMAPB"))
                for (int i = 0, o = 0; i < TEXTURE_BLOCKS_STAT_COUNT_B; i++, o += TextureBlockSize)
                    files.Add(new FileSource
                    {
                        Path = $"texs/{i}.tex",
                        Offset = o,
                        FileSize = TextureBlockSize,
                    });
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            var bytes = Data.AsSpan((int)file.Offset, (int)file.FileSize);
            return Task.FromResult((Stream)new MemoryStream(bytes.ToArray()));
        }
    }
}
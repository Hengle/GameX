using GameX.Bullfrog.Formats.Keeper;
using GameX.Formats;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    public class PakBinary_Keeper : PakBinary<PakBinary_Keeper>
    {
        #region Factories

        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactoryFactory(FileSource source, FamilyGame game)
            => source.Path.ToLowerInvariant() switch
            {
                _ => Path.GetExtension(source.Path).ToLowerInvariant() switch
                {
                    ".pal" => (0, Binary_Pal.Factory_3),
                    ".wav" => (0, Binary_Snd.Factory),
                    var x when x == ".tex" || x == ".raw" => (0, Binary_Bmp.Factory),
                    _ => (0, null),
                }
            };

        #endregion

        #region Headers

        const int TEXTURE_BLOCKSA = 544; // Static textures in tmapa
        const int TEXTURE_BLOCKSB = 544; // Static textures in tmapb

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        unsafe struct SBK_Header
        {
            public static (string, int) Struct = ("<14sI", sizeof(SBK_Header));
            public fixed byte Unknown1[14];
            public uint Unknown2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        unsafe struct SBK_Entry
        {
            public static (string, int) Struct = ("<4I", sizeof(SBK_Entry));
            public uint EntryOffset;
            public uint BankOffset;
            public uint Size;
            public uint Unknown;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        unsafe struct SBK_Sample
        {
            public static (string, int) Struct = ("<18QI2x", sizeof(SBK_Sample));
            public fixed byte Path[18];
            public ulong Offset;
            public uint DataSize;
            public byte Sfxid;
            public byte Unknown;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        internal unsafe struct CRE_Sprite
        {
            public static (string, int) Struct = ("<I8x2h", sizeof(CRE_Sprite));
            public uint DataOffset;
            public byte SWidth;
            public byte SHeight;
            public byte FrameWidth;
            public byte FrameHeight;
            public byte Rotable;
            public byte FramesCount;
            public byte FrameOffsW;
            public byte FrameOffsH;
            public short OffsetX;
            public short OffsetY;
        };

        #endregion

        byte[] Data;

        public override async Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            Data = Rnc.Read(r);
            List<FileSource> files;
            source.Files = files = new List<FileSource>();
            var fileName = Path.GetFileName(source.PakPath);
            if (fileName.StartsWith("CREATURE")) await source.Reader(s => ParseCreature(s, files), "DATA/CREATURE.TAB");
            else if (fileName.StartsWith("GUI")) ParseGui(r, files);
            else if (fileName.StartsWith("SOUND")) ParseSound(r, files, 2);
            else if (fileName.StartsWith("SPEECH")) ParseSound(r, files, 2);
            else if (fileName.StartsWith("TMAPA")) ParseTexure(files, TEXTURE_BLOCKSA);
            else if (fileName.StartsWith("TMAPB")) ParseTexure(files, TEXTURE_BLOCKSB);
            //return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            var bytes = Data.AsSpan((int)file.Offset, (int)file.FileSize);
            return Task.FromResult((Stream)new MemoryStream(bytes.ToArray()));
        }

        unsafe static Task ParseCreature(BinaryReader r, List<FileSource> files)
        {
            var numSprites = (int)(r.BaseStream.Length / sizeof(CRE_Sprite));
            var sprites = r.ReadSArray<CRE_Sprite>(numSprites);
            var lastI = numSprites - 1;
            for (var i = 0; i < numSprites; i++)
            {
                ref CRE_Sprite s = ref sprites[i];
                files.Add(new FileSource
                {
                    Path = $"{i:000}.tex",
                    Offset = s.DataOffset,
                    FileSize = (i != lastI ? sprites[i + 1].DataOffset : r.BaseStream.Length) - s.DataOffset,
                    Tag = s
                });
            }
            return Task.CompletedTask;
        }

        static void ParseGui(BinaryReader r, List<FileSource> files)
        {
            //const int TextureBlockSize = 100 * 100;
            //for (int i = 1, o = 0; i < 1; i++, o += TextureBlockSize)
            //    files.Add(new FileSource
            //    {
            //        Path = $"gui/{i:000}.tex",
            //        Offset = o,
            //        FileSize = TextureBlockSize,
            //        Tag = ("", 100, 100)
            //    });
        }

        static void ParseTexure(List<FileSource> files, int count)
        {
            const int TextureBlockSize = 32 * 32;
            for (int i = 1, o = 0; i < count; i++, o += TextureBlockSize)
                files.Add(new FileSource
                {
                    Path = $"texs/{i:000}.tex",
                    Offset = o,
                    FileSize = TextureBlockSize,
                });
        }

        unsafe static void ParseSound(BinaryReader r, List<FileSource> files, int bankId)
        {
            r.BaseStream.Seek(-4, SeekOrigin.End);
            var offset = r.ReadUInt32();
            r.Seek(offset);
            var header = r.ReadS<SBK_Header>();
            var banks = r.ReadSArray<SBK_Entry>(9);
            // read bank
            var bank = banks[bankId];
            if (bank.EntryOffset == 0 || bank.Size == 0) throw new FormatException("BAD BANK");
            var bankOffset = bank.BankOffset;
            r.Seek(bank.EntryOffset);
            var numSamples = (int)bank.Size / sizeof(SBK_Sample);
            files.AddRange(r.ReadSArray<SBK_Sample>(numSamples).Select(s =>
                new FileSource
                {
                    Path = UnsafeX.FixedAString(s.Path, 18) ?? "DEFAULT.WAV",
                    Offset = (long)(bankOffset + s.Offset),
                    FileSize = s.DataSize,
                }
            ));
        }
    }
}
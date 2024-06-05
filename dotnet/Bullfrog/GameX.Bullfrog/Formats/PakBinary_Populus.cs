using GameX.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    public class PakBinary_Populus : PakBinary<PakBinary_Populus>
    {
        #region Factories

        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactoryFactory(FileSource source, FamilyGame game)
            => source.Path.ToLowerInvariant() switch
            {
                _ => Path.GetExtension(source.Path).ToLowerInvariant() switch
                {
                    ".pal" => (0, Binary_Pal.Factory_3),
                    ".wav" => (0, Binary_Snd.Factory),
                    ".raw" => (0, Binary_Raw.FactoryMethod(Binary_RawFunc, (id, value) => id switch
                    {
                        "P2" => Games.P2.Database.GetPalette(value, "DATA/MQAZ"),
                        _ => throw new ArgumentOutOfRangeException(nameof(game.Id), game.Id),
                    })),
                    _ => (0, null),
                }
            };

        static void Binary_RawFunc(Binary_Raw s, BinaryReader r, FileSource f)
        {
            if (f.Tag == null)
            {
                s.Body = r.ReadToEnd();
                s.Palette = f.Path[..^4];
                switch (s.Body.Length)
                {
                    case 64000: s.Width = 320; s.Height = 200; break;
                    //case 307200: s.Width = 640; s.Height = 480; break;
                    default: throw new ArgumentOutOfRangeException(nameof(s.Body), $"{s.Body.Length}");
                };
            }
        }

        #endregion

        #region Header

        const uint MAGIC_SPR = 0x42465350;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct SPR_Record
        {
            public static (string, int) Struct = ("<2HI", sizeof(SPR_Record));
            public ushort Width;
            public ushort Height;
            public uint Offset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct DAT_Sprite
        {
            public static (string, int) Struct = ("<2xI", sizeof(DAT_Sprite));
            public byte Width;
            public byte Height;
            public ushort Unknown;
            public ushort Offset;
        }

        #endregion

        public override async Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            List<FileSource> files;
            source.Files = files = new List<FileSource>();
            var tabPath = $"{source.PakPath[..^4]}.TAB";
            var fileName = Path.GetFileName(source.PakPath);
            switch (source.Game.Id)
            {
                case "P2":
                    if (fileName.StartsWith("EMSCBLK")) await source.Reader(s => ParseSprite(s, files), tabPath);
                    else if (fileName.StartsWith("EMSFACES") || fileName.StartsWith("MEMSFACE")) await source.Reader(s => ParseSprite(s, files), tabPath);
                    else if (fileName.StartsWith("EMSICON") || fileName.StartsWith("MEMICON")) await source.Reader(s => ParseSprite(s, files), tabPath);
                    else if (fileName.StartsWith("EMSSPR") || fileName.StartsWith("MEMSPR")) await source.Reader(s => ParseSprite(s, files), tabPath);
                    else if (fileName.StartsWith("HPOINTER") || fileName.StartsWith("MPOINTER")) await source.Reader(s => ParseSprite(s, files), tabPath);
                    else if (fileName.StartsWith("MBLOCK")) await source.Reader(s => ParseSprite(s, files), tabPath);
                    else if (fileName.StartsWith("LAND")) ParseOther(files, 10);
                    return;
                case "P3":
                    var magic = r.ReadUInt32();
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
                                return;
                            }
                        default: throw new FormatException("BAD MAGIC");
                    }
                default: return;
            }
        }

        unsafe static Task ParseSprite(BinaryReader r, List<FileSource> files)
        {
            r.Skip(4);
            var numSprites = (int)(r.BaseStream.Length - 4 / sizeof(DAT_Sprite));
            var sprites = r.ReadSArray<DAT_Sprite>(numSprites);
            var lastI = numSprites - 1;
            for (var i = 0; i < numSprites; i++)
            {
                ref DAT_Sprite s = ref sprites[i];
                files.Add(new FileSource
                {
                    Path = $"{i:000}.raw",
                    Offset = s.Offset,
                    FileSize = (i != lastI ? sprites[i + 1].Offset : r.BaseStream.Length) - s.Offset,
                    Tag = new Binary_Raw.Tag { Width = s.Width, Height = s.Height },
                });
            }
            return Task.CompletedTask;
        }

        static void ParseOther(List<FileSource> files, int count)
        {
            const int TextureBlockSize = 32 * 32;
            for (int i = 1, o = 0; i < count; i++, o += TextureBlockSize)
                files.Add(new FileSource
                {
                    Path = $"sprs/{i:000}.raw",
                    Offset = o,
                    FileSize = TextureBlockSize,
                    Tag = new Binary_Raw.Tag { Width = 32, Height = 32 },
                });
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            r.Seek(file.Offset);
            var bytes = r.ReadBytes((int)file.FileSize);
            return Task.FromResult((Stream)new MemoryStream(bytes));
        }
    }
}
using GameX.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    public class PakBinary_Bullfrog : PakBinary<PakBinary_Bullfrog>
    {
        #region Factories

        static readonly string[] SYND_FLIFILES = new[] { "INTRO.DAT", "MBRIEF.DAT", "MBRIEOUT.DAT", "MCONFOUT.DAT", "MCONFUP.DAT", "MCONSCR.DAT", "MDEBRIEF.DAT", "MDEOUT.DAT", "MENDLOSE.DAT", "MENDWIN.DAT", "MGAMEWIN.DAT", "MLOSA.DAT", "MLOSAOUT.DAT", "MLOSEGAM.DAT", "MMAP.DAT", "MMAPOUT.DAT", "MOPTION.DAT", "MOPTOUT.DAT", "MRESOUT.DAT", "MRESRCH.DAT", "MSCRENUP.DAT", "MSELECT.DAT", "MSELOUT.DAT", "MTITLE.DAT", "MMULTI.DAT", "MMULTOUT.DAT" };
        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactoryFactory(FileSource source, FamilyGame game)
         => game.Id switch
         {
             "S" => Path.GetFileName(source.Path).ToUpperInvariant() switch
             {
                 var x when SYND_FLIFILES.Contains(x) => (0, Binary_Syndicate.Factory_Fli),
                 "HFNT01.DAT" => (0, Binary_Syndicate.Factory_Font),
                 var x when x.StartsWith("GAME") && x.EndsWith(".DAT") => (0, Binary_Syndicate.Factory_Game),
                 "COL01.DAT" => (0, Binary_Syndicate.Factory_MapColumn),
                 var x when x.StartsWith("MAP") && x.EndsWith(".DAT") => (0, Binary_Syndicate.Factory_MapData),
                 "HBLK01.DAT" => (0, Binary_Syndicate.Factory_MapTile),
                 var x when x.StartsWith("MISS") && x.EndsWith(".DAT") => (0, Binary_Syndicate.Factory_Mission),
                 var x when x == "INTRO.XMI" || x == "SYNGAME.XMI" => (0, Binary_Iif.Factory),
                 var x when x.StartsWith("HPAL") && x.EndsWith(".DAT") || x == "HPALETTE.DAT" || x == "MSELECT.PAL" => (0, Binary_Syndicate.Factory_Palette),
                 var x when x == "MLOGOS.DAT" || x == "MMAPBLK.DAT" || x == "MMINLOGO.PAL" => (0, Binary_Syndicate.Factory_Raw),
                 "HREQ.DAT" => (0, Binary_Syndicate.Factory_Req),
                 var x when x.StartsWith("ISNDS-") && x.EndsWith(".DAT") || x.StartsWith("SOUND-") && x.EndsWith(".DAT") => (0, Binary_Syndicate.Factory_SoundData),
                 var x when x.StartsWith("ISNDS-") && x.EndsWith(".TAB") || x.StartsWith("SOUND-") && x.EndsWith(".TAB") => (0, Binary_Syndicate.Factory_SoundTab),
                 "HSTA-0.ANI" => (0, Binary_Syndicate.Factory_SpriteAnim),
                 "HFRA-0.ANI" => (0, Binary_Syndicate.Factory_SpriteFrame),
                 "HELE-0.ANI" => (0, Binary_Syndicate.Factory_SpriteElement),
                 var x when x == "HPOINTER.TAB" || x == "HSPR-0.TAB" || x == "MFNT-0.TAB" || x == "MSPR-0.TAB" => (0, Binary_Syndicate.Factory_SpriteTab),
                 var x when x == "HPOINTER.DAT" || x == "HSPR-0.DAT" || x == "MFNT-0.DAT" || x == "MSPR-0.DAT" => (0, Binary_Syndicate.Factory_SpriteData),
                 _ => throw new ArgumentOutOfRangeException(),
             },
             _ => default
         };
        //=> source.Path.ToLowerInvariant() switch
        //{
        //    _ => Path.GetExtension(source.Path).ToLowerInvariant() switch
        //    {
        //        ".pal" => (0, Binary_Pal.Factory_3),
        //        ".wav" => (0, Binary_Snd.Factory),
        //        var x when x == ".tex" || x == ".raw" => (0, Binary_Raw.FactoryMethod(Binary_RawFunc, (id, value) => id switch
        //        {
        //            "DK" => Games.DK.Database.GetPalette(value, "DATA/MAIN"),
        //            "DK2" => Games.DK2.Database.GetPalette(value, "DATA/MAIN"),
        //            _ => throw new ArgumentOutOfRangeException(nameof(game.Id), game.Id),
        //        })),
        //        _ => (0, null),
        //    }
        //};

        static void Binary_RawFunc(Binary_Raw s, BinaryReader r, FileSource f)
        {
            s.Body = r.Peek(x => x.ReadUInt32()) == Rnc.RNC_MAGIC ? Rnc.Read(r) : r.ReadToEnd();
            if (f.Tag == null)
            {
                s.Palette = f.Path.EndsWith(".bmp") ? "" : f.Path[..^4];
                switch (s.Body.Length)
                {
                    case 1024: s.Width = 32; s.Height = 32; break;
                    case 64000: s.Width = 320; s.Height = 200; break;
                    case 307200: s.Width = 640; s.Height = 480; break;
                    case 1228800: s.Width = 640; s.Height = 1920; break;
                    default: throw new ArgumentOutOfRangeException(nameof(s.Body), $"{s.Body.Length}");
                };
            }
        }

        #endregion

        #region Headers

        const int TEXTURE_BLOCKSA = 544; // Static textures in tmapa
        const int TEXTURE_BLOCKSB = 544; // Static textures in tmapb

        // Dungeon Keeper
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct SBK_Header
        {
            public static (string, int) Struct = ("<14sI", sizeof(SBK_Header));
            public fixed byte Unknown1[14];
            public uint Unknown2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct SBK_Entry
        {
            public static (string, int) Struct = ("<4I", sizeof(SBK_Entry));
            public uint EntryOffset;
            public uint BankOffset;
            public uint Size;
            public uint Unknown;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct SBK_Sample
        {
            public static (string, int) Struct = ("<18QI2x", sizeof(SBK_Sample));
            public fixed byte Path[18];
            public ulong Offset;
            public uint DataSize;
            public byte Sfxid;
            public byte Unknown;
        }

        // Dungeon Keeper
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
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
            var tabPath = $"{source.PakPath[..^4]}.TAB";
            var fileName = Path.GetFileName(source.PakPath);
            if (fileName.StartsWith("CREATURE")) await source.Reader(s => ParseCreature(s, files), tabPath);
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
                    Tag = new Binary_Raw.Tag { Width = s.SWidth, Height = s.SHeight },
                });
            }
            return Task.CompletedTask;
        }

        static void ParseGui(BinaryReader r, List<FileSource> files)
        {
            const int TextureBlockSize = 100 * 100;
            for (int i = 1, o = 0; i < 1; i++, o += TextureBlockSize)
                files.Add(new FileSource
                {
                    Path = $"gui/{i:000}.tex",
                    Offset = o,
                    FileSize = TextureBlockSize,
                    Tag = new Binary_Raw.Tag { Width = 32, Height = 32 },
                });
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
                    Tag = new Binary_Raw.Tag { Width = 32, Height = 32 },
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
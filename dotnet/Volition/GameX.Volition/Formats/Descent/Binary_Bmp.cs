using GameX.Platforms;
using OpenStack.Gfx;
using OpenStack.Gfx.Textures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static GameX.Volition.Formats.PakBinary_Descent;

namespace GameX.Volition.Formats.Descent
{
    public class Binary_Bmp : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Bmp(r, s.Game, f.Tag));

        public Binary_Bmp(BinaryReader r, FamilyGame game, object tag)
        {
            Format = (
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGBA32,
                TextureUnrealFormat.Unknown);

            // get body
            game.Ensure();
            Body = r.ReadToEnd();

            // parse tag
            if (tag is ValueTuple<PIG_Flags, short, short> b)
            {
                PigFlags = b.Item1;
                Width = b.Item2;
                Height = b.Item3;
            }
            else throw new ArgumentOutOfRangeException(nameof(tag), tag.ToString());

            // get palette
            Palette = game.Id switch
            {
                "D" => Games.D.Database.Palette.Records,
                "D2" => Games.D2.Database.Palette.Records,
                _ => throw new ArgumentOutOfRangeException(nameof(game.Id), game.Id),
            };
        }

        PIG_Flags PigFlags;
        byte[] Body;
        byte[][] Palette;
        (object gl, object vulken, object unity, object unreal) Format;

        public int Width { get; }
        public int Height { get; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags TexFlags { get; } = 0;

        /// <summary>
        /// Set a color using palette index
        /// </summary>
        /// <param name="palette"></param>
        /// <param name="pixels"></param>
        /// <param name="pixel"></param>
        /// <param name="color"></param>
        static void SetPixel(byte[][] palette, byte[] pixels, ref int pixel, int color)
        {
            var record = palette[color];
            pixels[pixel + 0] = record[0];
            pixels[pixel + 1] = record[1];
            pixels[pixel + 2] = record[2];
            pixels[pixel + 3] = 255; // alpha channel
            pixel += 4;
        }

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
        {
            byte[] DecodeRLE()
            {
                var palette = Palette;
                var pixels = new byte[Width * Height * 4];
                var pixel = 0;
                var ofs = 0;
                var size = BitConverter.ToUInt32(Body);
                var ofsEnd = ofs + size;
                ofs += 4;
                ofs += (PigFlags & PIG_Flags.RLEBIG) != 0 ? Height * 2 : Height;
                while (ofs < ofsEnd)
                {
                    var b = Body[ofs++];
                    if ((b & 0xe0) == 0xe0)
                    {
                        var c = b & 0x1f;
                        if (c == 0) continue;
                        b = Body[ofs++];
                        for (var i = 0; i < c; i++) SetPixel(palette, pixels, ref pixel, b);
                    }
                    else SetPixel(palette, pixels, ref pixel, b);
                }
                return pixels;
            }

            byte[] DecodeRaw() => Body.SelectMany(s => Palette[s]).ToArray();

            return ((PigFlags & (PIG_Flags.RLE | PIG_Flags.RLEBIG)) != 0
                ? DecodeRLE()
                : DecodeRaw(), (Platform.Type)platform switch
                {
                    Platform.Type.OpenGL => Format.gl,
                    Platform.Type.Vulken => Format.vulken,
                    Platform.Type.Unity => Format.unity,
                    Platform.Type.Unreal => Format.unreal,
                    _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
                }, null);
        }
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new MetaInfo($"{nameof(Binary_Bmp)}", items: new List<MetaInfo> {
                new MetaInfo($"PigFlags: {PigFlags}"),
                new MetaInfo($"Width: {Width}"),
                new MetaInfo($"Height: {Height}"),
            })
        };
    }
}

/*
if (tag is PIG_Bitmap b)
{
    var width = b.Width + ((b.DFlags & PIG_DFlag.LARGE) != 0 ? 256U : 0U);
    var height = b.Height;
    var dataSize = width * height * 4;
    var s = new MemoryStream();
    // write header
    var w = new BinaryWriter(s);
    w.WriteT(new BmpHeader
    {
        Type = 0x4d42,
        Size = (uint)sizeof(BmpHeader) + dataSize,
        OffBits = (uint)sizeof(BmpHeader),
        Info = new BmpInfoHeader
        {
            Size = (uint)sizeof(BmpInfoHeader),
            Width = width,
            Height = height,
            Planes = 1,
            BitCount = 3,
            Compression = 1,
            SizeImage = 0,
            XPixelsPerM = 0,
            YPixelsPerM = 0,
            ColorsUsed = 256,
            ColorsImportant = 0,
        }
    });
    w.Write(r.ReadBytes((int)dataSize));
    s.Position = 0;
    File.WriteAllBytes(@"C:\T_\test.bmp", s.ToArray());
    return Task.FromResult<Stream>(s);
}
*/
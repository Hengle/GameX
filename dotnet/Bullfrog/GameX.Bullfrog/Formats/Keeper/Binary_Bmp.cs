using GameX.Meta;
using GameX.Platforms;
using OpenStack.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static GameX.Bullfrog.Formats.PakBinary_Keeper;

namespace GameX.Bullfrog.Formats.Keeper
{
    public class Binary_Bmp : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Bmp(r, s.Game, f.Path, f.Tag));

        public Binary_Bmp(BinaryReader r, FamilyGame game, string path, object tag)
        {
            Format = (
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGBA32,
                TextureUnrealFormat.Unknown);

            // get body
            game.Ensure();
            var magic = r.Peek(x => x.ReadUInt32());
            Body = magic == Rnc.RNC_MAGIC ? Rnc.Read(r) : r.ReadToEnd();

            // default tag
            if (tag == null)
            {
                var pal = path.EndsWith(".bmp") ? "" : path[..^4];
                tag = Body.Length switch
                {
                    1024 => (pal, 32, 32),
                    64000 => (pal, 320, 200),
                    307200 => (pal, 640, 480),
                    1228800 => (object)(pal, 640, 1920),
                    _ => throw new ArgumentOutOfRangeException(nameof(Body), $"{Body.Length}"),
                };
            }

            // parse tag
            if (tag is ValueTuple<string, int, int> b)
            {
                PaletteFile = b.Item1;
                Width = b.Item2;
                Height = b.Item3;
            }
            else if (tag is CRE_Sprite c)
            {
                Width = c.FrameWidth;
                Height = c.FrameHeight * 2;
            }
            else throw new ArgumentOutOfRangeException(nameof(tag), tag.ToString());

            // get palette
            Palette = game.Id switch
            {
                "DK" => Games.DK.Database.GetPalette(PaletteFile, "DATA/MAIN").Records,
                _ => throw new ArgumentOutOfRangeException(nameof(game.Id), game.Id),
            };
        }

        byte[] Body;
        byte[][] Palette;
        string PaletteFile;
        (object gl, object vulken, object unity, object unreal) Format;

        public IDictionary<string, object> Data { get; } = null;
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags Flags { get; } = 0;

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

        public void Select(int id) { }
        public byte[] Begin(int platform, out object format, out Range[] ranges)
        {
            byte[] DecodeRaw() => Body.SelectMany(s => Palette[s]).ToArray();

            var bytes = DecodeRaw();
            format = (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            };
            ranges = null;
            return bytes;
        }
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new MetaInfo($"{nameof(Binary_Bmp)}", items: new List<MetaInfo> {
                new MetaInfo($"Palette: {PaletteFile}"),
                new MetaInfo($"Width: {Width}"),
                new MetaInfo($"Height: {Height}"),
            })
        };
    }
}
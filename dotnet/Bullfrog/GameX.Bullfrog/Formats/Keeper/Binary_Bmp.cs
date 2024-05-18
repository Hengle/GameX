using GameX.Meta;
using GameX.Platforms;
using OpenStack.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats.Keeper
{
    public class Binary_Bmp : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Bmp(r, s.Game, f.Tag));

        public Binary_Bmp(BinaryReader r, FamilyGame game, object tag)
        {
            game.Ensure();
            Palette = game.Id switch
            {
                "DK" => Games.DK.Database.Palette.Records,
                _ => throw new ArgumentOutOfRangeException(nameof(game.Id), game.Id),
            };

            Format = (
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGBA32,
                TextureUnrealFormat.Unknown);
            Body = r.ReadToEnd();
            Width = 32;
            Height = 32;
        }

        byte[] Body;
        byte[][] Palette;
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
                new MetaInfo($"Width: {Width}"),
                new MetaInfo($"Height: {Height}"),
            })
        };
    }
}
using OpenStack.Gfx;
using System;
using UnrealEngine.Framework;

namespace GameX.Platforms
{
    public class UnrealTextureBuilder : TextureBuilderBase<Texture2D>
    {
        Texture2D _defaultTexture;
        public override Texture2D DefaultTexture => _defaultTexture ??= CreateDefaultTexture();

        public void Release()
        {
            if (_defaultTexture != null) { /*DeleteTexture(_defaultTexture);*/ _defaultTexture = null; }
        }

        Texture2D CreateDefaultTexture() => CreateSolidTexture(4, 4, new[]
        {
            0.9f, 0.2f, 0.8f, 1f,
            0f, 0.9f, 0f, 1f,
            0.9f, 0.2f, 0.8f, 1f,
            0f, 0.9f, 0f, 1f,

            0f, 0.9f, 0f, 1f,
            0.9f, 0.2f, 0.8f, 1f,
            0f, 0.9f, 0f, 1f,
            0.9f, 0.2f, 0.8f, 1f,

            0.9f, 0.2f, 0.8f, 1f,
            0f, 0.9f, 0f, 1f,
            0.9f, 0.2f, 0.8f, 1f,
            0f, 0.9f, 0f, 1f,

            0f, 0.9f, 0f, 1f,
            0.9f, 0.2f, 0.8f, 1f,
            0f, 0.9f, 0f, 1f,
            0.9f, 0.2f, 0.8f, 1f,
        });

        public override Texture2D CreateTexture(Texture2D reuse, ITexture source, Range? level = null)
        {
            Debug.Log(LogLevel.Display, "BuildTexture");
            var (bytes, format, _) = source.Begin((int)Platform.Type.Unreal);
            if (format is TextureUnrealFormat unrealFormat)
            {
                var pixelFormat = (PixelFormat)unrealFormat;
                Debug.Log(LogLevel.Display, $"bytes: {bytes.Length}");
                Debug.Log(LogLevel.Display, $"Width: {source.Width}");
                Debug.Log(LogLevel.Display, $"Height: {source.Height}");
                Debug.Log(LogLevel.Display, $"PixelFormat: {pixelFormat}");
                //var tex = new Texture2D(source.Width, source.Height, pixelFormat, "Texture");
                //return tex;
                return null;
            }
            else throw new ArgumentOutOfRangeException(nameof(format), $"{format}");
        }

        public override Texture2D CreateSolidTexture(int width, int height, float[] pixels)
        {
            return null;
        }

        public override Texture2D CreateNormalMap(Texture2D texture, float strength)
        {
            throw new NotImplementedException();
        }

        public override void DeleteTexture(Texture2D texture) { }
    }
}
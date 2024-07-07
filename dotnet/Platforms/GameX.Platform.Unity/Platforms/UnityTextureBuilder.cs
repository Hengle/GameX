using OpenStack.Graphics;
using OpenStack.Graphics.DirectX;
using System;
using UnityEngine;

namespace GameX.Platforms
{
    public class UnityTextureBuilder : TextureBuilderBase<Texture2D>
    {
        Texture2D _defaultTexture;
        public override Texture2D DefaultTexture => _defaultTexture ??= CreateDefaultTexture();

        public void Release()
        {
            if (_defaultTexture != null) { UnityEngine.Object.Destroy(_defaultTexture); _defaultTexture = null; }
        }

        Texture2D CreateDefaultTexture() => new Texture2D(4, 4);

        public override Texture2D CreateTexture(Texture2D reuse, ITexture source, Range? range = null)
        {
            var (bytes, format, _) = source.Begin((int)Platform.Type.Unity);
            if (format is TextureUnityFormat unityFormat)
            {
                if (unityFormat == TextureUnityFormat.DXT3_POLYFILL)
                {
                    unityFormat = TextureUnityFormat.DXT5;
                    DDS_HEADER.ConvertDxt3ToDtx5(bytes, source.Width, source.Height, source.MipMaps);
                }
                var textureFormat = (TextureFormat)unityFormat;
                var tex = new Texture2D(source.Width, source.Height, textureFormat, source.MipMaps, false);
                tex.LoadRawTextureData(bytes);
                tex.Apply();
                tex.Compress(true);
                return tex;
            }
            //else if (format is ValueTuple<TextureUnityFormat> unityPixelFormat)
            //{
            //    var textureFormat = (TextureFormat)unityPixelFormat.Item1;
            //    var tex = new Texture2D(source.Width, source.Height, textureFormat, source.MipMaps, false);
            //    return tex;
            //}
            else throw new ArgumentOutOfRangeException(nameof(format), $"{format}");
        }

        public override Texture2D CreateSolidTexture(int width, int height, float[] rgba) => new Texture2D(width, height);

        public override Texture2D CreateNormalMap(Texture2D texture, float strength)
        {
            strength = Mathf.Clamp(strength, 0.0F, 1.0F);
            float xLeft, xRight, yUp, yDown, yDelta, xDelta;
            var normalTexture = new Texture2D(texture.width, texture.height, TextureFormat.ARGB32, true);
            for (var y = 0; y < normalTexture.height; y++)
                for (var x = 0; x < normalTexture.width; x++)
                {
                    xLeft = texture.GetPixel(x - 1, y).grayscale * strength;
                    xRight = texture.GetPixel(x + 1, y).grayscale * strength;
                    yUp = texture.GetPixel(x, y - 1).grayscale * strength;
                    yDown = texture.GetPixel(x, y + 1).grayscale * strength;
                    xDelta = (xLeft - xRight + 1) * 0.5f;
                    yDelta = (yUp - yDown + 1) * 0.5f;
                    normalTexture.SetPixel(x, y, new UnityEngine.Color(xDelta, yDelta, 1.0f, yDelta));
                }
            normalTexture.Apply();
            return normalTexture;
        }

        public override void DeleteTexture(Texture2D texture) => UnityEngine.Object.Destroy(texture);
    }
}
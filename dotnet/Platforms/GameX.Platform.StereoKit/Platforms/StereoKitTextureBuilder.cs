using OpenStack.Graphics;
using StereoKit;
using System;

namespace GameX.Platforms
{
    public class StereoKitTextureBuilder : TextureBuilderBase<Tex>
    {
        public void Release()
        {
            if (_defaultTexture != null) { /*Object.Destroy(_defaultTexture);*/ _defaultTexture = null; }
        }

        Tex _defaultTexture;
        public override Tex DefaultTexture => _defaultTexture != null ? _defaultTexture : _defaultTexture = CreateDefaultTexture();

        Tex CreateDefaultTexture() => CreateSolidTexture(4, 4, new[]
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

        public override Tex CreateTexture(Tex reuse, ITexture source, Range? range = null)
        {
            return default;
        }

        public override Tex CreateSolidTexture(int width, int height, float[] rgba)
        {
            return default;
        }

        public override Tex CreateNormalMap(Tex texture, float strength) => throw new NotImplementedException();

        public override void DeleteTexture(Tex texture) { }
    }
}
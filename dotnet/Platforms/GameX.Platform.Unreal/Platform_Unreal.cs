using OpenStack.Gfx;
using OpenStack.Sfx;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnrealEngine.Framework;
using Framework_Debug = UnrealEngine.Framework.Debug;
using Framework_LogLevel = UnrealEngine.Framework.LogLevel;
using Debug = OpenStack.Debug;

namespace GameX.Platforms
{
    // UnrealExtensions
    public static class UnrealExtensions { }

    // UnrealObjectBuilder
    // MISSING

    // UnrealShaderBuilder
    // MISSING

    // UnrealTextureBuilder
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
            Framework_Debug.Log(Framework_LogLevel.Display, "BuildTexture");
            var (bytes, format, _) = source.Begin((int)Platform.Type.Unreal);
            if (format is TextureUnrealFormat unrealFormat)
            {
                var pixelFormat = (PixelFormat)unrealFormat;
                Framework_Debug.Log(Framework_LogLevel.Display, $"bytes: {bytes.Length}");
                Framework_Debug.Log(Framework_LogLevel.Display, $"Width: {source.Width}");
                Framework_Debug.Log(Framework_LogLevel.Display, $"Height: {source.Height}");
                Framework_Debug.Log(Framework_LogLevel.Display, $"PixelFormat: {pixelFormat}");
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

    // UnityMaterialBuilder
    // MISSING

    // IUnrealGfx
    public interface IUnrealGfx : IOpenGfxAny<object, object, Texture2D, object> { }

    // UnrealGfx
    public class UnrealGfx : IUnrealGfx
    {
        readonly PakFile _source;
        readonly ITextureManager<Texture2D> _textureManager;

        public UnrealGfx(PakFile source)
        {
            _source = source;
            _textureManager = new TextureManager<Texture2D>(source, new UnrealTextureBuilder());
        }

        public PakFile Source => _source;
        public ITextureManager<Texture2D> TextureManager => _textureManager;
        public IMaterialManager<object, Texture2D> MaterialManager => throw new NotImplementedException();
        public IObjectManager<object, object, Texture2D> ObjectManager => throw new NotImplementedException();
        public IShaderManager<object> ShaderManager => throw new NotImplementedException();
        public Texture2D CreateTexture(object path, Range? level = null) => _textureManager.CreateTexture(path, level).tex;
        public void PreloadTexture(object path) => throw new NotImplementedException();
        public object CreateObject(object path) => throw new NotImplementedException();
        public void PreloadObject(object path) => throw new NotImplementedException();
        public object CreateShader(object path, IDictionary<string, bool> args = null) => throw new NotImplementedException();

        public Task<T> LoadFileObject<T>(object path) => _source.LoadFileObject<T>(path);
    }

    // UnrealSfx
    public class UnrealSfx : SystemSfx
    {
        public UnrealSfx(PakFile source) : base(source) { }
    }

    // UnrealPlatform
    public static class UnrealPlatform
    {
        public static unsafe bool Startup()
        {
            Framework_Debug.Log(Framework_LogLevel.Display, "Startup");
            try
            {
                Platform.PlatformType = Platform.Type.Unreal;
                Platform.GfxFactory = source => new UnrealGfx(source);
                Platform.SfxFactory = source => new UnrealSfx(source);
                Debug.AssertFunc = x => System.Diagnostics.Debug.Assert(x);
                Debug.LogFunc = a => Framework_Debug.Log(Framework_LogLevel.Display, a);
                Debug.LogFormatFunc = (a, b) => Framework_Debug.Log(Framework_LogLevel.Display, string.Format(a, b));
                Framework_Debug.Log(Framework_LogLevel.Display, "Startup:GOOD");
                return true;
            }
            catch { return false; }
        }
    }
}
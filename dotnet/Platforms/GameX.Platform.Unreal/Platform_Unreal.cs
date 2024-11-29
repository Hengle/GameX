using OpenStack.Gfx;
using OpenStack.Gfx.Textures;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnrealEngine.Framework;
using static OpenStack.Gfx.Textures.TextureFormat;
using Debug = OpenStack.Debug;
using Framework_Debug = UnrealEngine.Framework.Debug;
using Framework_LogLevel = UnrealEngine.Framework.LogLevel;

namespace GameX.Platforms
{
    // UnrealExtensions
    public static class UnrealExtensions { }

    // UnrealObjectBuilder : MISSING

    // UnrealShaderBuilder : MISSING

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
            var (bytes, fmt, _) = source.Begin("UR");
            try
            {
                if (bytes == null) return DefaultTexture;
                else if (fmt is ValueTuple<TextureFormat, TexturePixel> z)
                {
                    var (format, pixel) = z;
                    var s = (pixel & TexturePixel.Signed) != 0;
                    var f = (pixel & TexturePixel.Float) != 0;
                    var pixelFormat = format switch
                    {
                        DXT1 => PixelFormat.DXT1,
                        DXT3 => PixelFormat.DXT3,
                        DXT5 => PixelFormat.DXT5,
                        BC4 => PixelFormat.BC4,
                        BC5 => PixelFormat.BC5,
                        BC6H => PixelFormat.BC6H,
                        BC7 => PixelFormat.BC7,
                        ETC2 => PixelFormat.ETC2RGB,
                        ETC2_EAC => PixelFormat.ETC2RGBA,
                        //
                        I8 => default,
                        L8 => default,
                        R8 => PixelFormat.R8,
                        R16 => f ? PixelFormat.R16F : s ? PixelFormat.R16SInt : PixelFormat.R16UInt,
                        RG16 => f ? PixelFormat.R8G8 : PixelFormat.R16G16UInt,
                        RGB24 => f ? PixelFormat.FloatRGB : default,
                        RGB565 => PixelFormat.R5G6B5UNorm,
                        RGBA32 => f ? PixelFormat.FloatRGBA : PixelFormat.R8G8B8A8,
                        ARGB32 => PixelFormat.A8R8G8B8,
                        BGRA32 => PixelFormat.B8G8R8A8,
                        BGRA1555 => PixelFormat.B5G5R5A1UNorm,
                        _ => throw new ArgumentOutOfRangeException("TextureFormat", $"{format}")
                    };
                    Framework_Debug.Log(Framework_LogLevel.Display, $"bytes: {bytes.Length}");
                    Framework_Debug.Log(Framework_LogLevel.Display, $"Width: {source.Width}");
                    Framework_Debug.Log(Framework_LogLevel.Display, $"Height: {source.Height}");
                    Framework_Debug.Log(Framework_LogLevel.Display, $"PixelFormat: {pixelFormat}");
                    //var tex = new Texture2D(source.Width, source.Height, pixelFormat, "Texture");
                    //return tex;
                    return null;
                }
                else throw new ArgumentOutOfRangeException(nameof(fmt), $"{fmt}");
            }
            finally { source.End(); }
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

    // UnrealMaterialBuilder : MISSING

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
    public class UnrealSfx(PakFile source) : SystemSfx(source)
    {
    }

    // UnrealPlatform
    public static class UnrealPlatform
    {
        public static unsafe bool Startup()
        {
            Framework_Debug.Log(Framework_LogLevel.Display, "Startup");
            try
            {
                Platform.PlatformType = "UR";
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
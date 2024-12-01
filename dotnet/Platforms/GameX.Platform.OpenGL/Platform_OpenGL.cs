using OpenStack;
using OpenStack.Gfx;
using OpenStack.Gfx.Gl;
using OpenStack.Gfx.Gl.Renders;
using OpenStack.Gfx.Textures;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using static OpenStack.Gfx.Textures.TextureFormat;

namespace GameX.Platforms
{
    /// <summary>
    /// OpenGLExtensions
    /// </summary>
    public static class OpenGLExtensions { }

    /// <summary>
    /// OpenGLObjectBuilder
    /// </summary>
    public class OpenGLObjectBuilder : ObjectBuilderBase<object, GLRenderMaterial, int>
    {
        public override void EnsurePrefab() { }
        public override object CreateNewObject(object prefab) => throw new NotImplementedException();
        public override object CreateObject(object path, IMaterialManager<GLRenderMaterial, int> materialManager) => throw new NotImplementedException();
    }

    /// <summary>
    /// OpenGLShaderBuilder
    /// </summary>
    public class OpenGLShaderBuilder : ShaderBuilderBase<Shader>
    {
        static readonly ShaderLoader _loader = new ShaderDebugLoader();
        public override Shader CreateShader(object path, IDictionary<string, bool> args = null) => _loader.CreateShader(path, args);
    }

    /// <summary>
    /// OpenGLTextureBuilder
    /// </summary>
    public unsafe class OpenGLTextureBuilder : TextureBuilderBase<int>
    {
        int _defaultTexture = -1;
        public override int DefaultTexture => _defaultTexture > -1 ? _defaultTexture : _defaultTexture = CreateDefaultTexture();

        public void Release()
        {
            if (_defaultTexture > -1) { GL.DeleteTexture(_defaultTexture); _defaultTexture = -1; }
        }

        int CreateDefaultTexture() => CreateSolidTexture(4, 4, [
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
        ]);

        public override int CreateTexture(int reuse, ITexture source, Range? level2 = null)
        {
            var id = reuse != 0 ? reuse : GL.GenTexture();
            var numMipMaps = Math.Max(1, source.MipMaps);
            (int start, int stop) level = (level2?.Start.Value ?? 0, numMipMaps);

            // bind
            GL.BindTexture(TextureTarget.Texture2D, id);
            if (level.start > 0) GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, level.start);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, level.stop - 1);
            var (bytes, fmt, spans) = source.Begin("GL");

            // decode
            bool CompressedTexImage2D(ITexture source, (int start, int stop) level, InternalFormat internalFormat)
            {
                int width = source.Width, height = source.Height;
                if (spans != null)
                    for (var l = level.start; l < level.stop; l++)
                    {
                        var span = spans[l];
                        if (span.Start.Value < 0) return false;
                        var pixels = bytes.AsSpan(span);
                        fixed (byte* data = pixels) GL.CompressedTexImage2D(TextureTarget.Texture2D, l, internalFormat, width >> l, height >> l, 0, pixels.Length, (IntPtr)data);
                    }
                else fixed (byte* data = bytes) GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0, bytes.Length, (IntPtr)data);
                return true;
            }
            bool TexImage2D(ITexture source, (int start, int stop) level, PixelInternalFormat internalFormat, PixelFormat format, PixelType type)
            {
                int width = source.Width, height = source.Height;
                if (spans != null)
                    for (var l = level.start; l < level.stop; l++)
                    {
                        var span = spans[l];
                        if (span.Start.Value < 0) return false;
                        var pixels = bytes.AsSpan(span);
                        fixed (byte* data = pixels) GL.TexImage2D(TextureTarget.Texture2D, l, internalFormat, width >> l, height >> l, 0, format, type, (IntPtr)data);
                    }
                else fixed (byte* data = bytes) GL.TexImage2D(TextureTarget.Texture2D, 0, internalFormat, width, height, 0, format, type, (IntPtr)data);
                return true;
            }

            try
            {
                if (bytes == null) return DefaultTexture;
                else if (fmt is ValueTuple<TextureFormat, TexturePixel> z)
                {
                    var (formatx, pixel) = z;
                    var s = (pixel & TexturePixel.Signed) != 0;
                    var f = (pixel & TexturePixel.Float) != 0;
                    if ((formatx & Compressed) != 0)
                    {
                        var internalFormat = formatx switch
                        {
                            DXT1 => s ? InternalFormat.CompressedSrgbS3tcDxt1Ext : InternalFormat.CompressedRgbS3tcDxt1Ext,
                            DXT1A => s ? InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext : InternalFormat.CompressedRgbaS3tcDxt1Ext,
                            DXT3 => s ? InternalFormat.CompressedSrgbAlphaS3tcDxt3Ext : InternalFormat.CompressedRgbaS3tcDxt3Ext,
                            DXT5 => s ? InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext : InternalFormat.CompressedRgbaS3tcDxt5Ext,
                            BC4 => s ? InternalFormat.CompressedSignedRedRgtc1 : InternalFormat.CompressedRedRgtc1,
                            BC5 => s ? InternalFormat.CompressedSignedRgRgtc2 : InternalFormat.CompressedRgRgtc2,
                            BC6H => s ? InternalFormat.CompressedRgbBptcSignedFloat : InternalFormat.CompressedRgbBptcUnsignedFloat,
                            BC7 => s ? InternalFormat.CompressedSrgbAlphaBptcUnorm : InternalFormat.CompressedRgbaBptcUnorm,
                            ETC2 => s ? InternalFormat.CompressedSrgb8Etc2 : InternalFormat.CompressedRgb8Etc2,
                            ETC2_EAC => s ? InternalFormat.CompressedSrgb8Alpha8Etc2Eac : InternalFormat.CompressedRgba8Etc2Eac,
                            _ => throw new ArgumentOutOfRangeException("TextureFormat", $"{formatx}")
                        };
                        if (internalFormat == 0 || !CompressedTexImage2D(source, level, internalFormat)) return DefaultTexture;
                    }
                    else
                    {
                        var (internalFormat, format, type) = formatx switch
                        {
                            I8 => (PixelInternalFormat.Intensity8, PixelFormat.Red, PixelType.UnsignedByte),
                            L8 => (PixelInternalFormat.Luminance, PixelFormat.Luminance, PixelType.UnsignedByte),
                            R8 => (PixelInternalFormat.R8, PixelFormat.Red, PixelType.UnsignedByte),
                            R16 => f ? (PixelInternalFormat.R16f, PixelFormat.Red, PixelType.Float) : (PixelInternalFormat.R16, PixelFormat.Red, PixelType.UnsignedShort),
                            RG16 => f ? (PixelInternalFormat.Rg16f, PixelFormat.Red, PixelType.Float) : (PixelInternalFormat.Rg16, PixelFormat.Red, PixelType.UnsignedShort),
                            RGB24 => (PixelInternalFormat.Rgb8, PixelFormat.Rgb, PixelType.UnsignedByte),
                            RGB565 => (PixelInternalFormat.Rgb5, PixelFormat.Rgb, PixelType.UnsignedByte), //UnsignedShort565
                            RGBA32 => (PixelInternalFormat.Rgba8, PixelFormat.Rgba, PixelType.UnsignedByte),
                            ARGB32 => (PixelInternalFormat.Rgba, PixelFormat.Rgb, PixelType.UnsignedInt8888Reversed), //: odd Rgb
                            BGRA32 => (PixelInternalFormat.Rgba, PixelFormat.Bgra, PixelType.UnsignedInt8888),
                            BGRA1555 => (PixelInternalFormat.Rgba, PixelFormat.Bgra, PixelType.UnsignedShort1555Reversed),
                            _ => throw new ArgumentOutOfRangeException("TextureFormat", $"{formatx}")
                        };
                        if (internalFormat == 0 || !TexImage2D(source, level, internalFormat, format, type)) return DefaultTexture;
                    }
                }
                else throw new ArgumentOutOfRangeException(nameof(fmt), $"{fmt}");

                // texture
                if (MaxTextureMaxAnisotropy >= 4)
                {
                    GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)ExtTextureFilterAnisotropic.TextureMaxAnisotropyExt, MaxTextureMaxAnisotropy);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                }
                else
                {
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                }
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)(source.TexFlags.HasFlag(TextureFlags.SUGGEST_CLAMPS) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat));
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)(source.TexFlags.HasFlag(TextureFlags.SUGGEST_CLAMPT) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat));
                GL.BindTexture(TextureTarget.Texture2D, 0); // release texture
                return id;
            }
            finally { source.End(); }
        }

        public override int CreateSolidTexture(int width, int height, float[] pixels)
        {
            var id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width, height, 0, PixelFormat.Rgba, PixelType.Float, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.BindTexture(TextureTarget.Texture2D, 0); // release texture
            return id;
        }

        public override int CreateNormalMap(int texture, float strength) => throw new NotImplementedException();

        public override void DeleteTexture(int texture) => GL.DeleteTexture(texture);
    }

    /// <summary>
    /// OpenGLMaterialBuilder
    /// </summary>
    public class OpenGLMaterialBuilder(TextureManager<int> textureManager) : MaterialBuilderBase<GLRenderMaterial, int>(textureManager)
    {
        GLRenderMaterial _defaultMaterial;
        public override GLRenderMaterial DefaultMaterial => _defaultMaterial ??= CreateDefaultMaterial(-1);

        GLRenderMaterial CreateDefaultMaterial(int type)
        {
            var m = new GLRenderMaterial(null);
            m.Textures["g_tColor"] = TextureManager.DefaultTexture;
            m.Material.ShaderName = "vrf.error";
            return m;
        }

        public override GLRenderMaterial CreateMaterial(object key)
        {
            switch (key)
            {
                case IMaterial s:
                    var m = new GLRenderMaterial(s);
                    switch (s)
                    {
                        case IFixedMaterial _: return m;
                        case IParamMaterial p:
                            foreach (var tex in p.TextureParams) m.Textures[tex.Key] = TextureManager.CreateTexture($"{tex.Value}_c").tex;
                            if (p.IntParams.ContainsKey("F_SOLID_COLOR") && p.IntParams["F_SOLID_COLOR"] == 1)
                            {
                                var a = p.VectorParams["g_vColorTint"];
                                m.Textures["g_tColor"] = TextureManager.CreateSolidTexture(1, 1, a.X, a.Y, a.Z, a.W);
                            }
                            if (!m.Textures.ContainsKey("g_tColor")) m.Textures["g_tColor"] = TextureManager.DefaultTexture;

                            // Since our shaders only use g_tColor, we have to find at least one texture to use here
                            if (m.Textures["g_tColor"] == TextureManager.DefaultTexture)
                                foreach (var name in new[] { "g_tColor2", "g_tColor1", "g_tColorA", "g_tColorB", "g_tColorC" })
                                    if (m.Textures.ContainsKey(name))
                                    {
                                        m.Textures["g_tColor"] = m.Textures[name];
                                        break;
                                    }

                            // Set default values for scale and positions
                            if (!p.VectorParams.ContainsKey("g_vTexCoordScale")) p.VectorParams["g_vTexCoordScale"] = Vector4.One;
                            if (!p.VectorParams.ContainsKey("g_vTexCoordOffset")) p.VectorParams["g_vTexCoordOffset"] = Vector4.Zero;
                            if (!p.VectorParams.ContainsKey("g_vColorTint")) p.VectorParams["g_vColorTint"] = Vector4.One;
                            return m;
                        default: throw new ArgumentOutOfRangeException(nameof(s));
                    }
                default: throw new ArgumentOutOfRangeException(nameof(key));
            }
        }
    }

    /// <summary>
    /// OpenGLGfx
    /// </summary>
    public class OpenGLGfx : IOpenGLGfx
    {
        readonly PakFile _source;
        readonly TextureManager<int> _textureManager;
        readonly MaterialManager<GLRenderMaterial, int> _materialManager;
        readonly ObjectManager<object, GLRenderMaterial, int> _objectManager;
        readonly ShaderManager<Shader> _shaderManager;

        public OpenGLGfx(PakFile source)
        {
            _source = source;
            _textureManager = new TextureManager<int>(source, new OpenGLTextureBuilder());
            _materialManager = new MaterialManager<GLRenderMaterial, int>(source, _textureManager, new OpenGLMaterialBuilder(_textureManager));
            _objectManager = new ObjectManager<object, GLRenderMaterial, int>(source, _materialManager, new OpenGLObjectBuilder());
            _shaderManager = new ShaderManager<Shader>(source, new OpenGLShaderBuilder());
            MeshBufferCache = new GLMeshBufferCache();
        }

        public PakFile Source => _source;
        public ITextureManager<int> TextureManager => _textureManager;
        public IMaterialManager<GLRenderMaterial, int> MaterialManager => _materialManager;
        public IObjectManager<object, GLRenderMaterial, int> ObjectManager => _objectManager;
        public IShaderManager<Shader> ShaderManager => _shaderManager;
        public int CreateTexture(object path, Range? level = null) => _textureManager.CreateTexture(path, level).tex;
        public void PreloadTexture(object path) => _textureManager.PreloadTexture(path);
        public object CreateObject(object path) => _objectManager.CreateObject(path).obj;
        public void PreloadObject(object path) => _objectManager.PreloadObject(path);
        public Shader CreateShader(object path, IDictionary<string, bool> args = null) => _shaderManager.CreateShader(path, args).sha;
        public Task<T> LoadFileObject<T>(object path) => _source.LoadFileObject<T>(path);

        // cache
        QuadIndexBuffer _quadIndices;
        public QuadIndexBuffer QuadIndices => _quadIndices ??= new QuadIndexBuffer(65532);
        public GLMeshBufferCache MeshBufferCache { get; }
    }

    /// <summary>
    /// OpenGLPlatform
    /// </summary>
    public static class OpenGLPlatform
    {
        public static unsafe bool Startup()
        {
            try
            {
                Platform.PlatformType = "GL";
                Platform.GfxFactory = source => new OpenGLGfx(source);
                Platform.SfxFactory = source => new SystemSfx(source);
                Debug.AssertFunc = x => System.Diagnostics.Debug.Assert(x);
                Debug.LogFunc = a => System.Diagnostics.Debug.Print(a);
                Debug.LogFormatFunc = (a, b) => System.Diagnostics.Debug.Print(a, b);
                return true;
            }
            catch { return false; }
        }
    }
}
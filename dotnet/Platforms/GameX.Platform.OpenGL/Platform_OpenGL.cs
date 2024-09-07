using OpenStack;
using OpenStack.Gfx;
using OpenStack.Gfx.Gl;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

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
            (int start, int stop) level = (level2?.Start.Value ?? 0, numMipMaps - 1);

            // bind
            GL.BindTexture(TextureTarget.Texture2D, id);
            if (level.start > 0) GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, level.start);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, level.stop - level.start);
            var (bytes, fmt, spans) = source.Begin((int)Platform.Type.OpenGL);
            if (bytes == null) return DefaultTexture;

            // decode
            bool CompressedTexImage2D(ITexture source, (int start, int stop) level, InternalFormat internalFormat)
            {
                int width = source.Width, height = source.Height;
                for (var l = level.start; l <= level.stop; l++)
                {
                    var span = spans[l];
                    if (span.Start.Value < 0) return false;
                    var pixels = bytes.AsSpan(span);
                    fixed (byte* data = pixels) GL.CompressedTexImage2D(TextureTarget.Texture2D, l, internalFormat, width >> l, height >> l, 0, pixels.Length, (IntPtr)data);
                }
                return true;
            }
            bool TexImage2D(ITexture source, (int start, int stop) level, PixelInternalFormat internalFormat, PixelFormat format, PixelType type)
            {
                int width = source.Width, height = source.Height;
                for (var l = level.start; l < level.stop; l++)
                {
                    var span = spans[l];
                    if (span.Start.Value < 0) return false;
                    var pixels = bytes.AsSpan(span);
                    fixed (byte* data = pixels) GL.TexImage2D(TextureTarget.Texture2D, l, internalFormat, width >> l, height >> l, 0, format, type, (IntPtr)data);
                }
                return true;
            }
            if (fmt is TextureGLFormat glFormat)
            {
                var internalFormat = glFormat;
                //if (glFormat == TextureGLFormat.CompressedRgbaS3tcDxt3Ext)
                //{
                //    glFormat = TextureGLFormat.CompressedRgbaS3tcDxt5Ext;
                //    DxtUtil2.ConvertDxt3ToDtx5(bytes, info.Width, info.Height, info.MipMaps);
                //}
                if (internalFormat == 0 || !CompressedTexImage2D(source, level, (InternalFormat)internalFormat)) return DefaultTexture;
            }
            else if (fmt is ValueTuple<TextureGLFormat, TextureGLPixelFormat, TextureGLPixelType> glPixelFormat)
            {
                var (internalFormat, format, type) = glPixelFormat;
                if (internalFormat == 0 || !TexImage2D(source, level, (PixelInternalFormat)internalFormat, (PixelFormat)format, (PixelType)type)) return DefaultTexture;
            }
            else throw new NotImplementedException();
            source.End();

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
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)(source.Flags.HasFlag(TextureFlags.SUGGEST_CLAMPS) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)(source.Flags.HasFlag(TextureFlags.SUGGEST_CLAMPT) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat));
            GL.BindTexture(TextureTarget.Texture2D, 0); // release texture
            return id;
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
        public QuadIndexBuffer QuadIndices => _quadIndices ?? (_quadIndices = new QuadIndexBuffer(65532));
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
                Platform.PlatformType = Platform.Type.OpenGL;
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
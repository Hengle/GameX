using OpenStack;
using OpenStack.Graphics;
using OpenStack.Graphics.OpenGL;
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
        public override void EnsurePrefabContainerExists() { }
        public override object CreateObject(object prefab) => throw new NotImplementedException();
        public override object BuildObject(object source, IMaterialManager<GLRenderMaterial, int> materialManager) => throw new NotImplementedException();
    }

    /// <summary>
    /// OpenGLShaderBuilder
    /// </summary>
    public class OpenGLShaderBuilder : ShaderBuilderBase<Shader>
    {
        static readonly ShaderLoader _loader = new ShaderDebugLoader();
        public override Shader BuildShader(string path, IDictionary<string, bool> args) => _loader.LoadShader(path, args);
        public override Shader BuildPlaneShader(string path, IDictionary<string, bool> args) => _loader.LoadPlaneShader(path, args);
    }

    /// <summary>
    /// OpenGLTextureBuilder
    /// </summary>
    public unsafe class OpenGLTextureBuilder : TextureBuilderBase<int>
    {
        public void Release()
        {
            if (_defaultTexture != 0) { GL.DeleteTexture(_defaultTexture); _defaultTexture = 0; }
        }

        int _defaultTexture = -1;
        public override int DefaultTexture => _defaultTexture > -1 ? _defaultTexture : _defaultTexture = BuildAutoTexture();

        int BuildAutoTexture() => BuildSolidTexture(4, 4, new[]
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

        public override int BuildTexture(ITexture info, Range? level = null)
        {
            //return DefaultTexture;
            var id = GL.GenTexture();
            var numMipMaps = Math.Max(1, info.MipMaps);
            var levelStart = level?.Start.Value ?? 0;
            var levelEnd = numMipMaps - 1;

            GL.BindTexture(TextureTarget.Texture2D, id);
            if (levelStart > 0) GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, levelStart);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, levelEnd - levelStart);
            var (bytes, fmt, spans) = info.Begin((int)Platform.Type.OpenGL);
            if (bytes == null) return DefaultTexture;

            bool CompressedTexImage2D(ITexture info, int i, InternalFormat internalFormat)
            {
                var span = spans != null ? spans[i] : Range.All;
                if (span.Start.Value < 0) return false;
                var width = info.Width >> i;
                var height = info.Height >> i;
                var pixels = bytes.AsSpan(span);
                fixed (byte* data = pixels) GL.CompressedTexImage2D(TextureTarget.Texture2D, i, internalFormat, width, height, 0, pixels.Length, (IntPtr)data);
                return true;
            }

            bool TexImage2D(ITexture info, int i, PixelInternalFormat internalFormat, PixelFormat format, PixelType type)
            {
                var span = spans != null ? spans[i] : Range.All;
                if (span.Start.Value < 0) return false;
                var width = info.Width >> i;
                var height = info.Height >> i;
                var pixels = bytes.AsSpan(span);
                fixed (byte* data = pixels) GL.TexImage2D(TextureTarget.Texture2D, i, internalFormat, width, height, 0, format, type, (IntPtr)data);
                return true;
            }

            if (fmt is TextureGLFormat glFormat)
            {
                //if (glFormat == TextureGLFormat.CompressedRgbaS3tcDxt3Ext)
                //{
                //    glFormat = TextureGLFormat.CompressedRgbaS3tcDxt5Ext;
                //    DxtUtil2.ConvertDxt3ToDtx5(bytes, info.Width, info.Height, info.MipMaps);
                //}
                var internalFormat = (InternalFormat)glFormat;
                if (internalFormat == 0) { Console.Error.WriteLine("Unsupported texture, using default"); return DefaultTexture; }
                for (var i = levelStart; i <= levelEnd; i++) { if (!CompressedTexImage2D(info, i, internalFormat)) return DefaultTexture; }
            }
            else if (fmt is ValueTuple<TextureGLFormat, TextureGLPixelFormat, TextureGLPixelType> glPixelFormat)
            {
                var internalFormat = (PixelInternalFormat)glPixelFormat.Item1;
                if (internalFormat == 0) { Console.Error.WriteLine("Unsupported texture, using default"); return DefaultTexture; }
                var format = (PixelFormat)glPixelFormat.Item2;
                var type = (PixelType)glPixelFormat.Item3;
                for (var i = levelStart; i < numMipMaps; i++) { if (!TexImage2D(info, i, internalFormat, format, type)) return DefaultTexture; }
            }
            else throw new NotImplementedException();

            //if (info is IDisposable disposable) disposable.Dispose();
            info.End();

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
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)(info.Flags.HasFlag(TextureFlags.SUGGEST_CLAMPS) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat));
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)(info.Flags.HasFlag(TextureFlags.SUGGEST_CLAMPT) ? TextureWrapMode.Clamp : TextureWrapMode.Repeat));
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return id;
        }

        public override int BuildSolidTexture(int width, int height, float[] pixels)
        {
            var id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, width, height, 0, PixelFormat.Rgba, PixelType.Float, pixels);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 0);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return id;
        }

        public override int BuildNormalMap(int source, float strength) => throw new NotImplementedException();

        public override void DeleteTexture(int id) => GL.DeleteTexture(id);
    }

    /// <summary>
    /// OpenGLMaterialBuilder
    /// </summary>
    public class OpenGLMaterialBuilder : MaterialBuilderBase<GLRenderMaterial, int>
    {
        public OpenGLMaterialBuilder(TextureManager<int> textureManager) : base(textureManager) { }

        GLRenderMaterial _defaultMaterial;
        public override GLRenderMaterial DefaultMaterial => _defaultMaterial ??= BuildAutoMaterial(-1);

        GLRenderMaterial BuildAutoMaterial(int type)
        {
            var m = new GLRenderMaterial(null);
            m.Textures["g_tColor"] = TextureManager.DefaultTexture;
            m.Material.ShaderName = "vrf.error";
            return m;
        }

        public override GLRenderMaterial BuildMaterial(object key)
        {
            switch (key)
            {
                case IMaterial s:
                    var m = new GLRenderMaterial(s);
                    switch (s)
                    {
                        case IFixedMaterial _: return m;
                        case IParamMaterial p:
                            foreach (var tex in p.TextureParams) m.Textures[tex.Key] = TextureManager.LoadTexture($"{tex.Value}_c", out _);
                            if (p.IntParams.ContainsKey("F_SOLID_COLOR") && p.IntParams["F_SOLID_COLOR"] == 1)
                            {
                                var a = p.VectorParams["g_vColorTint"];
                                m.Textures["g_tColor"] = TextureManager.BuildSolidTexture(1, 1, a.X, a.Y, a.Z, a.W);
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
    /// OpenGLGraphic
    /// </summary>
    public class OpenGLGraphic : IOpenGLGraphic
    {
        readonly PakFile _source;
        readonly TextureManager<int> _textureManager;
        readonly MaterialManager<GLRenderMaterial, int> _materialManager;
        readonly ObjectManager<object, GLRenderMaterial, int> _objectManager;
        readonly ShaderManager<Shader> _shaderManager;

        public OpenGLGraphic(PakFile source)
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
        public int LoadTexture(string path, out object tag, Range? rng = null) => _textureManager.LoadTexture(path, out tag, rng);
        public void PreloadTexture(string path) => _textureManager.PreloadTexture(path);
        public object CreateObject(string path, out object tag) => _objectManager.CreateObject(path, out tag);
        public void PreloadObject(string path) => _objectManager.PreloadObject(path);
        public Shader LoadShader(string path, IDictionary<string, bool> args = null) => _shaderManager.LoadShader(path, args);
        public Task<T> LoadFileObject<T>(string path) => _source.LoadFileObject<T>(path);

        // cache
        QuadIndexBuffer _quadIndices;
        public QuadIndexBuffer QuadIndices => _quadIndices != null ? _quadIndices : _quadIndices = new QuadIndexBuffer(65532);
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
                Platform.GraphicFactory = source => new OpenGLGraphic(source);
                Debug.AssertFunc = x => System.Diagnostics.Debug.Assert(x);
                Debug.LogFunc = a => System.Diagnostics.Debug.Print(a);
                Debug.LogFormatFunc = (a, b) => System.Diagnostics.Debug.Print(a, b);
                return true;
            }
            catch { return false; }
        }
    }
}
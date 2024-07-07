using OpenStack;
using OpenStack.Graphics;
using OpenStack.Graphics.OpenGL;
using OpenStack.Graphics.Vulken;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace GameX.Platforms
{
    /// <summary>
    /// VulkenExtensions
    /// </summary>
    public static class VulkenExtensions { }

    /// <summary>
    /// OpenGLObjectBuilder
    /// </summary>
    public class VulkenObjectBuilder : ObjectBuilderBase<object, GLRenderMaterial, int>
    {
        public override void EnsurePrefab() { }
        public override object CreateNewObject(object prefab) => throw new NotImplementedException();
        public override object CreateObject(object source, IMaterialManager<GLRenderMaterial, int> materialManager) => throw new NotImplementedException();
    }

    /// <summary>
    /// VulkenShaderBuilder
    /// </summary>
    public class VulkenShaderBuilder : ShaderBuilderBase<Shader>
    {
        static readonly ShaderLoader _loader = new ShaderDebugLoader();
        public override Shader CreateShader(object path, IDictionary<string, bool> args) => _loader.CreateShader(path, args);
        public override Shader CreatePlaneShader(object path, IDictionary<string, bool> args) => _loader.CreatePlaneShader(path, args);
    }

    /// <summary>
    /// VulkenTextureBuilder
    /// </summary>
    public unsafe class VulkenTextureBuilder : TextureBuilderBase<int>
    {
        int _defaultTexture = -1;
        public override int DefaultTexture => _defaultTexture > -1 ? _defaultTexture : _defaultTexture = CreateDefaultTexture();

        public void Release()
        {
            if (_defaultTexture > 0) { GL.DeleteTexture(_defaultTexture); _defaultTexture = -1; }
        }

        int CreateDefaultTexture() => CreateSolidTexture(4, 4, new[]
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

        public override int CreateTexture(int reuse, ITexture source, Range? level = null)
        {
            //return DefaultTexture;
            var id = GL.GenTexture();
            var numMipMaps = Math.Max(1, source.MipMaps);
            var levelStart = level?.Start.Value ?? 0;
            var levelEnd = numMipMaps - 1;

            GL.BindTexture(TextureTarget.Texture2D, id);
            if (levelStart > 0) GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, levelStart);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, levelEnd - levelStart);
            var (bytes, fmt, spans) = source.Begin((int)Platform.Type.OpenGL);
            if (bytes == null) return DefaultTexture;

            bool CompressedTexImage2D(ITexture source, int i, InternalFormat internalFormat)
            {
                var span = spans != null ? spans[i] : Range.All;
                if (span.Start.Value < 0) return false;
                var width = source.Width >> i;
                var height = source.Height >> i;
                var pixels = bytes.AsSpan(span);
                fixed (byte* data = pixels) GL.CompressedTexImage2D(TextureTarget.Texture2D, i, internalFormat, width, height, 0, pixels.Length, (IntPtr)data);
                return true;
            }

            bool TexImage2D(ITexture source, int i, PixelInternalFormat internalFormat, PixelFormat format, PixelType type)
            {
                var span = spans != null ? spans[i] : Range.All;
                if (span.Start.Value < 0) return false;
                var width = source.Width >> i;
                var height = source.Height >> i;
                var pixels = bytes.AsSpan(span);
                fixed (byte* data = pixels) GL.TexImage2D(TextureTarget.Texture2D, i, internalFormat, width, height, 0, format, type, (IntPtr)data);
                return true;
            }

            if (fmt is TextureGLFormat glFormat)
            {
                //if (glFormat == TextureGLFormat.CompressedRgbaS3tcDxt3Ext)
                //{
                //    glFormat = TextureGLFormat.CompressedRgbaS3tcDxt5Ext;
                //    DxtUtil2.ConvertDxt3ToDtx5(bytes, source.Width, source.Height, source.MipMaps);
                //}
                var internalFormat = (InternalFormat)glFormat;
                if (internalFormat == 0) { Console.Error.WriteLine("Unsupported texture, using default"); return DefaultTexture; }
                for (var i = levelStart; i <= levelEnd; i++) { if (!CompressedTexImage2D(source, i, internalFormat)) return DefaultTexture; }
            }
            else if (fmt is ValueTuple<TextureGLFormat, TextureGLPixelFormat, TextureGLPixelType> glPixelFormat)
            {
                var internalFormat = (PixelInternalFormat)glPixelFormat.Item1;
                if (internalFormat == 0) { Console.Error.WriteLine("Unsupported texture, using default"); return DefaultTexture; }
                var format = (PixelFormat)glPixelFormat.Item2;
                var type = (PixelType)glPixelFormat.Item3;
                for (var i = levelStart; i < numMipMaps; i++) { if (!TexImage2D(source, i, internalFormat, format, type)) return DefaultTexture; }
            }
            else throw new NotImplementedException();

            source.End();

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
            GL.BindTexture(TextureTarget.Texture2D, 0);
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
            GL.BindTexture(TextureTarget.Texture2D, 0);
            return id;
        }

        public override int CreateNormalMap(int texture, float strength) => throw new NotImplementedException();

        public override void DeleteTexture(int texture) => GL.DeleteTexture(texture);
    }

    /// <summary>
    /// VulkenMaterialBuilder
    /// </summary>
    public class VulkenMaterialBuilder : MaterialBuilderBase<GLRenderMaterial, int>
    {
        public VulkenMaterialBuilder(TextureManager<int> textureManager) : base(textureManager) { }

        GLRenderMaterial _defaultMaterial;
        public override GLRenderMaterial DefaultMaterial => _defaultMaterial ??= BuildAutoMaterial(-1);

        GLRenderMaterial BuildAutoMaterial(int type)
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
    /// OpenGLGraphic
    /// </summary>
    public class VulkenGraphic : IVulkenGraphic
    {
        readonly PakFile _source;
        readonly AudioManager<object> _audioManager;
        readonly TextureManager<int> _textureManager;
        readonly MaterialManager<GLRenderMaterial, int> _materialManager;
        readonly ObjectManager<object, GLRenderMaterial, int> _objectManager;
        readonly ShaderManager<Shader> _shaderManager;

        public VulkenGraphic(PakFile source)
        {
            _source = source;
            _audioManager = new AudioManager<object>(source, new SystemAudioBuilder());
            _textureManager = new TextureManager<int>(source, new VulkenTextureBuilder());
            _materialManager = new MaterialManager<GLRenderMaterial, int>(source, _textureManager, new VulkenMaterialBuilder(_textureManager));
            _objectManager = new ObjectManager<object, GLRenderMaterial, int>(source, _materialManager, new VulkenObjectBuilder());
            _shaderManager = new ShaderManager<Shader>(source, new VulkenShaderBuilder());
            MeshBufferCache = new GLMeshBufferCache();
        }

        public PakFile Source => _source;
        public IAudioManager<object> AudioManager => _audioManager;
        public ITextureManager<int> TextureManager => _textureManager;
        public IMaterialManager<GLRenderMaterial, int> MaterialManager => _materialManager;
        public IObjectManager<object, GLRenderMaterial, int> ObjectManager => _objectManager;
        public IShaderManager<Shader> ShaderManager => _shaderManager;
        public object CreateAudio(object path) => _audioManager.CreateAudio(path).aud;
        public int CreateTexture(object path, Range? level = null) => _textureManager.CreateTexture(path, level).tex;
        public void PreloadTexture(object path) => _textureManager.PreloadTexture(path);
        public object CreateObject(object path) => _objectManager.CreateObject(path).obj;
        public void PreloadObject(object path) => _objectManager.PreloadObject(path);
        public Shader CreateShader(object path, IDictionary<string, bool> args = null) => _shaderManager.CreateShader(path, args).sha;
        public Task<T> LoadFileObject<T>(object path) => _source.LoadFileObject<T>(path);

        // cache
        QuadIndexBuffer _quadIndices;
        public QuadIndexBuffer QuadIndices => _quadIndices != null ? _quadIndices : _quadIndices = new QuadIndexBuffer(65532);
        public GLMeshBufferCache MeshBufferCache { get; }
    }

    /// <summary>
    /// VulkenPlatform
    /// </summary>
    public static class VulkenPlatform
    {
        public static unsafe bool Startup()
        {
            try
            {
                Platform.PlatformType = Platform.Type.Vulken;
                Platform.GraphicFactory = source => new VulkenGraphic(source);
                Debug.AssertFunc = x => System.Diagnostics.Debug.Assert(x);
                Debug.LogFunc = a => System.Diagnostics.Debug.Print(a);
                Debug.LogFormatFunc = (a, b) => System.Diagnostics.Debug.Print(a, b);
                return true;
            }
            catch { return false; }
        }
    }
}
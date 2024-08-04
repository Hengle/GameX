using OpenStack.Gfx;
using OpenStack.Sfx;
using StereoKit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shader = StereoKit.Shader;
using OpenStack;

namespace GameX.Platforms
{
    /// <summary>
    /// OpenGLExtensions
    /// </summary>
    public static class StereoKitExtensions { }

    /// <summary>
    /// StereoKitObjectBuilder
    /// </summary>
    public class StereoKitObjectBuilder : ObjectBuilderBase<object, Material, Tex>
    {
        public override void EnsurePrefab() { }
        public override object CreateNewObject(object prefab) => throw new NotImplementedException();
        public override object CreateObject(object source, IMaterialManager<Material, Tex> materialManager) => throw new NotImplementedException();
    }

    /// <summary>
    /// StereoKitShaderBuilder
    /// </summary>
    public class StereoKitShaderBuilder : ShaderBuilderBase<Shader>
    {
        public override Shader CreateShader(object path, IDictionary<string, bool> args) => Shader.FromFile((string)path);
        public override Shader CreatePlaneShader(object path, IDictionary<string, bool> args) => Shader.FromFile((string)path);
    }

    /// <summary>
    /// StereoKitTextureBuilder
    /// </summary>
    public class StereoKitTextureBuilder : TextureBuilderBase<Tex>
    {
        public void Release()
        {
            if (_defaultTexture != null) { /*Object.Destroy(_defaultTexture);*/ _defaultTexture = null; }
        }

        Tex _defaultTexture;
        public override Tex DefaultTexture => _defaultTexture ??= CreateDefaultTexture();

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

    /// <summary>
    /// StereoKitMaterialBuilder
    /// </summary>
    public class StereoKitMaterialBuilder : MaterialBuilderBase<Material, Tex>
    {
        public StereoKitMaterialBuilder(TextureManager<Tex> textureManager) : base(textureManager) { }

        Material _defaultMaterial;
        public override Material DefaultMaterial => _defaultMaterial ??= BuildAutoMaterial(-1);

        Material BuildAutoMaterial(int type)
        {
            var m = new Material((string)null);
            return m;
        }

        public override Material CreateMaterial(object key)
        {
            switch (key)
            {
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }

    /// <summary>
    /// IStereoKitGfx
    /// </summary>
    public interface IStereoKitGfx : IOpenGfxAny<object, Material, Tex, Shader> { }

    /// <summary>
    /// IStereoKitSfx
    /// </summary>
    public interface IStereoKitSfx : IOpenSfxAny<object> { }

    /// <summary>
    /// StereoKitGfx
    /// </summary>
    public class StereoKitGfx : IStereoKitGfx
    {
        readonly PakFile _source;
        readonly IAudioManager<object> _audioManager;
        readonly TextureManager<Tex> _textureManager;
        readonly MaterialManager<Material, Tex> _materialManager;
        readonly ObjectManager<object, Material, Tex> _objectManager;
        readonly ShaderManager<Shader> _shaderManager;

        public StereoKitGfx(PakFile source)
        {
            _source = source;
            _audioManager = new AudioManager<object>(source, new SystemAudioBuilder());
            _textureManager = new TextureManager<Tex>(source, new StereoKitTextureBuilder());
            _materialManager = new MaterialManager<Material, Tex>(source, _textureManager, new StereoKitMaterialBuilder(_textureManager));
            _objectManager = new ObjectManager<object, Material, Tex>(source, _materialManager, new StereoKitObjectBuilder());
            _shaderManager = new ShaderManager<Shader>(source, new StereoKitShaderBuilder());
        }

        public PakFile Source => _source;
        public IAudioManager<object> AudioManager => _audioManager;
        public ITextureManager<Tex> TextureManager => _textureManager;
        public IMaterialManager<Material, Tex> MaterialManager => _materialManager;
        public IObjectManager<object, Material, Tex> ObjectManager => _objectManager;
        public IShaderManager<Shader> ShaderManager => _shaderManager;
        public object CreateAudio(object path) => _audioManager.CreateAudio(path).aud;
        public Tex CreateTexture(object path, Range? level = null) => _textureManager.CreateTexture(path, level).tex;
        public void PreloadTexture(object path) => _textureManager.PreloadTexture(path);
        public object CreateObject(object path) => _objectManager.CreateObject(path).obj;
        public void PreloadObject(object path) => _objectManager.PreloadObject(path);
        public Shader CreateShader(object path, IDictionary<string, bool> args = null) => _shaderManager.CreateShader(path, args).sha;

        public Task<T> LoadFileObject<T>(object path) => _source.LoadFileObject<T>(path);
    }

    /// <summary>
    /// StereoKitGfx
    /// </summary>
    public class StereoKitSfx : ISystemSfx
    {
        readonly PakFile _source;
        readonly IAudioManager<object> _audioManager;

        public StereoKitSfx(PakFile source)
        {
            _source = source;
            _audioManager = new AudioManager<object>(source, new SystemAudioBuilder());
        }

        public PakFile Source => _source;
        public IAudioManager<object> AudioManager => _audioManager;
        public object CreateAudio(object path) => _audioManager.CreateAudio(path).aud;
    }

    /// <summary>
    /// StereoKitPlatform
    /// </summary>
    public static class StereoKitPlatform
    {
        public static unsafe bool Startup()
        {
            try
            {
                Platform.PlatformType = Platform.Type.StereoKit;
                Platform.GfxFactory = source => new StereoKitGfx(source);
                Platform.SfxFactory = source => new StereoKitSfx(source);
                Debug.AssertFunc = x => System.Diagnostics.Debug.Assert(x);
                Debug.LogFunc = a => System.Diagnostics.Debug.Print(a);
                Debug.LogFormatFunc = (a, b) => System.Diagnostics.Debug.Print(a, b);
                return true;
            }
            catch { return false; }
        }
    }
}
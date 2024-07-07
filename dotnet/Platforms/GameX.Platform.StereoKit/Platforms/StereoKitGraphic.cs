using OpenStack.Graphics;
using StereoKit;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shader = StereoKit.Shader;

namespace GameX.Platforms
{
    public interface IStereoKitGraphic : IOpenGraphicAny<object, object, Material, Tex, Shader> { }

    public class StereoKitGraphic : IStereoKitGraphic
    {
        readonly PakFile _source;
        readonly IAudioManager<object> _audioManager;
        readonly TextureManager<Tex> _textureManager;
        readonly MaterialManager<Material, Tex> _materialManager;
        readonly ObjectManager<object, Material, Tex> _objectManager;
        readonly ShaderManager<Shader> _shaderManager;

        public StereoKitGraphic(PakFile source)
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
}
using OpenStack.Gfx;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Shader = UnityEngine.Shader;

namespace GameX.Platforms
{
    public interface IUnityGraphic : IOpenGraphicAny<object, GameObject, Material, Texture2D, Shader> { }

    public class UnityGraphic : IUnityGraphic
    {
        readonly PakFile _source;
        readonly IAudioManager<object> _audioManager;
        readonly ITextureManager<Texture2D> _textureManager;
        readonly IMaterialManager<Material, Texture2D> _materialManager;
        readonly IObjectManager<GameObject, Material, Texture2D> _objectManager;
        readonly IShaderManager<Shader> _shaderManager;

        public UnityGraphic(PakFile source)
        {
            _source = source;
            _audioManager = new AudioManager<object>(source, new SystemAudioBuilder());
            _textureManager = new TextureManager<Texture2D>(source, new UnityTextureBuilder());
            //switch (MaterialType.Default)
            //{
            //    case MaterialType.None: _material = null; break;
            //    case MaterialType.Default: _material = new DefaultMaterial(_textureManager); break;
            //    case MaterialType.Standard: _material = new StandardMaterial(_textureManager); break;
            //    case MaterialType.Unlit: _material = new UnliteMaterial(_textureManager); break;
            //    default: _material = new BumpedDiffuseMaterial(_textureManager); break;
            //}
            _materialManager = new MaterialManager<Material, Texture2D>(source, _textureManager, new BumpedDiffuseMaterialBuilder(_textureManager));
            //_objectManager = new ObjectManager<GameObject, Material, Texture2D>(source, _materialManager, new UnityObjectBuilder(0));
            _shaderManager = new ShaderManager<Shader>(source, new UnityShaderBuilder());
        }

        public PakFile Source => _source;
        public IAudioManager<object> AudioManager => _audioManager;
        public ITextureManager<Texture2D> TextureManager => _textureManager;
        public IMaterialManager<Material, Texture2D> MaterialManager => _materialManager;
        public IObjectManager<GameObject, Material, Texture2D> ObjectManager => _objectManager;
        public IShaderManager<Shader> ShaderManager => _shaderManager;
        public object CreateAudio(object path) => _audioManager.CreateAudio(path).aud;
        public Texture2D CreateTexture(object path, Range? level = null) => _textureManager.CreateTexture(path, level).tex;
        public void PreloadTexture(object path) => _textureManager.PreloadTexture(path);
        public GameObject CreateObject(object path) => _objectManager.CreateObject(path).obj;
        public void PreloadObject(object path) => _objectManager.PreloadObject(path);
        public Shader CreateShader(object path, IDictionary<string, bool> args = null) => _shaderManager.CreateShader(path, args).sha;

        public Task<T> LoadFileObject<T>(object path) => _source.LoadFileObject<T>(path);
    }
}
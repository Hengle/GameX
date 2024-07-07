using OpenStack.Graphics;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnrealEngine.Framework;

namespace GameX.Platforms
{
    public interface IUnrealGraphic : IOpenGraphicAny<object, object, object, Texture2D, object> { }

    public class UnrealGraphic : IUnrealGraphic
    {
        readonly PakFile _source;
        readonly IAudioManager<object> _audioManager;
        readonly ITextureManager<Texture2D> _textureManager;

        public UnrealGraphic(PakFile source)
        {
            _source = source;
            _audioManager = new AudioManager<object>(source, new SystemAudioBuilder());
            _textureManager = new TextureManager<Texture2D>(source, new UnrealTextureBuilder());
        }

        public PakFile Source => _source;
        public IAudioManager<object> AudioManager => _audioManager;
        public ITextureManager<Texture2D> TextureManager => _textureManager;
        public IMaterialManager<object, Texture2D> MaterialManager => throw new NotImplementedException();
        public IObjectManager<object, object, Texture2D> ObjectManager => throw new NotImplementedException();
        public IShaderManager<object> ShaderManager => throw new NotImplementedException();
        public object CreateAudio(object path) => _audioManager.CreateAudio(path).aud;
        public Texture2D CreateTexture(object path, Range? level = null) => _textureManager.CreateTexture(path, level).tex;
        public void PreloadTexture(object path) => throw new NotImplementedException();
        public object CreateObject(object path) => throw new NotImplementedException();
        public void PreloadObject(object path) => throw new NotImplementedException();
        public object CreateShader(object path, IDictionary<string, bool> args = null) => throw new NotImplementedException();

        public Task<T> LoadFileObject<T>(object path) => _source.LoadFileObject<T>(path);
    }
}
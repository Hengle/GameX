using OpenStack.Sfx;
using System;

namespace GameX.Platforms
{
    /// <summary>
    /// SystemAudioBuilder
    /// </summary>
    public class SystemAudioBuilder : AudioBuilderBase<object>
    {
        public override object CreateAudio(object path) => throw new NotImplementedException();
        public override void DeleteAudio(object audio) => throw new NotImplementedException();
    }

    /// <summary>
    /// SystemSfx
    /// </summary>
    public class SystemSfx(PakFile source) : ISystemSfx
    {
        readonly PakFile _source = source;
        readonly AudioManager<object> _audioManager = new AudioManager<object>(source, new SystemAudioBuilder());

        public PakFile Source => _source;
        public IAudioManager<object> AudioManager => _audioManager;
        public object CreateAudio(object path) => _audioManager.CreateAudio(path).aud;
    }
}
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
}
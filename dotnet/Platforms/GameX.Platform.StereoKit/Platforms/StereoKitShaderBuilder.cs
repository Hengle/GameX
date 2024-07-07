using StereoKit;
using System.Collections.Generic;

namespace GameX.Platforms
{
    public class StereoKitShaderBuilder : ShaderBuilderBase<Shader>
    {
        public override Shader CreateShader(object path, IDictionary<string, bool> args) => Shader.FromFile((string)path);
        public override Shader CreatePlaneShader(object path, IDictionary<string, bool> args) => Shader.FromFile((string)path);
    }
}
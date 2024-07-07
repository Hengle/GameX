using System.Collections.Generic;
using UnityEngine;

namespace GameX.Platforms
{
    public class UnityShaderBuilder : ShaderBuilderBase<Shader>
    {
        public override Shader CreateShader(object path, IDictionary<string, bool> args) => Shader.Find((string)path);
        public override Shader CreatePlaneShader(object path, IDictionary<string, bool> args) => Shader.Find((string)path);
    }
}
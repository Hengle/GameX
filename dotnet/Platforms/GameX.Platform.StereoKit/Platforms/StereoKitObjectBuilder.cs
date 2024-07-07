using OpenStack.Graphics;
using StereoKit;
using System;

namespace GameX.Platforms
{
    public class StereoKitObjectBuilder : ObjectBuilderBase<object, Material, Tex>
    {
        public override void EnsurePrefab() { }
        public override object CreateNewObject(object prefab) => throw new NotImplementedException();
        public override object CreateObject(object source, IMaterialManager<Material, Tex> materialManager) => throw new NotImplementedException();
    }
}
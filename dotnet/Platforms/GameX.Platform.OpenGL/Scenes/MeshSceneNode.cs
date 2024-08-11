using OpenStack.Gfx;
using OpenStack.Gfx.Gl;
using System.Collections.Generic;
using System.Numerics;

namespace GameX.Scenes
{
    public class MeshSceneNode : SceneNode, IMeshCollection
    {
        GLRenderableMesh Mesh;

        public MeshSceneNode(Scene scene, IMesh mesh, int meshIndex, IDictionary<string, string> skinMaterials = null) : base(scene)
        {
            Mesh = new GLRenderableMesh(Scene.Gfx as IOpenGLGfx, mesh, meshIndex, skinMaterials);
            LocalBoundingBox = Mesh.BoundingBox;
        }

        public Vector4 Tint
        {
            get => Mesh.Tint;
            set => Mesh.Tint = value;
        }

        public IEnumerable<RenderableMesh> RenderableMeshes
        {
            get { yield return Mesh; }
        }

        public override IEnumerable<string> GetSupportedRenderModes() => Mesh.GetSupportedRenderModes();
        public override void SetRenderMode(string renderMode) => Mesh.SetRenderMode(renderMode);
        public override void Update(Scene.UpdateContext context) => Mesh.Update(context.Timestep);
        public override void Render(Scene.RenderContext context) { } // This node does not render itself; it uses the batching system via IRenderableMeshCollection
    }
}

using OpenStack.Gfx;
using OpenStack.Gfx.Gl.Renders;
using OpenStack.Gfx.Renders;

namespace GameX.App.Explorer.Controls
{
    public class ViewMaterial : ViewBase<IMaterial>
    {
        protected override (IMaterial, IList<IRenderer>) GetObj(object source)
        {
            var obj = (IMaterial)source;
            GL.TextureManager.DeleteTexture(obj);
            var (material, _) = GL.MaterialManager.CreateMaterial(obj);
            return (obj, [new MaterialRenderer(GL, material)]);
        }
    }
}

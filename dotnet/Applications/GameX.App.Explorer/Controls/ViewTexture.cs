using OpenStack.Gfx;
using OpenStack.Gfx.Gl;

namespace GameX.App.Explorer.Controls
{
    public class ViewTexture : ViewBase<ITexture>
    {
        protected override void SetViewportSize(int x, int y, int width, int height)
        {
            if (Obj == null) return;
            if (Obj.Width > 1024 || Obj.Height > 1024 || false) base.SetViewportSize(x, y, width, height);
            else base.SetViewportSize(x, y, Obj.Width << FACTOR, Obj.Height << FACTOR);
        }

        protected override (ITexture, IList<IRenderer>) GetObj(object source)
        {
            var obj = (ITexture)source;
            GL.TextureManager.DeleteTexture(obj);
            var (texture, _) = GL.TextureManager.CreateTexture(obj, Level);
            return (obj, [new TextureRenderer(GL, texture, ToggleValue)]);
        }
    }
}

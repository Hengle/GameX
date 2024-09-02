using OpenStack.Gfx;
using OpenStack.Gfx.Gl;

namespace GameX.App.Explorer.Controls
{
    public class ViewTestTri : ViewBase<object>
    {
        //protected override void SetViewportSize(int x, int y, int width, int height)
        //{
        //    if (Obj == null) return;
        //    if (Obj.Width > 1024 || Obj.Height > 1024 || false) base.SetViewportSize(x, y, width, height);
        //    else base.SetViewportSize(x, y, Obj.Width << FACTOR, Obj.Height << FACTOR);
        //}

        protected override (object, IList<IRenderer>) GetObj(object source)
        {
            var obj = source;
            return (obj, [new TestTriRenderer(GL)]);
        }
    }
}

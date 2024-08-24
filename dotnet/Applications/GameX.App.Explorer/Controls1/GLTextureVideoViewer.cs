using OpenStack.Gfx;
using OpenStack.Gfx.Gl;

namespace GameX.App.Explorer.Controls1
{
    public class GLTextureVideoViewer : GLBaseViewer<ITextureVideo>
    {
        int FrameDelay;

        protected override void SetViewportSize(int x, int y, int width, int height)
        {
            if (Obj == null) return;
            if (Obj.Width > 1024 || Obj.Height > 1024 || false) base.SetViewportSize(x, y, width, height);
            else base.SetViewportSize(x, y, Obj.Width << FACTOR, Obj.Height << FACTOR);
        }

        protected override (ITextureVideo, IList<IRenderer>) GetObj(object source)
        {
            //Camera.SetLocation(new Vector3(200));
            //Camera.LookAt(new Vector3(0));
            var obj = (ITextureVideo)source;
            GL.TextureManager.DeleteTexture(obj);
            var (texture, _) = GL.TextureManager.CreateTexture(obj, Level);
            return (obj, [new TextureRenderer(GL, texture, ToggleValue)]);
        }

        public override void Tick(int? deltaTime = null)
        {
            base.Tick(deltaTime);
            if (GL == null || Obj == null || !Obj.HasFrames) return;
            FrameDelay += DeltaTime;
            if (FrameDelay <= Obj.Fps || !Obj.DecodeFrame()) return;
            FrameDelay = 0; // reset delay between frames
            GL.TextureManager.ReloadTexture(Obj, Level);
            Render(Camera, 0f);
        }
    }
}

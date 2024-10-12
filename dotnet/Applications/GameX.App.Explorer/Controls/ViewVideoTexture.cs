using OpenStack.Gfx.Gl.Renders;
using OpenStack.Gfx.Renders;
using OpenStack.Gfx.Textures;

namespace GameX.App.Explorer.Controls
{
    public class ViewVideoTexture : ViewBase<ITextureFrames>
    {
        int FrameDelay;

        public ViewVideoTexture() : base(new TimeSpan(1)) { }

        protected override void SetViewport(int x, int y, int width, int height)
        {
            if (Obj == null) return;
            if (Obj.Width > 1024 || Obj.Height > 1024 || false) base.SetViewport(x, y, width, height);
            else base.SetViewport(x, y, Obj.Width << FACTOR, Obj.Height << FACTOR);
        }

        protected override (ITextureFrames, IList<IRenderer>) GetObj(object source)
        {
            //Camera.SetLocation(new Vector3(200));
            //Camera.LookAt(new Vector3(0));
            var obj = (ITextureFrames)source;
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

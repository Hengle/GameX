using OpenStack.Gfx;
using OpenStack.Gfx.Gl;
using OpenTK.Input;
using System.Windows;
using Key = OpenTK.Input.Key;

namespace GameX.App.Explorer.Controls
{
    public abstract class ViewBase<TObj> : GLViewerControl
    {
        protected const int FACTOR = 0;
        protected bool ToggleValue;
        protected IOpenGLGfx GL;
        protected TObj Obj;
        protected Range Level = 0..;
        protected IList<IRenderer> Renderers;
        // ui
        static readonly Key[] Keys = [Key.Q, Key.W, Key.A, Key.Z, Key.Escape, Key.Space, Key.Tilde];
        readonly HashSet<Key> KeyDowns = [];
        int Id = 0;

        public static readonly DependencyProperty GfxProperty = DependencyProperty.Register(nameof(Gfx), typeof(object), typeof(ViewBase<TObj>),
            new PropertyMetadata((d, e) => (d as ViewBase<TObj>).OnSourceChanged()));

        public IOpenGfx Gfx
        {
            get => GetValue(GfxProperty) as IOpenGfx;
            set => SetValue(GfxProperty, value);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object), typeof(ViewBase<TObj>),
            new PropertyMetadata((d, e) => (d as ViewBase<TObj>).OnSourceChanged()));

        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        //protected override void SetViewport(int x, int y, int width, int height)
        //{
        //    if (Obj == null) return;
        //    if (Obj.Width > 1024 || Obj.Height > 1024 || false) base.SetViewport(x, y, width, height);
        //    else base.SetViewport(x, y, Obj.Width << FACTOR, Obj.Height << FACTOR);
        //}

        protected abstract (TObj, IList<IRenderer>) GetObj(object source);

        void OnSourceChanged()
        {
            IOpenGfx gfx; object source; if ((gfx = Gfx) == null || (source = Source) == null) return;
            GL = gfx as IOpenGLGfx;
            (Obj, Renderers) = GetObj(source);
            if (Obj == null) return;
            if (Obj is ITextureSelect z2) z2.Select(Id);
            //Camera.SetLocation(new Vector3(200));
            //Camera.LookAt(new Vector3(0));
        }

        protected override void Render(Camera camera, float frameTime)
        {
            if (Renderers == null) return;
            foreach (var renderer in Renderers) renderer.Render(camera, RenderPass.Both);
        }

        protected override void HandleInput(MouseState mouseState, KeyboardState keyboardState)
        {
            foreach (var key in Keys)
                if (!KeyDowns.Contains(key) && keyboardState.IsKeyDown(key)) KeyDowns.Add(key);
            foreach (var key in KeyDowns)
                if (keyboardState.IsKeyUp(key))
                {
                    KeyDowns.Remove(key);
                    switch (key)
                    {
                        case Key.W: Select(++Id); break;
                        case Key.Q: Select(--Id); break;
                        case Key.A: MovePrev(); break;
                        case Key.Z: MoveNext(); ; break;
                        case Key.Escape: Reset();  break;
                        case Key.Space: MoveReset(); break;
                        case Key.Tilde: Toggle(); break;
                    }
                }
        }

        void Select(int id)
        {
            if (Obj is ITextureSelect z2) z2.Select(id);
            OnSourceChanged();
            Views.FileExplorer.Current.OnInfoUpdated();
        }
        void MoveReset() { Id = 0; Level = 0..; OnSourceChanged(); }
        void MoveNext() { if (Level.Start.Value < 10) Level = new(Level.Start.Value + 1, Level.End); OnSourceChanged(); }
        void MovePrev() { if (Level.Start.Value > 0) Level = new(Level.Start.Value - 1, Level.End); OnSourceChanged(); }
        void Reset() { Id = 0; Level = 0..; OnSourceChanged(); }
        void Toggle() { ToggleValue = !ToggleValue; OnSourceChanged(); }
    }
}

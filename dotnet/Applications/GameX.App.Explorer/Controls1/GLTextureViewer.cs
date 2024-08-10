using OpenStack.Gfx;
using OpenStack.Gfx.Gl;
using OpenTK.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using Key = OpenTK.Input.Key;

namespace GameX.App.Explorer.Controls1
{
    public class GLTextureViewer : GLViewerControl
    {
        const int FACTOR = 1;
        bool Background;
        IOpenGLGfx GraphicGL;
        ITexture Obj;
        Range Level = 0..;
        readonly HashSet<TextureRenderer> Renderers = [];
        // ui
        Key[] Keys = [Key.Q, Key.W, Key.A, Key.Z, Key.Space, Key.Tilde];
        HashSet<Key> KeyDowns = [];
        int Id = 0;

        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static readonly DependencyProperty GraphicProperty = DependencyProperty.Register(nameof(Graphic), typeof(object), typeof(GLTextureViewer),
            new PropertyMetadata((d, e) => (d as GLTextureViewer).OnProperty()));

        public IOpenGfx Graphic
        {
            get => GetValue(GraphicProperty) as IOpenGfx;
            set => SetValue(GraphicProperty, value);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object), typeof(GLTextureViewer),
            new PropertyMetadata((d, e) => (d as GLTextureViewer).OnProperty()));

        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        protected override void SetViewportSize(int x, int y, int width, int height)
        {
            if (Obj == null) return;
            if (Obj.Width > 1024 || Obj.Height > 1024 || false) base.SetViewportSize(x, y, width, height);
            else base.SetViewportSize(x, y, Obj.Width << FACTOR, Obj.Height << FACTOR);
        }

        void OnProperty()
        {
            if (Graphic == null || Source == null) return;
            GraphicGL = Graphic as IOpenGLGfx;
            Obj = Source is ITexture z ? z
                : Source is IRedirected<ITexture> y ? y.Value
                : null;
            if (Obj == null) return;
            if (Obj is ITextureSelect z2) z2.Select(Id);

            //Camera.SetLocation(new Vector3(200));
            //Camera.LookAt(new Vector3(0));

            GraphicGL.TextureManager.DeleteTexture(Obj);
            var (texture, _) = GraphicGL.TextureManager.CreateTexture(Obj, Level);
            Renderers.Clear();
            Renderers.Add(new TextureRenderer(GraphicGL, texture, Background));
        }

        protected override void Render(Camera camera, float frameTime)
        {
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
                        case Key.Space: MoveReset(); break;
                        case Key.Tilde: ToggleBackground(); break;
                    }
                }
        }

        void Select(int id)
        {
            if (Obj == null) return;
            if (Obj is ITextureSelect z2) z2.Select(Id);
            OnProperty();
            Views.FileExplorer.Current.OnInfoUpdated();
        }
        void MoveReset() { Id = 0; Level = 0..; OnProperty(); }
        void MoveNext() { if (Level.Start.Value < 10) Level = new(Level.Start.Value + 1, Level.End); OnProperty(); }
        void MovePrev() { if (Level.Start.Value > 0) Level = new(Level.Start.Value - 1, Level.End); OnProperty(); }
        void ToggleBackground() { Background = !Background; OnProperty(); }
    }
}

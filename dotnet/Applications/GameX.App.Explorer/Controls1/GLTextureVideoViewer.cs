using OpenStack.Gfx;
using OpenStack.Gfx.Gl;
using OpenTK.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;
using Key = OpenTK.Input.Key;

namespace GameX.App.Explorer.Controls1
{
    public class GLTextureVideoViewer : GLViewerControl
    {
        const int FACTOR = 2;
        bool Background;
        IOpenGLGfx GL;
        ITextureVideo Obj;
        Range Level = 0..;
        readonly HashSet<TextureRenderer> Renderers = [];
        int Texture;
        int FrameDelay;
        // ui
        Key[] Keys = [Key.Escape, Key.Space];
        HashSet<Key> KeyDowns = [];
        int Id = 0;

        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static readonly DependencyProperty GfxProperty = DependencyProperty.Register(nameof(Gfx), typeof(object), typeof(GLTextureVideoViewer),
            new PropertyMetadata((d, e) => (d as GLTextureVideoViewer).OnProperty()));

        public IOpenGfx Gfx
        {
            get => GetValue(GfxProperty) as IOpenGfx;
            set => SetValue(GfxProperty, value);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object), typeof(GLTextureVideoViewer),
            new PropertyMetadata((d, e) => (d as GLTextureVideoViewer).OnProperty()));

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
            if (Gfx == null || Source == null) return;
            GL = Gfx as IOpenGLGfx;
            Obj = Source is ITextureVideo z ? z
                : Source is IRedirected<ITextureVideo> y ? y.Value
                : null;
            if (Obj == null) return;
            if (Obj is ITextureSelect z2) z2.Select(Id);

            Camera.SetLocation(new Vector3(200));
            Camera.LookAt(new Vector3(0));

            GL.TextureManager.DeleteTexture(Obj);
            (Texture, _) = GL.TextureManager.CreateTexture(Obj, Level);
            Renderers.Clear();
            Renderers.Add(new TextureRenderer(GL, Texture, Background));
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

        protected override void Render(Camera camera, float frameTime)
        {
            foreach (var renderer in Renderers) renderer.Render(camera, RenderPass.Both);
        }

        protected override void HandleInput(MouseState mouseState, KeyboardState keyboardState)
        {
            base.HandleInput(mouseState, keyboardState);
            foreach (var key in Keys)
                if (!KeyDowns.Contains(key) && keyboardState.IsKeyDown(key)) KeyDowns.Add(key);
            foreach (var key in KeyDowns)
                if (keyboardState.IsKeyUp(key))
                {
                    KeyDowns.Remove(key);
                    switch (key)
                    {
                        case Key.Escape: Reset(); ; break;
                        case Key.Space: Toggle(); break;
                    }
                }
        }

        void Reset() { Id = 0; Level = 0..; OnProperty(); }
        void Toggle() { OnProperty(); }
    }
}

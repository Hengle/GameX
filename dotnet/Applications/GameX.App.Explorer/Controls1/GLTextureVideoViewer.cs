using OpenStack.Graphics;
using OpenStack.Graphics.Controls;
using OpenStack.Graphics.OpenGL;
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
        const int FACTOR = 1;
        bool Background;
        IOpenGLGraphic GraphicGL;
        ITextureVideo Obj;
        Range Level = 0..;
        readonly HashSet<TextureRenderer> Renderers = [];
        int Texture;
        int FrameDelay;

        public GLTextureVideoViewer()
        {
            GLPaint += OnPaint;
            Unloaded += (s, e) => { GLPaint -= OnPaint; };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static readonly DependencyProperty GraphicProperty = DependencyProperty.Register(nameof(Graphic), typeof(object), typeof(GLTextureVideoViewer),
            new PropertyMetadata((d, e) => (d as GLTextureVideoViewer).OnProperty()));

        public IOpenGraphic Graphic
        {
            get => GetValue(GraphicProperty) as IOpenGraphic;
            set => SetValue(GraphicProperty, value);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object), typeof(GLTextureVideoViewer),
            new PropertyMetadata((d, e) => (d as GLTextureVideoViewer).OnProperty()));

        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        protected override void HandleResize()
        {
            if (Obj == null) return;
            if (Obj.Width > 1024 || Obj.Height > 1024 || false) { base.HandleResize(); return; }
            Camera.SetViewportSize(Obj.Width << FACTOR, Obj.Height << FACTOR);
            RecalculatePositions();
        }

        void OnProperty()
        {
            if (Graphic == null || Source == null) return;
            GraphicGL = Graphic as IOpenGLGraphic;
            Obj = Source is ITextureVideo z ? z
                : Source is IRedirected<ITextureVideo> y ? y.Value
                : null;
            if (Obj == null) return;
            if (Obj is ITextureSelect z2) z2.Select(Id);

            HandleResize();
            Camera.SetLocation(new Vector3(200));
            Camera.LookAt(new Vector3(0));

            GraphicGL.TextureManager.DeleteTexture(Obj);
            Texture = GraphicGL.TextureManager.LoadTexture(Obj, out _, Level);
            Renderers.Clear();
            Renderers.Add(new TextureRenderer(GraphicGL, Texture, Background));
        }

        public override void OnTick(int elapsedMs)
        {
            if (GraphicGL == null || Obj == null || !Obj.HasFrames) return;
            FrameDelay += elapsedMs;
            if (FrameDelay <= Obj.Fps || !Obj.DecodeFrame()) return;
            FrameDelay = 0; // reset delay between frames
            //GraphicGL.TextureManager.DeleteTexture(Obj);
            GraphicGL.TextureManager.ReloadTexture(Obj, out _, Level);
            Draw();
        }

        void OnPaint(object sender, RenderEventArgs e)
        {
            HandleInput(Keyboard.GetState());
            foreach (var renderer in Renderers) renderer.Render(e.Camera, RenderPass.Both);
        }

        Key[] Keys = [Key.Escape, Key.Space];
        HashSet<Key> KeyDowns = [];
        int Id = 0;

        public void HandleInput(KeyboardState keyboardState)
        {
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

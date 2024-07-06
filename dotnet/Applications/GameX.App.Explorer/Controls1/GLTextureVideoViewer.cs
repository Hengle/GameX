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

        public ITextureVideo Obj;

        const int Factor = 1;

        protected override void HandleResize()
        {
            if (Obj == null) return;
            if (Obj.Width > 1024 || Obj.Height > 1024 || false) { base.HandleResize(); return; }
            Camera.SetViewportSize(Obj.Width << Factor, Obj.Height << Factor);
            RecalculatePositions();
        }

        void OnProperty()
        {
            if (Graphic == null || Source == null) return;
            var graphic = Graphic as IOpenGLGraphic;
            Obj = Source is ITextureVideo z ? z
                : Source is IRedirected<ITextureVideo> y ? y.Value
                : null;
            if (Obj == null) return;
            if (Obj is ITextureSelect z2) z2.Select(Id);

            HandleResize();
            Camera.SetLocation(new Vector3(200));
            Camera.LookAt(new Vector3(0));

            graphic.TextureManager.DeleteTexture(Obj);
            Texture = graphic.TextureManager.LoadTexture(Obj, out _, Rng);
            Renderers.Clear();
            Renderers.Add(new TextureRenderer(graphic, Texture) { Background = Background });
        }

        int Texture;
        bool Background;
        Range Rng = 0..;
        readonly HashSet<TextureRenderer> Renderers = [];
        int FrameDelay;

        public override void OnTick(int elapsedMs)
        {
            if (Graphic == null || Obj == null || !Obj.HasFrames) return;
            var graphic = Graphic as IOpenGLGraphic;
            FrameDelay += elapsedMs;
            if (FrameDelay <= Obj.Fps || !Obj.DecodeFrame()) return;
            FrameDelay = 0; // Reset delay between frames
            graphic.TextureManager.DeleteTexture(Obj);
            Texture = graphic.TextureManager.LoadTexture(Obj, out _, Rng);
            Draw();
            //addDirtyRect(0, 0, 1, 1); // Add a dirty rect just to start the render routine
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

        void Reset() { Id = 0; Rng = 0..; OnProperty(); }
        void Toggle() { OnProperty(); }
    }
}

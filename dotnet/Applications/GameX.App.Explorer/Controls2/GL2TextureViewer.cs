using OpenStack;
using OpenStack.Graphics;
using OpenStack.Graphics.Controls;
using OpenStack.Graphics.OpenGL;
using System;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;

namespace GameX.App.Explorer.Controls2
{
    public class GL2TextureViewer : GLViewerControl
    {
        public GL2TextureViewer()
        {
            GLPaint += OnPaint;
            Unloaded += (a, b) => { GLPaint -= OnPaint; };
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static readonly DependencyProperty GraphicProperty = DependencyProperty.Register(nameof(Graphic), typeof(object), typeof(GL2ParticleViewer),
            new PropertyMetadata((d, e) => (d as GL2TextureViewer).OnProperty()));
        public IOpenGraphic Graphic
        {
            get => GetValue(GraphicProperty) as IOpenGraphic;
            set => SetValue(GraphicProperty, value);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object), typeof(GL2ParticleViewer),
            new PropertyMetadata((d, e) => (d as GL2TextureViewer).OnProperty()));
        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        void OnProperty()
        {
            if (Graphic == null || Source == null) return;
            var graphic = Graphic as IOpenGLGraphic;
            var source = Source is ITexture z ? z
                : Source is IRedirected<ITexture> y ? y.Value
                : null;
            if (source == null) return;

            Camera.SetViewportSize((int)ActualWidth, (int)ActualHeight);
            Camera.SetLocation(new Vector3(200));
            Camera.LookAt(new Vector3(0));
        }

        void OnPaint(object sender, RenderEventArgs e)
        {
        }
    }
}

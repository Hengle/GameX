using OpenStack.Gfx;
using OpenStack.Gfx.Gl;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows;

namespace GameX.App.Explorer.Controls1
{
    public class GLParticleViewer : GLViewerControl
    {
        ParticleGridRenderer particleGrid;
       
        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static readonly DependencyProperty GraphicProperty = DependencyProperty.Register(nameof(Graphic), typeof(object), typeof(GLParticleViewer),
            new PropertyMetadata((d, e) => (d as GLParticleViewer).OnProperty()));

        public IOpenGraphic Graphic
        {
            get => GetValue(GraphicProperty) as IOpenGraphic;
            set => SetValue(GraphicProperty, value);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object), typeof(GLParticleViewer),
            new PropertyMetadata((d, e) => (d as GLParticleViewer).OnProperty()));

        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        HashSet<ParticleRenderer> Renderers { get; } = new HashSet<ParticleRenderer>();

        void OnProperty()
        {
            if (Graphic == null || Source == null) return;
            var graphic = Graphic as IOpenGLGraphic;
            var source = Source is IParticleSystem z ? z
                : Source is IRedirected<IParticleSystem> y ? y.Value
                : null;
            if (source == null) return;

            particleGrid = new ParticleGridRenderer(graphic, 20, 5);
            Camera.SetViewportSize(0, 0, (int)ActualWidth, (int)ActualHeight);
            Camera.SetLocation(new Vector3(200));
            Camera.LookAt(new Vector3(0));

            Renderers.Add(new ParticleRenderer(graphic, source));
        }

        protected override void Render(Camera camera, float deltaTime)
        {
            particleGrid?.Render(Camera, RenderPass.Both);
            foreach (var renderer in Renderers)
            {
                renderer.Update(deltaTime);
                renderer.Render(Camera, RenderPass.Both);
            }
        }
    }
}

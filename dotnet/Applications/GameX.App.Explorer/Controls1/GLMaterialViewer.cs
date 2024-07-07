using OpenStack.Graphics;
using OpenStack.Graphics.Controls;
using OpenStack.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace GameX.App.Explorer.Controls1
{
    public class GLMaterialViewer : GLViewerControl
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static readonly DependencyProperty GraphicProperty = DependencyProperty.Register(nameof(Graphic), typeof(object), typeof(GLMaterialViewer),
            new PropertyMetadata((d, e) => (d as GLMaterialViewer).OnProperty()));

        public IOpenGraphic Graphic
        {
            get => GetValue(GraphicProperty) as IOpenGraphic;
            set => SetValue(GraphicProperty, value);
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(object), typeof(GLMaterialViewer),
            new PropertyMetadata((d, e) => (d as GLMaterialViewer).OnProperty()));

        public object Source
        {
            get => GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        void OnProperty()
        {
            if (Graphic == null || Source == null) return;
            var graphic = Graphic as IOpenGLGraphic;
            var source = Source is IMaterial z ? z
                : Source is IRedirected<IMaterial> y ? y.Value
                : null;
            if (source == null) return;
            var (material, _) = graphic.MaterialManager.CreateMaterial(source);
            Renderers.Add(new MaterialRenderer(graphic, material));
        }

        readonly HashSet<MaterialRenderer> Renderers = [];

        protected override void Render(Camera camera, float frameTime)
        {
            foreach (var renderer in Renderers) renderer.Render(camera, RenderPass.Both);
        }
    }
}

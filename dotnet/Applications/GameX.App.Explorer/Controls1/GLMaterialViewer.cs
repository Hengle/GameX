using OpenStack.Gfx;
using OpenStack.Gfx.Gl;
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

        public static readonly DependencyProperty GfxProperty = DependencyProperty.Register(nameof(Gfx), typeof(object), typeof(GLMaterialViewer),
            new PropertyMetadata((d, e) => (d as GLMaterialViewer).OnProperty()));

        public IOpenGfx Gfx
        {
            get => GetValue(GfxProperty) as IOpenGfx;
            set => SetValue(GfxProperty, value);
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
            if (Gfx == null || Source == null) return;
            var gfx = Gfx as IOpenGLGfx;
            var source = Source is IMaterial z ? z
                : Source is IRedirected<IMaterial> y ? y.Value
                : null;
            if (source == null) return;
            var (material, _) = gfx.MaterialManager.CreateMaterial(source);
            Renderers.Add(new MaterialRenderer(gfx, material));
        }

        readonly HashSet<MaterialRenderer> Renderers = [];

        protected override void Render(Camera camera, float frameTime)
        {
            foreach (var renderer in Renderers) renderer.Render(camera, RenderPass.Both);
        }
    }
}

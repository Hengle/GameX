//using OpenStack;
//using OpenStack.Gfx;
//using System.ComponentModel;
//using System.Runtime.CompilerServices;

//namespace GameX.App.Explorer.Controls1
//{
//    //was:Renderer/GLMaterialViewer
//    public class GLMaterialViewer : OpenGLView
//    {
//        public GLMaterialViewer()
//        {
//            OnDisplay += OnPaint;
//            //Unloaded += (a, b) => { GLPaint -= OnPaint; };
//        }

//        public event PropertyChangedEventHandler PropertyChanged;
//        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

//        public static readonly BindableProperty GfxProperty = BindableProperty.Create(nameof(Gfx), typeof(object), typeof(GLMaterialViewer),
//            propertyChanged: (d, e, n) => (d as GLMaterialViewer).OnProperty());

//        public IOpenGfx Gfx
//        {
//            get => GetValue(GfxProperty) as IOpenGfx;
//            set => SetValue(GfxProperty, value);
//        }

//        public static readonly BindableProperty SourceProperty = BindableProperty.Create(nameof(Source), typeof(object), typeof(GLMaterialViewer),
//            propertyChanged: (d, e, n) => (d as GLMaterialViewer).OnProperty());

//        public object Source
//        {
//            get => GetValue(SourceProperty);
//            set => SetValue(SourceProperty, value);
//        }

//        void OnProperty()
//        {
//            if (Gfx == null || Source == null) return;
//            var gfx = Gfx as IOpenGLGfx;
//            var source = Source is IMaterial z ? z
//                : Source is IRedirected<IMaterial> y ? y.Value
//                : null;
//            if (source == null) return;
//            var material = gfx.MaterialManager.LoadMaterial(source, out var _);
//            Renderers.Add(new MaterialRenderer(gfx, material));
//        }

//        readonly HashSet<MaterialRenderer> Renderers = new();

//        void OnPaint(object sender, EventArgs e)
//        {
//            foreach (var renderer in Renderers) renderer.Render(e.Camera, RenderPass.Both);
//        }
//    }
//}

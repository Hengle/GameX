//using OpenStack;
//using OpenStack.Gfx;
//using System.ComponentModel;
//using System.Numerics;
//using System.Runtime.CompilerServices;

//namespace GameX.App.Explorer.Controls1
//{
//    //was:Renderer/GLTextureViewer
//    public class GLTextureViewer : OpenGLView
//    {
//        public GLTextureViewer()
//        {
//            OnDisplay += OnPaint;
//            //Unloaded += (s, e) => { GLPaint -= OnPaint; };
//        }

//        public event PropertyChangedEventHandler PropertyChanged;
//        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

//        public static readonly BindableProperty GfxProperty = BindableProperty.Create(nameof(Gfx), typeof(object), typeof(GLTextureViewer),
//            propertyChanged: (d, e, n) => (d as GLTextureViewer).OnProperty());
        
//        public IOpenGfx Gfx
//        {
//            get => GetValue(GfxProperty) as IOpenGfx;
//            set => SetValue(GfxProperty, value);
//        }

//        public static readonly BindableProperty SourceProperty = BindableProperty.Create(nameof(Source), typeof(object), typeof(GLTextureViewer),
//            propertyChanged: (d, e, n) => (d as GLTextureViewer).OnProperty());

//        public object Source
//        {
//            get => GetValue(SourceProperty);
//            set => SetValue(SourceProperty, value);
//        }

//        void OnProperty()
//        {
//            if (Gfx == null || Source == null) return;
//            var gfx = Gfx as IOpenGLGfx;
//            var source = Source is ITexture z ? z
//                : Source is IRedirected<ITexture> y ? y.Value
//                : null;
//            if (source == null) return;

//#if true
//            Camera.SetViewportSize(source.Width, source.Height);
//#endif
//            Camera.SetLocation(new Vector3(200));
//            Camera.LookAt(new Vector3(0));

//            var texture = gfx.TextureManager.LoadTexture(source, out _);
//            Renderers.Add(new TextureRenderer(gfx, texture));
//        }

//        readonly HashSet<TextureRenderer> Renderers = new();

//        void OnPaint(object sender, EventArgs e)
//        {
//            foreach (var renderer in Renderers) renderer.Render(e.Camera, RenderPass.Both);
//        }
//    }
//}

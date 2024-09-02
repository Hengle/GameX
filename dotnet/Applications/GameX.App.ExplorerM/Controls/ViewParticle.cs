//using OpenStack;
//using OpenStack.Gfx;
//using System.ComponentModel;
//using System.Numerics;
//using System.Runtime.CompilerServices;

//namespace GameX.App.Explorer.Controls1
//{
//    //was:Renderer/GLParticleViewer
//    public class GLParticleViewer : OpenGLView
//    {
//        ParticleGridRenderer particleGrid;
       
//        public GLParticleViewer()
//        {
//            GLPaint += OnPaint;
//            //Unloaded += (a, b) => { GLPaint -= OnPaint; };
//        }

//        public event PropertyChangedEventHandler PropertyChanged;
//        void NotifyPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

//        public static readonly BindableProperty GfxProperty = BindableProperty.Create(nameof(Gfx), typeof(object), typeof(GLParticleViewer),
//            propertyChanged: (d, e, n) => (d as GLParticleViewer).OnProperty());

//        public IOpenGfx Gfx
//        {
//            get => GetValue(GfxProperty) as IOpenGfx;
//            set => SetValue(GfxProperty, value);
//        }

//        public static readonly BindableProperty SourceProperty = BindableProperty.Creat(nameof(Source), typeof(object), typeof(GLParticleViewer),
//            propertyChanged: (d, e, n) => (d as GLParticleViewer).OnProperty());

//        public object Source
//        {
//            get => GetValue(SourceProperty);
//            set => SetValue(SourceProperty, value);
//        }

//        HashSet<ParticleRenderer> Renderers { get; } = new HashSet<ParticleRenderer>();

//        void OnProperty()
//        {
//            if (Gfx == null || Source == null) return;
//            var gfx = Gfx as IOpenGLGfx;
//            var source = Source is IParticleSystem z ? z
//                : Source is IRedirected<IParticleSystem> y ? y.Value
//                : null;
//            if (source == null) return;

//            particleGrid = new ParticleGridRenderer(20, 5, gfx);
//            Camera.SetViewportSize((int)ActualWidth, (int)ActualHeight);
//            Camera.SetLocation(new Vector3(200));
//            Camera.LookAt(new Vector3(0));

//            Renderers.Add(new ParticleRenderer(gfx, source));
//        }

//        void OnPaint(object sender, RenderEventArgs e)
//        {
//            particleGrid?.Render(e.Camera, RenderPass.Both);
//            foreach (var renderer in Renderers)
//            {
//                renderer.Update(e.FrameTime);
//                renderer.Render(e.Camera, RenderPass.Both);
//            }
//        }
//    }
//}

using OpenStack.Gfx;
using OpenStack.Sfx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static OpenStack.Debug;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace GameX.Platforms
{
    #region AudioBuilderBase

    public abstract class AudioBuilderBase<Audio>
    {
        public abstract Audio CreateAudio(object path);
        public abstract void DeleteAudio(Audio audio);
    }

    #endregion

    #region AudioManager

    public class AudioManager<Audio>(PakFile pakFile, AudioBuilderBase<Audio> builder) : IAudioManager<Audio>
    {
        readonly PakFile PakFile = pakFile;
        readonly AudioBuilderBase<Audio> Builder = builder;
        readonly Dictionary<object, (Audio aud, object tag)> CachedAudios = [];
        readonly Dictionary<object, Task<object>> PreloadTasks = [];

        public (Audio aud, object tag) CreateAudio(object path)
        {
            if (CachedAudios.TryGetValue(path, out var c)) return c;
            // load & cache the audio.
            var tag = LoadAudio(path).Result;
            var audio = tag != null ? Builder.CreateAudio(tag) : default;
            CachedAudios[path] = (audio, tag);
            return (audio, tag);
        }

        public void PreloadAudio(object path)
        {
            if (CachedAudios.ContainsKey(path)) return;
            // start loading the texture file asynchronously if we haven't already started.
            if (!PreloadTasks.ContainsKey(path)) PreloadTasks[path] = PakFile.LoadFileObject<object>(path);
        }

        public void DeleteAudio(object path)
        {
            if (!CachedAudios.TryGetValue(path, out var c)) return;
            Builder.DeleteAudio(c.aud);
            CachedAudios.Remove(path);
        }

        async Task<object> LoadAudio(object path)
        {
            Assert(!CachedAudios.ContainsKey(path));
            PreloadAudio(path);
            var source = await PreloadTasks[path];
            PreloadTasks.Remove(path);
            return source;
        }
    }

    #endregion

    #region TextureBuilderBase

    public abstract class TextureBuilderBase<Texture>
    {
        public static int MaxTextureMaxAnisotropy
        {
            get => PlatformStats.MaxTextureMaxAnisotropy;
            set => PlatformStats.MaxTextureMaxAnisotropy = value;
        }

        public abstract Texture DefaultTexture { get; }
        public abstract Texture CreateTexture(Texture reuse, ITexture source, Range? level = null);
        public abstract Texture CreateSolidTexture(int width, int height, float[] rgba);
        public abstract Texture CreateNormalMap(Texture texture, float strength);
        public abstract void DeleteTexture(Texture texture);
    }

    #endregion

    #region TextureManager

    public class TextureManager<Texture>(PakFile pakFile, TextureBuilderBase<Texture> builder) : ITextureManager<Texture>
    {
        readonly PakFile PakFile = pakFile;
        readonly TextureBuilderBase<Texture> Builder = builder;
        readonly Dictionary<object, (Texture tex, object tag)> CachedTextures = [];
        readonly Dictionary<object, Task<ITexture>> PreloadTasks = [];

        public Texture CreateSolidTexture(int width, int height, params float[] rgba) => Builder.CreateSolidTexture(width, height, rgba);

        public Texture CreateNormalMap(Texture source, float strength) => Builder.CreateNormalMap(source, strength);

        public Texture DefaultTexture => Builder.DefaultTexture;

        public (Texture tex, object tag) CreateTexture(object path, Range? level = null)
        {
            if (CachedTextures.TryGetValue(path, out var c)) return c;
            // load & cache the texture.
            var tag = path is ITexture z ? z : LoadTexture(path).Result;
            var texture = tag != null ? Builder.CreateTexture(default, tag, level) : Builder.DefaultTexture;
            CachedTextures[path] = (texture, tag);
            return (texture, tag);
        }

        public (Texture tex, object tag) ReloadTexture(object path, Range? level = null)
        {
            if (!CachedTextures.TryGetValue(path, out var c)) return (default, default);
            Builder.CreateTexture(c.tex, (ITexture)c.tag, level);
            return c;
        }

        public void PreloadTexture(object path)
        {
            if (CachedTextures.ContainsKey(path)) return;
            // start loading the texture file asynchronously if we haven't already started.
            if (!PreloadTasks.ContainsKey(path)) PreloadTasks[path] = PakFile.LoadFileObject<ITexture>(path);
        }

        public void DeleteTexture(object path)
        {
            if (!CachedTextures.TryGetValue(path, out var c)) return;
            Builder.DeleteTexture(c.tex);
            CachedTextures.Remove(path);
        }

        async Task<ITexture> LoadTexture(object path)
        {
            Assert(!CachedTextures.ContainsKey(path));
            PreloadTexture(path);
            var source = await PreloadTasks[path];
            PreloadTasks.Remove(path);
            return source;
        }
    }

    #endregion

    #region ShaderBuilderBase

    public abstract class ShaderBuilderBase<Shader>
    {
        public abstract Shader CreateShader(object path, IDictionary<string, bool> args = null);
    }

    #endregion

    #region ShaderManager

    public class ShaderManager<Shader>(PakFile pakFile, ShaderBuilderBase<Shader> builder) : IShaderManager<Shader>
    {
        static readonly Dictionary<string, bool> EmptyArgs = [];
        readonly PakFile PakFile = pakFile;
        readonly ShaderBuilderBase<Shader> Builder = builder;

        public (Shader sha, object tag) CreateShader(object path, IDictionary<string, bool> args = null)
            => (Builder.CreateShader(path, args ?? EmptyArgs), null);
    }

    #endregion

    #region ObjectBuilderBase

    public abstract class ObjectBuilderBase<Object, Material, Texture>
    {
        public abstract void EnsurePrefab();
        public abstract Object CreateNewObject(Object prefab);
        public abstract Object CreateObject(object source, IMaterialManager<Material, Texture> materialManager);
    }

    #endregion

    #region ObjectManager

    public class ObjectManager<Object, Material, Texture>(PakFile pakFile, IMaterialManager<Material, Texture> materialManager, ObjectBuilderBase<Object, Material, Texture> builder) : IObjectManager<Object, Material, Texture>
    {
        readonly PakFile PakFile = pakFile;
        readonly IMaterialManager<Material, Texture> MaterialManager = materialManager;
        readonly ObjectBuilderBase<Object, Material, Texture> Builder = builder;
        readonly Dictionary<object, (Object obj, object tag)> CachedObjects = new Dictionary<object, (Object obj, object tag)>();
        readonly Dictionary<object, Task<object>> PreloadTasks = new Dictionary<object, Task<object>>();

        public (Object obj, object tag) CreateObject(object path)
        {
            Builder.EnsurePrefab();
            // load & cache the prefab.
            if (!CachedObjects.TryGetValue(path, out var prefab)) prefab = CachedObjects[path] = LoadObject(path).Result;
            return (Builder.CreateNewObject(prefab.obj), prefab.tag);
        }

        public void PreloadObject(object path)
        {
            if (CachedObjects.ContainsKey(path)) return;
            // start loading the object asynchronously if we haven't already started.
            if (!PreloadTasks.ContainsKey(path)) PreloadTasks[path] = PakFile.LoadFileObject<object>(path);
        }

        async Task<(Object obj, object tag)> LoadObject(object path)
        {
            Assert(!CachedObjects.ContainsKey(path));
            PreloadObject(path);
            var source = await PreloadTasks[path];
            PreloadTasks.Remove(path);
            return (Builder.CreateObject(source, MaterialManager), source);
        }
    }

    #endregion

    #region MaterialBuilderBase

    public abstract class MaterialBuilderBase<Material, Texture>(ITextureManager<Texture> textureManager)
    {
        protected ITextureManager<Texture> TextureManager = textureManager;
        public float? NormalGeneratorIntensity = 0.75f;
        public abstract Material DefaultMaterial { get; }

        public abstract Material CreateMaterial(object path);
    }

    #endregion

    #region MaterialManager

    /// <summary>
    /// Manages loading and instantiation of materials.
    /// </summary>
    public class MaterialManager<Material, Texture>(PakFile pakFile, ITextureManager<Texture> textureManager, MaterialBuilderBase<Material, Texture> builder) : IMaterialManager<Material, Texture>
    {
        readonly PakFile PakFile = pakFile;
        readonly MaterialBuilderBase<Material, Texture> Builder = builder;
        readonly Dictionary<object, (Material material, object tag)> CachedMaterials = [];
        readonly Dictionary<object, Task<IMaterial>> PreloadTasks = [];
        public ITextureManager<Texture> TextureManager { get; } = textureManager;

        public (Material mat, object tag) CreateMaterial(object path)
        {
            if (CachedMaterials.TryGetValue(path, out var c)) return c;
            // load & cache the material.
            var source = path is IMaterial z ? z : LoadMaterial(path).Result;
            var material = source != null ? Builder.CreateMaterial(source) : Builder.DefaultMaterial;
            object tag = null; // source?.Data;
            CachedMaterials[path] = (material, tag);
            return (material, tag);
        }

        public void PreloadMaterial(object path)
        {
            if (CachedMaterials.ContainsKey(path)) return;
            // start loading the material file asynchronously if we haven't already started.
            if (!PreloadTasks.ContainsKey(path)) PreloadTasks[path] = PakFile.LoadFileObject<IMaterial>(path);
        }

        async Task<IMaterial> LoadMaterial(object path)
        {
            Assert(!CachedMaterials.ContainsKey(path));
            PreloadMaterial(path);
            var source = await PreloadTasks[path];
            PreloadTasks.Remove(path);
            return source;
        }
    }

    #endregion

    #region Platform

    /// <summary>
    /// Platform
    /// </summary>
    public static class Platform
    {
        /// <summary>
        /// The platform type.
        /// </summary>
        public enum Type { Unknown, OpenGL, Unity, Unreal, Vulken, StereoKit, Test, Other }

        /// <summary>
        /// The platform OS.
        /// </summary>
        public enum OS { Windows, OSX, Linux, Android }

        /// <summary>
        /// Gets or sets the platform.
        /// </summary>
        public static Type PlatformType;

        /// <summary>
        /// Gets or sets the platform tag.
        /// </summary>
        public static string PlatformTag;

        /// <summary>
        /// Gets the platform os.
        /// </summary>
        public static readonly OS PlatformOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OS.Windows
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OS.OSX
            : RuntimeInformation.OSDescription.StartsWith("android-") ? OS.Android
            : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OS.Linux
            : throw new ArgumentOutOfRangeException(nameof(RuntimeInformation.IsOSPlatform), RuntimeInformation.OSDescription);

        /// <summary>
        /// Gets or sets the platforms gfx factory.
        /// </summary>
        public static Func<PakFile, IOpenGfx> GfxFactory;

        /// <summary>
        /// Gets or sets the platforms sfx factory.
        /// </summary>
        public static Func<PakFile, IOpenSfx> SfxFactory;

        /// <summary>
        /// Gets the platform startups.
        /// </summary>
        public static readonly List<Func<bool>> Startups = new List<Func<bool>>();

        /// <summary>
        /// Determines if in a test host.
        /// </summary>
        public static bool InTestHost => AppDomain.CurrentDomain.GetAssemblies().Any(x => x.FullName.StartsWith("testhost,"));

        /// <summary>
        /// The platform stats.
        /// </summary>
        public class Stats
        {
            static readonly bool HighRes = Stopwatch.IsHighResolution;
            static readonly double HighFrequency = 1000.0 / Stopwatch.Frequency;
            static readonly double LowFrequency = 1000.0 / TimeSpan.TicksPerSecond;
            static readonly bool UseHrt = false;
            public static bool UsingHighResolutionTiming => UseHrt && HighRes && !Unix;
            public static long TickCount => (long)Ticks;
            public static double Ticks => UseHrt && HighRes && !Unix ? Stopwatch.GetTimestamp() * HighFrequency : DateTime.UtcNow.Ticks * LowFrequency;
            public static readonly bool Is64Bit = Environment.Is64BitProcess;
            public static bool MultiProcessor { get; private set; }
            public static int ProcessorCount { get; private set; }
            public static bool Unix { get; private set; }
            public static bool VR { get; private set; }
        }

        public static void Startup()
        {
            if (InTestHost && Startups.Count == 0) Startups.Add(TestPlatform.Startup);
            foreach (var startup in Startups) if (startup()) return;
            PlatformType = Type.Unknown;
            GfxFactory = source => null; // throw new Exception("No GfxFactory");
            SfxFactory = source => null; // throw new Exception("No SfxFactory");
            AssertFunc = x => System.Diagnostics.Debug.Assert(x);
            LogFunc = a => System.Diagnostics.Debug.Print(a);
            LogFormatFunc = (a, b) => System.Diagnostics.Debug.Print(a, b);
        }
    }

    #endregion

    #region TestGfx

    public interface ITestGfx : IOpenGfx { }

    public class TestGfx(PakFile source) : ITestGfx
    {
        readonly PakFile _source = source;
        public object Source => _source;
        public Task<T> LoadFileObject<T>(object path) => throw new NotSupportedException();
        public void PreloadTexture(object path) => throw new NotSupportedException();
        public void PreloadObject(object path) => throw new NotSupportedException();
    }

    #endregion

    #region TestSfx

    public interface ITestSfx : IOpenSfx { }

    public class TestSfx(PakFile source) : ITestSfx
    {
        readonly PakFile _source = source;
        public object Source => _source;
    }

    #endregion

    #region TestPlatform

    public static class TestPlatform
    {
        public static bool Startup()
        {
            try
            {
                Platform.PlatformType = Platform.Type.Test;
                Platform.GfxFactory = source => new TestGfx(source);
                Platform.SfxFactory = source => new TestSfx(source);
                AssertFunc = x => System.Diagnostics.Debug.Assert(x);
                LogFunc = a => System.Diagnostics.Debug.Print(a);
                LogFormatFunc = (a, b) => System.Diagnostics.Debug.Print(a, b);
                return true;
            }
            catch { return false; }
        }
    }

    #endregion
}
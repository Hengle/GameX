using GameX.Formats;
using GameX.Unknown;
using OpenStack.Gfx;
using OpenStack.Sfx;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static OpenStack.Debug;

namespace GameX
{
    #region FileOption

    [Flags]
    public enum FileOption
    {
        Hosting = Raw | Marker,
        None = 0x0,
        Raw = 0x1,
        Marker = 0x2,
        Stream = 0x4,
        Model = 0x8,
        Supress = 0x10,
    }

    #endregion

    #region ITransformFileObject

    /// <summary>
    /// ITransformFileObject
    /// </summary>
    public interface ITransformFileObject<T>
    {
        /// <summary>
        /// Determines whether this instance [can transform file object] the specified transform to.
        /// </summary>
        /// <param name="transformTo">The transform to.</param>
        /// <param name="source">The source.</param>
        /// <returns>
        ///   <c>true</c> if this instance [can transform file object] the specified transform to; otherwise, <c>false</c>.
        /// </returns>
        bool CanTransformFileObject(PakFile transformTo, object source);
        /// <summary>
        /// Transforms the file object asynchronous.
        /// </summary>
        /// <param name="transformTo">The transform to.</param>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        Task<T> TransformFileObject(PakFile transformTo, object source);
    }

    #endregion

    #region PakState

    /// <summary>
    /// PakState
    /// </summary>
    /// <param name="fileSystem">The file system.</param>
    /// <param name="game">The game.</param>
    /// <param name="edition">The edition.</param>
    /// <param name="path">The path.</param>
    /// <param name="tag">The tag.</param>
    public class PakState(IFileSystem fileSystem, FamilyGame game, FamilyGame.Edition edition = null, string path = null, object tag = null)
    {
        /// <summary>
        /// Gets the filesystem.
        /// </summary>
        public readonly IFileSystem FileSystem = fileSystem;

        /// <summary>
        /// Gets the pak family game.
        /// </summary>
        public readonly FamilyGame Game = game;

        /// <summary>
        /// Gets the filesystem.
        /// </summary>
        public readonly FamilyGame.Edition Edition = edition;

        /// <summary>
        /// Gets the path.
        /// </summary>
        public readonly string Path = path ?? string.Empty;

        /// <summary>
        /// Gets the tag.
        /// </summary>
        public object Tag = tag;
    }

    #endregion

    #region PakFile

    /// <summary>
    /// PakFile
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public abstract class PakFile : IDisposable
    {
        public delegate (FileOption option, Func<BinaryReader, FileSource, PakFile, Task<object>> factory) FuncObjectFactory(FileSource source, FamilyGame game);

        /// <summary>
        /// An empty family.
        /// </summary>
        public static PakFile Empty = new UnknownPakFile(new PakState(new StandardFileSystem(""), FamilyGame.Empty)) { Name = "Empty" };

        public enum PakStatus { Opening, Opened, Closing, Closed }

        /// <summary>
        /// The status
        /// </summary>
        public volatile PakStatus Status;

        /// <summary>
        /// The filesystem.
        /// </summary>
        public readonly IFileSystem FileSystem;

        /// <summary>
        /// The pak family.
        /// </summary>
        public readonly Family Family;

        /// <summary>
        /// The pak family game.
        /// </summary>
        public readonly FamilyGame Game;

        /// <summary>
        /// The filesystem.
        /// </summary>
        public readonly FamilyGame.Edition Edition;

        /// <summary>
        /// The pak path.
        /// </summary>
        public string PakPath;

        /// <summary>
        /// The pak name.
        /// </summary>
        public string Name;

        /// <summary>
        /// The tag.
        /// </summary>
        public object Tag;

        /// <summary>
        /// The pak path finders.
        /// </summary>
        public readonly Dictionary<Type, Func<string, string>> PathFinders = [];

        /// <summary>
        /// The pak path finders.
        /// </summary>
        public FuncObjectFactory ObjectFactoryFunc;

        /// <summary>
        /// Initializes a new instance of the <see cref="PakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <param name="name">The name.</param>
        public PakFile(PakState state)
        {
            string z;
            Status = PakStatus.Closed;
            FileSystem = state.FileSystem ?? throw new ArgumentNullException(nameof(state.FileSystem));
            Family = state.Game.Family ?? throw new ArgumentNullException(nameof(state.Game.Family));
            Game = state.Game ?? throw new ArgumentNullException(nameof(state.Game));
            Edition = state.Edition;
            PakPath = state.Path;
            Name = !string.IsNullOrEmpty(z = Path.GetFileName(state.Path)) ? z : Path.GetFileName(Path.GetDirectoryName(state.Path));
            Tag = state.Tag;
            ObjectFactoryFunc = null;
            Gfx = null;
        }

        /// <summary>
        /// Determines whether this instance is valid.
        /// </summary>
        public virtual bool Valid => true;

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Close();
            GC.SuppressFinalize(this);
        }
        ~PakFile() => Close();

        /// <summary>
        /// Closes this instance.
        /// </summary>
        public PakFile Close()
        {
            Status = PakStatus.Closing;
            Closing();
            if (Tag is IDisposable s) s.Dispose();
            Status = PakStatus.Closed;
            return this;
        }

        /// <summary>
        /// Closes this instance.
        /// </summary>
        public abstract void Closing();

        /// <summary>
        /// Opens this instance.
        /// </summary>
        public virtual PakFile Open(List<MetaItem> items = null, MetaManager manager = null)
        {
            if (Status != PakStatus.Closed) return this;
            Status = PakStatus.Opening;
            var watch = new Stopwatch();
            watch.Start();
            Opening();
            watch.Stop();
            Status = PakStatus.Opened;
            items?.AddRange(GetMetaItems(manager));
            Log($"Opened: {Name} @ {watch.ElapsedMilliseconds}ms");
            return this;
        }

        /// <summary>
        /// Opens this instance.
        /// </summary>
        public abstract void Opening();

        /// <summary>
        /// Determines whether this instance contains the item.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>
        ///   <c>true</c> if [contains] [the specified file path]; otherwise, <c>false</c>.
        /// </returns>
        public abstract bool Contains(object path);

        /// <summary>
        /// Gets the pak item count.
        /// </summary>
        /// <value>
        /// The count.
        /// </value>
        public abstract int Count { get; }

        /// <summary>
        /// Finds the path.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public string FindPath<T>(string path)
        {
            if (PathFinders.Count != 1) return PathFinders.TryGetValue(typeof(T), out var pathFinder) ? pathFinder(path) : path;
            var first = PathFinders.First();
            return first.Key == typeof(T) || first.Key == typeof(object) ? first.Value(path) : path;
        }

        /// <summary>
        /// Gets the gfx.
        /// </summary>
        /// <value>
        /// The gfx.
        /// </value>
        public IOpenGfx Gfx { get; internal set; }

        /// <summary>
        /// Gets the gfx.
        /// </summary>
        /// <value>
        /// The sfx.
        /// </value>
        public IOpenSfx Sfx { get; internal set; }

        /// <summary>
        /// Gets the file source.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="throwOnError">Throws on error.</param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public abstract (PakFile, FileSource) GetFileSource(object path, bool throwOnError = true);

        /// <summary>
        /// Loads the file data asynchronous.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="option">The option.</param>
        /// <param name="throwOnError">Throws on error.</param>
        /// <returns></returns>
        public abstract Task<Stream> LoadFileData(object path, FileOption option = default, bool throwOnError = true);

        /// <summary>
        /// Loads the object asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path">The file path.</param>
        /// <param name="option">The option.</param>
        /// <param name="throwOnError">Throws on error.</param>
        /// <returns></returns>
        public abstract Task<T> LoadFileObject<T>(object path, FileOption option = default, bool throwOnError = true);

        /// Opens the family pak file.
        /// </summary>
        /// <param name="res">The res.</param>
        /// <param name="throwOnError">Throws on error.</param>
        /// <returns></returns>
        public PakFile OpenPakFile(object res, bool throwOnError = true)
            => res switch
            {
                string s => Game.CreatePakFile(FileSystem, Edition, s, throwOnError)?.Open(),
                _ => throw new ArgumentOutOfRangeException(nameof(res)),
            };

        #region Transform

        /// <summary>
        /// Loads the object transformed asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path">The file path.</param>
        /// <param name="transformTo">The transformTo.</param>
        /// <returns></returns>
        public async Task<T> LoadFileObject<T>(object path, PakFile transformTo) => await TransformFileObject<T>(transformTo, await LoadFileObject<object>(path));

        /// <summary>
        /// Transforms the file object asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="transformTo">The transformTo.</param>
        /// <param name="source">The source.</param>
        /// <returns></returns>
        Task<T> TransformFileObject<T>(PakFile transformTo, object source)
        {
            if (this is ITransformFileObject<T> left && left.CanTransformFileObject(transformTo, source)) return left.TransformFileObject(transformTo, source);
            else if (transformTo is ITransformFileObject<T> right && right.CanTransformFileObject(transformTo, source)) return right.TransformFileObject(transformTo, source);
            else throw new ArgumentOutOfRangeException(nameof(transformTo));
        }

        #endregion

        #region Metadata

        /// <summary>
        /// Gets the metadata item filters.
        /// </summary>
        /// <param name="manager">The resource.</param>
        /// <returns></returns>
        public virtual List<MetaItem.Filter> GetMetadataFilters(MetaManager manager) => Family.FileManager != null && Family.FileManager.Filters.TryGetValue(Game.Id, out var z) ? z.Select(x => new MetaItem.Filter(x.Key, x.Value)).ToList() : null;

        /// <summary>
        /// Gets the metadata infos.
        /// </summary>
        /// <param name="manager">The resource.</param>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        public virtual Task<List<MetaInfo>> GetMetaInfos(MetaManager manager, MetaItem item) => throw new NotImplementedException();

        /// <summary>
        /// Gets the metadata items.
        /// </summary>
        /// <param name="manager">The resource.</param>
        /// <returns></returns>
        public virtual List<MetaItem> GetMetaItems(MetaManager manager) => throw new NotImplementedException();

        #endregion
    }

    #endregion

    #region BinaryPakFile

    /// <summary>
    /// Initializes a new instance of the <see cref="BinaryPakFile" /> class.
    /// </summary>
    /// <param name="state">The state.</param>
    /// <param name="name">The name.</param>
    /// <param name="pakBinary">The pak binary.</param>
    /// <exception cref="ArgumentNullException">pakBinary</exception>
    [DebuggerDisplay("{Name}")]
    public abstract class BinaryPakFile(PakState state, PakBinary pakBinary) : PakFile(state)
    {
        public readonly PakBinary PakBinary = pakBinary;
        // options
        public int RetainInPool = 10;
        public bool UseReader = true;
        public bool UseWriter = true;
        public bool UseFileId = false;
        // state
        public Func<string, string> FileMask;
        public readonly Dictionary<string, string> Params = [];
        public uint Magic;
        public uint Version;
        // metadata/factory
        protected Dictionary<string, Func<MetaManager, BinaryPakFile, FileSource, Task<List<MetaInfo>>>> MetaInfos = [];

        // binary
        public IList<FileSource> Files;
        public HashSet<string> FilesRawSet;
        public ILookup<int, FileSource> FilesById { get; private set; }
        public ILookup<string, FileSource> FilesByPath { get; private set; }
        public int PathSkip;

        /// <summary>
        /// Valid
        /// </summary>
        public override bool Valid => Files != null;

        #region Pool

        readonly ConcurrentDictionary<string, GenericPool<BinaryReader>> Readers = new();
        readonly ConcurrentDictionary<string, GenericPool<BinaryWriter>> Writers = new();

        /// <summary>
        /// Gets the binary reader.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public virtual IGenericPool<BinaryReader> GetReader(string path = default, bool pooled = true) => pooled
            ? Readers.GetOrAdd(path ?? PakPath, path => FileSystem.FileExists(path) ? new GenericPool<BinaryReader>(() => FileSystem.OpenReader(path), r => r.Seek(0), RetainInPool) : null)
            : new SinglePool<BinaryReader>(FileSystem.FileExists(path ??= PakPath) ? FileSystem.OpenReader(path) : null);

        /// <summary>
        /// Reader
        /// </summary>
        /// <param name="func">The func.</param>
        /// <param name="path">The path.</param>
        /// <param name="pooled">The pooled.</param>
        /// <returns></returns>
        public void Reader(Action<BinaryReader> func, string path = default, bool pooled = false) => GetReader(path, pooled).Action(func);

        /// <summary>
        /// Reader
        /// </summary>
        /// <param name="func">The func.</param>
        /// <param name="path">The path.</param>
        /// <param name="pooled">The pooled.</param>
        /// <returns></returns>
        public TResult ReaderT<TResult>(Func<BinaryReader, TResult> func, string path = default, bool pooled = false) => GetReader(path, pooled).Func(func);

        /// <summary>
        /// Reader
        /// </summary>
        /// <param name="func">The func.</param>
        /// <param name="path">The path.</param>
        /// <param name="pooled">The ppooledath.</param>
        /// <returns></returns>
        public Task ReaderAsync(Func<BinaryReader, Task> func, string path = default, bool pooled = true) => GetReader(path, pooled).ActionAsync(func);

        /// <summary>
        /// Reader
        /// </summary>
        /// <param name="func">The func.</param>
        /// <param name="path">The path.</param>
        /// <param name="pooled">The pooled.</param>
        /// <returns></returns>
        public Task<TResult> ReaderTAsync<TResult>(Func<BinaryReader, Task<TResult>> func, string path = default, bool pooled = true) => GetReader(path, pooled).FuncAsync(func);

        /// <summary>
        /// Gets the binary reader.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public GenericPool<BinaryWriter> GetWriter(string path = default)
            => Writers.GetOrAdd(path ?? PakPath, path => FileSystem.FileExists(path) ? new GenericPool<BinaryWriter>(() => FileSystem.OpenWriter(path), r => r.Seek(0), RetainInPool) : null);

        /// <summary>
        /// Writer
        /// </summary>
        /// <param name="func">The func.</param>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public Task WriterActionAsync(Func<BinaryWriter, Task> func, string path = default) => GetWriter(path).ActionAsync(w => func(w));

        /// <summary>
        /// Writer
        /// </summary>
        /// <param name="func">The func.</param>
        /// <param name="path">The path.</param>
        /// <returns></returns>
        public Task<TResult> WriterFuncAsync<TResult>(Func<BinaryWriter, Task<TResult>> func, string path = default) => GetWriter(path).FuncAsync(w => func(w));

        #endregion

        /// <summary>
        /// Opens this instance.
        /// </summary>
        public async override void Opening()
        {
            await Read();
            await Process();
        }

        /// <summary>
        /// Closes this instance.
        /// </summary>
        public override void Closing()
        {
            Files = null;
            FilesRawSet = null;
            FilesById = null;
            FilesByPath = null;
            foreach (var r in Readers.Values) r.Dispose();
            Readers.Clear();
        }

        /// <summary>
        /// Determines whether the pak contains the specified file path.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <returns>
        ///   <c>true</c> if the specified file path contains file; otherwise, <c>false</c>.
        /// </returns>
        public override bool Contains(object path)
        {
            switch (path)
            {
                case null: throw new ArgumentNullException(nameof(path));
                case string s:
                    {
                        var (pak, s2) = FindPath(s);
                        return pak != null
                        ? pak.Contains(s2)
                        : FilesByPath != null && FilesByPath.Contains(s.Replace('\\', '/'));
                    }
                case int i: return FilesById != null && FilesById.Contains(i);
                default: throw new ArgumentOutOfRangeException(nameof(path));
            }
        }

        /// <summary>Gets the count.</summary>
        /// <value>The count.</value>
        /// <exception cref="System.NotSupportedException"></exception>
        public override int Count => FilesByPath.Count;

        /// <summary>
        /// Finds the texture.
        /// </summary>
        /// <param name="path">The texture path.</param>
        /// <returns></returns>
        //public override string FindTexture(string path) => Contains(path) ? path : null;

        /// <summary>
        /// Gets the file source.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="throwOnError">Throws on error.</param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public override (PakFile, FileSource) GetFileSource(object path, bool throwOnError = true)
        {
            switch (path)
            {
                case null: throw new ArgumentNullException(nameof(path));
                case FileSource f: return (this, f);
                case string s:
                    {
                        var (pak, s2) = FindPath(s);
                        if (pak != null) return pak.GetFileSource(s2);
                        var files = FilesByPath[s.Replace('\\', '/')].ToArray();
                        if (files.Length == 1) return (this, files[0]);
                        Log($"ERROR.LoadFileData: {s} @ {files.Length}");
                        if (throwOnError) throw new FileNotFoundException(files.Length == 0 ? $"File not found: {s}" : $"More then one file found: {s}");
                        return (null, null);
                    }
                case int i:
                    {
                        var files = FilesById[i].ToArray();
                        if (files.Length == 1) return (this, files[0]);
                        Log($"ERROR.LoadFileData: {i} @ {files.Length}");
                        if (throwOnError) throw new FileNotFoundException(files.Length == 0 ? $"File not found: {i}" : $"More then one file found: {i}");
                        return (null, null);
                    }
                default: throw new ArgumentOutOfRangeException(nameof(path));
            }
        }

        /// <summary>
        /// Loads the file data asynchronous.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="option">The option.</param>
        /// <param name="throwOnError">Throws on error.</param>
        /// <returns></returns>
        /// <exception cref="FileNotFoundException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public override Task<Stream> LoadFileData(object path, FileOption option = default, bool throwOnError = true)
        {
            if (path == null) return default;
            else if (path is not FileSource)
            {
                var (p, f2) = GetFileSource(path, throwOnError);
                return p?.LoadFileData(f2, option, throwOnError);
            }
            var f = (FileSource)path;
            return ReadData(f, option);
        }

        /// <summary>
        /// Loads the file object asynchronous.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path">The file.</param>
        /// <param name="option">The option.</param>
        /// <param name="throwOnError">Throws on error.</param>
        /// <returns></returns>
        public override async Task<T> LoadFileObject<T>(object path, FileOption option = default, bool throwOnError = true)
        {
            if (path == null) return default;
            else if (path is not FileSource)
            {
                var (p, f2) = GetFileSource(path, throwOnError);
                return await p.LoadFileObject<T>(f2, option, throwOnError);
            }
            var f = (FileSource)path;
            if (Game.IsPakFile(f.Path)) return default;
            var type = typeof(T);
            var data = await LoadFileData(f, option, throwOnError);
            if (data == null) return default;
            var objectFactory = EnsureCachedObjectFactory(f);
            if (objectFactory != FileSource.EmptyObjectFactory)
            {
                var r = new BinaryReader(data);
                object value = null;
                Task<object> task = null;
                try
                {
                    task = objectFactory(r, f, this);
                    if (task != null)
                    {
                        value = await task;
                        return value is T z ? z
                            : value is IRedirected<T> y ? y.Value
                            : throw new InvalidCastException();
                    }
                }
                catch (Exception e) { Log(e.Message); throw e; }
                finally { if (task != null && !(value != null && value is IDisposable)) r.Dispose(); }
            }
            return type == typeof(Stream) || type == typeof(object)
                ? (T)(object)data
                : throw new ArgumentOutOfRangeException(nameof(T), $"Stream not returned for {f.Path} with {type.Name}");
        }

        /// <summary>
        /// Ensures the file object factory.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        public Func<BinaryReader, FileSource, PakFile, Task<object>> EnsureCachedObjectFactory(FileSource file)
        {
            if (ObjectFactoryFunc == null) return FileSource.EmptyObjectFactory;
            if (file.CachedObjectFactory != null) return file.CachedObjectFactory;
            var factory = ObjectFactoryFunc(file, Game);
            file.CachedObjectOption = factory.option;
            file.CachedObjectFactory = factory.factory ?? FileSource.EmptyObjectFactory;
            return file.CachedObjectFactory;
        }

        /// <summary>
        /// Processes this instance.
        /// </summary>
        public async virtual Task Process()
        {
            if (UseFileId) FilesById = Files.Where(x => x != null).ToLookup(x => x.Id);
            FilesByPath = Files.Where(x => x != null).ToLookup(x => x.Path, StringComparer.OrdinalIgnoreCase);
            if (PakBinary != null) await PakBinary.Process(this);
        }

        /// <summary>
        /// FindPath.
        /// </summary>
        /// <param name="path">The path.</param>
        (PakFile pak, string next) FindPath(string path)
        {
            var paths = path.Split([':'], 2);
            var p = paths[0].Replace('\\', '/');
            var pak = FilesByPath[p]?.FirstOrDefault()?.Pak?.Open();
            return (pak, pak != null && paths.Length > 1 ? paths[1] : null);
        }

        #region PakBinary

        /// <summary>
        /// Reads the asynchronous.
        /// </summary>
        /// <param name="r">The r.</param>
        /// <param name="tag">The tag.</param>
        /// <returns></returns>
        public virtual Task Read(object tag = default) => UseReader
            ? ReaderT(r => PakBinary.Read(this, r, tag))
            : PakBinary.Read(this, null, tag);

        /// <summary>
        /// Reads the file data asynchronous.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="option">The option.</param>
        /// <returns></returns>
        public virtual Task<Stream> ReadData(FileSource file, FileOption option = default) => UseReader
            ? ReaderT(r => PakBinary.ReadData(this, r, file, option))
            : PakBinary.ReadData(this, null, file, option);

        /// <summary>
        /// Writes the asynchronous.
        /// </summary>
        /// <param name="w">The w.</param>
        /// <param name="tag">The tag.</param>
        /// <returns></returns>
        public virtual Task Write(object tag = default) => UseWriter
            ? WriterActionAsync(w => PakBinary.Write(this, w, tag))
            : PakBinary.Write(this, null, tag);

        /// <summary>
        /// Writes the file data asynchronous.
        /// </summary>
        /// <param name="file">The file.</param>
        /// <param name="data">The data.</param>
        /// <param name="option">The option.</param>
        /// <returns></returns>
        public virtual Task WriteData(FileSource file, Stream data, FileOption option = default) => UseWriter
            ? WriterActionAsync(w => PakBinary.WriteData(this, w, file, data, option))
            : PakBinary.WriteData(this, null, file, data, option);

        #endregion

        #region Metadata

        /// <summary>
        /// Gets the explorer information nodes.
        /// </summary>
        /// <param name="manager">The manager.</param>
        /// <param name="item">The item.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override Task<List<MetaInfo>> GetMetaInfos(MetaManager manager, MetaItem item)
            => Valid ? MetaManager.GetMetaInfos(manager, this, item.Source as FileSource) : Task.FromResult(new List<MetaInfo>());

        /// <summary>
        /// Gets the explorer item nodes.
        /// </summary>
        /// <param name="manager">The resource.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override List<MetaItem> GetMetaItems(MetaManager manager)
            => Valid ? MetaManager.GetMetaItems(manager, this) : new List<MetaItem>();

        #endregion
    }

    #endregion

    #region ManyPakFile

    public class ManyPakFile : BinaryPakFile
    {
        /// <summary>
        /// The paths
        /// </summary>
        public readonly string[] Paths;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPakFile" /> class.
        /// </summary>
        /// <param name="basis">The basis.</param>
        /// <param name="state">The state.</param>
        /// <param name="name">The name.</param>
        /// <param name="paths">The paths.</param>
        /// <param name="pathSkip">The pathSkip.</param>
        public ManyPakFile(PakFile basis, PakState state, string name, string[] paths, int pathSkip = 0) : base(state, null)
        {
            ObjectFactoryFunc = basis.ObjectFactoryFunc;
            Name = name;
            Paths = paths;
            PathSkip = pathSkip;
            UseReader = false;
        }

        #region PakBinary

        /// <summary>
        /// Reads the asynchronous.
        /// </summary>
        /// <param name="tag">The tag.</param>
        /// <returns></returns>
        public override Task Read(object tag = default)
        {
            Files = Paths.Select(s => new FileSource
            {
                Path = s.Replace('\\', '/'),
                Pak = Game.IsPakFile(s) ? (BinaryPakFile)Game.CreatePakFileType(new PakState(FileSystem, Game, Edition, s)) : default,
                FileSize = FileSystem.FileInfo(s).length,
            }).ToArray();
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(FileSource file, FileOption option = default)
            => file.Pak != null
                ? file.Pak.ReadData(file, option)
                : Task.FromResult<Stream>(new MemoryStream(FileSystem.OpenReader(file.Path).ReadBytes((int)file.FileSize)));

        #endregion
    }

    #endregion

    #region MultiPakFile

    [DebuggerDisplay("Paks: {Paks.Count}")]
    public class MultiPakFile : PakFile
    {
        /// <summary>
        /// The paks
        /// </summary>
        public readonly IList<PakFile> PakFiles;

        /// <summary>
        /// Initializes a new instance of the <see cref="MultiPakFile" /> class.
        /// </summary>
        /// <param name="state">The state.</param>
        /// <param name="name">The name.</param>
        /// <param name="pakFiles">The packs.</param>
        /// <param name="tag">The tag.</param>
        public MultiPakFile(PakState state, string name, IList<PakFile> pakFiles) : base(state)
        {
            Name = name;
            PakFiles = pakFiles ?? throw new ArgumentNullException(nameof(pakFiles));
        }

        /// <summary>
        /// Closes this instance.
        /// </summary>
        public override void Closing()
        {
            foreach (var pakFile in PakFiles) pakFile.Close();
        }

        /// <summary>
        /// Opens this instance.
        /// </summary>
        public override void Opening()
        {
            foreach (var pakFile in PakFiles) pakFile.Open();
        }

        /// <summary>
        /// Determines whether the specified file path contains file.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <returns>
        ///   <c>true</c> if the specified file path contains file; otherwise, <c>false</c>.
        /// </returns>
        public override bool Contains(object path)
            => path switch
            {
                null => throw new ArgumentNullException(nameof(path)),
                string s => FindPakFiles(s, out var next).Any(x => x.Valid && x.Contains(next)),
                int i => PakFiles.Any(x => x.Valid && x.Contains(i)),
                _ => throw new ArgumentOutOfRangeException(nameof(path)),
            };

        /// <summary>
        /// Gets the count.
        /// </summary>
        /// <value>
        /// The count.
        /// </value>
        public override int Count
        {
            get { var count = 0; foreach (var pakFile in PakFiles) count += pakFile.Count; return count; }
        }

        IList<PakFile> FindPakFiles(string path, out string next)
        {
            var paths = path.Split(['\\', '/', ':'], 2);
            if (paths.Length == 1) { next = path; return PakFiles; }
            path = paths[0]; next = paths[1];
            var pakFiles = PakFiles.Where(x => x.Name.StartsWith(path)).ToList();
            foreach (var pakFile in pakFiles) pakFile.Open();
            return pakFiles;
        }

        /// <summary>
        /// Gets the file source.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="throwOnError">Throws on error.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException">Could not find file \"{path}\".</exception>
        public override (PakFile, FileSource) GetFileSource(object path, bool throwOnError = true)
            => path switch
            {
                null => throw new ArgumentNullException(nameof(path)),
                string s => (FindPakFiles(s, out var s2).FirstOrDefault(x => x.Valid && x.Contains(s2)) ?? throw new FileNotFoundException($"Could not find file \"{s}\"."))
                    .GetFileSource(s2, throwOnError),
                int i => (PakFiles.FirstOrDefault(x => x.Valid && x.Contains(i)) ?? throw new FileNotFoundException($"Could not find file \"{i}\"."))
                    .GetFileSource(i, throwOnError),
                _ => throw new ArgumentOutOfRangeException(nameof(path)),
            };

        /// <summary>
        /// Loads the file data asynchronous.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="option">The option.</param>
        /// <param name="throwOnError">Throws on error.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException">Could not find file \"{path}\".</exception>
        public override Task<Stream> LoadFileData(object path, FileOption option = default, bool throwOnError = true)
            => path switch
            {
                null => throw new ArgumentNullException(nameof(path)),
                string s => (FindPakFiles(s, out var s2).FirstOrDefault(x => x.Valid && x.Contains(s2)) ?? throw new FileNotFoundException($"Could not find file \"{s}\"."))
                    .LoadFileData(s2, option, throwOnError),
                int i => (PakFiles.FirstOrDefault(x => x.Valid && x.Contains(i)) ?? throw new FileNotFoundException($"Could not find file \"{i}\"."))
                    .LoadFileData(i, option, throwOnError),
                _ => throw new ArgumentOutOfRangeException(nameof(path)),
            };

        /// <summary>
        /// Loads the object asynchronous.
        /// </summary>
        /// <param name="path">The path.</param>
        /// <param name="option">The option.</param>
        /// <param name="throwOnError">Throws on error.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException">Could not find file \"{path}\".</exception>
        public override Task<T> LoadFileObject<T>(object path, FileOption option = default, bool throwOnError = true)
            => path switch
            {
                null => throw new ArgumentNullException(nameof(path)),
                string s => (FindPakFiles(s, out var s2).FirstOrDefault(x => x.Valid && x.Contains(s2)) ?? throw new FileNotFoundException($"Could not find file \"{s}\"."))
                    .LoadFileObject<T>(s2, option, throwOnError),
                int i => (PakFiles.FirstOrDefault(x => x.Valid && x.Contains(i)) ?? throw new FileNotFoundException($"Could not find file \"{i}\"."))
                    .LoadFileObject<T>(i, option, throwOnError),
                _ => throw new ArgumentOutOfRangeException(nameof(path)),
            };

        #region Metadata

        /// <summary>
        /// Gets the metadata items.
        /// </summary>
        /// <param name="manager">The resource.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public override List<MetaItem> GetMetaItems(MetaManager manager)
        {
            var root = new List<MetaItem>();
            foreach (var pakFile in PakFiles.Where(x => x.Valid))
                root.Add(new MetaItem(pakFile, pakFile.Name, manager.PackageIcon, pakFile: pakFile, items: pakFile.GetMetaItems(manager)));
            return root;
        }

        #endregion
    }

    #endregion

    #region PakBinary

    public class PakBinary
    {
        /// <summary>
        /// The file
        /// </summary>
        public readonly static PakBinary Stream = new PakBinaryCanStream();

        /// <summary>
        /// Reads the asynchronous.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="r">The r.</param>
        /// <param name="tag">The tag.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public virtual Task Read(BinaryPakFile source, BinaryReader r, object tag = default) => throw new NotSupportedException();

        /// <summary>
        /// Reads the asynchronous.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="r">The r.</param>
        /// <param name="file">The file.</param>
        /// <param name="option">The option.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public virtual Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default) => throw new NotSupportedException();

        /// <summary>
        /// Writes the asynchronous.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="w">The w.</param>
        /// <param name="tag">The tag.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public virtual Task Write(BinaryPakFile source, BinaryWriter w, object tag = default) => throw new NotSupportedException();

        /// <summary>
        /// Writes the asynchronous.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="w">The w.</param>
        /// <param name="file">The file.</param>
        /// <param name="option">The option.</param>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        /// <exception cref="NotSupportedException"></exception>
        public virtual Task WriteData(BinaryPakFile source, BinaryWriter w, FileSource file, Stream data, FileOption option = default) => throw new NotSupportedException();

        /// <summary>
        /// Processes this instance.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <exception cref="NotSupportedException"></exception>
        public virtual Task Process(BinaryPakFile source) => Task.CompletedTask;

        /// <summary>
        /// handles an exception.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="option">The option.</param>
        /// <param name="message">The message.</param>
        /// <exception cref="NotSupportedException"></exception>
        public static void HandleException(object source, FileOption option, string message)
        {
            Log(message);
            if ((option & FileOption.Supress) != 0) throw new Exception(message);
        }
    }

    #endregion

    #region PakBinaryT

    public class PakBinary<Self> : PakBinary where Self : PakBinary, new()
    {
        public static readonly PakBinary Current = new Self();

        protected class SubPakFile : BinaryPakFile
        {
            readonly FileSource File;
            readonly BinaryPakFile Source;
            StaticPool<BinaryReader> Pool;
            BinaryReader R;

            public SubPakFile(BinaryPakFile source, FileSource file, string path, object tag = null, PakBinary instance = null) : base(new PakState(source.FileSystem, source.Game, source.Edition, path, tag), instance ?? Current)
            {
                File = file;
                Source = source;
                ObjectFactoryFunc = source.ObjectFactoryFunc;
                //Open();
            }

            public async override void Opening() { R = new BinaryReader(await Source.ReadData(File)); Pool = new StaticPool<BinaryReader>(R); base.Opening(); }
            public override void Closing() { R?.Dispose(); base.Closing(); }
            public override IGenericPool<BinaryReader> GetReader(string path = null, bool pooled = true) => Pool;
        }
    }

    #endregion
}
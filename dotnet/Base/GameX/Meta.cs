using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameX.Meta
{
    #region MetaContent

    [DebuggerDisplay("{Type}: {Name}")]
    public class MetaContent
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public object Value { get; set; }
        public object Tag { get; set; }
        public int MaxWidth { get; set; }
        public int MaxHeight { get; set; }
        public IDisposable Dispose { get; set; }
        public Type EngineType { get; set; }
    }

    #endregion

    #region MetaInfo

    [DebuggerDisplay("{Name}, items: {Items.Count} [{Tag}]")]
    public class MetaInfo(string name, object tag = null, IEnumerable<MetaInfo> items = null, bool clickable = false)
    {
        public string Name { get; set; } = name;
        public object Tag { get; } = tag;
        public IEnumerable<MetaInfo> Items { get; } = items ?? [];
        public bool Clickable { get; set; } = clickable;

        public static MetaInfo WrapWithGroup<T>(IList<T> source, string groupName, IEnumerable<MetaInfo> body)
            => source.Count == 0 ? null
            : source.Count == 1 ? body.First()
            : new MetaInfo(groupName, body);
    }

    #endregion

    #region MetaItem

    [DebuggerDisplay("{Name}, items: {Items.Count}")]
    public class MetaItem(object source, string name, object icon, object tag = null, PakFile pakFile = null, List<MetaItem> items = null)
    {
        [DebuggerDisplay("{Name}")]
        public class Filter(string name, string description = "")
        {
            public string Name = name;
            public string Description = description;

            public override string ToString() => Name;
        }

        public object Source { get; } = source;
        public string Name { get; } = name;
        public object Icon { get; } = icon;
        public object Tag { get; } = tag;
        public PakFile PakFile { get; } = pakFile;
        public List<MetaItem> Items { get; private set; } = items ?? [];

        public MetaItem Search(Func<MetaItem, bool> predicate)
        {
            // if node is a leaf
            if (Items == null || Items.Count == 0) return predicate(this) ? this : null;
            // otherwise if node is not a leaf
            else
            {
                var results = Items.Select(i => i.Search(predicate)).Where(i => i != null).ToList();
                if (results.Any())
                {
                    var result = (MetaItem)MemberwiseClone();
                    result.Items = results;
                    return result;
                }
                return null;
            }
        }

        public MetaItem FindByPath(string path, MetaManager manager)
        {
            var paths = path.Split(['\\', '/', ':'], 2);
            var node = Items.FirstOrDefault(x => x.Name == paths[0]);
            (node?.Source as FileSource)?.Pak?.Open(node.Items, manager);
            return node == null || paths.Length == 1 ? node : node.FindByPath(paths[1], manager);
        }

        public static MetaItem FindByPathForNodes(List<MetaItem> nodes, string path, MetaManager manager)
        {
            var paths = path.Split(['\\', '/', ':'], 2);
            var node = nodes.FirstOrDefault(x => x.Name == paths[0]);
            (node?.Source as FileSource)?.Pak?.Open(node.Items, manager);
            return node == null || paths.Length == 1 ? node : node.FindByPath(paths[1], manager);
        }
    }

    #endregion

    #region IHaveMetaInfo

    public interface IHaveMetaInfo
    {
        List<MetaInfo> GetInfoNodes(MetaManager resource = null, FileSource file = null, object tag = null);
    }

    #endregion

    #region MetaManager

    public abstract class MetaManager
    {
        public abstract object FolderIcon { get; }
        public abstract object PackageIcon { get; }
        public abstract object GetIcon(string name);
        public abstract object GetImage(string name);

        /// <summary>
        /// Gets the string or bytes.
        /// </summary>
        /// <param name="stream">The stream.</param>
        /// <param name="stream">The stream.</param>
        /// <param name="dispose">The dispose.</param>
        /// <returns></returns>
        public static object GuessStringOrBytes(Stream stream, bool dispose = true)
        {
            using var ms = new MemoryStream();
            stream.Position = 0;
            stream.CopyTo(ms);
            var bytes = ms.ToArray();
            if (dispose) stream.Dispose();
            return !bytes.Contains<byte>(0x00)
                ? Encoding.UTF8.GetString(bytes)
                : bytes;
        }

        /// <summary>
        /// Gets the explorer information nodes.
        /// </summary>
        /// <param name="manager">The manager.</param>
        /// <param name="pakFile">The pak file.</param>
        /// <param name="file">The file.</param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static async Task<List<MetaInfo>> GetMetaInfos(MetaManager manager, BinaryPakFile pakFile, FileSource file)
        {
            List<MetaInfo> nodes = null;
            var obj = await pakFile.LoadFileObject<object>(file);
            if (obj == null) return null;
            else if (obj is IHaveMetaInfo info) nodes = info.GetInfoNodes(manager, file);
            else if (obj is Stream stream)
            {
                var value = GuessStringOrBytes(stream);
                nodes = value is string text ? [
                    new(null, new MetaContent { Type = "Text", Name = "Text", Value = text }),
                    new("Text", items: [
                        new($"Length: {text.Length}"),
                    ])]
                : value is byte[] bytes ? [
                    new(null, new MetaContent { Type = "Hex", Name = "Hex", Value = new MemoryStream(bytes) }),
                    new("Bytes", items: [
                        new($"Length: {bytes.Length}"),
                    ])]
                : throw new ArgumentOutOfRangeException(nameof(value), value.GetType().Name);
            }
            else if (obj is IDisposable disposable) disposable.Dispose();
            if (nodes == null) return null;
            nodes.Add(new MetaInfo("File", items: [
                new($"Path: {file.Path}"),
                new($"FileSize: {file.FileSize}"),
                file.Parts != null
                    ? new MetaInfo("Parts", items: file.Parts.Select(part => new MetaInfo($"{part.FileSize}@{part.Path}")))
                    : null
            ]));
            //nodes.Add(new MetaInfo(null, new MetaContent { Type = "Hex", Name = "TEST", Value = new MemoryStream() }));
            //nodes.Add(new MetaInfo(null, new MetaContent { Type = "Image", Name = "TEST", MaxWidth = 500, MaxHeight = 500, Value = null }));
            return nodes;
        }

        /// <summary>
        /// Gets the meta items.
        /// </summary>
        /// <param name="manager">The manager.</param>
        /// <param name="pakFile">The pak file.</param>
        /// <returns></returns>
        public static List<MetaItem> GetMetaItems(MetaManager manager, BinaryPakFile pakFile)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));

            var root = new List<MetaItem>();
            if (pakFile.Files == null || pakFile.Files.Count == 0) return root;
            string currentPath = null; List<MetaItem> currentFolder = null;

            // parse paths
            foreach (var file in pakFile.Files.OrderBy(x => x.Path))
            {
                // next path, skip empty
                var path = file.Path[pakFile.PathSkip..];
                if (string.IsNullOrEmpty(path)) continue;

                // folder
                var fileFolder = Path.GetDirectoryName(path);
                if (currentPath != fileFolder)
                {
                    currentPath = fileFolder;
                    currentFolder = root;
                    if (!string.IsNullOrEmpty(fileFolder))
                        foreach (var folder in fileFolder.Split('\\'))
                        {
                            var found = currentFolder.Find(x => x.Name == folder && x.PakFile == null);
                            if (found != null) currentFolder = found.Items;
                            else
                            {
                                found = new MetaItem(null, folder, manager.FolderIcon);
                                currentFolder.Add(found);
                                currentFolder = found.Items;
                            }
                        }
                }

                // pakfile
                if (file.Pak != null)
                {
                    var items = GetMetaItems(manager, file.Pak);
                    currentFolder.Add(new MetaItem(file, Path.GetFileName(file.Path), manager.PackageIcon, pakFile: pakFile, items: items));
                    continue;
                }

                // file
                var fileName = Path.GetFileName(path);
                var fileNameForIcon = pakFile.FileMask?.Invoke(fileName) ?? fileName;
                var extentionForIcon = Path.GetExtension(fileNameForIcon);
                if (extentionForIcon.Length > 0) extentionForIcon = extentionForIcon[1..];
                currentFolder.Add(new MetaItem(file, fileName, manager.GetIcon(extentionForIcon), pakFile: pakFile));
            }
            return root;
        }
    }

    #endregion
}

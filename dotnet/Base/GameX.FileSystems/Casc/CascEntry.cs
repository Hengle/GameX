using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GameX.FileSystems.Casc
{
    public interface ICascEntry
    {
        string Name { get; }
        ulong Hash { get; }
        int CompareTo(ICascEntry entry, int col, CascHandler casc);
    }

    public class CascFolder : ICascEntry
    {
        public Dictionary<string, CascFile> Files { get; set; }
        public Dictionary<string, CascFolder> Folders { get; set; }

        public CascFolder(string name)
        {
            Files = new Dictionary<string, CascFile>(StringComparer.OrdinalIgnoreCase);
            Folders = new Dictionary<string, CascFolder>(StringComparer.OrdinalIgnoreCase);
            Name = name;
        }

        public string Name { get; private set; }

        public ulong Hash => 0;

        public CascFile GetFile(string name)
        {
            Files.TryGetValue(name, out CascFile entry);
            return entry;
        }

        public CascFolder GetFolder(string name)
        {
            Folders.TryGetValue(name, out CascFolder entry);
            return entry;
        }

        public static IEnumerable<CascFile> GetFiles(IEnumerable<ICascEntry> entries, IEnumerable<int> selection = null, bool recursive = true)
        {
            var entries2 = selection != null ? selection.Select(index => entries.ElementAt(index)) : entries;

            foreach (var entry in entries2)
            {
                if (entry is CascFile file1)
                {
                    yield return file1;
                }
                else
                {
                    if (recursive)
                    {
                        var folder = entry as CascFolder;

                        foreach (var file in GetFiles(folder.Files.Select(kv => kv.Value)))
                        {
                            yield return file;
                        }
                        foreach (var file in GetFiles(folder.Folders.Select(kv => kv.Value)))
                        {
                            yield return file;
                        }
                    }
                }
            }
        }

        public int CompareTo(ICascEntry other, int col, CascHandler casc)
        {
            int result = 0;

            if (other is CascFile)
                return -1;

            switch (col)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                    result = Name.CompareTo(other.Name);
                    break;
                case 4:
                    break;
            }

            return result;
        }
    }

    public class CascFile : ICascEntry
    {
        public CascFile(ulong hash, string fullname)
        {
            Hash = hash;
            FullName = fullname;
        }

        public string Name => Path.GetFileName(FullName);

        public string FullName { get; set; }

        public ulong Hash { get; private set; }

        public long GetSize(CascHandler casc) => casc.GetFileSize(Hash);

        public int CompareTo(ICascEntry other, int col, CascHandler casc)
        {
            int result = 0;

            if (other is CascFolder)
                return 1;

            switch (col)
            {
                case 0:
                    result = Name.CompareTo(other.Name);
                    break;
                case 1:
                    result = Path.GetExtension(Name).CompareTo(Path.GetExtension(other.Name));
                    break;
                case 2:
                    {
                        var e1 = casc.Root.GetEntries(Hash);
                        var e2 = casc.Root.GetEntries(other.Hash);
                        var flags1 = e1.Any() ? e1.First().LocaleFlags : LocaleFlags.None;
                        var flags2 = e2.Any() ? e2.First().LocaleFlags : LocaleFlags.None;
                        result = flags1.CompareTo(flags2);
                    }
                    break;
                case 3:
                    {
                        var e1 = casc.Root.GetEntries(Hash);
                        var e2 = casc.Root.GetEntries(other.Hash);
                        var flags1 = e1.Any() ? e1.First().ContentFlags : ContentFlags.None;
                        var flags2 = e2.Any() ? e2.First().ContentFlags : ContentFlags.None;
                        result = flags1.CompareTo(flags2);
                    }
                    break;
                case 4:
                    var size1 = GetSize(casc);
                    var size2 = (other as CascFile).GetSize(casc);

                    if (size1 == size2)
                        result = 0;
                    else
                        result = size1 < size2 ? -1 : 1;
                    break;
            }

            return result;
        }

        public static readonly Dictionary<ulong, CascFile> Files = new Dictionary<ulong, CascFile>();
    }
}

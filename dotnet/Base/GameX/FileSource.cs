using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace GameX
{
    [DebuggerDisplay("{Path}")]
    public class FileSource
    {
        internal static readonly Func<BinaryReader, FileSource, PakFile, Task<object>> EmptyObjectFactory = (a, b, c) => null;
        
        // common
        public int Id;
        public string Path;
        public long Offset;
        public long FileSize;
        public long PackedSize;
        public int Compressed;
        public int Flags;
        public ulong Hash;
        public BinaryPakFile Pak;
        public IList<FileSource> Parts;
        public object Tag;
        // extra
        public byte[] Data;
        public object ExtraArgs;
        // cached
        internal Func<BinaryReader, FileSource, PakFile, Task<object>> CachedObjectFactory;
        internal FileOption CachedObjectOption;
    }
}
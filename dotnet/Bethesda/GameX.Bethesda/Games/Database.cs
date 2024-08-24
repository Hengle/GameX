using GameX.Formats;

namespace GameX.Bethesda
{
    public class Database(PakFile source)
    {
        public readonly BinaryPakFile Source = source as BinaryPakFile;

        public override string ToString() => Source.Name;

        //public ConcurrentDictionary<uint, FileType> FileCache { get; } = new ConcurrentDictionary<uint, FileType>();
    }
}

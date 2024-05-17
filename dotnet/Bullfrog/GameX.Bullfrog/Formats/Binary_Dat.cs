using GameX.Meta;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    public unsafe class Binary_Dat : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Dat(r, Path.GetFileName(f.Path), (int)f.FileSize));

        public Binary_Dat(BinaryReader r, string filename, int fileSize)
        {
            Data = Rnc.Unpack(r);
        }

        byte[] Data;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo>
        {
            new MetaInfo(null, new MetaContent { Type = "Hex", Name = Path.GetFileName(file.Path), Value = new MemoryStream(Data), Tag = Path.GetExtension(file.Path) }),
        };
    }
}

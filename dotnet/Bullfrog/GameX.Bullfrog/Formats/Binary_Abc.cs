using GameX.Formats;
using GameX.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    public class Binary_Dat : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Dat(r, (int)f.FileSize));

        public Binary_Dat(BinaryReader r, int fileSize)
        {
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            //new MetaInfo(null, new MetaContent { Type = "Data", Name = Path.GetFileName(file.Path), Value = new MemoryStream(Data), Tag = Path.GetExtension(file.Path) }),
        };
    }
}

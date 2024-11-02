using GameX.Formats;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GameX.Volition.Formats
{
    public class Binary_Abc : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Abc(r));

        public Binary_Abc(BinaryReader r) { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = this }),
            new MetaInfo($"{nameof(Binary_Abc)}", items: new List<MetaInfo> {
                //new MetaInfo($"abc: {abc}"),
            })
        };
    }
}

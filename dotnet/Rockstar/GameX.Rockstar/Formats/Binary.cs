using GameX.Formats;
using System;
using System.Collections.Generic;
using System.IO;

namespace GameX.Rockstar.Formats
{
    #region Binary_Abc

    public class Binary_Abc : IHaveMetaInfo
    {
        public Binary_Abc(BinaryReader r)
        {
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new("BinaryPak", items: [
                //new($"Type: {Type}"),
            ])
        ];
    }

    #endregion
}

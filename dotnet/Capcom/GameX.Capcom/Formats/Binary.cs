using GameX.Formats;
using System;
using System.Collections.Generic;
using System.IO;

namespace GameX.Capcom.Formats
{
    #region Binary_Abc

    public class Binary_Abc : IHaveMetaInfo
    {
        public Binary_Abc(BinaryReader r)
        {
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new("BinaryPak", items: [
                //new MetaInfo($"Type: {Type}"),
            ])
        ];
    }

    #endregion

    #region Binary_Tex

    public class Binary_Tex : IHaveMetaInfo
    {
        public Binary_Tex(BinaryReader r)
        {
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new("BinaryPak", items: [
                //new MetaInfo($"Type: {Type}"),
            ])
        ];
    }

    #endregion
}

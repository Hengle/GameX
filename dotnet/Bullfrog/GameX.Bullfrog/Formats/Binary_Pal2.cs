using GameX.Formats;
using GameX.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    /*
    public unsafe class Binary_Pal2x : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Pal2x(r));

        #region Palette

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct RGB { public byte R; public byte G; public byte B; }

        public byte Bpp;
        public byte[][] Records;

        public Binary_Pal2x ConvertVgaPalette()
        {
            for (var i = 0; i < 256; i++)
            {
                var p = Records[i];
                p[0] = (byte)((p[0] << 2) | (p[0] >> 4));
                p[1] = (byte)((p[1] << 2) | (p[1] >> 4));
                p[2] = (byte)((p[2] << 2) | (p[2] >> 4));
            }
            return this;
        }

        #endregion

        public Binary_Pal2x(BinaryReader r)
        {
            using var r2 = new BinaryReader(new MemoryStream(Rnc.Read(r)));
            var numRecords = (int)(r2.BaseStream.Length / 3);
            Records = r2.ReadPArray<RGB>("3B", numRecords).Select(s => new[] { s.R, s.G, s.B, (byte)255 }).ToArray();
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => [
                new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "Pallet" }),
                new("Pallet", items: [
                    new($"Records: {Records.Length}"),
                ])
            ];
    }
    */
}
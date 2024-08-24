using System.Collections.Generic;
using System.IO;
using static System.IO.Polyfill;

namespace GameX.Bethesda.Formats.Records
{
    public class TES3Record : Record
    {
        public struct HEDRField(BinaryReader r, int dataSize)
        {
            public float Version = r.ReadSingle();
            public uint FileType = r.ReadUInt32();
            public string CompanyName = r.ReadZString(32);
            public string FileDescription = r.ReadZString(256);
            public uint NumRecords = r.ReadUInt32();
        }

        public HEDRField HEDR;
        public List<STRVField> MASTs;
        public List<INTVField> DATAs;

        public override bool CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize)
        {
            switch (type)
            {
                case "HEDR": HEDR = new HEDRField(r, dataSize); return true;
                case "MAST": MASTs ??= []; MASTs.Add(r.ReadSTRV(dataSize)); return true;
                case "DATA": DATAs ??= []; DATAs.Add(r.ReadINTV(dataSize)); return true;
                default: return false;
            }
        }
    }
}
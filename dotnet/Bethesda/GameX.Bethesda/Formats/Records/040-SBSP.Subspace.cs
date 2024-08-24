using System.IO;

namespace GameX.Bethesda.Formats.Records
{
    public class SBSPRecord : Record
    {
        public struct DNAMField(BinaryReader r, int dataSize)
        {
            public float X = r.ReadSingle(); // X dimension
            public float Y = r.ReadSingle(); // Y dimension
            public float Z = r.ReadSingle(); // Z dimension
        }

        public override string ToString() => $"SBSP: {EDID.Value}";
        public STRVField EDID { get; set; } // Editor ID
        public DNAMField DNAM;

        public override bool CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize)
        {
            switch (type)
            {
                case "EDID": EDID = r.ReadSTRV(dataSize); return true;
                case "DNAM": DNAM = new DNAMField(r, dataSize); return true;
                default: return false;
            }
        }
    }
}
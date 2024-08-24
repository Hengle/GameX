using System.Collections.Generic;
using System.IO;

namespace GameX.Bethesda.Formats.Records
{
    public class LTEXRecord : Record, IHaveEDID
    {
        public struct HNAMField(BinaryReader r, int dataSize)
        {
            public byte MaterialType = r.ReadByte();
            public byte Friction = r.ReadByte();
            public byte Restitution = r.ReadByte();
        }

        public override string ToString() => $"LTEX: {EDID.Value}";
        public STRVField EDID { get; set; } // Editor ID
        public FILEField ICON; // Texture
        // TES3
        public INTVField INTV;
        // TES4
        public HNAMField HNAM; // Havok data
        public BYTEField SNAM; // Texture specular exponent
        public List<FMIDField<GRASRecord>> GNAMs = []; // Potential grass

        public override bool CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize)
        {
            switch (type)
            {
                case "EDID":
                case "NAME": EDID = r.ReadSTRV(dataSize); return true;
                case "INTV": INTV = r.ReadINTV(dataSize); return true;
                case "ICON":
                case "DATA": ICON = r.ReadFILE(dataSize); return true;
                // TES4
                case "HNAM": HNAM = new HNAMField(r, dataSize); return true;
                case "SNAM": SNAM = r.ReadT<BYTEField>(dataSize); return true;
                case "GNAM": GNAMs.Add(new FMIDField<GRASRecord>(r, dataSize)); return true;
                default: return false;
            }
        }
    }
}
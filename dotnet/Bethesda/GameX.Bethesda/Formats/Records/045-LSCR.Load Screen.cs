using System.Collections.Generic;
using System.IO;

namespace GameX.Bethesda.Formats.Records
{
    public class LSCRRecord : Record
    {
        public struct LNAMField(BinaryReader r, int dataSize)
        {
            public FormId<Record> Direct = new FormId<Record>(r.ReadUInt32());
            public FormId<WRLDRecord> IndirectWorld = new FormId<WRLDRecord>(r.ReadUInt32());
            public short IndirectGridX = r.ReadInt16();
            public short IndirectGridY = r.ReadInt16();
        }

        public override string ToString() => $"LSCR: {EDID.Value}";
        public STRVField EDID { get; set; } // Editor ID
        public FILEField ICON; // Icon
        public STRVField DESC; // Description
        public List<LNAMField> LNAMs; // LoadForm

        public override bool CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize)
        {
            switch (type)
            {
                case "EDID": EDID = r.ReadSTRV(dataSize); return true;
                case "ICON": ICON = r.ReadFILE(dataSize); return true;
                case "DESC": DESC = r.ReadSTRV(dataSize); return true;
                case "LNAM": LNAMs ??= []; LNAMs.Add(new LNAMField(r, dataSize)); return true;
                default: return false;
            }
        }
    }
}
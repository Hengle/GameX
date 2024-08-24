using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace GameX.Bethesda.Formats.Records
{
    public class REFRRecord : Record
    {
        public struct XTELField(BinaryReader r, int dataSize)
        {
            public FormId<REFRRecord> Door = new(r.ReadUInt32());
            public Vector3 Position = new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            public Vector3 Rotation = new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        public struct DATAField(BinaryReader r, int dataSize)
        {
            public Vector3 Position = new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            public Vector3 Rotation = new(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        }

        public struct XLOCField
        {
            public override readonly string ToString() => $"{Key}";
            public byte LockLevel;
            public FormId<KEYMRecord> Key;
            public byte Flags;

            public XLOCField(BinaryReader r, int dataSize)
            {
                LockLevel = r.ReadByte();
                r.Skip(3); // Unused
                Key = new FormId<KEYMRecord>(r.ReadUInt32());
                if (dataSize == 16) r.Skip(4); // Unused
                Flags = r.ReadByte();
                r.Skip(3); // Unused
            }
        }

        public struct XESPField
        {
            public override readonly string ToString() => $"{Reference}";
            public FormId<Record> Reference;
            public byte Flags;

            public XESPField(BinaryReader r, int dataSize)
            {
                Reference = new FormId<Record>(r.ReadUInt32());
                Flags = r.ReadByte();
                r.Skip(3); // Unused
            }
        }

        public struct XSEDField
        {
            public override readonly string ToString() => $"{Seed}";
            public byte Seed;

            public XSEDField(BinaryReader r, int dataSize)
            {
                Seed = r.ReadByte();
                if (dataSize == 4) r.Skip(3); // Unused
            }
        }

        public class XMRKGroup
        {
            public override string ToString() => $"{FULL.Value}";
            public BYTEField FNAM; // Map Flags
            public STRVField FULL; // Name
            public BYTEField TNAM; // Type
        }

        public override string ToString() => $"REFR: {EDID.Value}";
        public STRVField EDID { get; set; } // Editor ID
        public FMIDField<Record> NAME; // Base
        public XTELField? XTEL; // Teleport Destination (optional)
        public DATAField DATA; // Position/Rotation
        public XLOCField? XLOC; // Lock information (optional)
        public List<CELLRecord.XOWNGroup> XOWNs; // Ownership (optional)
        public XESPField? XESP; // Enable Parent (optional)
        public FMIDField<Record>? XTRG; // Target (optional)
        public XSEDField? XSED; // SpeedTree (optional)
        public BYTVField? XLOD; // Distant LOD Data (optional)
        public FLTVField? XCHG; // Charge (optional)
        public FLTVField? XHLT; // Health (optional)
        public FMIDField<CELLRecord>? XPCI; // Unused (optional)
        public IN32Field? XLCM; // Level Modifier (optional)
        public FMIDField<REFRRecord>? XRTM; // Unknown (optional)
        public UI32Field? XACT; // Action Flag (optional)
        public IN32Field? XCNT; // Count (optional)
        public List<XMRKGroup> XMRKs; // Ownership (optional)
        //public bool? ONAM; // Open by Default
        public BYTVField? XRGD; // Ragdoll Data (optional)
        public FLTVField? XSCL; // Scale (optional)
        public BYTEField? XSOL; // Contained Soul (optional)
        int _nextFull;

        public override bool CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize)
        {
            switch (type)
            {
                case "EDID": EDID = r.ReadSTRV(dataSize); return true;
                case "NAME": NAME = new FMIDField<Record>(r, dataSize); return true;
                case "XTEL": XTEL = new XTELField(r, dataSize); return true;
                case "DATA": DATA = new DATAField(r, dataSize); return true;
                case "XLOC": XLOC = new XLOCField(r, dataSize); return true;
                case "XOWN": XOWNs ??= []; XOWNs.Add(new CELLRecord.XOWNGroup { XOWN = new FMIDField<Record>(r, dataSize) }); return true;
                case "XRNK": XOWNs.Last().XRNK = r.ReadSAndVerify<IN32Field>(dataSize); return true;
                case "XGLB": XOWNs.Last().XGLB = new FMIDField<Record>(r, dataSize); return true;
                case "XESP": XESP = new XESPField(r, dataSize); return true;
                case "XTRG": XTRG = new FMIDField<Record>(r, dataSize); return true;
                case "XSED": XSED = new XSEDField(r, dataSize); return true;
                case "XLOD": XLOD = r.ReadBYTV(dataSize); return true;
                case "XCHG": XCHG = r.ReadSAndVerify<FLTVField>(dataSize); return true;
                case "XHLT": XCHG = r.ReadSAndVerify<FLTVField>(dataSize); return true;
                case "XPCI": XPCI = new FMIDField<CELLRecord>(r, dataSize); _nextFull = 1; return true;
                case "FULL":
                    if (_nextFull == 1) XPCI.Value.AddName(r.ReadFString(dataSize));
                    else if (_nextFull == 2) XMRKs.Last().FULL = r.ReadSTRV(dataSize);
                    _nextFull = 0;
                    return true;
                case "XLCM": XLCM = r.ReadSAndVerify<IN32Field>(dataSize); return true;
                case "XRTM": XRTM = new FMIDField<REFRRecord>(r, dataSize); return true;
                case "XACT": XACT = r.ReadSAndVerify<UI32Field>(dataSize); return true;
                case "XCNT": XCNT = r.ReadSAndVerify<IN32Field>(dataSize); return true;
                case "XMRK": if (XMRKs == null) XMRKs = new List<XMRKGroup>(); XMRKs.Add(new XMRKGroup()); _nextFull = 2; return true;
                case "FNAM": XMRKs.Last().FNAM = r.ReadSAndVerify<BYTEField>(dataSize); return true;
                case "TNAM": XMRKs.Last().TNAM = r.ReadSAndVerify<BYTEField>(dataSize); r.ReadByte(); return true;
                case "ONAM": return true;
                case "XRGD": XRGD = r.ReadBYTV(dataSize); return true;
                case "XSCL": XSCL = r.ReadSAndVerify<FLTVField>(dataSize); return true;
                case "XSOL": XSOL = r.ReadSAndVerify<BYTEField>(dataSize); return true;
                default: return false;
            }
        }
    }
}
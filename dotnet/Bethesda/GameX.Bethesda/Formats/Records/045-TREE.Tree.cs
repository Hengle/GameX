using System.IO;

namespace GameX.Bethesda.Formats.Records
{
    public class TREERecord : Record, IHaveEDID, IHaveMODL
    {
        public struct SNAMField
        {
            public int[] Values;

            public SNAMField(BinaryReader r, int dataSize)
            {
                Values = new int[dataSize >> 2];
                for (var i = 0; i < Values.Length; i++)
                    Values[i] = r.ReadInt32();
            }
        }

        public struct CNAMField(BinaryReader r, int dataSize)
        {
            public float LeafCurvature = r.ReadSingle();
            public float MinimumLeafAngle = r.ReadSingle();
            public float MaximumLeafAngle = r.ReadSingle();
            public float BranchDimmingValue = r.ReadSingle();
            public float LeafDimmingValue = r.ReadSingle();
            public int ShadowRadius = r.ReadInt32();
            public float RockSpeed = r.ReadSingle();
            public float RustleSpeed = r.ReadSingle();
        }

        public struct BNAMField(BinaryReader r, int dataSize)
        {
            public float Width = r.ReadSingle();
            public float Height = r.ReadSingle();
        }

        public override string ToString() => $"TREE: {EDID.Value}";
        public STRVField EDID { get; set; } // Editor ID
        public MODLGroup MODL { get; set; } // Model
        public FILEField ICON; // Leaf Texture
        public SNAMField SNAM; // SpeedTree Seeds, array of ints
        public CNAMField CNAM; // Tree Parameters
        public BNAMField BNAM; // Billboard Dimensions

        public override bool CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize)
        {
            switch (type)
            {
                case "EDID": EDID = r.ReadSTRV(dataSize); return true;
                case "MODL": MODL = new MODLGroup(r, dataSize); return true;
                case "MODB": MODL.MODBField(r, dataSize); return true;
                case "MODT": MODL.MODTField(r, dataSize); return true;
                case "ICON": ICON = r.ReadFILE(dataSize); return true;
                case "SNAM": SNAM = new SNAMField(r, dataSize); return true;
                case "CNAM": CNAM = new CNAMField(r, dataSize); return true;
                case "BNAM": BNAM = new BNAMField(r, dataSize); return true;
                default: return false;
            }
        }
    }
}
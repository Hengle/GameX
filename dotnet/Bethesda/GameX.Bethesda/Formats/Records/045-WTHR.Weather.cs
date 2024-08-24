using System.Collections.Generic;
using System.IO;

namespace GameX.Bethesda.Formats.Records
{
    public class WTHRRecord : Record, IHaveEDID, IHaveMODL
    {
        public struct FNAMField(BinaryReader r, int dataSize)
        {
            public float DayNear = r.ReadSingle();
            public float DayFar = r.ReadSingle();
            public float NightNear = r.ReadSingle();
            public float NightFar = r.ReadSingle();
        }

        public struct HNAMField(BinaryReader r, int dataSize)
        {
            public float EyeAdaptSpeed = r.ReadSingle();
            public float BlurRadius = r.ReadSingle();
            public float BlurPasses = r.ReadSingle();
            public float EmissiveMult = r.ReadSingle();
            public float TargetLUM = r.ReadSingle();
            public float UpperLUMClamp = r.ReadSingle();
            public float BrightScale = r.ReadSingle();
            public float BrightClamp = r.ReadSingle();
            public float LUMRampNoTex = r.ReadSingle();
            public float LUMRampMin = r.ReadSingle();
            public float LUMRampMax = r.ReadSingle();
            public float SunlightDimmer = r.ReadSingle();
            public float GrassDimmer = r.ReadSingle();
            public float TreeDimmer = r.ReadSingle();
        }

        public struct DATAField
        {
            public byte WindSpeed;
            public byte CloudSpeed_Lower;
            public byte CloudSpeed_Upper;
            public byte TransDelta;
            public byte SunGlare;
            public byte SunDamage;
            public byte Precipitation_BeginFadeIn;
            public byte Precipitation_EndFadeOut;
            public byte ThunderLightning_BeginFadeIn;
            public byte ThunderLightning_EndFadeOut;
            public byte ThunderLightning_Frequency;
            public byte WeatherClassification;
            public ColorRef4 LightningColor;

            public DATAField(BinaryReader r, int dataSize)
            {
                WindSpeed = r.ReadByte();
                CloudSpeed_Lower = r.ReadByte();
                CloudSpeed_Upper = r.ReadByte();
                TransDelta = r.ReadByte();
                SunGlare = r.ReadByte();
                SunDamage = r.ReadByte();
                Precipitation_BeginFadeIn = r.ReadByte();
                Precipitation_EndFadeOut = r.ReadByte();
                ThunderLightning_BeginFadeIn = r.ReadByte();
                ThunderLightning_EndFadeOut = r.ReadByte();
                ThunderLightning_Frequency = r.ReadByte();
                WeatherClassification = r.ReadByte();
                LightningColor = new ColorRef4 { Red = r.ReadByte(), Green = r.ReadByte(), Blue = r.ReadByte(), Null = 255 };
            }
        }

        public struct SNAMField(BinaryReader r, int dataSize)
        {
            public FormId<SOUNRecord> Sound = new FormId<SOUNRecord>(r.ReadUInt32()); // Sound FormId
            public uint Type = r.ReadUInt32(); // Sound Type - 0=Default, 1=Precipitation, 2=Wind, 3=Thunder
        }

        public override string ToString() => $"WTHR: {EDID.Value}";
        public STRVField EDID { get; set; } // Editor ID
        public MODLGroup MODL { get; set; } // Model
        public FILEField CNAM; // Lower Cloud Layer
        public FILEField DNAM; // Upper Cloud Layer
        public BYTVField NAM0; // Colors by Types/Times
        public FNAMField FNAM; // Fog Distance
        public HNAMField HNAM; // HDR Data
        public DATAField DATA; // Weather Data
        public List<SNAMField> SNAMs = []; // Sounds

        public override bool CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize)
        {
            switch (type)
            {
                case "EDID": EDID = r.ReadSTRV(dataSize); return true;
                case "MODL": MODL = new MODLGroup(r, dataSize); return true;
                case "MODB": MODL.MODBField(r, dataSize); return true;
                case "CNAM": CNAM = r.ReadFILE(dataSize); return true;
                case "DNAM": DNAM = r.ReadFILE(dataSize); return true;
                case "NAM0": NAM0 = r.ReadBYTV(dataSize); return true;
                case "FNAM": FNAM = new FNAMField(r, dataSize); return true;
                case "HNAM": HNAM = new HNAMField(r, dataSize); return true;
                case "DATA": DATA = new DATAField(r, dataSize); return true;
                case "SNAM": SNAMs.Add(new SNAMField(r, dataSize)); return true;
                default: return false;
            }
        }
    }
}
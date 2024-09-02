using GameX.Formats;
using OpenStack.Gfx;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using static OpenStack.Debug;
using static System.IO.Polyfill;

namespace GameX.Bethesda.Formats.Records
{
    #region Base

    public interface IHaveMODL
    {
        MODLGroup MODL { get; }
    }

    public class FieldHeader(BinaryReader r, BethesdaFormat format)
    {
        public override string ToString() => Type;
        public string Type = r.ReadFString(4); // 4 bytes
        public int DataSize = (int)(format == BethesdaFormat.TES3 ? r.ReadUInt32() : r.ReadUInt16());
    }

    public class Record : IRecord
    {
        public static readonly Record Empty = new();
        public override string ToString() => $"XXXX: {EDID.Value}";
        internal Header Header;
        public uint Id => Header.FormId;
        public STRVField EDID;  // Editor ID

        /// <summary>
        /// Return an uninitialized subrecord to deserialize, or null to skip.
        /// </summary>
        /// <returns>Return an uninitialized subrecord to deserialize, or null to skip.</returns>
        public virtual object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => Empty;

        public void Read(BinaryReader r, string filePath, BethesdaFormat format)
        {
            long startPosition = r.Tell(), endPosition = startPosition + Header.DataSize;
            while (r.BaseStream.Position < endPosition)
            {
                var fieldHeader = new FieldHeader(r, format);
                if (fieldHeader.Type == "XXXX")
                {
                    if (fieldHeader.DataSize != 4) throw new InvalidOperationException();
                    fieldHeader.DataSize = (int)r.ReadUInt32();
                    continue;
                }
                else if (fieldHeader.Type == "OFST" && Header.Type == "WRLD") { r.Seek(endPosition); continue; }
                var position = r.BaseStream.Position;
                if (CreateField(r, format, fieldHeader.Type, fieldHeader.DataSize) == Empty) { Log($"Unsupported ESM record type: {Header.Type}:{fieldHeader.Type}"); r.Skip(fieldHeader.DataSize); continue; }
                // check full read
                if (r.BaseStream.Position != position + fieldHeader.DataSize) throw new FormatException($"Failed reading {Header.Type}:{fieldHeader.Type} field data at offset {position} in {filePath} of {r.BaseStream.Position - position - fieldHeader.DataSize}");
            }
            // check full read
            if (r.Tell() != endPosition) throw new FormatException($"Failed reading {Header.Type} record data at offset {startPosition} in {filePath}");
        }
    }

    #endregion

    #region Base : Extensions

    public static class Extensions
    {
        public static TResult Then<T, TResult>(this Record s, T value, Func<T, TResult> then) => then(value);
        public static T AddX<T>(this IList<T> s, T value) { s.Add(value); return value; }
        public static IEnumerable<T> AddRangeX<T>(this List<T> s, IEnumerable<T> value) { s.AddRange(value); return value; }

        public static INTVField ReadINTV(this BinaryReader r, int length)
            => length switch
            {
                1 => new INTVField { Value = r.ReadByte() },
                2 => new INTVField { Value = r.ReadInt16() },
                4 => new INTVField { Value = r.ReadInt32() },
                8 => new INTVField { Value = r.ReadInt64() },
                _ => throw new NotImplementedException($"Tried to read an INTV subrecord with an unsupported size ({length})"),
            };
        public static DATVField ReadDATV(this BinaryReader r, int length, char type)
            => type switch
            {
                'b' => new DATVField { B = r.ReadInt32() != 0 },
                'i' => new DATVField { I = r.ReadInt32() },
                'f' => new DATVField { F = r.ReadSingle() },
                's' => new DATVField { S = r.ReadYEncoding(length) },
                _ => throw new InvalidOperationException($"{type}"),
            };
        public static STRVField ReadSTRV(this BinaryReader r, int length) => new() { Value = r.ReadYEncoding(length) };
        public static STRVField ReadSTRV_ZPad(this BinaryReader r, int length) => new() { Value = r.ReadZString(length) };
        public static FILEField ReadFILE(this BinaryReader r, int length) => new() { Value = r.ReadYEncoding(length) };
        public static BYTVField ReadBYTV(this BinaryReader r, int length) => new() { Value = r.ReadBytes(length) };
        public static UNKNField ReadUNKN(this BinaryReader r, int length) => new() { Value = r.ReadBytes(length) };
    }

    #endregion

    #region Base : Reference Fields

    public readonly struct FormId32<TRecord> where TRecord : Record
    {
        public override readonly string ToString() => $"{Type}:{Id}";
        public readonly uint Id;
        public readonly string Type => typeof(TRecord).Name[..4];
    }

    public readonly struct FormId<TRecord> where TRecord : Record
    {
        public override string ToString() => $"{Type}:{Name}{Id}";
        public readonly uint Id;
        public readonly string Name;
        public string Type => typeof(TRecord).Name[..4];
        public FormId(uint id) { Id = id; Name = null; }
        public FormId(string name) { Id = 0; Name = name; }
        FormId(uint id, string name) { Id = id; Name = name; }
        public FormId<TRecord> AddName(string name) => new(Id, name);
    }

    public struct FMIDField<TRecord>(BinaryReader r, int dataSize) where TRecord : Record
    {
        public override readonly string ToString() => $"{Value}";
        public FormId<TRecord> Value = dataSize == 4 ? new FormId<TRecord>(r.ReadUInt32()) : new FormId<TRecord>(r.ReadZString(dataSize));
        public object AddName(string name) => Value = Value.AddName(name);
    }

    public struct FMID2Field<TRecord>(BinaryReader r, int dataSize) where TRecord : Record
    {
        public override readonly string ToString() => $"{Value1}x{Value2}";
        public FormId<TRecord> Value1 = new(r.ReadUInt32());
        public FormId<TRecord> Value2 = new(r.ReadUInt32());
    }

    #endregion

    #region Base : Standard Fields

    public class MODLGroup(BinaryReader r, int dataSize)
    {
        public override string ToString() => $"{Value}";
        public string Value = r.ReadYEncoding(dataSize);
        public float Bound;
        public byte[] Textures; // Texture Files Hashes
        public object MODBField(BinaryReader r, int dataSize) => Bound = r.ReadSingle();
        public object MODTField(BinaryReader r, int dataSize) => Textures = r.ReadBytes(dataSize);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ColorRef3 { public override readonly string ToString() => $"{Red}:{Green}:{Blue}"; public static (string, int) Struct = ("<3c", 3); public byte Red; public byte Green; public byte Blue; }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ColorRef4 { public override readonly string ToString() => $"{Red}:{Green}:{Blue}"; public static (string, int) Struct = ("<4c", 4); public byte Red; public byte Green; public byte Blue; public byte Null; public GXColor32 AsColor32 => new(Red, Green, Blue, 255); }

    public struct STRVField { public override readonly string ToString() => Value; public string Value; }
    public struct FILEField { public override readonly string ToString() => Value; public string Value; }
    public struct INTVField { public override readonly string ToString() => $"{Value}"; public static (string, int) Struct = ("<q", 8); public long Value; public UI16Field AsUI16Field => new() { Value = (ushort)Value }; }
    public struct DATVField { public override readonly string ToString() => "DATV"; public bool B; public int I; public float F; public string S; }
    public struct FLTVField { public override readonly string ToString() => $"{Value}"; public static (string, int) Struct = ("<f", 4); public float Value; }
    public struct BYTEField { public override readonly string ToString() => $"{Value}"; public static (string, int) Struct = ("<c", 1); public byte Value; }
    public struct IN16Field { public override readonly string ToString() => $"{Value}"; public static (string, int) Struct = ("<h", 2); public short Value; }
    public struct UI16Field { public override readonly string ToString() => $"{Value}"; public static (string, int) Struct = ("<H", 2); public ushort Value; }
    public struct IN32Field { public override readonly string ToString() => $"{Value}"; public static (string, int) Struct = ("<i", 4); public int Value; }
    public struct UI32Field { public override readonly string ToString() => $"{Value}"; public static (string, int) Struct = ("<I", 4); public uint Value; }
    public struct CREFField { public override readonly string ToString() => $"{Color}"; public static (string, int) Struct = ("<4c", 4); public ColorRef4 Color; }
    public struct CNTOField
    {
        public override readonly string ToString() => $"{Item}";
        public uint ItemCount; // Number of the item
        public FormId<Record> Item; // The ID of the item
        public CNTOField(BinaryReader r, int dataSize, BethesdaFormat format)
        {
            if (format == BethesdaFormat.TES3)
            {
                ItemCount = r.ReadUInt32();
                Item = new FormId<Record>(r.ReadZString(32));
                return;
            }
            Item = new FormId<Record>(r.ReadUInt32());
            ItemCount = r.ReadUInt32();
        }
    }
    public struct BYTVField { public override readonly string ToString() => $"BYTS"; public byte[] Value; }
    public struct UNKNField { public override readonly string ToString() => $"UNKN"; public byte[] Value; }

    #endregion

    #region 0050 : AACT.Action

    public class AACTRecord : Record
    {
        public CREFField CNAME; // RGB color

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "CNAME" => CNAME = r.ReadSAndVerify<CREFField>(size: dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0050 : ADDN-Addon Node

    public class ADDNRecord : Record
    {
        public CREFField CNAME; // RGB color

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "CNAME" => CNAME = r.ReadSAndVerify<CREFField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0050 : ARMA.Armature (Model)

    public class ARMARecord : Record
    {
        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            _ => false,
        };
    }

    #endregion

    #region 0050 : ARTO.Art Object

    public class ARTORecord : Record
    {
        public CREFField CNAME; // RGB color

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "CNAME" => CNAME = r.ReadSAndVerify<CREFField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0050 : ASPC.Acoustic Space

    public class ASPCRecord : Record
    {
        public CREFField CNAME; // RGB color

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "CNAME" => CNAME = r.ReadSAndVerify<CREFField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0050 : ASTP.Association Type

    public class ASTPRecord : Record
    {
        public CREFField CNAME; // RGB color

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "CNAME" => CNAME = r.ReadSAndVerify<CREFField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0050 : AVIF.Actor Values_Perk Tree Graphics

    public class AVIFRecord : Record
    {
        public CREFField CNAME; // RGB color

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "CNAME" => CNAME = r.ReadSAndVerify<CREFField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0050 : DLBR.Dialog Branch

    public class DLBRRecord : Record
    {
        public CREFField CNAME; // RGB color

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "CNAME" => CNAME = r.ReadSAndVerify<CREFField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0050 : DLVW.Dialog View

    public class DLVWRecord : Record
    {
        public CREFField CNAME; // RGB color

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "CNAME" => CNAME = r.ReadSAndVerify<CREFField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0050 : SNDR.Sound Reference

    public class SNDRRecord : Record
    {
        public CREFField CNAME; // RGB color

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "CNAME" => CNAME = r.ReadSAndVerify<CREFField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0400 : ACRE.Placed creature

    public class ACRERecord : Record
    {
        public FMIDField<Record> NAME; // Base
        public REFRRecord.DATAField DATA; // Position/Rotation
        public List<CELLRecord.XOWNGroup> XOWNs; // Ownership (optional)
        public REFRRecord.XESPField? XESP; // Enable Parent (optional)
        public FLTVField XSCL; // Scale (optional)
        public BYTVField? XRGD; // Ragdoll Data (optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "NAME" => NAME = new FMIDField<Record>(r, dataSize),
            "DATA" => DATA = new REFRRecord.DATAField(r, dataSize),
            "XOWN" => (XOWNs ??= []).AddX(new CELLRecord.XOWNGroup { XOWN = new FMIDField<Record>(r, dataSize) }),
            "XRNK" => XOWNs.Last().XRNK = r.ReadSAndVerify<IN32Field>(dataSize),
            "XGLB" => XOWNs.Last().XGLB = new FMIDField<Record>(r, dataSize),
            "XESP" => XESP = new REFRRecord.XESPField(r, dataSize),
            "XSCL" => XSCL = r.ReadSAndVerify<FLTVField>(dataSize),
            "XRGD" => XRGD = r.ReadBYTV(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0400 : HAIR.Hair

    public class HAIRRecord : Record, IHaveMODL
    {
        public STRVField FULL;
        public MODLGroup MODL { get; set; }
        public FILEField ICON;
        public BYTEField DATA; // Playable, Not Male, Not Female, Fixed

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "DATA" => DATA = r.ReadSAndVerify<BYTEField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0400 : KEYM.Key

    public class KEYMRecord : Record, IHaveMODL
    {
        public struct DATAField(BinaryReader r, int dataSize)
        {
            public int Value = r.ReadInt32();
            public float Weight = r.ReadSingle();
        }

        public MODLGroup MODL { get; set; } // Model
        public STRVField FULL; // Item Name
        public FMIDField<SCPTRecord> SCRI; // Script (optional)
        public DATAField DATA; // Type of soul contained in the gem
        public FILEField ICON; // Icon (optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            _ => false,
        };
    }

    #endregion

    #region 0400 : LVLC.Leveled Creature

    public class LVLCRecord : Record
    {
        public BYTEField LVLD; // Chance
        public BYTEField LVLF; // Flags - 0x01 = Calculate from all levels <= player's level, 0x02 = Calculate for each item in count
        public FMIDField<SCPTRecord> SCRI; // Script (optional)
        public FMIDField<CREARecord> TNAM; // Creature Template (optional)
        public List<LVLIRecord.LVLOField> LVLOs = [];

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "LVLD" => LVLD = r.ReadSAndVerify<BYTEField>(dataSize),
            "LVLF" => LVLF = r.ReadSAndVerify<BYTEField>(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "TNAM" => TNAM = new FMIDField<CREARecord>(r, dataSize),
            "LVLO" => LVLOs.AddX(new LVLIRecord.LVLOField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 0400 : LVLI.Leveled Item

    public class LVLIRecord : Record
    {
        public struct LVLOField
        {
            public short Level;
            public FormId<Record> ItemFormId;
            public int Count;

            public LVLOField(BinaryReader r, int dataSize)
            {
                Level = r.ReadInt16();
                r.Skip(2); // Unused
                ItemFormId = new FormId<Record>(r.ReadUInt32());
                if (dataSize == 12)
                {
                    Count = r.ReadInt16();
                    r.Skip(2); // Unused
                }
                else Count = 0;
            }
        }

        public BYTEField LVLD; // Chance
        public BYTEField LVLF; // Flags - 0x01 = Calculate from all levels <= player's level, 0x02 = Calculate for each item in count
        public BYTEField? DATA; // Data (optional)
        public List<LVLOField> LVLOs = [];

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "LVLD" => LVLD = r.ReadSAndVerify<BYTEField>(dataSize),
            "LVLF" => LVLF = r.ReadSAndVerify<BYTEField>(dataSize),
            "DATA" => DATA = r.ReadSAndVerify<BYTEField>(dataSize),
            "LVLO" => LVLOs.AddX(new LVLOField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 0400 : LVSP.Leveled Spell

    public class LVSPRecord : Record
    {
        public BYTEField LVLD; // Chance
        public BYTEField LVLF; // Flags
        public List<LVLIRecord.LVLOField> LVLOs = []; // Number of items in list

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "LVLD" => LVLD = r.ReadSAndVerify<BYTEField>(dataSize),
            "LVLF" => LVLF = r.ReadSAndVerify<BYTEField>(dataSize),
            "LVLO" => LVLOs.AddX(new LVLIRecord.LVLOField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 0400 : ROAD.Road

    public class ROADRecord : Record
    {
        public PGRDRecord.PGRPField[] PGRPs;
        public UNKNField PGRR;

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "PGRP" => PGRPs = [.. Enumerable.Range(0, dataSize >> 4).Select(x => new PGRDRecord.PGRPField(r, dataSize))],
            "PGRR" => PGRR = r.ReadUNKN(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0400 : SBSP.Subspace

    public class SBSPRecord : Record
    {
        public struct DNAMField(BinaryReader r, int dataSize)
        {
            public float X = r.ReadSingle(); // X dimension
            public float Y = r.ReadSingle(); // Y dimension
            public float Z = r.ReadSingle(); // Z dimension
        }

        public DNAMField DNAM;

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "DNAM" => DNAM = new DNAMField(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0400 : SGST.Sigil Stone

    public class SGSTRecord : Record, IHaveMODL
    {
        public struct DATAField(BinaryReader r, int dataSize)
        {
            public byte Uses = r.ReadByte();
            public int Value = r.ReadInt32();
            public float Weight = r.ReadSingle();
        }

        public MODLGroup MODL { get; set; } // Model
        public STRVField FULL; // Item Name
        public DATAField DATA; // Sigil Stone Data
        public FILEField ICON; // Icon
        public FMIDField<SCPTRecord>? SCRI; // Script (optional)
        public List<ENCHRecord.EFITField> EFITs = []; // Effect Data
        public List<ENCHRecord.SCITField> SCITs = []; // Script Effect Data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" => SCITs.Count == 0 ? FULL = r.ReadSTRV(dataSize) : SCITs.Last().FULLField(r, dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "EFID" => r.Skip(dataSize),
            "EFIT" => EFITs.AddX(new ENCHRecord.EFITField(r, dataSize, format)),
            "SCIT" => SCITs.AddX(new ENCHRecord.SCITField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : ACHR.Actor Reference

    public class ACHRRecord : Record
    {
        public FMIDField<Record> NAME; // Base
        public REFRRecord.DATAField DATA; // Position/Rotation
        public FMIDField<CELLRecord>? XPCI; // Unused (optional)
        public BYTVField? XLOD; // Distant LOD Data (optional)
        public REFRRecord.XESPField? XESP; // Enable Parent (optional)
        public FMIDField<REFRRecord>? XMRC; // Merchant container (optional)
        public FMIDField<ACRERecord>? XHRS; // Horse (optional)
        public FLTVField? XSCL; // Scale (optional)
        public BYTVField? XRGD; // Ragdoll Data (optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "NAME" => NAME = new FMIDField<Record>(r, dataSize),
            "DATA" => DATA = new REFRRecord.DATAField(r, dataSize),
            "XPCI" => XPCI = new FMIDField<CELLRecord>(r, dataSize),
            "FULL" => XPCI.Value.AddName(r.ReadFString(dataSize)),
            "XLOD" => XLOD = r.ReadBYTV(dataSize),
            "XESP" => XESP = new REFRRecord.XESPField(r, dataSize),
            "XMRC" => XMRC = new FMIDField<REFRRecord>(r, dataSize),
            "XHRS" => XHRS = new FMIDField<ACRERecord>(r, dataSize),
            "XSCL" => XSCL = r.ReadSAndVerify<FLTVField>(dataSize),
            "XRGD" => XRGD = r.ReadBYTV(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : AMMO.Ammo

    public class AMMORecord : Record, IHaveMODL
    {
        public struct DATAField(BinaryReader r, int dataSize)
        {
            public float Speed = r.ReadSingle();
            public uint Flags = r.ReadUInt32();
            public uint Value = r.ReadUInt32();
            public float Weight = r.ReadSingle();
            public ushort Damage = r.ReadUInt16();
        }

        public MODLGroup MODL { get; set; } // Model
        public STRVField FULL; // Item Name
        public FILEField? ICON; // Male Icon (optional)
        public FMIDField<ENCHRecord>? ENAM; // Enchantment ID (optional)
        public IN16Field? ANAM; // Enchantment points (optional)
        public DATAField DATA; // Ammo Data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "ENAM" => ENAM = new FMIDField<ENCHRecord>(r, dataSize),
            "ANAM" => ANAM = r.ReadSAndVerify<IN16Field>(dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : ANIO.Animated Object

    public class ANIORecord : Record, IHaveMODL
    {
        public MODLGroup MODL { get; set; } // Model
        public FMIDField<IDLERecord> DATA; // IDLE animation

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "DATA" => DATA = new FMIDField<IDLERecord>(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : CLMT.Climate

    public class CLMTRecord : Record, IHaveMODL
    {
        public struct WLSTField(BinaryReader r, int dataSize)
        {
            public FormId<WTHRRecord> Weather = new(r.ReadUInt32());
            public int Chance = r.ReadInt32();
        }

        public struct TNAMField(BinaryReader r, int dataSize)
        {
            public byte Sunrise_Begin = r.ReadByte();
            public byte Sunrise_End = r.ReadByte();
            public byte Sunset_Begin = r.ReadByte();
            public byte Sunset_End = r.ReadByte();
            public byte Volatility = r.ReadByte();
            public byte MoonsPhaseLength = r.ReadByte();
        }

        public MODLGroup MODL { get; set; } // Model
        public FILEField FNAM; // Sun Texture
        public FILEField GNAM; // Sun Glare Texture
        public List<WLSTField> WLSTs = []; // Climate
        public TNAMField TNAM; // Timing

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "FNAM" => FNAM = r.ReadFILE(dataSize),
            "GNAM" => GNAM = r.ReadFILE(dataSize),
            "WLST" => WLSTs.AddRangeX(Enumerable.Range(0, dataSize >> 3).Select(x => new WLSTField(r, dataSize))),
            "TNAM" => TNAM = new TNAMField(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : CSTY.Combat Style

    public class CSTYRecord : Record
    {
        public class CSTDField
        {
            public byte DodgePercentChance;
            public byte LeftRightPercentChance;
            public float DodgeLeftRightTimer_Min;
            public float DodgeLeftRightTimer_Max;
            public float DodgeForwardTimer_Min;
            public float DodgeForwardTimer_Max;
            public float DodgeBackTimer_Min;
            public float DodgeBackTimer_Max;
            public float IdleTimer_Min;
            public float IdleTimer_Max;
            public byte BlockPercentChance;
            public byte AttackPercentChance;
            public float RecoilStaggerBonusToAttack;
            public float UnconsciousBonusToAttack;
            public float HandToHandBonusToAttack;
            public byte PowerAttackPercentChance;
            public float RecoilStaggerBonusToPower;
            public float UnconsciousBonusToPowerAttack;
            public byte PowerAttack_Normal;
            public byte PowerAttack_Forward;
            public byte PowerAttack_Back;
            public byte PowerAttack_Left;
            public byte PowerAttack_Right;
            public float HoldTimer_Min;
            public float HoldTimer_Max;
            public byte Flags1;
            public byte AcrobaticDodgePercentChance;
            public float RangeMult_Optimal;
            public float RangeMult_Max;
            public float SwitchDistance_Melee;
            public float SwitchDistance_Ranged;
            public float BuffStandoffDistance;
            public float RangedStandoffDistance;
            public float GroupStandoffDistance;
            public byte RushingAttackPercentChance;
            public float RushingAttackDistanceMult;
            public uint Flags2;

            public CSTDField(BinaryReader r, int dataSize)
            {
                //if (dataSize != 124 && dataSize != 120 && dataSize != 112 && dataSize != 104 && dataSize != 92 && dataSize != 84)
                //    DodgePercentChance = 0;
                DodgePercentChance = r.ReadByte();
                LeftRightPercentChance = r.ReadByte();
                r.Skip(2); // Unused
                DodgeLeftRightTimer_Min = r.ReadSingle();
                DodgeLeftRightTimer_Max = r.ReadSingle();
                DodgeForwardTimer_Min = r.ReadSingle();
                DodgeForwardTimer_Max = r.ReadSingle();
                DodgeBackTimer_Min = r.ReadSingle();
                DodgeBackTimer_Max = r.ReadSingle();
                IdleTimer_Min = r.ReadSingle();
                IdleTimer_Max = r.ReadSingle();
                BlockPercentChance = r.ReadByte();
                AttackPercentChance = r.ReadByte();
                r.Skip(2); // Unused
                RecoilStaggerBonusToAttack = r.ReadSingle();
                UnconsciousBonusToAttack = r.ReadSingle();
                HandToHandBonusToAttack = r.ReadSingle();
                PowerAttackPercentChance = r.ReadByte();
                r.Skip(3); // Unused
                RecoilStaggerBonusToPower = r.ReadSingle();
                UnconsciousBonusToPowerAttack = r.ReadSingle();
                PowerAttack_Normal = r.ReadByte();
                PowerAttack_Forward = r.ReadByte();
                PowerAttack_Back = r.ReadByte();
                PowerAttack_Left = r.ReadByte();
                PowerAttack_Right = r.ReadByte();
                r.Skip(3); // Unused
                HoldTimer_Min = r.ReadSingle();
                HoldTimer_Max = r.ReadSingle();
                Flags1 = r.ReadByte();
                AcrobaticDodgePercentChance = r.ReadByte();
                r.Skip(2); // Unused
                if (dataSize == 84) return; RangeMult_Optimal = r.ReadSingle();
                RangeMult_Max = r.ReadSingle();
                if (dataSize == 92) return; SwitchDistance_Melee = r.ReadSingle();
                SwitchDistance_Ranged = r.ReadSingle();
                BuffStandoffDistance = r.ReadSingle();
                if (dataSize == 104) return; RangedStandoffDistance = r.ReadSingle();
                GroupStandoffDistance = r.ReadSingle();
                if (dataSize == 112) return; RushingAttackPercentChance = r.ReadByte();
                r.Skip(3); // Unused
                RushingAttackDistanceMult = r.ReadSingle();
                if (dataSize == 120) return; Flags2 = r.ReadUInt32();
            }
        }

        public struct CSADField(BinaryReader r, int dataSize)
        {
            public float DodgeFatigueModMult = r.ReadSingle();
            public float DodgeFatigueModBase = r.ReadSingle();
            public float EncumbSpeedModBase = r.ReadSingle();
            public float EncumbSpeedModMult = r.ReadSingle();
            public float DodgeWhileUnderAttackMult = r.ReadSingle();
            public float DodgeNotUnderAttackMult = r.ReadSingle();
            public float DodgeBackWhileUnderAttackMult = r.ReadSingle();
            public float DodgeBackNotUnderAttackMult = r.ReadSingle();
            public float DodgeForwardWhileAttackingMult = r.ReadSingle();
            public float DodgeForwardNotAttackingMult = r.ReadSingle();
            public float BlockSkillModifierMult = r.ReadSingle();
            public float BlockSkillModifierBase = r.ReadSingle();
            public float BlockWhileUnderAttackMult = r.ReadSingle();
            public float BlockNotUnderAttackMult = r.ReadSingle();
            public float AttackSkillModifierMult = r.ReadSingle();
            public float AttackSkillModifierBase = r.ReadSingle();
            public float AttackWhileUnderAttackMult = r.ReadSingle();
            public float AttackNotUnderAttackMult = r.ReadSingle();
            public float AttackDuringBlockMult = r.ReadSingle();
            public float PowerAttFatigueModBase = r.ReadSingle();
            public float PowerAttFatigueModMult = r.ReadSingle();
        }

        public CSTDField CSTD; // Standard
        public CSADField CSAD; // Advanced

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "CSTD" => CSTD = new CSTDField(r, dataSize),
            "CSAD" => CSAD = new CSADField(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : EFSH.Effect Shader

    public class EFSHRecord : Record
    {
        public class DATAField
        {
            public byte Flags;
            public uint MembraneShader_SourceBlendMode;
            public uint MembraneShader_BlendOperation;
            public uint MembraneShader_ZTestFunction;
            public ColorRef4 FillTextureEffect_Color;
            public float FillTextureEffect_AlphaFadeInTime;
            public float FillTextureEffect_FullAlphaTime;
            public float FillTextureEffect_AlphaFadeOutTime;
            public float FillTextureEffect_PresistentAlphaRatio;
            public float FillTextureEffect_AlphaPulseAmplitude;
            public float FillTextureEffect_AlphaPulseFrequency;
            public float FillTextureEffect_TextureAnimationSpeed_U;
            public float FillTextureEffect_TextureAnimationSpeed_V;
            public float EdgeEffect_FallOff;
            public ColorRef4 EdgeEffect_Color;
            public float EdgeEffect_AlphaFadeInTime;
            public float EdgeEffect_FullAlphaTime;
            public float EdgeEffect_AlphaFadeOutTime;
            public float EdgeEffect_PresistentAlphaRatio;
            public float EdgeEffect_AlphaPulseAmplitude;
            public float EdgeEffect_AlphaPulseFrequency;
            public float FillTextureEffect_FullAlphaRatio;
            public float EdgeEffect_FullAlphaRatio;
            public uint MembraneShader_DestBlendMode;
            public uint ParticleShader_SourceBlendMode;
            public uint ParticleShader_BlendOperation;
            public uint ParticleShader_ZTestFunction;
            public uint ParticleShader_DestBlendMode;
            public float ParticleShader_ParticleBirthRampUpTime;
            public float ParticleShader_FullParticleBirthTime;
            public float ParticleShader_ParticleBirthRampDownTime;
            public float ParticleShader_FullParticleBirthRatio;
            public float ParticleShader_PersistantParticleBirthRatio;
            public float ParticleShader_ParticleLifetime;
            public float ParticleShader_ParticleLifetime_Delta;
            public float ParticleShader_InitialSpeedAlongNormal;
            public float ParticleShader_AccelerationAlongNormal;
            public float ParticleShader_InitialVelocity1;
            public float ParticleShader_InitialVelocity2;
            public float ParticleShader_InitialVelocity3;
            public float ParticleShader_Acceleration1;
            public float ParticleShader_Acceleration2;
            public float ParticleShader_Acceleration3;
            public float ParticleShader_ScaleKey1;
            public float ParticleShader_ScaleKey2;
            public float ParticleShader_ScaleKey1Time;
            public float ParticleShader_ScaleKey2Time;
            public ColorRef4 ColorKey1_Color;
            public ColorRef4 ColorKey2_Color;
            public ColorRef4 ColorKey3_Color;
            public float ColorKey1_ColorAlpha;
            public float ColorKey2_ColorAlpha;
            public float ColorKey3_ColorAlpha;
            public float ColorKey1_ColorKeyTime;
            public float ColorKey2_ColorKeyTime;
            public float ColorKey3_ColorKeyTime;

            public DATAField(BinaryReader r, int dataSize)
            {
                if (dataSize != 224 && dataSize != 96) Flags = 0;
                Flags = r.ReadByte();
                r.Skip(3); // Unused
                MembraneShader_SourceBlendMode = r.ReadUInt32();
                MembraneShader_BlendOperation = r.ReadUInt32();
                MembraneShader_ZTestFunction = r.ReadUInt32();
                FillTextureEffect_Color = r.ReadSAndVerify<ColorRef4>(dataSize);
                FillTextureEffect_AlphaFadeInTime = r.ReadSingle();
                FillTextureEffect_FullAlphaTime = r.ReadSingle();
                FillTextureEffect_AlphaFadeOutTime = r.ReadSingle();
                FillTextureEffect_PresistentAlphaRatio = r.ReadSingle();
                FillTextureEffect_AlphaPulseAmplitude = r.ReadSingle();
                FillTextureEffect_AlphaPulseFrequency = r.ReadSingle();
                FillTextureEffect_TextureAnimationSpeed_U = r.ReadSingle();
                FillTextureEffect_TextureAnimationSpeed_V = r.ReadSingle();
                EdgeEffect_FallOff = r.ReadSingle();
                EdgeEffect_Color = r.ReadSAndVerify<ColorRef4>(dataSize);
                EdgeEffect_AlphaFadeInTime = r.ReadSingle();
                EdgeEffect_FullAlphaTime = r.ReadSingle();
                EdgeEffect_AlphaFadeOutTime = r.ReadSingle();
                EdgeEffect_PresistentAlphaRatio = r.ReadSingle();
                EdgeEffect_AlphaPulseAmplitude = r.ReadSingle();
                EdgeEffect_AlphaPulseFrequency = r.ReadSingle();
                FillTextureEffect_FullAlphaRatio = r.ReadSingle();
                EdgeEffect_FullAlphaRatio = r.ReadSingle();
                MembraneShader_DestBlendMode = r.ReadUInt32();
                if (dataSize == 96) return;
                ParticleShader_SourceBlendMode = r.ReadUInt32();
                ParticleShader_BlendOperation = r.ReadUInt32();
                ParticleShader_ZTestFunction = r.ReadUInt32();
                ParticleShader_DestBlendMode = r.ReadUInt32();
                ParticleShader_ParticleBirthRampUpTime = r.ReadSingle();
                ParticleShader_FullParticleBirthTime = r.ReadSingle();
                ParticleShader_ParticleBirthRampDownTime = r.ReadSingle();
                ParticleShader_FullParticleBirthRatio = r.ReadSingle();
                ParticleShader_PersistantParticleBirthRatio = r.ReadSingle();
                ParticleShader_ParticleLifetime = r.ReadSingle();
                ParticleShader_ParticleLifetime_Delta = r.ReadSingle();
                ParticleShader_InitialSpeedAlongNormal = r.ReadSingle();
                ParticleShader_AccelerationAlongNormal = r.ReadSingle();
                ParticleShader_InitialVelocity1 = r.ReadSingle();
                ParticleShader_InitialVelocity2 = r.ReadSingle();
                ParticleShader_InitialVelocity3 = r.ReadSingle();
                ParticleShader_Acceleration1 = r.ReadSingle();
                ParticleShader_Acceleration2 = r.ReadSingle();
                ParticleShader_Acceleration3 = r.ReadSingle();
                ParticleShader_ScaleKey1 = r.ReadSingle();
                ParticleShader_ScaleKey2 = r.ReadSingle();
                ParticleShader_ScaleKey1Time = r.ReadSingle();
                ParticleShader_ScaleKey2Time = r.ReadSingle();
                ColorKey1_Color = r.ReadSAndVerify<ColorRef4>(dataSize);
                ColorKey2_Color = r.ReadSAndVerify<ColorRef4>(dataSize);
                ColorKey3_Color = r.ReadSAndVerify<ColorRef4>(dataSize);
                ColorKey1_ColorAlpha = r.ReadSingle();
                ColorKey2_ColorAlpha = r.ReadSingle();
                ColorKey3_ColorAlpha = r.ReadSingle();
                ColorKey1_ColorKeyTime = r.ReadSingle();
                ColorKey2_ColorKeyTime = r.ReadSingle();
                ColorKey3_ColorKeyTime = r.ReadSingle();
            }
        }

        public FILEField ICON; // Fill Texture
        public FILEField ICO2; // Particle Shader Texture
        public DATAField DATA; // Data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "ICO2" => ICO2 = r.ReadFILE(dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : EYES.Eyes

    public class EYESRecord : Record
    {
        public STRVField FULL;
        public FILEField ICON;
        public BYTEField DATA; // Playable

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "DATA" => DATA = r.ReadSAndVerify<BYTEField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : FLOR.Flora

    public class FLORRecord : Record, IHaveMODL
    {
        public MODLGroup MODL { get; set; } // Model
        public STRVField FULL; // Plant Name
        public FMIDField<SCPTRecord> SCRI; // Script (optional)
        public FMIDField<INGRRecord> PFIG; // The ingredient the plant produces (optional)
        public BYTVField PFPC; // Spring, Summer, Fall, Winter Ingredient Production (byte)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "PFIG" => PFIG = new FMIDField<INGRRecord>(r, dataSize),
            "PFPC" => PFPC = r.ReadBYTV(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : FURN.Furniture

    public class FURNRecord : Record, IHaveMODL
    {
        public MODLGroup MODL { get; set; } // Model
        public STRVField FULL; // Furniture Name
        public FMIDField<SCPTRecord> SCRI; // Script (optional)
        public IN32Field MNAM; // Active marker flags, required. A bit field with a bit value of 1 indicating that the matching marker position in the NIF file is active.

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "MNAM" => MNAM = r.ReadSAndVerify<IN32Field>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : GRAS.Grass

    public class GRASRecord : Record
    {
        public struct DATAField
        {
            public byte Density;
            public byte MinSlope;
            public byte MaxSlope;
            public ushort UnitFromWaterAmount;
            public uint UnitFromWaterType;
            //Above - At Least,
            //Above - At Most,
            //Below - At Least,
            //Below - At Most,
            //Either - At Least,
            //Either - At Most,
            //Either - At Most Above,
            //Either - At Most Below
            public float PositionRange;
            public float HeightRange;
            public float ColorRange;
            public float WavePeriod;
            public byte Flags;

            public DATAField(BinaryReader r, int dataSize)
            {
                Density = r.ReadByte();
                MinSlope = r.ReadByte();
                MaxSlope = r.ReadByte();
                r.ReadByte();
                UnitFromWaterAmount = r.ReadUInt16();
                r.Skip(2);
                UnitFromWaterType = r.ReadUInt32();
                PositionRange = r.ReadSingle();
                HeightRange = r.ReadSingle();
                ColorRange = r.ReadSingle();
                WavePeriod = r.ReadSingle();
                Flags = r.ReadByte();
                r.Skip(3);
            }
        }

        public MODLGroup MODL;
        public DATAField DATA;

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : IDLE.Idle Animations

    public class IDLERecord : Record, IHaveMODL
    {
        public MODLGroup MODL { get; set; }
        public List<SCPTRecord.CTDAField> CTDAs = []; // Conditions
        public BYTEField ANAM;
        public FMIDField<IDLERecord>[] DATAs;

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "CTDA" or "CTDT" => CTDAs.AddX(new SCPTRecord.CTDAField(r, dataSize, format)),
            "ANAM" => ANAM = r.ReadSAndVerify<BYTEField>(dataSize),
            "DATA" => DATAs = [.. Enumerable.Range(0, dataSize >> 2).Select(x => new FMIDField<IDLERecord>(r, 4))],
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : LSCR.Load Screen

    public class LSCRRecord : Record
    {
        public struct LNAMField(BinaryReader r, int dataSize)
        {
            public FormId<Record> Direct = new(r.ReadUInt32());
            public FormId<WRLDRecord> IndirectWorld = new(r.ReadUInt32());
            public short IndirectGridX = r.ReadInt16();
            public short IndirectGridY = r.ReadInt16();
        }

        public FILEField ICON; // Icon
        public STRVField DESC; // Description
        public List<LNAMField> LNAMs; // LoadForm

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "DESC" => DESC = r.ReadSTRV(dataSize),
            "LNAM" => (LNAMs ??= []).AddX(new LNAMField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : PACK.AI Package

    public class PACKRecord : Record
    {
        public struct PKDTField
        {
            public ushort Flags;
            public byte Type;

            public PKDTField(BinaryReader r, int dataSize)
            {
                Flags = r.ReadUInt16();
                Type = r.ReadByte();
                r.Skip(dataSize - 3); // Unused
            }
        }

        public struct PLDTField(BinaryReader r, int dataSize)
        {
            public int Type = r.ReadInt32();
            public uint Target = r.ReadUInt32();
            public int Radius = r.ReadInt32();
        }

        public struct PSDTField(BinaryReader r, int dataSize)
        {
            public byte Month = r.ReadByte();
            public byte DayOfWeek = r.ReadByte();
            public byte Date = r.ReadByte();
            public sbyte Time = (sbyte)r.ReadByte();
            public int Duration = r.ReadInt32();
        }

        public struct PTDTField(BinaryReader r, int dataSize)
        {
            public int Type = r.ReadInt32();
            public uint Target = r.ReadUInt32();
            public int Count = r.ReadInt32();
        }

        public PKDTField PKDT; // General
        public PLDTField PLDT; // Location
        public PSDTField PSDT; // Schedule
        public PTDTField PTDT; // Target
        public List<SCPTRecord.CTDAField> CTDAs = []; // Conditions

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "PKDT" => PKDT = new PKDTField(r, dataSize),
            "PLDT" => PLDT = new PLDTField(r, dataSize),
            "PSDT" => PSDT = new PSDTField(r, dataSize),
            "PTDT" => PTDT = new PTDTField(r, dataSize),
            "CTDA" or "CTDT" => CTDAs.AddX(new SCPTRecord.CTDAField(r, dataSize, format)),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : QUST.Quest

    public class QUSTRecord : Record
    {
        public struct DATAField(BinaryReader r, int dataSize)
        {
            public byte Flags = r.ReadByte();
            public byte Priority = r.ReadByte();
        }

        public STRVField FULL; // Item Name
        public FILEField ICON; // Icon
        public DATAField DATA; // Icon
        public FMIDField<SCPTRecord> SCRI; // Script Name
        public SCPTRecord.SCHRField SCHR; // Script Data
        public BYTVField SCDA; // Compiled Script
        public STRVField SCTX; // Script Source
        public List<FMIDField<Record>> SCROs = []; // Global variable reference

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "CTDA" => r.Skip(dataSize),
            "INDX" => r.Skip(dataSize),
            "QSDT" => r.Skip(dataSize),
            "CNAM" => r.Skip(dataSize),
            "QSTA" => r.Skip(dataSize),
            "SCHR" => SCHR = new SCPTRecord.SCHRField(r, dataSize),
            "SCDA" => SCDA = r.ReadBYTV(dataSize),
            "SCTX" => SCTX = r.ReadSTRV(dataSize),
            "SCRO" => SCROs.AddX(new FMIDField<Record>(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : REFR.Placed Object

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

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "NAME" => NAME = new FMIDField<Record>(r, dataSize),
            "XTEL" => XTEL = new XTELField(r, dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            "XLOC" => XLOC = new XLOCField(r, dataSize),
            "XOWN" => (XOWNs ??= []).AddX(new CELLRecord.XOWNGroup { XOWN = new FMIDField<Record>(r, dataSize) }),
            "XRNK" => XOWNs.Last().XRNK = r.ReadSAndVerify<IN32Field>(dataSize),
            "XGLB" => XOWNs.Last().XGLB = new FMIDField<Record>(r, dataSize),
            "XESP" => XESP = new XESPField(r, dataSize),
            "XTRG" => XTRG = new FMIDField<Record>(r, dataSize),
            "XSED" => XSED = new XSEDField(r, dataSize),
            "XLOD" => XLOD = r.ReadBYTV(dataSize),
            "XCHG" => XCHG = r.ReadSAndVerify<FLTVField>(dataSize),
            "XHLT" => XCHG = r.ReadSAndVerify<FLTVField>(dataSize),
            "XPCI" => (_nextFull = 1, XPCI = new FMIDField<CELLRecord>(r, dataSize)),
            "FULL" => _nextFull == 1 ? XPCI.Value.AddName(r.ReadFString(dataSize)) : _nextFull == 2 ? XMRKs.Last().FULL = r.ReadSTRV(dataSize) : _nextFull = 0,
            "XLCM" => XLCM = r.ReadSAndVerify<IN32Field>(dataSize),
            "XRTM" => XRTM = new FMIDField<REFRRecord>(r, dataSize),
            "XACT" => XACT = r.ReadSAndVerify<UI32Field>(dataSize),
            "XCNT" => XCNT = r.ReadSAndVerify<IN32Field>(dataSize),
            "XMRK" => (_nextFull = 2, (XMRKs ??= []).AddX(new XMRKGroup())),
            "FNAM" => XMRKs.Last().FNAM = r.ReadSAndVerify<BYTEField>(dataSize),
            "TNAM" => XMRKs.Last().TNAM = r.ReadSAndVerify<BYTEField>(dataSize),
            "ONAM" => true,
            "XRGD" => XRGD = r.ReadBYTV(dataSize),
            "XSCL" => XSCL = r.ReadSAndVerify<FLTVField>(dataSize),
            "XSOL" => XSOL = r.ReadSAndVerify<BYTEField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : SLGM.Soul Gem

    public class SLGMRecord : Record, IHaveMODL
    {
        public struct DATAField(BinaryReader r, int dataSize)
        {
            public int Value = r.ReadInt32();
            public float Weight = r.ReadSingle();
        }

        public MODLGroup MODL { get; set; } // Model
        public STRVField FULL; // Item Name
        public FMIDField<SCPTRecord> SCRI; // Script (optional)
        public DATAField DATA; // Type of soul contained in the gem
        public FILEField ICON; // Icon (optional)
        public BYTEField SOUL; // Type of soul contained in the gem
        public BYTEField SLCP; // Soul gem maximum capacity

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "SOUL" => SOUL = r.ReadSAndVerify<BYTEField>(dataSize),
            "SLCP" => SLCP = r.ReadSAndVerify<BYTEField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : TES4.Plugin Info

    public unsafe class TES4Record : Record
    {
        public struct HEDRField
        {
            public static (string, int) Struct = ("<fiI", sizeof(HEDRField));
            public float Version;
            public int NumRecords; // Number of records and groups (not including TES4 record itself).
            public uint NextObjectId; // Next available object ID.
        }

        public HEDRField HEDR;
        public STRVField? CNAM; // author (Optional)
        public STRVField? SNAM; // description (Optional)
        public List<STRVField> MASTs; // master
        public List<INTVField> DATAs; // fileSize
        public UNKNField? ONAM; // overrides (Optional)
        public IN32Field INTV; // unknown
        public IN32Field? INCC; // unknown (Optional)
        // TES5
        public UNKNField? TNAM; // overrides (Optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "HEDR" => HEDR = r.ReadSAndVerify<HEDRField>(dataSize),
            "OFST" => r.Skip(dataSize),
            "DELE" => r.Skip(dataSize),
            "CNAM" => CNAM = r.ReadSTRV(dataSize),
            "SNAM" => SNAM = r.ReadSTRV(dataSize),
            "MAST" => (MASTs ??= []).AddX(r.ReadSTRV(dataSize)),
            "DATA" => (DATAs ??= []).AddX(r.ReadINTV(dataSize)),
            "ONAM" => ONAM = r.ReadUNKN(dataSize),
            "INTV" => INTV = r.ReadSAndVerify<IN32Field>(dataSize),
            "INCC" => INCC = r.ReadSAndVerify<IN32Field>(dataSize),
            // TES5
            "TNAM" => TNAM = r.ReadUNKN(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : TREE.Tree

    public class TREERecord : Record, IHaveMODL
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

        public MODLGroup MODL { get; set; } // Model
        public FILEField ICON; // Leaf Texture
        public SNAMField SNAM; // SpeedTree Seeds, array of ints
        public CNAMField CNAM; // Tree Parameters
        public BNAMField BNAM; // Billboard Dimensions

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "SNAM" => SNAM = new SNAMField(r, dataSize),
            "CNAM" => CNAM = new CNAMField(r, dataSize),
            "BNAM" => BNAM = new BNAMField(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : WATR.Water Type

    public class WATRRecord : Record
    {
        public class DATAField
        {
            public float WindVelocity;
            public float WindDirection;
            public float WaveAmplitude;
            public float WaveFrequency;
            public float SunPower;
            public float ReflectivityAmount;
            public float FresnelAmount;
            public float ScrollXSpeed;
            public float ScrollYSpeed;
            public float FogDistance_NearPlane;
            public float FogDistance_FarPlane;
            public ColorRef4 ShallowColor;
            public ColorRef4 DeepColor;
            public ColorRef4 ReflectionColor;
            public byte TextureBlend;
            public float RainSimulator_Force;
            public float RainSimulator_Velocity;
            public float RainSimulator_Falloff;
            public float RainSimulator_Dampner;
            public float RainSimulator_StartingSize;
            public float DisplacementSimulator_Force;
            public float DisplacementSimulator_Velocity;
            public float DisplacementSimulator_Falloff;
            public float DisplacementSimulator_Dampner;
            public float DisplacementSimulator_StartingSize;
            public ushort Damage;

            public DATAField(BinaryReader r, int dataSize)
            {
                if (dataSize != 102 && dataSize != 86 && dataSize != 62 && dataSize != 42 && dataSize != 2) WindVelocity = 1;
                if (dataSize == 2) { Damage = r.ReadUInt16(); return; }
                WindVelocity = r.ReadSingle();
                WindDirection = r.ReadSingle();
                WaveAmplitude = r.ReadSingle();
                WaveFrequency = r.ReadSingle();
                SunPower = r.ReadSingle();
                ReflectivityAmount = r.ReadSingle();
                FresnelAmount = r.ReadSingle();
                ScrollXSpeed = r.ReadSingle();
                ScrollYSpeed = r.ReadSingle();
                FogDistance_NearPlane = r.ReadSingle();
                if (dataSize == 42) { Damage = r.ReadUInt16(); return; }
                FogDistance_FarPlane = r.ReadSingle();
                ShallowColor = r.ReadSAndVerify<ColorRef4>(dataSize);
                DeepColor = r.ReadSAndVerify<ColorRef4>(dataSize);
                ReflectionColor = r.ReadSAndVerify<ColorRef4>(dataSize);
                TextureBlend = r.ReadByte();
                r.Skip(3); // Unused
                if (dataSize == 62) { Damage = r.ReadUInt16(); return; }
                RainSimulator_Force = r.ReadSingle();
                RainSimulator_Velocity = r.ReadSingle();
                RainSimulator_Falloff = r.ReadSingle();
                RainSimulator_Dampner = r.ReadSingle();
                RainSimulator_StartingSize = r.ReadSingle();
                DisplacementSimulator_Force = r.ReadSingle();
                if (dataSize == 86)
                {
                    //DisplacementSimulator_Velocity = DisplacementSimulator_Falloff = DisplacementSimulator_Dampner = DisplacementSimulator_StartingSize = 0F;
                    Damage = r.ReadUInt16();
                    return;
                }
                DisplacementSimulator_Velocity = r.ReadSingle();
                DisplacementSimulator_Falloff = r.ReadSingle();
                DisplacementSimulator_Dampner = r.ReadSingle();
                DisplacementSimulator_StartingSize = r.ReadSingle();
                Damage = r.ReadUInt16();
            }
        }

        public struct GNAMField(BinaryReader r, int dataSize)
        {
            public FormId<WATRRecord> Daytime = new(r.ReadUInt32());
            public FormId<WATRRecord> Nighttime = new(r.ReadUInt32());
            public FormId<WATRRecord> Underwater = new(r.ReadUInt32());
        }

        public STRVField TNAM; // Texture
        public BYTEField ANAM; // Opacity
        public BYTEField FNAM; // Flags
        public STRVField MNAM; // Material ID
        public FMIDField<SOUNRecord> SNAM; // Sound
        public DATAField DATA; // DATA
        public GNAMField GNAM; // GNAM

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "TNAM" => TNAM = r.ReadSTRV(dataSize),
            "ANAM" => ANAM = r.ReadSAndVerify<BYTEField>(dataSize),
            "FNAM" => FNAM = r.ReadSAndVerify<BYTEField>(dataSize),
            "MNAM" => MNAM = r.ReadSTRV(dataSize),
            "SNAM" => SNAM = new FMIDField<SOUNRecord>(r, dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            "GNAM" => GNAM = new GNAMField(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : WRLD.Worldspace

    public unsafe class WRLDRecord : Record
    {
        public struct MNAMField
        {
            public static (string, int) Struct = ($"<2i4h", sizeof(MNAMField));
            public Int2 UsableDimensions;
            // Cell Coordinates
            public short NWCell_X;
            public short NWCell_Y;
            public short SECell_X;
            public short SECell_Y;
        }

        public struct NAM0Field(BinaryReader r, int dataSize)
        {
            public static (string, int) Struct = ("<2f2f", sizeof(NAM0Field));
            public Vector2 Min = new(r.ReadSingle(), r.ReadSingle());
            public Vector2 Max = Vector2.Zero;
            public object NAM9Field(BinaryReader r, int dataSize) => Max = new Vector2(r.ReadSingle(), r.ReadSingle());
        }

        // TES5
        public struct RNAMField
        {
            public struct Reference
            {
                public FormId32<REFRRecord> Ref;
                public short X;
                public short Y;
            }
            public short GridX;
            public short GridY;
            public Reference[] GridReferences;

            public RNAMField(BinaryReader r, int dataSize)
            {
                GridX = r.ReadInt16();
                GridY = r.ReadInt16();
                var referenceCount = r.ReadUInt32();
                var referenceSize = dataSize - 8;
                Assert(referenceSize >> 3 == referenceCount);
                GridReferences = r.ReadTArray<Reference>(referenceSize, referenceSize >> 3);
            }
        }

        public STRVField FULL;
        public FMIDField<WRLDRecord>? WNAM; // Parent Worldspace
        public FMIDField<CLMTRecord>? CNAM; // Climate
        public FMIDField<WATRRecord>? NAM2; // Water
        public FILEField? ICON; // Icon
        public MNAMField? MNAM; // Map Data
        public BYTEField? DATA; // Flags
        public NAM0Field NAM0; // Object Bounds
        public UI32Field? SNAM; // Music
        // TES5
        public List<RNAMField> RNAMs = []; // Large References

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "WNAM" => WNAM = new FMIDField<WRLDRecord>(r, dataSize),
            "CNAM" => CNAM = new FMIDField<CLMTRecord>(r, dataSize),
            "NAM2" => NAM2 = new FMIDField<WATRRecord>(r, dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "MNAM" => MNAM = r.ReadSAndVerify<MNAMField>(dataSize),
            "DATA" => DATA = r.ReadSAndVerify<BYTEField>(dataSize),
            "NAM0" => NAM0 = new NAM0Field(r, dataSize),
            "NAM9" => NAM0.NAM9Field(r, dataSize),
            "SNAM" => SNAM = r.ReadSAndVerify<UI32Field>(dataSize),
            "OFST" => r.Skip(dataSize),
            // TES5
            "RNAM" => RNAMs.AddX(new RNAMField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 0450 : WTHR.Weather

    public class WTHRRecord : Record, IHaveMODL
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
            public FormId<SOUNRecord> Sound = new(r.ReadUInt32()); // Sound FormId
            public uint Type = r.ReadUInt32(); // Sound Type - 0=Default, 1=Precipitation, 2=Wind, 3=Thunder
        }

        public MODLGroup MODL { get; set; } // Model
        public FILEField CNAM; // Lower Cloud Layer
        public FILEField DNAM; // Upper Cloud Layer
        public BYTVField NAM0; // Colors by Types/Times
        public FNAMField FNAM; // Fog Distance
        public HNAMField HNAM; // HDR Data
        public DATAField DATA; // Weather Data
        public List<SNAMField> SNAMs = []; // Sounds

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "CNAM" => CNAM = r.ReadFILE(dataSize),
            "DNAM" => DNAM = r.ReadFILE(dataSize),
            "NAM0" => NAM0 = r.ReadBYTV(dataSize),
            "FNAM" => FNAM = new FNAMField(r, dataSize),
            "HNAM" => HNAM = new HNAMField(r, dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            "SNAM" => SNAMs.AddX(new SNAMField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 3000 : BODY.Body

    public class BODYRecord : Record, IHaveMODL
    {
        public struct BYDTField(BinaryReader r, int dataSize)
        {
            public byte Part = r.ReadByte();
            public byte Vampire = r.ReadByte();
            public byte Flags = r.ReadByte();
            public byte PartType = r.ReadByte();
        }

        public MODLGroup MODL { get; set; } // NIF Model
        public STRVField FNAM; // Body name
        public BYDTField BYDT;

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "MODL" => MODL = new MODLGroup(r, dataSize),
                "FNAM" => FNAM = r.ReadSTRV(dataSize),
                "BYDT" => BYDT = new BYDTField(r, dataSize),
                _ => Empty,
            }
            : null;
    }

    #endregion

    #region 3000 : LEVC.Leveled Creature

    public class LEVCRecord : Record
    {
        public IN32Field DATA; // List data - 1 = Calc from all levels <= PC level
        public BYTEField NNAM; // Chance None?
        public IN32Field INDX; // Number of items in list
        public List<STRVField> CNAMs = []; // ID string of list item
        public List<IN16Field> INTVs = []; // PC level for previous CNAM
        // The CNAM/INTV can occur many times in pairs

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "DATA" => DATA = r.ReadSAndVerify<IN32Field>(dataSize),
                "NNAM" => NNAM = r.ReadSAndVerify<BYTEField>(dataSize),
                "INDX" => INDX = r.ReadSAndVerify<IN32Field>(dataSize),
                "CNAM" => CNAMs.AddX(r.ReadSTRV(dataSize)),
                "INTV" => INTVs.AddX(r.ReadSAndVerify<IN16Field>(dataSize)),
                _ => Empty,
            }
            : null;
    }

    #endregion

    #region 3000 : LEVI.Leveled item

    public class LEVIRecord : Record
    {
        public IN32Field DATA; // List data - 1 = Calc from all levels <= PC level, 2 = Calc for each item
        public BYTEField NNAM; // Chance None?
        public IN32Field INDX; // Number of items in list
        public List<STRVField> INAMs = []; // ID string of list item
        public List<IN16Field> INTVs = []; // PC level for previous INAM
        // The CNAM/INTV can occur many times in pairs

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "DATA" => DATA = r.ReadSAndVerify<IN32Field>(dataSize),
                "NNAM" => NNAM = r.ReadSAndVerify<BYTEField>(dataSize),
                "INDX" => INDX = r.ReadSAndVerify<IN32Field>(dataSize),
                "INAM" => INAMs.AddX(r.ReadSTRV(dataSize)),
                "INTV" => INTVs.AddX(r.ReadSAndVerify<IN16Field>(dataSize)),
                _ => Empty,
            }
            : null;
    }

    #endregion

    #region 3000 : PROB.Probe

    public class PROBRecord : Record, IHaveMODL
    {
        public struct PBDTField(BinaryReader r, int dataSize)
        {
            public float Weight = r.ReadSingle();
            public int Value = r.ReadInt32();
            public float Quality = r.ReadSingle();
            public int Uses = r.ReadInt32();
        }

        public MODLGroup MODL { get; set; } // Model Name
        public STRVField FNAM; // Item Name
        public PBDTField PBDT; // Probe Data
        public FILEField ICON; // Inventory Icon
        public FMIDField<SCPTRecord> SCRI; // Script Name

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "MODL" => MODL = new MODLGroup(r, dataSize),
                "FNAM" => FNAM = r.ReadSTRV(dataSize),
                "PBDT" => PBDT = new PBDTField(r, dataSize),
                "ITEX" => ICON = r.ReadFILE(dataSize),
                "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
                _ => Empty,
            }
            : null;
    }

    #endregion

    #region 3000 : REPA.Repair Item

    public class REPARecord : Record, IHaveMODL
    {
        public struct RIDTField(BinaryReader r, int dataSize)
        {
            public float Weight = r.ReadSingle();
            public int Value = r.ReadInt32();
            public int Uses = r.ReadInt32();
            public float Quality = r.ReadSingle();
        }

        public MODLGroup MODL { get; set; } // Model Name
        public STRVField FNAM; // Item Name
        public RIDTField RIDT; // Repair Data
        public FILEField ICON; // Inventory Icon
        public FMIDField<SCPTRecord> SCRI; // Script Name

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "MODL" => MODL = new MODLGroup(r, dataSize),
                "FNAM" => FNAM = r.ReadSTRV(dataSize),
                "RIDT" => RIDT = new RIDTField(r, dataSize),
                "ITEX" => ICON = r.ReadFILE(dataSize),
                "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
                _ => Empty,
            }
            : null;
    }

    #endregion

    #region 3000 : SNDG.Sound Generator

    public class SNDGRecord : Record
    {
        public enum SNDGType : uint
        {
            LeftFoot = 0,
            RightFoot = 1,
            SwimLeft = 2,
            SwimRight = 3,
            Moan = 4,
            Roar = 5,
            Scream = 6,
            Land = 7,
        }

        public IN32Field DATA; // Sound Type Data
        public STRVField SNAM; // Sound ID
        public STRVField? CNAM; // Creature name (optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "DATA" => DATA = r.ReadSAndVerify<IN32Field>(dataSize),
                "SNAM" => SNAM = r.ReadSTRV(dataSize),
                "CNAM" => CNAM = r.ReadSTRV(dataSize),
                _ => Empty,
            }
            : null;
    }

    #endregion

    #region 3000 : SSCR.Start Script

    public class SSCRRecord : Record
    {
        public STRVField DATA; // Digits

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "DATA" => DATA = r.ReadSTRV(dataSize),
                _ => Empty,
            }
            : null;
    }

    #endregion

    #region 3000 : TES3.Plugin info

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

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "HEDR" => HEDR = new HEDRField(r, dataSize),
            "MAST" => (MASTs ??= []).AddX(r.ReadSTRV(dataSize)),
            "DATA" => (DATAs ??= []).AddX(r.ReadINTV(dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 3400 : BSGN.Birthsign

    public class BSGNRecord : Record
    {
        public STRVField FULL; // Sign name
        public FILEField ICON; // Texture
        public STRVField DESC; // Description
        public List<STRVField> NPCSs = []; // TES3: Spell/ability
        public List<FMIDField<Record>> SPLOs = []; // TES4: (points to a SPEL or LVSP record)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "FULL" or "FNAM" => FULL = r.ReadSTRV(dataSize),
            "ICON" or "TNAM" => ICON = r.ReadFILE(dataSize),
            "DESC" => DESC = r.ReadSTRV(dataSize),
            "SPLO" => (SPLOs ??= []).AddX(new FMIDField<Record>(r, dataSize)),
            "NPCS" => (NPCSs ??= []).AddX(r.ReadSTRV(dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 3400 : MGEF.Magic Effect

    public class MGEFRecord : Record
    {
        // TES3
        public struct MEDTField
        {
            public int SpellSchool; // 0 = Alteration, 1 = Conjuration, 2 = Destruction, 3 = Illusion, 4 = Mysticism, 5 = Restoration
            public float BaseCost;
            public int Flags; // 0x0200 = Spellmaking, 0x0400 = Enchanting, 0x0800 = Negative
            public ColorRef4 Color;
            public float SpeedX;
            public float SizeX;
            public float SizeCap;

            public MEDTField(BinaryReader r, int dataSize)
            {
                SpellSchool = r.ReadInt32();
                BaseCost = r.ReadSingle();
                Flags = r.ReadInt32();
                Color = new ColorRef4 { Red = (byte)r.ReadInt32(), Green = (byte)r.ReadInt32(), Blue = (byte)r.ReadInt32(), Null = 255 };
                SpeedX = r.ReadSingle();
                SizeX = r.ReadSingle();
                SizeCap = r.ReadSingle();
            }
        }

        // TES4
        [Flags]
        public enum MFEGFlag : uint
        {
            Hostile = 0x00000001,
            Recover = 0x00000002,
            Detrimental = 0x00000004,
            MagnitudePercent = 0x00000008,
            Self = 0x00000010,
            Touch = 0x00000020,
            Target = 0x00000040,
            NoDuration = 0x00000080,
            NoMagnitude = 0x00000100,
            NoArea = 0x00000200,
            FXPersist = 0x00000400,
            Spellmaking = 0x00000800,
            Enchanting = 0x00001000,
            NoIngredient = 0x00002000,
            Unknown14 = 0x00004000,
            Unknown15 = 0x00008000,
            UseWeapon = 0x00010000,
            UseArmor = 0x00020000,
            UseCreature = 0x00040000,
            UseSkill = 0x00080000,
            UseAttribute = 0x00100000,
            Unknown21 = 0x00200000,
            Unknown22 = 0x00400000,
            Unknown23 = 0x00800000,
            UseActorValue = 0x01000000,
            SprayProjectileType = 0x02000000, // (Ball if Spray, Bolt or Fog is not specified)
            BoltProjectileType = 0x04000000,
            NoHitEffect = 0x08000000,
            Unknown28 = 0x10000000,
            Unknown29 = 0x20000000,
            Unknown30 = 0x40000000,
            Unknown31 = 0x80000000,
        }

        public class DATAField
        {
            public uint Flags;
            public float BaseCost;
            public int AssocItem;
            public int MagicSchool;
            public int ResistValue;
            public uint CounterEffectCount; // Must be updated automatically when ESCE length changes!
            public FormId<LIGHRecord> Light;
            public float ProjectileSpeed;
            public FormId<EFSHRecord> EffectShader;
            public FormId<EFSHRecord> EnchantEffect;
            public FormId<SOUNRecord> CastingSound;
            public FormId<SOUNRecord> BoltSound;
            public FormId<SOUNRecord> HitSound;
            public FormId<SOUNRecord> AreaSound;
            public float ConstantEffectEnchantmentFactor;
            public float ConstantEffectBarterFactor;

            public DATAField(BinaryReader r, int dataSize)
            {
                Flags = r.ReadUInt32();
                BaseCost = r.ReadSingle();
                AssocItem = r.ReadInt32();
                //wbUnion('Assoc. Item', wbMGEFFAssocItemDecider, [
                //  wbFormIDCk('Unused', [NULL]),
                //  wbFormIDCk('Assoc. Weapon', [WEAP]),
                //  wbFormIDCk('Assoc. Armor', [ARMO, NULL{?}]),
                //  wbFormIDCk('Assoc. Creature', [CREA, LVLC, NPC_]),
                //  wbInteger('Assoc. Actor Value', itS32, wbActorValueEnum)
                MagicSchool = r.ReadInt32();
                ResistValue = r.ReadInt32();
                CounterEffectCount = r.ReadUInt16();
                r.Skip(2); // Unused
                Light = new FormId<LIGHRecord>(r.ReadUInt32());
                ProjectileSpeed = r.ReadSingle();
                EffectShader = new FormId<EFSHRecord>(r.ReadUInt32());
                if (dataSize == 36)
                    return;
                EnchantEffect = new FormId<EFSHRecord>(r.ReadUInt32());
                CastingSound = new FormId<SOUNRecord>(r.ReadUInt32());
                BoltSound = new FormId<SOUNRecord>(r.ReadUInt32());
                HitSound = new FormId<SOUNRecord>(r.ReadUInt32());
                AreaSound = new FormId<SOUNRecord>(r.ReadUInt32());
                ConstantEffectEnchantmentFactor = r.ReadSingle();
                ConstantEffectBarterFactor = r.ReadSingle();
            }
        }

        public override string ToString() => $"MGEF: {INDX.Value}:{EDID.Value}";
        public STRVField DESC; // Description
                               // TES3
        public INTVField INDX; // The Effect ID (0 to 137)
        public MEDTField MEDT; // Effect Data
        public FILEField ICON; // Effect Icon
        public STRVField PTEX; // Particle texture
        public STRVField CVFX; // Casting visual
        public STRVField BVFX; // Bolt visual
        public STRVField HVFX; // Hit visual
        public STRVField AVFX; // Area visual
        public STRVField? CSND; // Cast sound (optional)
        public STRVField? BSND; // Bolt sound (optional)
        public STRVField? HSND; // Hit sound (optional)
        public STRVField? ASND; // Area sound (optional)
                                // TES4
        public STRVField FULL;
        public MODLGroup MODL;
        public DATAField DATA;
        public STRVField[] ESCEs;

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "INDX" => INDX = r.ReadINTV(dataSize),
                "MEDT" => MEDT = new MEDTField(r, dataSize),
                "ITEX" => ICON = r.ReadFILE(dataSize),
                "PTEX" => PTEX = r.ReadSTRV(dataSize),
                "CVFX" => CVFX = r.ReadSTRV(dataSize),
                "BVFX" => BVFX = r.ReadSTRV(dataSize),
                "HVFX" => HVFX = r.ReadSTRV(dataSize),
                "AVFX" => AVFX = r.ReadSTRV(dataSize),
                "DESC" => DESC = r.ReadSTRV(dataSize),
                "CSND" => CSND = r.ReadSTRV(dataSize),
                "BSND" => BSND = r.ReadSTRV(dataSize),
                "HSND" => HSND = r.ReadSTRV(dataSize),
                "ASND" => ASND = r.ReadSTRV(dataSize),
                _ => Empty,
            }
            : type switch
            {
                "EDID" => EDID = r.ReadSTRV(dataSize),
                "FULL" => FULL = r.ReadSTRV(dataSize),
                "DESC" => DESC = r.ReadSTRV(dataSize),
                "ICON" => ICON = r.ReadFILE(dataSize),
                "MODL" => MODL = new MODLGroup(r, dataSize),
                "MODB" => MODL.MODBField(r, dataSize),
                "DATA" => DATA = new DATAField(r, dataSize),
                "ESCE" => ESCEs = [.. Enumerable.Range(0, dataSize >> 2).Select(x => r.ReadSTRV(4))],
                _ => Empty,
            };
    }

    #endregion

    #region 3400 : PGRD.Path grid

    public class PGRDRecord : Record
    {
        public struct DATAField
        {
            public int X;
            public int Y;
            public short Granularity;
            public short PointCount;

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format != BethesdaFormat.TES3)
                {
                    X = Y = Granularity = 0;
                    PointCount = r.ReadInt16();
                    return;
                }
                X = r.ReadInt32();
                Y = r.ReadInt32();
                Granularity = r.ReadInt16();
                PointCount = r.ReadInt16();
            }
        }

        public struct PGRPField
        {
            public Vector3 Point;
            public byte Connections;

            public PGRPField(BinaryReader r, int dataSize)
            {
                Point = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                Connections = r.ReadByte();
                r.Skip(3); // Unused
            }
        }

        public struct PGRRField(BinaryReader r, int dataSize)
        {
            public short StartPointId = r.ReadInt16();
            public short EndPointId = r.ReadInt16();
        }

        public struct PGRIField
        {
            public short PointId;
            public Vector3 ForeignNode;

            public PGRIField(BinaryReader r, int dataSize)
            {
                PointId = r.ReadInt16();
                r.Skip(2); // Unused (can merge back)
                ForeignNode = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            }
        }

        public struct PGRLField
        {
            public FormId<REFRRecord> Reference;
            public short[] PointIds;

            public PGRLField(BinaryReader r, int dataSize)
            {
                Reference = new FormId<REFRRecord>(r.ReadUInt32());
                PointIds = new short[(dataSize - 4) >> 2];
                for (var i = 0; i < PointIds.Length; i++)
                {
                    PointIds[i] = r.ReadInt16();
                    r.Skip(2); // Unused (can merge back)
                }
            }
        }

        public DATAField DATA; // Number of nodes
        public PGRPField[] PGRPs;
        public UNKNField PGRC;
        public UNKNField PGAG;
        public PGRRField[] PGRRs; // Point-to-Point Connections
        public List<PGRLField> PGRLs; // Point-to-Reference Mappings
        public PGRIField[] PGRIs; // Inter-Cell Connections

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "DATA" => DATA = new DATAField(r, dataSize, format),
            "PGRP" => PGRPs = [.. Enumerable.Range(0, dataSize >> 4).Select(x => new PGRPField(r, 16))],
            "PGRC" => PGRC = r.ReadUNKN(dataSize),
            "PGAG" => PGAG = r.ReadUNKN(dataSize),
            "PGRR" => (PGRRs = [.. Enumerable.Range(0, dataSize >> 2).Select(x => new PGRRField(r, 4))], r.Skip(dataSize % 4)),
            "PGRL" => (PGRLs ??= []).AddX(new PGRLField(r, dataSize)),
            "PGRI" => PGRIs = [.. Enumerable.Range(0, dataSize >> 4).Select(x => new PGRIField(r, 16))],
            _ => Empty,
        };
    }

    #endregion

    #region 3400 : SCPT.Script

    public class SCPTRecord : Record
    {
        // TESX
        public struct CTDAField
        {
            public enum INFOType : byte
            {
                Nothing = 0, Function, Global, Local, Journal, Item, Dead, NotId, NotFaction, NotClass, NotRace, NotCell, NotLocal
            }

            // TES3: 0 = [=], 1 = [!=], 2 = [>], 3 = [>=], 4 = [<], 5 = [<=]
            // TES4: 0 = [=], 2 = [!=], 4 = [>], 6 = [>=], 8 = [<], 10 = [<=]
            public byte CompareOp;
            // (00-71) - sX = Global/Local/Not Local types, JX = Journal type, IX = Item Type, DX = Dead Type, XX = Not ID Type, FX = Not Faction, CX = Not Class, RX = Not Race, LX = Not Cell
            public string FunctionId;
            // TES3
            public byte Index; // (0-5)
            public byte Type;
            // Except for the function type, this is the ID for the global/local/etc. Is not nessecarily NULL terminated.The function type SCVR sub-record has
            public string Name;
            // TES4
            public float ComparisonValue;
            public int Parameter1; // Parameter #1
            public int Parameter2; // Parameter #2

            public CTDAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    Index = r.ReadByte();
                    Type = r.ReadByte();
                    FunctionId = r.ReadFString(2);
                    CompareOp = (byte)(r.ReadByte() << 1);
                    Name = r.ReadFString(dataSize - 5);
                    ComparisonValue = Parameter1 = Parameter2 = 0;
                    return;
                }
                CompareOp = r.ReadByte();
                r.Skip(3); // Unused
                ComparisonValue = r.ReadSingle();
                FunctionId = r.ReadFString(4);
                Parameter1 = r.ReadInt32();
                Parameter2 = r.ReadInt32();
                if (dataSize != 24) r.Skip(4); // Unused
                Index = Type = 0;
                Name = null;
            }
        }

        // TES3
        public class SCHDField(BinaryReader r, int dataSize)
        {
            public override string ToString() => $"{Name}";
            public string Name = r.ReadZString(32);
            public int NumShorts = r.ReadInt32();
            public int NumLongs = r.ReadInt32();
            public int NumFloats = r.ReadInt32();
            public int ScriptDataSize = r.ReadInt32();
            public int LocalVarSize = r.ReadInt32();
            public string[] Variables = null;
            public object SCVRField(BinaryReader r, int dataSize) => Variables = r.ReadZAStringList(dataSize).ToArray();
        }

        // TES4
        public struct SCHRField
        {
            public override readonly string ToString() => $"{RefCount}";
            public uint RefCount;
            public uint CompiledSize;
            public uint VariableCount;
            public uint Type; // 0x000 = Object, 0x001 = Quest, 0x100 = Magic Effect

            public SCHRField(BinaryReader r, int dataSize)
            {
                r.Skip(4); // Unused
                RefCount = r.ReadUInt32();
                CompiledSize = r.ReadUInt32();
                VariableCount = r.ReadUInt32();
                Type = r.ReadUInt32();
                if (dataSize == 20) return;
                r.Skip(dataSize - 20);
            }
        }

        public class SLSDField
        {
            public override string ToString() => $"{Idx}:{VariableName}";
            public uint Idx;
            public uint Type;
            public string VariableName;

            public SLSDField(BinaryReader r, int dataSize)
            {
                Idx = r.ReadUInt32();
                r.ReadUInt32(); // Unknown
                r.ReadUInt32(); // Unknown
                r.ReadUInt32(); // Unknown
                Type = r.ReadUInt32();
                r.ReadUInt32(); // Unknown
                                // SCVRField
                VariableName = null;
            }
            public object SCVRField(BinaryReader r, int dataSize) => VariableName = r.ReadYEncoding(dataSize);
        }

        public override string ToString() => $"SCPT: {EDID.Value ?? SCHD.Name}";
        public BYTVField SCDA; // Compiled Script
        public STRVField SCTX; // Script Source
                               // TES3
        public SCHDField SCHD; // Script Data
                               // TES4
        public SCHRField SCHR; // Script Data
        public List<SLSDField> SLSDs = []; // Variable data
        public List<SLSDField> SCRVs = []; // Ref variable data (one for each ref declared)
        public List<FMIDField<Record>> SCROs = []; // Global variable reference

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize)
        {
            return type switch
            {
                "EDID" => EDID = r.ReadSTRV(dataSize),
                "SCHD" => SCHD = new SCHDField(r, dataSize),
                "SCVR" => format != BethesdaFormat.TES3 ? SLSDs.Last().SCVRField(r, dataSize) : SCHD.SCVRField(r, dataSize),
                "SCDA" or "SCDT" => SCDA = r.ReadBYTV(dataSize),
                "SCTX" => SCTX = r.ReadSTRV(dataSize),
                // TES4
                "SCHR" => SCHR = new SCHRField(r, dataSize),
                "SLSD" => SLSDs.AddX(new SLSDField(r, dataSize)),
                "SCRO" => SCROs.AddX(new FMIDField<Record>(r, dataSize)),
                "SCRV" => SCRVs.AddX(this.Then(r.ReadUInt32(), idx => SLSDs.Single(x => x.Idx == idx))),
                _ => Empty,
            };
        }
    }

    #endregion

    #region 3450 : ACTI.Activator

    public class ACTIRecord : Record, IHaveMODL
    {
        public MODLGroup MODL { get; set; } // Model Name
        public STRVField FULL; // Item Name
        public FMIDField<SCPTRecord> SCRI; // Script (Optional)
                                           // TES4
        public FMIDField<SOUNRecord> SNAM; // Sound (Optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" or "FNAM" => FULL = r.ReadSTRV(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "SNAM" => SNAM = new FMIDField<SOUNRecord>(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : ALCH.Potion

    public class ALCHRecord : Record, IHaveMODL
    {
        // TESX
        public class DATAField
        {
            public float Weight;
            public int Value;
            public int Flags; //: AutoCalc

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                Weight = r.ReadSingle();
                if (format == BethesdaFormat.TES3)
                {
                    Value = r.ReadInt32();
                    Flags = r.ReadInt32();
                }
            }
            public object ENITField(BinaryReader r, int dataSize)
            {
                Value = r.ReadInt32();
                Flags = r.ReadByte();
                r.Skip(3); // Unknown
                return true;
            }
        }

        // TES3
        public struct ENAMField(BinaryReader r, int dataSize)
        {
            public short EffectId = r.ReadInt16();
            public byte SkillId = r.ReadByte(); // for skill related effects, -1/0 otherwise
            public byte AttributeId = r.ReadByte(); // for attribute related effects, -1/0 otherwise
            public int Unknown1 = r.ReadInt32();
            public int Unknown2 = r.ReadInt32();
            public int Duration = r.ReadInt32();
            public int Magnitude = r.ReadInt32();
            public int Unknown4 = r.ReadInt32();
        }

        public MODLGroup MODL { get; set; } // Model
        public STRVField FULL; // Item Name
        public DATAField DATA; // Alchemy Data
        public ENAMField? ENAM; // Enchantment
        public FILEField ICON; // Icon
        public FMIDField<SCPTRecord>? SCRI; // Script (optional)
                                            // TES4
        public List<ENCHRecord.EFITField> EFITs = []; // Effect Data
        public List<ENCHRecord.SCITField> SCITs = []; // Script Effect Data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" => SCITs.Count == 0 ? FULL = r.ReadSTRV(dataSize) : SCITs.Last().FULLField(r, dataSize),
            "FNAM" => FULL = r.ReadSTRV(dataSize),
            "DATA" or "ALDT" => DATA = new DATAField(r, dataSize, format),
            "ENAM" => ENAM = new ENAMField(r, dataSize),
            "ICON" or "TEXT" => ICON = r.ReadFILE(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            //
            "ENIT" => DATA.ENITField(r, dataSize),
            "EFID" => r.Skip(dataSize),
            "EFIT" => EFITs.AddX(new ENCHRecord.EFITField(r, dataSize, format)),
            "SCIT" => SCITs.AddX(new ENCHRecord.SCITField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : APPA.Alchem Apparatus

    public class APPARecord : Record, IHaveMODL
    {
        // TESX
        public struct DATAField
        {
            public byte Type; // 0 = Mortar and Pestle, 1 = Albemic, 2 = Calcinator, 3 = Retort
            public float Quality;
            public float Weight;
            public int Value;

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    Type = (byte)r.ReadInt32();
                    Quality = r.ReadSingle();
                    Weight = r.ReadSingle();
                    Value = r.ReadInt32();
                    return;
                }
                Type = r.ReadByte();
                Value = r.ReadInt32();
                Weight = r.ReadSingle();
                Quality = r.ReadSingle();
            }
        }

        public MODLGroup MODL { get; set; } // Model Name
        public STRVField FULL; // Item Name
        public DATAField DATA; // Alchemy Data
        public FILEField ICON; // Inventory Icon
        public FMIDField<SCPTRecord> SCRI; // Script Name

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" or "FNAM" => FULL = r.ReadSTRV(dataSize),
            "DATA" or "AADT" => DATA = new DATAField(r, dataSize, format),
            "ICON" or "ITEX" => ICON = r.ReadFILE(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : ARMO.Armor

    public class ARMORecord : Record, IHaveMODL
    {
        // TESX
        public struct DATAField
        {
            public enum ARMOType { Helmet = 0, Cuirass, L_Pauldron, R_Pauldron, Greaves, Boots, L_Gauntlet, R_Gauntlet, Shield, L_Bracer, R_Bracer, }

            public short Armour;
            public int Value;
            public int Health;
            public float Weight;
            // TES3
            public int Type;
            public int EnchantPts;

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    Type = r.ReadInt32();
                    Weight = r.ReadSingle();
                    Value = r.ReadInt32();
                    Health = r.ReadInt32();
                    EnchantPts = r.ReadInt32();
                    Armour = (short)r.ReadInt32();
                    return;
                }
                Armour = r.ReadInt16();
                Value = r.ReadInt32();
                Health = r.ReadInt32();
                Weight = r.ReadSingle();
                Type = 0;
                EnchantPts = 0;
            }
        }

        public MODLGroup MODL { get; set; } // Male biped model
        public STRVField FULL; // Item Name
        public FILEField ICON; // Male icon
        public DATAField DATA; // Armour Data
        public FMIDField<SCPTRecord>? SCRI; // Script Name (optional)
        public FMIDField<ENCHRecord>? ENAM; // Enchantment FormId (optional)
                                            // TES3
        public List<CLOTRecord.INDXFieldGroup> INDXs = []; // Body Part Index
                                                           // TES4
        public UI32Field BMDT; // Flags
        public MODLGroup MOD2; // Male world model (optional)
        public MODLGroup MOD3; // Female biped (optional)
        public MODLGroup MOD4; // Female world model (optional)
        public FILEField? ICO2; // Female icon (optional)
        public IN16Field? ANAM; // Enchantment points (optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" or "FNAM" => FULL = r.ReadSTRV(dataSize),
            "DATA" or "AODT" => DATA = new DATAField(r, dataSize, format),
            "ICON" or "ITEX" => ICON = r.ReadFILE(dataSize),
            "INDX" => INDXs.AddX(new CLOTRecord.INDXFieldGroup { INDX = r.ReadINTV(dataSize) }),
            "BNAM" => INDXs.Last().BNAM = r.ReadSTRV(dataSize),
            "CNAM" => INDXs.Last().CNAM = r.ReadSTRV(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "ENAM" => ENAM = new FMIDField<ENCHRecord>(r, dataSize),
            "BMDT" => BMDT = r.ReadSAndVerify<UI32Field>(dataSize),
            "MOD2" => MOD2 = new MODLGroup(r, dataSize),
            "MO2B" => MOD2.MODBField(r, dataSize),
            "MO2T" => MOD2.MODTField(r, dataSize),
            "MOD3" => MOD3 = new MODLGroup(r, dataSize),
            "MO3B" => MOD3.MODBField(r, dataSize),
            "MO3T" => MOD3.MODTField(r, dataSize),
            "MOD4" => MOD4 = new MODLGroup(r, dataSize),
            "MO4B" => MOD4.MODBField(r, dataSize),
            "MO4T" => MOD4.MODTField(r, dataSize),
            "ICO2" => ICO2 = r.ReadFILE(dataSize),
            "ANAM" => ANAM = r.ReadSAndVerify<IN16Field>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : BOOK.Book

    public class BOOKRecord : Record, IHaveMODL
    {
        public struct DATAField
        {
            public byte Flags; //: Scroll - (1 is scroll, 0 not)
            public byte Teaches; //: SkillId - (-1 is no skill)
            public int Value;
            public float Weight;
            //
            public int EnchantPts;

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    Weight = r.ReadSingle();
                    Value = r.ReadInt32();
                    Flags = (byte)r.ReadInt32();
                    Teaches = (byte)r.ReadInt32();
                    EnchantPts = r.ReadInt32();
                    return;
                }
                Flags = r.ReadByte();
                Teaches = r.ReadByte();
                Value = r.ReadInt32();
                Weight = r.ReadSingle();
                EnchantPts = 0;
            }
        }

        public MODLGroup MODL { get; set; } // Model (optional)
        public STRVField FULL; // Item Name
        public DATAField DATA; // Book Data
        public STRVField DESC; // Book Text
        public FILEField ICON; // Inventory Icon (optional)
        public FMIDField<SCPTRecord> SCRI; // Script Name (optional)
        public FMIDField<ENCHRecord> ENAM; // Enchantment FormId (optional)
                                           // TES4
        public IN16Field? ANAM; // Enchantment points (optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" or "FNAM" => FULL = r.ReadSTRV(dataSize),
            "DATA" or "BKDT" => DATA = new DATAField(r, dataSize, format),
            "ICON" or "ITEX" => ICON = r.ReadFILE(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "DESC" or "TEXT" => DESC = r.ReadSTRV(dataSize),
            "ENAM" => ENAM = new FMIDField<ENCHRecord>(r, dataSize),
            "ANAM" => ANAM = r.ReadSAndVerify<IN16Field>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : CELL.Cell

    public unsafe class CELLRecord : Record, ICellRecord
    {
        [Flags]
        public enum CELLFlags : ushort
        {
            Interior = 0x0001,
            HasWater = 0x0002,
            InvertFastTravel = 0x0004, //: IllegalToSleepHere
            BehaveLikeExterior = 0x0008, //: BehaveLikeExterior (Tribunal), Force hide land (exterior cell) / Oblivion interior (interior cell)
            Unknown1 = 0x0010,
            PublicArea = 0x0020, // Public place
            HandChanged = 0x0040,
            ShowSky = 0x0080, // Behave like exterior
            UseSkyLighting = 0x0100,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct XCLCField
        {
            public static (string, int) StructN = ("<2iI", -1);
            public int GridX;
            public int GridY;
            public uint Flags;
            public override readonly string ToString() => $"{GridX}x{GridY}";
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct XCLLField
        {
            public static (string, int) StructN = ("<12c2f2i3f", -1);
            public ColorRef4 AmbientColor;
            public ColorRef4 DirectionalColor; //: SunlightColor
            public ColorRef4 FogColor;
            public float FogNear; //: FogDensity
                                  // TES4
            public float FogFar;
            public int DirectionalRotationXY;
            public int DirectionalRotationZ;
            public float DirectionalFade;
            public float FogClipDist;
            // TES5
            public float FogPow;
        }

        public class XOWNGroup
        {
            public FMIDField<Record> XOWN;
            public IN32Field XRNK; // Faction rank
            public FMIDField<Record> XGLB;
        }

        public class RefObj
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct XYZAField
            {
                public static (string, int) Struct = ("<3f3f", sizeof(XYZAField));
                public Float3 Position;
                public Float3 EulerAngles;
            }

            public UI32Field? FRMR; // Object Index (starts at 1)
                                    // This is used to uniquely identify objects in the cell. For new files the index starts at 1 and is incremented for each new object added. For modified
                                    // objects the index is kept the same.
            public override string ToString() => $"CREF: {EDID.Value}";
            public STRVField EDID; // Object ID
            public FLTVField? XSCL; // Scale (Static)
            public IN32Field? DELE; // Indicates that the reference is deleted.
            public XYZAField? DODT; // XYZ Pos, XYZ Rotation of exit
            public STRVField DNAM; // Door exit name (Door objects)
            public FLTVField? FLTV; // Follows the DNAM optionally, lock level
            public STRVField KNAM; // Door key
            public STRVField TNAM; // Trap name
            public BYTEField? UNAM; // Reference Blocked (only occurs once in MORROWIND.ESM)
            public STRVField ANAM; // Owner ID string
            public STRVField BNAM; // Global variable/rank ID
            public IN32Field? INTV; // Number of uses, occurs even for objects that don't use it
            public UI32Field? NAM9; // Unknown
            public STRVField XSOL; // Soul Extra Data (ID string of creature)
            public XYZAField DATA; // Ref Position Data
                                   //
            public STRVField CNAM; // Unknown
            public UI32Field? NAM0; // Unknown
            public IN32Field? XCHG; // Unknown
            public IN32Field? INDX; // Unknown
        }

        public STRVField FULL; // Full Name / TES3:RGNN - Region name
        public UI16Field DATA; // Flags
        public XCLCField? XCLC; // Cell Data (only used for exterior cells)
        public XCLLField? XCLL; // Lighting (only used for interior cells)
        public FLTVField? XCLW; // Water Height
                                // TES3
        public UI32Field? NAM0; // Number of objects in cell in current file (Optional)
        public INTVField INTV; // Unknown
        public CREFField? NAM5; // Map Color (COLORREF)
                                // TES4
        public FMIDField<REGNRecord>[] XCLRs; // Regions
        public BYTEField? XCMT; // Music (optional)
        public FMIDField<CLMTRecord>? XCCM; // Climate
        public FMIDField<WATRRecord>? XCWT; // Water
        public List<XOWNGroup> XOWNs = []; // Ownership

        // Referenced Object Data Grouping
        public bool InFRMR = false;
        public List<RefObj> RefObjs = [];
        RefObj _lastRef;

        public bool IsInterior => (DATA.Value & 0x01) == 0x01;
        public Int3 GridId; // => new Int3(XCLC.Value.GridX, XCLC.Value.GridY, !IsInterior ? 0 : -1);
        public GXColor? AmbientLight => XCLL != null ? (GXColor?)XCLL.Value.AmbientColor.AsColor32 : null;

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize)
        {
            //Console.WriteLine($"   {type}");
            if (!InFRMR && type == "FRMR") InFRMR = true;
            if (!InFRMR)
                return type switch
                {
                    "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
                    "FULL" or "RGNN" => FULL = r.ReadSTRV(dataSize),
                    "DATA" => (DATA = r.ReadINTV(format == BethesdaFormat.TES3 ? 4 : dataSize).AsUI16Field, format == BethesdaFormat.TES3 ? XCLC = r.ReadSAndVerify<XCLCField>(format == BethesdaFormat.TES3 ? 8 : dataSize) : null),
                    "XCLC" => XCLC = r.ReadSAndVerify<XCLCField>(format == BethesdaFormat.TES3 ? 8 : dataSize),
                    "XCLL" or "AMBI" => XCLL = r.ReadSAndVerify<XCLLField>(dataSize),
                    "XCLW" or "WHGT" => XCLW = r.ReadSAndVerify<FLTVField>(dataSize),
                    // TES3
                    "NAM0" => NAM0 = r.ReadSAndVerify<UI32Field>(dataSize),
                    "INTV" => INTV = r.ReadINTV(dataSize),
                    "NAM5" => NAM5 = r.ReadSAndVerify<CREFField>(dataSize),
                    // TES4
                    "XCLR" => XCLRs = [.. Enumerable.Range(0, dataSize >> 2).Select(x => new FMIDField<REGNRecord>(r, 4))],
                    "XCMT" => XCMT = r.ReadSAndVerify<BYTEField>(dataSize),
                    "XCCM" => XCCM = new FMIDField<CLMTRecord>(r, dataSize),
                    "XCWT" => XCWT = new FMIDField<WATRRecord>(r, dataSize),
                    "XOWN" => XOWNs.AddX(new XOWNGroup { XOWN = new FMIDField<Record>(r, dataSize) }),
                    "XRNK" => XOWNs.Last().XRNK = r.ReadSAndVerify<IN32Field>(dataSize),
                    "XGLB" => XOWNs.Last().XGLB = new FMIDField<Record>(r, dataSize),
                    _ => Empty,
                };
            // Referenced Object Data Grouping
            else return type switch
            {
                // RefObjDataGroup sub-records
                "FRMR" => RefObjs.AddX(_lastRef = new RefObj()).FRMR = r.ReadSAndVerify<UI32Field>(dataSize),
                "NAME" => _lastRef.EDID = r.ReadSTRV(dataSize),
                "XSCL" => _lastRef.XSCL = r.ReadSAndVerify<FLTVField>(dataSize),
                "DODT" => _lastRef.DODT = r.ReadSAndVerify<RefObj.XYZAField>(dataSize),
                "DNAM" => _lastRef.DNAM = r.ReadSTRV(dataSize),
                "FLTV" => _lastRef.FLTV = r.ReadSAndVerify<FLTVField>(dataSize),
                "KNAM" => _lastRef.KNAM = r.ReadSTRV(dataSize),
                "TNAM" => _lastRef.TNAM = r.ReadSTRV(dataSize),
                "UNAM" => _lastRef.UNAM = r.ReadSAndVerify<BYTEField>(dataSize),
                "ANAM" => _lastRef.ANAM = r.ReadSTRV(dataSize),
                "BNAM" => _lastRef.BNAM = r.ReadSTRV(dataSize),
                "INTV" => _lastRef.INTV = r.ReadSAndVerify<IN32Field>(dataSize),
                "NAM9" => _lastRef.NAM9 = r.ReadSAndVerify<UI32Field>(dataSize),
                "XSOL" => _lastRef.XSOL = r.ReadSTRV(dataSize),
                "DATA" => _lastRef.DATA = r.ReadSAndVerify<RefObj.XYZAField>(dataSize),
                //
                "CNAM" => _lastRef.CNAM = r.ReadSTRV(dataSize),
                "NAM0" => _lastRef.NAM0 = r.ReadSAndVerify<UI32Field>(dataSize),
                "XCHG" => _lastRef.XCHG = r.ReadSAndVerify<IN32Field>(dataSize),
                "INDX" => _lastRef.INDX = r.ReadSAndVerify<IN32Field>(dataSize),
                _ => Empty,
            };
        }
    }

    #endregion

    #region 3450 : CLAS.Class

    public class CLASRecord : Record
    {
        public struct DATAField
        {
            //wbArrayS('Primary Attributes', wbInteger('Primary Attribute', itS32, wbActorValueEnum), 2),
            //wbInteger('Specialization', itU32, wbSpecializationEnum),
            //wbArrayS('Major Skills', wbInteger('Major Skill', itS32, wbActorValueEnum), 7),
            //wbInteger('Flags', itU32, wbFlags(['Playable', 'Guard'])),
            //wbInteger('Buys/Sells and Services', itU32, wbServiceFlags),
            //wbInteger('Teaches', itS8, wbSkillEnum),
            //wbInteger('Maximum training level', itU8),
            //wbInteger('Unused', itU16)
            public DATAField(BinaryReader r, int dataSize) => r.Skip(dataSize);
        }

        public STRVField FULL; // Name
        public STRVField DESC; // Description
        public STRVField? ICON; // Icon (Optional)
        public DATAField DATA; // Data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "FNAM" => FULL = r.ReadSTRV(dataSize),
                "CLDT" => r.Skip(dataSize),
                "DESC" => DESC = r.ReadSTRV(dataSize),
                _ => Empty,
            }
            : type switch
            {
                "EDID" => EDID = r.ReadSTRV(dataSize),
                "FULL" => FULL = r.ReadSTRV(dataSize),
                "DESC" => DESC = r.ReadSTRV(dataSize),
                "ICON" => ICON = r.ReadSTRV(dataSize),
                "DATA" => DATA = new DATAField(r, dataSize),
                _ => Empty,
            };
    }

    #endregion

    #region 3450 : CLOT.Clothing

    public class CLOTRecord : Record, IHaveMODL
    {
        // TESX
        public struct DATAField
        {
            public enum CLOTType { Pants = 0, Shoes, Shirt, Belt, Robe, R_Glove, L_Glove, Skirt, Ring, Amulet }

            public int Value;
            public float Weight;
            //
            public int Type;
            public short EnchantPts;

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    Type = r.ReadInt32();
                    Weight = r.ReadSingle();
                    Value = r.ReadInt16();
                    EnchantPts = r.ReadInt16();
                    return;
                }
                Value = r.ReadInt32();
                Weight = r.ReadSingle();
                Type = 0;
                EnchantPts = 0;
            }
        }

        public class INDXFieldGroup
        {
            public override string ToString() => $"{INDX.Value}: {BNAM.Value}";
            public INTVField INDX;
            public STRVField BNAM;
            public STRVField CNAM;
        }

        public MODLGroup MODL { get; set; } // Model Name
        public STRVField FULL; // Item Name
        public DATAField DATA; // Clothing Data
        public FILEField ICON; // Male Icon
        public STRVField ENAM; // Enchantment Name
        public FMIDField<SCPTRecord> SCRI; // Script Name
                                           // TES3
        public List<INDXFieldGroup> INDXs = []; // Body Part Index (Moved to Race)
                                                // TES4
        public UI32Field BMDT; // Clothing Flags
        public MODLGroup MOD2; // Male world model (optional)
        public MODLGroup MOD3; // Female biped (optional)
        public MODLGroup MOD4; // Female world model (optional)
        public FILEField? ICO2; // Female icon (optional)
        public IN16Field? ANAM; // Enchantment points (optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" or "FNAM" => FULL = r.ReadSTRV(dataSize),
            "DATA" or "CTDT" => DATA = new DATAField(r, dataSize, format),
            "ICON" or "ITEX" => ICON = r.ReadFILE(dataSize),
            "INDX" => INDXs.AddX(new INDXFieldGroup { INDX = r.ReadINTV(dataSize) }),
            "BNAM" => INDXs.Last().BNAM = r.ReadSTRV(dataSize),
            "CNAM" => INDXs.Last().CNAM = r.ReadSTRV(dataSize),
            "ENAM" => ENAM = r.ReadSTRV(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "BMDT" => BMDT = r.ReadSAndVerify<UI32Field>(dataSize),
            "MOD2" => MOD2 = new MODLGroup(r, dataSize),
            "MO2B" => MOD2.MODBField(r, dataSize),
            "MO2T" => MOD2.MODTField(r, dataSize),
            "MOD3" => MOD3 = new MODLGroup(r, dataSize),
            "MO3B" => MOD3.MODBField(r, dataSize),
            "MO3T" => MOD3.MODTField(r, dataSize),
            "MOD4" => MOD4 = new MODLGroup(r, dataSize),
            "MO4B" => MOD4.MODBField(r, dataSize),
            "MO4T" => MOD4.MODTField(r, dataSize),
            "ICO2" => ICO2 = r.ReadFILE(dataSize),
            "ANAM" => ANAM = r.ReadSAndVerify<IN16Field>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : CONT.Container

    public class CONTRecord : Record, IHaveMODL
    {
        // TESX
        public class DATAField
        {
            public byte Flags; // flags 0x0001 = Organic, 0x0002 = Respawns, organic only, 0x0008 = Default, unknown
            public float Weight;

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    Weight = r.ReadSingle();
                    return;
                }
                Flags = r.ReadByte();
                Weight = r.ReadSingle();
            }
            public object FLAGField(BinaryReader r, int dataSize) => Flags = (byte)r.ReadUInt32();
        }

        public MODLGroup MODL { get; set; } // Model
        public STRVField FULL; // Container Name
        public DATAField DATA; // Container Data
        public FMIDField<SCPTRecord>? SCRI;
        public List<CNTOField> CNTOs = new();
        // TES4
        public FMIDField<SOUNRecord> SNAM; // Open sound
        public FMIDField<SOUNRecord> QNAM; // Close sound

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" or "FNAM" => FULL = r.ReadSTRV(dataSize),
            "DATA" or "CNDT" => DATA = new DATAField(r, dataSize, format),
            "FLAG" => DATA.FLAGField(r, dataSize),
            "CNTO" or "NPCO" => CNTOs.AddX(new CNTOField(r, dataSize, format)),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "SNAM" => SNAM = new FMIDField<SOUNRecord>(r, dataSize),
            "QNAM" => QNAM = new FMIDField<SOUNRecord>(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : CREA.Creature

    public class CREARecord : Record, IHaveMODL
    {
        [Flags]
        public enum CREAFlags : uint
        {
            Biped = 0x0001,
            Respawn = 0x0002,
            WeaponAndShield = 0x0004,
            None = 0x0008,
            Swims = 0x0010,
            Flies = 0x0020,
            Walks = 0x0040,
            DefaultFlags = 0x0048,
            Essential = 0x0080,
            SkeletonBlood = 0x0400,
            MetalBlood = 0x0800
        }

        public struct NPDTField(BinaryReader r, int dataSize)
        {
            public int Type = r.ReadInt32(); // 0 = Creature, 1 = Daedra, 2 = Undead, 3 = Humanoid
            public int Level = r.ReadInt32();
            public int Strength = r.ReadInt32();
            public int Intelligence = r.ReadInt32();
            public int Willpower = r.ReadInt32();
            public int Agility = r.ReadInt32();
            public int Speed = r.ReadInt32();
            public int Endurance = r.ReadInt32();
            public int Personality = r.ReadInt32();
            public int Luck = r.ReadInt32();
            public int Health = r.ReadInt32();
            public int SpellPts = r.ReadInt32();
            public int Fatigue = r.ReadInt32();
            public int Soul = r.ReadInt32();
            public int Combat = r.ReadInt32();
            public int Magic = r.ReadInt32();
            public int Stealth = r.ReadInt32();
            public int AttackMin1 = r.ReadInt32();
            public int AttackMax1 = r.ReadInt32();
            public int AttackMin2 = r.ReadInt32();
            public int AttackMax2 = r.ReadInt32();
            public int AttackMin3 = r.ReadInt32();
            public int AttackMax3 = r.ReadInt32();
            public int Gold = r.ReadInt32();
        }

        public struct AIDTField(BinaryReader r, int dataSize)
        {
            public enum AIFlags : uint
            {
                Weapon = 0x00001,
                Armor = 0x00002,
                Clothing = 0x00004,
                Books = 0x00008,
                Ingrediant = 0x00010,
                Picks = 0x00020,
                Probes = 0x00040,
                Lights = 0x00080,
                Apparatus = 0x00100,
                Repair = 0x00200,
                Misc = 0x00400,
                Spells = 0x00800,
                MagicItems = 0x01000,
                Potions = 0x02000,
                Training = 0x04000,
                Spellmaking = 0x08000,
                Enchanting = 0x10000,
                RepairItem = 0x20000
            }

            public byte Hello = r.ReadByte();
            public byte Unknown1 = r.ReadByte();
            public byte Fight = r.ReadByte();
            public byte Flee = r.ReadByte();
            public byte Alarm = r.ReadByte();
            public byte Unknown2 = r.ReadByte();
            public byte Unknown3 = r.ReadByte();
            public byte Unknown4 = r.ReadByte();
            public uint Flags = r.ReadUInt32();
        }

        public struct AI_WField(BinaryReader r, int dataSize)
        {
            public short Distance = r.ReadInt16();
            public short Duration = r.ReadInt16();
            public byte TimeOfDay = r.ReadByte();
            public byte[] Idle = r.ReadBytes(8);
            public byte Unknown = r.ReadByte();
        }

        public struct AI_TField(BinaryReader r, int dataSize)
        {
            public float X = r.ReadSingle();
            public float Y = r.ReadSingle();
            public float Z = r.ReadSingle();
            public float Unknown = r.ReadSingle();
        }

        public struct AI_FField(BinaryReader r, int dataSize)
        {
            public float X = r.ReadSingle();
            public float Y = r.ReadSingle();
            public float Z = r.ReadSingle();
            public short Duration = r.ReadInt16();
            public string Id = r.ReadZString(32);
            public short Unknown = r.ReadInt16();
        }

        public struct AI_AField(BinaryReader r, int dataSize)
        {
            public string Name = r.ReadZString(32);
            public byte Unknown = r.ReadByte();
        }

        public MODLGroup MODL { get; set; } // NIF Model
        public STRVField FNAM; // Creature name
        public NPDTField NPDT; // Creature data
        public IN32Field FLAG; // Creature Flags
        public FMIDField<SCPTRecord> SCRI; // Script
        public CNTOField NPCO; // Item record
        public AIDTField AIDT; // AI data
        public AI_WField AI_W; // AI Wander
        public AI_TField? AI_T; // AI Travel
        public AI_FField? AI_F; // AI Follow
        public AI_FField? AI_E; // AI Escort
        public AI_AField? AI_A; // AI Activate
        public FLTVField? XSCL; // Scale (optional), Only present if the scale is not 1.0
        public STRVField? CNAM;
        public List<STRVField> NPCSs = [];

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "MODL" => MODL = new MODLGroup(r, dataSize),
                "FNAM" => FNAM = r.ReadSTRV(dataSize),
                "NPDT" => NPDT = new NPDTField(r, dataSize),
                "FLAG" => FLAG = r.ReadSAndVerify<IN32Field>(dataSize),
                "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
                "NPCO" => NPCO = new CNTOField(r, dataSize, format),
                "AIDT" => AIDT = new AIDTField(r, dataSize),
                "AI_W" => AI_W = new AI_WField(r, dataSize),
                "AI_T" => AI_T = new AI_TField(r, dataSize),
                "AI_F" => AI_F = new AI_FField(r, dataSize),
                "AI_E" => AI_E = new AI_FField(r, dataSize),
                "AI_A" => AI_A = new AI_AField(r, dataSize),
                "XSCL" => XSCL = r.ReadSAndVerify<FLTVField>(dataSize),
                "CNAM" => CNAM = r.ReadSTRV(dataSize),
                "NPCS" => NPCSs.AddX(r.ReadSTRV_ZPad(dataSize)),
                _ => Empty,
            }
            : null;
    }

    #endregion

    #region 3450 : DIAL.Dialog Topic

    public class DIALRecord : Record
    {
        internal static DIALRecord LastRecord;

        public enum DIALType : byte { RegularTopic = 0, Voice, Greeting, Persuasion, Journal }

        public STRVField FULL; // Dialogue Name
        public BYTEField DATA; // Dialogue Type
        public List<FMIDField<QUSTRecord>> QSTIs; // Quests (optional)
        public List<INFORecord> INFOs = []; // Info Records

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => (LastRecord = this, EDID = r.ReadSTRV(dataSize)),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "DATA" => DATA = r.ReadSAndVerify<BYTEField>(dataSize),
            "QSTI" or "QSTR" => (QSTIs ??= []).AddX(new FMIDField<QUSTRecord>(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : DOOR.Door

    public class DOORRecord : Record, IHaveMODL
    {
        public STRVField FULL; // Door name
        public MODLGroup MODL { get; set; } // NIF model filename
        public FMIDField<SCPTRecord>? SCRI; // Script (optional)
        public FMIDField<SOUNRecord> SNAM; // Open Sound
        public FMIDField<SOUNRecord> ANAM; // Close Sound
                                           // TES4
        public FMIDField<SOUNRecord> BNAM; // Loop Sound
        public BYTEField FNAM; // Flags
        public FMIDField<Record> TNAM; // Random teleport destination

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "FNAM" => format != BethesdaFormat.TES3 ? FNAM = r.ReadT<BYTEField>(dataSize) : FULL = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "SNAM" => SNAM = new FMIDField<SOUNRecord>(r, dataSize),
            "ANAM" => ANAM = new FMIDField<SOUNRecord>(r, dataSize),
            "BNAM" => ANAM = new FMIDField<SOUNRecord>(r, dataSize),
            "TNAM" => TNAM = new FMIDField<Record>(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : ENCH.Enchantment

    public class ENCHRecord : Record
    {
        // TESX
        public struct ENITField
        {
            // TES3: 0 = Cast Once, 1 = Cast Strikes, 2 = Cast when Used, 3 = Constant Effect
            // TES4: 0 = Scroll, 1 = Staff, 2 = Weapon, 3 = Apparel
            public int Type;
            public int EnchantCost;
            public int ChargeAmount; //: Charge
            public int Flags; //: AutoCalc

            public ENITField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                Type = r.ReadInt32();
                if (format == BethesdaFormat.TES3)
                {
                    EnchantCost = r.ReadInt32();
                    ChargeAmount = r.ReadInt32();
                }
                else
                {
                    ChargeAmount = r.ReadInt32();
                    EnchantCost = r.ReadInt32();
                }
                Flags = r.ReadInt32();
            }
        }

        public class EFITField
        {
            public string EffectId;
            public int Type; //:RangeType - 0 = Self, 1 = Touch, 2 = Target
            public int Area;
            public int Duration;
            public int MagnitudeMin;
            // TES3
            public byte SkillId; // (-1 if NA)
            public byte AttributeId; // (-1 if NA)
            public int MagnitudeMax;
            // TES4
            public int ActorValue;

            public EFITField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    EffectId = r.ReadFString(2);
                    SkillId = r.ReadByte();
                    AttributeId = r.ReadByte();
                    Type = r.ReadInt32();
                    Area = r.ReadInt32();
                    Duration = r.ReadInt32();
                    MagnitudeMin = r.ReadInt32();
                    MagnitudeMax = r.ReadInt32();
                    return;
                }
                EffectId = r.ReadFString(4);
                MagnitudeMin = r.ReadInt32();
                Area = r.ReadInt32();
                Duration = r.ReadInt32();
                Type = r.ReadInt32();
                ActorValue = r.ReadInt32();
            }
        }

        // TES4
        public class SCITField
        {
            public string Name;
            public int ScriptFormId;
            public int School; // 0 = Alteration, 1 = Conjuration, 2 = Destruction, 3 = Illusion, 4 = Mysticism, 5 = Restoration
            public string VisualEffect;
            public uint Flags;

            public SCITField(BinaryReader r, int dataSize)
            {
                Name = "Script Effect";
                ScriptFormId = r.ReadInt32();
                if (dataSize == 4) return;
                School = r.ReadInt32();
                VisualEffect = r.ReadFString(4);
                Flags = dataSize > 12 ? r.ReadUInt32() : 0;
            }
            public object FULLField(BinaryReader r, int dataSize) => Name = r.ReadYEncoding(dataSize);
        }

        public STRVField FULL; // Enchant name
        public ENITField ENIT; // Enchant Data
        public List<EFITField> EFITs = []; // Effect Data
                                           // TES4
        public List<SCITField> SCITs = []; // Script effect data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "FULL" => SCITs.Count == 0 ? FULL = r.ReadSTRV(dataSize) : SCITs.Last().FULLField(r, dataSize),
            "ENIT" or "ENDT" => ENIT = new ENITField(r, dataSize, format),
            "EFID" => r.Skip(dataSize),
            "EFIT" or "ENAM" => EFITs.AddX(new EFITField(r, dataSize, format)),
            "SCIT" => SCITs.AddX(new SCITField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : FACT.Faction

    public class FACTRecord : Record
    {
        // TESX
        public class RNAMGroup
        {
            public override string ToString() => $"{RNAM.Value}:{MNAM.Value}";
            public IN32Field RNAM; // rank
            public STRVField MNAM; // male
            public STRVField FNAM; // female
            public STRVField INAM; // insignia
        }

        // TES3
        public struct FADTField
        {
            public FADTField(BinaryReader r, int dataSize) => r.Skip(dataSize);
        }

        // TES4
        public struct XNAMField(BinaryReader r, int dataSize, BethesdaFormat format)
        {
            public override string ToString() => $"{FormId}";
            public int FormId = r.ReadInt32();
            public int Mod = r.ReadInt32();
            public int Combat = format > BethesdaFormat.TES4 ? r.ReadInt32() : 0;
        }

        public STRVField FNAM; // Faction name
        public List<RNAMGroup> RNAMs = []; // Rank Name
        public FADTField FADT; // Faction data
        public List<STRVField> ANAMs = []; // Faction name
        public List<INTVField> INTVs = []; // Faction reaction
                                           // TES4
        public XNAMField XNAM; // Interfaction Relations
        public INTVField DATA; // Flags (byte, uint32)
        public UI32Field CNAM;

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "FNAM" => FNAM = r.ReadSTRV(dataSize),
                "RNAM" => RNAMs.AddX(new RNAMGroup { MNAM = r.ReadSTRV(dataSize) }),
                "FADT" => FADT = new FADTField(r, dataSize),
                "ANAM" => ANAMs.AddX(r.ReadSTRV(dataSize)),
                "INTV" => INTVs.AddX(r.ReadINTV(dataSize)),
                _ => Empty,
            }
            : type switch
            {
                "EDID" => EDID = r.ReadSTRV(dataSize),
                "FULL" => FNAM = r.ReadSTRV(dataSize),
                "XNAM" => XNAM = new XNAMField(r, dataSize, format),
                "DATA" => DATA = r.ReadINTV(dataSize),
                "CNAM" => CNAM = r.ReadT<UI32Field>(dataSize),
                "RNAM" => RNAMs.AddX(new RNAMGroup { RNAM = r.ReadT<IN32Field>(dataSize) }),
                "MNAM" => RNAMs.Last().MNAM = r.ReadSTRV(dataSize),
                "FNAM" => RNAMs.Last().FNAM = r.ReadSTRV(dataSize),
                "INAM" => RNAMs.Last().INAM = r.ReadSTRV(dataSize),
                _ => Empty,
            };
    }

    #endregion

    #region 3450 : GLOB.Global

    public class GLOBRecord : Record
    {
        public BYTEField? FNAM; // Type of global (s, l, f)
        public FLTVField? FLTV; // Float data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "FNAM" => FNAM = r.ReadT<BYTEField>(dataSize),
            "FLTV" => FLTV = r.ReadT<FLTVField>(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : GMST.Game Setting

    public class GMSTRecord : Record
    {
        public DATVField DATA; // Data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "STRV" => DATA = r.ReadDATV(dataSize, 's'),
                "INTV" => DATA = r.ReadDATV(dataSize, 'i'),
                "FLTV" => DATA = r.ReadDATV(dataSize, 'f'),
                _ => Empty,
            }
            : type switch
            {
                "EDID" => EDID = r.ReadSTRV(dataSize),
                "DATA" => DATA = r.ReadDATV(dataSize, EDID.Value[0]),
                _ => Empty,
            };
    }

    #endregion

    #region 3450 : INFO.Dialog Topic Info

    public class INFORecord : Record
    {
        // TES3
        public struct DATA3Field(BinaryReader r, int dataSize)
        {
            public int Unknown1 = r.ReadInt32();
            public int Disposition = r.ReadInt32();
            public byte Rank = r.ReadByte(); // (0-10)
            public byte Gender = r.ReadByte(); // 0xFF = None, 0x00 = Male, 0x01 = Female
            public byte PCRank = r.ReadByte(); // (0-10)
            public byte Unknown2 = r.ReadByte();
        }

        public class TES3Group
        {
            public STRVField NNAM; // Next info ID (form a linked list of INFOs for the DIAL). First INFO has an empty PNAM, last has an empty NNAM.
            public DATA3Field DATA; // Info data
            public STRVField ONAM; // Actor
            public STRVField RNAM; // Race
            public STRVField CNAM; // Class
            public STRVField FNAM; // Faction 
            public STRVField ANAM; // Cell
            public STRVField DNAM; // PC Faction
            public STRVField NAME; // The info response string (512 max)
            public FILEField SNAM; // Sound
            public BYTEField QSTN; // Journal Name
            public BYTEField QSTF; // Journal Finished
            public BYTEField QSTR; // Journal Restart
            public SCPTRecord.CTDAField SCVR; // String for the function/variable choice
            public UNKNField INTV; //
            public UNKNField FLTV; // The function/variable result for the previous SCVR
            public STRVField BNAM; // Result text (not compiled)
        }

        // TES4
        public struct DATA4Field(BinaryReader r, int dataSize)
        {
            public byte Type = r.ReadByte();
            public byte NextSpeaker = r.ReadByte();
            public byte Flags = dataSize == 3 ? r.ReadByte() : (byte)0;
        }

        public class TRDTField
        {
            public uint EmotionType;
            public int EmotionValue;
            public byte ResponseNumber;
            public string ResponseText;
            public string ActorNotes;

            public TRDTField(BinaryReader r, int dataSize)
            {
                EmotionType = r.ReadUInt32();
                EmotionValue = r.ReadInt32();
                r.Skip(4); // Unused
                ResponseNumber = r.ReadByte();
                r.Skip(3); // Unused
            }
            public object NAM1Field(BinaryReader r, int dataSize) => ResponseText = r.ReadYEncoding(dataSize);
            public object NAM2Field(BinaryReader r, int dataSize) => ActorNotes = r.ReadYEncoding(dataSize);
        }

        public class TES4Group
        {
            public DATA4Field DATA; // Info data
            public FMIDField<QUSTRecord> QSTI; // Quest
            public FMIDField<DIALRecord> TPIC; // Topic
            public List<FMIDField<DIALRecord>> NAMEs = []; // Topics
            public List<TRDTField> TRDTs = []; // Responses
            public List<SCPTRecord.CTDAField> CTDAs = []; // Conditions
            public List<FMIDField<DIALRecord>> TCLTs = []; // Choices
            public List<FMIDField<DIALRecord>> TCLFs = []; // Link From Topics
            public SCPTRecord.SCHRField SCHR; // Script Data
            public BYTVField SCDA; // Compiled Script
            public STRVField SCTX; // Script Source
            public List<FMIDField<Record>> SCROs = []; // Global variable reference
        }

        public FMIDField<INFORecord> PNAM; // Previous info ID
        public TES3Group TES3 = new();
        public TES4Group TES4 = new();

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "INAM" => (DIALRecord.LastRecord?.INFOs.AddX(this), EDID = r.ReadSTRV(dataSize)),
                "PNAM" => PNAM = new FMIDField<INFORecord>(r, dataSize),
                "NNAM" => TES3.NNAM = r.ReadSTRV(dataSize),
                "DATA" => TES3.DATA = new DATA3Field(r, dataSize),
                "ONAM" => TES3.ONAM = r.ReadSTRV(dataSize),
                "RNAM" => TES3.RNAM = r.ReadSTRV(dataSize),
                "CNAM" => TES3.CNAM = r.ReadSTRV(dataSize),
                "FNAM" => TES3.FNAM = r.ReadSTRV(dataSize),
                "ANAM" => TES3.ANAM = r.ReadSTRV(dataSize),
                "DNAM" => TES3.DNAM = r.ReadSTRV(dataSize),
                "NAME" => TES3.NAME = r.ReadSTRV(dataSize),
                "SNAM" => TES3.SNAM = r.ReadFILE(dataSize),
                "QSTN" => TES3.QSTN = r.ReadT<BYTEField>(dataSize),
                "QSTF" => TES3.QSTF = r.ReadT<BYTEField>(dataSize),
                "QSTR" => TES3.QSTR = r.ReadT<BYTEField>(dataSize),
                "SCVR" => TES3.SCVR = new SCPTRecord.CTDAField(r, dataSize, format),
                "INTV" => TES3.INTV = r.ReadUNKN(dataSize),
                "FLTV" => TES3.FLTV = r.ReadUNKN(dataSize),
                "BNAM" => TES3.BNAM = r.ReadSTRV(dataSize),
                _ => Empty,
            }
            : type switch
            {
                "DATA" => TES4.DATA = new DATA4Field(r, dataSize),
                "QSTI" => TES4.QSTI = new FMIDField<QUSTRecord>(r, dataSize),
                "TPIC" => TES4.TPIC = new FMIDField<DIALRecord>(r, dataSize),
                "NAME" => TES4.NAMEs.AddX(new FMIDField<DIALRecord>(r, dataSize)),
                "TRDT" => TES4.TRDTs.AddX(new TRDTField(r, dataSize)),
                "NAM1" => TES4.TRDTs.Last().NAM1Field(r, dataSize),
                "NAM2" => TES4.TRDTs.Last().NAM2Field(r, dataSize),
                "CTDA" or "CTDT" => TES4.CTDAs.AddX(new SCPTRecord.CTDAField(r, dataSize, format)),
                "TCLT" => TES4.TCLTs.AddX(new FMIDField<DIALRecord>(r, dataSize)),
                "TCLF" => TES4.TCLFs.AddX(new FMIDField<DIALRecord>(r, dataSize)),
                "SCHR" or "SCHD" => TES4.SCHR = new SCPTRecord.SCHRField(r, dataSize),
                "SCDA" => TES4.SCDA = r.ReadBYTV(dataSize),
                "SCTX" => TES4.SCTX = r.ReadSTRV(dataSize),
                "SCRO" => TES4.SCROs.AddX(new FMIDField<Record>(r, dataSize)),
                _ => Empty,
            };
    }

    #endregion

    #region 3450 : INGR.Ingredient

    public class INGRRecord : Record, IHaveMODL
    {
        // TES3
        public struct IRDTField
        {
            public float Weight;
            public int Value;
            public int[] EffectId; // 0 or -1 means no effect
            public int[] SkillId; // only for Skill related effects, 0 or -1 otherwise
            public int[] AttributeId; // only for Attribute related effects, 0 or -1 otherwise

            public IRDTField(BinaryReader r, int dataSize)
            {
                Weight = r.ReadSingle();
                Value = r.ReadInt32();
                EffectId = new int[4];
                for (var i = 0; i < EffectId.Length; i++) EffectId[i] = r.ReadInt32();
                SkillId = new int[4];
                for (var i = 0; i < SkillId.Length; i++) SkillId[i] = r.ReadInt32();
                AttributeId = new int[4];
                for (var i = 0; i < AttributeId.Length; i++) AttributeId[i] = r.ReadInt32();
            }
        }

        // TES4
        public class DATAField(BinaryReader r, int dataSize)
        {
            public float Weight = r.ReadSingle();
            public int Value;
            public uint Flags;

            public object ENITField(BinaryReader r, int dataSize)
            {
                Value = r.ReadInt32();
                Flags = r.ReadUInt32();
                return Value;
            }
        }

        public MODLGroup MODL { get; set; } // Model Name
        public STRVField FULL; // Item Name
        public IRDTField IRDT; // Ingrediant Data //: TES3
        public DATAField DATA; // Ingrediant Data //: TES4
        public FILEField ICON; // Inventory Icon
        public FMIDField<SCPTRecord> SCRI; // Script Name
        // TES4
        public List<ENCHRecord.EFITField> EFITs = []; // Effect Data
        public List<ENCHRecord.SCITField> SCITs = []; // Script effect data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" => SCITs.Count == 0 ? FULL = r.ReadSTRV(dataSize) : SCITs.Last().FULLField(r, dataSize),
            "FNAM" => FULL = r.ReadSTRV(dataSize),
            "DATA" => DATA = new DATAField(r, dataSize),
            "IRDT" => IRDT = new IRDTField(r, dataSize),
            "ICON" or "ITEX" => ICON = r.ReadFILE(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            //
            "ENIT" => DATA.ENITField(r, dataSize),
            "EFID" => r.Skip(dataSize),
            "EFIT" => EFITs.AddX(new ENCHRecord.EFITField(r, dataSize, format)),
            "SCIT" => SCITs.AddX(new ENCHRecord.SCITField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : LAND.Land

    public unsafe class LANDRecord : Record
    {
        // TESX
        public struct VNMLField(BinaryReader r, int dataSize)
        {
            public Byte3[] Vertexs = r.ReadTArray<Byte3>(dataSize, dataSize / 3); // XYZ 8 bit floats
        }

        public struct VHGTField
        {
            public float ReferenceHeight; // A height offset for the entire cell. Decreasing this value will shift the entire cell land down.
            public sbyte[] HeightData; // HeightData

            public VHGTField(BinaryReader r, int dataSize)
            {
                ReferenceHeight = r.ReadSingle();
                var count = dataSize - 4 - 3;
                HeightData = r.ReadTArray<sbyte>(count, count);
                r.Skip(3); // Unused
            }
        }

        public struct VCLRField(BinaryReader r, int dataSize)
        {
            public ColorRef3[] Colors = r.ReadTArray<ColorRef3>(dataSize, dataSize / 24); // 24-bit RGB
        }

        public struct VTEXField
        {
            public ushort[] TextureIndicesT3;
            public uint[] TextureIndicesT4;

            public VTEXField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    TextureIndicesT3 = r.ReadTArray<ushort>(dataSize, dataSize >> 1);
                    TextureIndicesT4 = null;
                    return;
                }
                TextureIndicesT3 = null;
                TextureIndicesT4 = r.ReadTArray<uint>(dataSize, dataSize >> 2);
            }
        }

        // TES3
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct CORDField
        {
            public static (string, int) Struct = ("<ii", sizeof(CORDField));
            public int CellX;
            public int CellY;
            public override readonly string ToString() => $"{CellX},{CellY}";
        }

        public struct WNAMField
        {
            // Low-LOD heightmap (signed chars)
            public WNAMField(BinaryReader r, int dataSize)
            {
                r.Skip(dataSize);
                //var heightCount = dataSize;
                //for (var i = 0; i < heightCount; i++) { var height = r.ReadByte(); }
            }
        }

        // TES4
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BTXTField
        {
            public static (string, int) Struct = ("<Icch", sizeof(BTXTField));
            public uint Texture;
            public byte Quadrant;
            public byte Pad01;
            public short Layer;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct VTXTField
        {
            public ushort Position;
            public ushort Pad01;
            public float Opacity;
        }

        public class ATXTGroup
        {
            public BTXTField ATXT;
            public VTXTField[] VTXTs;
        }

        public override string ToString() => $"LAND: {INTV}";
        public IN32Field DATA; // Unknown (default of 0x09) Changing this value makes the land 'disappear' in the editor.
        // A RGB color map 65x65 pixels in size representing the land normal vectors.
        // The signed value of the 'color' represents the vector's component. Blue
        // is vertical(Z), Red the X direction and Green the Y direction.Note that
        // the y-direction of the data is from the bottom up.
        public VNMLField VNML;
        public VHGTField VHGT; // Height data
        public VNMLField? VCLR; // Vertex color array, looks like another RBG image 65x65 pixels in size. (Optional)
        public VTEXField? VTEX; // A 16x16 array of short texture indices. (Optional)
        // TES3
        public CORDField INTV; // The cell coordinates of the cell
        public WNAMField WNAM; // Unknown byte data.
        // TES4
        public BTXTField[] BTXTs = new BTXTField[4]; // Base Layer
        public ATXTGroup[] ATXTs; // Alpha Layer
        ATXTGroup _lastATXT;

        public Int3 GridId; // => new Int3(INTV.CellX, INTV.CellY, 0);

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "DATA" => DATA = r.ReadSAndVerify<IN32Field>(dataSize),
            "VNML" => VNML = new VNMLField(r, dataSize),
            "VHGT" => VHGT = new VHGTField(r, dataSize),
            "VCLR" => VCLR = new VNMLField(r, dataSize),
            "VTEX" => VTEX = new VTEXField(r, dataSize, format),
            // TES3
            "INTV" => INTV = r.ReadSAndVerify<CORDField>(dataSize),
            "WNAM" => WNAM = new WNAMField(r, dataSize),
            // TES4
            "BTXT" => this.Then(r.ReadSAndVerify<BTXTField>(dataSize), btxt => BTXTs[btxt.Quadrant] = btxt),
            "ATXT" => (ATXTs ??= new ATXTGroup[4], this.Then(r.ReadSAndVerify<BTXTField>(dataSize), atxt => _lastATXT = ATXTs[atxt.Quadrant] = new ATXTGroup { ATXT = atxt })),
            "VTXT" => _lastATXT.VTXTs = r.ReadTArray<VTXTField>(dataSize, dataSize >> 3),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : LIGH.Light

    public class LIGHRecord : Record, IHaveMODL
    {
        // TESX
        public struct DATAField
        {
            public enum ColorFlags
            {
                Dynamic = 0x0001,
                CanCarry = 0x0002,
                Negative = 0x0004,
                Flicker = 0x0008,
                Fire = 0x0010,
                OffDefault = 0x0020,
                FlickerSlow = 0x0040,
                Pulse = 0x0080,
                PulseSlow = 0x0100
            }

            public float Weight;
            public int Value;
            public int Time;
            public int Radius;
            public ColorRef4 LightColor;
            public int Flags;
            // TES4
            public float FalloffExponent;
            public float FOV;

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    Weight = r.ReadSingle();
                    Value = r.ReadInt32();
                    Time = r.ReadInt32();
                    Radius = r.ReadInt32();
                    LightColor = r.ReadSAndVerify<ColorRef4>(4);
                    Flags = r.ReadInt32();
                    FalloffExponent = 1;
                    FOV = 90;
                    return;
                }
                Time = r.ReadInt32();
                Radius = r.ReadInt32();
                LightColor = r.ReadSAndVerify<ColorRef4>(4);
                Flags = r.ReadInt32();
                if (dataSize == 32) { FalloffExponent = r.ReadSingle(); FOV = r.ReadSingle(); }
                else { FalloffExponent = 1; FOV = 90; }
                Value = r.ReadInt32();
                Weight = r.ReadSingle();
            }
        }

        public MODLGroup MODL { get; set; } // Model
        public STRVField? FULL; // Item Name (optional)
        public DATAField DATA; // Light Data
        public STRVField? SCPT; // Script Name (optional)??
        public FMIDField<SCPTRecord>? SCRI; // Script FormId (optional)
        public FILEField? ICON; // Male Icon (optional)
        public FLTVField FNAM; // Fade Value
        public FMIDField<SOUNRecord> SNAM; // Sound FormId (optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "FULL" => FULL = r.ReadSTRV(dataSize),
            "FNAM" => format != BethesdaFormat.TES3 ? FNAM = r.ReadSAndVerify<FLTVField>(dataSize) : FULL = r.ReadSTRV(dataSize),
            "DATA" or "LHDT" => DATA = new DATAField(r, dataSize, format),
            "SCPT" => SCPT = r.ReadSTRV(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "ICON" or "ITEX" => ICON = r.ReadFILE(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "SNAM" => SNAM = new FMIDField<SOUNRecord>(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : LOCK.Lock

    public class LOCKRecord : Record, IHaveMODL
    {
        public struct LKDTField(BinaryReader r, int dataSize)
        {
            public float Weight = r.ReadSingle();
            public int Value = r.ReadInt32();
            public float Quality = r.ReadSingle();
            public int Uses = r.ReadInt32();
        }

        public MODLGroup MODL { get; set; } // Model Name
        public STRVField FNAM; // Item Name
        public LKDTField LKDT; // Lock Data
        public FILEField ICON; // Inventory Icon
        public FMIDField<SCPTRecord> SCRI; // Script Name

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => format == BethesdaFormat.TES3
            ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "MODL" => MODL = new MODLGroup(r, dataSize),
                "FNAM" => FNAM = r.ReadSTRV(dataSize),
                "LKDT" => LKDT = new LKDTField(r, dataSize),
                "ITEX" => ICON = r.ReadFILE(dataSize),
                "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
                _ => Empty,
            }
            : null;
    }

    #endregion

    #region 3450 : LTEX.Land Texture

    public class LTEXRecord : Record
    {
        public struct HNAMField(BinaryReader r, int dataSize)
        {
            public byte MaterialType = r.ReadByte();
            public byte Friction = r.ReadByte();
            public byte Restitution = r.ReadByte();
        }

        public FILEField ICON; // Texture
        // TES3
        public INTVField INTV;
        // TES4
        public HNAMField HNAM; // Havok data
        public BYTEField SNAM; // Texture specular exponent
        public List<FMIDField<GRASRecord>> GNAMs = []; // Potential grass

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "INTV" => INTV = r.ReadINTV(dataSize),
            "ICON" or "DATA" => ICON = r.ReadFILE(dataSize),
            // TES4
            "HNAM" => HNAM = new HNAMField(r, dataSize),
            "SNAM" => SNAM = r.ReadT<BYTEField>(dataSize),
            "GNAM" => GNAMs.AddX(new FMIDField<GRASRecord>(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : MISC.Misc Item

    public class MISCRecord : Record, IHaveMODL
    {
        // TESX
        public struct DATAField
        {
            public float Weight;
            public uint Value;
            public uint Unknown;

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    Weight = r.ReadSingle();
                    Value = r.ReadUInt32();
                    Unknown = r.ReadUInt32();
                    return;
                }
                Value = r.ReadUInt32();
                Weight = r.ReadSingle();
                Unknown = 0;
            }
        }

        public MODLGroup MODL { get; set; } // Model
        public STRVField FULL; // Item Name
        public DATAField DATA; // Misc Item Data
        public FILEField ICON; // Icon (optional)
        public FMIDField<SCPTRecord> SCRI; // Script FormID (optional)
        // TES3
        public FMIDField<ENCHRecord> ENAM; // enchantment ID

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" or "FNAM" => FULL = r.ReadSTRV(dataSize),
            "DATA" or "MCDT" => DATA = new DATAField(r, dataSize, format),
            "ICON" or "ITEX" => ICON = r.ReadFILE(dataSize),
            "ENAM" => ENAM = new FMIDField<ENCHRecord>(r, dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : NPC_.Non-Player Character

    public class NPC_Record : Record, IHaveMODL
    {
        [Flags]
        public enum NPC_Flags : uint
        {
            Female = 0x0001,
            Essential = 0x0002,
            Respawn = 0x0004,
            None = 0x0008,
            Autocalc = 0x0010,
            BloodSkel = 0x0400,
            BloodMetal = 0x0800,
        }

        public class NPDTField
        {
            public short Level;
            public byte Strength;
            public byte Intelligence;
            public byte Willpower;
            public byte Agility;
            public byte Speed;
            public byte Endurance;
            public byte Personality;
            public byte Luck;
            public byte[] Skills;
            public byte Reputation;
            public short Health;
            public short SpellPts;
            public short Fatigue;
            public byte Disposition;
            public byte FactionId;
            public byte Rank;
            public byte Unknown1;
            public int Gold;

            // 12 byte version
            //public short Level;
            //public byte Disposition;
            //public byte FactionId;
            //public byte Rank;
            //public byte Unknown1;
            public byte Unknown2;
            public byte Unknown3;
            //public int Gold;

            public NPDTField(BinaryReader r, int dataSize)
            {
                if (dataSize == 52)
                {
                    Level = r.ReadInt16();
                    Strength = r.ReadByte();
                    Intelligence = r.ReadByte();
                    Willpower = r.ReadByte();
                    Agility = r.ReadByte();
                    Speed = r.ReadByte();
                    Endurance = r.ReadByte();
                    Personality = r.ReadByte();
                    Luck = r.ReadByte();
                    Skills = r.ReadBytes(27);
                    Reputation = r.ReadByte();
                    Health = r.ReadInt16();
                    SpellPts = r.ReadInt16();
                    Fatigue = r.ReadInt16();
                    Disposition = r.ReadByte();
                    FactionId = r.ReadByte();
                    Rank = r.ReadByte();
                    Unknown1 = r.ReadByte();
                    Gold = r.ReadInt32();
                }
                else
                {
                    Level = r.ReadInt16();
                    Disposition = r.ReadByte();
                    FactionId = r.ReadByte();
                    Rank = r.ReadByte();
                    Unknown1 = r.ReadByte();
                    Unknown2 = r.ReadByte();
                    Unknown3 = r.ReadByte();
                    Gold = r.ReadInt32();
                }
            }
        }

        public struct DODTField(BinaryReader r, int dataSize)
        {
            public float XPos = r.ReadSingle();
            public float YPos = r.ReadSingle();
            public float ZPos = r.ReadSingle();
            public float XRot = r.ReadSingle();
            public float YRot = r.ReadSingle();
            public float ZRot = r.ReadSingle();
        }

        public STRVField FULL; // NPC name
        public MODLGroup MODL { get; set; } // Animation
        public STRVField RNAM; // Race Name
        public STRVField ANAM; // Faction name
        public STRVField BNAM; // Head model
        public STRVField CNAM; // Class name
        public STRVField KNAM; // Hair model
        public NPDTField NPDT; // NPC Data
        public INTVField FLAG; // NPC Flags
        public List<CNTOField> NPCOs = new List<CNTOField>(); // NPC item
        public List<STRVField> NPCSs = new List<STRVField>(); // NPC spell
        public CREARecord.AIDTField AIDT; // AI data
        public CREARecord.AI_WField? AI_W; // AI
        public CREARecord.AI_TField? AI_T; // AI Travel
        public CREARecord.AI_FField? AI_F; // AI Follow
        public CREARecord.AI_FField? AI_E; // AI Escort
        public STRVField? CNDT; // Cell escort/follow to string (optional)
        public CREARecord.AI_AField? AI_A; // AI Activate
        public DODTField DODT; // Cell Travel Destination
        public STRVField DNAM; // Cell name for previous DODT, if interior
        public FLTVField? XSCL; // Scale (optional) Only present if the scale is not 1.0
        public FMIDField<SCPTRecord>? SCRI; // Unknown

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "FULL" or "FNAM" => FULL = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "RNAM" => RNAM = r.ReadSTRV(dataSize),
            "ANAM" => ANAM = r.ReadSTRV(dataSize),
            "BNAM" => BNAM = r.ReadSTRV(dataSize),
            "CNAM" => CNAM = r.ReadSTRV(dataSize),
            "KNAM" => KNAM = r.ReadSTRV(dataSize),
            "NPDT" => NPDT = new NPDTField(r, dataSize),
            "FLAG" => FLAG = r.ReadINTV(dataSize),
            "NPCO" => NPCOs.AddX(new CNTOField(r, dataSize, format)),
            "NPCS" => NPCSs.AddX(r.ReadSTRV_ZPad(dataSize)),
            "AIDT" => AIDT = new CREARecord.AIDTField(r, dataSize),
            "AI_W" => AI_W = new CREARecord.AI_WField(r, dataSize),
            "AI_T" => AI_T = new CREARecord.AI_TField(r, dataSize),
            "AI_F" => AI_F = new CREARecord.AI_FField(r, dataSize),
            "AI_E" => AI_E = new CREARecord.AI_FField(r, dataSize),
            "CNDT" => CNDT = r.ReadSTRV(dataSize),
            "AI_A" => AI_A = new CREARecord.AI_AField(r, dataSize),
            "DODT" => DODT = new DODTField(r, dataSize),
            "DNAM" => DNAM = r.ReadSTRV(dataSize),
            "XSCL" => XSCL = r.ReadSAndVerify<FLTVField>(dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : RACE.Race_Creature type

    public class RACERecord : Record
    {
        // TESX
        public class DATAField
        {
            public enum RaceFlag : uint
            {
                Playable = 0x00000001,
                FaceGenHead = 0x00000002,
                Child = 0x00000004,
                TiltFrontBack = 0x00000008,
                TiltLeftRight = 0x00000010,
                NoShadow = 0x00000020,
                Swims = 0x00000040,
                Flies = 0x00000080,
                Walks = 0x00000100,
                Immobile = 0x00000200,
                NotPushable = 0x00000400,
                NoCombatInWater = 0x00000800,
                NoRotatingToHeadTrack = 0x00001000,
                DontShowBloodSpray = 0x00002000,
                DontShowBloodDecal = 0x00004000,
                UsesHeadTrackAnims = 0x00008000,
                SpellsAlignWMagicNode = 0x00010000,
                UseWorldRaycastsForFootIK = 0x00020000,
                AllowRagdollCollision = 0x00040000,
                RegenHPInCombat = 0x00080000,
                CantOpenDoors = 0x00100000,
                AllowPCDialogue = 0x00200000,
                NoKnockdowns = 0x00400000,
                AllowPickpocket = 0x00800000,
                AlwaysUseProxyController = 0x01000000,
                DontShowWeaponBlood = 0x02000000,
                OverlayHeadPartList = 0x04000000, //{> Only one can be active <}
                OverrideHeadPartList = 0x08000000, //{> Only one can be active <}
                CanPickupItems = 0x10000000,
                AllowMultipleMembraneShaders = 0x20000000,
                CanDualWield = 0x40000000,
                AvoidsRoads = 0x80000000,
            }

            public struct SkillBoost
            {
                public byte SkillId;
                public sbyte Bonus;

                public SkillBoost(BinaryReader r, int dataSize, BethesdaFormat format)
                {
                    if (format == BethesdaFormat.TES3)
                    {
                        SkillId = (byte)r.ReadInt32();
                        Bonus = (sbyte)r.ReadInt32();
                        return;
                    }
                    SkillId = r.ReadByte();
                    Bonus = r.ReadSByte();
                }
            }

            public struct RaceStats
            {
                public float Height;
                public float Weight;
                // Attributes;
                public byte Strength;
                public byte Intelligence;
                public byte Willpower;
                public byte Agility;
                public byte Speed;
                public byte Endurance;
                public byte Personality;
                public byte Luck;
            }

            public SkillBoost[] SkillBoosts = new SkillBoost[7]; // Skill Boosts
            public RaceStats Male = new();
            public RaceStats Female = new();
            public uint Flags; // 1 = Playable 2 = Beast Race

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    for (var i = 0; i < SkillBoosts.Length; i++) SkillBoosts[i] = new SkillBoost(r, 8, format);
                    Male.Strength = (byte)r.ReadInt32(); Female.Strength = (byte)r.ReadInt32();
                    Male.Intelligence = (byte)r.ReadInt32(); Female.Intelligence = (byte)r.ReadInt32();
                    Male.Willpower = (byte)r.ReadInt32(); Female.Willpower = (byte)r.ReadInt32();
                    Male.Agility = (byte)r.ReadInt32(); Female.Agility = (byte)r.ReadInt32();
                    Male.Speed = (byte)r.ReadInt32(); Female.Speed = (byte)r.ReadInt32();
                    Male.Endurance = (byte)r.ReadInt32(); Female.Endurance = (byte)r.ReadInt32();
                    Male.Personality = (byte)r.ReadInt32(); Female.Personality = (byte)r.ReadInt32();
                    Male.Luck = (byte)r.ReadInt32(); Female.Luck = (byte)r.ReadInt32();
                    Male.Height = r.ReadSingle(); Female.Height = r.ReadSingle();
                    Male.Weight = r.ReadSingle(); Female.Weight = r.ReadSingle();
                    Flags = r.ReadUInt32();
                    return;
                }
                for (var i = 0; i < SkillBoosts.Length; i++) SkillBoosts[i] = new SkillBoost(r, 2, format);
                r.ReadInt16(); // padding
                Male.Height = r.ReadSingle(); Female.Height = r.ReadSingle();
                Male.Weight = r.ReadSingle(); Female.Weight = r.ReadSingle();
                Flags = r.ReadUInt32();
            }

            public object ATTRField(BinaryReader r, int dataSize)
            {
                Male.Strength = r.ReadByte();
                Male.Intelligence = r.ReadByte();
                Male.Willpower = r.ReadByte();
                Male.Agility = r.ReadByte();
                Male.Speed = r.ReadByte();
                Male.Endurance = r.ReadByte();
                Male.Personality = r.ReadByte();
                Male.Luck = r.ReadByte();
                Female.Strength = r.ReadByte();
                Female.Intelligence = r.ReadByte();
                Female.Willpower = r.ReadByte();
                Female.Agility = r.ReadByte();
                Female.Speed = r.ReadByte();
                Female.Endurance = r.ReadByte();
                Female.Personality = r.ReadByte();
                Female.Luck = r.ReadByte();
                return this;
            }
        }

        // TES4
        public class FacePartGroup
        {
            public enum Indx : uint { Head, Ear_Male, Ear_Female, Mouth, Teeth_Lower, Teeth_Upper, Tongue, Eye_Left, Eye_Right, }
            public UI32Field INDX;
            public MODLGroup MODL;
            public FILEField ICON;
        }

        public class BodyPartGroup
        {
            public enum Indx : uint { UpperBody, LowerBody, Hand, Foot, Tail }
            public UI32Field INDX;
            public FILEField ICON;
        }

        public class BodyGroup
        {
            public FILEField MODL;
            public FLTVField MODB;
            public List<BodyPartGroup> BodyParts = [];
        }

        public STRVField FULL; // Race name
        public STRVField DESC; // Race description
        public List<STRVField> SPLOs = []; // NPCs: Special power/ability name
        // TESX
        public DATAField DATA; // RADT:DATA/ATTR: Race data/Base Attributes
        // TES4
        public FMID2Field<RACERecord> VNAM; // Voice
        public FMID2Field<HAIRRecord> DNAM; // Default Hair
        public BYTEField CNAM; // Default Hair Color
        public FLTVField PNAM; // FaceGen - Main clamp
        public FLTVField UNAM; // FaceGen - Face clamp
        public UNKNField XNAM; // Unknown
        //
        public List<FMIDField<HAIRRecord>> HNAMs = [];
        public List<FMIDField<EYESRecord>> ENAMs = [];
        public BYTVField FGGS; // FaceGen Geometry-Symmetric
        public BYTVField FGGA; // FaceGen Geometry-Asymmetric
        public BYTVField FGTS; // FaceGen Texture-Symmetric
        public UNKNField SNAM; // Unknown

        // Parts
        public List<FacePartGroup> FaceParts = [];
        public BodyGroup[] Bodys = [new BodyGroup(), new BodyGroup()];
        sbyte _nameState;
        sbyte _genderState;

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) =>
            format == BethesdaFormat.TES3 ? type switch
            {
                "NAME" => EDID = r.ReadSTRV(dataSize),
                "FNAM" => FULL = r.ReadSTRV(dataSize),
                "RADT" => DATA = new DATAField(r, dataSize, format),
                "NPCS" => SPLOs.AddX(r.ReadSTRV(dataSize)),
                "DESC" => DESC = r.ReadSTRV(dataSize),
                _ => Empty,
            }
            : format == BethesdaFormat.TES4 ? _nameState switch
            {
                // preamble
                0 => type switch
                {
                    "EDID" => EDID = r.ReadSTRV(dataSize),
                    "FULL" => FULL = r.ReadSTRV(dataSize),
                    "DESC" => DESC = r.ReadSTRV(dataSize),
                    "DATA" => DATA = new DATAField(r, dataSize, format),
                    "SPLO" => SPLOs.AddX(r.ReadSTRV(dataSize)),
                    "VNAM" => VNAM = new FMID2Field<RACERecord>(r, dataSize),
                    "DNAM" => DNAM = new FMID2Field<HAIRRecord>(r, dataSize),
                    "CNAM" => CNAM = r.ReadSAndVerify<BYTEField>(dataSize),
                    "PNAM" => PNAM = r.ReadSAndVerify<FLTVField>(dataSize),
                    "UNAM" => UNAM = r.ReadSAndVerify<FLTVField>(dataSize),
                    "XNAM" => XNAM = r.ReadUNKN(dataSize),
                    "ATTR" => DATA.ATTRField(r, dataSize),
                    "NAM0" => _nameState++,
                    _ => Empty,
                },
                // face data
                1 => type switch
                {
                    "INDX" => FaceParts.AddX(new FacePartGroup { INDX = r.ReadSAndVerify<UI32Field>(dataSize) }),
                    "MODL" => FaceParts.Last().MODL = new MODLGroup(r, dataSize),
                    "ICON" => FaceParts.Last().ICON = r.ReadFILE(dataSize),
                    "MODB" => FaceParts.Last().MODL.MODBField(r, dataSize),
                    "NAM1" => _nameState++,
                    _ => Empty,
                },
                // body data
                2 => type switch
                {
                    "MNAM" => _genderState = 0,
                    "FNAM" => _genderState = 1,
                    "MODL" => Bodys[_genderState].MODL = r.ReadFILE(dataSize),
                    "MODB" => Bodys[_genderState].MODB = r.ReadSAndVerify<FLTVField>(dataSize),
                    "INDX" => Bodys[_genderState].BodyParts.AddX(new BodyPartGroup { INDX = r.ReadSAndVerify<UI32Field>(dataSize) }),
                    "ICON" => Bodys[_genderState].BodyParts.Last().ICON = r.ReadFILE(dataSize),
                    "HNAM" => (_nameState++, HNAMs.AddRangeX(Enumerable.Range(0, dataSize >> 2).Select(x => new FMIDField<HAIRRecord>(r, 4)))),
                    _ => Empty,
                },
                // postamble
                3 => type switch
                {
                    "HNAM" => HNAMs.AddRangeX(Enumerable.Range(0, dataSize >> 2).Select(x => new FMIDField<HAIRRecord>(r, 4))),
                    "ENAM" => ENAMs.AddRangeX(Enumerable.Range(0, dataSize >> 2).Select(x => new FMIDField<EYESRecord>(r, 4))),
                    "FGGS" => FGGS = r.ReadBYTV(dataSize),
                    "FGGA" => FGGA = r.ReadBYTV(dataSize),
                    "FGTS" => FGTS = r.ReadBYTV(dataSize),
                    "SNAM" => SNAM = r.ReadUNKN(dataSize),
                    _ => Empty,
                },
                _ => Empty,
            }
            : null;
    }

    #endregion

    #region 3450 : REGN.Region

    public class REGNRecord : Record
    {
        // TESX
        public class RDATField
        {
            public enum REGNType : byte { Objects = 2, Weather, Map, Landscape, Grass, Sound }

            public uint Type;
            public REGNType Flags;
            public byte Priority;
            // groups
            public RDOTField[] RDOTs; // Objects
            public STRVField RDMP; // MapName
            public RDGSField[] RDGSs; // Grasses
            public UI32Field RDMD; // Music Type
            public RDSDField[] RDSDs; // Sounds
            public RDWTField[] RDWTs; // Weather Types

            public RDATField() { }
            public RDATField(BinaryReader r, int dataSize)
            {
                Type = r.ReadUInt32();
                Flags = (REGNType)r.ReadByte();
                Priority = r.ReadByte();
                r.Skip(2); // Unused
            }
        }

        public struct RDOTField
        {
            public override readonly string ToString() => $"{Object}";
            public FormId<Record> Object;
            public ushort ParentIdx;
            public float Density;
            public byte Clustering;
            public byte MinSlope; // (degrees)
            public byte MaxSlope; // (degrees)
            public byte Flags;
            public ushort RadiusWrtParent;
            public ushort Radius;
            public float MinHeight;
            public float MaxHeight;
            public float Sink;
            public float SinkVariance;
            public float SizeVariance;
            public Int3 AngleVariance;
            public ColorRef4 VertexShading; // RGB + Shading radius (0 - 200) %

            public RDOTField(BinaryReader r, int dataSize)
            {
                Object = new FormId<Record>(r.ReadUInt32());
                ParentIdx = r.ReadUInt16();
                r.Skip(2); // Unused
                Density = r.ReadSingle();
                Clustering = r.ReadByte();
                MinSlope = r.ReadByte();
                MaxSlope = r.ReadByte();
                Flags = r.ReadByte();
                RadiusWrtParent = r.ReadUInt16();
                Radius = r.ReadUInt16();
                MinHeight = r.ReadSingle();
                MaxHeight = r.ReadSingle();
                Sink = r.ReadSingle();
                SinkVariance = r.ReadSingle();
                SizeVariance = r.ReadSingle();
                AngleVariance = new Int3(r.ReadUInt16(), r.ReadUInt16(), r.ReadUInt16());
                r.Skip(2); // Unused
                VertexShading = r.ReadSAndVerify<ColorRef4>(dataSize);
            }
        }

        public struct RDGSField
        {
            public override readonly string ToString() => $"{Grass}";
            public FormId<GRASRecord> Grass;

            public RDGSField(BinaryReader r, int dataSize)
            {
                Grass = new FormId<GRASRecord>(r.ReadUInt32());
                r.Skip(4); // Unused
            }
        }

        public struct RDSDField
        {
            public override readonly string ToString() => $"{Sound}";
            public FormId<SOUNRecord> Sound;
            public uint Flags;
            public uint Chance;

            public RDSDField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    Sound = new FormId<SOUNRecord>(r.ReadZString(32));
                    Flags = 0;
                    Chance = r.ReadByte();
                    return;
                }
                Sound = new FormId<SOUNRecord>(r.ReadUInt32());
                Flags = r.ReadUInt32();
                Chance = r.ReadUInt32(); //: float with TES5
            }
        }

        public struct RDWTField(BinaryReader r, int dataSize, BethesdaFormat format)
        {
            public override readonly string ToString() => $"{Weather}";
            public static byte SizeOf(BethesdaFormat format) => format == BethesdaFormat.TES4 ? (byte)8 : (byte)12;
            public FormId<WTHRRecord> Weather = new FormId<WTHRRecord>(r.ReadUInt32());
            public uint Chance = r.ReadUInt32();
            public FormId<GLOBRecord> Global = format == BethesdaFormat.TES5 ? new FormId<GLOBRecord>(r.ReadUInt32()) : new FormId<GLOBRecord>();
        }

        // TES3
        public struct WEATField
        {
            public byte Clear;
            public byte Cloudy;
            public byte Foggy;
            public byte Overcast;
            public byte Rain;
            public byte Thunder;
            public byte Ash;
            public byte Blight;

            public WEATField(BinaryReader r, int dataSize)
            {
                Clear = r.ReadByte();
                Cloudy = r.ReadByte();
                Foggy = r.ReadByte();
                Overcast = r.ReadByte();
                Rain = r.ReadByte();
                Thunder = r.ReadByte();
                Ash = r.ReadByte();
                Blight = r.ReadByte();
                // v1.3 ESM files add 2 bytes to WEAT subrecords.
                if (dataSize == 10)
                    r.Skip(2);
            }
        }

        // TES4
        public class RPLIField(BinaryReader r, int dataSize)
        {
            public uint EdgeFalloff = r.ReadUInt32(); // (World Units)
            public Vector2[] Points; // Region Point List Data

            public object RPLDField(BinaryReader r, int dataSize)
            {
                Points = new Vector2[dataSize >> 3];
                for (var i = 0; i < Points.Length; i++) Points[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                return Points;
            }
        }

        public STRVField ICON; // Icon / Sleep creature
        public FMIDField<WRLDRecord> WNAM; // Worldspace - Region name
        public CREFField RCLR; // Map Color (COLORREF)
        public List<RDATField> RDATs = []; // Region Data Entries / TES3: Sound Record (order determines the sound priority)
        // TES3
        public WEATField? WEAT; // Weather Data
        // TES4
        public List<RPLIField> RPLIs = []; // Region Areas

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "WNAM" or "FNAM" => WNAM = new FMIDField<WRLDRecord>(r, dataSize),
            "WEAT" => WEAT = new WEATField(r, dataSize),//: TES3
            "ICON" or "BNAM" => ICON = r.ReadSTRV(dataSize),
            "RCLR" or "CNAM" => RCLR = r.ReadSAndVerify<CREFField>(dataSize),
            "SNAM" => RDATs.AddX(new RDATField { RDSDs = [new RDSDField(r, dataSize, format)] }),
            "RPLI" => RPLIs.AddX(new RPLIField(r, dataSize)),
            "RPLD" => RPLIs.Last().RPLDField(r, dataSize),
            "RDAT" => RDATs.AddX(new RDATField(r, dataSize)),
            "RDOT" => RDATs.Last().RDOTs = [.. Enumerable.Range(0, dataSize / 52).Select(x => new RDOTField(r, dataSize))],
            "RDMP" => RDATs.Last().RDMP = r.ReadSTRV(dataSize),
            "RDGS" => RDATs.Last().RDGSs = [.. Enumerable.Range(0, dataSize / 8).Select(x => new RDGSField(r, dataSize))],
            "RDMD" => RDATs.Last().RDMD = r.ReadSAndVerify<UI32Field>(dataSize),
            "RDSD" => RDATs.Last().RDSDs = [.. Enumerable.Range(0, dataSize / 12).Select(x => new RDSDField(r, dataSize, format))],
            "RDWT" => RDATs.Last().RDWTs = [.. Enumerable.Range(0, dataSize / RDWTField.SizeOf(format)).Select(x => new RDWTField(r, dataSize, format))],
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : SKIL.Skill

    public class SKILRecord : Record
    {
        // TESX
        public struct DATAField
        {
            public int Action;
            public int Attribute;
            public uint Specialization; // 0 = Combat, 1 = Magic, 2 = Stealth
            public float[] UseValue; // The use types for each skill are hard-coded.

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                Action = format == BethesdaFormat.TES3 ? 0 : r.ReadInt32();
                Attribute = r.ReadInt32();
                Specialization = r.ReadUInt32();
                UseValue = new float[format == BethesdaFormat.TES3 ? 4 : 2];
                for (var i = 0; i < UseValue.Length; i++) UseValue[i] = r.ReadSingle();
            }
        }

        public override string ToString() => $"SKIL: {INDX.Value}:{EDID.Value}";
        public IN32Field INDX; // Skill ID
        public DATAField DATA; // Skill Data
        public STRVField DESC; // Skill description
        // TES4
        public FILEField ICON; // Icon
        public STRVField ANAM; // Apprentice Text
        public STRVField JNAM; // Journeyman Text
        public STRVField ENAM; // Expert Text
        public STRVField MNAM; // Master Text

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" => EDID = r.ReadSTRV(dataSize),
            "INDX" => INDX = r.ReadT<IN32Field>(dataSize),
            "DATA" or "SKDT" => DATA = new DATAField(r, dataSize, format),
            "DESC" => DESC = r.ReadSTRV(dataSize),
            "ICON" => ICON = r.ReadFILE(dataSize),
            "ANAM" => ANAM = r.ReadSTRV(dataSize),
            "JNAM" => JNAM = r.ReadSTRV(dataSize),
            "ENAM" => ENAM = r.ReadSTRV(dataSize),
            "MNAM" => MNAM = r.ReadSTRV(dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : SOUN.Sound

    public class SOUNRecord : Record
    {
        [Flags]
        public enum SOUNFlags : ushort
        {
            RandomFrequencyShift = 0x0001,
            PlayAtRandom = 0x0002,
            EnvironmentIgnored = 0x0004,
            RandomLocation = 0x0008,
            Loop = 0x0010,
            MenuSound = 0x0020,
            _2D = 0x0040,
            _360LFE = 0x0080,
        }

        // TESX
        public class DATAField
        {
            public byte Volume; // (0=0.00, 255=1.00)
            public byte MinRange; // Minimum attenuation distance
            public byte MaxRange; // Maximum attenuation distance
            // Bethesda4
            public sbyte FrequencyAdjustment; // Frequency adjustment %
            public ushort Flags; // Flags
            public ushort StaticAttenuation; // Static Attenuation (db)
            public byte StopTime; // Stop time
            public byte StartTime; // Start time

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                Volume = format == BethesdaFormat.TES3 ? r.ReadByte() : (byte)0;
                MinRange = r.ReadByte();
                MaxRange = r.ReadByte();
                if (format == BethesdaFormat.TES3) return;
                FrequencyAdjustment = r.ReadSByte();
                r.ReadByte(); // Unused
                Flags = r.ReadUInt16();
                r.ReadUInt16(); // Unused
                if (dataSize == 8) return;
                StaticAttenuation = r.ReadUInt16();
                StopTime = r.ReadByte();
                StartTime = r.ReadByte();
            }
        }

        public FILEField FNAM; // Sound Filename (relative to Sounds\)
        public DATAField DATA; // Sound Data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "FNAM" => FNAM = r.ReadFILE(dataSize),
            "SNDX" => DATA = new DATAField(r, dataSize, format),
            "SNDD" => DATA = new DATAField(r, dataSize, format),
            "DATA" => DATA = new DATAField(r, dataSize, format),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : SPEL.Spell

    public class SPELRecord : Record
    {
        // TESX
        public struct SPITField(BinaryReader r, int dataSize, BethesdaFormat format)
        {
            public override readonly string ToString() => $"{Type}";
            // TES3: 0 = Spell, 1 = Ability, 2 = Blight, 3 = Disease, 4 = Curse, 5 = Power
            // TES4: 0 = Spell, 1 = Disease, 2 = Power, 3 = Lesser Power, 4 = Ability, 5 = Poison
            public uint Type = r.ReadUInt32();
            public int SpellCost = r.ReadInt32();
            public uint Flags = r.ReadUInt32(); // 0x0001 = AutoCalc, 0x0002 = PC Start, 0x0004 = Always Succeeds
            // TES4
            public int SpellLevel = format != BethesdaFormat.TES3 ? r.ReadInt32() : 0;
        }

        public STRVField FULL; // Spell name
        public SPITField SPIT; // Spell data
        public List<ENCHRecord.EFITField> EFITs = []; // Effect Data
        // TES4
        public List<ENCHRecord.SCITField> SCITs = []; // Script effect data

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "FULL" => SCITs.Count == 0 ? FULL = r.ReadSTRV(dataSize) : SCITs.Last().FULLField(r, dataSize),
            "FNAM" => FULL = r.ReadSTRV(dataSize),
            "SPIT" or "SPDT" => SPIT = new SPITField(r, dataSize, format),
            "EFID" => r.Skip(dataSize),
            "EFIT" or "ENAM" => EFITs.AddX(new ENCHRecord.EFITField(r, dataSize, format)),
            "SCIT" => SCITs.AddX(new ENCHRecord.SCITField(r, dataSize)),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : STAT.Static

    public class STATRecord : Record, IHaveMODL
    {
        public MODLGroup MODL { get; set; } // Model

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            _ => Empty,
        };
    }

    #endregion

    #region 3450 : WEAP.Weapon

    public class WEAPRecord : Record, IHaveMODL
    {
        public struct DATAField
        {
            public enum WEAPType { ShortBladeOneHand = 0, LongBladeOneHand, LongBladeTwoClose, BluntOneHand, BluntTwoClose, BluntTwoWide, SpearTwoWide, AxeOneHand, AxeTwoHand, MarksmanBow, MarksmanCrossbow, MarksmanThrown, Arrow, Bolt, }

            public float Weight;
            public int Value;
            public ushort Type;
            public short Health;
            public float Speed;
            public float Reach;
            public short Damage; //: EnchantPts;
            public byte ChopMin;
            public byte ChopMax;
            public byte SlashMin;
            public byte SlashMax;
            public byte ThrustMin;
            public byte ThrustMax;
            public int Flags; // 0 = ?, 1 = Ignore Normal Weapon Resistance?

            public DATAField(BinaryReader r, int dataSize, BethesdaFormat format)
            {
                if (format == BethesdaFormat.TES3)
                {
                    Weight = r.ReadSingle();
                    Value = r.ReadInt32();
                    Type = r.ReadUInt16();
                    Health = r.ReadInt16();
                    Speed = r.ReadSingle();
                    Reach = r.ReadSingle();
                    Damage = r.ReadInt16();
                    ChopMin = r.ReadByte();
                    ChopMax = r.ReadByte();
                    SlashMin = r.ReadByte();
                    SlashMax = r.ReadByte();
                    ThrustMin = r.ReadByte();
                    ThrustMax = r.ReadByte();
                    Flags = r.ReadInt32();
                    return;
                }
                Type = (ushort)r.ReadUInt32();
                Speed = r.ReadSingle();
                Reach = r.ReadSingle();
                Flags = r.ReadInt32();
                Value = r.ReadInt32();
                Health = (short)r.ReadInt32();
                Weight = r.ReadSingle();
                Damage = r.ReadInt16();
                ChopMin = ChopMax = SlashMin = SlashMax = ThrustMin = ThrustMax = 0;
            }
        }

        public MODLGroup MODL { get; set; } // Model
        public STRVField FULL; // Item Name
        public DATAField DATA; // Weapon Data
        public FILEField ICON; // Male Icon (optional)
        public FMIDField<ENCHRecord> ENAM; // Enchantment ID
        public FMIDField<SCPTRecord> SCRI; // Script (optional)
                                           // TES4
        public IN16Field? ANAM; // Enchantment points (optional)

        public override object CreateField(BinaryReader r, BethesdaFormat format, string type, int dataSize) => type switch
        {
            "EDID" or "NAME" => EDID = r.ReadSTRV(dataSize),
            "MODL" => MODL = new MODLGroup(r, dataSize),
            "MODB" => MODL.MODBField(r, dataSize),
            "MODT" => MODL.MODTField(r, dataSize),
            "FULL" or "FNAM" => FULL = r.ReadSTRV(dataSize),
            "DATA" or "WPDT" => DATA = new DATAField(r, dataSize, format),
            "ICON" or "ITEX" => ICON = r.ReadFILE(dataSize),
            "ENAM" => ENAM = new FMIDField<ENCHRecord>(r, dataSize),
            "SCRI" => SCRI = new FMIDField<SCPTRecord>(r, dataSize),
            "ANAM" => ANAM = r.ReadSAndVerify<IN16Field>(dataSize),
            _ => Empty,
        };
    }

    #endregion
}
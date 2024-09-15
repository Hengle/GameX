using GameX.Meta;
using GameX.Platforms;
using GameX.WB.Formats.AC.AnimationHooks;
using GameX.WB.Formats.AC.Entity;
using GameX.WB.Formats.AC.Props;
using OpenStack.Gfx.Textures;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using static GameX.WB.Formats.AC.Props.SurfacePixelFormat;

namespace GameX.WB.Formats.AC.FileTypes
{
    #region FileType
    public abstract class FileType //(uint Id)
    {
        public uint Id; // = Id;
    }
    #endregion

    #region Animation
    /// <summary>
    /// These are client_portal.dat files starting with 0x03. 
    /// Special thanks to Dan Skorupski for his work on Bael'Zharon's Respite, which helped fill in some of the gaps https://github.com/boardwalk/bzr
    /// </summary>
    //: FileTypes.Animation
    [PakFileType(PakFileType.Animation)]
    public class Animation : FileType, IHaveMetaInfo
    {
        public readonly AnimationFlags Flags;
        public readonly uint NumParts;
        public readonly uint NumFrames;
        public readonly Frame[] PosFrames;
        public readonly AnimationFrame[] PartFrames;

        public Animation(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Flags = (AnimationFlags)r.ReadUInt32();
            NumParts = r.ReadUInt32();
            NumFrames = r.ReadUInt32();
            PosFrames = (Flags & AnimationFlags.PosFrames) != 0 ? r.ReadFArray(x => new Frame(x), (int)NumFrames) : default;
            PartFrames = r.ReadFArray(x => new AnimationFrame(x, NumParts), (int)NumFrames);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(Animation)}: {Id:X8}", items: [
                Flags.HasFlag(AnimationFlags.PosFrames) ? new($"PosFrames", items: PosFrames.Select(x => new MetaInfo($"{x}"))) : null,
                new($"PartFrames", items: PartFrames.Select(x => new MetaInfo($"{x}")))
            ])
        ];
    }
    #endregion

    #region BadData
    //: FileTypes.BadData
    [PakFileType(PakFileType.BadData)]
    public class BadData : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E00001A;

        // Key is a list of a WCIDs that are "bad" and should not exist. The value is always 1 (could be a bool?)
        public readonly IDictionary<uint, uint> Bad;

        public BadData(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Bad = r.Skip(2).ReadL16PMany<uint, uint>("I", x => x.ReadUInt32());
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = "Bad Data", Value = string.Join(", ", Bad.Keys.OrderBy(x => x)) }),
            new($"{nameof(TabooTable)}: {Id:X8}")
        ];
    }
    #endregion

    #region CharGen
    //: FileTypes.CharGen
    [PakFileType(PakFileType.CharacterGenerator)]
    public class CharGen : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E000002;

        public readonly StarterArea[] StarterAreas;
        public readonly IDictionary<uint, HeritageGroupCG> HeritageGroups;

        public CharGen(BinaryReader r)
        {
            Id = r.ReadUInt32();
            r.Skip(4);
            StarterAreas = r.ReadC32FArray(x => new StarterArea(x));
            // HERITAGE GROUPS -- 11 standard player races and 2 Olthoi.
            r.Skip(1); // Not sure what this byte 0x01 is indicating, but we'll skip it because we can.
            HeritageGroups = r.ReadC32PMany<uint, HeritageGroupCG>("I", x => new HeritageGroupCG(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(CharGen)}: {Id:X8}", items: [
                new("Starter Areas", items: StarterAreas.Select(x => {
                    var items = (x as IHaveMetaInfo).GetInfoNodes();
                    var name = items[0].Name.Replace("Name: ", "");
                    items.RemoveAt(0);
                    return new MetaInfo(name, items: items);
                })),
                new("Heritage Groups", items: HeritageGroups.Select(x => {
                    var items = (x.Value as IHaveMetaInfo).GetInfoNodes();
                    var name = items[0].Name.Replace("Name: ", "");
                    items.RemoveAt(0);
                    return new MetaInfo(name, items: items);
                })),
            ])
        ];
    }
    #endregion

    #region ChatPoseTable
    //: FileTypes.ChatPoseTable
    [PakFileType(PakFileType.ChatPoseTable)]
    public class ChatPoseTable : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E000007;

        // Key is a emote command, value is the state you are enter into
        public readonly IDictionary<string, string> ChatPoseHash;
        // Key is the state, value are the strings that players see during the emote
        public readonly IDictionary<string, ChatEmoteData> ChatEmoteHash;

        public ChatPoseTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            ChatPoseHash = r.Skip(2).ReadL16FMany(x => (x.ReadL16Encoding(Encoding.Default), x.Align()).Item1, x => (x.ReadL16Encoding(Encoding.Default), x.Align()).Item1);
            ChatEmoteHash = r.Skip(2).ReadL16FMany(x => (x.ReadL16Encoding(Encoding.Default), x.Align()).Item1, x => new ChatEmoteData(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(ChatPoseTable)}: {Id:X8}", items: [
                new("ChatPoseHash", items: ChatPoseHash.OrderBy(i => i.Key).Select(x => new MetaInfo($"{x.Key}: {x.Value}"))),
                new("ChatEmoteHash", items: ChatEmoteHash.OrderBy(i => i.Key).Select(x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes(tag: tag)))),
            ])
        ];
    }
    #endregion

    #region ClothingTable
    /// <summary>
    /// These are client_portal.dat files starting with 0x10. 
    /// It contains information on an items model, texture changes, available palette(s) and icons for that item.
    /// </summary>
    /// <remarks>
    /// Thanks to Steven Nygard and his work on the Mac program ACDataTools that were used to help debug & verify some of this data.
    /// </remarks>
    //: FileTypes.ClothingTable
    [PakFileType(PakFileType.Clothing)]
    public class ClothingTable : FileType, IHaveMetaInfo
    {
        /// <summary>
        /// Key is the setup model id
        /// </summary>
        public readonly IDictionary<uint, ClothingBaseEffect> ClothingBaseEffects;
        /// <summary>
        /// Key is PaletteTemplate
        /// </summary>
        public readonly IDictionary<uint, CloSubPalEffect> ClothingSubPalEffects;

        public ClothingTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            ClothingBaseEffects = r.Skip(2).ReadL16PMany<uint, ClothingBaseEffect>("I", x => new ClothingBaseEffect(x));
            ClothingSubPalEffects = r.Skip(2).ReadL16PMany<uint, CloSubPalEffect>("I", x => new CloSubPalEffect(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(ClothingTable)}: {Id:X8}", items: [
                new("Base Effects", items: ClothingBaseEffects.Select(x => new MetaInfo($"{x.Key:X8}", items: (x.Value as IHaveMetaInfo).GetInfoNodes(), clickable: true))),
                new("SubPalette Effects", items: ClothingSubPalEffects.OrderBy(i => i.Key).Select(x => new MetaInfo($"{x.Key} - {(PaletteTemplate)x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes()))),
            ])
        ];

        public uint GetIcon(uint palEffectIdx) => ClothingSubPalEffects.TryGetValue(palEffectIdx, out CloSubPalEffect result) ? result.Icon : 0;

        /// <summary>
        /// Calculates the ClothingPriority of an item based on the actual coverage. So while an Over-Robe may just be "Chest", we want to know it covers everything but head & arms.
        /// </summary>
        /// <param name="setupId">Defaults to HUMAN_MALE if not set, which is good enough</param>
        /// <returns></returns>
        public CoverageMask? GetVisualPriority(uint setupId = 0x02000001)
        {
            if (!ClothingBaseEffects.TryGetValue(setupId, out var clothingBaseEffect)) return null;
            CoverageMask visualPriority = 0;
            foreach (var t in clothingBaseEffect.CloObjectEffects)
                switch (t.Index)
                {
                    case 0: // HUMAN_ABDOMEN;
                        visualPriority |= CoverageMask.OuterwearAbdomen; break;
                    case 1: // HUMAN_LEFT_UPPER_LEG;
                    case 5: // HUMAN_RIGHT_UPPER_LEG;
                        visualPriority |= CoverageMask.OuterwearUpperLegs; break;
                    case 2: // HUMAN_LEFT_LOWER_LEG;
                    case 6: // HUMAN_RIGHT_LOWER_LEG;
                        visualPriority |= CoverageMask.OuterwearLowerLegs; break;
                    case 3: // HUMAN_LEFT_FOOT;
                    case 4: // HUMAN_LEFT_TOE;
                    case 7: // HUMAN_RIGHT_FOOT;
                    case 8: // HUMAN_RIGHT_TOE;
                        visualPriority |= CoverageMask.Feet; break;
                    case 9: // HUMAN_CHEST;
                        visualPriority |= CoverageMask.OuterwearChest; break;
                    case 10: // HUMAN_LEFT_UPPER_ARM;
                    case 13: // HUMAN_RIGHT_UPPER_ARM;
                        visualPriority |= CoverageMask.OuterwearUpperArms; break;
                    case 11: // HUMAN_LEFT_LOWER_ARM;
                    case 14: // HUMAN_RIGHT_LOWER_ARM;
                        visualPriority |= CoverageMask.OuterwearLowerArms; break;
                    case 12: // HUMAN_LEFT_HAND;
                    case 15: // HUMAN_RIGHT_HAND;
                        visualPriority |= CoverageMask.Hands; break;
                    case 16: // HUMAN_HEAD;
                        visualPriority |= CoverageMask.Head; break;
                    default: break; // Lots of things we don't care about
                }
            return visualPriority;
        }
    }
    #endregion

    #region CombatManeuverTable
    /// <summary>
    /// These are client_portal.dat files starting with 0x30. 
    /// </summary>
    //: FileTypes.CombatTable
    [PakFileType(PakFileType.CombatTable)]
    public class CombatManeuverTable : FileType, IHaveMetaInfo
    {
        public readonly CombatManeuver[] CMT;
        public readonly Dictionary<MotionStance, AttackHeights> Stances;

        public CombatManeuverTable(BinaryReader r)
        {
            Id = r.ReadUInt32(); // This should always equal the fileId
            CMT = r.ReadL32FArray(x => new CombatManeuver(x));
            Stances = [];
            foreach (var maneuver in CMT)
            {
                if (!Stances.TryGetValue(maneuver.Style, out var attackHeights)) Stances.Add(maneuver.Style, attackHeights = new AttackHeights());
                if (!attackHeights.Table.TryGetValue(maneuver.AttackHeight, out var attackTypes)) attackHeights.Table.Add(maneuver.AttackHeight, attackTypes = new AttackTypes());
                if (!attackTypes.Table.TryGetValue(maneuver.AttackType, out var motionCommands)) attackTypes.Table.Add(maneuver.AttackType, motionCommands = new List<MotionCommand>());
                motionCommands.Add(maneuver.Motion);
            }
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(CombatManeuverTable)}: {Id:X8}", items: [
                new("Maneuvers", items: CMT.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            ])
        ];

        //: was:ShowCombatTable
        public override string ToString()
        {
            var b = new StringBuilder();
            foreach (var stance in Stances)
            {
                b.AppendLine($"- {stance.Key}");
                foreach (var attackHeight in stance.Value.Table)
                {
                    b.AppendLine($"  - {attackHeight.Key}");
                    foreach (var attackType in attackHeight.Value.Table)
                    {
                        b.AppendLine($"    - {attackType.Key}");
                        foreach (var motion in attackType.Value) b.AppendLine($"      - {motion}");
                    }
                }
            }
            return b.ToString();
        }


        public class AttackHeights
        {
            public readonly Dictionary<AttackHeight, AttackTypes> Table = new Dictionary<AttackHeight, AttackTypes>();
        }

        public class AttackTypes
        {
            // technically there is another MinSkillLevels here in the data,
            // but every MinSkillLevel in the client dats are always 0
            public readonly Dictionary<AttackType, List<MotionCommand>> Table = new Dictionary<AttackType, List<MotionCommand>>();
        }

        public static readonly List<MotionCommand> Invalid = new List<MotionCommand>() { MotionCommand.Invalid };

        public List<MotionCommand> GetMotion(MotionStance stance, AttackHeight attackHeight, AttackType attackType, MotionCommand prevMotion)
        {
            if (!Stances.TryGetValue(stance, out var attackHeights)) return Invalid;
            if (!attackHeights.Table.TryGetValue(attackHeight, out var attackTypes)) return Invalid;
            if (!attackTypes.Table.TryGetValue(attackType, out var maneuvers)) return Invalid;

#if false
            //if (maneuvers.Count == 1)
                //return maneuvers[0];

            /*Console.WriteLine($"CombatManeuverTable({Id:X8}).GetMotion({stance}, {attackHeight}, {attackType}) - found {maneuvers.Count} maneuvers");
            foreach (var maneuver in maneuvers)
                Console.WriteLine(maneuver);*/

            // CombatManeuverTable(30000000).GetMotion(SwordCombat, Medium, Slash) - found 2 maneuvers
            // SlashMed
            // BackhandMed

            // rng, or alternate?
            /*for (var i = 0; i < maneuvers.Count; i++)
            {
                var maneuver = maneuvers[i];

                if (maneuver == prevMotion)
                {
                    if (i < maneuvers.Count - 1)
                        return maneuvers[i + 1];
                    else
                        return maneuvers[0];
                }
            }
            return maneuvers[0];*/
#endif

            // if the CMT contains > 1 entries for this lookup, return both the code determines which motion to use based on the power bar
            return maneuvers;
        }
    }
    #endregion

    #region ContractTable
    /// <summary>
    /// This is the client_portal.dat file 0x0E00001D
    /// </summary>
    //: FileTypes.ContractTable
    [PakFileType(PakFileType.ContractTable)]
    public class ContractTable : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E00001D;

        public readonly IDictionary<uint, Contract> Contracts;

        public ContractTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Contracts = r.Skip(2).ReadL16PMany<uint, Contract>("I", x => new Contract(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(ContractTable)}: {Id:X8}", items: Contracts.Select(
                x => new MetaInfo($"{x.Key} - {x.Value.ContractName}", items: (x.Value as IHaveMetaInfo).GetInfoNodes(tag: tag))
            ))
        ];
    }
    #endregion

    #region DidMapper
    /// <summary>
    /// EnumMapper files are 0x25 in the client_portal.dat
    /// They contain, as the name implies, a map of different enumeration types to a DataID value (item that exist in a client_portal.dat file)
    /// A description of each DidMapper is in DidMapper entry 0x25000000
    /// </summary>
    [PakFileType(PakFileType.DidMapper)]
    public class DidMapper : FileType, IHaveMetaInfo
    {
        // The client/server designation is guessed based on the content in each list.
        // The keys in these two Dictionaries are common. So ClientEnumToId[key] = ClientEnumToName[key].
        public readonly NumberingType ClientIDNumberingType; // bitfield designating how the numbering is organized. Not really needed for our usage.
        public readonly IDictionary<uint, uint> ClientEnumToID; // _EnumToID
        public readonly NumberingType ClientNameNumberingType; // bitfield designating how the numbering is organized. Not really needed for our usage.
        public readonly IDictionary<uint, string> ClientEnumToName; // _EnumToName
        // The keys in these two Dictionaries are common. So ServerEnumToId[key] = ServerEnumToName[key].
        public readonly NumberingType ServerIDNumberingType; // bitfield designating how the numbering is organized. Not really needed for our usage.
        public readonly IDictionary<uint, uint> ServerEnumToID; // _EnumToID
        public readonly NumberingType ServerNameNumberingType; // bitfield designating how the numbering is organized. Not really needed for our usage.
        public readonly IDictionary<uint, string> ServerEnumToName; // _EnumToName

        public DidMapper(BinaryReader r)
        {
            Id = r.ReadUInt32();
            ClientIDNumberingType = (NumberingType)r.ReadByte();
            ClientEnumToID = r.ReadC32PMany<uint, uint>("I", x => x.ReadUInt32());
            ClientNameNumberingType = (NumberingType)r.ReadByte();
            ClientEnumToName = r.ReadC32PMany<uint, string>("I", x => x.ReadL8Encoding(Encoding.Default));
            ServerIDNumberingType = (NumberingType)r.ReadByte();
            ServerEnumToID = r.ReadC32PMany<uint, uint>("I", x => x.ReadUInt32());
            ServerNameNumberingType = (NumberingType)r.ReadByte();
            ServerEnumToName = r.ReadC32PMany<uint, string>("I", x => x.ReadL8Encoding(Encoding.Default));
        }

        //: FileTypes.DidMapper
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(DidMapper)}: {Id:X8}", items: [
                ClientEnumToID.Count > 0 ? new($"ClientIDNumberingType: {ClientIDNumberingType}") : null,
                ClientEnumToID.Count > 0 ? new("ClientEnumToID", items: ClientEnumToID.OrderBy(x => x.Key).Select(x => new MetaInfo($"{x.Key}: {x.Value::X8}"))) : null,
                ClientEnumToName.Count > 0 ? new($"ClientNameNumberingType: {ClientNameNumberingType}") : null,
                ClientEnumToName.Count > 0 ? new("ClientEnumToName", items: ClientEnumToName.OrderBy(x => x.Key).Select(x => new MetaInfo($"{x.Key}: {x.Value::X8}"))) : null,
                ServerEnumToID.Count > 0 ? new($"ServerIDNumberingType: {ServerIDNumberingType}") : null,
                ServerEnumToID.Count > 0 ? new("ServerEnumToID", items: ServerEnumToID.OrderBy(x => x.Key).Select(x => new MetaInfo($"{x.Key}: {x.Value::X8}"))) : null,
                ServerEnumToName.Count > 0 ? new($"ServerNameNumberingType: {ClientIDNumberingType}") : null,
                ServerEnumToName.Count > 0 ? new("ServerEnumToName", items: ServerEnumToName.OrderBy(x => x.Key).Select(x => new MetaInfo($"{x.Key}: {x.Value::X8}"))) : null,
            ])
        ];
    }
    #endregion

    #region DualDidMapper
    /// <summary>
    /// EnumMapper files are 0x27 in the client_portal.dat
    /// They contain a list of Weenie IDs and their W_Class. The client uses these for items such as tracking spell components (to know if the player has all required to cast a spell).
    ///
    /// A description of each DualDidMapper is in DidMapper entry 0x25000005 (WEENIE_CATEGORIES)
    /// 27000000 - Materials
    /// 27000001 - Gems
    /// 27000002 - SpellComponents
    /// 27000003 - ComponentPacks
    /// 27000004 - TradeNotes
    /// </summary>
    //: FileTypes.DualDidMapper
    [PakFileType(PakFileType.DualDidMapper)]
    public class DualDidMapper : FileType, IHaveMetaInfo
    {
        // The client/server designation is guessed based on the content in each list.
        // The keys in these two Dictionaries are common. So ClientEnumToId[key] = ClientEnumToName[key].
        public readonly NumberingType ClientIDNumberingType; // bitfield designating how the numbering is organized. Not really needed for our usage.
        public readonly IDictionary<uint, uint> ClientEnumToID; // _EnumToID
        public readonly NumberingType ClientNameNumberingType; // bitfield designating how the numbering is organized. Not really needed for our usage.
        public readonly IDictionary<uint, string> ClientEnumToName = new Dictionary<uint, string>(); // _EnumToName
        // The keys in these two Dictionaries are common. So ServerEnumToId[key] = ServerEnumToName[key].
        public readonly NumberingType ServerIDNumberingType; // bitfield designating how the numbering is organized. Not really needed for our usage.
        public readonly IDictionary<uint, uint> ServerEnumToID; // _EnumToID
        public readonly NumberingType ServerNameNumberingType; // bitfield designating how the numbering is organized. Not really needed for our usage.
        public readonly IDictionary<uint, string> ServerEnumToName; // _EnumToName

        public DualDidMapper(BinaryReader r)
        {
            Id = r.ReadUInt32();
            ClientIDNumberingType = (NumberingType)r.ReadByte();
            ClientEnumToID = r.ReadC32PMany<uint, uint>("I", x => x.ReadUInt32());
            ClientNameNumberingType = (NumberingType)r.ReadByte();
            ClientEnumToName = r.ReadC32PMany<uint, string>("I", x => x.ReadL8Encoding(Encoding.Default));
            ServerIDNumberingType = (NumberingType)r.ReadByte();
            ServerEnumToID = r.ReadC32PMany<uint, uint>("I", x => x.ReadUInt32());
            ServerNameNumberingType = (NumberingType)r.ReadByte();
            ServerEnumToName = r.ReadC32PMany<uint, string>("I", x => x.ReadL8Encoding(Encoding.Default));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(DualDidMapper)}: {Id:X8}", items: [
                ClientEnumToID.Count > 0 ? new($"ClientIDNumberingType: {ClientIDNumberingType}") : null,
                ClientEnumToID.Count > 0 ? new("ClientEnumToID", items: ClientEnumToID.OrderBy(x => x.Key).Select(x => new MetaInfo($"{x.Key}: {x.Value::X8}"))) : null,
                ClientEnumToName.Count > 0 ? new($"ClientNameNumberingType: {ClientNameNumberingType}") : null,
                ClientEnumToName.Count > 0 ? new("ClientEnumToName", items: ClientEnumToName.OrderBy(x => x.Key).Select(x => new MetaInfo($"{x.Key}: {x.Value::X8}"))) : null,
                ServerEnumToID.Count > 0 ? new($"ServerIDNumberingType: {ServerIDNumberingType}") : null,
                ServerEnumToID.Count > 0 ? new("ServerEnumToID", items: ServerEnumToID.OrderBy(x => x.Key).Select(x => new MetaInfo($"{x.Key}: {x.Value::X8}"))) : null,
                ServerEnumToName.Count > 0 ? new($"ServerNameNumberingType: {ClientIDNumberingType}") : null,
                ServerEnumToName.Count > 0 ? new("ServerEnumToName", items: ServerEnumToName.OrderBy(x => x.Key).Select(x => new MetaInfo($"{x.Key}: {x.Value::X8}"))) : null,
            ])
        ];
    }
    #endregion

    #region EnumMapper
    //: FileTypes.EnumMapper
    [PakFileType(PakFileType.EnumMapper)]
    public class EnumMapper : FileType, IHaveMetaInfo
    {
        public readonly uint BaseEnumMap; // _base_emp_did
        public readonly NumberingType NumberingType;
        public readonly IDictionary<uint, string> IdToStringMap; // _id_to_string_map

        public EnumMapper(BinaryReader r)
        {
            Id = r.ReadUInt32();
            BaseEnumMap = r.ReadUInt32();
            NumberingType = (NumberingType)r.ReadByte();
            IdToStringMap = r.ReadC32PMany<uint, string>("I", x => x.ReadL8Encoding(Encoding.Default));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(EnumMapper)}: {Id:X8}", items: [
                BaseEnumMap != 0 ? new($"BaseEnumMap: {BaseEnumMap:X8}") : null,
                NumberingType != NumberingType.Undefined ? new($"NumberingType: {NumberingType}") : null,
                IdToStringMap.Count > 0 ? new("IdToStringMap", items: IdToStringMap.OrderBy(x => x.Key).Select(x => new MetaInfo($"{x.Key}: {x.Value::X8}"))) : null,
            ])
        ];
    }
    #endregion

    #region EnvCell
    /// <summary>
    /// This reads an "indoor" cell from the client_cell.dat. This is mostly dungeons, but can also be a building interior.
    /// An EnvCell is designated by starting 0x0100 (whereas all landcells are in the 0x0001 - 0x003E range.
    /// <para />
    /// The fileId is the full int32/dword landblock value as reported by the @loc command (e.g. 0x12345678)
    /// </summary>
    /// <remarks>
    /// Very special thanks again to David Simpson for his early work on reading the cell.dat. Even bigger thanks for his documentation of it!
    /// </remarks>
    //: FileTypes.EnvCell
    [PakFileType(PakFileType.EnvCell)]
    public class EnvCell : FileType, IHaveMetaInfo
    {
        public readonly EnvCellFlags Flags;
        public readonly uint[] Surfaces; // 0x08000000 surfaces (which contains degrade/quality info to reference the specific 0x06000000 graphics)
        public readonly uint EnvironmentId; // the 0x0D000000 model of the pre-fab dungeon block
        public readonly ushort CellStructure;
        public readonly Frame Position;
        public readonly CellPortal[] CellPortals;
        public readonly ushort[] VisibleCells;
        public readonly Stab[] StaticObjects;
        public readonly uint RestrictionObj;
        public bool SeenOutside => Flags.HasFlag(EnvCellFlags.SeenOutside);

        public EnvCell(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Flags = (EnvCellFlags)r.ReadUInt32();
            r.Skip(4); // Skip ahead 4 bytes, because this is the CellId. Again. Twice.
            var numSurfaces = r.ReadByte();
            var numPortals = r.ReadByte(); // Note that "portal" in this context does not refer to the swirly pink/purple thing, its basically connecting cells
            var numStabs = r.ReadUInt16(); // I believe this is what cells can be seen from this one. So the engine knows what else it needs to load/draw.
            // Read what surfaces are used in this cell
            Surfaces = r.ReadFArray(x => 0x08000000u | r.ReadUInt16(), numSurfaces); // these are stored in the dat as short values, so we'll make them a full dword
            EnvironmentId = 0x0D000000u | r.ReadUInt16();
            CellStructure = r.ReadUInt16();
            Position = new Frame(r);
            CellPortals = r.ReadFArray(x => new CellPortal(x), numPortals);
            VisibleCells = r.ReadPArray<ushort>("H", numStabs);
            if ((Flags & EnvCellFlags.HasStaticObjs) != 0) StaticObjects = r.ReadL32FArray(x => new Stab(x));
            if ((Flags & EnvCellFlags.HasRestrictionObj) != 0) RestrictionObj = r.ReadUInt32();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(EnvCell)}: {Id:X8}", items: [
                Flags != 0 ? new($"Flags: {Flags}") : null,
                new("Surfaces", items: Surfaces.Select(x => new MetaInfo($"{x:X8}", clickable: true))),
                new($"Environment: {EnvironmentId:X8}", clickable: true),
                CellStructure != 0 ? new($"CellStructure: {CellStructure}") : null,
                new($"Position: {Position}"),
                CellPortals.Length > 0 ? new("CellPortals", items: CellPortals.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))) : null,
                StaticObjects.Length > 0 ? new("StaticObjects", items: StaticObjects.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))) : null,
                RestrictionObj != 0 ? new($"RestrictionObj: {RestrictionObj:X8}", clickable: true) : null,
            ])
        ];
    }
    #endregion

    #region Environment
    /// <summary>
    /// These are client_portal.dat files starting with 0x0D. 
    /// These are basically pre-fab regions for things like the interior of a dungeon.
    /// </summary>
    [PakFileType(PakFileType.Environment)]
    public class Environment : FileType, IHaveMetaInfo
    {
        public readonly IDictionary<uint, CellStruct> Cells;

        public Environment(BinaryReader r)
        {
            Id = r.ReadUInt32(); // this will match fileId
            Cells = r.ReadL32PMany<uint, CellStruct>("I", x => new CellStruct(x));
        }

        //: FileTypes.Environment
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(Environment)}: {Id:X8}", items: Cells.Select(x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes()))),
        ];
    }
    #endregion

    #region Font
    /// <summary>
    /// These are client_portal.dat files starting with 0x40.
    /// It is essentially a map to a specific texture file (spritemap) that contains all the characters in this font.
    /// </summary>
    [PakFileType(PakFileType.Font)]
    public class Font : FileType, IHaveMetaInfo
    {
        public readonly uint MaxCharHeight;
        public readonly uint MaxCharWidth;
        //public uint NumCharacters => (uint)CharDescs.Length;
        public readonly FontCharDesc[] CharDescs;
        public readonly uint NumHorizontalBorderPixels;
        public readonly uint NumVerticalBorderPixels;
        public readonly uint BaselineOffset;
        public readonly uint ForegroundSurfaceDataID; // This is a DataID to a Texture (0x06) type, if set
        public readonly uint BackgroundSurfaceDataID; // This is a DataID to a Texture (0x06) type, if set

        public Font(BinaryReader r)
        {
            Id = r.ReadUInt32();
            MaxCharHeight = r.ReadUInt32();
            MaxCharWidth = r.ReadUInt32();
            CharDescs = r.ReadL32FArray(x => new FontCharDesc(x));
            NumHorizontalBorderPixels = r.ReadUInt32();
            NumVerticalBorderPixels = r.ReadUInt32();
            BaselineOffset = r.ReadUInt32();
            ForegroundSurfaceDataID = r.ReadUInt32();
            BackgroundSurfaceDataID = r.ReadUInt32();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(Font)}: {Id:X8}", items: [])
        ];
    }
    #endregion

    #region GeneratorTable
    /// <summary>
    /// Class for reading the File 0x0E00000D from the portal.dat.
    /// Thanks alot Widgeon of Leafcull for his ACDataTools which helped understanding this structure.
    /// And thanks alot to Pea as well whos hard work surely helped in the creation of those Tools too.
    /// </summary>
    [PakFileType(PakFileType.ObjectHierarchy)]
    public class GeneratorTable : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E00000D;

        public readonly Generator Generators;
        /// <summary>
        /// This is just a shortcut to Generators.Items[0].Items
        /// </summary>
        public readonly Generator[] PlayDayItems;
        /// <summary>
        /// This is just a shortcut to Generators.Items[1].Items
        /// </summary>
        public readonly Generator[] WeenieObjectsItems;

        public GeneratorTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Generators = new Generator(r);
            PlayDayItems = Generators.Items[0].Items;
            WeenieObjectsItems = Generators.Items[1].Items;
        }

        //: FileTypes.GeneratorTable
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(GeneratorTable)}: {Id:X8}", items: [
                new("Generators", items: (Generators as IHaveMetaInfo).GetInfoNodes(tag: tag)),
                new("PlayDayItems", items: PlayDayItems.Select(x => new MetaInfo(x.Id != 0 ? $"{x.Id} - {x.Name}" : $"{x.Name}", items: (x as IHaveMetaInfo).GetInfoNodes(tag: tag)))),
                new("WeenieObjectsItems", items: WeenieObjectsItems.Select(x => new MetaInfo(x.Id != 0 ? $"{x.Id} - {x.Name}" : $"{x.Name}", items: (x as IHaveMetaInfo).GetInfoNodes(tag: tag)))),
            ])
        ];
    }
    #endregion

    #region GfxObj
    /// <summary>
    /// These are client_portal.dat files starting with 0x01. 
    /// These are used both on their own for some pre-populated structures in the world (trees, buildings, etc) or make up SetupModel (0x02) objects.
    /// </summary>
    //: FileTypes.GfxObj
    [PakFileType(PakFileType.GfxObject)]
    public class GfxObj : FileType, IHaveMetaInfo
    {
        public readonly GfxObjFlags Flags;
        public readonly uint[] Surfaces; // also referred to as m_rgSurfaces in the client
        public readonly CVertexArray VertexArray;
        public readonly IDictionary<ushort, Polygon> PhysicsPolygons;
        public readonly BspTree PhysicsBSP;
        public readonly Vector3 SortCenter;
        public readonly IDictionary<ushort, Polygon> Polygons;
        public readonly BspTree DrawingBSP;
        public readonly uint DIDDegrade;

        public GfxObj(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Flags = (GfxObjFlags)r.ReadUInt32();
            Surfaces = r.ReadC32PArray<uint>("I");
            VertexArray = new CVertexArray(r);
            // Has Physics 
            if ((Flags & GfxObjFlags.HasPhysics) != 0)
            {
                PhysicsPolygons = r.ReadC32PMany<ushort, Polygon>("H", x => new Polygon(x));
                PhysicsBSP = new BspTree(r, BSPType.Physics);
            }
            SortCenter = r.ReadVector3();
            // Has Drawing 
            if ((Flags & GfxObjFlags.HasDrawing) != 0)
            {
                Polygons = r.ReadC32PMany<ushort, Polygon>("H", x => new Polygon(x));
                DrawingBSP = new BspTree(r, BSPType.Drawing);
            }
            if ((Flags & GfxObjFlags.HasDIDDegrade) != 0) DIDDegrade = r.ReadUInt32();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Model", Name = "Model", Value = this }),
            new($"{nameof(GfxObj)}: {Id:X8}", items: [
                new($"Surfaces", items: Surfaces.Select(x => new MetaInfo($"{x:X8}", clickable: true))),
                new($"VertexArray", items: (VertexArray as IHaveMetaInfo).GetInfoNodes(resource, file)),
                Flags.HasFlag(GfxObjFlags.HasPhysics) ? new($"PhysicsPolygons", items: PhysicsPolygons.Select(x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes()))) : null,
                Flags.HasFlag(GfxObjFlags.HasPhysics) ? new($"PhysicsBSP", items: (PhysicsBSP as IHaveMetaInfo).GetInfoNodes(tag: BSPType.Physics).First().Items) : null,
                new($"SortCenter: {SortCenter}"),
                Flags.HasFlag(GfxObjFlags.HasDrawing) ? new($"Polygons", items: Polygons.Select(x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes()))) : null,
                Flags.HasFlag(GfxObjFlags.HasDrawing) ? new($"DrawingBSP", items: (DrawingBSP as IHaveMetaInfo).GetInfoNodes(tag: BSPType.Drawing).First().Items) : null,
                Flags.HasFlag(GfxObjFlags.HasDIDDegrade) ? new($"DIDDegrade: {DIDDegrade:X8}", clickable: true) : null,
            ])
        ];
    }
    #endregion

    #region GfxObjDegradeInfo
    /// <summary>
    /// These are client_portal.dat files starting with 0x11. 
    /// Contains info on what objects to display at what distance to help with render performance (e.g. low-poly very far away, but high-poly when close)
    /// </summary>
    //: FileTypes.DegradeInfo
    [PakFileType(PakFileType.DegradeInfo)]
    public class GfxObjDegradeInfo : FileType, IHaveMetaInfo
    {
        public readonly GfxObjInfo[] Degrades;

        public GfxObjDegradeInfo(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Degrades = r.ReadL32FArray(x => new GfxObjInfo(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(GfxObjDegradeInfo)}: {Id:X8}", items: [
                new("Starter Areas", items: Degrades.Select(x => {
                    var items = (x as IHaveMetaInfo).GetInfoNodes();
                    var name = items[0].Name.Replace("Id: ", "");
                    items.RemoveAt(0);
                    return new MetaInfo(name, items: items, clickable: true);
                })),
            ])
        ];
    }
    #endregion

    #region Iteration
    /// <summary>
    /// These are stored in the client_cell.dat, client_portal.dat, and client_local_English.dat files with the index 0xFFFF0001
    ///
    /// This is essentially the dat "versioning" system.
    /// This is used when first connecting to the server to compare the client dat files with the server dat files and any subsequent patching that may need to be done.
    /// 
    /// Special thanks to the GDLE team for pointing me the right direction on how/where to find this info in the dat files- OptimShi
    /// </summary>
    //: New
    public class Iteration : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0xFFFF0001;

        public readonly int[] Ints;
        public readonly bool Sorted;

        public Iteration(BinaryReader r)
        {
            Ints = new[] { r.ReadInt32(), r.ReadInt32() };
            Sorted = r.ReadBoolean(); r.Align();
        }

        public override string ToString()
        {
            var b = new StringBuilder();
            for (var i = 0; i < Ints.Length; i++) b.Append($"{Ints[i]},");
            b.Append(Sorted ? "1" : "0");
            return b.ToString();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(Iteration)}: {Id:X8}", items: [])
        ];
    }
    #endregion

    #region Landblock
    /// <summary>
    /// A landblock is divided into 8 x 8 tiles, which means 9 x 9 vertices reporesenting those tiles. 
    /// (Draw a grid of 9x9 dots; connect those dots to form squares; you'll have 8x8 squares)
    /// It is also divided in 192x192 units (this is the x and the y)
    /// 
    /// 0,0 is the bottom left corner of the landblock. 
    /// 
    /// Height 0-9 is Western most edge. 10-18 is S-to-N strip just to the East. And so on.
    /// <para />
    /// The fileId is CELL + 0xFFFF. e.g. a cell of 1234, the file index would be 0x1234FFFF.
    /// </summary>
    /// <remarks>
    /// Very special thanks to David Simpson for his early work on reading the cell.dat. Even bigger thanks for his documentation of it!
    /// </remarks>
    //: FileTypes.CellLandblock
    [PakFileType(PakFileType.LandBlock)]
    public class Landblock : FileType, IHaveMetaInfo
    {
        /// <summary>
        /// Places in the inland sea, for example, are false. Should denote presence of xxxxFFFE (where xxxx is the cell).
        /// </summary>
        public readonly bool HasObjects;
        public readonly ushort[] Terrain;
        public static ushort TerrainMask_Road = 0x3;
        public static ushort TerrainMask_Type = 0x7C;
        public static ushort TerrainMask_Scenery = 0XF800;
        public static byte TerrainShift_Road = 0;
        public static byte TerrainShift_Type = 2;
        public static byte TerrainShift_Scenery = 11;
        /// <summary>
        /// Z value in-game is double this height.
        /// </summary>
        public readonly byte[] Height;

        public Landblock(BinaryReader r)
        {
            Id = r.ReadUInt32();
            HasObjects = r.ReadUInt32() == 1;
            // Read in the terrain. 9x9 so 81 records.
            Terrain = r.ReadPArray<ushort>("H", 81);
            Height = r.ReadPArray<byte>("B", 81);
            r.Align();
        }

        public static ushort GetRoad(ushort terrain) => GetTerrain(terrain, TerrainMask_Road, TerrainShift_Road);
        public static ushort GetType(ushort terrain) => GetTerrain(terrain, TerrainMask_Type, TerrainShift_Type);
        public static ushort GetScenery(ushort terrain) => GetTerrain(terrain, TerrainMask_Scenery, TerrainShift_Scenery);
        public static ushort GetTerrain(ushort terrain, ushort mask, byte shift) => (ushort)((terrain & mask) >> shift);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var terrainTypes = DatabaseManager.Portal.RegionDesc.TerrainInfo.TerrainTypes;
            return [
                new($"{nameof(Landblock)}: {Id:X8}", items: [
                    new($"HasObjects: {HasObjects}"),
                    new("Terrain", items: Terrain.Select((x, i) => new MetaInfo($"{i}: Road: {GetRoad(x)}, Type: {terrainTypes[GetType(x)].TerrainName}, Scenery: {GetScenery(x)}"))),
                    new("Heights", items: Height.Select((x, i) => new MetaInfo($"{i}: {x}"))),
                ])
            ];
        }
    }
    #endregion

    #region LandblockInfo
    /// <summary>
    /// This reads the extra items in a landblock from the client_cell.dat. This is mostly buildings, but other static/non-interactive objects like tables, lamps, are also included.
    /// CLandBlockInfo is a file designated xxyyFFFE, where xxyy is the landblock.
    /// <para />
    /// The fileId is CELL + 0xFFFE. e.g. a cell of 1234, the file index would be 0x1234FFFE.
    /// </summary>
    /// <remarks>
    /// Very special thanks again to David Simpson for his early work on reading the cell.dat. Even bigger thanks for his documentation of it!
    /// </remarks>
    //: FileTypes.LandblockInfo
    [PakFileType(PakFileType.LandBlockInfo)]
    public class LandblockInfo : FileType, IHaveMetaInfo
    {
        /// <summary>
        /// number of EnvCells in the landblock. This should match up to the unique items in the building stab lists.
        /// </summary>
        public readonly uint NumCells;
        /// <summary>
        /// list of model numbers. 0x01 and 0x02 types and their specific locations
        /// </summary>
        public readonly Stab[] Objects;
        /// <summary>
        /// As best as I can tell, this only affects whether there is a restriction table or not
        /// </summary>
        public readonly uint PackMask;
        /// <summary>
        /// Buildings and other structures with interior locations in the landblock
        /// </summary>
        public readonly BuildInfo[] Buildings;
        /// <summary>
        /// The specific landblock/cell controlled by a specific guid that controls access (e.g. housing barrier)
        /// </summary>
        public readonly IDictionary<uint, uint> RestrictionTables;

        public LandblockInfo(BinaryReader r)
        {
            Id = r.ReadUInt32();
            NumCells = r.ReadUInt32();
            Objects = r.ReadL32FArray(x => new Stab(x));
            var numBuildings = r.ReadUInt16();
            PackMask = r.ReadUInt16();
            Buildings = r.ReadFArray(x => new BuildInfo(x), numBuildings);
            if ((PackMask & 1) == 1) RestrictionTables = r.Skip(2).ReadL16PMany<uint, uint>("I", x => x.ReadUInt32());
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var landblock = Id & 0xFFFF0000;
            return [
                new MetaInfo($"{nameof(LandblockInfo)}: {Id:X8}", items: [
                    new($"NumCells: {NumCells}"),
                    NumCells > 0 ? new("Objects", items: Enumerable.Range(0, (int)NumCells).Select(i => new MetaInfo($"{landblock + 0x100 + i:X8}", clickable: true))) : null,
                    Objects.Length > 0 ? new("Objects", items: Objects.Select(x => {
                        var items = (x as IHaveMetaInfo).GetInfoNodes();
                        var name = items[0].Name.Replace("ID: ", "");
                        items.RemoveAt(0);
                        return new MetaInfo(name, items: items, clickable: true);
                    })) : null,
                    //PackMask != 0 ? new($"PackMask: {PackMask}") : null,
                    Buildings.Length > 0 ? new("Buildings", items: Buildings.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))) : null,
                    RestrictionTables.Count > 0 ? new("Restrictions", items: RestrictionTables.Select(x => new MetaInfo($"{x.Key:X8}: {x.Value:X8}"))) : null,
                ])
            ];
        }
    }
    #endregion

    #region LanguageInfo
    /// <summary>
    /// This is in client_local_English.dat with the ID of 0x41000000.
    /// 
    /// Contains some very basic language and formatting rules.
    /// </summary>
    //: New
    [PakFileType(PakFileType.StringState)]
    public class LanguageInfo : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x41000000;

        public readonly int Version;
        public readonly short Base;
        public readonly short NumDecimalDigits;
        public readonly bool LeadingZero;

        public readonly short GroupingSize;
        public readonly char[] Numerals;
        public readonly char[] DecimalSeperator;
        public readonly char[] GroupingSeperator;
        public readonly char[] NegativeNumberFormat;
        public readonly bool IsZeroSingular;
        public readonly bool IsOneSingular;
        public readonly bool IsNegativeOneSingular;
        public readonly bool IsTwoOrMoreSingular;
        public readonly bool IsNegativeTwoOrLessSingular;

        public readonly char[] TreasurePrefixLetters;
        public readonly char[] TreasureMiddleLetters;
        public readonly char[] TreasureSuffixLetters;
        public readonly char[] MalePlayerLetters;
        public readonly char[] FemalePlayerLetters;
        public readonly uint ImeEnabledSetting;

        public readonly uint SymbolColor;
        public readonly uint SymbolColorText;
        public readonly uint SymbolHeight;
        public readonly uint SymbolTranslucence;
        public readonly uint SymbolPlacement;
        public readonly uint CandColorBase;
        public readonly uint CandColorBorder;
        public readonly uint CandColorText;
        public readonly uint CompColorInput;
        public readonly uint CompColorTargetConv;
        public readonly uint CompColorConverted;
        public readonly uint CompColorTargetNotConv;
        public readonly uint CompColorInputErr;
        public readonly uint CompTranslucence;
        public readonly uint CompColorText;
        public readonly uint OtherIME;

        public readonly int WordWrapOnSpace;
        public readonly char[] AdditionalSettings;
        public readonly uint AdditionalFlags;

        public LanguageInfo(BinaryReader r)
        {
            Version = r.ReadInt32();
            Base = r.ReadInt16();
            NumDecimalDigits = r.ReadInt16();
            LeadingZero = r.ReadBoolean();

            GroupingSize = r.ReadInt16();
            Numerals = UnpackList(r);
            DecimalSeperator = UnpackList(r);
            GroupingSeperator = UnpackList(r);
            NegativeNumberFormat = UnpackList(r);
            IsZeroSingular = r.ReadBoolean();
            IsOneSingular = r.ReadBoolean();
            IsNegativeOneSingular = r.ReadBoolean();
            IsTwoOrMoreSingular = r.ReadBoolean();
            IsNegativeTwoOrLessSingular = r.ReadBoolean(); r.Align();

            TreasurePrefixLetters = UnpackList(r);
            TreasureMiddleLetters = UnpackList(r);
            TreasureSuffixLetters = UnpackList(r);
            MalePlayerLetters = UnpackList(r);
            FemalePlayerLetters = UnpackList(r);
            ImeEnabledSetting = r.ReadUInt32();

            SymbolColor = r.ReadUInt32();
            SymbolColorText = r.ReadUInt32();
            SymbolHeight = r.ReadUInt32();
            SymbolTranslucence = r.ReadUInt32();
            SymbolPlacement = r.ReadUInt32();
            CandColorBase = r.ReadUInt32();
            CandColorBorder = r.ReadUInt32();
            CandColorText = r.ReadUInt32();
            CompColorInput = r.ReadUInt32();
            CompColorTargetConv = r.ReadUInt32();
            CompColorConverted = r.ReadUInt32();
            CompColorTargetNotConv = r.ReadUInt32();
            CompColorInputErr = r.ReadUInt32();
            CompTranslucence = r.ReadUInt32();
            CompColorText = r.ReadUInt32();
            OtherIME = r.ReadUInt32();

            WordWrapOnSpace = r.ReadInt32();
            AdditionalSettings = UnpackList(r);
            AdditionalFlags = r.ReadUInt32();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(LanguageInfo)}: {Id:X8}", items: [])
        ];

        static char[] UnpackList(BinaryReader r)
        {
            var l = new List<char>();
            var numElements = r.ReadByte();
            for (var i = 0; i < numElements; i++) l.Add((char)r.ReadUInt16());
            return l.ToArray();
        }
    }
    #endregion

    #region LanguageString
    /// <summary>
    /// These are client_portal.dat files starting with 0x31.
    /// This is called a "String" in the client; It has been renamed to avoid conflicts with the generic "String" class.
    /// </summary>
    //: New
    [PakFileType(PakFileType.String)]
    public class LanguageString : FileType, IHaveMetaInfo
    {
        public string CharBuffer;

        public LanguageString(BinaryReader r)
        {
            Id = r.ReadUInt32();
            CharBuffer = r.ReadC32Encoding(Encoding.Default); //:TODO ?FALLBACK
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(LanguageString)}: {Id:X8}", items: [])
        ];
    }
    #endregion

    #region MotionTable
    [PakFileType(PakFileType.MotionTable)]
    public class MotionTable : FileType, IHaveMetaInfo
    {
        public static Dictionary<ushort, MotionCommand> RawToInterpreted = Enum.GetValues(typeof(MotionCommand)).Cast<object>().ToDictionary(x => (ushort)(uint)x, x => (MotionCommand)x);
        public readonly uint DefaultStyle;
        public readonly IDictionary<uint, uint> StyleDefaults;
        public readonly IDictionary<uint, MotionData> Cycles;
        public readonly IDictionary<uint, MotionData> Modifiers;
        public readonly IDictionary<uint, IDictionary<uint, MotionData>> Links;

        public MotionTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            DefaultStyle = r.ReadUInt32();
            StyleDefaults = r.ReadL32PMany<uint, uint>("I", x => x.ReadUInt32());
            Cycles = r.ReadL32PMany<uint, MotionData>("I", x => new MotionData(x));
            Modifiers = r.ReadL32PMany<uint, MotionData>("I", x => new MotionData(x));
            Links = r.ReadL32PMany<uint, IDictionary<uint, MotionData>>("I", x => x.ReadL32PMany<uint, MotionData>("I", y => new MotionData(y)));
        }

        //: FileTypes.MotionTable
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            static string GetLabel(uint combined)
            {
                var stanceKey = (ushort)(combined >> 16);
                var motionKey = (ushort)combined;
                if (RawToInterpreted.TryGetValue(stanceKey, out var stance) && RawToInterpreted.TryGetValue(motionKey, out var motion)) return $"{stance} - {motion}";
                else if (Enum.IsDefined(typeof(MotionCommand), combined)) return $"{(MotionCommand)combined}";
                else return $"{combined:X8}";
            }
            return [
                new($"{nameof(MotionTable)}: {Id:X8}", items: [
                    new($"Default style: {(MotionCommand)DefaultStyle}"),
                    new("Style defaults", items: StyleDefaults.OrderBy(i => i.Key).Select(x => new MetaInfo($"{(MotionCommand)x.Key}: {(MotionCommand)x.Value}"))),
                    new("Cycles", items: Cycles.OrderBy(i => i.Key).Select(x => new MetaInfo(GetLabel(x.Key), items: (x.Value as IHaveMetaInfo).GetInfoNodes()))),
                    new("Modifiers", items: Modifiers.OrderBy(i => i.Key).Select(x => new MetaInfo(GetLabel(x.Key), items: (x.Value as IHaveMetaInfo).GetInfoNodes()))),
                    new("Links", items: Links.OrderBy(i => i.Key).Select(x => new MetaInfo(GetLabel(x.Key), items: x.Value.OrderBy(i => i.Key).Select(y => new MetaInfo(GetLabel(y.Key), items: (y.Value as IHaveMetaInfo).GetInfoNodes()))))),
                ])
            ];
        }

        /// <summary>
        /// Gets the default style for the requested MotionStance
        /// </summary>
        /// <returns>The default style or MotionCommand.Invalid if not found</returns>
        MotionCommand GetDefaultMotion(MotionStance style) => StyleDefaults.TryGetValue((uint)style, out var z) ? (MotionCommand)z : MotionCommand.Invalid;
        public float GetAnimationLength(MotionCommand motion) => GetAnimationLength((MotionStance)DefaultStyle, motion, GetDefaultMotion((MotionStance)DefaultStyle));
        public float GetAnimationLength(MotionStance stance, MotionCommand motion, MotionCommand? currentMotion = null) => GetAnimationLength(stance, motion, currentMotion ?? GetDefaultMotion(stance));

        public float GetCycleLength(MotionStance stance, MotionCommand motion)
        {
            var key = (uint)stance << 16 | (uint)motion & 0xFFFFF;
            if (!Cycles.TryGetValue(key, out var motionData) || motionData == null) return 0.0f;

            var length = 0.0f;
            foreach (var anim in motionData.Anims) length += GetAnimationLength(anim);
            return length;
        }

        static readonly ConcurrentDictionary<AttackFrameParams, List<(float time, AttackHook attackHook)>> attackFrameCache = new ConcurrentDictionary<AttackFrameParams, List<(float time, AttackHook attackHook)>>();

        public List<(float time, AttackHook attackHook)> GetAttackFrames(uint motionTableId, MotionStance stance, MotionCommand motion)
        {
            // could also do uint, and then a packed ulong, but would be more complicated maybe?
            var attackFrameParams = new AttackFrameParams(motionTableId, stance, motion);
            if (attackFrameCache.TryGetValue(attackFrameParams, out var attackFrames))
                return attackFrames;

            var motionTable = DatabaseManager.Portal.GetFile<MotionTable>(motionTableId);

            var animData = GetAnimData(stance, motion, GetDefaultMotion(stance));

            var frameNums = new List<int>();
            var attackHooks = new List<AttackHook>();
            var totalFrames = 0;
            foreach (var anim in animData)
            {
                var animation = DatabaseManager.Portal.GetFile<Animation>(anim.AnimId);
                foreach (var frame in animation.PartFrames)
                {
                    foreach (var hook in frame.Hooks) if (hook is AttackHook attackHook) { frameNums.Add(totalFrames); attackHooks.Add(attackHook); }
                    totalFrames++;
                }
            }

            attackFrames = [];
            for (var i = 0; i < frameNums.Count; i++) attackFrames.Add(((float)frameNums[i] / totalFrames, attackHooks[i]));

            attackFrameCache.TryAdd(attackFrameParams, attackFrames);

            return attackFrames;
        }

        public AnimData[] GetAnimData(MotionStance stance, MotionCommand motion, MotionCommand currentMotion)
        {
            var animData = new AnimData[0];
            var motionKey = (uint)stance << 16 | (uint)currentMotion & 0xFFFFF;
            if (!Links.TryGetValue(motionKey, out var link) || link == null) return animData;
            if (!link.TryGetValue((uint)motion, out var motionData) || motionData == null)
            {
                motionKey = (uint)stance << 16;
                if (!Links.TryGetValue(motionKey, out link) || link == null) return animData;
                if (!link.TryGetValue((uint)motion, out motionData) || motionData == null) return animData;
            }
            return motionData.Anims;
        }

        public float GetAnimationLength(MotionStance stance, MotionCommand motion, MotionCommand currentMotion)
        {
            var animData = GetAnimData(stance, motion, currentMotion);
            var length = 0.0f;
            foreach (var anim in animData) length += GetAnimationLength(anim);
            return length;
        }

        public float GetAnimationLength(AnimData anim)
        {
            var highFrame = anim.HighFrame;
            // get the maximum # of animation frames
            var animation = DatabaseManager.Portal.GetFile<Animation>(anim.AnimId);
            if (anim.HighFrame == -1) highFrame = (int)animation.NumFrames;
            if (highFrame > animation.NumFrames)
            {
                // magic windup for level 6 spells appears to be the only animation w/ bugged data
                //Console.WriteLine($"MotionTable.GetAnimationLength({anim}): highFrame({highFrame}) > animation.NumFrames({animation.NumFrames})");
                highFrame = (int)animation.NumFrames;
            }
            var numFrames = highFrame - anim.LowFrame;
            return numFrames / Math.Abs(anim.Framerate); // framerates can be negative, which tells the client to play in reverse
        }

        public XPosition GetAnimationFinalPositionFromStart(XPosition position, float objScale, MotionCommand motion)
        {
            var defaultStyle = (MotionStance)DefaultStyle;
            var defaultMotion = GetDefaultMotion(defaultStyle); // get the default motion for the default
            return GetAnimationFinalPositionFromStart(position, objScale, defaultMotion, defaultStyle, motion);
        }

        public XPosition GetAnimationFinalPositionFromStart(XPosition position, float objScale, MotionCommand currentMotionState, MotionStance style, MotionCommand motion)
        {
            var length = 0F; // init our length var...will return as 0 if not found
            var finalPosition = new XPosition();
            var motionHash = ((uint)currentMotionState & 0xFFFFFF) | ((uint)style << 16);

            if (Links.ContainsKey(motionHash))
            {
                var links = Links[motionHash];
                if (links.ContainsKey((uint)motion))
                {
                    // loop through all that animations to get our total count
                    for (var i = 0; i < links[(uint)motion].Anims.Length; i++)
                    {
                        var anim = links[(uint)motion].Anims[i];
                        uint numFrames;
                        // check if the animation is set to play the whole thing, in which case we need to get the numbers of frames in the raw animation
                        if ((anim.LowFrame == 0) && (anim.HighFrame == -1))
                        {
                            var animation = DatabaseManager.Portal.GetFile<Animation>(anim.AnimId);
                            numFrames = animation.NumFrames;
                            if (animation.PosFrames.Length > 0)
                            {
                                finalPosition = position;
                                var origin = new Vector3(position.PositionX, position.PositionY, position.PositionZ);
                                var orientation = new Quaternion(position.RotationX, position.RotationY, position.RotationZ, position.RotationW);
                                foreach (var posFrame in animation.PosFrames)
                                {
                                    origin += Vector3.Transform(posFrame.Origin, orientation) * objScale;

                                    orientation *= posFrame.Orientation;
                                    orientation = Quaternion.Normalize(orientation);
                                }

                                finalPosition.PositionX = origin.X;
                                finalPosition.PositionY = origin.Y;
                                finalPosition.PositionZ = origin.Z;

                                finalPosition.RotationW = orientation.W;
                                finalPosition.RotationX = orientation.X;
                                finalPosition.RotationY = orientation.Y;
                                finalPosition.RotationZ = orientation.Z;
                            }
                            else return position;
                        }
                        else numFrames = (uint)(anim.HighFrame - anim.LowFrame);

                        length += numFrames / Math.Abs(anim.Framerate); // Framerates can be negative, which tells the client to play in reverse
                    }
                }
            }

            return finalPosition;
        }

        public MotionStance[] GetStances()
        {
            var stances = new HashSet<MotionStance>();
            foreach (var cycle in Cycles.Keys)
            {
                var stance = (MotionStance)(0x80000000 | cycle >> 16);
                if (!stances.Contains(stance)) stances.Add(stance);
            }
            if (stances.Count > 0 && !stances.Contains(MotionStance.Invalid)) stances.Add(MotionStance.Invalid);
            return stances.ToArray();
        }

        public MotionCommand[] GetMotionCommands(MotionStance stance = MotionStance.Invalid)
        {
            var commands = new HashSet<MotionCommand>();
            foreach (var cycle in Cycles.Keys)
            {
                if ((cycle >> 16) != ((uint)stance & 0xFFFF)) continue;
                var rawCommand = (ushort)(cycle & 0xFFFF);
                var motionCommand = RawToInterpreted[rawCommand];
                if (!commands.Contains(motionCommand)) commands.Add(motionCommand);
            }
            foreach (var kvp in Links)
            {
                var stanceMotion = kvp.Key;
                var links = kvp.Value;
                if ((stanceMotion >> 16) != ((uint)stance & 0xFFFF)) continue;
                foreach (var link in links.Keys)
                {
                    var rawCommand = (ushort)(link & 0xFFFF);
                    var motionCommand = RawToInterpreted[rawCommand];
                    if (!commands.Contains(motionCommand)) commands.Add(motionCommand);
                }
            }
            return commands.ToArray();
        }
    }
    #endregion

    #region NameFilterTable
    //: FileTypes.GeneratorTable
    [PakFileType(PakFileType.NameFilterTable)]
    public class NameFilterTable : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E000020;

        // Key is a list of a WCIDs that are "bad" and should not exist. The value is always 1 (could be a bool?)
        public readonly IDictionary<uint, NameFilterLanguageData> LanguageData;

        public NameFilterTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            LanguageData = r.Skip(1).ReadL8PMany<uint, NameFilterLanguageData>("I", x => new NameFilterLanguageData(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(NameFilterTable)}: {Id:X8}", items: LanguageData.Select(
                x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes(tag: tag))
            ))
        ];
    }
    #endregion

    #region Palette
    /// <summary>
    /// These are client_portal.dat files starting with 0x04. 
    /// </summary>
    //: FileTypes.Palette
    [PakFileType(PakFileType.Palette)]
    public class Palette : FileType, IHaveMetaInfo
    {
        /// <summary>
        /// Color data is stored in ARGB format
        /// </summary>
        public uint[] Colors;

        public Palette(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Colors = r.ReadL32PArray<uint>("I");
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(Palette)}: {Id:X8}", items: Colors.Select(
                x => new MetaInfo(ColorX.ToRGBA(x))
            )),
        ];
    }
    #endregion

    #region PaletteSet
    /// <summary>
    /// These are client_portal.dat files starting with 0x0F. 
    /// They contain, as the name may imply, a set of palettes (0x04 files)
    /// </summary>
    //: FileTypes.Palette
    [PakFileType(PakFileType.PaletteSet)]
    public class PaletteSet : FileType, IHaveMetaInfo
    {
        public uint[] PaletteList;

        public PaletteSet(BinaryReader r)
        {
            Id = r.ReadUInt32();
            PaletteList = r.ReadL32PArray<uint>("I");
        }

        /// <summary>
        /// Returns the palette ID (uint, 0x04 file) from the Palette list based on the corresponding hue
        /// Hue is mostly (only?) used in Character Creation data.
        /// "Hue" referred to as "shade" in acclient.c
        /// </summary>
        public uint GetPaletteID(double hue)
        {
            // Make sure the PaletteList has valid data and the hue is within valid ranges
            if (PaletteList.Length == 0 || hue < 0 || hue > 1) return 0;
            // This was the original function - had an issue specifically with Aerfalle's Pallium, WCID 8133
            // var palIndex = Convert.ToInt32(Convert.ToDouble(PaletteList.Count - 0.000001) * hue); // Taken from acclient.c (PalSet::GetPaletteID)
            // Hue is stored in DB as a percent of the total, so do some math to figure out the int position
            var palIndex = (int)((PaletteList.Length - 0.000001) * hue); // Taken from acclient.c (PalSet::GetPaletteID)
            // Since the hue numbers are a little odd, make sure we're in the bounds.
            if (palIndex < 0) palIndex = 0;
            if (palIndex > PaletteList.Length - 1) palIndex = PaletteList.Length - 1;
            return PaletteList[palIndex];
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(PaletteSet)}: {Id:X8}", items: PaletteList.Select(
                x => new MetaInfo($"{x:X8}", clickable: true)
            )),
        ];
    }
    #endregion

    #region ParticleEmitterInfo
    /// <summary>
    /// These are client_portal.dat files starting with 0x32. 
    /// </summary>
    //: FileTypes.ParticleEmitterInfo
    [PakFileType(PakFileType.ParticleEmitter)]
    public class ParticleEmitterInfo : FileType, IHaveMetaInfo
    {
        public readonly uint Unknown;
        public readonly EmitterType EmitterType;
        public readonly ParticleType ParticleType;
        public readonly uint GfxObjId; public readonly uint HwGfxObjId;
        public readonly double Birthrate;
        public readonly int MaxParticles; public readonly int InitialParticles; public readonly int TotalParticles;
        public readonly double TotalSeconds;
        public readonly double Lifespan; public readonly double LifespanRand;
        public readonly Vector3 OffsetDir; public readonly float MinOffset; public readonly float MaxOffset;
        public readonly Vector3 A; public readonly float MinA; public readonly float MaxA;
        public readonly Vector3 B; public readonly float MinB; public readonly float MaxB;
        public readonly Vector3 C; public readonly float MinC; public readonly float MaxC;
        public readonly float StartScale; public readonly float FinalScale; public readonly float ScaleRand;
        public readonly float StartTrans; public readonly float FinalTrans; public readonly float TransRand;
        public readonly int IsParentLocal;

        public ParticleEmitterInfo(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Unknown = r.ReadUInt32();
            EmitterType = (EmitterType)r.ReadInt32();
            ParticleType = (ParticleType)r.ReadInt32();
            GfxObjId = r.ReadUInt32(); HwGfxObjId = r.ReadUInt32();
            Birthrate = r.ReadDouble();
            MaxParticles = r.ReadInt32(); InitialParticles = r.ReadInt32(); TotalParticles = r.ReadInt32();
            TotalSeconds = r.ReadDouble();
            Lifespan = r.ReadDouble(); LifespanRand = r.ReadDouble();
            OffsetDir = r.ReadVector3(); MinOffset = r.ReadSingle(); MaxOffset = r.ReadSingle();
            A = r.ReadVector3(); MinA = r.ReadSingle(); MaxA = r.ReadSingle();
            B = r.ReadVector3(); MinB = r.ReadSingle(); MaxB = r.ReadSingle();
            C = r.ReadVector3(); MinC = r.ReadSingle(); MaxC = r.ReadSingle();
            StartScale = r.ReadSingle(); FinalScale = r.ReadSingle(); ScaleRand = r.ReadSingle();
            StartTrans = r.ReadSingle(); FinalTrans = r.ReadSingle(); TransRand = r.ReadSingle();
            IsParentLocal = r.ReadInt32();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(ParticleEmitterInfo)}: {Id:X8}", items: [
                new($"EmitterType: {EmitterType}"),
                new($"ParticleType: {ParticleType}"),
                new($"GfxObjId: {GfxObjId:X8}", clickable: true),
                new($"HWGfxObjId: {HwGfxObjId:X8}", clickable: true),
                new($"Birthrate: {Birthrate}"),
                new($"MaxParticles: {MaxParticles} InitialParticles: {InitialParticles} TotalParticles: {TotalParticles}"),
                new($"TotalSeconds: {TotalSeconds}"),
                new($"Lifespan: {Lifespan} LifespanRand: {LifespanRand}"),
                new($"OffsetDir: {OffsetDir} MinOffset: {MinOffset} MaxOffset: {MaxOffset}"),
                new($"A: {A} MinA: {MinA}: MaxA: {MaxA}"),
                new($"B: {B} MinB: {MinB}: MaxB: {MaxB}"),
                new($"C: {C} MinC: {MinC}: MaxC: {MaxC}"),
                new($"StartScale: {StartScale} FinalScale: {FinalScale}: ScaleRand: {ScaleRand}"),
                new($"StartTrans: {StartTrans} FinalTrans: {FinalTrans}: TransRand: {TransRand}"),
                new($"IsParentLocal: {IsParentLocal}"),
            ])
        ];

        public override string ToString()
        {
            var b = new StringBuilder();
            b.AppendLine("------------------");
            b.AppendLine($"ID: {Id:X8}");
            b.AppendLine($"EmitterType: {EmitterType}");
            b.AppendLine($"ParticleType: {ParticleType}");
            b.AppendLine($"GfxObjID: {GfxObjId:X8} HWGfxObjID: {HwGfxObjId:X8}");
            b.AppendLine($"Birthrate: {Birthrate}");
            b.AppendLine($"MaxParticles: {MaxParticles} InitialParticles: {InitialParticles} TotalParticles: {TotalParticles}");
            b.AppendLine($"TotalSeconds: {TotalSeconds}");
            b.AppendLine($"Lifespan: {Lifespan} LifespanRand: {LifespanRand}");
            b.AppendLine($"OffsetDir: {OffsetDir} MinOffset: {MinOffset} MaxOffset: {MaxOffset}");
            b.AppendLine($"A: {A} MinA: {MinA} MaxA: {MaxA}");
            b.AppendLine($"B: {B} MinB: {MinB} MaxB: {MaxB}");
            b.AppendLine($"C: {C} MinC: {MinC} MaxC: {MaxC}");
            b.AppendLine($"StartScale: {StartScale} FinalScale: {FinalScale} ScaleRand: {ScaleRand}");
            b.AppendLine($"StartTrans: {StartTrans} FinalTrans: {FinalTrans} TransRand: {TransRand}");
            b.AppendLine($"IsParentLocal: {IsParentLocal}");
            return b.ToString();
        }
    }
    #endregion

    #region PhysicsScript
    /// <summary>
    /// These are client_portal.dat files starting with 0x33. 
    /// </summary>
    //: FileTypes.PhysicsScript
    [PakFileType(PakFileType.PhysicsScript)]
    public class PhysicsScript : FileType, IHaveMetaInfo
    {
        public readonly PhysicsScriptData[] ScriptData;

        public PhysicsScript(BinaryReader r)
        {
            Id = r.ReadUInt32();
            ScriptData = r.ReadL32FArray(x => new PhysicsScriptData(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(PhysicsScript)}: {Id:X8}", items: new List<MetaInfo> {
                new("Scripts", items: ScriptData.Select(x => new MetaInfo($"HookType: {x.Hook.HookType}, StartTime: {x.StartTime}", items: (AnimationHook.Factory(x.Hook) as IHaveMetaInfo).GetInfoNodes(tag: tag)))),
            })
        ];
    }
    #endregion

    #region PhysicsScriptTable
    /// <summary>
    /// These are client_portal.dat files starting with 0x34. 
    /// </summary>
    //: FileTypes.PhysicsScriptTable
    [PakFileType(PakFileType.PhysicsScriptTable)]
    public class PhysicsScriptTable : FileType, IHaveMetaInfo
    {
        public readonly IDictionary<uint, PhysicsScriptTableData> ScriptTable;

        public PhysicsScriptTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            ScriptTable = r.ReadL32PMany<uint, PhysicsScriptTableData>("I", x => new PhysicsScriptTableData(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(PhysicsScriptTable)}: {Id:X8}", items: [
                new("ScriptTable", items: ScriptTable.Select(x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes()))),
            ])
        ];
    }
    #endregion

    #region QualityFilter
    //: New
    [PakFileType(PakFileType.QualityFilter)]
    public class QualityFilter : FileType, IHaveMetaInfo
    {
        public readonly uint[] IntStatFilter;
        public readonly uint[] Int64StatFilter;
        public readonly uint[] BoolStatFilter;
        public readonly uint[] FloatStatFilter;
        public readonly uint[] DidStatFilter;
        public readonly uint[] IidStatFilter;
        public readonly uint[] StringStatFilter;
        public readonly uint[] PositionStatFilter;
        public readonly uint[] AttributeStatFilter;
        public readonly uint[] Attribute2ndStatFilter;
        public readonly uint[] SkillStatFilter;

        public QualityFilter(BinaryReader r)
        {
            Id = r.ReadUInt32();
            var numInt = r.ReadUInt32();
            var numInt64 = r.ReadUInt32();
            var numBool = r.ReadUInt32();
            var numFloat = r.ReadUInt32();
            var numDid = r.ReadUInt32();
            var numIid = r.ReadUInt32();
            var numString = r.ReadUInt32();
            var numPosition = r.ReadUInt32();
            IntStatFilter = r.ReadPArray<uint>("I", (int)numInt);
            Int64StatFilter = r.ReadPArray<uint>("I", (int)numInt64);
            BoolStatFilter = r.ReadPArray<uint>("I", (int)numBool);
            FloatStatFilter = r.ReadPArray<uint>("I", (int)numFloat);
            DidStatFilter = r.ReadPArray<uint>("I", (int)numDid);
            IidStatFilter = r.ReadPArray<uint>("I", (int)numIid);
            StringStatFilter = r.ReadPArray<uint>("I", (int)numString);
            PositionStatFilter = r.ReadPArray<uint>("I", (int)numPosition);
            var numAttribute = r.ReadUInt32();
            var numAttribute2nd = r.ReadUInt32();
            var numSkill = r.ReadUInt32();
            AttributeStatFilter = r.ReadPArray<uint>("I", (int)numAttribute);
            Attribute2ndStatFilter = r.ReadPArray<uint>("I", (int)numAttribute2nd);
            SkillStatFilter = r.ReadPArray<uint>("I", (int)numSkill);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(QualityFilter)}: {Id:X8}", items: [])
        ];
    }
    #endregion

    #region RegionDesc
    /// <summary>
    /// This is the client_portal.dat file starting with 0x13 -- There is only one of these, which is why REGION_ID is a constant.
    /// </summary>
    //: FileTypes.Region
    [PakFileType(PakFileType.Region)]
    public class RegionDesc : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x13000000;

        public readonly uint RegionNumber;
        public readonly uint Version;
        public readonly string RegionName;
        public readonly LandDefs LandDefs;
        public readonly GameTime GameTime;
        public readonly uint PartsMask;
        public readonly SkyDesc SkyInfo;
        public readonly SoundDesc SoundInfo;
        public readonly SceneDesc SceneInfo;
        public readonly TerrainDesc TerrainInfo;
        public readonly RegionMisc RegionMisc;

        public RegionDesc(BinaryReader r)
        {
            Id = r.ReadUInt32();
            RegionNumber = r.ReadUInt32();
            Version = r.ReadUInt32();
            RegionName = r.ReadL16Encoding(Encoding.Default); r.Align(); // "Dereth"

            LandDefs = new LandDefs(r);
            GameTime = new GameTime(r);
            PartsMask = r.ReadUInt32();
            if ((PartsMask & 0x10) != 0) SkyInfo = new SkyDesc(r);
            if ((PartsMask & 0x01) != 0) SoundInfo = new SoundDesc(r);
            if ((PartsMask & 0x02) != 0) SceneInfo = new SceneDesc(r);
            TerrainInfo = new TerrainDesc(r);
            if ((PartsMask & 0x0200) != 0) RegionMisc = new RegionMisc(r);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(RegionDesc)}: {Id:X8}", items: [
                new($"RegionNum: {RegionNumber}"),
                new($"Version: {Version}"),
                new($"Name: {RegionName}"),
                new("LandDefs", items: (LandDefs as IHaveMetaInfo).GetInfoNodes()),
                new("GameTime", items: (GameTime as IHaveMetaInfo).GetInfoNodes()),
                new($"PartsMask: {PartsMask:X8}"),
                (PartsMask & 0x10) != 0 ? new("SkyInfo", items: (SkyInfo as IHaveMetaInfo).GetInfoNodes()) : null,
                (PartsMask & 0x01) != 0 ? new("SoundInfo", items: (SoundInfo as IHaveMetaInfo).GetInfoNodes()) : null,
                (PartsMask & 0x02) != 0 ? new("SceneInfo", items: (SceneInfo as IHaveMetaInfo).GetInfoNodes()) : null,
                new("TerrainInfo", items: (TerrainInfo as IHaveMetaInfo).GetInfoNodes()),
                (PartsMask & 0x200) != 0 ? new("RegionMisc", items: (RegionMisc as IHaveMetaInfo).GetInfoNodes()) : null,
            ])
        ];
    }
    #endregion

    #region RenderTexture
    /// <summary>
    /// These are client_portal.dat files starting with 0x15.
    /// These are references to the textures for the DebugConsole
    ///
    /// This is identical to SurfaceTexture.
    ///
    /// As defined in DidMapper.UNIQUEDB (0x25000002)
    /// 0x15000000 = ConsoleOutputBackgroundTexture
    /// 0x15000001 = ConsoleInputBackgroundTexture
    /// </summary>
    //: New
    [PakFileType(PakFileType.RenderTexture)]
    public class RenderTexture : FileType, IHaveMetaInfo
    {
        public readonly int Unknown;
        public readonly byte UnknownByte;
        public readonly uint[] Textures; // These values correspond to a Surface (0x06) entry

        public RenderTexture(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Unknown = r.ReadInt32();
            UnknownByte = r.ReadByte();
            Textures = r.ReadL32PArray<uint>("I");
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(RenderTexture)}: {Id:X8}", items: [])
        ];
    }
    #endregion

    #region Scene
    /// <summary>
    /// These are client_portal.dat files starting with 0x12. 
    /// </summary>
    //: FileTypes.Scene
    [PakFileType(PakFileType.Scene)]
    public class Scene : FileType, IHaveMetaInfo
    {
        public readonly ObjectDesc[] Objects;

        public Scene(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Objects = r.ReadL32FArray(x => new ObjectDesc(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(Scene)}: {Id:X8}", items: [
                new("Objects", items: Objects.Select(x => {
                    var items = (x as IHaveMetaInfo).GetInfoNodes();
                    var name = items[0].Name.Replace("Object ID: ", "");
                    items.RemoveAt(0);
                    return new MetaInfo(name, items: items, clickable: true);
                })),
            ])
        ];
    }
    #endregion

    #region SecondaryAttributeTable
    //: FileTypes.SecondaryAttributeTable
    [PakFileType(PakFileType.SecondaryAttributeTable)]
    public class SecondaryAttributeTable : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E000003;

        public readonly Attribute2ndBase MaxHealth;
        public readonly Attribute2ndBase MaxStamina;
        public readonly Attribute2ndBase MaxMana;

        public SecondaryAttributeTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            MaxHealth = new Attribute2ndBase(r);
            MaxStamina = new Attribute2ndBase(r);
            MaxMana = new Attribute2ndBase(r);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(SecondaryAttributeTable)}: {Id:X8}", items: [
                new("Health", items: (MaxHealth.Formula as IHaveMetaInfo).GetInfoNodes(tag: tag)),
                new("Stamina", items: (MaxStamina.Formula as IHaveMetaInfo).GetInfoNodes(tag: tag)),
                new("Mana", items: (MaxMana.Formula as IHaveMetaInfo).GetInfoNodes(tag: tag)),
            ])
        ];
    }
    #endregion

    #region SetupModel
    /// <summary>
    /// These are client_portal.dat files starting with 0x02. 
    /// They are basically 3D model descriptions.
    /// </summary>
    //: FileTypes.Setup
    [PakFileType(PakFileType.Setup)]
    public class SetupModel : FileType, IHaveMetaInfo
    {
        public static readonly SetupModel Empty = new SetupModel();
        public readonly SetupFlags Flags;
        public readonly bool AllowFreeHeading;
        public readonly bool HasPhysicsBSP;
        public readonly uint[] Parts;
        public readonly uint[] ParentIndex;
        public readonly Vector3[] DefaultScale;
        public readonly IDictionary<int, LocationType> HoldingLocations;
        public readonly IDictionary<int, LocationType> ConnectionPoints;
        public readonly IDictionary<int, PlacementType> PlacementFrames;
        public readonly CylSphere[] CylSpheres;
        public readonly Sphere[] Spheres;
        public readonly float Height;
        public readonly float Radius;
        public readonly float StepUpHeight;
        public readonly float StepDownHeight;
        public readonly Sphere SortingSphere;
        public readonly Sphere SelectionSphere;
        public readonly IDictionary<int, LightInfo> Lights;
        public readonly uint DefaultAnimation;
        public readonly uint DefaultScript;
        public readonly uint DefaultMotionTable;
        public readonly uint DefaultSoundTable;
        public readonly uint DefaultScriptTable;

        public bool HasMissileFlightPlacement => PlacementFrames.ContainsKey((int)Placement.MissileFlight);

        SetupModel()
        {
            SortingSphere = Sphere.Empty;
            SelectionSphere = Sphere.Empty;
            AllowFreeHeading = true;
        }
        public SetupModel(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Flags = (SetupFlags)r.ReadUInt32();
            AllowFreeHeading = (Flags & SetupFlags.AllowFreeHeading) != 0;
            HasPhysicsBSP = (Flags & SetupFlags.HasPhysicsBSP) != 0;
            // Get all the GfxObjects in this SetupModel. These are all the 01-types.
            var numParts = r.ReadUInt32();
            Parts = r.ReadPArray<uint>("I", (int)numParts);
            if ((Flags & SetupFlags.HasParent) != 0) ParentIndex = r.ReadPArray<uint>("I", (int)numParts);
            if ((Flags & SetupFlags.HasDefaultScale) != 0) DefaultScale = r.ReadFArray(x => x.ReadVector3(), (int)numParts);
            HoldingLocations = r.ReadL32PMany<int, LocationType>("i", x => new LocationType(x));
            ConnectionPoints = r.ReadL32PMany<int, LocationType>("i", x => new LocationType(x));
            // there is a frame for each Part
            PlacementFrames = r.ReadL32PMany<int, PlacementType>("i", x => new PlacementType(r, (uint)Parts.Length));
            CylSpheres = r.ReadL32FArray(x => new CylSphere(x));
            Spheres = r.ReadL32FArray(x => new Sphere(x));
            Height = r.ReadSingle();
            Radius = r.ReadSingle();
            StepUpHeight = r.ReadSingle();
            StepDownHeight = r.ReadSingle();
            SortingSphere = new Sphere(r);
            SelectionSphere = new Sphere(r);
            Lights = r.ReadL32PMany<int, LightInfo>("i", x => new LightInfo(x));
            DefaultAnimation = r.ReadUInt32();
            DefaultScript = r.ReadUInt32();
            DefaultMotionTable = r.ReadUInt32();
            DefaultSoundTable = r.ReadUInt32();
            DefaultScriptTable = r.ReadUInt32();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(SetupModel)}: {Id:X8}", items: [
                Flags != 0 ? new($"Flags: {Flags}") : null,
                new("Parts", items: Parts.Select((x, i) => new MetaInfo($"{i} - {x:X8}", clickable: true))),
                Flags.HasFlag(SetupFlags.HasParent) ? new("Parents", items: ParentIndex.Select(x => new MetaInfo($"{x:X8}"))) : null,
                Flags.HasFlag(SetupFlags.HasDefaultScale) ? new("Default Scales", items: DefaultScale.Select(x => new MetaInfo($"{x}"))) : null,
                HoldingLocations.Count > 0 ? new("Holding Locations", items: HoldingLocations.OrderBy(i => i.Key).Select(x => new MetaInfo($"{x.Key} - {(ParentLocation)x.Key} - {x.Value}"))) : null,
                ConnectionPoints.Count > 0 ? new("Connection Points", items: ConnectionPoints.Select(x => new MetaInfo($"{x.Key}: {x.Value}"))) : null,
                new MetaInfo("Placement frames", items: PlacementFrames.OrderBy(i => i.Key).Select(x => new MetaInfo($"{x.Key} - {(Placement)x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes()))),
                CylSpheres.Length > 0 ? new("CylSpheres", items: CylSpheres.Select(x => new MetaInfo($"{x}"))) : null,
                Spheres.Length > 0 ? new("Spheres", items: Spheres.Select(x => new MetaInfo($"{x}"))) : null,
                new MetaInfo($"Height: {Height}"),
                new MetaInfo($"Radius: {Radius}"),
                new MetaInfo($"Step Up Height: {StepUpHeight}"),
                new MetaInfo($"Step Down Height: {StepDownHeight}"),
                new MetaInfo($"Sorting Sphere: {SortingSphere}"),
                new MetaInfo($"Selection Sphere: {SelectionSphere}"),
                Lights.Count > 0 ? new($"Lights", items: Lights.Select(x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes(tag: tag)))) : null,
                DefaultAnimation != 0 ? new($"Default Animation: {DefaultAnimation:X8}", clickable: true) : null,
                DefaultScript != 0 ? new($"Default Script: {DefaultScript:X8}", clickable: true) : null,
                DefaultMotionTable != 0 ? new($"Default Motion Table: {DefaultMotionTable:X8}", clickable: true) : null,
                DefaultSoundTable != 0 ? new($"Default Sound Table: {DefaultSoundTable:X8}", clickable: true) : null,
                DefaultScriptTable != 0 ? new($"Default Script Table: {DefaultScriptTable:X8}", clickable: true) : null,
            ])
        ];
    }
    #endregion

    #region SkillTable
    //: FileTypes.SkillTable
    [PakFileType(PakFileType.SkillTable)]
    public class SkillTable : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E000004;

        // Key is the SkillId
        public IDictionary<uint, SkillBase> SkillBaseHash;

        public SkillTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            SkillBaseHash = r.Skip(2).ReadL16PMany<uint, SkillBase>("I", x => new SkillBase(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(SkillTable)}: {Id:X8}", items: SkillBaseHash.OrderBy(i => i.Key).Where(x => !string.IsNullOrEmpty(x.Value.Name)).Select(
                x => new MetaInfo($"{x.Key}: {x.Value.Name}", items: (x.Value as IHaveMetaInfo).GetInfoNodes(tag: tag))
            ))
        ];

        public void AddRetiredSkills()
        {
            SkillBaseHash.Add((int)Skill.Axe, new SkillBase(new SkillFormula(PropertyAttribute.Strength, PropertyAttribute.Coordination, 3)));
            SkillBaseHash.Add((int)Skill.Bow, new SkillBase(new SkillFormula(PropertyAttribute.Coordination, PropertyAttribute.Undef, 2)));
            SkillBaseHash.Add((int)Skill.Crossbow, new SkillBase(new SkillFormula(PropertyAttribute.Coordination, PropertyAttribute.Undef, 2)));
            SkillBaseHash.Add((int)Skill.Dagger, new SkillBase(new SkillFormula(PropertyAttribute.Quickness, PropertyAttribute.Coordination, 3)));
            SkillBaseHash.Add((int)Skill.Mace, new SkillBase(new SkillFormula(PropertyAttribute.Strength, PropertyAttribute.Coordination, 3)));
            SkillBaseHash.Add((int)Skill.Spear, new SkillBase(new SkillFormula(PropertyAttribute.Strength, PropertyAttribute.Coordination, 3)));
            SkillBaseHash.Add((int)Skill.Staff, new SkillBase(new SkillFormula(PropertyAttribute.Strength, PropertyAttribute.Coordination, 3)));
            SkillBaseHash.Add((int)Skill.Sword, new SkillBase(new SkillFormula(PropertyAttribute.Strength, PropertyAttribute.Coordination, 3)));
            SkillBaseHash.Add((int)Skill.ThrownWeapon, new SkillBase(new SkillFormula(PropertyAttribute.Coordination, PropertyAttribute.Undef, 2)));
            SkillBaseHash.Add((int)Skill.UnarmedCombat, new SkillBase(new SkillFormula(PropertyAttribute.Strength, PropertyAttribute.Coordination, 3)));
        }
    }
    #endregion

    #region SoundTable
    /// <summary>
    /// SoundTable files contain a listing of which Wav types to play in response to certain events.
    /// They are located in the client_portal.dat and are files starting with 0x20
    /// </summary>
    //: FileTypes.SoundTable
    [PakFileType(PakFileType.SoundTable)]
    public class SoundTable : FileType, IHaveMetaInfo
    {
        public readonly uint Unknown; // As the name implies, not sure what this is
        // Not quite sure what this is for, but it's the same in every file.
        public readonly SoundTableData[] SoundHash;
        // The uint key corresponds to an Enum.Sound
        public readonly IDictionary<uint, SoundData> Data;

        public SoundTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Unknown = r.ReadUInt32();
            SoundHash = r.ReadL32FArray(x => new SoundTableData(x));
            Data = r.Skip(2).ReadL16PMany<uint, SoundData>("I", x => new SoundData(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(SoundTable)}: {Id:X8}", items: [
                new("SoundHash", items: SoundHash.Select(x => {
                    var items = (x as IHaveMetaInfo).GetInfoNodes();
                    var name = items[0].Name.Replace("Sound ID: ", "");
                    items.RemoveAt(0);
                    return new MetaInfo(name, items: items);
                })),
                new($"Sounds", items: Data.Select(x => new MetaInfo($"{(Sound)x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes()))),
            ])
        ];
    }
    #endregion

    #region SpellComponentTable
    //: FileTypes.SpellComponentTable
    [PakFileType(PakFileType.SpellComponentTable)]
    public class SpellComponentTable : FileType, IHaveMetaInfo
    {
        public enum Type
        {
            Scarab = 1,
            Herb = 2,
            Powder = 3,
            Potion = 4,
            Talisman = 5,
            Taper = 6,
            PotionPea = 7,
            TalismanPea = 5,
            TaperPea = 7
        }

        public const uint FILE_ID = 0x0E00000F;

        public readonly IDictionary<uint, SpellComponentBase> SpellComponents;

        public SpellComponentTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            var numComps = r.ReadUInt16(); r.Align(); // Should be 163 or 0xA3
            SpellComponents = r.ReadPMany<uint, SpellComponentBase>("I", x => new SpellComponentBase(r), numComps);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(SpellComponentTable)}: {Id:X8}", items: SpellComponents.Select(
                x => new MetaInfo($"{x.Key} - {x.Value.Name}", items: (x.Value as IHaveMetaInfo).GetInfoNodes(tag: tag))
            ))
        ];

        public static string GetSpellWords(SpellComponentTable comps, uint[] formula)
        {
            var firstSpellWord = string.Empty;
            var secondSpellWord = string.Empty;
            var thirdSpellWord = string.Empty;
            if (formula == null) return string.Empty;
            // Locate the herb component in the Spell formula
            for (var i = 0; i < formula.Length; i++) if (comps.SpellComponents[formula[i]].Type == (uint)Type.Herb) firstSpellWord = comps.SpellComponents[formula[i]].Text;
            // Locate the powder component in the Spell formula
            for (var i = 0; i < formula.Length; i++) if (comps.SpellComponents[formula[i]].Type == (uint)Type.Powder) secondSpellWord = comps.SpellComponents[formula[i]].Text;
            // Locate the potion component in the Spell formula
            for (var i = 0; i < formula.Length; i++) if (comps.SpellComponents[formula[i]].Type == (uint)Type.Potion) thirdSpellWord = comps.SpellComponents[formula[i]].Text;
            // We need to make sure our second spell word, if any, is capitalized
            // Some spell words have no "secondSpellWord", so we're basically making sure the third word is capitalized.
            var secondSpellWordSet = secondSpellWord + thirdSpellWord.ToLowerInvariant();
            if (secondSpellWordSet != string.Empty) { var firstLetter = secondSpellWordSet.Substring(0, 1).ToUpperInvariant(); secondSpellWordSet = firstLetter + secondSpellWordSet.Substring(1); }
            var result = $"{firstSpellWord} {secondSpellWordSet}".Trim();
            return result;
        }
    }
    #endregion

    #region SpellTable
    //: FileTypes.SpellTable
    [PakFileType(PakFileType.SpellTable)]
    public class SpellTable : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E00000E;

        public readonly IDictionary<uint, SpellBase> Spells;
        /// <summary>
        /// the key uint refers to the SpellSetID, set in PropInt.EquipmentSetId
        /// </summary>
        public readonly IDictionary<uint, SpellSet> SpellSet;

        public SpellTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Spells = r.Skip(2).ReadL16PMany<uint, SpellBase>("I", x => new SpellBase(x));
            SpellSet = r.Skip(2).ReadL16PMany<uint, SpellSet>("I", x => new SpellSet(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(SpellTable)}: {Id:X8}", items: [
                new("Spells", items: Spells.Select(x => new MetaInfo($"{x.Key}: {x.Value.Name}", items: (x.Value as IHaveMetaInfo).GetInfoNodes(tag: tag)))),
                new("Spell Sets", items: SpellSet.OrderBy(i => i.Key).Select(x => new MetaInfo($"{x.Key}: {(EquipmentSet)x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes(tag: tag)))),
            ])
        ];

        /// <summary>
        /// Generates a hash based on the string. Used to decrypt spell formulas and calculate taper rotation for players.
        /// </summary>
        public static uint ComputeHash(string strToHash)
        {
            var result = 0L;
            if (strToHash.Length > 0)
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                var str = Encoding.GetEncoding(1252).GetBytes(strToHash);
                foreach (var c in str)
                {
                    result = c + (result << 4);
                    if ((result & 0xF0000000) != 0) result = (result ^ ((result & 0xF0000000) >> 24)) & 0x0FFFFFFF;
                }
            }
            return (uint)result;
        }

        const uint LOWEST_TAPER_ID = 63; // This is the lowest id in the SpellComponentTable of a taper (Red Taper)

        /// <summary>
        /// Returns the correct spell formula, which is hashed from a player's account name
        /// </summary>
        public static uint[] GetSpellFormula(SpellTable spellTable, uint spellId, string accountName)
        {
            var spell = spellTable.Spells[spellId];
            return spell.FormulaVersion switch
            {
                1 => RandomizeVersion1(spell, accountName),
                2 => RandomizeVersion2(spell, accountName),
                3 => RandomizeVersion3(spell, accountName),
                _ => spell.Formula,
            };
        }

        static uint[] RandomizeVersion1(SpellBase spell, string accountName)
        {
            var comps = new List<uint>(spell.Formula);
            var hasTaper1 = false;
            var hasTaper2 = false;
            var hasTaper3 = false;

            var key = ComputeHash(accountName);
            var seed = key % 0x13D573;

            var scarab = comps[0];
            var herb_index = 1;
            if (comps.Count > 5) { herb_index = 2; hasTaper1 = true; }
            var herb = comps[herb_index];

            var powder_index = herb_index + 1;
            if (comps.Count > 6) { powder_index++; hasTaper2 = true; }
            var powder = comps[powder_index];

            var potion_index = powder_index + 1;
            var potion = comps[potion_index];

            var talisman_index = potion_index + 1;
            if (comps.Count > 7) { talisman_index++; hasTaper3 = true; }
            var talisman = comps[talisman_index];
            if (hasTaper1) comps[1] = (powder + 2 * herb + potion + talisman + scarab) % 0xC + LOWEST_TAPER_ID;
            if (hasTaper2) comps[3] = (scarab + herb + talisman + 2 * (powder + potion)) * (seed / (scarab + (powder + potion))) % 0xC + LOWEST_TAPER_ID;
            if (hasTaper3) comps[6] = (powder + 2 * talisman + potion + herb + scarab) * (seed / (talisman + scarab)) % 0xC + LOWEST_TAPER_ID;
            return comps.ToArray();
        }

        static uint[] RandomizeVersion2(SpellBase spell, string accountName)
        {
            var comps = new List<uint>(spell.Formula);

            var key = ComputeHash(accountName);
            var seed = key % 0x13D573;

            var p1 = comps[0];
            var c = comps[4];
            var x = comps[5];
            var a = comps[7];

            comps[3] = (a + 2 * comps[0] + 2 * c * x + comps[0] + comps[2] + comps[1]) % 0xC + LOWEST_TAPER_ID;
            comps[6] = (a + 2 * p1 * comps[2] + 2 * x + p1 * comps[2] + c) * (seed / (comps[1] * a + 2 * c)) % 0xC + LOWEST_TAPER_ID;

            return comps.ToArray();
        }

        static uint[] RandomizeVersion3(SpellBase spell, string accountName)
        {
            var comps = new List<uint>(spell.Formula);

            var key = ComputeHash(accountName);
            var seed1 = key % 0x13D573;
            var seed2 = key % 0x4AEFD;
            var seed3 = key % 0x96A7F;
            var seed4 = key % 0x100A03;
            var seed5 = key % 0xEB2EF;
            var seed6 = key % 0x121E7D;

            var compHash0 = (seed1 + comps[0]) % 0xC;
            var compHash1 = (seed2 + comps[1]) % 0xC;
            var compHash2 = (seed3 + comps[2]) % 0xC;
            var compHash4 = (seed4 + comps[4]) % 0xC;
            var compHash5 = (seed5 + comps[5]) % 0xC;

            // Some spells don't have the full number of comps. 2697 ("Aerfalle's Touch"), is one example.
            var compHash7 = comps.Count < 8 ? (seed6 + 0) % 0xC : (seed6 + comps[7]) % 0xC;
            comps[3] = (compHash0 + compHash1 + compHash2 + compHash4 + compHash5 + compHash2 * compHash5 + compHash0 * compHash1 + compHash7 * (compHash4 + 1)) % 0xC + LOWEST_TAPER_ID;
            comps[6] = (compHash0 + compHash1 + compHash2 + compHash4 + key % 0x65039 % 0xC + compHash7 * (compHash4 * (compHash0 * compHash1 * compHash2 * compHash5 + 7) + 1) + compHash5 + 4 * compHash0 * compHash1 + compHash0 * compHash1 + 11 * compHash2 * compHash5) % 0xC + LOWEST_TAPER_ID;

            return comps.ToArray();
        }
    }
    #endregion

    #region StringTable
    //: FileTypes.StringTable
    [PakFileType(PakFileType.StringTable)]
    public class StringTable : FileType, IHaveMetaInfo
    {
        public static uint CharacterTitle_FileID = 0x2300000E;

        public readonly uint Language; // This should always be 1 for English
        public readonly byte Unknown;
        public readonly StringTableData[] StringTableData;

        public StringTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Language = r.ReadUInt32();
            Unknown = r.ReadByte();
            StringTableData = r.ReadC32FArray(x => new StringTableData(x));
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(StringTable)}: {Id:X8}", items: [
                new($"Language: {Language}"),
                new($"Unknown: {Unknown}"),
                new("String Tables", items: StringTableData.Select(x => {
                    var items = (x as IHaveMetaInfo).GetInfoNodes();
                    var name = items[0].Name;
                    items.RemoveAt(0);
                    return new MetaInfo(name, items: items);
                })),
            ])
        ];
    }
    #endregion

    #region Surface
    /// <summary>
    /// These are client_portal.dat files starting with 0x08.
    /// As the name implies this contains surface info for an object. Either texture reference or color and whatever effects applied to it.
    /// </summary>
    //: FileTypes.Surface
    [PakFileType(PakFileType.Surface)]
    public class Surface : FileType, IHaveMetaInfo
    {
        public readonly SurfaceType Type;
        public readonly uint OrigTextureId;
        public readonly uint OrigPaletteId;
        public readonly uint ColorValue;
        public readonly float Translucency;
        public readonly float Luminosity;
        public readonly float Diffuse;

        public Surface(BinaryReader r)
        {
            Type = (SurfaceType)r.ReadUInt32();
            if (Type.HasFlag(SurfaceType.Base1Image) || Type.HasFlag(SurfaceType.Base1ClipMap)) { OrigTextureId = r.ReadUInt32(); OrigPaletteId = r.ReadUInt32(); } // image or clipmap
            else ColorValue = r.ReadUInt32(); // solid color
            Translucency = r.ReadSingle();
            Luminosity = r.ReadSingle();
            Diffuse = r.ReadSingle();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var hasSurface = Type.HasFlag(SurfaceType.Base1Image) || Type.HasFlag(SurfaceType.Base1ClipMap);
            return [
                new($"{nameof(Surface)}: {Id:X8}", items: [
                    new($"Type: {Type}"),
                    hasSurface ? new($"Surface Texture: {OrigTextureId:X8}", clickable: true) : null,
                    hasSurface && OrigPaletteId != 0 ? new($"Palette ID: {OrigPaletteId:X8}", clickable: true) : null,
                    !hasSurface ? new($"Color: {ColorX.ToRGBA(ColorValue)}") : null,
                    /*Translucency != 0f ?*/ new($"Translucency: {Translucency}") /*: null*/,
                    /*Luminosity != 0f ?*/ new($"Luminosity: {Luminosity}") /*: null*/,
                    /*Diffuse != 1f ?*/ new($"Diffuse: {Diffuse}") /*: null*/,
                ])
            ];
        }
    }
    #endregion

    #region SurfaceTexture
    //: FileTypes.SurfaceTexture
    [PakFileType(PakFileType.SurfaceTexture)]
    public class SurfaceTexture : FileType, IHaveMetaInfo
    {
        public readonly int Unknown;
        public readonly byte UnknownByte;
        public readonly uint[] Textures; // These values correspond to a Surface (0x06) entry

        public SurfaceTexture(BinaryReader r)
        {
            Id = r.ReadUInt32();
            Unknown = r.ReadInt32();
            UnknownByte = r.ReadByte();
            Textures = r.ReadL32PArray<uint>("I");
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(SurfaceTexture)}: {Id:X8}", items: [
                new($"Unknown: {Unknown}"),
                new($"UnknownByte: {UnknownByte}"),
                new("Textures", items: Textures.Select(x => new MetaInfo($"{x:X8}", clickable: true))),
            ])
        ];
    }
    #endregion

    #region TabooTable
    //: FileTypes.TabooTable
    [PakFileType(PakFileType.TabooTable)]
    public class TabooTable : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E00001E;

        /// <summary>
        /// The key is a 32 bit flag variable, and only one flag is set per entry.<para />
        /// In the current dats, this isn't used for anything. All tables share the same values for any given flag.<para />
        /// It's possible the intended use for the flags was to separate words based on the type of offense, ie: racist, sexual, harassment, etc...
        /// </summary>
        public readonly IDictionary<uint, TabooTableEntry> TabooTableEntries;

        public TabooTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            // I don't actually know the structure of TabooTableEntries. It could be a Dictionary as I have it defined, or it could be a List where the key is just a variable in TabooTableEntry
            // I was unable to find the unpack code in the client. If someone can point me to it, I can make sure we match what the client is doing. - Mag
            r.ReadByte();
            var length = r.ReadByte();
            TabooTableEntries = r.ReadPMany<uint, TabooTableEntry>("I", x => new TabooTableEntry(x), length);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo>
            {
                //new($"{nameof(TabooTable)}: {Id:X8}", items: TabooTableEntries.OrderBy(i => i.Key).Select(
                //    x => new MetaInfo($"{x.Key:X8}", items: x.Value.BannedPatterns.Select(y => new MetaInfo($"{y}")))
                //))
            };
            foreach (var x in TabooTableEntries.OrderBy(i => i.Key))
                nodes.Add(new(null, new MetaContent { Type = "Text", Name = $"F:{x.Key:X8}", Value = string.Join(", ", x.Value.BannedPatterns) }));
            return nodes;
        }

        /// <summary>
        /// This will search all the first entry to see if the input passes or fails.<para />
        /// Only the first entry is searched (for now) because they're all the same.
        /// </summary>
        public bool ContainsBadWord(string input)
        {
            foreach (var kvp in TabooTableEntries)
            {
                if (kvp.Value.ContainsBadWord(input)) return true;
                // If in the future, the dat is changed so that each entry has unique patterns, remove this break.
                break;
            }
            return false;
        }
    }
    #endregion

    #region Texture
    [PakFileType(PakFileType.Texture)]
    public unsafe class Texture : FileType, IHaveMetaInfo, ITexture
    {
        public readonly int Unknown;
        public readonly SurfacePixelFormat PixFormat;
        public readonly int Length;
        public readonly byte[] SourceData;
        public readonly uint[] Palette;

        public Texture(BinaryReader r, FamilyGame game)
        {
            Id = r.ReadUInt32();
            Unknown = r.ReadInt32();
            Width = r.ReadInt32();
            Height = r.ReadInt32();
            PixFormat = (SurfacePixelFormat)r.ReadUInt32();
            Length = r.ReadInt32();
            SourceData = r.ReadBytes(Length);
            var hasPalette = PixFormat == PFID_INDEX16 || PixFormat == PFID_P8;
            if (hasPalette) game.Ensure();
            Palette = hasPalette ? DatabaseManager.Portal.GetFile<Palette>(r.ReadUInt32()).Colors : null;
            if (PixFormat == PFID_CUSTOM_RAW_JPEG)
            {
                using var image = new Bitmap(new MemoryStream(SourceData));
                Width = image.Width;
                Height = image.Height;
            }
            Format = PixFormat switch
            {
                PFID_DXT1 => (PFID_DXT1, TextureGLFormat.CompressedRgbaS3tcDxt1Ext, TextureGLFormat.CompressedRgbaS3tcDxt1Ext, TextureUnityFormat.DXT1, TextureUnityFormat.DXT1),
                PFID_DXT3 => (PFID_DXT3, TextureGLFormat.CompressedRgbaS3tcDxt3Ext, TextureGLFormat.CompressedRgbaS3tcDxt3Ext, TextureUnityFormat.Unknown, TextureUnityFormat.Unknown),
                PFID_DXT5 => (PFID_DXT5, TextureGLFormat.CompressedRgbaS3tcDxt5Ext, TextureGLFormat.CompressedRgbaS3tcDxt5Ext, TextureUnityFormat.DXT5, TextureUnityFormat.DXT5),
                var x when x == PFID_CUSTOM_RAW_JPEG ||
                x == PFID_R8G8B8 ||
                x == PFID_CUSTOM_LSCAPE_R8G8B8 ||
                x == PFID_A8 ||
                x == PFID_CUSTOM_LSCAPE_ALPHA ||
                x == PFID_R5G6B5 => (x, (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), TextureUnityFormat.RGB24, TextureUnityFormat.RGB24),
                var x when x == PFID_INDEX16 ||
                x == PFID_P8 ||
                x == PFID_A8R8G8B8 ||
                x == PFID_A4R4G4B4 => (x, (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), TextureUnityFormat.RGBA32, TextureUnityFormat.RGBA32),
                _ => throw new ArgumentOutOfRangeException(nameof(Format), $"{Format}"),
            };
        }

        (SurfacePixelFormat type, object gl, object vulken, object unity, object unreal) Format;

        public int Width { get; }
        public int Height { get; }
        public int Depth => 0;
        public int MipMaps => 1;
        public TextureFlags Flags => 0;

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
        {
            byte[] Expand()
            {
                // https://www.hanselman.com/blog/how-do-you-use-systemdrawing-in-net-core
                // https://stackoverflow.com/questions/1563038/fast-work-with-bitmaps-in-c-sharp
                switch (PixFormat)
                {
                    case PFID_CUSTOM_RAW_JPEG:
                        {
                            var d = new byte[Width * Height * 3];
                            using var image = new Bitmap(new MemoryStream(SourceData));
                            var data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                            var s = (byte*)data.Scan0.ToPointer();
                            for (var i = 0; i < d.Length; i += 3) { d[i + 0] = s[i + 0]; d[i + 1] = s[i + 1]; d[i + 2] = s[i + 2]; }
                            image.UnlockBits(data);
                            return d;
                        }
                    case PFID_DXT1:
                    case PFID_DXT3:
                    case PFID_DXT5: return SourceData;
                    case PFID_R8G8B8: // RGB
                    case PFID_CUSTOM_LSCAPE_R8G8B8: return SourceData;
                    //case PFID_CUSTOM_LSCAPE_R8G8B8:
                    //    {
                    //        var d = new byte[Width * Height * 3];
                    //        var s = SourceData;
                    //        for (int i = 0; i < d.Length; i += 3) { d[i + 0] = s[i + 2]; d[i + 1] = s[i + 1]; d[i + 2] = s[i + 0]; }
                    //        return d;
                    //    }
                    case PFID_A8R8G8B8: // ARGB format. Most UI textures fall into this category
                        {
                            var d = new byte[Width * Height * 4];
                            var s = SourceData;
                            for (var i = 0; i < d.Length; i += 4) { d[i + 0] = s[i + 1]; d[i + 1] = s[i + 2]; d[i + 2] = s[i + 3]; d[i + 3] = s[i + 0]; }
                            return d;
                        }
                    case PFID_A8: // Greyscale, also known as Cairo A8.
                    case PFID_CUSTOM_LSCAPE_ALPHA:
                        {
                            var d = new byte[Width * Height * 3];
                            var s = SourceData;
                            for (int i = 0, j = 0; i < d.Length; i += 3, j++) { d[i + 0] = s[j]; d[i + 1] = s[j]; d[i + 2] = s[j]; }
                            return d;
                        }
                    case PFID_R5G6B5: // 16-bit RGB
                        {
                            var d = new byte[Width * Height * 3];
                            fixed (byte* _ = SourceData)
                            {
                                var s = (ushort*)_;
                                for (int i = 0, j = 0; i < d.Length; i += 4, j++)
                                {
                                    var val = s[j];
                                    d[i + 0] = (byte)((val >> 8 & 0xF) / 0xF * 255);
                                    d[i + 1] = (byte)((val >> 4 & 0xF) / 0xF * 255);
                                    d[i + 2] = (byte)((val & 0xF) / 0xF * 255);
                                }
                            }
                            return d;
                        }
                    case PFID_A4R4G4B4:
                        {
                            var d = new byte[Width * Height * 4];
                            fixed (byte* s_ = SourceData)
                            {
                                var s = (ushort*)s_;
                                for (int i = 0, j = 0; i < d.Length; i += 4, j++)
                                {
                                    var val = s[j];
                                    d[i + 0] = (byte)(((val & 0xF800) >> 11) << 3);
                                    d[i + 1] = (byte)(((val & 0x7E0) >> 5) << 2);
                                    d[i + 2] = (byte)((val & 0x1F) << 3);
                                }
                            }
                            return d;
                        }
                    case PFID_INDEX16: // 16-bit indexed colors. Index references position in a palette;
                        {
                            var p = Palette;
                            var d = new byte[Width * Height * 4];
                            fixed (byte* s_ = SourceData)
                            fixed (byte* d_ = d)
                            {
                                var s = (ushort*)s_;
                                var d2 = (uint*)d_;
                                for (var i = 0; i < d.Length >> 2; i++) d2[i] = p[s[i]];
                            }
                            return d;
                        }
                    case PFID_P8: // Indexed
                        {
                            var p = Palette;
                            var d = new byte[Width * Height * 4];
                            var s = SourceData;
                            fixed (byte* d_ = d)
                            {
                                var d2 = (uint*)d_;
                                for (var i = 0; i < d.Length >> 2; i++) d2[i] = p[s[i]];
                            }
                            return d;
                        }
                    default: Console.WriteLine($"Unhandled SurfacePixelFormat ({Format}) in RenderSurface {Id:X8}"); return null;
                }
            }

            return (Expand(), (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.StereoKit => throw new NotImplementedException("StereoKit"),
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, new[] { Range.All });
        }
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            //new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "PICTURE" }),
            new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new($"{nameof(Texture)}: {Id:X8}", items: [
                new($"Unknown: {Unknown}"),
                new($"Format: {Format.type}"),
                new($"Width: {Width}"),
                new($"Height: {Height}"),
                new($"Type: {Format}"),
                new($"Size: {Length} bytes"),
            ])
        ];
    }
    #endregion

    #region Wave
    /// <summary>
    /// These are client_portal.dat files starting with 0x0A. All are stored in .WAV data format, though the header slightly different than a .WAV file header.
    /// I'm not sure of an instance where the server would ever need this data, but it's fun nonetheless and included for completion sake.
    /// </summary>
    //: FileTypes.Sound
    [PakFileType(PakFileType.Wave)]
    public class Wave : FileType, IHaveMetaInfo
    {
        public byte[] Header { get; private set; }
        public byte[] Data { get; private set; }

        public Wave(BinaryReader r)
        {
            Id = r.ReadUInt32();
            var headerSize = r.ReadInt32();
            var dataSize = r.ReadInt32();
            Header = r.ReadBytes(headerSize);
            Data = r.ReadBytes(dataSize);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var type = Header[0] == 0x55 ? ".mp3" : ".wav";
            return [
                new(null, new MetaContent { Type = "AudioPlayer", Name = "Sound", Value = null, Tag = type }),
                new($"{nameof(Wave)}: {Id:X8}", items: [
                    new($"Type: {type}"),
                    new($"Header Size: {Header.Length}"),
                    new($"Data Size: {Data.Length}"),
                ])
            ];
        }

        /// <summary>
        /// Exports Wave to a playable .wav file
        /// </summary>
        public void ExportWave(string directory)
        {
            var ext = Header[0] == 0x55 ? ".mp3" : ".wav";
            var filename = Path.Combine(directory, Id.ToString("X8") + ext);
            // Good summary of the header for a WAV file and what all this means
            // http://www.topherlee.com/software/pcm-tut-wavformat.html
            var f = new FileStream(filename, FileMode.Create);
            WriteData(f);
            f.Close();
        }

        public void WriteData(Stream stream)
        {
            var w = new BinaryWriter(stream);
            w.Write(Encoding.ASCII.GetBytes("RIFF"));
            var filesize = (uint)(Data.Length + 36); // 36 is added for all the extra we're adding for the WAV header format
            w.Write(filesize);
            w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt"));
            w.Write((byte)0x20); // Null ending to the fmt
            w.Write((int)0x10); // 16 ... length of all the above
            // AC audio headers start at Format Type, and are usually 18 bytes, with some exceptions notably objectID A000393 which is 30 bytes
            // WAV headers are always 16 bytes from Format Type to end of header, so this extra data is truncated here.
            w.Write(Header.Take(16).ToArray());
            w.Write(Encoding.ASCII.GetBytes("data"));
            w.Write((uint)Data.Length);
            w.Write(Data);
        }
    }
    #endregion

    #region XpTable
    /// <summary>
    /// Reads and stores the XP Tables from the client_portal.dat (file 0x0E000018).
    /// </summary>
    //: FileTypes.XpTable
    [PakFileType(PakFileType.XpTable)]
    public class XpTable : FileType, IHaveMetaInfo
    {
        public const uint FILE_ID = 0x0E000018;

        public readonly uint[] AttributeXpList;
        public readonly uint[] VitalXpList;
        public readonly uint[] TrainedSkillXpList;
        public readonly uint[] SpecializedSkillXpList;
        /// <summary>
        /// The XP needed to reach each level
        /// </summary>
        public readonly ulong[] CharacterLevelXPList;
        /// <summary>
        /// Number of credits gifted at each level
        /// </summary>
        public readonly uint[] CharacterLevelSkillCreditList;

        public XpTable(BinaryReader r)
        {
            Id = r.ReadUInt32();
            // The counts for each "Table" are at the top of the file.
            var attributeCount = r.ReadInt32() + 1;
            var vitalCount = r.ReadInt32() + 1;
            var trainedSkillCount = r.ReadInt32() + 1;
            var specializedSkillCount = r.ReadInt32() + 1;
            var levelCount = r.ReadUInt32() + 1;
            AttributeXpList = r.ReadPArray<uint>("I", attributeCount);
            VitalXpList = r.ReadPArray<uint>("I", vitalCount);
            TrainedSkillXpList = r.ReadPArray<uint>("I", trainedSkillCount);
            SpecializedSkillXpList = r.ReadPArray<uint>("I", specializedSkillCount);
            CharacterLevelXPList = r.ReadPArray<ulong>("Q", (int)levelCount);
            CharacterLevelSkillCreditList = r.ReadPArray<uint>("I", (int)levelCount);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{nameof(XpTable)}: {Id:X8}", items: [
                new("AttributeXpList", items: AttributeXpList.Select((x, i) => new MetaInfo($"{i}: {x:N0}"))),
                new("VitalXpList", items: VitalXpList.Select((x, i) => new MetaInfo($"{i}: {x:N0}"))),
                new("TrainedSkillXpList", items: TrainedSkillXpList.Select((x, i) => new MetaInfo($"{i}: {x:N0}"))),
                new("SpecializedSkillXpList", items: SpecializedSkillXpList.Select((x, i) => new MetaInfo($"{i}: {x:N0}"))),
                new("CharacterLevelXpList", items: CharacterLevelXPList.Select((x, i) => new MetaInfo($"{i}: {x:N0}"))),
                new("CharacterLevelSkillCreditList", items: CharacterLevelSkillCreditList.Select((x, i) => new MetaInfo($"{i}: {x:N0}"))),
            ])
        ];
    }
    #endregion
}
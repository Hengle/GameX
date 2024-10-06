using GameX.Meta;
using GameX.WB.Formats.AC.AnimationHooks;
using GameX.WB.Formats.AC.FileTypes;
using GameX.WB.Formats.AC.Props;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace GameX.WB.Formats.AC.Entity
{
    #region AmbientSoundDesc
    //: Entity.AmbientSoundDesc
    public class AmbientSoundDesc(BinaryReader r) : IHaveMetaInfo
    {
        public readonly Sound SType = (Sound)r.ReadUInt32();
        public readonly float Volume = r.ReadSingle();
        public readonly float BaseChance = r.ReadSingle();
        public readonly float MinRate = r.ReadSingle();
        public readonly float MaxRate = r.ReadSingle();
        public bool IsContinuous => BaseChance == 0;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"SoundType: {SType}"),
            new($"Volume: {Volume}"),
            new($"BaseChance: {BaseChance}"),
            new($"MinRate: {MinRate}"),
            new($"MaxRate: {MaxRate}"),
        ];
    }
    #endregion

    #region AmbientSTBDesc
    //: Entity.AmbientSoundTableDesc
    public class AmbientSTBDesc(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint STBId = r.ReadUInt32();
        public readonly AmbientSoundDesc[] AmbientSounds = r.ReadL32FArray(x => new AmbientSoundDesc(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Ambient Sound Table ID: {STBId:X8}", clickable: true),
            new($"Ambient Sounds", items: AmbientSounds.Select((x, i)
                => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
        ];
    }
    #endregion

    #region AnimationFrame
    //: Entity.AnimationFrame
    public class AnimationFrame(BinaryReader r, uint numParts) : IHaveMetaInfo
    {
        public readonly Frame[] Frames = r.ReadFArray(x => new Frame(r), (int)numParts);
        public readonly AnimationHook[] Hooks = r.ReadL32FArray(AnimationHook.Factory);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Frames", items: Frames.Select(x => new MetaInfo($"{x}"))),
            MetaInfo.WrapWithGroup(Hooks, "Hooks", Hooks.Select(x => new MetaInfo($"HookType: {x.HookType}", items: (AnimationHook.Factory(x) as IHaveMetaInfo).GetInfoNodes(tag: tag)))),
        ];
    }
    #endregion

    #region AnimationHook
    //: Entity.AnimationHook
    public class AnimationHook : IHaveMetaInfo
    {
        public override string ToString() => $"HookType: {HookType}, Dir: {Direction}";
        public static readonly AnimationHook AnimDoneHook = new();
        protected readonly AnimationHook Base;
        public readonly AnimationHookType HookType;
        public readonly AnimationHookDir Direction;

        AnimationHook() => HookType = AnimationHookType.AnimationDone;
        protected AnimationHook(AnimationHook @base) => Base = @base;
        /// <summary>
        /// WARNING: If you're reading a hook from the dat, you should use AnimationHook.ReadHook(reader).
        /// If you read a hook from the dat using this function, it is likely you will not read all the data correctly.
        /// </summary>
        public AnimationHook(BinaryReader r)
        {
            HookType = (AnimationHookType)r.ReadUInt32();
            Direction = (AnimationHookDir)r.ReadInt32();
        }

        public static AnimationHook Factory(AnimationHook animationHook)
            => animationHook.HookType switch
            {
                AnimationHookType.AnimationDone => new AnimationHook(animationHook),
                AnimationHookType.Attack => new AttackHook(animationHook),
                AnimationHookType.CallPES => new CallPESHook(animationHook),
                AnimationHookType.CreateBlockingParticle => new AnimationHook(animationHook),
                AnimationHookType.CreateParticle => new CreateParticleHook(animationHook),
                AnimationHookType.DefaultScript => new AnimationHook(animationHook),
                AnimationHookType.DefaultScriptPart => new DefaultScriptPartHook(animationHook),
                AnimationHookType.DestroyParticle => new DestroyParticleHook(animationHook),
                AnimationHookType.Diffuse => new DiffuseHook(animationHook),
                AnimationHookType.DiffusePart => new DiffusePartHook(animationHook),
                AnimationHookType.Ethereal => new EtherealHook(animationHook),
                AnimationHookType.ForceAnimationHook32Bit => new AnimationHook(animationHook),
                AnimationHookType.Luminous => new LuminousHook(animationHook),
                AnimationHookType.LuminousPart => new LuminousPartHook(animationHook),
                AnimationHookType.NoDraw => new NoDrawHook(animationHook),
                AnimationHookType.NoOp => new AnimationHook(animationHook),
                AnimationHookType.ReplaceObject => new ReplaceObjectHook(animationHook),
                AnimationHookType.Scale => new ScaleHook(animationHook),
                AnimationHookType.SetLight => new SetLightHook(animationHook),
                AnimationHookType.SetOmega => new SetOmegaHook(animationHook),
                AnimationHookType.Sound => new SoundHook(animationHook),
                AnimationHookType.SoundTable => new SoundTableHook(animationHook),
                AnimationHookType.SoundTweaked => new SoundTweakedHook(animationHook),
                AnimationHookType.StopParticle => new StopParticleHook(animationHook),
                AnimationHookType.TextureVelocity => new TextureVelocityHook(animationHook),
                AnimationHookType.TextureVelocityPart => new TextureVelocityPartHook(animationHook),
                AnimationHookType.Transparent => new TransparentHook(animationHook),
                AnimationHookType.TransparentPart => new TransparentPartHook(animationHook),
                _ => new AnimationHook(animationHook),
            };

        public static AnimationHook Factory(BinaryReader r)
        {
            // We peek forward to get the hook type, then revert our position.
            var hookType = (AnimationHookType)r.ReadUInt32();
            r.Skip(-4);
            return hookType switch
            {
                AnimationHookType.Sound => new SoundHook(r),
                AnimationHookType.SoundTable => new SoundTableHook(r),
                AnimationHookType.Attack => new AttackHook(r),
                AnimationHookType.ReplaceObject => new ReplaceObjectHook(r),
                AnimationHookType.Ethereal => new EtherealHook(r),
                AnimationHookType.TransparentPart => new TransparentPartHook(r),
                AnimationHookType.Luminous => new LuminousHook(r),
                AnimationHookType.LuminousPart => new LuminousPartHook(r),
                AnimationHookType.Diffuse => new DiffuseHook(r),
                AnimationHookType.DiffusePart => new DiffusePartHook(r),
                AnimationHookType.Scale => new ScaleHook(r),
                AnimationHookType.CreateParticle => new CreateParticleHook(r),
                AnimationHookType.DestroyParticle => new DestroyParticleHook(r),
                AnimationHookType.StopParticle => new StopParticleHook(r),
                AnimationHookType.NoDraw => new NoDrawHook(r),
                AnimationHookType.DefaultScriptPart => new DefaultScriptPartHook(r),
                AnimationHookType.CallPES => new CallPESHook(r),
                AnimationHookType.Transparent => new TransparentHook(r),
                AnimationHookType.SoundTweaked => new SoundTweakedHook(r),
                AnimationHookType.SetOmega => new SetOmegaHook(r),
                AnimationHookType.TextureVelocity => new TextureVelocityHook(r),
                AnimationHookType.TextureVelocityPart => new TextureVelocityPartHook(r),
                AnimationHookType.SetLight => new SetLightHook(r),
                AnimationHookType.CreateBlockingParticle => new CreateBlockingParticle(r),
                // The following HookTypes have no additional properties:
                AnimationHookType.AnimationDone => new AnimationHook(r),
                AnimationHookType.DefaultScript => new AnimationHook(r),
                _ => throw new FormatException($"Not Implemented Hook type encountered: {hookType}"),
            };
        }

        public virtual List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Dir: {Direction}"),
        ];
    }
    #endregion

    #region AnimationPartChange
    //: Entity+AnimationPartChange
    //: Entity.AnimPartChange
    public class AnimationPartChange : IHaveMetaInfo
    {
        public override string ToString() => $"PartIdx: {PartIndex}, PartID: {PartID:X8}";
        public readonly byte PartIndex;
        public readonly uint PartID;

        public AnimationPartChange(BinaryReader r)
        {
            PartIndex = r.ReadByte();
            PartID = r.ReadAsDataIDOfKnownType(0x01000000);
        }
        public AnimationPartChange(BinaryReader r, ushort partIndex)
        {
            PartIndex = (byte)(partIndex & 255);
            PartID = r.ReadAsDataIDOfKnownType(0x01000000);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Part Idx: {PartIndex}"),
            new($"Part ID: {PartID:X8}", clickable: true),
        ];
    }
    #endregion

    #region AnimData
    //: Entity.AnimData
    public class AnimData : IHaveMetaInfo
    {
        public override string ToString() => $"AnimId: {AnimId:X8}, LowFrame: {LowFrame}, HighFrame: {HighFrame}, FrameRate: {Framerate}";
        public readonly uint AnimId;
        public readonly int LowFrame;
        public readonly int HighFrame;
        /// <summary>
        /// Negative framerates play animation in reverse
        /// </summary>
        public readonly float Framerate;

        //: Entity+AnimData
        public AnimData(uint animationId, int lowFrame, int highFrame, float framerate)
        {
            AnimId = animationId;
            LowFrame = lowFrame;
            HighFrame = highFrame;
            Framerate = framerate;
        }
        public AnimData(BinaryReader r)
        {
            AnimId = r.ReadUInt32();
            LowFrame = r.ReadInt32();
            HighFrame = r.ReadInt32();
            Framerate = r.ReadSingle();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Anim ID: {AnimId:X8}", clickable: true),
            new($"Low frame: {LowFrame}"),
            new($"High frame: {HighFrame}"),
            new($"Framerate: {Framerate}"),
        ];
    }
    #endregion

    #region AttackCone
    //: Entity.AttackCone
    public class AttackCone(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint PartIndex = r.ReadUInt32();
        // these Left and Right are technically Vec2D types
        public readonly float LeftX = r.ReadSingle(); public readonly float LeftY = r.ReadSingle();
        public readonly float RightX = r.ReadSingle(); public readonly float RightY = r.ReadSingle();
        public readonly float Radius = r.ReadSingle(); public readonly float Height = r.ReadSingle();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"PartIndex: {PartIndex}"),
            new($"LeftX: {LeftX}"),
            new($"LeftY: {LeftY}"),
            new($"RightX: {RightX}"),
            new($"RightY: {RightY}"),
            new($"Radius: {Radius}"),
            new($"Height: {Height}"),
        ];
    }
    #endregion

    #region Attribute2ndBase
    public class Attribute2ndBase(BinaryReader r)
    {
        public readonly SkillFormula Formula = new(r);
    }
    #endregion

    #region BspLeaf
    //: Entity.BSPLeaf
    public class BspLeaf : BspNode, IHaveMetaInfo
    {
        public readonly int LeafIndex;
        public readonly int Solid;

        public BspLeaf(BinaryReader r, BSPType treeType) : base()
        {
            Type = Encoding.ASCII.GetString(r.ReadBytes(4)).Reverse();
            LeafIndex = r.ReadInt32();
            if (treeType == BSPType.Physics)
            {
                Solid = r.ReadInt32();
                // Note that if Solid is equal to 0, these values will basically be null. Still read them, but they don't mean anything.
                Sphere = new Sphere(r);
                InPolys = r.ReadL32PArray<ushort>("H");
            }
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo> {
                new($"Type: {Type}"),
                new($"LeafIndex: {LeafIndex}"),
            };
            if ((BSPType)tag == BSPType.Physics)
                nodes.AddRange([
                    new ($"Solid: {Solid}"),
                    new ($"Sphere: {Sphere}"),
                    new ($"InPolys: {string.Join(", ", InPolys)}"),
                ]);
            return nodes;
        }
    }
    #endregion

    #region BspNode
    //: Entity.BSPNode
    public class BspNode : IHaveMetaInfo
    {
        // These constants are actually strings in the dat file
        const uint PORT = 1347375700; // 0x504F5254
        const uint LEAF = 1279607110; // 0x4C454146
        const uint BPnn = 1112567406; // 0x42506E6E
        const uint BPIn = 1112557934; // 0x4250496E
        const uint BpIN = 1114655054; // 0x4270494E
        const uint BpnN = 1114664526; // 0x42706E4E
        const uint BPIN = 1112557902; // 0x4250494E
        const uint BPnN = 1112567374; // 0x42506E4E

        public string Type;
        public Plane SplittingPlane;
        public BspNode PosNode;
        public BspNode NegNode;
        public Sphere Sphere;
        public ushort[] InPolys; // List of PolygonIds

        protected BspNode() { }
        public BspNode(BinaryReader r, BSPType treeType)
        {
            Type = Encoding.ASCII.GetString(r.ReadBytes(4)).Reverse();
            switch (Type)
            {
                // These types will unpack the data completely, in their own classes
                case "PORT":
                case "LEAF": throw new Exception();
            }
            SplittingPlane = new Plane(r);
            switch (Type)
            {
                case "BPnn": case "BPIn": PosNode = Factory(r, treeType); break;
                case "BpIN": case "BpnN": NegNode = Factory(r, treeType); break;
                case "BPIN": case "BPnN": PosNode = Factory(r, treeType); NegNode = Factory(r, treeType); break;
            }
            if (treeType == BSPType.Cell) return;
            Sphere = new Sphere(r);
            if (treeType == BSPType.Physics) return;
            InPolys = r.ReadL32PArray<ushort>("H");
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo> {
                new($"Type: {Type:X8}"),
                new($"Splitting Plane: {SplittingPlane}"),
                PosNode != null ? new("PosNode", items: (PosNode as IHaveMetaInfo).GetInfoNodes(tag: tag)) : null,
                NegNode != null ? new("NegNode", items: (NegNode as IHaveMetaInfo).GetInfoNodes(tag: tag)) : null,
            };
            if ((BSPType)tag == BSPType.Cell) return nodes;
            nodes.Add(new($"Sphere: {Sphere}"));
            if ((BSPType)tag == BSPType.Physics) return nodes;
            nodes.Add(new($"InPolys: {string.Join(", ", InPolys)}"));
            return nodes;
        }

        public static BspNode Factory(BinaryReader r, BSPType treeType)
        {
            // We peek forward to get the type, then revert our position.
            var type = Encoding.ASCII.GetString(r.ReadBytes(4)).Reverse();
            r.BaseStream.Position -= 4;
            return type switch
            {
                "PORT" => new BspPortal(r, treeType),
                "LEAF" => new BspLeaf(r, treeType),
                _ => new BspNode(r, treeType),
            };
        }
    }
    #endregion

    #region BspPortal
    //: Entity.BSPPortal
    public class BspPortal : BspNode, IHaveMetaInfo
    {
        public readonly PortalPoly[] InPortals;

        public BspPortal(BinaryReader r, BSPType treeType) : base()
        {
            Type = Encoding.ASCII.GetString(r.ReadBytes(4)).Reverse();
            SplittingPlane = new Plane(r);
            PosNode = Factory(r, treeType);
            NegNode = Factory(r, treeType);
            if (treeType == BSPType.Drawing)
            {
                Sphere = new Sphere(r);
                var numPolys = r.ReadUInt32();
                var numPortals = r.ReadUInt32();
                InPolys = r.ReadPArray<ushort>("H", (int)numPolys);
                InPortals = r.ReadFArray(x => new PortalPoly(x), (int)numPortals);
            }
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo> {
                new($"Type: {Type:X8}"),
                new($"Splitting Plane: {SplittingPlane}"),
                PosNode != null ? new("PosNode", items: (PosNode as IHaveMetaInfo).GetInfoNodes(tag: tag)) : null,
                NegNode != null ? new("NegNode", items: (NegNode as IHaveMetaInfo).GetInfoNodes(tag: tag)) : null,
            };
            if ((BSPType)tag != BSPType.Drawing) return nodes;
            nodes.Add(new($"Sphere: {Sphere}"));
            nodes.Add(new($"InPolys: {string.Join(", ", InPolys)}"));
            nodes.Add(new("InPortals", items: InPortals.Select(x => new MetaInfo($"{x}"))));
            return nodes;
        }
    }
    #endregion

    #region BspTree
    //: Entity.BSPTree
    public class BspTree(BinaryReader r, BSPType treeType) : IHaveMetaInfo
    {
        public readonly BspNode RootNode = BspNode.Factory(r, treeType);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Root", items: (RootNode as IHaveMetaInfo).GetInfoNodes(tag: tag)),
        ];
    }
    #endregion

    #region BuildInfo
    //: Entity.BuildInfo
    public class BuildInfo(BinaryReader r) : IHaveMetaInfo
    {
        /// <summary>
        /// 0x01 or 0x02 model of the building
        /// </summary>
        public readonly uint ModelId = r.ReadUInt32();
        /// <summary>
        /// specific @loc of the model
        /// </summary>
        public readonly Frame Frame = new Frame(r);
        /// <summary>
        /// unsure what this is used for
        /// </summary>
        public readonly uint NumLeaves = r.ReadUInt32();
        /// <summary>
        /// portals are things like doors, windows, etc.
        /// </summary>
        public CBldPortal[] Portals = r.ReadL32FArray(x => new CBldPortal(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Model ID: {ModelId:X8}", clickable: true),
            new($"Frame: {Frame}"),
            new($"NumLeaves: {NumLeaves}"),
            new($"Portals", items: Portals.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
        ];
    }
    #endregion

    #region CBldPortal
    //: Entity.BldPortal
    public class CBldPortal(BinaryReader r) : IHaveMetaInfo
    {
        public readonly PortalFlags Flags = (PortalFlags)r.ReadUInt16();

        // Not sure what these do. They are both calculated from the flags.
        public bool ExactMatch => Flags.HasFlag(PortalFlags.ExactMatch);
        public bool PortalSide => Flags.HasFlag(PortalFlags.PortalSide);
        // Basically the cells that connect both sides of the portal
        public readonly ushort OtherCellId = r.ReadUInt16();
        public readonly ushort OtherPortalId = r.ReadUInt16();
        /// <summary>
        /// List of cells used in this structure. (Or possibly just those visible through it.)
        /// </summary>
        public readonly ushort[] StabList = (r.ReadL16PArray<ushort>("H"), r.Align()).Item1;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            Flags != 0 ? new($"Flags: {Flags}") : null,
            OtherCellId != 0 ? new($"OtherCell ID: {OtherCellId:X}") : null,
            OtherPortalId != 0 ? new($"OtherPortal ID: {OtherPortalId:X}") : null,
        ];
    }
    #endregion

    #region CellPortal
    //: Entity.CellPortal
    public class CellPortal(BinaryReader r) : IHaveMetaInfo
    {
        public readonly PortalFlags Flags = (PortalFlags)r.ReadUInt16();
        public readonly ushort PolygonId = r.ReadUInt16();
        public readonly ushort OtherCellId = r.ReadUInt16();
        public readonly ushort OtherPortalId = r.ReadUInt16();
        public bool ExactMatch => (Flags & PortalFlags.ExactMatch) != 0;
        public bool PortalSide => (Flags & PortalFlags.PortalSide) == 0;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            Flags != 0 ? new($"Flags: {Flags}") : null,
            new($"Polygon ID: {PolygonId}"),
            OtherCellId != 0 ? new($"OtherCell ID: {OtherCellId:X}") : null,
            OtherPortalId != 0 ? new($"OtherPortal ID: {OtherPortalId:X}") : null,
        ];
    }
    #endregion

    #region CellStruct
    //: Entity.CellStruct
    public class CellStruct : IHaveMetaInfo
    {
        public readonly CVertexArray VertexArray;
        public readonly IDictionary<ushort, Polygon> Polygons;
        public readonly ushort[] Portals;
        public readonly BspTree CellBSP;
        public readonly IDictionary<ushort, Polygon> PhysicsPolygons;
        public readonly BspTree PhysicsBSP;
        public readonly BspTree DrawingBSP;

        public CellStruct(BinaryReader r)
        {
            var numPolygons = r.ReadUInt32();
            var numPhysicsPolygons = r.ReadUInt32();
            var numPortals = r.ReadUInt32();
            VertexArray = new CVertexArray(r);
            Polygons = r.ReadPMany<ushort, Polygon>("H", x => new Polygon(x), (int)numPolygons);
            Portals = r.ReadPArray<ushort>("H", (int)numPortals); r.Align();
            CellBSP = new BspTree(r, BSPType.Cell);
            PhysicsPolygons = r.ReadPMany<ushort, Polygon>("H", x => new Polygon(x), (int)numPhysicsPolygons);
            PhysicsBSP = new BspTree(r, BSPType.Physics);
            var hasDrawingBSP = r.ReadUInt32();
            if (hasDrawingBSP != 0) DrawingBSP = new BspTree(r, BSPType.Drawing);
            r.Align();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"VertexArray", items: (VertexArray as IHaveMetaInfo).GetInfoNodes()),
            new($"Polygons", items: Polygons.Select(x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes()))),
            new($"Portals", items: Portals.Select(x => new MetaInfo($"{x:X8}"))),
            new($"CellBSP", items: (CellBSP as IHaveMetaInfo).GetInfoNodes(tag: BSPType.Cell).First().Items),
            new($"PhysicsPolygons", items: PhysicsPolygons.Select(x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes()))),
            new($"PhysicsBSP", items: (PhysicsBSP as IHaveMetaInfo).GetInfoNodes(tag: BSPType.Physics).First().Items),
            DrawingBSP != null ? new($"DrawingBSP", items: (DrawingBSP as IHaveMetaInfo).GetInfoNodes(tag: BSPType.Drawing).First().Items) : null,
        ];
    }
    #endregion

    #region ChatEmoteData
    //: Entity.ChatEmoteData
    public class ChatEmoteData(BinaryReader r) : IHaveMetaInfo
    {
        public readonly string MyEmote = r.ReadL16Encoding(Encoding.Default); // What the emote string is to the character doing the emote
        public readonly string OtherEmote = (r.Align().ReadL16Encoding(Encoding.Default), r.Align()).Item1; // What the emote string is to other characters

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"MyEmote: {MyEmote}"),
            new($"OtherEmote: {OtherEmote}"),
        ];
    }
    #endregion

    #region CloObjectEffect
    //: Entity.ClothingObjectEffect
    public class CloObjectEffect(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint Index = r.ReadUInt32();
        public readonly uint ModelId = r.ReadUInt32();
        public readonly CloTextureEffect[] CloTextureEffects = r.ReadL32FArray(x => new CloTextureEffect(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Index: {Index}"),
            new($"Model ID: {ModelId:X8}", clickable: true),
            new($"Texture Effects", items: CloTextureEffects.Select(x=> new MetaInfo($"{x}", clickable: true))),
        ];
    }
    #endregion

    #region CloSubPalEffect
    //: Entity.ClothingSubPaletteEffect
    public class CloSubPalEffect(BinaryReader r) : IHaveMetaInfo
    {
        /// <summary>
        /// Icon portal.dat 0x06000000
        /// </summary>
        public readonly uint Icon = r.ReadUInt32();
        public readonly CloSubPalette[] CloSubPalettes = r.ReadL32FArray(x => new CloSubPalette(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Icon: {Icon:X8}", clickable: true),
            new("SubPalettes", items: CloSubPalettes.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[1].Name.Replace("Palette Set: ", "");
                items.RemoveAt(1);
                return new MetaInfo(name, items: items, clickable: true);
            })),
        ];
    }
    #endregion

    #region CloSubPalette
    //: Entity.ClothingSubPalette
    public class CloSubPalette(BinaryReader r) : IHaveMetaInfo
    {
        /// <summary>
        /// Contains a list of valid offsets & color values
        /// </summary>
        public readonly CloSubPaletteRange[] Ranges = r.ReadL32FArray(x => new CloSubPaletteRange(x));
        /// <summary>
        /// Icon portal.dat 0x0F000000
        /// </summary>
        public readonly uint PaletteSet = r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            Ranges.Length == 1
                ? new($"Range: {Ranges[0]}")
                : new($"SubPalette Ranges", items: Ranges.Select(x => new MetaInfo($"{x}"))),
            new($"Palette Set: {PaletteSet:X8}", clickable: true),
        ];
    }
    #endregion

    #region CloSubPaletteRange
    //: Entity.ClothingSubPaletteRange
    public class CloSubPaletteRange(BinaryReader r) : IHaveMetaInfo
    {
        public override string ToString() => $"Offset: {Offset}, NumColors: {NumColors}";
        public readonly uint Offset = r.ReadUInt32();
        public readonly uint NumColors = r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Offset: {Offset}"),
            new($"NumColors: {NumColors}"),
        ];
    }
    #endregion

    #region CloTextureEffect
    //: Entity.ClothingTextureEffect
    public class CloTextureEffect(BinaryReader r)
    {
        public override string ToString() => $"OldTex: {OldTexture:X8}, NewTex: {NewTexture:X8}";
        /// <summary>
        /// Texture portal.dat 0x05000000
        /// </summary>
        public readonly uint OldTexture = r.ReadUInt32();
        /// <summary>
        /// Texture portal.dat 0x05000000
        /// </summary>
        public readonly uint NewTexture = r.ReadUInt32();
    }
    #endregion

    #region ClothingBaseEffect
    //: Entity.ClothingBaseEffect
    public class ClothingBaseEffect(BinaryReader r) : IHaveMetaInfo
    {
        public readonly CloObjectEffect[] CloObjectEffects = r.ReadL32FArray(x => new CloObjectEffect(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new("Object Effects", items: CloObjectEffects.OrderBy(i => i.Index).Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[0].Name;
                items.RemoveAt(0);
                return new MetaInfo(name, items: items);
            })),
        ];
    }
    #endregion

    #region CombatManeuver
    //: Entity.CombatManeuver
    public class CombatManeuver(BinaryReader r) : IHaveMetaInfo
    {
        public readonly MotionStance Style = (MotionStance)r.ReadUInt32();
        public readonly AttackHeight AttackHeight = (AttackHeight)r.ReadUInt32();
        public readonly AttackType AttackType = (AttackType)r.ReadUInt32();
        public readonly uint MinSkillLevel = r.ReadUInt32();
        public readonly MotionCommand Motion = (MotionCommand)r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Stance: {Style}"),
            new($"Attack Height: {AttackHeight}"),
            new($"Attack Type: {AttackType}"),
            MinSkillLevel != 0 ? new($"Min Skill: {MinSkillLevel}") : null,
            new($"Motion: {Motion}"),
        ];
    }
    #endregion

    #region Contract
    //: Entity.Contract
    public class Contract(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint Version = r.ReadUInt32();
        public readonly uint ContractId = r.ReadUInt32();
        public readonly string ContractName = r.ReadL16Encoding(Encoding.Default);

        public readonly string Description = r.Align().ReadL16Encoding(Encoding.Default);
        public readonly string DescriptionProgress = r.Align().ReadL16Encoding(Encoding.Default);

        public readonly string NameNPCStart = r.Align().ReadL16Encoding(Encoding.Default);
        public readonly string NameNPCEnd = r.Align().ReadL16Encoding(Encoding.Default);

        public readonly string QuestflagStamped = r.Align().ReadL16Encoding(Encoding.Default);
        public readonly string QuestflagStarted = r.Align().ReadL16Encoding(Encoding.Default);
        public readonly string QuestflagFinished = r.Align().ReadL16Encoding(Encoding.Default);
        public readonly string QuestflagProgress = r.Align().ReadL16Encoding(Encoding.Default);
        public readonly string QuestflagTimer = r.Align().ReadL16Encoding(Encoding.Default);
        public readonly string QuestflagRepeatTime = r.Align().ReadL16Encoding(Encoding.Default);

        public readonly Position LocationNPCStart = new(r.Align());
        public readonly Position LocationNPCEnd = new(r);
        public readonly Position LocationQuestArea = new(r);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"ContractId: {ContractId}"),
            new($"ContractName: {ContractName}"),
            new($"Version: {Version}"),
            new($"Description: {Description}"),
            new($"DescriptionProgress: {DescriptionProgress}"),
            new($"NameNPCStart: {NameNPCStart}"),
            new($"NameNPCEnd: {NameNPCEnd}"),
            new($"QuestflagStamped: {QuestflagStamped}"),
            new($"QuestflagStarted: {QuestflagStarted}"),
            new($"QuestflagFinished: {QuestflagFinished}"),
            new($"QuestflagProgress: {QuestflagProgress}"),
            new($"QuestflagTimer: {QuestflagTimer}"),
            new($"QuestflagRepeatTime: {QuestflagRepeatTime}"),
            new("LocationNPCStart", items: (LocationNPCStart as IHaveMetaInfo).GetInfoNodes(tag: tag)),
            new("LocationNPCEnd", items: (LocationNPCEnd as IHaveMetaInfo).GetInfoNodes(tag: tag)),
            new("LocationQuestArea", items: (LocationQuestArea as IHaveMetaInfo).GetInfoNodes(tag: tag)),
        ];
    }
    #endregion

    #region CVertexArray
    /// <summary>
    /// A list of indexed vertices, and their associated type
    /// </summary>
    //: Entity.VertexArray
    public class CVertexArray : IHaveMetaInfo
    {
        public readonly int VertexType;
        public readonly IDictionary<ushort, SWVertex> Vertices;

        public CVertexArray(BinaryReader r)
        {
            VertexType = r.ReadInt32();
            var numVertices = r.ReadUInt32();
            if (VertexType == 1) Vertices = r.ReadPMany<ushort, SWVertex>("H", x => new SWVertex(x), (int)numVertices);
            else throw new FormatException("VertexType should be 1");
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"VertexType: {VertexType}"),
            new($"Vertices", items: Vertices.Select(x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes(resource, file)))),
        ];
    }
    #endregion

    #region CylSphere
    //: Entity.CylSphere
    public class CylSphere(BinaryReader r)
    {
        public override string ToString() => $"Origin: {Origin}, Radius: {Radius}, Height: {Height}";
        public readonly Vector3 Origin = r.ReadVector3();
        public readonly float Radius = r.ReadSingle();
        public readonly float Height = r.ReadSingle();
    }
    #endregion

    #region DayGroup
    //: Entity.DayGroup
    public class DayGroup(BinaryReader r) : IHaveMetaInfo
    {
        public readonly float ChanceOfOccur = r.ReadSingle();
        public readonly string DayName = r.ReadL16Encoding(Encoding.Default);
        public readonly SkyObject[] SkyObjects = r.Align().ReadL32FArray(x => new SkyObject(x));
        public readonly SkyTimeOfDay[] SkyTime = r.ReadL32FArray(x => new SkyTimeOfDay(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"ChanceOfOccur: {ChanceOfOccur}"),
            new($"Weather: {DayName}"),
            new("SkyObjects", items: SkyObjects.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            new("SkyTimesOfDay", items: SkyTime.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
        ];
    }
    #endregion

    #region EyeStripCG
    //: Entity.EyeStripCG
    public class EyeStripCG(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint IconImage = r.ReadUInt32();
        public readonly uint IconImageBald = r.ReadUInt32();
        public readonly ObjDesc ObjDesc = new(r);
        public readonly ObjDesc ObjDescBald = new(r);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            IconImage != 0 ? new($"Icon: {IconImage:X8}", clickable: true) : null,
            IconImageBald != 0 ? new($"Bald Icon: {IconImageBald:X8}", clickable: true) : null,
            new("ObjDesc", items: (ObjDesc as IHaveMetaInfo).GetInfoNodes()),
            new("ObjDescBald", items: (ObjDescBald as IHaveMetaInfo).GetInfoNodes()),
        ];
    }
    #endregion

    #region FaceStripCG
    //: Entity.FaceStripCG
    public class FaceStripCG(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint IconImage = r.ReadUInt32();
        public readonly ObjDesc ObjDesc = new(r);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            IconImage != 0 ? new($"Icon: {IconImage:X8}", clickable: true) : null,
            new("ObjDesc", items: (ObjDesc as IHaveMetaInfo).GetInfoNodes()),
        ];
    }
    #endregion

    #region FileType
    //: Entity.FileType
    public class FileType(uint id, string name, Type t, string description = "")
    {
        public override string ToString() => $"0x{Id:X2} - {Name}";
        public uint Id = id;
        public string Name = name;
        public string Description = description;
        public Type Type = t;
    }
    #endregion

    #region FontCharDesc
    public class FontCharDesc(BinaryReader r)
    {
        public readonly ushort Unicode = r.ReadUInt16();
        public readonly ushort OffsetX = r.ReadUInt16();
        public readonly ushort OffsetY = r.ReadUInt16();
        public readonly byte Width = r.ReadByte();
        public readonly byte Height = r.ReadByte();
        public readonly byte HorizontalOffsetBefore = r.ReadByte();
        public readonly byte HorizontalOffsetAfter = r.ReadByte();
        public readonly byte VerticalOffsetBefore = r.ReadByte();
    }
    #endregion

    #region Frame
    /// <summary>
    /// Frame consists of a Vector3 Origin and a Quaternion Orientation
    /// </summary>
    //: Entity.Frame
    public class Frame
    {
        public override string ToString() => $"{Origin} - {Orientation}";
        public Vector3 Origin { get; private set; }
        public Quaternion Orientation { get; private set; }

        public Frame()
        {
            Origin = Vector3.Zero;
            Orientation = Quaternion.Identity;
        }
        //public Frame(EPosition position) : this(position.Pos, position.Rotation) { }
        public Frame(Vector3 origin, Quaternion orientation)
        {
            Origin = origin;
            Orientation = new Quaternion(orientation.X, orientation.Y, orientation.Z, orientation.W);
        }
        public Frame(BinaryReader r)
        {
            Origin = r.ReadVector3();
            Orientation = r.ReadQuaternionWFirst();
        }
    }
    #endregion

    #region GameTime
    //: Entity.GameTime
    public class GameTime(BinaryReader r) : IHaveMetaInfo
    {
        public double ZeroTimeOfYear = r.ReadDouble();
        public uint ZeroYear = r.ReadUInt32(); // Year "0" is really "P.Y. 10" in the calendar.
        public float DayLength = r.ReadSingle();
        public uint DaysPerYear = r.ReadUInt32(); // 360. Likely for easier math so each month is same length
        public string YearSpec = r.ReadL16Encoding(Encoding.Default); // "P.Y."
        public TimeOfDay[] TimesOfDay = r.Align().ReadL32FArray(x => new TimeOfDay(x));
        public string[] DaysOfTheWeek = r.ReadL32FArray(x => { var weekDay = r.ReadL16Encoding(); r.Align(); return weekDay; });
        public Season[] Seasons = r.ReadL32FArray(x => new Season(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"ZeroTimeOfYear: {ZeroTimeOfYear}"),
            new($"ZeroYear: {ZeroYear}"),
            new($"DayLength: {DayLength}"),
            new($"DaysPerYear: {DaysPerYear}"),
            new($"YearSpec: {YearSpec}"),
            new("TimesOfDay", items: TimesOfDay.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[2].Name.Replace("Name: ", "");
                items.RemoveAt(2);
                return new MetaInfo(name, items: items);
            })),
            new("DaysOfWeek", items: DaysOfTheWeek.Select(x => new MetaInfo($"{x}"))),
            new("Seasons", items: Seasons.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[1].Name.Replace("Name: ", "");
                items.RemoveAt(1);
                return new MetaInfo(name, items: items);
            })),
        ];
    }
    #endregion

    #region GearCG
    //: Entity.GearCG
    public class GearCG(BinaryReader r) : IHaveMetaInfo
    {
        public readonly string Name = r.ReadString();
        public readonly uint ClothingTable = r.ReadUInt32();
        public readonly uint WeenieDefault = r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Name: {Name}"),
            new($"Clothing Table: {ClothingTable:X8}", clickable: true),
            new($"Weenie Default: {WeenieDefault}"),
        ];
    }
    #endregion

    #region Generator
    //: Entity.Generator
    public class Generator(BinaryReader r) : IHaveMetaInfo
    {
        public readonly string Name = r.ReadL16OString();
        public readonly uint Id = r.Align().ReadUInt32();
        public readonly Generator[] Items = r.ReadL32FArray(x => new Generator(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo> {
                Id != 0 ? new($"Id: {Id}") : null,
                !string.IsNullOrEmpty(Name) ? new($"Name: {Name}") : null,
            };
            if (Items.Length > 0) nodes.AddRange(Items.Select(x => new MetaInfo(x.Id != 0 ? $"{x.Id} - {x.Name}" : x.Name, items: (x as IHaveMetaInfo).GetInfoNodes())));
            return nodes;
        }
    }
    #endregion

    #region GfxObjInfo
    //: Entity.GfxObjInfo
    public class GfxObjInfo(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint Id = r.ReadUInt32();
        public readonly uint DegradeMode = r.ReadUInt32();
        public readonly float MinDist = r.ReadSingle();
        public readonly float IdealDist = r.ReadSingle();
        public readonly float MaxDist = r.ReadSingle();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"ID: {Id:X8}", clickable: true),
            new($"DegradeMode: {DegradeMode}"),
            new($"MinDist: {MinDist}"),
            new($"IdealDist: {IdealDist}"),
            new($"MaxDist: {MaxDist}"),
        ];
    }
    #endregion

    #region HairStyleCG
    //: Entity.HairStyleCG
    public class HairStyleCG(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint IconImage = r.ReadUInt32();
        public readonly bool Bald = r.ReadByte() == 1;
        public readonly uint AlternateSetup = r.ReadUInt32();
        public readonly ObjDesc ObjDesc = new(r);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            IconImage != 0 ? new($"Icon: {IconImage:X8}", clickable: true) : null,
            Bald ? new($"Bald: True") : null,
            AlternateSetup != 0 ? new($"Alternate Setup: {AlternateSetup:X8}", clickable: true) : null,
            new("ObjDesc", items: (ObjDesc as IHaveMetaInfo).GetInfoNodes()),
        ];
    }
    #endregion

    #region HeritageGroupCG
    //: Entity.HeritageGroupCG
    public class HeritageGroupCG(BinaryReader r) : IHaveMetaInfo
    {
        public readonly string Name = r.ReadString();
        public readonly uint IconImage = r.ReadUInt32();
        public readonly uint SetupID = r.ReadUInt32(); // Basic character model
        public readonly uint EnvironmentSetupID = r.ReadUInt32(); // This is the background environment during Character Creation
        public readonly uint AttributeCredits = r.ReadUInt32();
        public readonly uint SkillCredits = r.ReadUInt32();
        public readonly int[] PrimaryStartAreas = r.ReadC32PArray<int>("i");
        public readonly int[] SecondaryStartAreas = r.ReadC32PArray<int>("i");
        public readonly SkillCG[] Skills = r.ReadC32FArray(x => new SkillCG(x));
        public readonly TemplateCG[] Templates = r.ReadC32FArray(x => new TemplateCG(x));
        public readonly byte Unknown = r.ReadByte();
        public readonly IDictionary<int, SexCG> Genders = r.ReadC32PMany<int, SexCG>("i", x => new SexCG(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Name: {Name}"),
            new($"Icon: {IconImage:X8}", clickable: true),
            new($"Setup: {SetupID:X8}", clickable: true),
            new($"Environment: {EnvironmentSetupID:X8}", clickable: true),
            new($"Attribute Credits: {AttributeCredits}"),
            new($"Skill Credits: {SkillCredits}"),
            new($"Primary Start Areas: {string.Join(",", PrimaryStartAreas)}"),
            new($"Secondary Start Areas: {string.Join(",", SecondaryStartAreas)}"),
            new("Skills", items: Skills.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[0].Name.Replace("Skill: ", "");
                items.RemoveAt(0);
                return new MetaInfo(name, items: items);
            })),
            new("Templates", items: Templates.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[0].Name.Replace("Name: ", "");
                items.RemoveAt(0);
                return new MetaInfo(name, items: items);
            })),
            new("Genders", items: Genders.Select(x => {
                var name = $"{(Gender)x.Key}";
                var items = (x.Value as IHaveMetaInfo).GetInfoNodes();
                items.RemoveAt(0);
                return new MetaInfo(name, items: items);
            })),
        ];
    }
    #endregion

    #region LandDefs
    //: Entity.LandDefs
    public class LandDefs(BinaryReader r) : IHaveMetaInfo
    {
        public readonly int NumBlockLength = r.ReadInt32();
        public readonly int NumBlockWidth = r.ReadInt32();
        public readonly float SquareLength = r.ReadSingle();
        public readonly int LBlockLength = r.ReadInt32();
        public readonly int VertexPerCell = r.ReadInt32();
        public readonly float MaxObjHeight = r.ReadSingle();
        public readonly float SkyHeight = r.ReadSingle();
        public readonly float RoadWidth = r.ReadSingle();
        public readonly float[] LandHeightTable = r.ReadFArray(x => x.ReadSingle(), 256);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"NumBlockLength: {NumBlockLength}"),
            new($"NumBlockWidth: {NumBlockWidth}"),
            new($"SquareLength: {SquareLength}"),
            new($"LBlockLength: {LBlockLength}"),
            new($"VertexPerCell: {VertexPerCell}"),
            new($"MaxObjHeight: {MaxObjHeight}"),
            new($"SkyHeight: {SkyHeight}"),
            new($"RoadWidth: {RoadWidth}"),
            new("LandHeightTable", items: LandHeightTable.Select((x, i) => new MetaInfo($"{i}: {x}"))),
        ];
    }
    #endregion

    #region LandSurf
    //: Entity.LandSurf
    public class LandSurf : IHaveMetaInfo
    {
        public readonly uint Type;
        //public readonly PalShift PalShift; // This is used if Type == 1 (which we haven't seen yet)
        public readonly TexMerge TexMerge;

        public LandSurf(BinaryReader r)
        {
            Type = r.ReadUInt32(); // This is always 0
            if (Type == 1) throw new FormatException("Type value unknown");
            TexMerge = new TexMerge(r);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => (TexMerge as IHaveMetaInfo).GetInfoNodes(resource, file, tag);
    }
    #endregion

    #region LightInfo
    //: Entity.LightInfo
    public class LightInfo(BinaryReader r) : IHaveMetaInfo
    {
        public override string ToString() => $"Viewer Space Location: {ViewerSpaceLocation}, Color: {ColorX.ToRGBA(Color)}, Intensity: {Intensity}, Falloff: {Falloff}, Cone Angle: {ConeAngle}";
        public readonly Frame ViewerSpaceLocation = new(r);
        public readonly uint Color = r.ReadUInt32(); // _RGB Color. Red is bytes 3-4, Green is bytes 5-6, Blue is bytes 7-8. Bytes 1-2 are always FF (?)
        public readonly float Intensity = r.ReadSingle();
        public readonly float Falloff = r.ReadSingle();
        public readonly float ConeAngle = r.ReadSingle();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Viewer space location: {ViewerSpaceLocation}"),
            new($"Color: {ColorX.ToRGBA(Color)}"),
            new($"Intensity: {Intensity}"),
            new($"Falloff: {Falloff}"),
            new($"ConeAngle: {ConeAngle}"),
        ];
    }
    #endregion

    #region LocationType
    //: Entity.LocationType
    public class LocationType(BinaryReader r)
    {
        public override string ToString() => $"Part ID: {PartId}, Frame: {Frame}";
        public readonly int PartId = r.ReadInt32();
        public readonly Frame Frame = new(r);
    }
    #endregion

    #region MotionData
    //: Entity.MotionData
    public class MotionData : IHaveMetaInfo
    {
        public readonly byte Bitfield;
        public readonly MotionDataFlags Flags;
        public readonly AnimData[] Anims;
        public readonly Vector3 Velocity;
        public readonly Vector3 Omega;

        public MotionData(BinaryReader r)
        {
            var numAnims = r.ReadByte();
            Bitfield = r.ReadByte();
            Flags = (MotionDataFlags)r.ReadByte();
            Anims = r.Align().ReadFArray(x => new AnimData(x), numAnims);
            if ((Flags & MotionDataFlags.HasVelocity) != 0) Velocity = r.ReadVector3();
            if ((Flags & MotionDataFlags.HasOmega) != 0) Omega = r.ReadVector3();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            Bitfield != 0 ? new($"Bitfield: {Bitfield:X8}") : null,
            Anims.Length == 0 ? null : Anims.Length == 1
                ? new("Animation", items: (Anims[0] as IHaveMetaInfo).GetInfoNodes())
                : new("Animations", items: Anims.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            Flags.HasFlag(MotionDataFlags.HasVelocity) ? new($"Velocity: {Velocity}") : null,
            Flags.HasFlag(MotionDataFlags.HasOmega) ? new($"Omega: {Omega}") : null,
        ];
    }
    #endregion

    #region NameFilterLanguageData
    //: Entity.NameFilterLanguageData
    public class NameFilterLanguageData(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint MaximumVowelsInARow = r.ReadUInt32();
        public readonly uint FirstNCharactersMustHaveAVowel = r.ReadUInt32();
        public readonly uint VowelContainingSubstringLength = r.ReadUInt32();
        public readonly uint ExtraAllowedCharacters = r.ReadUInt32();
        public readonly byte Unknown = r.ReadByte();
        public readonly string[] CompoundLetterGroups = r.ReadL32FArray(x => x.ReadC32UString());

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"MaximumVowelsInARow: {MaximumVowelsInARow}"),
            new($"FirstNCharactersMustHaveAVowel: {FirstNCharactersMustHaveAVowel}"),
            new($"VowelContainingSubstringLength: {VowelContainingSubstringLength}"),
            new($"ExtraAllowedCharacters: {ExtraAllowedCharacters}"),
            new($"Unknown: {Unknown}"),
            new($"CompoundLetterGrounds", items: CompoundLetterGroups.Select(x => new MetaInfo($"{x}"))),
        ];
    }
    #endregion

    #region ObjDesc
    //: Entity.ObjDesc
    public class ObjDesc : IHaveMetaInfo
    {
        public readonly uint PaletteID;
        public readonly List<SubPalette> SubPalettes;
        public readonly List<TextureMapChange> TextureChanges;
        public readonly List<AnimationPartChange> AnimPartChanges;

        public ObjDesc()
        {
            SubPalettes = [];
            TextureChanges = [];
            AnimPartChanges = [];
        }
        public ObjDesc(BinaryReader r)
        {
            r.Align();
            r.ReadByte(); // ObjDesc always starts with 11.
            var numPalettes = r.ReadByte();
            var numTextureMapChanges = r.ReadByte();
            var numAnimPartChanges = r.ReadByte();
            if (numPalettes > 0) PaletteID = r.ReadAsDataIDOfKnownType(0x04000000);
            SubPalettes = [.. r.ReadFArray(x => new SubPalette(x), numPalettes)];
            TextureChanges = [.. r.ReadFArray(x => new TextureMapChange(x), numTextureMapChanges)];
            AnimPartChanges = [.. r.ReadFArray(x => new AnimationPartChange(x), numAnimPartChanges)]; r.Align();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            PaletteID != 0 ? new($"Palette ID: {PaletteID:X8}", clickable: true) : null,
            SubPalettes.Count > 0 ? new("SubPalettes", items: SubPalettes.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[0].Name;
                items.RemoveAt(0);
                return new MetaInfo(name, items: items);
            })) : null,
            TextureChanges.Count > 0 ? new("Texture Changes", items: TextureChanges.Select(x => new MetaInfo($"{x}", clickable: true))) : null,
            AnimPartChanges.Count > 0 ? new("AnimPart Changes", items: AnimPartChanges.Select(x => new MetaInfo($"{x}", clickable: true))) : null,
        ];

        /// <summary>
        /// Helper function to ensure we don't add redundant parts to the list
        /// </summary>
        public void AddTextureChange(TextureMapChange tm)
        {
            var e = TextureChanges.FirstOrDefault(c => c.PartIndex == tm.PartIndex && c.OldTexture == tm.OldTexture && c.NewTexture == tm.NewTexture);
            if (e == null) TextureChanges.Add(tm);
        }

        /// <summary>
        /// Helper function to ensure we only have one AnimationPartChange.PartId in the list
        /// </summary>
        public void AddAnimPartChange(AnimationPartChange ap)
        {
            var p = AnimPartChanges.FirstOrDefault(c => c.PartIndex == ap.PartIndex && c.PartID == ap.PartID);
            if (p != null) AnimPartChanges.Remove(p);
            AnimPartChanges.Add(ap);
        }
    }
    #endregion

    #region ObjectDesc
    //: Entity.ObjectDesc
    public class ObjectDesc(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint ObjId = r.ReadUInt32();
        public readonly Frame BaseLoc = new Frame(r);
        public readonly float Freq = r.ReadSingle();
        public readonly float DisplaceX = r.ReadSingle(); public readonly float DisplaceY = r.ReadSingle();
        public readonly float MinScale = r.ReadSingle(); public readonly float MaxScale = r.ReadSingle();
        public readonly float MaxRotation = r.ReadSingle();
        public readonly float MinSlope = r.ReadSingle(); public readonly float MaxSlope = r.ReadSingle();
        public readonly uint Align = r.ReadUInt32(); public readonly uint Orient = r.ReadUInt32();
        public readonly uint WeenieObj = r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Object ID: {ObjId:X8}", clickable: true),
            new($"BaseLoc: {BaseLoc}"),
            new($"Frequency: {Freq}"),
            new($"DisplaceX: {DisplaceX} DisplaceY: {DisplaceY}"),
            new($"MinScale: {MinScale} MaxScale: {MaxScale}"),
            new($"MaxRotation: {MaxRotation}"),
            new($"MinSlope: {MinSlope} MaxSlope: {MaxSlope}"),
            Align != 0 ? new($"Align: {Align}") : null,
            Orient != 0 ? new($"Orient: {Orient}") : null,
            WeenieObj != 0 ? new($"WeenieObj: {WeenieObj}") : null,
        ];
    }
    #endregion

    #region PhysicsScriptData
    public class PhysicsScriptData(BinaryReader r) : IHaveMetaInfo
    {
        public readonly double StartTime = r.ReadDouble();
        public readonly AnimationHook Hook = AnimationHook.Factory(r);

        //: Entity.PhysicsScriptData
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"StartTime: {StartTime}"),
            new($"Hook:", items: (Hook as IHaveMetaInfo).GetInfoNodes(tag:tag)),
        ];
    }
    #endregion

    #region PhysicsScriptTableData
    //: Entity.PhysicsScriptTableData
    public class PhysicsScriptTableData(BinaryReader r) : IHaveMetaInfo
    {
        public readonly ScriptAndModData[] Scripts = r.ReadL32FArray(x => new ScriptAndModData(r));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new("ScriptMods", items: Scripts.Select(x=>new MetaInfo($"{x}", clickable: true))),
        ];
    }
    #endregion

    #region PlacementType
    //: Entity.PlacementType
    public class PlacementType(BinaryReader r, uint numParts) : IHaveMetaInfo
    {
        public readonly AnimationFrame AnimFrame = new(r, numParts);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => (AnimFrame as IHaveMetaInfo).GetInfoNodes(resource, file, tag);
    }
    #endregion

    #region Plane
    //: Entity.Plane
    public class Plane
    {
        public override string ToString() => $"Normal: {N} - Distance: {D}";
        public Vector3 N;
        public float D;
        public Plane() { }
        public Plane(BinaryReader r)
        {
            N = r.ReadVector3();
            D = r.ReadSingle();
        }
        public System.Numerics.Plane ToNumerics() => new System.Numerics.Plane(N, D);
    }
    #endregion

    #region Polygon
    //: Entity.Polygon
    public class Polygon : IHaveMetaInfo
    {
        public byte NumPts;
        public StipplingType Stippling; // Whether it has that textured/bumpiness to it
        public CullMode SidesType;
        public short PosSurface;
        public short NegSurface;
        public short[] VertexIds;
        public byte[] PosUVIndices;
        public byte[] NegUVIndices;
        public SWVertex[] Vertices;

        public Polygon() { }
        public Polygon(BinaryReader r)
        {
            NumPts = r.ReadByte();
            Stippling = (StipplingType)r.ReadByte();
            SidesType = (CullMode)r.ReadInt32();
            PosSurface = r.ReadInt16();
            NegSurface = r.ReadInt16();
            VertexIds = r.ReadPArray<short>("h", NumPts);
            PosUVIndices = !Stippling.HasFlag(StipplingType.NoPos) ? r.ReadBytes(NumPts) : new byte[0];
            NegUVIndices = SidesType == CullMode.Clockwise && !Stippling.HasFlag(StipplingType.NoNeg) ? r.ReadBytes(NumPts) : new byte[0];
            if (SidesType == CullMode.None) { NegSurface = PosSurface; NegUVIndices = PosUVIndices; }
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            //new($"NumPoints: {NumPts}"),
            new($"Stippling: {Stippling}"),
            new($"CullMode: {SidesType}"),
            new($"PosSurface: {PosSurface}"),
            new($"NegSurface: {NegSurface}"),
            new($"Vertex IDs: {string.Join(", ", VertexIds)}"),
            new($"PosUVIndices: {string.Join(", ", PosUVIndices)}"),
            new($"NegUVIndices: {string.Join(", ", NegUVIndices)}"),
        ];

        public void LoadVertices(CVertexArray vertexArray) => Vertices = VertexIds.Select(id => vertexArray.Vertices[(ushort)id]).ToArray();
    }
    #endregion

    #region PortalPoly
    //: Entity.PortalPoly
    public class PortalPoly(BinaryReader r)
    {
        public override string ToString() => $"Portal Idx: {PortalIndex}, Polygon ID: {PolygonId}";
        public readonly short PortalIndex = r.ReadInt16();
        public readonly short PolygonId = r.ReadInt16();
    }
    #endregion

    #region Position
    /// <summary>
    /// Position consists of a CellID + a Frame (Origin + Orientation)
    /// </summary>
    //: Entity.Position
    public class Position(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint ObjCellID = r.ReadUInt32();
        public readonly Frame Frame = new Frame(r);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            ObjCellID != 0 ? new($"ObjCell ID: {ObjCellID:X8}", clickable: true) : null,
            !Frame.Origin.IsZeroEpsilon() ? new($"Origin: {Frame.Origin}") : null,
            !Frame.Orientation.IsIdentity ? new($"Orientation: {Frame.Orientation}") : null,
        ];
    }
    #endregion

    #region RegionMisc
    //: Entity.RegionMisc
    public class RegionMisc(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint Version = r.ReadUInt32();
        public readonly uint GameMapID = r.ReadUInt32();
        public readonly uint AutotestMapId = r.ReadUInt32();
        public readonly uint AutotestMapSize = r.ReadUInt32();
        public readonly uint ClearCellId = r.ReadUInt32();
        public readonly uint ClearMonsterId = r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Version: {Version}"),
            new($"GameMap ID: {GameMapID:X8}", clickable: true),
            new($"AutoTest Map ID: {AutotestMapId:X8}", clickable: true),
            new($"AutoTest Map Size: {AutotestMapSize}"),
            new($"ClearCell ID: {ClearCellId:X8}", clickable: true),
            new($"ClearMonster ID: {ClearMonsterId:X8}", clickable: true),
        ];
    }
    #endregion

    #region RoadAlphaMap
    //: Entity.RoadAlphaMap
    public class RoadAlphaMap(BinaryReader r) : IHaveMetaInfo
    {
        public override string ToString() => $"RoadCode: {RCode}, RoadTexGID: {RoadTexGID:X8}";
        public readonly uint RCode = r.ReadUInt32();
        public readonly uint RoadTexGID = r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"RoadCode: {RCode}"),
            new($"RoadTexGID: {RoadTexGID:X8}", clickable: true),
        ];
    }
    #endregion

    #region SceneDesc
    //: Entity.SceneDesc
    public class SceneDesc(BinaryReader r) : IHaveMetaInfo
    {
        public readonly SceneType[] SceneTypes = r.ReadL32FArray(x => new SceneType(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new("SceneTypes", items: SceneTypes.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
        ];
    }
    #endregion

    #region SceneType
    //: Entity.SceneType
    public class SceneType(BinaryReader r) : IHaveMetaInfo
    {
        public uint StbIndex = r.ReadUInt32();
        public uint[] Scenes = r.ReadL32PArray<uint>("I");

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"SceneTableIdx: {StbIndex}"),
            new("Scenes", items: Scenes.Select(x => new MetaInfo($"{x:X8}", clickable: true))),
        ];
    }
    #endregion

    #region ScriptAndModData
    //: Entity.ScriptMod
    public class ScriptAndModData(BinaryReader r) : IHaveMetaInfo
    {
        public override string ToString() => $"Mod: {Mod}, Script: {ScriptId:X8}";
        public readonly float Mod = r.ReadSingle();
        public readonly uint ScriptId = r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{Mod}"),
            new($"{ScriptId:X8}", clickable: true),
        ];
    }
    #endregion

    #region Season
    //: Entity.Season
    public class Season(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint StartDate = r.ReadUInt32();
        public readonly string Name = (r.ReadL16Encoding(Encoding.Default), r.Align()).Item1;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"StartDate: {StartDate}"),
            new($"Name: {Name}"),
        ];
    }
    #endregion

    #region SexCG
    //: Entity.SexCG
    public class SexCG(BinaryReader r) : IHaveMetaInfo
    {
        public string Name = r.ReadString();
        public uint Scale = r.ReadUInt32();
        public uint SetupID = r.ReadUInt32();
        public uint SoundTable = r.ReadUInt32();
        public uint IconImage = r.ReadUInt32();
        public uint BasePalette = r.ReadUInt32();
        public uint SkinPalSet = r.ReadUInt32();
        public uint PhysicsTable = r.ReadUInt32();
        public uint MotionTable = r.ReadUInt32();
        public uint CombatTable = r.ReadUInt32();
        public ObjDesc BaseObjDesc = new ObjDesc(r);
        public uint[] HairColorList = r.ReadC32PArray<uint>("I");
        public HairStyleCG[] HairStyleList = r.ReadC32FArray(x => new HairStyleCG(x));
        public uint[] EyeColorList = r.ReadC32PArray<uint>("I");
        public EyeStripCG[] EyeStripList = r.ReadC32FArray(x => new EyeStripCG(x));
        public FaceStripCG[] NoseStripList = r.ReadC32FArray(x => new FaceStripCG(x));
        public FaceStripCG[] MouthStripList = r.ReadC32FArray(x => new FaceStripCG(x));
        public GearCG[] HeadgearList = r.ReadC32FArray(x => new GearCG(x));
        public GearCG[] ShirtList = r.ReadC32FArray(x => new GearCG(x));
        public GearCG[] PantsList = r.ReadC32FArray(x => new GearCG(x));
        public GearCG[] FootwearList = r.ReadC32FArray(x => new GearCG(x));
        public uint[] ClothingColorsList = r.ReadC32PArray<uint>("I");

        // Eyes
        public uint GetEyeTexture(uint eyesStrip, bool isBald) => (isBald ? EyeStripList[Convert.ToInt32(eyesStrip)].ObjDescBald : EyeStripList[Convert.ToInt32(eyesStrip)].ObjDesc).TextureChanges[0].NewTexture;
        public uint GetDefaultEyeTexture(uint eyesStrip, bool isBald) => (isBald ? EyeStripList[Convert.ToInt32(eyesStrip)].ObjDescBald : EyeStripList[Convert.ToInt32(eyesStrip)].ObjDesc).TextureChanges[0].OldTexture;

        // Nose
        public uint GetNoseTexture(uint noseStrip) => NoseStripList[Convert.ToInt32(noseStrip)].ObjDesc.TextureChanges[0].NewTexture;
        public uint GetDefaultNoseTexture(uint noseStrip) => NoseStripList[Convert.ToInt32(noseStrip)].ObjDesc.TextureChanges[0].OldTexture;

        // Mouth
        public uint GetMouthTexture(uint mouthStrip) => MouthStripList[Convert.ToInt32(mouthStrip)].ObjDesc.TextureChanges[0].NewTexture;
        public uint GetDefaultMouthTexture(uint mouthStrip) => MouthStripList[Convert.ToInt32(mouthStrip)].ObjDesc.TextureChanges[0].OldTexture;

        // Hair (Head)
        public uint? GetHeadObject(uint hairStyle)
        {
            var hairstyle = HairStyleList[Convert.ToInt32(hairStyle)];
            // Gear Knights, both Olthoi types have multiple anim part changes.
            return hairstyle.ObjDesc.AnimPartChanges.Count == 1 ? hairstyle.ObjDesc.AnimPartChanges[0].PartID : (uint?)null;
        }
        public uint? GetHairTexture(uint hairStyle)
        {
            var hairstyle = HairStyleList[Convert.ToInt32(hairStyle)];
            // OlthoiAcid has no TextureChanges
            return hairstyle.ObjDesc.TextureChanges.Count > 0 ? (uint?)hairstyle.ObjDesc.TextureChanges[0].NewTexture : null;
        }
        public uint? GetDefaultHairTexture(uint hairStyle)
        {
            var hairstyle = HairStyleList[Convert.ToInt32(hairStyle)];
            // OlthoiAcid has no TextureChanges
            return hairstyle.ObjDesc.TextureChanges.Count > 0 ? (uint?)hairstyle.ObjDesc.TextureChanges[0].OldTexture : null;
        }

        // Headgear
        public uint GetHeadgearWeenie(uint headgearStyle) => HeadgearList[Convert.ToInt32(headgearStyle)].WeenieDefault;
        public uint GetHeadgearClothingTable(uint headgearStyle) => HeadgearList[Convert.ToInt32(headgearStyle)].ClothingTable;

        // Shirt
        public uint GetShirtWeenie(uint shirtStyle) => ShirtList[Convert.ToInt32(shirtStyle)].WeenieDefault;
        public uint GetShirtClothingTable(uint shirtStyle) => ShirtList[Convert.ToInt32(shirtStyle)].ClothingTable;

        // Pants
        public uint GetPantsWeenie(uint pantsStyle) => PantsList[Convert.ToInt32(pantsStyle)].WeenieDefault;
        public uint GetPantsClothingTable(uint pantsStyle) => PantsList[Convert.ToInt32(pantsStyle)].ClothingTable;

        // Footwear
        public uint GetFootwearWeenie(uint footwearStyle) => FootwearList[Convert.ToInt32(footwearStyle)].WeenieDefault;
        public uint GetFootwearClothingTable(uint footwearStyle) => FootwearList[Convert.ToInt32(footwearStyle)].ClothingTable;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Name: {Name}"),
            new($"Scale: {Scale}%"),
            new($"Setup: {SetupID:X8}", clickable: true),
            new($"Sound Table: {SoundTable:X8}", clickable: true),
            new($"Icon: {IconImage:X8}", clickable: true),
            new($"Base Palette: {BasePalette:X8}", clickable: true),
            new($"Skin Palette Set: {SkinPalSet:X8}", clickable: true),
            new($"Physics Table: {PhysicsTable:X8}", clickable: true),
            new($"Motion Table: {MotionTable:X8}", clickable: true),
            new($"Combat Table: {CombatTable:X8}", clickable: true),
            new("ObjDesc", items: (BaseObjDesc as IHaveMetaInfo).GetInfoNodes()),
            new("Hair Colors", items: HairColorList.Select(x => new MetaInfo($"{x:X8}", clickable: true))),
            new("Hair Styles", items: HairStyleList.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            new("Eye Colors", items: EyeColorList.Select(x => new MetaInfo($"{x:X8}", clickable: true))),
            new("Eye Strips", items: EyeStripList.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            new("Nose Strips", items: NoseStripList.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            new("Mouth Strips", items: MouthStripList.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            new("Headgear", items: HeadgearList.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            new("Shirt", items: ShirtList.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            new("Pants", items: PantsList.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            new("Footwear", items: FootwearList.Select((x, i) => new MetaInfo($"{i}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
            new("Clothing Colors", items: ClothingColorsList.OrderBy(i => i).Select(x => new MetaInfo($"{x} - {(PaletteTemplate)x}"))),
        ];
    }
    #endregion

    #region SkillBase
    //: Entity.SkillBase
    public class SkillBase : IHaveMetaInfo
    {
        public readonly string Description;
        public readonly string Name;
        public readonly uint IconId;
        public readonly int TrainedCost;
        /// <summary>
        /// This is the total cost to specialize a skill, which INCLUDES the trained cost.
        /// </summary>
        public readonly int SpecializedCost;
        public readonly uint Category;      // 1 = combat, 2 = other, 3 = magic
        public readonly uint ChargenUse;    // always 1?
        /// <summary>
        /// This is the minimum SAC required for usability.
        /// 1 = Usable when untrained
        /// 2 = Trained or greater required for usability
        /// </summary>
        public readonly uint MinLevel;      // 1-2?
        public readonly SkillFormula Formula;
        public readonly double UpperBound;
        public readonly double LowerBound;
        public readonly double LearnMod;
        public int UpgradeCostFromTrainedToSpecialized => SpecializedCost - TrainedCost;

        public SkillBase() { }
        public SkillBase(SkillFormula formula) => Formula = formula;
        public SkillBase(BinaryReader r)
        {
            Description = r.ReadL16Encoding(Encoding.Default); r.Align();
            Name = r.ReadL16Encoding(Encoding.Default); r.Align();
            IconId = r.ReadUInt32();
            TrainedCost = r.ReadInt32();
            SpecializedCost = r.ReadInt32();
            Category = r.ReadUInt32();
            ChargenUse = r.ReadUInt32();
            MinLevel = r.ReadUInt32();
            Formula = new SkillFormula(r);
            UpperBound = r.ReadDouble();
            LowerBound = r.ReadDouble();
            LearnMod = r.ReadDouble();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Name: {Name}"),
            new($"Description: {Description}"),
            new($"Icon: {IconId:X8}", clickable: true),
            new($"TrainedCost: {TrainedCost}"),
            new($"SpecializedCost: {SpecializedCost}"),
            new($"Category: {(SpellCategory)Category}"),
            new($"CharGenUse: {ChargenUse}"),
            new($"MinLevel: {MinLevel}"),
            new("SkillFormula", items: (Formula as IHaveMetaInfo).GetInfoNodes()),
            new($"UpperBound: {UpperBound}"),
            new($"LowerBound: {LowerBound}"),
            new($"LearnMod: {LearnMod}"),
        ];
    }
    #endregion

    #region SkillCG
    //: Entity.SkillCG
    public class SkillCG(BinaryReader r) : IHaveMetaInfo
    {
        public readonly Skill SkillNum = (Skill)r.ReadUInt32();
        public readonly int NormalCost = r.ReadInt32();
        public readonly int PrimaryCost = r.ReadInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Skill: {SkillNum}"),
            new($"Normal Cost: {NormalCost}"),
            new($"Primary Cost: {PrimaryCost}"),
        ];
    }
    #endregion

    #region SkillFormula
    //: Entity.SkillFormula
    public class SkillFormula : IHaveMetaInfo
    {
        public readonly uint W;
        public readonly uint X;
        public readonly uint Y;
        public readonly uint Z;
        public readonly uint Attr1;
        public readonly uint Attr2;

        public SkillFormula() { }
        public SkillFormula(PropertyAttribute attr1, PropertyAttribute attr2, uint divisor)
        {
            X = 1;
            Z = divisor;
            Attr1 = (uint)attr1;
            Attr2 = (uint)attr2;
        }
        public SkillFormula(BinaryReader r)
        {
            W = r.ReadUInt32();
            X = r.ReadUInt32();
            Y = r.ReadUInt32();
            Z = r.ReadUInt32();
            Attr1 = r.ReadUInt32();
            Attr2 = r.ReadUInt32();
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Attr1: {(PropertyAttribute)Attr1}"),
            new($"Attr2: {(PropertyAttribute)Attr2}"),
            new($"W: {W}"),
            new($"X: {X}"),
            new($"Y: {Y}"),
            new($"Z (divisor): {Z}"),
        ];
    }
    #endregion

    #region SkyDesc
    //: Entity.SkyDesc
    public class SkyDesc(BinaryReader r) : IHaveMetaInfo
    {
        public readonly double TickSize = r.ReadDouble();
        public readonly double LightTickSize = r.ReadDouble();
        public readonly DayGroup[] DayGroups = r.Align().ReadL32FArray(x => new DayGroup(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"TickSize: {TickSize}"),
            new($"LightTickSize: {LightTickSize}"),
            new("DayGroups", items: DayGroups.Select((x, i) => new MetaInfo($"{i:D2}", items: (x as IHaveMetaInfo).GetInfoNodes()))),
        ];
    }
    #endregion

    #region SkyObject
    //: Entity.SkyObject
    public class SkyObject(BinaryReader r) : IHaveMetaInfo
    {
        public readonly float BeginTime = r.ReadSingle();
        public readonly float EndTime = r.ReadSingle();
        public readonly float BeginAngle = r.ReadSingle();
        public readonly float EndAngle = r.ReadSingle();
        public readonly float TexVelocityX = r.ReadSingle();
        public readonly float TexVelocityY = r.ReadSingle();
        public readonly float TexVelocityZ = 0;
        public readonly uint DefaultGFXObjectId = r.ReadUInt32();
        public readonly uint DefaultPESObjectId = r.ReadUInt32();
        public readonly uint Properties = (r.ReadUInt32(), r.Align()).Item1;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            BeginTime != 0 ? new($"BeginTime: {BeginTime}") : null,
            EndTime != 0 ? new($"EndTime: {EndTime}") : null,
            BeginAngle != 0 ? new($"BeginAngle: {BeginAngle}") : null,
            EndAngle != 0 ? new($"EndAngle: {EndAngle}") : null,
            TexVelocityX != 0 ? new($"TexVelocityX: {TexVelocityX}") : null,
            TexVelocityY != 0 ? new($"TexVelocityY: {TexVelocityY}") : null,
            DefaultGFXObjectId != 0 ? new($"DefaultGFXObjectId: {DefaultGFXObjectId:X8}", clickable: true) : null,
            DefaultPESObjectId != 0 ? new($"DefaultPESObjectId: {DefaultPESObjectId:X8}", clickable: true) : null,
            Properties != 0 ? new($"Properties: {Properties:X}") : null,
        ];
    }
    #endregion

    #region SkyObjectReplace
    //: Entity.SkyObjectReplace
    public class SkyObjectReplace(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint ObjectIndex = r.ReadUInt32();
        public readonly uint GFXObjId = r.ReadUInt32();
        public readonly float Rotate = r.ReadSingle();
        public readonly float Transparent = r.ReadSingle();
        public readonly float Luminosity = r.ReadSingle();
        public readonly float MaxBright = (r.ReadSingle(), r.Align()).Item1;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Obj Idx: {ObjectIndex}"),
            GFXObjId != 0 ? new($"GfxObj ID: {GFXObjId:X8}", clickable: true) : null,
            Rotate != 0 ? new($"Rotate: {Rotate}") : null,
            Transparent != 0 ? new($"Transparent: {Transparent}") : null,
            Luminosity != 0 ? new($"Luminosity: {Luminosity}") : null,
            MaxBright != 0 ? new($"MaxBright: {MaxBright}") : null,
        ];
    }
    #endregion

    #region SkyTimeOfDay
    //: Entity.SkyTimeOfDay
    public class SkyTimeOfDay(BinaryReader r) : IHaveMetaInfo
    {
        public readonly float Begin = r.ReadSingle();

        public readonly float DirBright = r.ReadSingle();
        public readonly float DirHeading = r.ReadSingle();
        public readonly float DirPitch = r.ReadSingle();
        public readonly uint DirColor = r.ReadUInt32();

        public readonly float AmbBright = r.ReadSingle();
        public readonly uint AmbColor = r.ReadUInt32();

        public readonly float MinWorldFog = r.ReadSingle();
        public readonly float MaxWorldFog = r.ReadSingle();
        public readonly uint WorldFogColor = r.ReadUInt32();
        public readonly uint WorldFog = r.ReadUInt32();

        public readonly SkyObjectReplace[] SkyObjReplace = r.Align().ReadL32FArray(x => new SkyObjectReplace(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Begin: {Begin}"),
            new($"DirBright: {DirBright}"),
            new($"DirHeading: {DirHeading}"),
            new($"DirPitch: {DirPitch}"),
            new($"DirColor: {DirColor:X8}"),
            new($"AmbientBrightness: {AmbBright}"),
            new($"AmbientColor: {AmbColor:X8}"),
            new($"MinFog: {MinWorldFog}"),
            new($"MaxFog: {MaxWorldFog}"),
            new($"FogColor: {WorldFogColor:X8}"),
            new($"Fog: {WorldFog}"),
            new("SkyObjectReplace", items: SkyObjReplace.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[0].Name.Replace("ObjIdx: ", "");
                items.RemoveAt(0);
                return new MetaInfo(name, items: items);
            })),
        ];
    }
    #endregion

    #region SoundData
    //: Entity.SoundData
    public class SoundData(BinaryReader r) : IHaveMetaInfo
    {
        public readonly SoundTableData[] Data = r.ReadL32FArray(x => new SoundTableData(x));
        public readonly uint Unknown = r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new("SoundTable", items: Data.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[0].Name.Replace("Sound ID: ", "");
                items.RemoveAt(0);
                return new MetaInfo(name, items: items, clickable: true);
            })),
            new($"Unknown: {Unknown}"),
        ];
    }
    #endregion

    #region SoundDesc
    //: Entity.SoundDesc
    public class SoundDesc(BinaryReader r) : IHaveMetaInfo
    {
        public readonly AmbientSTBDesc[] STBDesc = r.ReadL32FArray(x => new AmbientSTBDesc(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new("SoundTable", items: STBDesc.Select((x, i) => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[0].Name.Replace("Ambient Sound Table ID: ", "");
                items.RemoveAt(0);
                return new MetaInfo($"{i}: {name}", items: items, clickable: true);
            })),
        ];
    }
    #endregion

    #region SoundTableData
    //: Entity.SoundTableData
    public class SoundTableData(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint SoundId = r.ReadUInt32(); // Corresponds to the DatFileType.Wave
        public readonly float Priority = r.ReadSingle();
        public readonly float Probability = r.ReadSingle();
        public readonly float Volume = r.ReadSingle();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Sound ID: {SoundId:X8}", clickable: true),
            new($"Priority: {Priority}"),
            new($"Probability: {Probability}"),
            new($"Volume: {Volume}"),
        ];
    }
    #endregion

    #region SpellBase
    //: Entity.SpellBase
    public class SpellBase : IHaveMetaInfo
    {
        public readonly string Name;
        public readonly string Desc;
        public readonly MagicSchool School;
        public readonly uint Icon;
        public readonly SpellCategory Category; // All related levels of the same spell. Same category spells will not stack. (Strength Self I & Strength Self II)
        public readonly uint Bitfield;
        public readonly uint BaseMana; // Mana Cost
        public readonly float BaseRangeConstant;
        public readonly float BaseRangeMod;
        public readonly uint Power; // Used to determine which spell in the catgory is the strongest.
        public readonly float SpellEconomyMod; // A legacy of a bygone era
        public readonly uint FormulaVersion;
        public readonly float ComponentLoss; // Burn rate
        public readonly SpellType MetaSpellType;
        public readonly uint MetaSpellId; // Just the spell id again

        // Only on EnchantmentSpell/FellowshipEnchantmentSpells
        public readonly double Duration;
        public readonly float DegradeModifier; // Unknown what this does
        public readonly float DegradeLimit;  // Unknown what this does

        public readonly double PortalLifetime; // Only for PortalSummon_SpellType

        public readonly uint[] Formula; // UInt Values correspond to the SpellComponentsTable

        public readonly uint CasterEffect;  // effect that playes on the caster of the casted spell (e.g. for buffs, protects, etc)
        public readonly uint TargetEffect; // effect that playes on the target of the casted spell (e.g. for debuffs, vulns, etc)
        public readonly uint FizzleEffect; // is always zero. All spells have the same fizzle effect.
        public readonly double RecoveryInterval; // is always zero
        public readonly float RecoveryAmount; // is always zero
        public readonly uint DisplayOrder; // for soring in the spell list in the client UI
        public readonly uint NonComponentTargetType; // Unknown what this does
        public readonly uint ManaMod; // Additional mana cost per target (e.g. "Incantation of Acid Bane" Mana Cost = 80 + 14 per target)

        public SpellBase() { }
        public SpellBase(uint power, double duration, float degradeModifier, float degradeLimit)
        {
            Power = power;
            Duration = duration;
            DegradeModifier = degradeModifier;
            DegradeLimit = degradeLimit;
        }
        public SpellBase(BinaryReader r)
        {
            Name = r.ReadL16OString(); r.Align();
            Desc = r.ReadL16OString(); r.Align();
            School = (MagicSchool)r.ReadUInt32();
            Icon = r.ReadUInt32();
            Category = (SpellCategory)r.ReadUInt32();
            Bitfield = r.ReadUInt32();
            BaseMana = r.ReadUInt32();
            BaseRangeConstant = r.ReadSingle();
            BaseRangeMod = r.ReadSingle();
            Power = r.ReadUInt32();
            SpellEconomyMod = r.ReadSingle();
            FormulaVersion = r.ReadUInt32();
            ComponentLoss = r.ReadSingle();
            MetaSpellType = (SpellType)r.ReadUInt32();
            MetaSpellId = r.ReadUInt32();
            switch (MetaSpellType)
            {
                case SpellType.Enchantment:
                case SpellType.FellowEnchantment:
                    Duration = r.ReadDouble();
                    DegradeModifier = r.ReadSingle();
                    DegradeLimit = r.ReadSingle();
                    break;
                case SpellType.PortalSummon: PortalLifetime = r.ReadDouble(); break;
            }

            // Components : Load them first, then decrypt them. More efficient to hash all at once.
            var rawComps = r.ReadPArray<uint>("I", 8);

            // Get the decryped component values
            Formula = DecryptFormula(rawComps, Name, Desc);

            CasterEffect = r.ReadUInt32();
            TargetEffect = r.ReadUInt32();
            FizzleEffect = r.ReadUInt32();
            RecoveryInterval = r.ReadDouble();
            RecoveryAmount = r.ReadSingle();
            DisplayOrder = r.ReadUInt32();
            NonComponentTargetType = r.ReadUInt32();
            ManaMod = r.ReadUInt32();
        }

        const uint HIGHEST_COMP_ID = 198; // "Essence of Kemeroi", for Void Spells -- not actually ever in game!

        /// <summary>
        /// Does the math based on the crypto keys (name and description) for the spell formula.
        /// </summary>
        static uint[] DecryptFormula(uint[] rawComps, string name, string desc)
        {
            // uint testDescHash = ComputeHash(" – 200");
            uint nameHash = SpellTable.ComputeHash(name);
            uint descHash = SpellTable.ComputeHash(desc);
            var key = (nameHash % 0x12107680) + (descHash % 0xBEADCF45);

            var comps = new uint[rawComps.Length];
            for (var i = 0; i < rawComps.Length; i++)
            {
                var comp = rawComps[i] - key;
                // This seems to correct issues with certain spells with extended characters.
                if (comp > HIGHEST_COMP_ID) comp &= 0xFF; // highest comp ID is 198 - "Essence of Kemeroi", for Void Spells
                comps[i] = comp;
            }
            return comps;
        }

        string _spellWords;

        /// <summary>
        /// Not technically part of this function, but saves numerous looks later.
        /// </summary>
        public string GetSpellWords(SpellComponentTable comps)
        {
            if (_spellWords != null) return _spellWords;
            _spellWords = SpellComponentTable.GetSpellWords(comps, Formula);
            return _spellWords;
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var componentTable = DatabaseManager.Portal.SpellComponentTable;
            return [
                new($"Name: {Name}"),
                new($"Description: {Desc}"),
                new($"School: {School}"),
                new($"Icon: {Icon:X8}", clickable: true),
                new($"Category: {Category}"),
                new($"Flags: {(SpellFlags)Bitfield}"),
                new($"BaseMana: {BaseMana}"),
                new($"BaseRangeConstant: {BaseRangeConstant}"),
                new($"BaseRangeMod: {BaseRangeMod}"),
                new($"Power: {Power}"),
                new($"SpellEconomyMod: {SpellEconomyMod}"),
                new($"FormulaVersion: {FormulaVersion}"),
                new($"ComponentLoss: {ComponentLoss}"),
                new($"MetaSpellType: {MetaSpellType}"),
                new($"MetaSpellId: {MetaSpellId}"),
                new($"Duration: {Duration}"),
                new($"DegradeModifier: {DegradeModifier}"),
                new($"DegradeLimit: {DegradeLimit}"),
                new("Formula", items: Formula.Select(x => new MetaInfo($"{x}: {componentTable.SpellComponents[x].Name}"))),
                new($"CasterEffect: {(PlayScript)CasterEffect}"),
                new($"TargetEffect: {(PlayScript)TargetEffect}"),
                new($"FizzleEffect: {(PlayScript)FizzleEffect}"),
                new($"RecoveryInterval: {RecoveryInterval}"),
                new($"RecoveryAmount: {RecoveryAmount}"),
                new($"DisplayOrder: {DisplayOrder}"),
                new($"NonComponentTargetType: {(ItemType)NonComponentTargetType}"),
                new($"ManaMod: {ManaMod}"),
            ];
        }
    }
    #endregion

    #region SpellComponentBase
    //: Entity.SpellComponentBase
    public class SpellComponentBase(BinaryReader r) : IHaveMetaInfo
    {
        public readonly string Name = r.ReadL16OString();
        public readonly uint Category = r.Align().ReadUInt32();
        public readonly uint Icon = r.ReadUInt32();
        public readonly uint Type = r.ReadUInt32();
        public readonly uint Gesture = r.ReadUInt32();
        public readonly float Time = r.ReadSingle();
        public readonly string Text = r.ReadL16OString();
        public readonly float CDM = r.Align().ReadSingle(); // Unsure what this is

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Name: {Name}"),
            new($"Category: {Category}"),
            new($"Icon: {Icon:X8}", clickable: true),
            new($"Type: {(SpellComponentTable.Type)Type}"),
            Gesture != 0x80000000 ? new($"Gesture: {(MotionCommand)Gesture}") : null,
            new($"Time: {Time}"),
            !string.IsNullOrEmpty(Text) ? new($"Text: {Text}") : null,
            new($"CDM: {CDM}"),
        ];
    }
    #endregion

    #region SpellSet
    //: Entity.SpellSet
    public class SpellSet : IHaveMetaInfo
    {
        // uint key is the total combined item level of all the equipped pieces in the set client calls this m_PieceCount
        public readonly IDictionary<uint, SpellSetTiers> SpellSetTiers;
        public readonly uint HighestTier;
        public readonly IDictionary<uint, SpellSetTiers> SpellSetTiersNoGaps;

        public SpellSet(BinaryReader r)
        {
            SpellSetTiers = r.Skip(2).ReadL16PMany<uint, SpellSetTiers>("I", x => new SpellSetTiers(x), sorted: true);
            HighestTier = SpellSetTiers.Keys.LastOrDefault();
            SpellSetTiersNoGaps = new SortedDictionary<uint, SpellSetTiers>();
            var lastSpellSetTier = default(SpellSetTiers);
            for (var i = 0U; i <= HighestTier; i++)
            {
                if (SpellSetTiers.TryGetValue(i, out var spellSetTiers)) lastSpellSetTier = spellSetTiers;
                if (lastSpellSetTier != null) SpellSetTiersNoGaps.Add(i, lastSpellSetTier);
            }
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new("SpellSetTiers", items: SpellSetTiers.Select(x => new MetaInfo($"{x.Key}", items: (x.Value as IHaveMetaInfo).GetInfoNodes()))),
            new($"HighestTier: {HighestTier}"),
        ];
    }
    #endregion

    #region SpellSetTiers
    //: Entity.SpellSetTier
    public class SpellSetTiers : IHaveMetaInfo
    {
        /// <summary>
        /// A list of spell ids that are active in the spell set tier
        /// </summary>
        public readonly uint[] Spells;

        public SpellSetTiers(BinaryReader r)
            => Spells = r.ReadL32PArray<uint>("I");

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var spells = DatabaseManager.Portal.SpellTable.Spells;
            var nodes = Spells.Select(x => new MetaInfo($"{x} - {spells[x].Name}")).ToList();
            return nodes;
        }
    }
    #endregion

    #region Sphere
    //: Entity.Sphere
    public class Sphere
    {
        public override string ToString() => $"Origin: {Origin}, Radius: {Radius}";
        public static readonly Sphere Empty = new();
        public Vector3 Origin;
        public float Radius;

        public Sphere() { Origin = Vector3.Zero; }
        public Sphere(BinaryReader r)
        {
            Origin = r.ReadVector3();
            Radius = r.ReadSingle();
        }
    }
    #endregion

    #region Stab
    /// <summary>
    /// I'm not quite sure what a "Stab" is, but this is what the client calls these.
    /// It is an object and a corresponding position. 
    /// Note that since these are referenced by either a landblock or a cellblock, the corresponding Landblock and Cell should come from the parent.
    /// </summary>
    //: Entity.Stab
    public class Stab(BinaryReader r) : IHaveMetaInfo
    {
        public override string ToString() => $"ID: {Id:X8}, Frame: {Frame}";
        readonly uint Id = r.ReadUInt32();
        public readonly Frame Frame = new(r);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"ID: {Id:X8}", clickable: true),
            new($"Frame: {Frame}"),
        ];
    }
    #endregion

    #region StarterArea
    //: Entity.StarterArea
    public class StarterArea(BinaryReader r) : IHaveMetaInfo
    {
        public readonly string Name = r.ReadString();
        public readonly Position[] Locations = r.ReadC32FArray(x => new Position(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Name: {Name}"),
            new("Locations", items: Locations.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[0].Name.Replace("ObjCellID: ", "");
                items.RemoveAt(0);
                return new MetaInfo(name, items: items, clickable: true);
            })),
        ];
    }
    #endregion

    #region StringTableData
    //: Entity.StringTableData
    public class StringTableData(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint Id = r.ReadUInt32();
        public readonly string[] VarNames = r.ReadL16FArray(x => x.ReadC32UString());
        public readonly string[] Vars = r.ReadL16FArray(x => x.ReadC32UString());
        public readonly string[] Strings = r.ReadL32FArray(x => x.ReadC32UString());
        public readonly uint[] Comments = r.ReadL32PArray<uint>("I");
        public readonly byte Unknown = r.ReadByte();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"{Id:X8}"),
            VarNames.Length > 0 ? new("Variable Names", items: VarNames.Select(x => new MetaInfo($"{x}"))) : null,
            Vars.Length > 0 ? new("Variables", items: Vars.Select(x => new MetaInfo($"{x}"))) : null,
            Strings.Length > 0 ? new("Strings", items: Strings.Select(x => new MetaInfo($"{x}"))) : null,
            Comments.Length > 0 ? new("Comments", items: Comments.Select(x => new MetaInfo($"{x:X8}"))) : null,
        ];
    }
    #endregion

    #region SubPalette
    // TODO: refactor to use existing PaletteOverride object
    //: Entity.SubPalette
    public class SubPalette : IHaveMetaInfo
    {
        public uint SubID;
        public uint Offset;
        public uint NumColors;

        public SubPalette() { }
        public SubPalette(BinaryReader r)
        {
            SubID = r.ReadAsDataIDOfKnownType(0x04000000);
            Offset = (uint)(r.ReadByte() * 8);
            NumColors = r.ReadByte();
            if (NumColors == 0) NumColors = 256;
            NumColors *= 8;
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"SubID: {SubID:X8}", clickable: true),
            new($"Offset: {Offset}"),
            new($"NumColors: {NumColors}"),
        ];
    }
    #endregion

    #region SWVertex
    /// <summary>
    /// A vertex position, normal, and texture coords
    /// </summary>
    //: Entity.Vertex
    public class SWVertex : IHaveMetaInfo
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Normal;
        public readonly Vec2Duv[] UVs;

        //: Entity+SWVertex
        public SWVertex(Vector3 origin, Vector3 normal)
        {
            Origin = origin;    // ref?
            Normal = normal;
        }
        public SWVertex(BinaryReader r)
        {
            var numUVs = r.ReadUInt16();
            Origin = r.ReadVector3();
            Normal = r.ReadVector3();
            UVs = r.ReadFArray(x => new Vec2Duv(x), numUVs);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Origin: {Origin}"),
            new($"Normal: {Normal}"),
            new($"UVs", items: UVs.SelectMany(x => (x as IHaveMetaInfo).GetInfoNodes(resource, file))),
        ];
    }
    #endregion

    #region TabooTableEntry
    public class TabooTableEntry(BinaryReader r)
    {
        public readonly uint Unknown1 = r.ReadUInt32(); // This always seems to be 0x00010101
        public readonly ushort Unknown2 = r.ReadUInt16(); // This always seems to be 0
        /// <summary>
        /// All patterns are lower case<para />
        /// Patterns are expected in the following format: [*]word[*]<para />
        /// The asterisk is optional. They can be used to forbid strings that contain a pattern, require the pattern to be the whole word, or require the word to either start/end with the pattern.
        /// </summary>
        public readonly string[] BannedPatterns = r.ReadL32FArray(x => x.ReadString());

        /// <summary>
        /// This will search all the BannedPatterns to see if the input passes or fails.
        /// </summary>
        public bool ContainsBadWord(string input)
        {
            // Our entire banned patterns list should be lower case
            input = input.ToLowerInvariant();
            // First, we need to split input into separate words
            var words = input.Split(' ');
            foreach (var word in words)
                foreach (var bannedPattern in BannedPatterns) if (Regex.IsMatch(word, $"^{bannedPattern.Replace("*", ".*")}$")) return true;
            return false;
        }
    }
    #endregion

    #region TemplateCG
    //: Entity.TemplateCG
    public class TemplateCG(BinaryReader r) : IHaveMetaInfo
    {
        public string Name = r.ReadString();
        public uint IconImage = r.ReadUInt32();
        public CharacterTitle Title = (CharacterTitle)r.ReadUInt32();
        // Attributes
        public uint Strength = r.ReadUInt32();
        public uint Endurance = r.ReadUInt32();
        public uint Coordination = r.ReadUInt32();
        public uint Quickness = r.ReadUInt32();
        public uint Focus = r.ReadUInt32();
        public uint Self = r.ReadUInt32();
        public Skill[] NormalSkillsList = r.ReadC32PArray<Skill>("I");
        public Skill[] PrimarySkillsList = r.ReadC32PArray<Skill>("I");

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Name: {Name}"),
            new($"Icon: {IconImage:X8}", clickable: true),
            new($"Title: {Title}"),
            new($"Strength: {Strength}"),
            new($"Endurance: {Endurance}"),
            new($"Coordination: {Coordination}"),
            new($"Quickness: {Quickness}"),
            new($"Focus: {Focus}"),
            new($"Self: {Self}"),
            NormalSkillsList.Length > 0 ? new("Normal Skills", items: NormalSkillsList.Select(x => new MetaInfo($"{x}"))) : null,
            PrimarySkillsList.Length > 0 ? new("Primary Skills", items: PrimarySkillsList.Select(x => new MetaInfo($"{x}"))) : null,
        ];
    }
    #endregion

    #region TerrainAlphaMap
    //: Entity.TerrainAlphaMap
    public class TerrainAlphaMap(BinaryReader r) : IHaveMetaInfo
    {
        public override string ToString() => $"TerrainCode: {TCode}, TextureGID: {TexGID:X8}";
        public readonly uint TCode = r.ReadUInt32();
        public readonly uint TexGID = r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"TerrainCode: {TCode}"),
            new($"TextureGID: {TexGID:X8}", clickable: true),
        ];
    }
    #endregion

    #region TerrainDesc
    //: Entity.TerrainDesc
    public class TerrainDesc(BinaryReader r) : IHaveMetaInfo
    {
        public readonly TerrainType[] TerrainTypes = r.ReadL32FArray(x => new TerrainType(x));
        public readonly LandSurf LandSurfaces = new(r);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new("TerrainTypes", items: TerrainTypes.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[0].Name.Replace("TerrainName: ", "");
                items.RemoveAt(0);
                return new MetaInfo(name, items: items);
            })),
            new($"LandSurf", items: (LandSurfaces as IHaveMetaInfo).GetInfoNodes()),
        ];
    }
    #endregion

    #region TerrainTex
    //: Entity.TerrainTex
    public class TerrainTex(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint TexGID = r.ReadUInt32();
        public readonly uint TexTiling = r.ReadUInt32();
        public readonly uint MaxVertBright = r.ReadUInt32();
        public readonly uint MinVertBright = r.ReadUInt32();
        public readonly uint MaxVertSaturate = r.ReadUInt32();
        public readonly uint MinVertSaturate = r.ReadUInt32();
        public readonly uint MaxVertHue = r.ReadUInt32();
        public readonly uint MinVertHue = r.ReadUInt32();
        public readonly uint DetailTexTiling = r.ReadUInt32();
        public readonly uint DetailTexGID = r.ReadUInt32();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"TexGID: {TexGID:X8}", clickable: true),
            new($"TexTiling: {TexTiling}"),
            new($"MaxVertBrightness: {MaxVertBright}"),
            new($"MinVertBrightness: {MinVertBright}"),
            new($"MaxVertSaturate: {MaxVertSaturate}"),
            new($"MinVertSaturate: {MinVertSaturate}"),
            new($"MaxVertHue: {MaxVertHue}"),
            new($"MinVertHue: {MinVertHue}"),
            new($"DetailTexTiling: {DetailTexTiling}"),
            new($"DetailTexGID: {DetailTexGID:X8}", clickable: true),
        ];
    }
    #endregion

    #region TerrainType
    public class TerrainType(BinaryReader r) : IHaveMetaInfo
    {
        public readonly string TerrainName = r.ReadL16Encoding(Encoding.Default);
        public readonly uint TerrainColor = r.Align().ReadUInt32();
        public readonly uint[] SceneTypes = r.ReadL32PArray<uint>("I");

        //: Entity.TerrainType
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"TerrainName: {TerrainName}"),
            new($"TerrainColor: {TerrainColor:X8}"),
            new("SceneTypes", items: SceneTypes.Select((x, i) => new MetaInfo($"{i}: {x}"))),
        ];
    }
    #endregion

    #region TexMerge
    //: Entity.TexMerge
    public class TexMerge(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint BaseTexSize = r.ReadUInt32();
        public readonly TerrainAlphaMap[] CornerTerrainMaps = r.ReadL32FArray(x => new TerrainAlphaMap(x));
        public readonly TerrainAlphaMap[] SideTerrainMaps = r.ReadL32FArray(x => new TerrainAlphaMap(x));
        public readonly RoadAlphaMap[] RoadMaps = r.ReadL32FArray(x => new RoadAlphaMap(x));
        public readonly TMTerrainDesc[] TerrainDesc = r.ReadL32FArray(x => new TMTerrainDesc(x));

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"BaseTextureSize: {BaseTexSize}"),
            new("CornerTerrainMaps", items: CornerTerrainMaps.Select(x => new MetaInfo($"{x}", clickable: true))),
            new("SideTerrainMap", items: SideTerrainMaps.Select(x => new MetaInfo($"{x}", clickable: true))),
            new("RoadAlphaMap", items: RoadMaps.Select(x => new MetaInfo($"{x}", clickable: true))),
            new("TMTerrainDesc", items: TerrainDesc.Select(x => {
                var items = (x as IHaveMetaInfo).GetInfoNodes();
                var name = items[0].Name.Replace("TerrainType: ", "");
                items.RemoveAt(0);
                return new MetaInfo(name, items: items);
            })),
        ];
    }
    #endregion

    #region TextureMapChange
    // TODO: refactor to merge with existing TextureMapOverride object
    //: Entity.TextureMapChange
    public class TextureMapChange(BinaryReader r) : IHaveMetaInfo
    {
        public override string ToString() => $"PartIdx: {PartIndex}, Old Tex: {OldTexture:X8}, New Tex: {NewTexture:X8}";
        public readonly byte PartIndex = r.ReadByte();
        public readonly uint OldTexture = r.ReadAsDataIDOfKnownType(0x05000000);
        public readonly uint NewTexture = r.ReadAsDataIDOfKnownType(0x05000000);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"PartIdx: {PartIndex}"),
            new($"Old Texture: {OldTexture:X8}", clickable: true),
            new($"New Texture: {NewTexture:X8}", clickable: true),
        ];
    }
    #endregion

    #region TimeOfDay
    //: Entity.TimeOfDay
    public class TimeOfDay(BinaryReader r) : IHaveMetaInfo
    {
        public readonly float Start = r.ReadSingle();
        public readonly bool IsNight = r.ReadUInt32() == 1;
        public readonly string Name = (r.ReadL16Encoding(Encoding.Default), r.Align()).Item1;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"Start: {Start}"),
            new($"IsNight: {IsNight}"),
            new($"Name: {Name}"),
        ];
    }
    #endregion

    #region TMTerrainDesc
    //: Entity.TMTerrainDesc
    public class TMTerrainDesc(BinaryReader r) : IHaveMetaInfo
    {
        public readonly uint TerrainType = r.ReadUInt32();
        public readonly TerrainTex TerrainTex = new(r);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"TerrainType: {TerrainType}"),
            new("TerrainTexture", items: (TerrainTex as IHaveMetaInfo).GetInfoNodes()),
        ];
    }
    #endregion

    #region Vec2Duv
    /// <summary>
    /// Info on texture UV mapping
    /// </summary>
    //: Entity.UV
    public class Vec2Duv(BinaryReader r) : IHaveMetaInfo
    {
        public readonly float U = r.ReadSingle();
        public readonly float V = r.ReadSingle();

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new($"U: {U} V: {V}"),
        ];
    }
    #endregion
}

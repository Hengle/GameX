using GameX.Valve.Formats.Vpk;
using OpenStack.Gfx;
using OpenStack.Gfx.Renders;
using OpenStack.Gfx.Textures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static GameX.Valve.Formats.Vpk.D_Texture;
using static System.IO.Polyfill;

namespace GameX.Valve.Formats
{
    #region Binary_Src
    //was:Resource/Resource

    public class Binary_Src : IDisposable, IHaveMetaInfo, IRedirected<ITexture>, IRedirected<IMaterial>, IRedirected<IMesh>, IRedirected<IModel>, IRedirected<IParticleSystem>
    {
        internal const ushort KnownHeaderVersion = 12;
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s)
        {
            if (r.BaseStream.Length < 6) return null;
            var input = r.Peek(z => z.ReadBytes(6));
            var magic = BitConverter.ToUInt32(input, 0);
            var magicResourceVersion = BitConverter.ToUInt16(input, 4);
            if (magic == PakBinary_Vpk.MAGIC) throw new InvalidOperationException("Pak File");
            else if (magic == CompiledShader.MAGIC) return Task.FromResult((object)new CompiledShader(r, f.Path));
            else if (magic == ClosedCaptions.MAGIC) return Task.FromResult((object)new ClosedCaptions(r));
            else if (magic == ToolsAssetInfo.MAGIC || magic == ToolsAssetInfo.MAGIC2) return Task.FromResult((object)new ToolsAssetInfo(r));
            else if (magic == XKV3.MAGIC || magic == XKV3.MAGIC2) { var kv3 = new XKV3 { Size = (uint)r.BaseStream.Length }; kv3.Read(null, r); return Task.FromResult((object)kv3); }
            else if (magicResourceVersion == KnownHeaderVersion) return Task.FromResult((object)new Binary_Src(r));
            //else if (magicResourceVersion == BinaryPak.KnownHeaderVersion)
            //{
            //    var pak = new BinaryPak(r);
            //    switch (pak.DataType)
            //    {
            //        //case DATA.DataType.Mesh: return Task.FromResult((object)new DATAMesh(pak));
            //        default: return Task.FromResult((object)pak);
            //    }
            //}
            else return null;
        }

        public Binary_Src() { }
        public Binary_Src(BinaryReader r) => Read(r);

        public void Dispose()
        {
            Reader?.Dispose();
            Reader = null;
            GC.SuppressFinalize(this);
        }

        public void Read(BinaryReader r, bool verifyFileSize = false) //:true
        {
            Reader = r;
            FileSize = r.ReadUInt32();
            if (FileSize == 0x55AA1234) throw new FormatException("VPK file");
            else if (FileSize == CompiledShader.MAGIC) throw new FormatException("Shader file");
            else if (FileSize != r.BaseStream.Length) { }
            var headerVersion = r.ReadUInt16();
            if (headerVersion != KnownHeaderVersion) throw new FormatException($"Bad Magic: {headerVersion}, expected {KnownHeaderVersion}");
            //if (FileName != null) DataType = DetermineResourceTypeByFileExtension();
            Version = r.ReadUInt16();
            var blockOffset = r.ReadUInt32();
            var blockCount = r.ReadUInt32();
            r.Skip(blockOffset - 8); // 8 is uint32 x2 we just read
            for (var i = 0; i < blockCount; i++)
            {
                var blockType = Encoding.UTF8.GetString(r.ReadBytes(4));
                var position = r.BaseStream.Position;
                var offset = (uint)position + r.ReadUInt32();
                var size = r.ReadUInt32();
                var block = size >= 4 && blockType == "DATA" && !Block.IsHandledType(DataType) ? r.Peek(z =>
                {
                    var magic = z.ReadUInt32();
                    return magic == XKV3.MAGIC || magic == XKV3.MAGIC2 || magic == XKV3.MAGIC3
                        ? new XKV3()
                        : magic == XKV1.MAGIC ? (Block)new XKV1() : null;
                }) : null;
                block ??= Block.Factory(this, blockType);
                block.Offset = offset;
                block.Size = size;
                if (blockType == "REDI" || blockType == "RED2" || blockType == "NTRO") block.Read(this, r);
                Blocks.Add(block);
                switch (block)
                {
                    case REDI redi:
                        // Try to determine resource type by looking at first compiler indentifier
                        if (DataType == ResourceType.Unknown && REDI.Structs.TryGetValue(REDI.REDIStruct.SpecialDependencies, out var specialBlock))
                        {
                            var specialDeps = (R_SpecialDependencies)specialBlock;
                            if (specialDeps.List.Count > 0) DataType = Block.DetermineTypeByCompilerIdentifier(specialDeps.List[0]);
                        }
                        // Try to determine resource type by looking at the input dependency if there is only one
                        if (DataType == ResourceType.Unknown && REDI.Structs.TryGetValue(REDI.REDIStruct.InputDependencies, out var inputBlock))
                        {
                            var inputDeps = (R_InputDependencies)inputBlock;
                            if (inputDeps.List.Count == 1) DataType = Block.DetermineResourceTypeByFileExtension(Path.GetExtension(inputDeps.List[0].ContentRelativeFilename));
                        }
                        break;
                    case NTRO ntro:
                        if (DataType == ResourceType.Unknown && ntro.ReferencedStructs.Count > 0)
                            switch (ntro.ReferencedStructs[0].Name)
                            {
                                case "VSoundEventScript_t": DataType = ResourceType.SoundEventScript; break;
                                case "CWorldVisibility": DataType = ResourceType.WorldVisibility; break;
                            }
                        break;
                }
                r.BaseStream.Position = position + 8;
            }
            foreach (var block in Blocks) if (!(block is REDI) && !(block is RED2) && !(block is NTRO)) block.Read(this, r);

            var fullFileSize = FullFileSize;
            if (verifyFileSize && Reader.BaseStream.Length != fullFileSize)
            {
                if (DataType == ResourceType.Texture)
                {
                    var data = (D_Texture)DATA;
                    // TODO: We do not currently have a way of calculating buffer size for these types, Texture.GenerateBitmap also just reads until end of the buffer
                    if (data.Format == VTexFormat.JPEG_DXT5 || data.Format == VTexFormat.JPEG_RGBA8888) return;
                    // TODO: Valve added null bytes after the png for whatever reason, so assume we have the full file if the buffer is bigger than the size we calculated
                    if (data.Format == VTexFormat.PNG_DXT5 || data.Format == VTexFormat.PNG_RGBA8888 && Reader.BaseStream.Length > fullFileSize) return;
                }
                throw new InvalidDataException($"File size ({Reader.BaseStream.Length}) does not match size specified in file ({fullFileSize}) ({DataType}).");
            }
        }

        ITexture IRedirected<ITexture>.Value => DATA as ITexture;
        IMaterial IRedirected<IMaterial>.Value => DATA as IMaterial;
        IMesh IRedirected<IMesh>.Value => DataType == ResourceType.Mesh ? new D_Mesh(this) as IMesh : null;
        IModel IRedirected<IModel>.Value => DATA as IModel;
        IParticleSystem IRedirected<IParticleSystem>.Value => DATA as IParticleSystem;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo> {
                new("BinaryPak", items: [
                    new($"FileSize: {FileSize}"),
                    new($"Version: {Version}"),
                    new($"Blocks: {Blocks.Count}"),
                    new($"DataType: {DataType}"),
                ])
            };
            switch (DataType)
            {
                case ResourceType.Texture:
                    {
                        var data = (D_Texture)DATA;
                        try
                        {
                            nodes.AddRange([
                                //new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "PICTURE" }), //(tex.GenerateBitmap().ToBitmap(), tex.Width, tex.Height)
                                new(null, new MetaContent { Type = "Texture", Name = "Texture", Value = this, Dispose = this }),
                                new("Texture", items: [
                                    new($"Width: {data.Width}"),
                                    new($"Height: {data.Height}"),
                                    new($"NumMipMaps: {data.NumMipMaps}"),
                                ])
                            ]);
                        }
                        catch (Exception e)
                        {
                            nodes.Add(new MetaInfo(null, new MetaContent { Type = "Text", Name = "Exception", Value = e.Message }));
                        }
                    }
                    break;
                case ResourceType.Panorama:
                    {
                        var data = (D_Panorama)DATA;
                        nodes.AddRange([
                            new(null, new MetaContent { Type = "DataGrid", Name = "Panorama Names", Value = data.Names }),
                            new("Panorama", items: [
                                new($"Names: {data.Names.Count}"),
                            ])
                        ]);
                    }
                    break;
                case ResourceType.PanoramaLayout: break;
                case ResourceType.PanoramaScript: break;
                case ResourceType.PanoramaStyle: break;
                case ResourceType.ParticleSystem: nodes.Add(new MetaInfo(null, new MetaContent { Type = "ParticleSystem", Name = "ParticleSystem", Value = this, Dispose = this })); break;
                case ResourceType.Sound:
                    {
                        var sound = (D_Sound)DATA;
                        var stream = sound.GetSoundStream();
                        nodes.Add(new(null, new MetaContent { Type = "AudioPlayer", Name = "Sound", Value = stream, Tag = $".{sound.SoundType}", Dispose = this }));
                    }
                    break;
                case ResourceType.World: nodes.Add(new(null, new MetaContent { Type = "World", Name = "World", Value = (D_World)DATA, Dispose = this })); break;
                case ResourceType.WorldNode: nodes.Add(new(null, new MetaContent { Type = "World", Name = "World Node", Value = (D_WorldNode)DATA, Dispose = this })); break;
                case ResourceType.Model: nodes.Add(new(null, new MetaContent { Type = "Model", Name = "Model", Value = this, Dispose = this })); break;
                case ResourceType.Mesh: nodes.Add(new(null, new MetaContent { Type = "Model", Name = "Mesh", Value = this, Dispose = this })); break;
                case ResourceType.Material: nodes.Add(new(null, new MetaContent { Type = "Material", Name = "Material", Value = this, Dispose = this })); break;
            }
            foreach (var block in Blocks)
            {
                if (block is RERL repl) { nodes.Add(new(null, new MetaContent { Type = "DataGrid", Name = "External Refs", Value = repl.RERLInfos })); continue; }
                else if (block is NTRO ntro)
                {
                    if (ntro.ReferencedStructs.Count > 0) nodes.Add(new(null, new MetaContent { Type = "DataGrid", Name = "Introspection Manifest: Structs", Value = ntro.ReferencedStructs }));
                    if (ntro.ReferencedEnums.Count > 0) nodes.Add(new(null, new MetaContent { Type = "DataGrid", Name = "Introspection Manifest: Enums", Value = ntro.ReferencedEnums }));
                }
                var tab = new MetaContent { Type = "Text", Name = block.GetType().Name };
                nodes.Add(new(null, tab));
                if (block is DATA)
                    switch (DataType)
                    {
                        case ResourceType.Sound: tab.Value = ((D_Sound)block).ToString(); break;
                        case ResourceType.ParticleSystem:
                        case ResourceType.Mesh:
                            if (block is XKV3 kv3) tab.Value = kv3.ToString();
                            else if (block is NTRO blockNTRO) tab.Value = blockNTRO.ToString();
                            break;
                        default: tab.Value = block.ToString(); break;
                    }
                else tab.Value = block.ToString();
            }
            if (!nodes.Any(x => x.Tag is MetaContent { Dispose: not null })) Dispose();
            return nodes;
        }

        public BinaryReader Reader { get; private set; }

        public uint FileSize { get; private set; }

        public ushort Version { get; private set; }

        public RERL RERL => GetBlockByType<RERL>();
        public REDI REDI => GetBlockByType<REDI>();
        public NTRO NTRO => GetBlockByType<NTRO>();
        public VBIB VBIB => GetBlockByType<VBIB>();
        public DATA DATA => GetBlockByType<DATA>();

        public T GetBlockByIndex<T>(int index) where T : Block => Blocks[index] as T;

        public T GetBlockByType<T>() where T : Block => (T)Blocks.Find(b => typeof(T).IsAssignableFrom(b.GetType()));

        public bool ContainsBlockType<T>() where T : Block => Blocks.Exists(b => typeof(T).IsAssignableFrom(b.GetType()));

        public bool TryGetBlockType<T>(out T value) where T : Block => (value = (T)Blocks.Find(b => typeof(T).IsAssignableFrom(b.GetType()))) != null;

        public readonly List<Block> Blocks = [];

        public ResourceType DataType;

        /// <summary>
        /// Resource files have a FileSize in the metadata, however certain file types such as sounds have streaming audio data come
        /// after the resource file, and the size is specified within the DATA block. This property attemps to return the correct size.
        /// </summary>
        public uint FullFileSize
        {
            get
            {
                var size = FileSize;
                if (DataType == ResourceType.Sound)
                {
                    var data = (D_Sound)DATA;
                    size += data.StreamingDataSize;
                }
                else if (DataType == ResourceType.Texture)
                {
                    var data = (D_Texture)DATA;
                    size += (uint)data.CalculateTextureDataSize();
                }
                return size;
            }
        }
    }

    #endregion

    #region Binary_Mdl10

    public unsafe class Binary_Mdl10 : ITexture, IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Mdl10(r, f, (BinaryPakFile)s));

        #region Headers

        const uint M_MAGIC = 0x54534449; //: IDST
        const uint M_MAGIC2 = 0x51534449; //: IDSQ
        public const int CoordinateAxes = 6;
        public const int SequenceBlends = 2;

        /// <summary>
        /// header flags
        /// </summary>
        [Flags]
        public enum HeaderFlags : int
        {
            ROCKET = 1,             // leave a trail
            GRENADE = 2,            // leave a trail
            GIB = 4,                // leave a trail
            ROTATE = 8,             // rotate (bonus items)
            TRACER = 16,            // green split trail
            ZOMGIB = 32,            // small blood trail
            TRACER2 = 64,           // orange split trail + rotate
            TRACER3 = 128,          // purple trail
            NOSHADELIGHT = 256,     // No shade lighting
            HITBOXCOLLISIONS = 512, // Use hitbox collisions
            FORCESKYLIGHT = 1024,	// Forces the model to be lit by skybox lighting
        }

        /// <summary>
        /// lighting flags
        /// </summary>
        [Flags]
        public enum LightFlags : int
        {
            FLATSHADE = 0x0001,
            CHROME = 0x0002,
            FULLBRIGHT = 0x0004,
            MIPMAPS = 0x0008,
            ALPHA = 0x0010,
            ADDITIVE = 0x0020,
            MASKED = 0x0040,
            RENDER_FLAGS = CHROME | ADDITIVE | MASKED | FULLBRIGHT
        }

        /// <summary>
        /// motion flags
        /// </summary>
        [Flags]
        public enum MotionFlags : int
        {
            X = 0x0001,
            Y = 0x0002,
            Z = 0x0004,
            XR = 0x0008,
            YR = 0x0010,
            ZR = 0x0020,
            LX = 0x0040,
            LY = 0x0080,
            LZ = 0x0100,
            AX = 0x0200,
            AY = 0x0400,
            AZ = 0x0800,
            AXR = 0x1000,
            AYR = 0x2000,
            AZR = 0x4000,
            BONECONTROLLER_TYPES = X | Y | Z | XR | YR | ZR,
            TYPES = 0x7FFF,
            CONTROL_FIRST = X,
            CONTROL_LAST = AZR,
            RLOOP = 0x8000 // controller that wraps shortest distance
        }

        /// <summary>
        /// sequence flags
        /// </summary>
        [Flags]
        public enum SeqFlags : int
        {
            LOOPING = 0x0001
        }

        /// <summary>
        /// bone flags
        /// </summary>
        [Flags]
        public enum BoneFlags : int
        {
            NORMALS = 0x0001,
            VERTICES = 0x0002,
            BBOX = 0x0004,
            CHROME = 0x0008 // if any of the textures have chrome on them
        }

        // sequence header
        public struct M_SeqHeader
        {
            public static (string, int) Struct = ("<2i64si", sizeof(M_SeqHeader));
            public int Magic;
            public int Version;
            public fixed byte Name[64];
            public int Length;
        }

        // bones
        public struct M_Bone
        {
            public static (string, int) Struct = ("<32s8i12f", sizeof(M_Bone));
            public fixed byte Name[32]; // bone name for symbolic links
            public int Parent; // parent bone
            public BoneFlags Flags;
            public fixed int BoneController[CoordinateAxes]; // bone controller index, -1 == none
            public fixed float Value[CoordinateAxes]; // default DoF values
            public fixed float Scale[CoordinateAxes]; // scale for delta DoF values
        }

        public class BoneAxis(BoneController controller, float value, float scale)
        {
            public BoneController Controller = controller;
            public float Value = value;
            public float Scale = scale;
        }

        public class Bone(M_Bone s, int id, BoneController[] controllers)
        {
            public string Name = UnsafeX.FixedAString(s.Name, 32);
            public Bone Parent;
            public int ParentId = s.Parent;
            public BoneFlags Flags = s.Flags;
            public BoneAxis[] Axes = [
                new BoneAxis(s.BoneController[0] != -1 ? controllers[s.BoneController[0]] : null, s.Value[0], s.Scale[0]),
                new BoneAxis(s.BoneController[1] != -1 ? controllers[s.BoneController[1]] : null, s.Value[1], s.Scale[1]),
                new BoneAxis(s.BoneController[2] != -1 ? controllers[s.BoneController[2]] : null, s.Value[2], s.Scale[2]),
                new BoneAxis(s.BoneController[3] != -1 ? controllers[s.BoneController[3]] : null, s.Value[3], s.Scale[3]),
                new BoneAxis(s.BoneController[4] != -1 ? controllers[s.BoneController[4]] : null, s.Value[4], s.Scale[4]),
                new BoneAxis(s.BoneController[5] != -1 ? controllers[s.BoneController[5]] : null, s.Value[5], s.Scale[5])];
            public int Id = id;

            public static void Remap(Bone[] bones)
            {
                foreach (var bone in bones) if (bone.ParentId != -1) bone.Parent = bones[bone.ParentId];
            }
        }

        // bone controllers
        public struct M_BoneController
        {
            public static (string, int) Struct = ("<2i2f2i", sizeof(M_BoneController));
            public int Bone;   // -1 == 0
            public int Type;   // X, Y, Z, XR, YR, ZR, M
            public float Start, End;
            public int Rest;   // byte index value at rest
            public int Index;  // 0-3 user set controller, 4 mouth
        }

        public class BoneController(ref M_BoneController s, int id)
        {
            public int Type = s.Type;
            public float Start = s.Start, End = s.End;
            public int Rest = s.Rest;
            public int Index = s.Index;
            public int Id = id;
        }

        // intersection boxes
        public struct M_BBox
        {
            public static (string, int) Struct = ("<2i6f", sizeof(M_BBox));
            public int Bone;
            public int Group; // intersection group
            public Vector3 BBMin, BBMax; // bounding box
        }

        public class BBox(ref M_BBox s, Bone[] bones)
        {
            public Bone Bone = bones[s.Bone];
            public int Group = s.Group;
            public Vector3 BBMin = s.BBMin, BBMax = s.BBMax;
        }

        // sequence groups
        public struct M_SeqGroup
        {
            public static (string, int) Struct = ("<32s64s2i", sizeof(M_SeqGroup));
            public fixed byte Label[32]; // textual name
            public fixed byte Name[64]; // file name
            public int Unused1; // was "cache"  - index pointer
            public int Unused2; // was "data" -  hack for group 0
        }

        public class SeqGroup(M_SeqGroup s)
        {
            public string Label = UnsafeX.FixedAString(s.Label, 32);
            public string Name = UnsafeX.FixedAString(s.Name, 64);
            public int Offset = s.Unused2;
        }

        // sequence descriptions
        public struct M_Seq
        {
            public static (string, int) Struct = ("<32sf10i3f2i6f4i4f6i", sizeof(M_Seq));
            public fixed byte Label[32]; // sequence label
            public float Fps;           // frames per second
            public int Flags;           // looping/non-looping flags
            public int Activity;
            public int ActWeight;
            public X_LumpNO Events;
            public int NumFrames;       // number of frames per sequence
            public X_LumpNO Pivots;     // number of foot pivots
            public int MotionType;
            public int MotionBone;
            public Vector3 LinearMovement;
            public int AutomovePosIndex;
            public int AutomoveAngleIndex;
            public Vector3 BBMin, BBMax; // per sequence bounding box
            public int NumBlends;
            public int AnimIndex;       // mstudioanim_t pointer relative to start of sequence group data: [blend][bone][X, Y, Z, XR, YR, ZR]
            public fixed int BlendType[SequenceBlends];  // X, Y, Z, XR, YR, ZR
            public fixed float BlendStart[SequenceBlends]; // starting value
            public fixed float BlendEnd[SequenceBlends]; // ending value
            public int BlendParent;
            public int SeqGroup;        // sequence group for demand loading
            public int EntryNode;       // transition node at entry
            public int ExitNode;        // transition node at exit
            public int NodeFlags;       // transition rules
            public int NextSeq;         // auto advancing sequences
        }

        public class SeqBlend(int type, float start, float end)
        {
            public int Type = type;
            public float Start = start;
            public float End = end;
        }

        public class SeqPivot(Vector3 origin, int start, int end)
        {
            public Vector3 Origin = origin;
            public int Start = start;
            public int End = end;
        }

        public class SeqAnimation(M_AnimValue[][] axis)
        {
            public M_AnimValue[][] Axis = axis;
        }

        public class Seq
        {
            public string Label;
            public float Fps;
            public int Flags;
            public int Activity;
            public int ActWeight;
            public M_Event[] Events;
            public M_Event[] SortedEvents;
            public int NumFrames;
            public M_Pivot[] Pivots;
            public int MotionType;
            public int MotionBone;
            public Vector3 LinearMovement;
            public Vector3 BBMin, BBMax;
            public SeqAnimation[][] AnimationBlends;
            public SeqBlend[] Blend;
            public int EntryNode;
            public int ExitNode;
            public int NodeFlags;
            public int NextSequence;

            public Seq(BinaryReader r, M_Seq s, (BinaryReader r, M_SeqHeader h)[] sequences, int zeroGroupOffset, bool isXashModel, Bone[] bones)
            {
                if (s.SeqGroup < 0 || (s.SeqGroup != 0 && (s.SeqGroup - 1) >= sequences.Length)) throw new Exception("Invalid seqgroup value");
                Label = UnsafeX.FixedAString(s.Label, 32);
                Fps = s.Fps;
                Flags = s.Flags;
                Activity = s.Activity;
                ActWeight = s.ActWeight;
                r.Seek(s.Events.Offset); Events = r.ReadSArray<M_Event>(s.Events.Num);
                SortedEvents = [.. Events.OrderBy(x => x.Frame)];
                NumFrames = s.NumFrames;
                if (isXashModel) { Pivots = []; }
                else { r.Seek(s.Pivots.Offset); Pivots = r.ReadSArray<M_Pivot>(s.Pivots.Num); }
                MotionType = s.MotionType;
                MotionBone = s.MotionBone;
                LinearMovement = s.LinearMovement;
                BBMin = s.BBMin; BBMax = s.BBMax;
                AnimationBlends = GetAnimationBlends(r, s, sequences, zeroGroupOffset, bones.Length);
                Blend = [new SeqBlend(s.BlendType[0], s.BlendStart[0], s.BlendEnd[0]), new SeqBlend(s.BlendType[1], s.BlendStart[1], s.BlendEnd[1])];
                EntryNode = s.EntryNode;
                ExitNode = s.ExitNode;
                NodeFlags = s.NodeFlags;
                NextSequence = s.NextSeq;
            }

            static SeqAnimation[][] GetAnimationBlends(BinaryReader r, M_Seq s, (BinaryReader r, M_SeqHeader h)[] sequences, int zeroGroupOffset, int numBones)
            {
                BinaryReader sr; int so;
                (sr, so) = s.SeqGroup == 0 ? (r, zeroGroupOffset + s.AnimIndex) : (sequences[s.SeqGroup - 1].r, s.AnimIndex);
                sr.Seek(so);
                var anim = sr.ReadS<M_Anim>();
                var blends = new SeqAnimation[s.NumBlends][];
                for (var i = 0; i < s.NumBlends; i++)
                {
                    var animations = new SeqAnimation[numBones];
                    for (var b = 0; b < numBones; b++)
                    {
                        var animation = new SeqAnimation(new M_AnimValue[CoordinateAxes][]);
                        for (var j = 0; j < CoordinateAxes; j++)
                            if (anim.Offsets[j] != 0)
                            {
                                sr.Seek(so + anim.Offsets[j]);
                                var values = new List<M_AnimValue> { sr.ReadS<M_AnimValue>() };
                                if (s.NumFrames > 0)
                                    for (var f = 0; f < s.NumFrames;)
                                    {
                                        var v = values[^1];
                                        f += v.Total;
                                        values.AddRange(sr.ReadSArray<M_AnimValue>(1 + v.Valid));
                                    }
                                animation.Axis[j] = values.ToArray();
                            }
                        animations[b] = animation;
                    }
                    blends[i] = animations;
                }
                return blends;
            }
        }

        // events
        public struct M_Event
        {
            public static (string, int) Struct = ("<3i64s", sizeof(M_Event));
            public int Frame;
            public int Event;
            public int Type;
            public fixed byte Options[64];
        }

        // pivots
        public struct M_Pivot
        {
            public static (string, int) Struct = ("<3f2i", sizeof(M_Pivot));
            public Vector3 Org;         // pivot point
            public int Start, End;
        }

        // attachments
        public struct M_Attachment
        {
            public static (string, int) Struct = ("<32s2i12f", sizeof(M_Attachment));
            public fixed byte Name[32]; // Name of this attachment. Unused in GoldSource.
            public int Type;            // Type of this attachment. Unused in GoldSource;
            public int Bone;            // Index of the bone this is attached to.
            public Vector3 Org;         // Offset from bone origin.
            public Vector3 Vector0, Vector1, Vector2; // Directional vectors? Unused in GoldSource.
        }

        public class Attachment(M_Attachment s, Bone[] bones)
        {
            public string Name = UnsafeX.FixedAString(s.Name, 32);
            public int Type = s.Type;
            public Bone Bone = bones[s.Bone];
            public Vector3 Org = s.Org;
            public Vector3[] Vectors = [s.Vector0, s.Vector1, s.Vector2];
        }

        // animations
        public struct M_Anim
        {
            public static (string, int) Struct = ("<6H", sizeof(M_Anim));
            public fixed ushort Offsets[CoordinateAxes];
        }

        public struct M_AnimValue
        {
            public static (string, int) Struct = ("<2B", sizeof(M_AnimValue));
            public byte Valid;
            public byte Total;
        }

        // body part index
        public struct M_Bodypart
        {
            public static (string, int) Struct = ("<64s3i", sizeof(M_Bodypart));
            public fixed byte Name[64];
            public X_LumpNO2 Models;
        }

        public class Bodypart
        {
            public string Name;
            public int Base;
            public Model[] Models;

            public Bodypart(BinaryReader r, M_Bodypart s, Bone[] bones)
            {
                Name = UnsafeX.FixedAString(s.Name, 64);
                Base = s.Models.Offset;
                r.Seek(s.Models.Offset2); Models = r.ReadSArray<M_Model>(s.Models.Num).Select(x => new Model(r, x, bones)).ToArray();
            }
        }

        // skin info
        public struct M_Texture
        {
            public static (string, int) Struct = ("<64s4i", sizeof(M_Texture));
            public fixed byte Name[64];
            public int Flags;
            public int Width, Height;
            public int Index;
        }

        public class Texture
        {
            public string Name;
            public int Flags;
            public int Width, Height;
            public byte[] Pixels;
            public byte[] Palette;

            public Texture(BinaryReader r, M_Texture s)
            {
                Name = UnsafeX.FixedAString(s.Name, 64);
                Flags = s.Flags;
                Width = s.Width; Height = s.Height;
                r.Seek(s.Index); Pixels = r.ReadBytes(Width * Height);
                Palette = r.ReadBytes(3 * 256);
            }
        }

        // studio models
        public struct M_Model
        {
            public static (string, int) Struct = ("<64sif10i", sizeof(M_Model));
            public fixed byte Name[64];
            public int Type;
            public float BoundingRadius;
            public X_LumpNO Meshs;
            public X_LumpNO2 Verts;     // number of unique vertices, vertex bone info, vertex glm::vec3
            public X_LumpNO2 Norms;     // number of unique surface normals, normal bone info, normal glm::vec3
            public X_LumpNO Groups;     // deformation groups
        }

        public class ModelVertex(Bone bone, Vector3 vertex)
        {
            public Bone Bone = bone;
            public Vector3 Vertex = vertex;
            public static ModelVertex[] Create(BinaryReader r, X_LumpNO2 s, Bone[] bones)
            {
                r.Seek(s.Offset); var boneIds = r.ReadPArray<byte>("B", s.Num);
                r.Seek(s.Offset2); var verts = r.ReadPArray<Vector3>("3f", s.Num);
                return Enumerable.Range(0, s.Num).Select(i => new ModelVertex(bones[boneIds[i]], verts[i])).ToArray();
            }
        }

        public class Model
        {
            public string Name;
            public int Type;
            public float BoundingRadius;
            public Mesh[] Meshes;
            public ModelVertex[] Vertices;
            public ModelVertex[] Normals;

            public Model(BinaryReader r, M_Model s, Bone[] bones)
            {
                Name = UnsafeX.FixedAString(s.Name, 64);
                Type = s.Type;
                BoundingRadius = s.BoundingRadius;
                r.Seek(s.Meshs.Offset); Meshes = r.ReadSArray<M_Mesh>(s.Meshs.Num).Select(x => new Mesh(r, x)).ToArray();
                Vertices = ModelVertex.Create(r, s.Verts, bones);
                Normals = ModelVertex.Create(r, s.Norms, bones);
            }
        }

        // meshes
        public struct M_Mesh
        {
            public static (string, int) Struct = ("<5i", sizeof(M_Mesh));
            public X_LumpNO Tris;
            public int SkinRef;
            public X_LumpNO Norms; // per mesh normals, normal vec3
        }

        public class Mesh
        {
            public short[] Triangles;
            public int NumTriangles;
            public int NumNorms;
            public int SkinRef;

            public Mesh(BinaryReader r, M_Mesh s)
            {
                r.Seek(s.Tris.Offset); Triangles = r.ReadPArray<short>("H", s.Tris.Num); //TODO
                NumTriangles = s.Tris.Num;
                NumNorms = s.Norms.Num;
                SkinRef = s.SkinRef;
            }
        }

        // header
        [StructLayout(LayoutKind.Sequential)]
        public struct M_Header
        {
            public static (string, int) Struct = ("<2i64si15f27i", sizeof(M_Header));
            public int Magic;
            public int Version;
            public fixed byte Name[64];
            public int Length;
            public Vector3 EyePosition;     // ideal eye position
            public Vector3 Min, Max;        // ideal movement hull size
            public Vector3 BBMin, BBMax;    // clipping bounding box
            public HeaderFlags Flags;
            public X_LumpNO Bones;            // bones
            public X_LumpNO BoneControllers; 	// bone controllers
            public X_LumpNO Hitboxs; 		    // complex bounding boxes
            public X_LumpNO Seqs; 		    // animation sequences
            public X_LumpNO SeqGroups; 		// lazy sequences
            public X_LumpNO2 Textures;        // raw textures
            public int NumSkinRef;          // replaceable textures
            public X_LumpNO SkinFamilies;
            public X_LumpNO Bodyparts;
            public X_LumpNO Attachments;      // attachable points
            public X_LumpNO Sounds;           // This seems to be obsolete. Probably replaced by events that reference external sounds?
            public X_LumpNO SoundGroups;      // This seems to be obsolete. Probably replaced by events that reference external sounds?
            public X_LumpNO Transitions;      // animation node to animation node transition graph
        }

        #endregion

        public string Name;
        public bool IsDol;
        public bool IsXashModel;
        public bool HasTextureFile;
        public Vector3 EyePosition;
        public Vector3 BoundingMin, BoundingMax;
        public Vector3 ClippingMin, ClippingMax;
        public HeaderFlags Flags;
        public BoneController[] BoneControllers;
        public Bone[] Bones;
        public BBox[] Hitboxes;
        public SeqGroup[] SequenceGroups;
        public Seq[] Sequences;
        public Attachment[] Attachments;
        public Bodypart[] Bodyparts;
        public byte[][] Transitions;
        public Texture[] Textures;
        public short[][] SkinFamilies;

        public Binary_Mdl10(BinaryReader r, FileSource f, BinaryPakFile s)
        {
            // read file
            var header = r.ReadS<M_Header>();
            if (header.Magic != M_MAGIC) throw new FormatException("BAD MAGIC");
            else if (header.Version != 10) throw new FormatException("BAD VERSION");
            Name = UnsafeX.FixedAString(header.Name, 64);
            if (string.IsNullOrEmpty(Name)) throw new FormatException($"The file '{Name}' is not a model main header file");
            string path = f.Path, pathExt = Path.GetExtension(path), pathName = path[..^pathExt.Length];
            IsDol = pathExt == ".dol";
            if (IsDol) throw new NotImplementedException();
            // Xash models store the offset to the second header in this variable.
            IsXashModel = header.Sounds.Num != 0; // If it's not zero this is a Xash model.
            HasTextureFile = header.Textures.Offset == 0;

            // load texture
            BinaryReader tr; M_Header theader;
            (tr, theader) = HasTextureFile
                ? s.ReaderT(r2 =>
                {
                    if (r2 == null) throw new Exception($"External texture file '{path}' does not exist");
                    var header = r2.ReadS<M_Header>();
                    if (header.Magic != M_MAGIC) throw new FormatException("BAD MAGIC");
                    else if (header.Version != 10) throw new FormatException("BAD VERSION");
                    return Task.FromResult((r2, header));
                }, path = $"{pathName}T{pathExt}", true).Result
                : (r, header);

            // load animations
            (BinaryReader r, M_SeqHeader h)[] sequences;
            if (header.SeqGroups.Num > 1)
            {
                sequences = new (BinaryReader, M_SeqHeader)[header.SeqGroups.Num - 1];
                for (var i = 0; i < sequences.Length; i++)
                    sequences[i] = s.ReaderT(r2 =>
                    {
                        if (r2 == null) throw new Exception($"Sequence group file '{path}' does not exist");
                        var header = r2.ReadS<M_SeqHeader>();
                        if (header.Magic != M_MAGIC2) throw new FormatException("BAD MAGIC");
                        else if (header.Version != 10) throw new FormatException("BAD VERSION");
                        return Task.FromResult((r2, header));
                    }, path = $"{pathName}{i + 1:00}{pathExt}", true).Result;
            }
            else sequences = [];

            // validate
            if (header.Bones.Num < 0
                || header.BoneControllers.Num < 0
                || header.Hitboxs.Num < 0
                || header.Seqs.Num < 0
                || header.SeqGroups.Num < 0
                || header.Bodyparts.Num < 0
                || header.Attachments.Num < 0
                || header.Transitions.Num < 0
                || theader.Textures.Num < 0
                || theader.SkinFamilies.Num < 0
                || theader.NumSkinRef < 0) throw new Exception("Negative data chunk count value");

            // build
            EyePosition = header.EyePosition;
            BoundingMin = header.Min;
            BoundingMax = header.Max;
            ClippingMin = header.BBMin;
            ClippingMax = header.BBMax;
            Flags = header.Flags;
            r.Seek(header.BoneControllers.Offset); BoneControllers = r.ReadSArray<M_BoneController>(header.BoneControllers.Num).Select((x, i) => new BoneController(ref x, i)).ToArray();
            r.Seek(header.Bones.Offset); Bones = r.ReadSArray<M_Bone>(header.Bones.Num).Select((x, i) => new Bone(x, i, BoneControllers)).ToArray(); Bone.Remap(Bones);
            r.Seek(header.Hitboxs.Offset); Hitboxes = r.ReadSArray<M_BBox>(header.Hitboxs.Num).Select(x => new BBox(ref x, Bones)).ToArray();
            r.Seek(header.SeqGroups.Offset); SequenceGroups = r.ReadSArray<M_SeqGroup>(header.SeqGroups.Num).Select(x => new SeqGroup(x)).ToArray();
            var zeroGroupOffset = SequenceGroups.Length > 0 ? SequenceGroups[0].Offset : 0;
            r.Seek(header.Seqs.Offset); Sequences = r.ReadSArray<M_Seq>(header.Seqs.Num).Select(x => new Seq(r, x, sequences, zeroGroupOffset, IsXashModel, Bones)).ToArray();
            r.Seek(header.Attachments.Offset); Attachments = r.ReadSArray<M_Attachment>(header.Attachments.Num).Select(x => new Attachment(x, Bones)).ToArray();
            r.Seek(header.Bodyparts.Offset); Bodyparts = r.ReadSArray<M_Bodypart>(header.Bodyparts.Num).Select(x => new Bodypart(r, x, Bones)).ToArray();
            r.Seek(header.Transitions.Offset); Transitions = r.ReadFArray(x => x.ReadBytes(1), header.Transitions.Num);
            tr.Seek(theader.Textures.Offset); Textures = tr.ReadSArray<M_Texture>(theader.Textures.Num).Select(x => new Texture(tr, x)).ToArray();
            tr.Seek(theader.SkinFamilies.Offset); SkinFamilies = tr.ReadFArray(x => x.ReadPArray<short>("H", theader.NumSkinRef), theader.SkinFamilies.Num);
        }

        #region ITexture

        static readonly object Format = (TextureFormat.RGB24, TexturePixel.Unknown);
        //(TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
        //(TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
        //TextureUnityFormat.RGB24,
        //TextureUnityFormat.RGB24);
        public int Width { get; set; }
        public int Height { get; set; }
        public int Depth => 0;
        public int MipMaps => 1;
        public TextureFlags TexFlags => 0;

        public (byte[] bytes, object format, Range[] spans) Begin(string platform)
        {
            var tex = Textures[0];
            Width = tex.Width; Height = tex.Height;
            var buf = new byte[Width * Height * 3];
            Rasterize.CopyPixelsByPalette(buf, 3, tex.Pixels, tex.Palette, 3);
            return (buf, Format, null);
        }
        public void End() { }

        #endregion

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new("Model", items: [
                new($"Name: {Name}"),
            ]),
        ];
    }

    #endregion

    #region Binary_Mdl40
    //https://developer.valvesoftware.com/wiki/MDL

    public unsafe class Binary_Mdl40 : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Mdl40(r, f, (BinaryPakFile)s));

        #region Headers

        const uint M_MAGIC = 0x54534449; //: IDST
        //const uint M_MAGIC2 = 0x51534449; //: IDSQ

        /// <summary>
        /// header flags
        /// </summary>
        [Flags]
        public enum HeaderFlags : int
        {
            AUTOGENERATED_HITBOX = 0x00000001,          // This flag is set if no hitbox information was specified
            USES_ENV_CUBEMAP = 0x00000002,              // This flag is set at loadtime, not mdl build time so that we don't have to rebuild models when we change materials.
            FORCE_OPAQUE = 0x00000004,                  // Use this when there are translucent parts to the model but we're not going to sort it
            TRANSLUCENT_TWOPASS = 0x00000008,           // Use this when we want to render the opaque parts during the opaque pass and the translucent parts during the translucent pass
            STATIC_PROP = 0x00000010,                   // This is set any time the .qc files has $staticprop in it
            USES_FB_TEXTURE = 0x00000020,               // This flag is set at loadtime, not mdl build time so that we don't have to rebuild models when we change materials.
            HASSHADOWLOD = 0x00000040,                  // This flag is set by studiomdl.exe if a separate "$shadowlod" entry was present for the .mdl (the shadow lod is the last entry in the lod list if present)
            USES_BUMPMAPPING = 0x00000080,              // This flag is set at loadtime, not mdl build time so that we don't have to rebuild models when we change materials.	S
            USE_SHADOWLOD_MATERIALS = 0x00000100,       // This flag is set when we should use the actual materials on the shadow LOD instead of overriding them with the default one (necessary for translucent shadows)
            OBSOLETE = 0x00000200,                      // This flag is set when we should use the actual materials on the shadow LOD instead of overriding them with the default one (necessary for translucent shadows)
            UNUSED = 0x00000400,	                    // N/A
            NO_FORCED_FADE = 0x00000800,	            // This flag is set at mdl build time
            FORCE_PHONEME_CROSSFADE = 0x00001000,	    // The npc will lengthen the viseme check to always include two phonemes
            CONSTANT_DIRECTIONAL_LIGHT_DOT = 0x00002000, // This flag is set when the .qc has $constantdirectionallight in it
            FLEXES_CONVERTED = 0x00004000,	            // Flag to mark delta flexes as already converted from disk format to memory format
            BUILT_IN_PREVIEW_MODE = 0x00008000,	        // Indicates the studiomdl was built in preview mode
            AMBIENT_BOOST = 0x00010000,	                // Ambient boost (runtime flag)
            DO_NOT_CAST_SHADOWS = 0x00010000,	        // Forces the model to be lit by skybox lighting
            CAST_TEXTURE_SHADOWS = 0x00010000,	        // Don't cast shadows from this model (useful on first-person models)
            NA_1 = 0x00080000,	                        // N/A (undefined)
            NA_2 = 0x00100000,	                        // N/A (undefined)
            VERT_ANIM_FIXED_POINT_SCALE = 0x00200000,   // flagged on load to indicate no animation events on this model
        }

        public struct M_Texture
        {
            public static (string, int) Struct = ("<6i10s", sizeof(M_Texture));
            public int NameOffset;
            public int Flags;
            public int Used;
            public int Unused;
            public int Material;        // (Placeholder)
            public int ClientMaterial;  // (Placeholder)
            public fixed byte Unused2[10];
        }

        // header
        [StructLayout(LayoutKind.Sequential)]
        public struct M_Header
        {
            public static (string, int) Struct = ("<3i64si18f44if11i4B3if3i", sizeof(M_Header));
            public int Magic;
            public int Version;
            public int Checksum;
            public fixed byte Name[64];
            public int Length;
            public Vector3 EyePosition;     // ideal eye position
            public Vector3 IllumPosition;   // ideal illum position
            public Vector3 Min, Max;        // ideal movement hull size
            public Vector3 BBMin, BBMax;    // clipping bounding box
            public HeaderFlags Flags;
            public X_LumpNO Bones;            // bones
            public X_LumpNO BoneControllers; 	// bone controllers
            public X_LumpNO Hitboxs; 		    // bounding boxes
            public X_LumpNO LocalAnims; 		// local animations
            public X_LumpNO LocalSeqs; 		// local sequences
            public X_LumpNO Events;           // events
            public X_LumpNO Textures;         // textures
            public X_LumpNO TexturesDirs;     // textures directories
            public X_LumpNO2 SkinFamilies;    // skin-families
            public X_LumpNO Bodyparts;        // bodyparts
            public X_LumpNO Attachments;      // attachable points
            public X_LumpNO2 LocalNodes;      // local nodes
            public X_LumpNO Flexs;            // flexs
            public X_LumpNO FlexControllers;  // flex controllers
            public X_LumpNO FlexRules;        // flex rules
            public X_LumpNO IKChains;         // ik chains
            public X_LumpNO Mouths;           // Mouths
            public X_LumpNO LocalPoseParams;  // local pose params
            public int SurfacePropIndex;    // surface properties
            public X_LumpNO KeyValues;        // key values
            public X_LumpNO IKLocks;          // ik locks
            public float Mass;              // mass
            public int Contents;            // contents
            public X_LumpNO IncludeModel;     // include model
            public int VirtualModel;        // virtual model (Placeholder)
            public int AnimBlockNameIndex;  // anim block name
            public X_LumpNO AnimBlocks;       // anim blocks
            public int AnimBlockModel;      // anim block model (Placeholder)
            public int BoneNameIndex;       // bone name index
            public int VertexBase;          // (Placeholder)
            public int OffsetBase;          // (Placeholder)
            public byte DirectionalDotProduct;
            public byte RootLod;            // preferred rather than clamped
            public byte NumAllowedRootLods; // 0 means any allowed, N means Lod 0 -> (N-1)
            public byte Unused0;
            public int Unused1;
            public X_LumpNO FlexControllerUI; // flex controller ui
            public float VertAnimFixedPointScale;
            public int Unused2;
            public int Header2Index;
            public int Unused3;
        }

        // header
        [StructLayout(LayoutKind.Sequential)]
        public struct M_Header2
        {
            public static (string, int) Struct = ("<3ifi64s", sizeof(M_Header2));
            public X_LumpNO SrcBoneTransform;
            public int IllumPositionAttachmentIndex;
            public float MaxEyeDeflection;    // if set to 0, then equivalent to cos(30)
            public int LinearBoneIndex;
            public fixed byte Unknown[64];
        }

        #endregion

        public string Name;
        public Vector3 EyePosition;
        public Vector3 IllumPosition;
        public Vector3 BoundingMin, BoundingMax;
        public Vector3 ClippingMin, ClippingMax;
        public HeaderFlags Flags;

        public Binary_Mdl40(BinaryReader r, FileSource f, BinaryPakFile s)
        {
            // read file
            var header = r.ReadS<M_Header>();
            if (header.Magic != M_MAGIC) throw new FormatException("BAD MAGIC");
            else if (header.Version < 40) throw new FormatException("BAD VERSION");
            Name = UnsafeX.FixedAString(header.Name, 64);
            if (string.IsNullOrEmpty(Name)) throw new FormatException($"The file '{Name}' is not a model main header file");
            string path = f.Path, pathExt = Path.GetExtension(path), pathName = path[..^pathExt.Length];

            //// load texture
            //BinaryReader tr; M_Header theader;
            //(tr, theader) = HasTextureFile
            //    ? s.ReaderT(r2 =>
            //    {
            //        if (r2 == null) throw new Exception($"External texture file '{path}' does not exist");
            //        var header = r2.ReadS<M_Header>();
            //        if (header.Magic != M_MAGIC) throw new FormatException("BAD MAGIC");
            //        else if (header.Version != 10) throw new FormatException("BAD VERSION");
            //        return Task.FromResult((r2, header));
            //    }, path = $"{pathName}T{pathExt}", true).Result
            //    : (r, header);

            //// load animations
            //(BinaryReader r, M_SeqHeader h)[] sequences;
            //if (header.SeqGroups.Num > 1)
            //{
            //    sequences = new (BinaryReader, M_SeqHeader)[header.SeqGroups.Num - 1];
            //    for (var i = 0; i < sequences.Length; i++)
            //        sequences[i] = s.ReaderT(r2 =>
            //        {
            //            if (r2 == null) throw new Exception($"Sequence group file '{path}' does not exist");
            //            var header = r2.ReadS<M_SeqHeader>();
            //            if (header.Magic != M_MAGIC2) throw new FormatException("BAD MAGIC");
            //            else if (header.Version != 10) throw new FormatException("BAD VERSION");
            //            return Task.FromResult((r2, header));
            //        }, path = $"{pathName}{i + 1:00}{pathExt}", true).Result;
            //}
            //else sequences = [];

            //// validate
            //if (header.Bones.Num < 0
            //    || header.BoneControllers.Num < 0
            //    || header.Hitboxs.Num < 0
            //    || header.Seqs.Num < 0
            //    || header.SeqGroups.Num < 0
            //    || header.Bodyparts.Num < 0
            //    || header.Attachments.Num < 0
            //    || header.Transitions.Num < 0
            //    || theader.Textures.Num < 0
            //    || theader.SkinFamilies.Num < 0
            //    || theader.NumSkinRef < 0) throw new Exception("Negative data chunk count value");

            // build
            EyePosition = header.EyePosition;
            IllumPosition = header.IllumPosition;
            BoundingMin = header.Min;
            BoundingMax = header.Max;
            ClippingMin = header.BBMin;
            ClippingMax = header.BBMax;
            Flags = header.Flags;
            //r.Seek(header.BoneControllers.Offset); BoneControllers = r.ReadSArray<M_BoneController>(header.BoneControllers.Num).Select((x, i) => new BoneController(ref x, i)).ToArray();
            //r.Seek(header.Bones.Offset); Bones = r.ReadSArray<M_Bone>(header.Bones.Num).Select((x, i) => new Bone(x, i, BoneControllers)).ToArray(); Bone.Remap(Bones);
            //r.Seek(header.Hitboxs.Offset); Hitboxes = r.ReadSArray<M_BBox>(header.Hitboxs.Num).Select(x => new BBox(ref x, Bones)).ToArray();
            //r.Seek(header.SeqGroups.Offset); SequenceGroups = r.ReadSArray<M_SeqGroup>(header.SeqGroups.Num).Select(x => new SeqGroup(x)).ToArray();
            //var zeroGroupOffset = SequenceGroups.Length > 0 ? SequenceGroups[0].Offset : 0;
            //r.Seek(header.Seqs.Offset); Sequences = r.ReadSArray<M_Seq>(header.Seqs.Num).Select(x => new Seq(r, x, sequences, zeroGroupOffset, IsXashModel, Bones)).ToArray();
            //r.Seek(header.Attachments.Offset); Attachments = r.ReadSArray<M_Attachment>(header.Attachments.Num).Select(x => new Attachment(x, Bones)).ToArray();
            //r.Seek(header.Bodyparts.Offset); Bodyparts = r.ReadSArray<M_Bodypart>(header.Bodyparts.Num).Select(x => new Bodypart(r, x, Bones)).ToArray();
            //r.Seek(header.Transitions.Offset); Transitions = r.ReadFArray(x => x.ReadBytes(1), header.Transitions.Num);
            //tr.Seek(theader.Textures.Offset); Textures = tr.ReadSArray<M_Texture>(theader.Textures.Num).Select(x => new Texture(tr, x)).ToArray();
            //tr.Seek(theader.SkinFamilies.Offset); SkinFamilies = tr.ReadFArray(x => x.ReadPArray<short>("H", theader.NumSkinRef), theader.SkinFamilies.Num);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = this }),
            new("Model", items: [
                new($"Name: {Name}"),
            ]),
        ];
    }

    #endregion

    #region Binary_Wad3
    // https://github.com/dreamstalker/rehlds/blob/master/rehlds/engine/model.cpp
    // https://greg-kennedy.com/hl_materials/
    // https://github.com/tmp64/BSPRenderer

    public unsafe class Binary_Wad3 : ITexture, IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Wad3(r, f));

        #region Headers

        [StructLayout(LayoutKind.Sequential)]
        struct CharInfo
        {
            public static (string, int) Struct = ("<2H", sizeof(CharInfo));
            public ushort StartOffset;
            public ushort CharWidth;
        }

        enum Formats : byte
        {
            None = 0,
            Tex2 = 0x40,
            Pic = 0x42,
            Tex = 0x43,
            Fnt = 0x46
        }

        #endregion

        public Binary_Wad3(BinaryReader r, FileSource f)
        {
            var type = Path.GetExtension(f.Path) switch
            {
                ".pic" => Formats.Pic,
                ".tex" => Formats.Tex,
                ".tex2" => Formats.Tex2,
                ".fnt" => Formats.Fnt,
                _ => Formats.None
            };
            transparent = Path.GetFileName(f.Path).StartsWith('{');
            Format = transparent
                ? (type, (TextureFormat.RGBA32, TexturePixel.Unknown))
                : (type, (TextureFormat.RGB24, TexturePixel.Unknown));
            //? (type, (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), TextureUnityFormat.RGBA32, TextureUnityFormat.RGBA32)
            //: (type, (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), TextureUnityFormat.RGB24, TextureUnityFormat.RGB24);
            if (type == Formats.Tex2 || type == Formats.Tex) name = r.ReadFUString(16);
            width = (int)r.ReadUInt32();
            height = (int)r.ReadUInt32();

            // validate
            if (width > 0x1000 || height > 0x1000) throw new FormatException("Texture width or height exceeds maximum size!");
            else if (width == 0 || height == 0) throw new FormatException("Texture width and height must be larger than 0!");

            // read pixel offsets
            if (type == Formats.Tex2 || type == Formats.Tex)
            {
                uint[] offsets = [r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32(), r.ReadUInt32()];
                if (r.BaseStream.Position != offsets[0]) throw new Exception("BAD OFFSET");
            }
            else if (type == Formats.Fnt)
            {
                width = 0x100;
                var rowCount = r.ReadUInt32();
                var rowHeight = r.ReadUInt32();
                var charInfos = r.ReadSArray<CharInfo>(0x100);
            }

            // read pixels
            var pixelSize = width * height;
            pixels = type == Formats.Tex2 || type == Formats.Tex
                ? [r.ReadBytes(pixelSize), r.ReadBytes(pixelSize >> 2), r.ReadBytes(pixelSize >> 4), r.ReadBytes(pixelSize >> 6)]
                : [r.ReadBytes(pixelSize)];

            // read pallet
            r.Skip(2);
            var p = palette = r.ReadBytes(0x100 * 3);
            if (type == Formats.Tex2) //e.g.: tempdecal.wad
                for (int i = 0, j = 0; i < 0x100; i++, j += 3)
                {
                    p[j + 0] = (byte)i;
                    p[j + 1] = (byte)i;
                    p[j + 2] = (byte)i;
                }

            //if (type == Formats.Pic) r.Skip(2);
            //r.EnsureComplete();
        }

        bool transparent;
        string name;
        int width;
        int height;
        byte[][] pixels;
        byte[] palette;

        #region ITexture

        (Formats type, object value) Format;
        public int Width => width;
        public int Height => height;
        public int Depth => 0;
        public int MipMaps => pixels.Length;
        public TextureFlags TexFlags => 0;

        public (byte[] bytes, object format, Range[] spans) Begin(string platform)
        {
            var bbp = transparent ? 4 : 3;
            var buf = new byte[pixels.Sum(x => x.Length) * bbp];
            var spans = new Range[pixels.Length];
            int size;
            for (int index = 0, offset = 0; index < pixels.Length; index++, offset += size)
            {
                var p = pixels[index];
                size = p.Length * bbp; var span = spans[index] = new Range(offset, offset + size);
                if (transparent) Rasterize.CopyPixelsByPaletteWithAlpha(buf.AsSpan(span), bbp, p, palette, 3, 0xFF);
                else Rasterize.CopyPixelsByPalette(buf.AsSpan(span), bbp, p, palette, 3);
            }
            return (buf, Format.value, spans);
        }
        public void End() { }

        #endregion

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new("Texture", items: [
                new($"Name: {name}"),
                new($"Format: {Format.type}"),
                new($"Width: {Width}"),
                new($"Height: {Height}"),
                new($"Mipmaps: {MipMaps}"),
            ]),
        ];
    }

    #endregion
}

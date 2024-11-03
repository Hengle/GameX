using GameX.Platforms;
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

    #region Binary_Spr
    // https://github.com/yuraj11/HL-Texture-Tools

    public unsafe class Binary_Spr : ITextureFrames, IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Spr(r, f));

        (object gl, object vulken, object unity, object unreal) Format;
        public int Width => width;
        public int Height => height;
        public int Depth => 0;
        public int MipMaps => 1;
        public TextureFlags Flags => 0;
        public int Fps { get; } = 60;

        #region Headers

        const uint SPR_MAGIC = 0x50534449; //: IDSP

        /// <summary>
        /// Type of sprite.
        /// </summary>
        public enum SprType : int
        {
            VP_PARALLEL_UPRIGHT,
            FACING_UPRIGHT,
            VP_PARALLEL,
            ORIENTED,
            VP_PARALLEL_ORIENTED
        }

        /// <summary>
        /// Texture format of sprite.
        /// </summary>
        public enum SprTextFormat : int
        {
            SPR_NORMAL,
            SPR_ADDITIVE,
            SPR_INDEXALPHA,
            SPR_ALPHTEST
        }

        /// <summary>
        /// Synch. type of sprite.
        /// </summary>
        public enum SprSynchType : int
        {
            Synchronized,
            Random
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SPR_Header
        {
            public static (string, int) Struct = ("<I3if3ifi", sizeof(SPR_Header));
            public uint Magic;
            public int Version;
            public SprType Type;
            public SprTextFormat TextFormat;
            public float BoundingRadius;
            public int MaxWidth;
            public int MaxHeight;
            public int NumFrames;
            public float BeamLen;
            public SprSynchType SynchType;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SPR_Frame
        {
            public static (string, int) Struct = ("<5i", sizeof(SPR_Frame));
            public int Group;
            public int OriginX;
            public int OriginY;
            public int Width;
            public int Height;
        }

        #endregion

        int width;
        int height;
        SPR_Frame[] frames;
        byte[][] pixels;
        byte[] palette;
        int frame;
        byte[] bytes;

        public Binary_Spr(BinaryReader r, FileSource f)
        {
            Format = (
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGBA32,
                TextureUnityFormat.RGBA32);

            // read file
            var header = r.ReadS<SPR_Header>();
            if (header.Magic != SPR_MAGIC) throw new FormatException("BAD MAGIC");

            // load palette
            palette = r.ReadBytes(r.ReadUInt16() * 3);

            // load frames
            frames = new SPR_Frame[header.NumFrames];
            pixels = new byte[header.NumFrames][];
            for (var i = 0; i < header.NumFrames; i++)
            {
                frames[i] = r.ReadS<SPR_Frame>();
                ref SPR_Frame frame = ref frames[i];
                pixels[i] = r.ReadBytes(frame.Width * frame.Height);
            }
            width = frames[0].Width;
            height = frames[0].Height;
            bytes = new byte[width * height << 2];
        }

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
        {
            return (bytes, (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.StereoKit => throw new NotImplementedException("StereoKit"),
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, null);
        }
        public void End() { }

        public bool HasFrames => frame < frames.Length;

        public bool DecodeFrame()
        {
            var p = pixels[frame];
            Rasterize.CopyPixelsByPalette(bytes, 4, p, palette, 3);
            frame++;
            return true;
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "VideoTexture", Name = Path.GetFileName(file.Path), Value = this }),
            new("Sprite", items: [
                new($"Frames: {frames.Length}"),
                new($"Width: {Width}"),
                new($"Height: {Height}"),
                new($"Mipmaps: {MipMaps}"),
            ]),
        ];
    }

    #endregion

    #region Binary_Mdl

    public unsafe class Binary_Mdl : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Mdl(r, f, (BinaryPakFile)s));

        #region Headers

        const uint M_MAGIC = 0x54534449; //: IDST
        const uint M_MAGIC2 = 0x51534449; //: IDSQ
        public const int CoordinateAxes = 6;
        public const int SequenceBlendCount = 2;
        public const int ControllerCount = 4;
        //MAXCONTROLLERS = 4,
        //MOUTHCONTROLLER = 4,
        //MAXBLENDERS = 2

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

        // lumps
        public struct M_Lump { public int Num; public int Offset; }
        public struct M_Lump2 { public int Num; public int Offset; public int Offset2; }

        // sequence header
        public struct M_SeqHeader
        {
            public static (string, int) Struct = ("<2I64sI", sizeof(M_SeqHeader));
            public int Magic;
            public int Version;
            public fixed byte Name[64];
            public int Length;
        }

        // bones
        public struct M_Bone
        {
            public static (string, int) Struct = ("<?", sizeof(M_Bone));
            public fixed char Name[32]; // bone name for symbolic links
            public int Parent; // parent bone
            public BoneFlags Flags;
            public fixed int BoneController[CoordinateAxes]; // bone controller index, -1 == none
            public fixed float Value[CoordinateAxes];    // default DoF values
            public fixed float Scale[CoordinateAxes];   // scale for delta DoF values
        }

        // bone controllers
        public struct M_BoneController
        {
            public static (string, int) Struct = ("<?", sizeof(M_BoneController));
            public int Bone;   // -1 == 0
            public int Type;   // X, Y, Z, XR, YR, ZR, M
            public float Start, End;
            public int Rest;   // byte index value at rest
            public int Index;  // 0-3 user set controller, 4 mouth
        }

        // intersection boxes
        public struct M_BBox
        {
            public static (string, int) Struct = ("<?", sizeof(M_BBox));
            public int Bone;
            public int Group;          // intersection group
            public Vector3 BBMin, BBMax;        // bounding box
        }

        // sequence groups
        public struct M_SeqGroup
        {
            public static (string, int) Struct = ("<?", sizeof(M_SeqGroup));
            public fixed byte Label[32]; // textual name
            public fixed byte Name[64];  // file name
            public int Unused1;    // was "cache"  - index pointer
            public int Unused2;    // was "data" -  hack for group 0
        }

        // sequence descriptions
        public struct M_SeqDesc
        {
            public static (string, int) Struct = ("<?", sizeof(M_SeqDesc));
            public fixed byte Label[32]; // sequence label

            public float Fps;      // frames per second	
            public int Flags;      // looping/non-looping flags

            public int Activity;
            public int ActWeight;

            public M_Lump Events;
            public int NumFrames;  // number of frames per sequence
            public M_Lump Pivots;  // number of foot pivots

            public int MotionType;
            public int MotionBone;
            public Vector3 LinearMovement;
            public int AutomovePosIndex;
            public int AutomoveAngleIndex;

            public Vector3 BBMin, BBMax;        // per sequence bounding box

            public int NumBlends;
            public int AnimIndex;      // mstudioanim_t pointer relative to start of sequence group data: [blend][bone][X, Y, Z, XR, YR, ZR]

            public fixed int BlendType[SequenceBlendCount];  // X, Y, Z, XR, YR, ZR
            public fixed float BlendStart[SequenceBlendCount];   // starting value
            public fixed float BlendEnd[SequenceBlendCount]; // ending value
            public int BlendParent;

            public int SeqGroup;       // sequence group for demand loading

            public int EntryNode;      // transition node at entry
            public int ExitNode;       // transition node at exit
            public int NodeFlags;      // transition rules

            public int NextSeq;        // auto advancing sequences
        }

        // events
        public struct M_Event
        {
            public static (string, int) Struct = ("<?", sizeof(M_Event));
            public int Frame;
            public int Event;
            public int Type;
            public fixed byte Options[64];
        }

        // pivots
        public struct M_Pivot
        {
            public static (string, int) Struct = ("<?", sizeof(M_Pivot));
            public Vector3 Org;  // pivot point
            public int Start, End;
        }

        // attachments
        public struct M_Attachment
        {
            public static (string, int) Struct = ("<?", sizeof(M_Attachment));
            public fixed byte Name[32]; // Name of this attachment. Unused in GoldSource.
            public int Type; // Type of this attachment. Unused in GoldSource;
            public int Bone; // Index of the bone this is attached to.
            public Vector3 Org; // Offset from bone origin.
            public fixed float Vectors[3 * 3]; // Directional vectors? Unused in GoldSource.
        }

        // animations
        public struct M_Anim
        {
            public static (string, int) Struct = ("<?", sizeof(M_Anim));
            public fixed ushort Offset[CoordinateAxes];
        }

        // body part index
        public struct M_Bodypart
        {
            public static (string, int) Struct = ("<?", sizeof(M_Bodypart));
            public fixed byte Name[64];
            public int NumModels;
            public int Base;
            public int ModelIndex; // index into models array
        }

        // skin info
        public struct M_Texture
        {
            public static (string, int) Struct = ("<?", sizeof(M_Texture));
            public fixed byte Name[64];
            public int Flags;
            public int Width, Height;
            public int Index;
        }

        // studio models
        public struct M_Model
        {
            public static (string, int) Struct = ("<?", sizeof(M_Model));
            public fixed byte Name[64];
            public int Type;
            public float BoundingRadius;
            public M_Lump Meshs;
            public M_Lump2 Verts;       // number of unique vertices, vertex bone info, vertex glm::vec3
            public M_Lump2 Norms;       // number of unique surface normals, normal bone info, normal glm::vec3
            public M_Lump Groups;      // deformation groups
        }

        // meshes
        public struct M_Mesh
        {
            public static (string, int) Struct = ("<?", sizeof(M_Mesh));
            public M_Lump Tris;
            public int SkinRef;
            public M_Lump Norms;       // per mesh normals, normal glm::vec3
        }

        // header
        [StructLayout(LayoutKind.Sequential)]
        public struct M_Header
        {
            public static (string, int) Struct = ("<2I64sI15f27I", sizeof(M_Header));
            public int Magic;
            public int Version;
            public fixed byte Name[64];
            public int Length;
            public Vector3 EyePosition;     // ideal eye position
            public Vector3 Min, Max;        // ideal movement hull size
            public Vector3 BBMin, BBMax;    // clipping bounding box
            public HeaderFlags Flags;
            public M_Lump Bones;            // bones
            public M_Lump BoneControllers; 	// bone controllers
            public M_Lump Hitboxs; 		    // complex bounding boxes
            public M_Lump Seqs; 		    // animation sequences
            public M_Lump SeqGroups; 		// lazy sequences
            public M_Lump2 Textures;        // raw textures
            public int NumSkinRef;          // replaceable textures
            public M_Lump Skins;
            public M_Lump BodyParts;
            public M_Lump Attachments;      // attachable points
            public M_Lump Sounds;           // This seems to be obsolete. Probably replaced by events that reference external sounds?
            public M_Lump SoundGroups;      // This seems to be obsolete. Probably replaced by events that reference external sounds?
            public M_Lump Transitions;      // animation node to animation node transition graph
        }

        #endregion

        public M_Header Header;
        public M_Header Texture;
        public M_SeqHeader[] Sequences;
        string HeaderName;
        public bool IsDol;

        public Binary_Mdl(BinaryReader r, FileSource f, BinaryPakFile s)
        {
            // read file
            var header = Header = r.ReadS<M_Header>();
            if (header.Magic != M_MAGIC) throw new FormatException("BAD MAGIC");
            else if (header.Version != 10) throw new FormatException("BAD VERSION");
            HeaderName = UnsafeX.FixedAString(header.Name, 64);
            if (string.IsNullOrEmpty(HeaderName)) throw new FormatException($"The file '{HeaderName}' is not a model main header file");
            string pathExt = Path.GetExtension(f.Path), pathName = f.Path[..^pathExt.Length];
            IsDol = pathExt == ".dol";

            // load texture
            if (header.Textures.Offset == 0)
            {
                var path = $"{pathName}T{pathExt}";
                Texture = s.Reader(r2 =>
                {
                    if (r2 == null) throw new Exception($"External texture file '{path}' does not exist");
                    var header = r2.ReadS<M_Header>();
                    if (header.Magic != M_MAGIC) throw new FormatException("BAD MAGIC");
                    else if (header.Version != 10) throw new FormatException("BAD VERSION");
                    return Task.FromResult(header);
                }, path).Result;
            }

            // load animations
            if (header.SeqGroups.Num > 1)
            {
                Sequences = new M_SeqHeader[header.SeqGroups.Num - 1];
                for (var i = 0; i < Sequences.Length; i++)
                {
                    var path = $"{pathName}{i + 1:00}{pathExt}";
                    Sequences[i] = s.Reader(r2 =>
                    {
                        if (r2 == null) throw new Exception($"Sequence group file '{path}' does not exist");
                        var header = r2.ReadS<M_SeqHeader>();
                        if (header.Magic != M_MAGIC2) throw new FormatException("BAD MAGIC");
                        else if (header.Version != 10) throw new FormatException("BAD VERSION");
                        return Task.FromResult(header);
                    }, path).Result;
                }
            }
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = this }),
            new("Model", items: [
                new($"Name: {HeaderName}"),
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
                ? (type, (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), TextureUnityFormat.RGBA32, TextureUnityFormat.RGBA32)
                : (type, (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), TextureUnityFormat.RGB24, TextureUnityFormat.RGB24);
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

        (Formats type, object gl, object vulken, object unity, object unreal) Format;

        public int Width => width;
        public int Height => height;
        public int Depth => 0;
        public int MipMaps => pixels.Length;
        public TextureFlags Flags => 0;

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
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
            return (buf, (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.StereoKit => throw new NotImplementedException("StereoKit"),
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, spans);
        }
        public void End() { }

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

using GameX.Meta;
using GameX.Platforms;
using GameX.Valve.Formats.Vpk;
using OpenStack.Gfx;
using OpenStack.Gfx.Renders;
using OpenStack.Gfx.Textures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static GameX.Valve.Formats.Vpk.D_Texture;

namespace GameX.Valve.Formats
{
    #region Binary_Bsp
    // https://hlbsp.sourceforge.net/index.php?content=bspdef
    // https://github.com/bernhardmgruber/hlbsp/tree/master/src

    public unsafe class Binary_Bsp : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Bsp(r, f));

        #region Headers

        struct BSP_Lump
        {
            public int Offset;
            public int Length;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct BSP_Header
        {
            public static (string, int) Struct = ("<31i", sizeof(BSP_Header));
            public int Version;
            public BSP_Lump Entities;
            public BSP_Lump Planes;
            public BSP_Lump Textures;
            public BSP_Lump Vertices;
            public BSP_Lump Visibility;
            public BSP_Lump Nodes;
            public BSP_Lump TexInfo;
            public BSP_Lump Faces;
            public BSP_Lump Lighting;
            public BSP_Lump ClipNodes;
            public BSP_Lump Leaves;
            public BSP_Lump MarkSurfaces;
            public BSP_Lump Edges;
            public BSP_Lump SurfEdges;
            public BSP_Lump Models;
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

        const int MAX_MAP_HULLS = 4;

        const int MAX_MAP_MODELS = 400;
        const int MAX_MAP_BRUSHES = 4096;
        const int MAX_MAP_ENTITIES = 1024;
        const int MAX_MAP_ENTSTRING = (128 * 1024);

        const int MAX_MAP_PLANES = 32767;
        const int MAX_MAP_NODES = 32767;
        const int MAX_MAP_CLIPNODES = 32767;
        const int MAX_MAP_LEAFS = 8192;
        const int MAX_MAP_VERTS = 65535;
        const int MAX_MAP_FACES = 65535;
        const int MAX_MAP_MARKSURFACES = 65535;
        const int MAX_MAP_TEXINFO = 8192;
        const int MAX_MAP_EDGES = 256000;
        const int MAX_MAP_SURFEDGES = 512000;
        const int MAX_MAP_TEXTURES = 512;
        const int MAX_MAP_MIPTEX = 0x200000;
        const int MAX_MAP_LIGHTING = 0x200000;
        const int MAX_MAP_VISIBILITY = 0x200000;

        const int MAX_MAP_PORTALS = 65536;

        #endregion

        public Binary_Bsp(BinaryReader r, FileSource f)
        {
            // read file
            var header = r.ReadS<BSP_Header>();
            if (header.Version != 30) throw new FormatException("BAD VERSION");
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            //new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new("Bsp", items: [
                //new($"Width: {Width}"),
                //new($"Height: {Height}"),
                //new($"Mipmaps: {MipMaps}"),
            ]),
        ];
    }

    #endregion

    #region Binary_Pak
    //was:Resource/Resource

    public class Binary_Pak : IDisposable, IHaveMetaInfo, IRedirected<ITexture>, IRedirected<IMaterial>, IRedirected<IMesh>, IRedirected<IModel>, IRedirected<IParticleSystem>
    {
        internal const ushort KnownHeaderVersion = 12;

        public Binary_Pak() { }
        public Binary_Pak(BinaryReader r) => Read(r);

        public void Dispose()
        {
            Reader?.Dispose();
            Reader = null;
            GC.SuppressFinalize(this);
        }

        ITexture IRedirected<ITexture>.Value => DATA as ITexture;
        IMaterial IRedirected<IMaterial>.Value => DATA as IMaterial;
        IMesh IRedirected<IMesh>.Value => DataType == ResourceType.Mesh ? new D_Mesh(this) as IMesh : null;
        IModel IRedirected<IModel>.Value => DATA as IModel;
        IParticleSystem IRedirected<IParticleSystem>.Value => DATA as IParticleSystem;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
        {
            var nodes = new List<MetaInfo> {
                new("BinaryPak", items: new List<MetaInfo> {
                    new($"FileSize: {FileSize}"),
                    new($"Version: {Version}"),
                    new($"Blocks: {Blocks.Count}"),
                    new($"DataType: {DataType}"),
                })
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
    }

    #endregion

    #region Binary_Spr
    // https://github.com/yuraj11/HL-Texture-Tools

    public unsafe class Binary_Spr : ITexture, IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Spr(r, f));

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
            public uint Signature;
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

        //[StructLayout(LayoutKind.Sequential)]
        //struct WAD_Lump
        //{
        //    public const int SizeOf = 32;
        //    public uint Offset;
        //    public uint DiskSize;
        //    public uint Size;
        //    public byte Type;
        //    public byte Compression;
        //    public ushort Padding;
        //    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)] public string Name;
        //}

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

        public Binary_Spr(BinaryReader r, FileSource f)
        {
            Format = ((TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), TextureUnityFormat.RGBA32, TextureUnityFormat.RGBA32);

            // read file
            var header = r.ReadS<SPR_Header>();
            if (header.Signature != SPR_MAGIC) throw new FormatException("BAD MAGIC");

            // load palette
            palette = r.ReadBytes(r.ReadUInt16() * 3);

            // load frames
            frames = new SPR_Frame[header.NumFrames];
            pixels = new byte[header.NumFrames][];
            for (var i = 0; i < header.NumFrames; i++)
            {
                frames[i] = r.ReadS<SPR_Frame>();
                ref SPR_Frame frame = ref frames[i];
                var pixelSize = frame.Width * frame.Height;
                pixels[i] = r.ReadBytes(pixelSize);
            }
            width = frames[0].Width;
            height = frames[0].Height;
        }

        int width;
        int height;
        SPR_Frame[] frames;
        byte[][] pixels;
        byte[] palette;

        (object gl, object vulken, object unity, object unreal) Format;

        public int Width => width;
        public int Height => height;
        public int Depth => 0;
        public int MipMaps => pixels.Length;
        public TextureFlags Flags => 0;

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
        {
            static void FlattenPalette(Span<byte> data, byte[] source, byte[] palette)
            {
                fixed (byte* _ = data)
                    for (int i = 0, pi = 0; i < source.Length; i++, pi += 4)
                    {
                        var pa = source[i] * 3;
                        //if (pa + 3 > palette.Length) continue;
                        _[pi + 0] = palette[pa + 0];
                        _[pi + 1] = palette[pa + 1];
                        _[pi + 2] = palette[pa + 2];
                        _[pi + 3] = 0xFF;
                    }
            }

            var bytes = new byte[pixels.Sum(x => x.Length) * 4];
            var spans = new Range[pixels.Length];
            byte[] p;
            for (int index = 0, offset = 0; index < pixels.Length; index++, offset += p.Length * 4)
            {
                p = pixels[index];
                var span = spans[index] = new Range(offset, offset + p.Length * 4);
                FlattenPalette(bytes.AsSpan(span), p, palette);
            }
            return (bytes, (Platform.Type)platform switch
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
                new($"Width: {Width}"),
                new($"Height: {Height}"),
                new($"Mipmaps: {MipMaps}"),
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
                ? [r.ReadBytes(pixelSize), r.ReadBytes(pixelSize >> 2), r.ReadBytes(pixelSize >> 4), r.ReadBytes(pixelSize >> 8)]
                : [r.ReadBytes(pixelSize)];

            // read pallet
            r.Skip(2);
            palette = r.ReadBytes(0x100 * 3);

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
                Rasterize.CopyPixelsByPalette(buf.AsSpan(span), bbp, p, palette);
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

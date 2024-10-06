using GameX.Bethesda.Formats.Records;
using GameX.Formats;
using OpenStack.Gfx.Textures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Bethesda.Formats
{
    #region PakBinary_Ba2

    // http://en.uesp.net/wiki/Bethesda5Mod:Archive_File_Format

    public unsafe class PakBinary_Ba2 : PakBinary<PakBinary_Ba2>
    {
        // Header : TES5
        #region Header : TES5

        // Default header data
        const uint F4_BSAHEADER_FILEID = 0x58445442; // Magic for Fallout 4 BA2, the literal string "BTDX".
        const uint F4_BSAHEADER_VERSION1 = 0x01; // Version number of a Fallout 4 BA2
        const uint F4_BSAHEADER_VERSION2 = 0x02; // Version number of a Starfield BA2

        public enum F4_HeaderType : uint
        {
            GNRL = 0x4c524e47,
            DX10 = 0x30315844,
            GNMF = 0x464d4e47,
            Unknown,
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct F4_Header
        {
            public static (string, int) Struct = ("<3IQ", sizeof(F4_Header));
            public uint Version;            // 04
            public F4_HeaderType Type;      // 08 GNRL=General, DX10=Textures, GNMF=?, ___=?
            public uint NumFiles;           // 0C
            public ulong NameTableOffset;   // 10 - relative to start of file
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct F4_File
        {
            public static (string, int) Struct = ("<4IQ3I", sizeof(F4_File));
            public uint NameHash;           // 00
            public uint Ext;                // 04 - extension
            public uint DirHash;            // 08
            public uint Flags;              // 0C - flags? 00100100
            public ulong Offset;            // 10 - relative to start of file
            public uint PackedSize;         // 18 - packed length (zlib)
            public uint FileSize;           // 1C - unpacked length
            public uint Align;              // 20 - BAADF00D
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct F4_Texture
        {
            public static (string, int) Struct = ("<3I2B3H4B", sizeof(F4_Texture));
            public uint NameHash;           // 00
            public uint Ext;                // 04 - extension
            public uint DirHash;            // 08
            public byte Unk0C;              // 0C
            public byte NumChunks;          // 0D
            public ushort ChunkHeaderSize;  // 0E - size of one chunk header
            public ushort Height;           // 10
            public ushort Width;            // 12
            public byte NumMips;            // 14
            public byte Format;             // 15 - DXGI_FORMAT
            public byte IsCubemap;          // 16
            public byte TileMode;           // 17
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct F4_GNMF
        {
            public static (string, int) Struct = ("<3I2BH32sQ4I", sizeof(F4_GNMF));
            public uint NameHash;           // 00
            public uint Ext;                // 04 - extension
            public uint DirHash;            // 08
            public byte Unk0C;              // 0C
            public byte NumChunks;          // 0D
            public ushort Unk0E;            // 0E
            public fixed byte Header[32];   // 10
            public ulong Offset;            // 30
            public uint PackedSize;         // 38
            public uint FileSize;           // 3C
            public uint Unk40;              // 40
            public uint Align;              // 44
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct F4_TextureChunk
        {
            public static (string, int) Struct = ("<Q2I2HI", sizeof(F4_TextureChunk));
            public ulong Offset;            // 00
            public uint PackedSize;         // 08
            public uint FileSize;           // 0C
            public ushort StartMip;         // 10
            public ushort EndMip;           // 12
            public uint Align;              // 14 - BAADFOOD
        }

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            FileSource[] files;

            // Fallout 4 - Starfield
            var magic = source.Magic = r.ReadUInt32();
            if (magic == F4_BSAHEADER_FILEID)
            {
                var header = r.ReadS<F4_Header>();
                if (header.Version > F4_BSAHEADER_VERSION2) throw new FormatException("BAD MAGIC");
                source.Version = header.Version;
                source.Files = files = new FileSource[header.NumFiles];
                // version2
                //if (header.Version == F4_BSAHEADER_VERSION2) r.Skip(8);

                switch (header.Type)
                {
                    // General BA2 Format
                    case F4_HeaderType.GNRL:
                        var headerFiles = r.ReadSArray<F4_File>((int)header.NumFiles);
                        for (var i = 0; i < headerFiles.Length; i++)
                        {
                            ref F4_File headerFile = ref headerFiles[i];
                            files[i] = new FileSource
                            {
                                Compressed = headerFile.PackedSize != 0 ? 1 : 0,
                                PackedSize = headerFile.PackedSize,
                                FileSize = headerFile.FileSize,
                                Offset = (long)headerFile.Offset,
                            };
                        }
                        break;
                    // Texture BA2 Format
                    case F4_HeaderType.DX10:
                        for (var i = 0; i < header.NumFiles; i++)
                        {
                            var headerTexture = r.ReadS<F4_Texture>();
                            var headerTextureChunks = r.ReadSArray<F4_TextureChunk>(headerTexture.NumChunks);
                            ref F4_TextureChunk firstChunk = ref headerTextureChunks[0];
                            files[i] = new FileSource
                            {
                                PackedSize = firstChunk.PackedSize,
                                FileSize = firstChunk.FileSize,
                                Offset = (long)firstChunk.Offset,
                                Tag = ((object)headerTexture, headerTextureChunks),
                            };
                        }
                        break;
                    // GNMF BA2 Format
                    case F4_HeaderType.GNMF:
                        for (var i = 0; i < header.NumFiles; i++)
                        {
                            var headerGNMF = r.ReadS<F4_GNMF>();
                            var headerTextureChunks = r.ReadSArray<F4_TextureChunk>(headerGNMF.NumChunks);
                            files[i] = new FileSource
                            {
                                PackedSize = headerGNMF.PackedSize,
                                FileSize = headerGNMF.FileSize,
                                Offset = (long)headerGNMF.Offset,
                                Tag = ((object)headerGNMF, headerTextureChunks),
                            };
                        }
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(header.Type), header.Type.ToString());
                }

                // assign full names to each file
                if (header.NameTableOffset > 0)
                {
                    r.Seek((long)header.NameTableOffset);
                    var path = r.ReadL16Encoding().Replace('\\', '/');
                    foreach (var file in files) file.Path = path;
                }
            }
            else throw new InvalidOperationException("BAD MAGIC");
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            const int GNF_HEADER_MAGIC = 0x20464E47;
            const int GNF_HEADER_CONTENT_SIZE = 248;

            // position
            r.Seek(file.Offset);

            // General BA2 Format
            if (file.Tag == null)
                return Task.FromResult<Stream>(file.Compressed != 0
                    ? new MemoryStream(r.DecompressZlib2((int)file.PackedSize, (int)file.FileSize))
                    : new MemoryStream(r.ReadBytes((int)file.FileSize)));

            var tag = ((object tex, F4_TextureChunk[] chunks))file.Tag;

            // Texture BA2 Format
            if (tag.tex is F4_Texture tex)
            {
                var s = new MemoryStream();
                // write header
                var w = new BinaryWriter(s);
                var ddsHeader = new DDS_HEADER
                {
                    dwSize = DDS_HEADER.SizeOf,
                    dwFlags = DDSD.HEADER_FLAGS_TEXTURE | DDSD.HEADER_FLAGS_LINEARSIZE | DDSD.HEADER_FLAGS_MIPMAP,
                    dwHeight = tex.Height,
                    dwWidth = tex.Width,
                    dwMipMapCount = tex.NumMips,
                    dwCaps = DDSCAPS.SURFACE_FLAGS_TEXTURE | DDSCAPS.SURFACE_FLAGS_MIPMAP,
                    dwCaps2 = tex.IsCubemap == 1 ? DDSCAPS2.CUBEMAP_ALLFACES : 0,
                };
                ddsHeader.ddspf.dwSize = DDS_PIXELFORMAT.SizeOf;
                switch ((DXGI_FORMAT)tex.Format)
                {
                    case DXGI_FORMAT.BC1_UNORM:
                        ddsHeader.ddspf.dwFlags = DDPF.FOURCC;
                        ddsHeader.ddspf.dwFourCC = FourCC.DXT1;
                        ddsHeader.dwPitchOrLinearSize = (uint)(tex.Width * tex.Height / 2U); // 4bpp
                        break;
                    case DXGI_FORMAT.BC2_UNORM:
                        ddsHeader.ddspf.dwFlags = DDPF.FOURCC;
                        ddsHeader.ddspf.dwFourCC = FourCC.DXT3;
                        ddsHeader.dwPitchOrLinearSize = (uint)(tex.Width * tex.Height); // 8bpp
                        break;
                    case DXGI_FORMAT.BC3_UNORM:
                        ddsHeader.ddspf.dwFlags = DDPF.FOURCC;
                        ddsHeader.ddspf.dwFourCC = FourCC.DXT5;
                        ddsHeader.dwPitchOrLinearSize = (uint)(tex.Width * tex.Height); // 8bpp
                        break;
                    case DXGI_FORMAT.BC5_UNORM:
                        ddsHeader.ddspf.dwFlags = DDPF.FOURCC;
                        ddsHeader.ddspf.dwFourCC = FourCC.ATI2;
                        ddsHeader.dwPitchOrLinearSize = (uint)(tex.Width * tex.Height); // 8bpp
                        break;
                    case DXGI_FORMAT.BC1_UNORM_SRGB:
                        ddsHeader.ddspf.dwFlags = DDPF.FOURCC;
                        ddsHeader.ddspf.dwFourCC = FourCC.DX10;
                        ddsHeader.dwPitchOrLinearSize = (uint)(tex.Width * tex.Height / 2); // 4bpp
                        break;
                    case DXGI_FORMAT.BC3_UNORM_SRGB:
                    case DXGI_FORMAT.BC4_UNORM:
                    case DXGI_FORMAT.BC5_SNORM:
                    case DXGI_FORMAT.BC7_UNORM:
                    case DXGI_FORMAT.BC7_UNORM_SRGB:
                        ddsHeader.ddspf.dwFlags = DDPF.FOURCC;
                        ddsHeader.ddspf.dwFourCC = FourCC.DX10;
                        ddsHeader.dwPitchOrLinearSize = (uint)(tex.Width * tex.Height); // 8bpp
                        break;
                    case DXGI_FORMAT.R8G8B8A8_UNORM:
                    case DXGI_FORMAT.R8G8B8A8_UNORM_SRGB:
                        ddsHeader.ddspf.dwFlags = DDPF.RGB | DDPF.ALPHA;
                        ddsHeader.ddspf.dwRGBBitCount = 32;
                        ddsHeader.ddspf.dwRBitMask = 0x000000FF;
                        ddsHeader.ddspf.dwGBitMask = 0x0000FF00;
                        ddsHeader.ddspf.dwBBitMask = 0x00FF0000;
                        ddsHeader.ddspf.dwABitMask = 0xFF000000;
                        ddsHeader.dwPitchOrLinearSize = (uint)(tex.Width * tex.Height * 4); // 32bpp
                        break;
                    case DXGI_FORMAT.B8G8R8A8_UNORM:
                    case DXGI_FORMAT.B8G8R8X8_UNORM:
                        ddsHeader.ddspf.dwFlags = DDPF.RGB | DDPF.ALPHA;
                        ddsHeader.ddspf.dwRGBBitCount = 32;
                        ddsHeader.ddspf.dwRBitMask = 0x00FF0000;
                        ddsHeader.ddspf.dwGBitMask = 0x0000FF00;
                        ddsHeader.ddspf.dwBBitMask = 0x000000FF;
                        ddsHeader.ddspf.dwABitMask = 0xFF000000;
                        ddsHeader.dwPitchOrLinearSize = (uint)(tex.Width * tex.Height * 4); // 32bpp
                        break;
                    case DXGI_FORMAT.R8_UNORM:
                        ddsHeader.ddspf.dwFlags = DDPF.RGB | DDPF.ALPHA;
                        ddsHeader.ddspf.dwRGBBitCount = 8;
                        ddsHeader.ddspf.dwRBitMask = 0xFF;
                        ddsHeader.dwPitchOrLinearSize = (uint)(tex.Width * tex.Height); // 8bpp
                        break;
                    default: throw new ArgumentOutOfRangeException(nameof(tex.Format), $"Unsupported DDS header format. File: {file.Path}");
                }
                w.Write(DDS_HEADER.MAGIC);
                w.WriteS(ddsHeader);
                switch ((DXGI_FORMAT)tex.Format)
                {
                    case DXGI_FORMAT.BC1_UNORM_SRGB:
                    case DXGI_FORMAT.BC3_UNORM_SRGB:
                    case DXGI_FORMAT.BC4_UNORM:
                    case DXGI_FORMAT.BC5_SNORM:
                    case DXGI_FORMAT.BC7_UNORM:
                    case DXGI_FORMAT.BC7_UNORM_SRGB:
                        w.WriteS(new DDS_HEADER_DXT10
                        {
                            dxgiFormat = (DXGI_FORMAT)tex.Format,
                            resourceDimension = D3D10_RESOURCE_DIMENSION.TEXTURE2D,
                            miscFlag = 0,
                            arraySize = 1,
                            miscFlags2 = (uint)DDS_ALPHA_MODE.ALPHA_MODE_UNKNOWN,
                        });
                        break;
                }

                // write chunks
                var chunks = tag.chunks;
                for (var i = 0; i < tex.NumChunks; i++)
                {
                    var chunk = chunks[i];
                    r.Seek((long)chunk.Offset);
                    if (chunk.PackedSize != 0) s.WriteBytes(r.DecompressZlib((int)file.PackedSize, (int)file.FileSize));
                    else s.WriteBytes(r, (int)file.FileSize);
                }
                s.Position = 0;
                return Task.FromResult<Stream>(s);
            }
            // GNMF BA2 Format
            else if (tag.tex is F4_GNMF gnmf)
            {
                var s = new MemoryStream();
                // write header
                var w = new BinaryWriter(s);
                w.Write(GNF_HEADER_MAGIC); // 'GNF ' magic
                w.Write(GNF_HEADER_CONTENT_SIZE); // Content-size. Seems to be either 4 or 8 bytes
                w.Write((byte)0x2); // Version
                w.Write((byte)0x1); // Texture Count
                w.Write((byte)0x8); // Alignment
                w.Write((byte)0x0); // Unused
                w.Write(BitConverter.GetBytes(gnmf.FileSize + 256).Reverse().ToArray()); // File size + header size
                w.Write(UnsafeX.FixedTArray(gnmf.Header, 32));
                for (var i = 0; i < 208; i++) w.Write((byte)0x0); // Padding

                // write chunks
                var chunks = tag.chunks;
                for (var i = 0; i < gnmf.NumChunks; i++)
                {
                    var chunk = chunks[i];
                    r.Seek((long)chunk.Offset);
                    if (chunk.PackedSize != 0) s.WriteBytes(r.DecompressZlib2((int)file.PackedSize, (int)file.FileSize));
                    else s.WriteBytes(r, (int)file.FileSize);
                }
                s.Position = 0;
                return Task.FromResult<Stream>(s);
            }
            else throw new ArgumentOutOfRangeException(nameof(tag.tex), tag.tex.ToString());
        }
    }

    #endregion

    #region PakBinary_Bsa

    // http://en.uesp.net/wiki/Bethesda4Mod:BSA_File_Format
    // http://en.uesp.net/wiki/Bethesda3Mod:BSA_File_Format

    public unsafe class PakBinary_Bsa : PakBinary<PakBinary_Bsa>
    {
        // Header : TES4
        #region Header : TES4

        // Default header data
        const uint OB_BSAHEADER_FILEID = 0x00415342;    // Magic for Oblivion BSA, the literal string "BSA\0".
        const uint OB_BSAHEADER_VERSION = 0x67;         // Version number of an Oblivion BSA
        const uint F3_BSAHEADER_VERSION = 0x68;         // Version number of a Fallout 3 BSA
        const uint SSE_BSAHEADER_VERSION = 0x69;        // Version number of a Skyrim SE BSA

        // Archive flags
        const ushort OB_BSAARCHIVE_PATHNAMES = 0x0001;  // Whether the BSA has names for paths
        const ushort OB_BSAARCHIVE_FILENAMES = 0x0002;  // Whether the BSA has names for files
        const ushort OB_BSAARCHIVE_COMPRESSFILES = 0x0004; // Whether the files are compressed
        const ushort F3_BSAARCHIVE_PREFIXFULLFILENAMES = 0x0100; // Whether the name is prefixed to the data?

        // File flags
        //const ushort OB_BSAFILE_NIF = 0x0001; // Set when the BSA contains NIF files (Meshes)
        //const ushort OB_BSAFILE_DDS = 0x0002; // Set when the BSA contains DDS files (Textures)
        //const ushort OB_BSAFILE_XML = 0x0004; // Set when the BSA contains XML files (Menus)
        //const ushort OB_BSAFILE_WAV = 0x0008; // Set when the BSA contains WAV files (Sounds)
        //const ushort OB_BSAFILE_MP3 = 0x0010; // Set when the BSA contains MP3 files (Voices)
        //const ushort OB_BSAFILE_TXT = 0x0020; // Set when the BSA contains TXT files (Shaders)
        //const ushort OB_BSAFILE_HTML = 0x0020; // Set when the BSA contains HTML files
        //const ushort OB_BSAFILE_BAT = 0x0020; // Set when the BSA contains BAT files
        //const ushort OB_BSAFILE_SCC = 0x0020; // Set when the BSA contains SCC files
        //const ushort OB_BSAFILE_SPT = 0x0040; // Set when the BSA contains SPT files (Trees)
        //const ushort OB_BSAFILE_TEX = 0x0080; // Set when the BSA contains TEX files
        //const ushort OB_BSAFILE_FNT = 0x0080; // Set when the BSA contains FNT files (Fonts)
        //const ushort OB_BSAFILE_CTL = 0x0100; // Set when the BSA contains CTL files (Miscellaneous)

        // Bitmasks for the size field in the header
        const uint OB_BSAFILE_SIZEMASK = 0x3fffffff; // Bit mask with OB_HeaderFile:SizeFlags to get the compression status
        const uint OB_BSAFILE_SIZECOMPRESS = 0xC0000000; // Bit mask with OB_HeaderFile:SizeFlags to get the compression status

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct OB_Header
        {
            public static (string, int) Struct = ("<8I", sizeof(OB_Header));
            public uint Version;            // 04
            public uint FolderRecordOffset; // Offset of beginning of folder records
            public uint ArchiveFlags;       // Archive flags
            public uint FolderCount;        // Total number of folder records (OBBSAFolderInfo)
            public uint FileCount;          // Total number of file records (OBBSAFileInfo)
            public uint FolderNameLength;   // Total length of folder names
            public uint FileNameLength;     // Total length of file names
            public uint FileFlags;          // File flags
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct OB_Folder
        {
            public static (string, int) Struct = ("<Q2I", sizeof(OB_Folder));
            public ulong Hash;              // Hash of the folder name
            public uint FileCount;          // Number of files in folder
            public uint Offset;             // The offset
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct OB_FolderSSE
        {
            public static (string, int) Struct = ("<Q2IQ", sizeof(OB_FolderSSE));
            public ulong Hash;              // Hash of the folder name
            public uint FileCount;          // Number of files in folder
            public uint Unk;                // Unknown
            public ulong Offset;            // The offset
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct OB_File
        {
            public static (string, int) Struct = ("<Q2I", sizeof(OB_File));
            public ulong Hash;              // Hash of the filename
            public uint Size;               // Size of the data, possibly with OB_BSAFILE_SIZECOMPRESS set
            public uint Offset;             // Offset to raw file data
        }

        #endregion

        // Header : TES3
        #region Header : TES3

        // Default header data
        const uint MW_BSAHEADER_FILEID = 0x00000100; // Magic for Morrowind BSA

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct MW_Header
        {
            public static (string, int) Struct = ("<2I", sizeof(MW_Header));
            public uint HashOffset;         // Offset of hash table minus header size (12)
            public uint FileCount;          // Number of files in the archive
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct MW_File
        {
            public static (string, int) Struct = ("<2I", sizeof(MW_File));
            public uint FileSize;           // File size
            public uint FileOffset;         // File offset relative to data position
            public readonly uint Size => FileSize > 0 ? FileSize & 0x3FFFFFFF : 0; // The size of the file inside the BSA
        }

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            FileSource[] files;
            var magic = source.Magic = r.ReadUInt32();

            // Oblivion - Skyrim
            if (magic == OB_BSAHEADER_FILEID)
            {
                var header = r.ReadS<OB_Header>();
                if (header.Version != OB_BSAHEADER_VERSION
                    && header.Version != F3_BSAHEADER_VERSION
                    && header.Version != SSE_BSAHEADER_VERSION)
                    throw new FormatException("BAD MAGIC");
                if ((header.ArchiveFlags & OB_BSAARCHIVE_PATHNAMES) == 0
                    || (header.ArchiveFlags & OB_BSAARCHIVE_FILENAMES) == 0)
                    throw new FormatException("HEADER FLAGS");
                source.Version = header.Version;

                // calculate some useful values
                var compressedToggle = (header.ArchiveFlags & OB_BSAARCHIVE_COMPRESSFILES) > 0;
                if (header.Version == F3_BSAHEADER_VERSION
                    || header.Version == SSE_BSAHEADER_VERSION)
                    source.Tag = (header.ArchiveFlags & F3_BSAARCHIVE_PREFIXFULLFILENAMES) > 0;

                // read-all folders
                var foldersFiles = header.Version == SSE_BSAHEADER_VERSION
                    ? r.ReadSArray<OB_FolderSSE>((int)header.FolderCount).Select(x => x.FileCount).ToArray()
                    : r.ReadSArray<OB_Folder>((int)header.FolderCount).Select(x => x.FileCount).ToArray();

                // read-all folder files
                var fileX = 0U;
                source.Files = files = new FileSource[header.FileCount];
                for (var i = 0; i < header.FolderCount; i++)
                {
                    var folderName = r.ReadFAString(r.ReadByte() - 1).Replace('\\', '/');
                    r.Skip(1);
                    var headerFiles = r.ReadSArray<OB_File>((int)foldersFiles[i]);
                    foreach (var headerFile in headerFiles)
                    {
                        var compressed = (headerFile.Size & OB_BSAFILE_SIZECOMPRESS) != 0;
                        var packedSize = compressed ? headerFile.Size ^ OB_BSAFILE_SIZECOMPRESS : headerFile.Size;
                        files[fileX++] = new FileSource
                        {
                            Path = folderName,
                            Offset = headerFile.Offset,
                            Compressed = compressed ^ compressedToggle ? 1 : 0,
                            PackedSize = packedSize,
                            FileSize = source.Version == SSE_BSAHEADER_VERSION ? packedSize & OB_BSAFILE_SIZEMASK : packedSize,
                        };
                    };
                }

                // read-all names
                foreach (var file in files) file.Path = $"{file.Path}/{r.ReadVUString()}";
            }
            // Morrowind
            else if (magic == MW_BSAHEADER_FILEID)
            {
                var header = r.ReadS<MW_Header>();
                var dataOffset = 12 + header.HashOffset + (header.FileCount << 3);

                // create filesources
                source.Files = files = new FileSource[header.FileCount];
                var headerFiles = r.ReadSArray<MW_File>((int)header.FileCount);
                for (var i = 0; i < headerFiles.Length; i++)
                {
                    ref MW_File headerFile = ref headerFiles[i];
                    var size = headerFile.Size;
                    files[i] = new FileSource
                    {
                        Offset = dataOffset + headerFile.FileOffset,
                        FileSize = size,
                        PackedSize = size,
                    };
                }

                // read filename offsets
                var filenameOffsets = r.ReadPArray<uint>("I", (int)header.FileCount); // relative offset in filenames section

                // read filenames
                var filenamesPosition = r.Tell();
                for (var i = 0; i < files.Length; i++)
                {
                    r.Seek(filenamesPosition + filenameOffsets[i]);
                    files[i].Path = r.ReadVAString(1000).Replace('\\', '/');
                }
            }
            else throw new InvalidOperationException("BAD MAGIC");
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            // position
            var fileSize = (int)file.FileSize;
            r.Seek(file.Offset);
            if (source.Tag is bool z && z)
            {
                var prefixLength = r.ReadByte() + 1;
                if (source.Version == SSE_BSAHEADER_VERSION) fileSize -= prefixLength;
                r.Seek(file.Offset + prefixLength);
            }

            // not compressed
            if (fileSize <= 0 || file.Compressed == 0)
                return Task.FromResult<Stream>(new MemoryStream(r.ReadBytes(fileSize)));

            // compressed
            var newFileSize = (int)r.ReadUInt32(); fileSize -= 4;
            return Task.FromResult<Stream>(source.Version == SSE_BSAHEADER_VERSION
                ? new MemoryStream(r.DecompressLz4(fileSize, newFileSize))
                : new MemoryStream(r.DecompressZlib2(fileSize, newFileSize)));
        }
    }

    #endregion

    #region PakBinary_Esm

    // TES3
    //http://en.uesp.net/wiki/Bethesda3Mod:File_Format
    //https://github.com/TES5Edit/TES5Edit/blob/dev/wbDefinitionsTES3.pas
    //http://en.uesp.net/morrow/tech/mw_esm.txt
    //https://github.com/mlox/mlox/blob/master/util/tes3cmd/tes3cmd
    // TES4
    //https://github.com/WrinklyNinja/esplugin/tree/master/src
    //http://en.uesp.net/wiki/Bethesda4Mod:Mod_File_Format
    //https://github.com/TES5Edit/TES5Edit/blob/dev/wbDefinitionsTES4.pas 
    // TES5
    //http://en.uesp.net/wiki/Bethesda5Mod:Mod_File_Format
    //https://github.com/TES5Edit/TES5Edit/blob/dev/wbDefinitionsTES5.pas 

    /// <summary>
    /// PakBinaryBethesdaEsm
    /// </summary>
    /// <seealso cref="GameX.Formats._Packages.PakBinaryBethesdaEsm" />
    public unsafe class PakBinary_Esm : PakBinary<PakBinary_Esm>
    {
        const int RecordHeaderSizeInBytes = 16;
        public FormType Format;
        public Dictionary<FormType, RecordGroup> Groups;

        static FormType GetFormat(string game)
            => game switch
            {
                // tes
                "Morrowind" => FormType.TES3,
                "Oblivion" => FormType.TES4,
                var x when x == "Skyrim" || x == "SkyrimSE" || x == "SkyrimVR" => FormType.TES5,
                // fallout
                var x when x == "Fallout3" || x == "FalloutNV" => FormType.TES4,
                var x when x == "Fallout4" || x == "Fallout4VR" => FormType.TES5,
                "Starfield" => FormType.TES6,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game),
            };

        /// <summary>
        /// Reads the asynchronous.
        /// </summary>
        /// <param name="source">The source.</param>
        /// <param name="r">The r.</param>
        /// <param name="stage">The stage.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException">stage</exception>
        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            Format = GetFormat(source.Game.Id);
            var recordLevel = 1;
            var filePath = source.PakPath;
            var poolAction = (GenericPoolAction<BinaryReader>)source.GetReader().Action; //: Leak
            var rootHeader = new Header(r, Format, null);
            //if ((Format == FormFormat.TES3 && rootHeader.Type != FormType.TES3) || (Format != FormFormat.TES3 && rootHeader.Type != FormType.TES4)) throw new FormatException($"{filePath} record header {rootHeader.Type} is not valid for this {Format}");
            var rootRecord = rootHeader.CreateRecord(rootHeader.Position, recordLevel);
            rootRecord.Read(r, filePath, Format);

            // morrowind hack
            if (Format == FormType.TES3)
            {
                var group = new RecordGroup(poolAction, filePath, Format, recordLevel);
                group.AddHeader(new Header { Label = 0, DataSize = (uint)(r.BaseStream.Length - r.Tell()), Position = r.Tell() });
                group.Load();
                Groups = group.Records.GroupBy(x => x.Header.Type).ToDictionary(x => x.Key, x =>
                {
                    var s = new RecordGroup(null, filePath, Format, recordLevel) { Records = [..x] };
                    s.AddHeader(new Header { Label = x.Key }, load: false);
                    return s;
                });
                return Task.CompletedTask;
            }

            // read groups
            Groups = [];
            var endPosition = r.BaseStream.Length;
            while (r.BaseStream.Position < endPosition)
            {
                var header = new Header(r, Format, null);
                if (header.Type != FormType.GRUP) throw new InvalidOperationException($"{header.Type} not GRUP");
                var nextPosition = r.Tell() + header.DataSize;
                if (!Groups.TryGetValue(header.Label, out var group)) { group = new RecordGroup(poolAction, filePath, Format, recordLevel); Groups.Add(header.Label, group); }
                group.AddHeader(header);
                r.Seek(nextPosition);
            }
            return Task.CompletedTask;
        }

        // TES3
        Dictionary<string, IRecord> MANYsById;
        Dictionary<long, LTEXRecord> LTEXsById;
        Dictionary<Int3, LANDRecord> LANDsById;
        Dictionary<Int3, CELLRecord> CELLsById;
        Dictionary<string, CELLRecord> CELLsByName;

        // TES4
        Dictionary<uint, Tuple<WRLDRecord, RecordGroup[]>> WRLDsById;
        Dictionary<string, LTEXRecord> LTEXsByEid;

        public override Task Process(BinaryPakFile source)
        {
            if (Format == FormType.TES3)
            {
                var statGroups = new List<Record>[] { Groups.ContainsKey(FormType.STAT) ? Groups[FormType.STAT].Load() : null };
                MANYsById = statGroups.SelectMany(x => x).Where(x => x != null).ToDictionary(x => x.EDID.Value, x => (IRecord)x);
                LTEXsById = Groups[FormType.LTEX].Load().Cast<LTEXRecord>().ToDictionary(x => x.INTV.Value);
                var lands = Groups[FormType.LAND].Load().Cast<LANDRecord>().ToList();
                foreach (var land in lands) land.GridId = new Int3(land.INTV.CellX, land.INTV.CellY, 0);
                LANDsById = lands.ToDictionary(x => x.GridId);
                var cells = Groups[FormType.CELL].Load().Cast<CELLRecord>().ToList();
                foreach (var cell in cells) cell.GridId = new Int3(cell.XCLC.Value.GridX, cell.XCLC.Value.GridY, !cell.IsInterior ? 0 : -1);
                CELLsById = cells.Where(x => !x.IsInterior).ToDictionary(x => x.GridId);
                CELLsByName = cells.Where(x => x.IsInterior).ToDictionary(x => x.EDID.Value);
                return Task.CompletedTask;
            }
            var wrldsByLabel = Groups[FormType.WRLD].GroupsByLabel;
            WRLDsById = Groups[FormType.WRLD].Load().Cast<WRLDRecord>().ToDictionary(x => x.Id, x => { wrldsByLabel.TryGetValue(x.Id, out var wrlds); return new Tuple<WRLDRecord, RecordGroup[]>(x, wrlds); });
            LTEXsByEid = Groups[FormType.LTEX].Load().Cast<LTEXRecord>().ToDictionary(x => x.EDID.Value);
            return Task.CompletedTask;
        }
    }

    #endregion
}
using GameX.Algorithms;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static System.IO.Polyfill;

namespace GameX.Valve.Formats
{
    #region PakBinary_Bsp30
    // https://hlbsp.sourceforge.net/index.php?content=bspdef
    // https://github.com/bernhardmgruber/hlbsp/tree/master/src
    // https://developer.valvesoftware.com/wiki/BSP_(Source)
    // https://developer.valvesoftware.com/wiki/BSP_(GoldSrc)

    public unsafe class PakBinary_Bsp30 : PakBinary<PakBinary_Bsp30>
    {
        #region Headers

        [StructLayout(LayoutKind.Sequential)]
        struct X_Header
        {
            public static (string, int) Struct = ("<31i", sizeof(X_Header));
            public int Version;
            public X_LumpON Entities;
            public X_LumpON Planes;
            public X_LumpON Textures;
            public X_LumpON Vertices;
            public X_LumpON Visibility;
            public X_LumpON Nodes;
            public X_LumpON TexInfo;
            public X_LumpON Faces;
            public X_LumpON Lighting;
            public X_LumpON ClipNodes;
            public X_LumpON Leaves;
            public X_LumpON MarkSurfaces;
            public X_LumpON Edges;
            public X_LumpON SurfEdges;
            public X_LumpON Models;

            public void ForGameId(string id)
            {
                if (id == "HL:BS") (Entities, Planes) = (Planes, Entities);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct X_Texture
        {
            public static (string, int) Struct = ("<16s6I", sizeof(X_Texture));
            public fixed byte Name[16];
            public uint Width;
            public uint Height;
            public fixed uint Offsets[4];
        }

        //const int MAX_MAP_HULLS = 4;
        //const int MAX_MAP_MODELS = 400;
        //const int MAX_MAP_BRUSHES = 4096;
        //const int MAX_MAP_ENTITIES = 1024;
        //const int MAX_MAP_ENTSTRING = (128 * 1024);
        //const int MAX_MAP_PLANES = 32767;
        //const int MAX_MAP_NODES = 32767;
        //const int MAX_MAP_CLIPNODES = 32767;
        //const int MAX_MAP_LEAFS = 8192;
        //const int MAX_MAP_VERTS = 65535;
        //const int MAX_MAP_FACES = 65535;
        //const int MAX_MAP_MARKSURFACES = 65535;
        //const int MAX_MAP_TEXINFO = 8192;
        //const int MAX_MAP_EDGES = 256000;
        //const int MAX_MAP_SURFEDGES = 512000;
        //const int MAX_MAP_TEXTURES = 512;
        //const int MAX_MAP_MIPTEX = 0x200000;
        //const int MAX_MAP_LIGHTING = 0x200000;
        //const int MAX_MAP_VISIBILITY = 0x200000;
        //const int MAX_MAP_PORTALS = 65536;

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            var files = source.Files = [];

            // read file
            int start, stop, stride;
            var header = r.ReadS<X_Header>();
            if (header.Version != 30) throw new FormatException("BAD VERSION");
            header.ForGameId(source.Game.Id);
            files.Add(new FileSource { Path = "entities.txt", Offset = header.Entities.Offset, FileSize = header.Entities.Num });
            files.Add(new FileSource { Path = "planes.dat", Offset = header.Planes.Offset, FileSize = header.Planes.Num });
            r.Seek(start = header.Textures.Offset);
            foreach (var o in r.ReadL32PArray<uint>("I"))
            {
                r.Seek(start + o);
                var tex = r.ReadS<X_Texture>();
                files.Add(new FileSource { Path = $"textures/{UnsafeX.FixedAString(tex.Name, 16)}.tex", Tag = tex });
            }
            files.Add(new FileSource { Path = "vertices.dat", Offset = header.Vertices.Offset, FileSize = header.Vertices.Num });
            files.Add(new FileSource { Path = "visibility.dat", Offset = header.Visibility.Offset, FileSize = header.Visibility.Num });
            files.Add(new FileSource { Path = "nodes.dat", Offset = header.Nodes.Offset, FileSize = header.Nodes.Num });
            files.Add(new FileSource { Path = "texInfo.dat", Offset = header.TexInfo.Offset, FileSize = header.TexInfo.Num });
            files.Add(new FileSource { Path = "faces.dat", Offset = header.Faces.Offset, FileSize = header.Faces.Num });
            files.Add(new FileSource { Path = "lighting.dat", Offset = header.Lighting.Offset, FileSize = header.Lighting.Num });
            files.Add(new FileSource { Path = "clipNodes.dat", Offset = header.ClipNodes.Offset, FileSize = header.ClipNodes.Num });
            files.Add(new FileSource { Path = "leaves.dat", Offset = header.Leaves.Offset, FileSize = header.Leaves.Num });
            files.Add(new FileSource { Path = "markSurfaces.dat", Offset = header.MarkSurfaces.Offset, FileSize = header.MarkSurfaces.Num });
            files.Add(new FileSource { Path = "edges.dat", Offset = header.Edges.Offset, FileSize = header.Edges.Num });
            files.Add(new FileSource { Path = "surfEdges.dat", Offset = header.SurfEdges.Offset, FileSize = header.SurfEdges.Num });
            start = header.Models.Offset; stop = start + header.Models.Num; stride = 33 + (4 << 2);
            for (var o = start; o < stop; o += stride) files.Add(new FileSource { Path = $"models/model{o}.dat", Offset = o, FileSize = stride });
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            r.Seek(file.Offset);
            return Task.FromResult<Stream>(new MemoryStream(r.ReadBytes((int)file.FileSize)));
        }
    }

    #endregion

    #region PakBinary_Vpk

    // https://developer.valvesoftware.com/wiki/VPK_File_Format
    public unsafe class PakBinary_Vpk : PakBinary<PakBinary_Vpk>
    {
        #region Headers

        public const int MAGIC = 0x55AA1234;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct V_HeaderV2
        {
            public static (string, int) Struct = ("<4I", sizeof(V_HeaderV2));
            public uint FileDataSectionSize;
            public uint ArchiveMd5SectionSize;
            public uint OtherMd5SectionSize;
            public uint SignatureSectionSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct V_ArchiveMd5
        {
            public static (string, int) Struct = ("<3I16s", sizeof(V_ArchiveMd5));
            public uint ArchiveIndex;       // Gets or sets the CRC32 checksum of this entry.
            public uint Offset;             // Gets or sets the offset in the package.
            public uint Length;             // Gets or sets the length in bytes.
            public fixed byte Checksum[16]; // Gets or sets the expected Checksum checksum.
        }

        /// <summary>
        /// Verification
        /// </summary>
        class Verification
        {
            public (long p, V_ArchiveMd5[] h) ArchiveMd5s;  // Gets the archive MD5 checksum section entries. Also known as cache line hashes.
            public byte[] TreeChecksum;                     // Gets the MD5 checksum of the file tree.
            public byte[] ArchiveMd5EntriesChecksum;        // Gets the MD5 checksum of the archive MD5 checksum section entries.
            public (long p, byte[] h) WholeFileChecksum;    // Gets the MD5 checksum of the complete package until the signature structure.
            public byte[] PublicKey;                        // Gets the public key.
            public (long p, byte[] h) Signature;            // Gets the signature.

            public Verification(BinaryReader r, ref V_HeaderV2 h)
            {
                // archive md5
                if (h.ArchiveMd5SectionSize != 0)
                {
                    ArchiveMd5s = (r.Tell(), r.ReadSArray<V_ArchiveMd5>((int)h.ArchiveMd5SectionSize / sizeof(V_ArchiveMd5)));
                }
                // other md5
                if (h.OtherMd5SectionSize != 0)
                {
                    TreeChecksum = r.ReadBytes(16);
                    ArchiveMd5EntriesChecksum = r.ReadBytes(16);
                    WholeFileChecksum = (r.Tell(), r.ReadBytes(16));
                }
                // signature
                if (h.SignatureSectionSize != 0)
                {
                    var position = r.Tell();
                    var publicKeySize = r.ReadInt32();
                    if (h.SignatureSectionSize == 20 && publicKeySize == MAGIC) return; // CS2 has this
                    PublicKey = r.ReadBytes(publicKeySize);
                    Signature = (position, r.ReadBytes(r.ReadInt32()));
                }
            }

            /// <summary>
            /// Verify checksums and signatures provided in the VPK
            /// </summary>
            public void VerifyHashes(BinaryReader r, uint treeSize, ref V_HeaderV2 h, long headerPosition)
            {
                byte[] hash;
                using var md5 = MD5.Create();
                // treeChecksum
                r.Seek(headerPosition);
                hash = md5.ComputeHash(r.ReadBytes((int)treeSize));
                if (!hash.SequenceEqual(TreeChecksum)) throw new InvalidDataException($"File tree checksum mismatch ({hash:X} != expected {TreeChecksum:X})");
                // archiveMd5SectionSize
                r.Seek(ArchiveMd5s.p);
                hash = md5.ComputeHash(r.ReadBytes((int)h.ArchiveMd5SectionSize));
                if (!hash.SequenceEqual(ArchiveMd5EntriesChecksum)) throw new InvalidDataException($"Archive MD5 checksum mismatch ({hash:X} != expected {ArchiveMd5EntriesChecksum:X})");
                // wholeFileChecksum
                r.Seek(0);
                hash = md5.ComputeHash(r.ReadBytes((int)WholeFileChecksum.p));
                if (!hash.SequenceEqual(WholeFileChecksum.h)) throw new InvalidDataException($"Package checksum mismatch ({hash:X} != expected {WholeFileChecksum.h:X})");
            }

            /// <summary>
            /// Verifies the RSA signature
            /// </summary>
            public void VerifySignature(BinaryReader r)
            {
                if (PublicKey == null || Signature.h == null) return;
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(PublicKey, out _);
                r.Seek(0);
                var data = r.ReadBytes((int)Signature.p);
                if (!rsa.VerifyData(data, Signature.h, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw new InvalidDataException("VPK signature is not valid");
            }
        }

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            var files = source.Files = [];

            // file mask
            source.FileMask = path =>
            {
                var extension = Path.GetExtension(path);
                if (extension.EndsWith("_c", StringComparison.Ordinal)) extension = extension[..^2];
                if (extension.StartsWith(".v")) extension = extension.Remove(1, 1);
                return $"{Path.GetFileNameWithoutExtension(path)}{extension}";
            };

            // pakPath
            var pakPath = source.PakPath;
            var dirVpk = pakPath.EndsWith("_dir.vpk", StringComparison.OrdinalIgnoreCase);
            if (dirVpk) pakPath = pakPath[..^8];

            // read header
            if (r.ReadUInt32() != MAGIC) throw new FormatException("BAD MAGIC");
            var version = r.ReadUInt32();
            var treeSize = r.ReadUInt32();
            if (version == 0x00030002) throw new FormatException("Unsupported VPK: Apex Legends, Titanfall");
            else if (version > 2) throw new FormatException($"Bad VPK version. ({version})");
            var headerV2 = version == 2 ? r.ReadS<V_HeaderV2>() : default;
            var headerPosition = (uint)r.Tell();

            // read entires
            var ms = new MemoryStream();
            while (true)
            {
                var typeName = r.ReadVUString(ms: ms);
                if (string.IsNullOrEmpty(typeName)) break;
                while (true)
                {
                    var directoryName = r.ReadVUString(ms: ms);
                    if (string.IsNullOrEmpty(directoryName)) break;
                    while (true)
                    {
                        var fileName = r.ReadVUString(ms: ms);
                        if (string.IsNullOrEmpty(fileName)) break;
                        // get file
                        var file = new FileSource
                        {
                            Path = $"{(directoryName[0] != ' ' ? $"{directoryName}/" : null)}{fileName}.{typeName}",
                            Hash = r.ReadUInt32(),
                            Data = new byte[r.ReadUInt16()],
                            Id = r.ReadUInt16(),
                            Offset = r.ReadUInt32(),
                            FileSize = r.ReadUInt32(),
                        };
                        var terminator = r.ReadUInt16();
                        if (terminator != 0xFFFF) throw new FormatException($"Invalid terminator, was 0x{terminator:X} but expected 0x{0xFFFF:X}");
                        if (file.Data.Length > 0) r.Read(file.Data, 0, file.Data.Length);
                        if (file.Id != 0x7FFF)
                        {
                            if (!dirVpk) throw new FormatException("Given VPK is not a _dir, but entry is referencing an external archive.");
                            file.Tag = $"{pakPath}_{file.Id:D3}.vpk";
                        }
                        else file.Tag = (long)(headerPosition + treeSize);
                        // add file
                        files.Add(file);
                    }
                }
            }

            // verification
            if (version == 2)
            {
                // skip over file data, if any
                r.Skip(headerV2.FileDataSectionSize);
                var v = new Verification(r, ref headerV2);
                v.VerifyHashes(r, treeSize, ref headerV2, headerPosition);
                v.VerifySignature(r);
            }

            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            var fileDataLength = file.Data.Length;
            var data = new byte[fileDataLength + file.FileSize];
            if (fileDataLength > 0) file.Data.CopyTo(data, 0);
            if (file.FileSize == 0) { }
            else if (file.Tag is long offset) { r.Seek(file.Offset + offset); r.Read(data, fileDataLength, (int)file.FileSize); }
            else if (file.Tag is string pakPath) source.Reader(r2 => { r2.Seek(file.Offset); r2.Read(data, fileDataLength, (int)file.FileSize); }, pakPath);
            var actualChecksum = Crc32Digest.Compute(data);
            if (file.Hash != actualChecksum) throw new InvalidDataException($"CRC32 mismatch for read data (expected {file.Hash:X2}, got {actualChecksum:X2})");
            return Task.FromResult((Stream)new MemoryStream(data));
        }
    }

    #endregion

    #region PakBinary_Wad3

    // https://github.com/Rupan/HLLib/blob/master/HLLib/WADFile.h
    public unsafe class PakBinary_Wad3 : PakBinary<PakBinary_Wad3>
    {
        #region Headers

        const uint W_MAGIC = 0x33444157; //: WAD3

        [StructLayout(LayoutKind.Sequential)]
        struct W_Header
        {
            public static (string, int) Struct = ("<3I", sizeof(W_Header));
            public uint Magic;
            public uint LumpCount;
            public uint LumpOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct W_Lump
        {
            public static (string, int) Struct = ("<3I2bH16s", 32);
            public uint Offset;
            public uint DiskSize;
            public uint Size;
            public byte Type;
            public byte Compression;
            public ushort Padding;
            public fixed byte Name[16];
        }

        [StructLayout(LayoutKind.Sequential)]
        struct W_LumpInfo
        {
            public static (string, int) Struct = ("<3I", 32);
            public uint Width;
            public uint Height;
            public uint PaletteSize;
        }

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            var files = source.Files = [];

            // read file
            var header = r.ReadS<W_Header>();
            if (header.Magic != W_MAGIC) throw new FormatException("BAD MAGIC");
            r.Seek(header.LumpOffset);
            var lumps = r.ReadSArray<W_Lump>((int)header.LumpCount);
            foreach (var lump in lumps)
            {
                var name = UnsafeX.FixedAString(lump.Name, 16);
                files.Add(new FileSource
                {
                    Path = lump.Type switch
                    {
                        0x40 => $"{name}.tex2",
                        0x42 => $"{name}.pic",
                        0x43 => $"{name}.tex",
                        0x46 => $"{name}.fnt",
                        _ => $"{name}.{lump.Type:x}"
                    },
                    Offset = lump.Offset,
                    Compressed = lump.Compression,
                    FileSize = lump.DiskSize,
                    PackedSize = lump.Size,
                });
            }
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            r.Seek(file.Offset);
            return Task.FromResult<Stream>(new MemoryStream(file.Compressed == 0
                ? r.ReadBytes((int)file.FileSize)
                : throw new NotSupportedException()));
        }
    }

    #endregion
}
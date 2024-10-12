using GameX.Algorithms;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GameX.Valve.Formats
{
    #region PakBinary_Vpk

    // https://developer.valvesoftware.com/wiki/VPK_File_Format
    public unsafe class PakBinary_Vpk : PakBinary<PakBinary_Vpk>
    {
        #region Headers

        public const int MAGIC = 0x55AA1234;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct HeaderV2
        {
            public static (string, int) Struct = ("<5I", sizeof(HeaderV2));
            public uint FileDataSectionSize;
            public uint ArchiveMd5SectionSize;
            public uint OtherMd5SectionSize;
            public uint SignatureSectionSize;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct ArchiveMd5Entry
        {
            public uint ArchiveIndex; // Gets or sets the CRC32 checksum of this entry.
            public uint Offset; // Gets or sets the offset in the package.
            public uint Length; // Gets or sets the length in bytes.
            public fixed byte Checksum[16];// Gets or sets the expected Checksum checksum.
        }

        /// <summary>
        /// Verification
        /// </summary>
        class Verification
        {
            public (long p, ArchiveMd5Entry[] h) ArchiveMd5Entries; // Gets the archive MD5 checksum section entries. Also known as cache line hashes.
            public byte[] TreeChecksum;                     // Gets the MD5 checksum of the file tree.
            public byte[] ArchiveMd5EntriesChecksum;        // Gets the MD5 checksum of the archive MD5 checksum section entries.
            public (long p, byte[] h) WholeFileChecksum;    // Gets the MD5 checksum of the complete package until the signature structure.
            public byte[] PublicKey;                        // Gets the public key.
            public (long p, byte[] h) Signature;            // Gets the signature.

            public Verification(BinaryReader r, ref HeaderV2 h)
            {
                // archive md5
                if (h.ArchiveMd5SectionSize != 0)
                {
                    ArchiveMd5Entries = (r.Tell(), r.ReadSArray<ArchiveMd5Entry>((int)h.ArchiveMd5SectionSize / sizeof(ArchiveMd5Entry)));
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
            public void VerifyHashes(BinaryReader r, uint treeSize, ref HeaderV2 h, long headerPosition)
            {
                byte[] hash;
                using var md5 = MD5.Create();
                r.Seek(headerPosition);
                hash = md5.ComputeHash(r.ReadBytes((int)treeSize));
                if (!hash.SequenceEqual(TreeChecksum)) throw new InvalidDataException($"File tree checksum mismatch ({BitConverter.ToString(hash)} != expected {BitConverter.ToString(TreeChecksum)})");

                r.Seek(ArchiveMd5Entries.p);
                hash = md5.ComputeHash(r.ReadBytes((int)h.ArchiveMd5SectionSize));
                if (!hash.SequenceEqual(ArchiveMd5EntriesChecksum)) throw new InvalidDataException($"Archive MD5 entries checksum mismatch ({BitConverter.ToString(hash)} != expected {BitConverter.ToString(ArchiveMd5EntriesChecksum)})");

                r.Seek(0);
                hash = md5.ComputeHash(r.ReadBytes((int)WholeFileChecksum.p));
                if (!hash.SequenceEqual(WholeFileChecksum.h)) throw new InvalidDataException($"Package checksum mismatch ({BitConverter.ToString(hash)} != expected {BitConverter.ToString(WholeFileChecksum.h)})");
            }

            /// <summary>
            /// Verifies the RSA signature.
            /// </summary>
            /// <returns>True if signature is valid, false otherwise.</returns>
            public void VerifySignature(BinaryReader r)
            {
                if (PublicKey == null || Signature.h == null) return;

                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(PublicKey, out _);
                //rsa.ImportParameters(new AsnKeyParser(PublicKey).ParseRSAPublicKey());
                r.Seek(0);
                var data = r.ReadBytes((int)Signature.p);
                if (!rsa.VerifyData(data, Signature.h, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) throw new InvalidDataException("VPK signature is not valid.");
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

            // header
            if (r.ReadUInt32() != MAGIC) throw new FormatException("BAD MAGIC");
            var version = r.ReadUInt32();
            var treeSize = r.ReadUInt32();
            if (version == 0x00030002) throw new FormatException($"Unsupported VPK: Apex Legends, Titanfall");
            else if (version > 2) throw new FormatException($"Bad VPK version. ({version})");
            var headerV2 = version == 2 ? r.ReadS<HeaderV2>() : default;
            var headerPosition = (uint)r.Tell();

            // read entires
            var ms = new MemoryStream();
            while (true)
            {
                var typeName = r.ReadVUString(ms: ms); if (string.IsNullOrEmpty(typeName)) break;
                // directories
                while (true)
                {
                    var directoryName = r.ReadVUString(ms: ms); if (string.IsNullOrEmpty(directoryName)) break;
                    // files
                    while (true)
                    {
                        var fileName = r.ReadVUString(ms: ms); if (string.IsNullOrEmpty(fileName)) break;
                        var metadata = new FileSource
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
                        if (metadata.Data.Length > 0) r.Read(metadata.Data, 0, metadata.Data.Length);
                        if (metadata.Id != 0x7FFF)
                        {
                            if (!dirVpk) throw new FormatException("Given VPK is not a _dir, but entry is referencing an external archive.");
                            metadata.Tag = $"{pakPath}_{metadata.Id:D3}.vpk";
                        }
                        else metadata.Tag = (long)(headerPosition + treeSize);
                        files.Add(metadata);
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
            var data = new byte[file.Data.Length + file.FileSize];
            if (file.Data.Length > 0) file.Data.CopyTo(data, 0);
            if (file.FileSize > 0)
            {
                if (file.Tag is string path)
                    source.GetReader(path).Action(r2 =>
                    {
                        r2.Seek(file.Offset);
                        r2.Read(data, file.Data.Length, (int)file.FileSize);
                    });
                else
                {
                    r.Seek(file.Offset + (long)file.Tag);
                    r.Read(data, file.Data.Length, (int)file.FileSize);
                }
            }
            var actualChecksum = Crc32Digest.Compute(data);
            if (file.Hash != actualChecksum) throw new InvalidDataException($"CRC32 mismatch for read data (expected {file.Hash:X2}, got {actualChecksum:X2})");
            return Task.FromResult((Stream)new MemoryStream(data));
        }
    }

    #endregion

    #region PakBinary_Wad

    // https://github.com/Rupan/HLLib/blob/master/HLLib/WADFile.h
    public unsafe class PakBinary_Wad : PakBinary<PakBinary_Wad>
    {
        #region Headers

        const uint W_MAGIC = 0x33444157; //: WAD3

        [StructLayout(LayoutKind.Sequential, Pack = 1), DebuggerDisplay("Header:{LumpCount}")]
        struct W_Header
        {
            public static (string, int) Struct = ("<3I", sizeof(W_Header));
            public uint Magic;
            public uint LumpCount;
            public uint LumpOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1), DebuggerDisplay("Lump:{Name}")]
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

        [StructLayout(LayoutKind.Sequential, Pack = 1), DebuggerDisplay("LumpInfo:{Width}x{Height}")]
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
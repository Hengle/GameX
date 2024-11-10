using GameX.Algorithms;
using GameX.Formats;
using GameX.Platforms;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Encoders;
using OpenStack.Gfx;
using OpenStack.Gfx.Algorithms;
using OpenStack.Gfx.Renders;
using OpenStack.Gfx.Textures;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using static GameX.Valve.Formats.Vpk.D_Texture.VTexFormat;

namespace GameX.Valve.Formats.Vpk
{
    #region ResourceType
    //was:Resource/Enums/ResourceType

    public enum ResourceType
    {
        Unknown = 0,
        [ExtensionX("vanim")] Animation,
        [ExtensionX("vagrp")] AnimationGroup,
        [ExtensionX("vanmgrph")] AnimationGraph,
        [ExtensionX("valst")] ActionList,
        [ExtensionX("vseq")] Sequence,
        [ExtensionX("vpcf")] ParticleSystem,
        [ExtensionX("vmat")] Material,
        [ExtensionX("vmks")] Sheet,
        [ExtensionX("vmesh")] Mesh,
        [ExtensionX("vtex")] Texture,
        [ExtensionX("vmdl")] Model,
        [ExtensionX("vphys")] PhysicsCollisionMesh,
        [ExtensionX("vsnd")] Sound,
        [ExtensionX("vmorf")] Morph,
        [ExtensionX("vrman")] ResourceManifest,
        [ExtensionX("vwrld")] World,
        [ExtensionX("vwnod")] WorldNode,
        [ExtensionX("vvis")] WorldVisibility,
        [ExtensionX("vents")] EntityLump,
        [ExtensionX("vsurf")] SurfaceProperties,
        [ExtensionX("vsndevts")] SoundEventScript,
        [ExtensionX("vmix")] VMix,
        [ExtensionX("vsndstck")] SoundStackScript,
        [ExtensionX("vfont")] BitmapFont,
        [ExtensionX("vrmap")] ResourceRemapTable,
        [ExtensionX("vcdlist")] ChoreoSceneFileData,
        // All Panorama* are compiled just as CompilePanorama
        [ExtensionX("vtxt")] Panorama, // vtxt is not a real extension
        [ExtensionX("vcss")] PanoramaStyle,
        [ExtensionX("vxml")] PanoramaLayout,
        [ExtensionX("vpdi")] PanoramaDynamicImages,
        [ExtensionX("vjs")] PanoramaScript,
        [ExtensionX("vts")] PanoramaTypescript,
        [ExtensionX("vsvg")] PanoramaVectorGraphic,
        [ExtensionX("vpsf")] ParticleSnapshot,
        [ExtensionX("vmap")] Map,
        [ExtensionX("vpost")] PostProcessing,
        [ExtensionX("vdata")] VData,
        [ExtensionX("item")] ArtifactItem,
        [ExtensionX("sbox")] SboxManagedResource, // TODO: Managed resources can have any extension
    }

    #endregion

    #region KV3File
    //was:Serialization/KV3File

    public class KV3File(IDictionary<string, object> root,
        string encoding = "text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d}",
        string format = "generic:version{7412167c-06e9-4698-aff2-e63eb59037e7}")
    {
        // <!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->
        public IDictionary<string, object> Root = root;
        public string Encoding = encoding;
        public string Format = format;

        public override string ToString()
        {
            using var w = new IndentedTextWriter();
            w.WriteLine(string.Format("<!-- kv3 encoding:{0} format:{1} -->", Encoding, Format));
            //Root.Serialize(w);
            return w.ToString();
        }
    }

    #endregion

    #region Block
    //was:Resource/Block

    /// <summary>
    /// Represents a block within the resource file.
    /// </summary>
    public abstract class Block
    {
        /// <summary>
        /// Gets or sets the offset to the data.
        /// </summary>
        public uint Offset { get; set; }

        /// <summary>
        /// Gets or sets the data size.
        /// </summary>
        public uint Size { get; set; }

        public abstract void Read(Binary_Src parent, BinaryReader r);

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>A string that represents the current object.</returns>
        public override string ToString()
        {
            using var w = new IndentedTextWriter();
            WriteText(w);
            return w.ToString();
        }

        /// <summary>
        /// Writers the correct object to IndentedTextWriter.
        /// </summary>
        /// <param name="w">IndentedTextWriter.</param>
        public virtual void WriteText(IndentedTextWriter w) => w.WriteLine("{0:X8}", Offset);

        //was:Resource.ConstructFromType()
        public static Block Factory(Binary_Src source, string value)
            => value switch
            {
                "DATA" => Factory(source),
                "REDI" => new REDI(),
                "RED2" => new RED2(),
                "RERL" => new RERL(),
                "NTRO" => new NTRO(),
                "VBIB" => new VBIB(),
                "VXVS" => new VXVS(),
                "SNAP" => new SNAP(),
                "MBUF" => new MBUF(),
                "CTRL" => new CTRL(),
                "MDAT" => new MDAT(),
                "INSG" => new INSG(),
                "SrMa" => new SRMA(),
                "LaCo" => new LACO(),
                "MRPH" => new MRPH(),
                "ANIM" => new ANIM(),
                "ASEQ" => new ASEQ(),
                "AGRP" => new AGRP(),
                "PHYS" => new PHYS(),
                _ => throw new ArgumentOutOfRangeException(nameof(value), $"Unrecognized block type '{value}'"),
            };

        //was:Resource.ConstructResourceType()
        internal static DATA Factory(Binary_Src source) => source.DataType switch
        {
            var x when x == ResourceType.Panorama || x == ResourceType.PanoramaScript || x == ResourceType.PanoramaTypescript || x == ResourceType.PanoramaDynamicImages || x == ResourceType.PanoramaVectorGraphic => new D_Panorama(),
            ResourceType.PanoramaStyle => new D_PanoramaStyle(),
            ResourceType.PanoramaLayout => new D_PanoramaLayout(),
            ResourceType.Sound => new D_Sound(),
            ResourceType.Texture => new D_Texture(),
            ResourceType.Model => new D_Model(),
            ResourceType.World => new D_World(),
            ResourceType.WorldNode => new D_WorldNode(),
            ResourceType.EntityLump => new D_EntityLump(),
            ResourceType.Material => new D_Material(),
            ResourceType.SoundEventScript => new D_SoundEventScript(),
            ResourceType.SoundStackScript => new D_SoundStackScript(),
            ResourceType.ParticleSystem => new D_ParticleSystem(),
            ResourceType.PostProcessing => new D_PostProcessing(),
            ResourceType.ResourceManifest => new D_ResourceManifest(),
            var x when x == ResourceType.SboxManagedResource || x == ResourceType.ArtifactItem => new D_Plaintext(),
            ResourceType.PhysicsCollisionMesh => new D_PhysAggregateData(),
            ResourceType.Mesh => new D_Mesh(source),
            //ResourceType.Mesh => source.Version != 0 ? new DATABinaryKV3() : source.ContainsBlockType<NTRO>() ? new DATABinaryNTRO() : new DATA(),
            _ => source.ContainsBlockType<NTRO>() ? new D_NTRO() : new DATA(),
        };

        internal static ResourceType DetermineResourceTypeByFileExtension(string fileName, string extension = null)
        {
            extension ??= Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension)) return ResourceType.Unknown;
            extension = extension.EndsWith("_c", StringComparison.Ordinal) ? extension[1..^2] : extension[1..];
            foreach (ResourceType typeValue in Enum.GetValues(typeof(ResourceType)))
            {
                if (typeValue == ResourceType.Unknown) continue;
                var type = typeof(ResourceType).GetMember(typeValue.ToString())[0];
                var typeExt = (ExtensionXAttribute)type.GetCustomAttributes(typeof(ExtensionXAttribute), false)[0];
                if (typeExt.Extension == extension) return typeValue;
            }
            return ResourceType.Unknown;
        }

        internal static ResourceType DetermineTypeByCompilerIdentifier(R_SpecialDependencies.SpecialDependency value)
        {
            var identifier = value.CompilerIdentifier;
            if (identifier.StartsWith("Compile", StringComparison.Ordinal)) identifier = identifier.Remove(0, "Compile".Length);
            return identifier switch
            {
                "Psf" => ResourceType.ParticleSnapshot,
                "AnimGroup" => ResourceType.AnimationGroup,
                "Animgraph" => ResourceType.AnimationGraph,
                "VPhysXData" => ResourceType.PhysicsCollisionMesh,
                "Font" => ResourceType.BitmapFont,
                "RenderMesh" => ResourceType.Mesh,
                "ChoreoSceneFileData" => ResourceType.ChoreoSceneFileData,
                "Panorama" => value.String switch
                {
                    "Panorama Style Compiler Version" => ResourceType.PanoramaStyle,
                    "Panorama Script Compiler Version" => ResourceType.PanoramaScript,
                    "Panorama Layout Compiler Version" => ResourceType.PanoramaLayout,
                    "Panorama Dynamic Images Compiler Version" => ResourceType.PanoramaDynamicImages,
                    _ => ResourceType.Panorama,
                },
                "VectorGraphic" => ResourceType.PanoramaVectorGraphic,
                "VData" => ResourceType.VData,
                "DotaItem" => ResourceType.ArtifactItem,
                var x when x == "SBData" || x == "ManagedResourceCompiler" => ResourceType.SboxManagedResource, // This is without the "Compile" prefix
                _ => Enum.TryParse(identifier, false, out ResourceType resourceType) ? resourceType : ResourceType.Unknown,
            };
        }

        internal static bool IsHandledType(ResourceType type) =>
            type == ResourceType.Model ||
            type == ResourceType.World ||
            type == ResourceType.WorldNode ||
            type == ResourceType.ParticleSystem ||
            type == ResourceType.Material ||
            type == ResourceType.EntityLump ||
            type == ResourceType.PhysicsCollisionMesh ||
            type == ResourceType.Morph ||
            type == ResourceType.PostProcessing;

        public static ReadOnlySpan<byte> FastDecompress(BinaryReader r)
        {
            var decompressedSize = r.ReadUInt32();

            // Valve sets fourth byte in the compressed buffer to 0x80 to indicate that the data is uncompressed,
            // 0x80000000 is 2147483648 which automatically makes any number higher than max signed 32-bit integer.
            if (decompressedSize > int.MaxValue) return r.ReadBytes((int)decompressedSize & 0x7FFFFFFF);

            var result = new Span<byte>(new byte[decompressedSize]);
            var position = 0;
            ushort blockMask = 0;
            var i = 0;

            while (position < decompressedSize)
            {
                if (i == 0) { blockMask = r.ReadUInt16(); i = 16; }
                if ((blockMask & 1) > 0)
                {
                    var offsetSize = r.ReadUInt16();
                    var offset = (offsetSize >> 4) + 1;
                    var size = (offsetSize & 0xF) + 3;
                    var positionSource = position - offset;

                    // This path is seemingly useless, because it produces equal results.
                    // Is this draw of the luck because `result` is initialized to zeroes?
                    if (offset == 1) while (size-- > 0) result[position++] = result[positionSource];
                    else while (size-- > 0) result[position++] = result[positionSource++];
                }
                else result[position++] = r.ReadByte();
                blockMask >>= 1;
                i--;
            }
            return result;
        }
    }

    #endregion

    #region Data : XKV1
    //was:Resource/ResourceTypes/BinaryKV1

    public class XKV1 : DATA
    {
        public const int MAGIC = 0x564B4256; // VBKV

        public IDictionary<string, object> KeyValues { get; private set; }

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.BaseStream.Position = Offset;
            //KeyValues = KVSerializer.Create(KVSerializationFormat.KeyValues1Binary).Deserialize(r.BaseStream);
        }

        public override string ToString()
        {
            using var ms = new MemoryStream();
            using var r = new StreamReader(ms);
            //KVSerializer.Create(KVSerializationFormat.KeyValues1Text).Serialize(ms, KeyValues);
            ms.Seek(0, SeekOrigin.Begin);
            return r.ReadToEnd();
        }
    }

    #endregion

    #region Data : XKV3
    //was:Resource/ResourceTypes/BinaryKV3

    public class XKV3 : DATA, IHaveMetaInfo
    {
        public enum KVFlag //was:Serialization/KeyValues/KVFlaggedValue
        {
            None,
            Resource,
            DeferredResource
        }

        public enum KVType : byte //was:Serialization/KeyValues/KVValue
        {
            STRING_MULTI = 0, // STRING_MULTI doesn't have an ID
            NULL = 1,
            BOOLEAN = 2,
            INT64 = 3,
            UINT64 = 4,
            DOUBLE = 5,
            STRING = 6,
            BINARY_BLOB = 7,
            ARRAY = 8,
            OBJECT = 9,
            ARRAY_TYPED = 10,
            INT32 = 11,
            UINT32 = 12,
            BOOLEAN_TRUE = 13,
            BOOLEAN_FALSE = 14,
            INT64_ZERO = 15,
            INT64_ONE = 16,
            DOUBLE_ZERO = 17,
            DOUBLE_ONE = 18,
        }

        static readonly Guid KV3_ENCODING_BINARY_BLOCK_COMPRESSED = new Guid(new byte[] { 0x46, 0x1A, 0x79, 0x95, 0xBC, 0x95, 0x6C, 0x4F, 0xA7, 0x0B, 0x05, 0xBC, 0xA1, 0xB7, 0xDF, 0xD2 });
        static readonly Guid KV3_ENCODING_BINARY_UNCOMPRESSED = new Guid(new byte[] { 0x00, 0x05, 0x86, 0x1B, 0xD8, 0xF7, 0xC1, 0x40, 0xAD, 0x82, 0x75, 0xA4, 0x82, 0x67, 0xE7, 0x14 });
        static readonly Guid KV3_ENCODING_BINARY_BLOCK_LZ4 = new Guid(new byte[] { 0x8A, 0x34, 0x47, 0x68, 0xA1, 0x63, 0x5C, 0x4F, 0xA1, 0x97, 0x53, 0x80, 0x6F, 0xD9, 0xB1, 0x19 });
        static readonly Guid KV3_FORMAT_GENERIC = new Guid(new byte[] { 0x7C, 0x16, 0x12, 0x74, 0xE9, 0x06, 0x98, 0x46, 0xAF, 0xF2, 0xE6, 0x3E, 0xB5, 0x90, 0x37, 0xE7 });
        public const int MAGIC = 0x03564B56; // VKV3 (3 isn't ascii, its 0x03)
        public const int MAGIC2 = 0x4B563301; // KV3\x01
        public const int MAGIC3 = 0x4B563302; // KV3\x02

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = "BinaryKV3", Value = ToString() }),
            new("BinaryKV3", items: [
                new($"Data: {Data.Count}"),
                new($"Encoding: {Encoding}"),
                new($"Format: {Format}"),
            ]),
        ];

        public IDictionary<string, object> Data { get; private set; }
        public Guid Encoding { get; private set; }
        public Guid Format { get; private set; }

        string[] strings;
        byte[] types;
        BinaryReader uncompressedBlockDataReader;
        int[] uncompressedBlockLengthArray;
        long currentCompressedBlockIndex;
        long currentTypeIndex;
        long currentEightBytesOffset = -1;
        long currentBinaryBytesOffset = -1;

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            var magic = r.ReadUInt32();
            switch (magic)
            {
                case MAGIC: ReadVersion1(r); break;
                case MAGIC2: ReadVersion2(r); break;
                case MAGIC3: ReadVersion3(r); break;
                default: throw new ArgumentOutOfRangeException(nameof(magic), $"Invalid XKV3 signature {magic}");
            }
        }

        void DecompressLZ4(BinaryReader r, MemoryStream s)
        {
            var uncompressedSize = r.ReadUInt32();
            var compressedSize = (int)(Size - (r.BaseStream.Position - Offset));

            var output = new Span<byte>(new byte[uncompressedSize]);
            var buf = ArrayPool<byte>.Shared.Rent(compressedSize);
            try
            {
                var input = buf.AsSpan(0, compressedSize);
                r.Read(input);

                var written = LZ4Codec.Decode(input, output);
                if (written != output.Length) throw new InvalidDataException($"Failed to decompress LZ4 (expected {output.Length} bytes, got {written}).");
            }
            finally { ArrayPool<byte>.Shared.Return(buf); }

            s.Write(output);
        }

        void ReadVersion1(BinaryReader r)
        {
            Encoding = r.ReadGuid();
            Format = r.ReadGuid();

            using var s = new MemoryStream();
            using var r2 = new BinaryReader(s, System.Text.Encoding.UTF8, true);

            if (Encoding.CompareTo(KV3_ENCODING_BINARY_BLOCK_COMPRESSED) == 0) s.Write(FastDecompress(r));
            else if (Encoding.CompareTo(KV3_ENCODING_BINARY_BLOCK_LZ4) == 0) DecompressLZ4(r, s);
            else if (Encoding.CompareTo(KV3_ENCODING_BINARY_UNCOMPRESSED) == 0) r.CopyTo(s);
            else throw new ArgumentOutOfRangeException(nameof(Encoding), $"Unrecognised XKV3 Encoding: {Encoding}");
            s.Seek(0, SeekOrigin.Begin);

            strings = new string[r2.ReadUInt32()];
            for (var i = 0; i < strings.Length; i++) strings[i] = r2.ReadVUString();

            Data = (IDictionary<string, object>)ParseBinaryKV3(r2, null, true);

            var trailer = r2.ReadUInt32();
            if (trailer != 0xFFFFFFFF) throw new ArgumentOutOfRangeException(nameof(trailer), $"Invalid trailer {trailer}");
        }

        void ReadVersion2(BinaryReader r)
        {
            Format = r.ReadGuid();
            var compressionMethod = r.ReadInt32();
            var countOfBinaryBytes = r.ReadInt32(); // how many bytes (binary blobs)
            var countOfIntegers = r.ReadInt32(); // how many 4 byte values (ints)
            var countOfEightByteValues = r.ReadInt32(); // how many 8 byte values (doubles)

            using var s = new MemoryStream();

            if (compressionMethod == 0)
            {
                var length = r.ReadInt32();
                var output = new Span<byte>(new byte[length]);
                r.Read(output);
                s.Write(output);
                s.Seek(0, SeekOrigin.Begin);
            }
            else if (compressionMethod == 1) DecompressLZ4(r, s);
            else throw new ArgumentOutOfRangeException(nameof(compressionMethod), $"Unknown XKV3 compression method: {compressionMethod}");

            using var r2 = new BinaryReader(s, System.Text.Encoding.UTF8, true);

            currentBinaryBytesOffset = 0;
            r2.SeekAndAlign(countOfBinaryBytes, 4);

            var countOfStrings = r2.ReadInt32();
            var kvDataOffset = r2.BaseStream.Position;

            // Subtract one integer since we already read it (countOfStrings)
            r2.SkipAndAlign((countOfIntegers - 1) * 4, 8);

            currentEightBytesOffset = r2.BaseStream.Position;
            r2.BaseStream.Position += countOfEightByteValues * 8;

            strings = new string[countOfStrings];
            for (var i = 0; i < countOfStrings; i++) strings[i] = r2.ReadVUString();

            // bytes after the string table is kv types, minus 4 static bytes at the end
            var typesLength = r2.BaseStream.Length - 4 - r2.BaseStream.Position;
            types = new byte[typesLength];
            for (var i = 0; i < typesLength; i++) types[i] = r2.ReadByte();

            // Move back to the start of the KV data for reading.
            r2.Seek(kvDataOffset);
            Data = (IDictionary<string, object>)ParseBinaryKV3(r2, null, true);
        }

        void ReadVersion3(BinaryReader r)
        {
            Format = r.ReadGuid();

            var compressionMethod = r.ReadUInt32();
            var compressionDictionaryId = r.ReadUInt16();
            var compressionFrameSize = r.ReadUInt16();
            var countOfBinaryBytes = r.ReadUInt32(); // how many bytes (binary blobs)
            var countOfIntegers = r.ReadUInt32(); // how many 4 byte values (ints)
            var countOfEightByteValues = r.ReadUInt32(); // how many 8 byte values (doubles)

            // 8 bytes that help valve preallocate, useless for us
            var stringAndTypesBufferSize = r.ReadUInt32();
            var b = r.ReadUInt16();
            var c = r.ReadUInt16();

            var uncompressedSize = r.ReadUInt32();
            var compressedSize = r.ReadUInt32();
            var blockCount = r.ReadUInt32();
            var blockTotalSize = r.ReadUInt32();

            if (compressedSize > int.MaxValue) throw new NotImplementedException("XKV3 compressedSize is higher than 32-bit integer, which we currently don't handle.");
            else if (blockTotalSize > int.MaxValue) throw new NotImplementedException("XKV3 compressedSize is higher than 32-bit integer, which we currently don't handle.");

            using var s = new MemoryStream();

            if (compressionMethod == 0)
            {
                if (compressionDictionaryId != 0) throw new ArgumentOutOfRangeException(nameof(compressionDictionaryId), $"Unhandled: {compressionDictionaryId}");
                else if (compressionFrameSize != 0) throw new ArgumentOutOfRangeException(nameof(compressionFrameSize), $"Unhandled: {compressionFrameSize}");

                var output = new Span<byte>(new byte[compressedSize]);
                r.Read(output);
                s.Write(output);
            }
            else if (compressionMethod == 1)
            {
                if (compressionDictionaryId != 0) throw new ArgumentOutOfRangeException(nameof(compressionDictionaryId), $"Unhandled: {compressionDictionaryId}");
                else if (compressionFrameSize != 16384) throw new ArgumentOutOfRangeException(nameof(compressionFrameSize), $"Unhandled: {compressionFrameSize}");

                var output = new Span<byte>(new byte[uncompressedSize]);
                var buf = ArrayPool<byte>.Shared.Rent((int)compressedSize);
                try
                {
                    var input = buf.AsSpan(0, (int)compressedSize);
                    r.Read(input);
                    var written = LZ4Codec.Decode(input, output);
                    if (written != output.Length) throw new InvalidDataException($"Failed to decompress LZ4 (expected {output.Length} bytes, got {written}).");
                }
                finally { ArrayPool<byte>.Shared.Return(buf); }
                s.Write(output);
            }
            else if (compressionMethod == 2)
            {
                if (compressionDictionaryId != 0) throw new ArgumentOutOfRangeException(nameof(compressionDictionaryId), $"Unhandled {compressionDictionaryId}");
                else if (compressionFrameSize != 0) throw new ArgumentOutOfRangeException(nameof(compressionFrameSize), $"Unhandled {compressionFrameSize}");

                using var zstd = new ZstdSharp.Decompressor();
                var totalSize = uncompressedSize + blockTotalSize;
                var output = new Span<byte>(new byte[totalSize]);
                var buf = ArrayPool<byte>.Shared.Rent((int)compressedSize);
                try
                {
                    var input = buf.AsSpan(0, (int)compressedSize);
                    r.Read(input);
                    if (!zstd.TryUnwrap(input, output, out var written) || totalSize != written) throw new InvalidDataException($"Failed to decompress zstd correctly (written {written} bytes, expected {totalSize} bytes)");
                }
                finally { ArrayPool<byte>.Shared.Return(buf); }
                s.Write(output);
            }
            else throw new ArgumentOutOfRangeException(nameof(compressionMethod), $"Unknown compression method {compressionMethod}");

            s.Seek(0, SeekOrigin.Begin);
            using var r2 = new BinaryReader(s, System.Text.Encoding.UTF8, true);

            currentBinaryBytesOffset = 0;
            r2.BaseStream.Position = countOfBinaryBytes;
            r2.SeekAndAlign(countOfBinaryBytes, 4); // Align to % 4 after binary blobs

            var countOfStrings = r2.ReadUInt32();
            var kvDataOffset = r2.BaseStream.Position;

            // Subtract one integer since we already read it (countOfStrings)
            r2.SkipAndAlign((countOfIntegers - 1) * 4, 8); // Align to % 8 for the start of doubles

            currentEightBytesOffset = r2.BaseStream.Position;

            r2.BaseStream.Position += countOfEightByteValues * 8;
            var stringArrayStartPosition = r2.BaseStream.Position;

            strings = new string[countOfStrings];
            for (var i = 0; i < countOfStrings; i++) strings[i] = r2.ReadVUString();

            var typesLength = stringAndTypesBufferSize - (r2.BaseStream.Position - stringArrayStartPosition);
            types = new byte[typesLength];
            for (var i = 0; i < typesLength; i++) types[i] = r2.ReadByte();

            if (blockCount == 0)
            {
                var noBlocksTrailer = r2.ReadUInt32();
                if (noBlocksTrailer != 0xFFEEDD00) throw new ArgumentOutOfRangeException(nameof(noBlocksTrailer), $"Invalid trailer {noBlocksTrailer}");

                // Move back to the start of the KV data for reading.
                r2.BaseStream.Position = kvDataOffset;

                Data = (IDictionary<string, object>)ParseBinaryKV3(r2, null, true);
                return;
            }

            uncompressedBlockLengthArray = new int[blockCount];
            for (var i = 0; i < blockCount; i++) uncompressedBlockLengthArray[i] = r2.ReadInt32();

            var trailer = r2.ReadUInt32();
            if (trailer != 0xFFEEDD00) throw new ArgumentOutOfRangeException(nameof(trailer), $"Invalid trailer {trailer}");

            try
            {
                using var uncompressedBlocks = new MemoryStream((int)blockTotalSize);
                uncompressedBlockDataReader = new BinaryReader(uncompressedBlocks);

                if (compressionMethod == 0)
                {
                    for (var i = 0; i < blockCount; i++) r.BaseStream.CopyTo(uncompressedBlocks, uncompressedBlockLengthArray[i]);
                }
                else if (compressionMethod == 1)
                {
                    using var lz4decoder = new LZ4ChainDecoder(compressionFrameSize, 0);
                    while (r2.BaseStream.Position < r2.BaseStream.Length)
                    {
                        var compressedBlockLength = r2.ReadUInt16();
                        var output = new Span<byte>(new byte[compressionFrameSize]);
                        var buf = ArrayPool<byte>.Shared.Rent(compressedBlockLength);
                        try
                        {
                            var input = buf.AsSpan(0, compressedBlockLength);
                            r.Read(input);
                            if (lz4decoder.DecodeAndDrain(input, output, out var decoded) && decoded > 0) uncompressedBlocks.Write(decoded < output.Length ? output[..decoded] : output);
                            else throw new InvalidOperationException("LZ4 decode drain failed, this is likely a bug.");
                        }
                        finally { ArrayPool<byte>.Shared.Return(buf); }
                    }
                }
                else if (compressionMethod == 2)
                {
                    // This is supposed to be a streaming decompress using ZSTD_decompressStream,
                    // but as it turns out, zstd unwrap above already decompressed all of the blocks for us,
                    // so all we need to do is just copy the buffer.
                    // It's possible that Valve's code needs extra decompress because they set ZSTD_d_stableOutBuffer parameter.
                    r2.BaseStream.CopyTo(uncompressedBlocks);
                }
                else throw new ArgumentOutOfRangeException(nameof(compressionMethod), $"Unimplemented compression method in block decoder {compressionMethod}");

                uncompressedBlocks.Position = 0;

                // Move back to the start of the KV data for reading.
                r2.BaseStream.Position = kvDataOffset;

                Data = (IDictionary<string, object>)ParseBinaryKV3(r2, null, true);
            }
            finally { uncompressedBlockDataReader.Dispose(); }
        }

        (KVType Type, KVFlag Flag) ReadType(BinaryReader r)
        {
            var databyte = types != null ? types[currentTypeIndex++] : r.ReadByte();
            var flag = KVFlag.None;
            if ((databyte & 0x80) > 0)
            {
                databyte &= 0x7F; // Remove the flag bit
                flag = types != null ? (KVFlag)types[currentTypeIndex++] : (KVFlag)r.ReadByte();
            }
            return ((KVType)databyte, flag);
        }

        object ParseBinaryKV3(BinaryReader r, IDictionary<string, object> parent, bool inArray = false)
        {
            string name;
            if (!inArray)
            {
                var stringId = r.ReadInt32();
                name = stringId == -1 ? string.Empty : strings[stringId];
            }
            else name = null;
            var (type, flag) = ReadType(r);
            var value = ReadBinaryValue(name, type, flag, r);
            if (name != null) parent?.Add(name, value);
            return value;
        }

        object ReadBinaryValue(string name, KVType type, KVFlag flag, BinaryReader r)
        {
            var currentOffset = r.BaseStream.Position;
            object value;
            switch (type)
            {
                case KVType.NULL: value = MakeValue(type, null, flag); break;
                case KVType.BOOLEAN:
                    {
                        if (currentBinaryBytesOffset > -1) r.BaseStream.Position = currentBinaryBytesOffset;
                        value = MakeValue(type, r.ReadBoolean(), flag);
                        if (currentBinaryBytesOffset > -1) { currentBinaryBytesOffset++; r.BaseStream.Position = currentOffset; }
                        break;
                    }
                case KVType.BOOLEAN_TRUE: value = MakeValue(type, true, flag); break;
                case KVType.BOOLEAN_FALSE: value = MakeValue(type, false, flag); break;
                case KVType.INT64_ZERO: value = MakeValue(type, 0L, flag); break;
                case KVType.INT64_ONE: value = MakeValue(type, 1L, flag); break;
                case KVType.INT64:
                    {
                        if (currentEightBytesOffset > 0) r.BaseStream.Position = currentEightBytesOffset;
                        value = MakeValue(type, r.ReadInt64(), flag);
                        if (currentEightBytesOffset > 0) { currentEightBytesOffset = r.BaseStream.Position; r.BaseStream.Position = currentOffset; }
                        break;
                    }
                case KVType.UINT64:
                    {
                        if (currentEightBytesOffset > 0) r.BaseStream.Position = currentEightBytesOffset;
                        value = MakeValue(type, r.ReadUInt64(), flag);
                        if (currentEightBytesOffset > 0) { currentEightBytesOffset = r.BaseStream.Position; r.BaseStream.Position = currentOffset; }
                        break;
                    }
                case KVType.INT32: value = MakeValue(type, r.ReadInt32(), flag); break;
                case KVType.UINT32: value = MakeValue(type, r.ReadUInt32(), flag); break;
                case KVType.DOUBLE:
                    {
                        if (currentEightBytesOffset > 0) r.BaseStream.Position = currentEightBytesOffset;
                        value = MakeValue(type, r.ReadDouble(), flag);
                        if (currentEightBytesOffset > 0) { currentEightBytesOffset = r.BaseStream.Position; r.BaseStream.Position = currentOffset; }
                        break;
                    }
                case KVType.DOUBLE_ZERO: value = MakeValue(type, 0.0D, flag); break;
                case KVType.DOUBLE_ONE: value = MakeValue(type, 1.0D, flag); break;
                case KVType.STRING:
                    {
                        var id = r.ReadInt32();
                        value = MakeValue(type, id == -1 ? string.Empty : strings[id], flag);
                        break;
                    }
                case KVType.BINARY_BLOB:
                    {
                        if (uncompressedBlockDataReader != null)
                        {
                            var output = uncompressedBlockDataReader.ReadBytes(uncompressedBlockLengthArray[currentCompressedBlockIndex++]);
                            value = MakeValue(type, output, flag);
                            break;
                        }
                        var length = r.ReadInt32();
                        if (currentBinaryBytesOffset > -1) r.BaseStream.Position = currentBinaryBytesOffset;
                        value = MakeValue(type, r.ReadBytes(length), flag);
                        if (currentBinaryBytesOffset > -1) { currentBinaryBytesOffset = r.BaseStream.Position; r.BaseStream.Position = currentOffset + 4; }
                        break;
                    }
                case KVType.ARRAY:
                    {
                        var arrayLength = r.ReadInt32();
                        var array = new object[arrayLength];
                        for (var i = 0; i < arrayLength; i++) array[i] = ParseBinaryKV3(r, null, true);
                        value = MakeValue(type, array, flag);
                        break;
                    }
                case KVType.ARRAY_TYPED:
                    {
                        var typeArrayLength = r.ReadInt32();
                        var (subType, subFlag) = ReadType(r);
                        var typedArray = new object[typeArrayLength];
                        for (var i = 0; i < typeArrayLength; i++) typedArray[i] = ReadBinaryValue(null, subType, subFlag, r);
                        value = MakeValue(type, typedArray, flag);
                        break;
                    }
                case KVType.OBJECT:
                    {
                        var objectLength = r.ReadInt32();
                        var newObject = new Dictionary<string, object>();
                        if (name != null) newObject.Add("_key", name);
                        for (var i = 0; i < objectLength; i++) ParseBinaryKV3(r, newObject, false);
                        value = MakeValue(type, newObject, flag);
                        break;
                    }
                default: throw new InvalidDataException($"Unknown KVType {type} on byte {r.BaseStream.Position - 1}");
            }
            return value;
        }

        static object MakeValue(KVType type, object data, KVFlag flag) => data;

        public KV3File GetKV3File()
        {
            // TODO: Other format guids are not "generic" but strings like "vpc19"
            var formatType = Format != KV3_FORMAT_GENERIC ? "vrfunknown" : "generic";
            return new KV3File(Data, format: $"{formatType}:version{{{Format}}}");
        }

        public override void WriteText(IndentedTextWriter w) => w.Write(KVExtensions.Print(Data));
    }

    #endregion

    #region Data : XKV3_NTRO
    //was:Resource/ResourceTypes/KeyValuesOrNTRO

    public class XKV3_NTRO : DATA
    {
        readonly string IntrospectionStructName;
        protected Binary_Src Parent { get; private set; }
        public IDictionary<string, object> Data { get; private set; }
        DATA BackingData;

        public XKV3_NTRO() { }
        public XKV3_NTRO(string introspectionStructName) => IntrospectionStructName = introspectionStructName;

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            Parent = parent;
            if (!parent.ContainsBlockType<NTRO>())
            {
                var kv3 = new XKV3 { Offset = Offset, Size = Size };
                kv3.Read(parent, r);
                Data = kv3.Data;
                BackingData = kv3;
            }
            else
            {
                var ntro = new D_NTRO { StructName = IntrospectionStructName, Offset = Offset, Size = Size };
                ntro.Read(parent, r);
                Data = ntro.Data;
                BackingData = ntro;
            }
        }

        public override string ToString() => BackingData is XKV3 kv3 ? kv3.ToString() : BackingData.ToString();
    }

    #endregion

    #region Block : NTRO

    /// <summary>
    /// "NTRO" block. CResourceIntrospectionManifest.
    /// </summary>
    public class NTRO : Block
    {
        public enum SchemaFieldType //was:Resource/Enum.SchemaFieldType
        {
            Unknown = 0,
            Struct = 1,
            Enum = 2,
            ExternalReference = 3,
            Char = 4,
            UChar = 5,
            Int = 6,
            UInt = 7,
            Float_8 = 8,
            Double = 9,
            SByte = 10, // Int8
            Byte = 11, // UInt8
            Int16 = 12,
            UInt16 = 13,
            Int32 = 14,
            UInt32 = 15,
            Int64 = 16,
            UInt64 = 17,
            Float = 18, // Float32
            Float64 = 19,
            Time = 20,
            Vector2D = 21,
            Vector3D = 22,
            Vector4D = 23,
            QAngle = 24,
            Quaternion = 25,
            VMatrix = 26,
            Fltx4 = 27,
            Color = 28,
            UniqueId = 29,
            Boolean = 30,
            ResourceString = 31,
            Void = 32,
            Matrix3x4 = 33,
            UtlSymbol = 34,
            UtlString = 35,
            Matrix3x4a = 36,
            UtlBinaryBlock = 37,
            Uuid = 38,
            OpaqueType = 39,
            Transform = 40,
            Unused = 41,
            RadianEuler = 42,
            DegreeEuler = 43,
            FourVectors = 44,
        }

        public enum SchemaIndirectionType //was:Resource/Enum.SchemaIndirectionType
        {
            Unknown = 0,
            Pointer = 1,
            Reference = 2,
            ResourcePointer = 3,
            ResourceArray = 4,
            UtlVector = 5,
            UtlReference = 6,
            Ignorable = 7,
            Opaque = 8,
        }

        public class ResourceDiskStruct
        {
            public class Field
            {
                public string FieldName { get; set; }
                public short Count { get; set; }
                public short OnDiskOffset { get; set; }
                public List<byte> Indirections { get; private set; } = new List<byte>();
                public uint TypeData { get; set; }
                public SchemaFieldType Type { get; set; }
                public ushort Unknown { get; set; }

                public void WriteText(IndentedTextWriter w)
                {
                    w.WriteLine("CResourceDiskStructField {"); w.Indent++;
                    w.WriteLine($"CResourceString m_pFieldName = \"{FieldName}\"");
                    w.WriteLine($"int16 m_nCount = {Count}");
                    w.WriteLine($"int16 m_nOnDiskOffset = {OnDiskOffset}");
                    w.WriteLine($"uint8[{Indirections.Count}] m_Indirection = ["); w.Indent++;
                    foreach (var dep in Indirections) w.WriteLine("{0:D2}", dep);
                    w.Indent--; w.WriteLine("]");
                    w.WriteLine($"uint32 m_nTypeData = 0x{TypeData:X8}");
                    w.WriteLine($"int16 m_nType = {(int)Type}");
                    w.Indent--; w.WriteLine("}");
                }
            }

            public uint IntrospectionVersion { get; set; }
            public uint Id { get; set; }
            public string Name { get; set; }
            public uint DiskCrc { get; set; }
            public int UserVersion { get; set; }
            public ushort DiskSize { get; set; }
            public ushort Alignment { get; set; }
            public uint BaseStructId { get; set; }
            public byte StructFlags { get; set; }
            public ushort Unknown { get; set; }
            public byte Unknown2 { get; set; }
            public List<Field> FieldIntrospection { get; private set; } = new List<Field>();

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("CResourceDiskStruct {"); w.Indent++;
                w.WriteLine($"uint32 m_nIntrospectionVersion = 0x{IntrospectionVersion:X8}");
                w.WriteLine($"uint32 m_nId = 0x{Id:X8}");
                w.WriteLine($"CResourceString m_pName = \"{Name}\"");
                w.WriteLine($"uint32 m_nDiskCrc = 0x{DiskCrc:X8}");
                w.WriteLine($"int32 m_nUserVersion = {UserVersion}");
                w.WriteLine($"uint16 m_nDiskSize = 0x{DiskSize:X4}");
                w.WriteLine($"uint16 m_nAlignment = 0x{Alignment:X4}");
                w.WriteLine($"uint32 m_nBaseStructId = 0x{BaseStructId:X8}");
                w.WriteLine($"Struct m_FieldIntrospection[{FieldIntrospection.Count}] = ["); w.Indent++;
                foreach (var field in FieldIntrospection) field.WriteText(w);
                w.Indent--; w.WriteLine("]");
                w.WriteLine($"uint8 m_nStructFlags = 0x{StructFlags:X2}");
                w.Indent--; w.WriteLine("}");
            }
        }

        public class ResourceDiskEnum
        {
            public class Value
            {
                public string EnumValueName { get; set; }
                public int EnumValue { get; set; }

                public void WriteText(IndentedTextWriter w)
                {
                    w.WriteLine("CResourceDiskEnumValue {"); w.Indent++;
                    w.WriteLine("CResourceString m_pEnumValueName = \"{EnumValueName}\"");
                    w.WriteLine("int32 m_nEnumValue = {EnumValue}");
                    w.Indent--; w.WriteLine("}");
                }
            }

            public uint IntrospectionVersion { get; set; }
            public uint Id { get; set; }
            public string Name { get; set; }
            public uint DiskCrc { get; set; }
            public int UserVersion { get; set; }
            public List<Value> EnumValueIntrospection { get; private set; } = new List<Value>();

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("CResourceDiskEnum {"); w.Indent++;
                w.WriteLine($"uint32 m_nIntrospectionVersion = 0x{IntrospectionVersion:X8}");
                w.WriteLine($"uint32 m_nId = 0x{Id:X8}");
                w.WriteLine($"CResourceString m_pName = \"{Name}\"");
                w.WriteLine($"uint32 m_nDiskCrc = 0x{DiskCrc:X8}");
                w.WriteLine($"int32 m_nUserVersion = {UserVersion}");
                w.WriteLine($"Struct m_EnumValueIntrospection[{EnumValueIntrospection.Count}] = ["); w.Indent++;
                foreach (var value in EnumValueIntrospection) value.WriteText(w);
                w.Indent--; w.WriteLine("]");
                w.Indent--; w.WriteLine("}");
            }
        }

        public uint IntrospectionVersion { get; private set; }

        public List<ResourceDiskStruct> ReferencedStructs { get; } = new List<ResourceDiskStruct>();
        public List<ResourceDiskEnum> ReferencedEnums { get; } = new List<ResourceDiskEnum>();

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            IntrospectionVersion = r.ReadUInt32();
            ReadStructs(r);
            r.BaseStream.Position = Offset + 12; // skip 3 ints
            ReadEnums(r);
        }

        void ReadStructs(BinaryReader r)
        {
            var entriesOffset = r.ReadUInt32();
            var entriesCount = r.ReadUInt32();
            if (entriesCount == 0) return;

            r.BaseStream.Position += entriesOffset - 8; // offset minus 2 ints we just read
            for (var i = 0; i < entriesCount; i++)
            {
                var diskStruct = new ResourceDiskStruct
                {
                    IntrospectionVersion = r.ReadUInt32(),
                    Id = r.ReadUInt32(),
                    Name = r.ReadO32UTF8(),
                    DiskCrc = r.ReadUInt32(),
                    UserVersion = r.ReadInt32(),
                    DiskSize = r.ReadUInt16(),
                    Alignment = r.ReadUInt16(),
                    BaseStructId = r.ReadUInt32()
                };

                var fieldsOffset = r.ReadUInt32();
                var fieldsSize = r.ReadUInt32();
                if (fieldsSize > 0)
                {
                    var prev = r.BaseStream.Position;
                    r.BaseStream.Position += fieldsOffset - 8; // offset minus 2 ints we just read

                    for (var y = 0; y < fieldsSize; y++)
                    {
                        var field = new ResourceDiskStruct.Field
                        {
                            FieldName = r.ReadO32UTF8(),
                            Count = r.ReadInt16(),
                            OnDiskOffset = r.ReadInt16()
                        };

                        var indirectionOffset = r.ReadUInt32();
                        var indirectionSize = r.ReadUInt32();
                        if (indirectionSize > 0)
                        {
                            // jump to indirections
                            var prev2 = r.BaseStream.Position;
                            r.BaseStream.Position += indirectionOffset - 8; // offset minus 2 ints we just read
                            for (var x = 0; x < indirectionSize; x++)
                                field.Indirections.Add(r.ReadByte());
                            r.BaseStream.Position = prev2;
                        }
                        field.TypeData = r.ReadUInt32();
                        field.Type = (SchemaFieldType)r.ReadInt16();
                        field.Unknown = r.ReadUInt16();
                        diskStruct.FieldIntrospection.Add(field);
                    }
                    r.BaseStream.Position = prev;
                }

                diskStruct.StructFlags = r.ReadByte();
                diskStruct.Unknown = r.ReadUInt16();
                diskStruct.Unknown2 = r.ReadByte();
                ReferencedStructs.Add(diskStruct);
            }
        }

        void ReadEnums(BinaryReader r)
        {
            var entriesOffset = r.ReadUInt32();
            var entriesCount = r.ReadUInt32();
            if (entriesCount == 0) return;

            r.BaseStream.Position += entriesOffset - 8; // offset minus 2 ints we just read
            for (var i = 0; i < entriesCount; i++)
            {
                var diskEnum = new ResourceDiskEnum
                {
                    IntrospectionVersion = r.ReadUInt32(),
                    Id = r.ReadUInt32(),
                    Name = r.ReadO32UTF8(),
                    DiskCrc = r.ReadUInt32(),
                    UserVersion = r.ReadInt32()
                };

                var fieldsOffset = r.ReadUInt32();
                var fieldsSize = r.ReadUInt32();
                if (fieldsSize > 0)
                {
                    var prev = r.BaseStream.Position;
                    r.BaseStream.Position += fieldsOffset - 8; // offset minus 2 ints we just read
                    for (var y = 0; y < fieldsSize; y++) diskEnum.EnumValueIntrospection.Add(new ResourceDiskEnum.Value { EnumValueName = r.ReadO32UTF8(), EnumValue = r.ReadInt32() });
                    r.BaseStream.Position = prev;
                }
                ReferencedEnums.Add(diskEnum);
            }
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine("CResourceIntrospectionManifest {"); w.Indent++;
            w.WriteLine($"uint32 m_nIntrospectionVersion = 0x{IntrospectionVersion:x8}");
            w.WriteLine($"Struct m_ReferencedStructs[{ReferencedStructs.Count}] = ["); w.Indent++;
            foreach (var refStruct in ReferencedStructs) refStruct.WriteText(w);
            w.Indent--; w.WriteLine("]");
            w.WriteLine($"Struct m_ReferencedEnums[{ReferencedEnums.Count}] = ["); w.Indent++;
            foreach (var refEnum in ReferencedEnums) refEnum.WriteText(w);
            w.Indent--; w.WriteLine("]");
            w.Indent--; w.WriteLine("}");
        }
    }

    #endregion

    #region Block : AGRP

    /// <summary>
    /// "AGRP" block.
    /// </summary>
    public class AGRP : XKV3_NTRO
    {
        public AGRP() : base("AnimationGroupResourceData_t") { }
    }

    #endregion

    #region Block : ANIM

    /// <summary>
    /// "ANIM" block.
    /// </summary>
    public class ANIM : XKV3_NTRO
    {
        public ANIM() : base("AnimationResourceData_t") { }
    }

    #endregion

    #region Block : ASEQ

    /// <summary>
    /// "ASEQ" block.
    /// </summary>
    public class ASEQ : XKV3_NTRO
    {
        public ASEQ() : base("SequenceGroupResourceData_t") { }
    }

    #endregion

    #region Block : CTRL

    /// <summary>
    /// "CTRL" block.
    /// </summary>
    public class CTRL : XKV3 { }

    #endregion

    #region Block : DATA
    //was:Resource/Blocks/ResourceData

    /// <summary>
    /// "DATA" block.
    /// </summary>
    public class DATA : Block
    {
        public IDictionary<string, object> AsKeyValue()
        {
            if (this is D_NTRO ntro) return ntro.Data;
            else if (this is XKV3 kv3) return kv3.Data;
            return default;
        }

        public override void Read(Binary_Src parent, BinaryReader r) { }
    }

    #endregion

    #region Block : INSG

    /// <summary>
    /// "INSG" block.
    /// </summary>
    public class INSG : XKV3 { }

    #endregion

    #region Block : LACO

    /// <summary>
    /// "LACO" block.
    /// </summary>
    public class LACO : XKV3 { }

    #endregion

    #region Block : MBUF

    /// <summary>
    /// "MBUF" block.
    /// </summary>
    public class MBUF : VBIB { }

    #endregion

    #region Block : MDAT

    /// <summary>
    /// "MDAT" block.
    /// </summary>
    public class MDAT : XKV3 { }

    #endregion

    #region Block : MRPH

    /// <summary>
    /// "MRPH" block.
    /// </summary>
    public class MRPH : XKV3_NTRO
    {
        public MRPH() : base("MorphSetData_t") { }
    }

    #endregion

    #region Block : PHYS

    /// <summary>
    /// "PHYS" block.
    /// </summary>
    public class PHYS : XKV3_NTRO
    {
        public PHYS() : base("VPhysXAggregateData_t") { }
    }

    #endregion

    #region Block : RED2

    /// <summary>
    /// "RED2" block. CResourceEditInfo.
    /// </summary>
    public class RED2 : REDI
    {
        /// <summary>
        /// This is not a real Valve enum, it's just the order they appear in.
        /// </summary>
        public XKV3 BackingData;

        public IDictionary<string, object> SearchableUserData { get; private set; }

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            var kv3 = new XKV3
            {
                Offset = Offset,
                Size = Size,
            };
            kv3.Read(parent, r);
            BackingData = kv3;

            ConstructSpecialDependencies();
            ConstuctInputDependencies();

            SearchableUserData = kv3.Data.GetSub("m_SearchableUserData");
            //foreach (var kv in kv3.Data) { } //var structType = ConstructStruct(kv.Key);
        }

        public override void WriteText(IndentedTextWriter w)
           => BackingData.WriteText(w);

        void ConstructSpecialDependencies()
        {
            var specialDependenciesRedi = new R_SpecialDependencies();
            foreach (var specialDependency in BackingData.Data.GetArray("m_SpecialDependencies"))
                specialDependenciesRedi.List.Add(new R_SpecialDependencies.SpecialDependency
                {
                    String = specialDependency.Get<string>("m_String"),
                    CompilerIdentifier = specialDependency.Get<string>("m_CompilerIdentifier"),
                    Fingerprint = specialDependency.GetUInt32("m_nFingerprint"),
                    UserData = specialDependency.GetUInt32("m_nUserData"),
                });
            Structs.Add(REDIStruct.SpecialDependencies, specialDependenciesRedi);
        }

        void ConstuctInputDependencies()
        {
            var dependenciesRedi = new R_InputDependencies();
            foreach (var dependency in BackingData.Data.GetArray("m_InputDependencies"))
                dependenciesRedi.List.Add(new R_InputDependencies.InputDependency
                {
                    ContentRelativeFilename = dependency.Get<string>("m_RelativeFilename"),
                    ContentSearchPath = dependency.Get<string>("m_SearchPath"),
                    FileCRC = dependency.GetUInt32("m_nFileCRC"),
                });
            Structs.Add(REDIStruct.InputDependencies, dependenciesRedi);
        }
    }

    #endregion

    #region Block : REDI

    /// <summary>
    /// "REDI" block. ResourceEditInfoBlock_t.
    /// </summary>
    public class REDI : Block
    {
        /// <summary>
        /// This is not a real Valve enum, it's just the order they appear in.
        /// </summary>
        public enum REDIStruct
        {
            InputDependencies,
            AdditionalInputDependencies,
            ArgumentDependencies,
            SpecialDependencies,
            CustomDependencies,
            AdditionalRelatedFiles,
            ChildResourceList,
            ExtraIntData,
            ExtraFloatData,
            ExtraStringData,
            End,
        }

        public Dictionary<REDIStruct, REDI> Structs { get; private set; } = new Dictionary<REDIStruct, REDI>();

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            for (var i = REDIStruct.InputDependencies; i < REDIStruct.End; i++)
            {
                var block = REDIFactory(i);
                block.Offset = (uint)r.BaseStream.Position + r.ReadUInt32();
                block.Size = r.ReadUInt32();
                Structs.Add(i, block);
            }
            foreach (var block in Structs) block.Value.Read(parent, r);
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine("ResourceEditInfoBlock_t {"); w.Indent++;
            foreach (var dep in Structs) dep.Value.WriteText(w);
            w.Indent--; w.WriteLine("}");
        }

        static REDI REDIFactory(REDIStruct id)
            => id switch
            {
                REDIStruct.InputDependencies => new R_InputDependencies(),
                REDIStruct.AdditionalInputDependencies => new R_AdditionalInputDependencies(),
                REDIStruct.ArgumentDependencies => new R_ArgumentDependencies(),
                REDIStruct.SpecialDependencies => new R_SpecialDependencies(),
                REDIStruct.CustomDependencies => new R_CustomDependencies(),
                REDIStruct.AdditionalRelatedFiles => new R_AdditionalRelatedFiles(),
                REDIStruct.ChildResourceList => new R_ChildResourceList(),
                REDIStruct.ExtraIntData => new R_ExtraIntData(),
                REDIStruct.ExtraFloatData => new R_ExtraFloatData(),
                REDIStruct.ExtraStringData => new R_ExtraStringData(),
                _ => throw new InvalidDataException("Unknown struct in REDI block."),
            };
    }

    #endregion

    #region Block : RERL

    /// <summary>
    /// "RERL" block. ResourceExtRefList_t.
    /// </summary>
    public class RERL : Block
    {
        public class RERLInfo
        {
            /// <summary>
            /// Gets or sets the resource id.
            /// </summary>
            public ulong Id { get; set; }

            /// <summary>
            /// Gets or sets the resource name.
            /// </summary>
            public string Name { get; set; }

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("ResourceReferenceInfo_t {"); w.Indent++;
                w.WriteLine($"uint64 m_nId = 0x{Id:X16}");
                w.WriteLine($"CResourceString m_pResourceName = \"{Name}\"");
                w.Indent--; w.WriteLine("}");
            }
        }

        public IList<RERLInfo> RERLInfos { get; private set; } = new List<RERLInfo>();

        public string this[ulong id] => RERLInfos.FirstOrDefault(c => c.Id == id)?.Name;

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            var offset = r.ReadUInt32();
            var size = r.ReadUInt32();
            if (size == 0) return;

            r.Skip(offset - 8); // 8 is 2 uint32s we just read
            for (var i = 0; i < size; i++)
            {
                var id = r.ReadUInt64();
                var previousPosition = r.BaseStream.Position;
                // jump to string: offset is counted from current position, so we will need to add 8 to position later
                r.BaseStream.Position += r.ReadInt64();
                RERLInfos.Add(new RERLInfo { Id = id, Name = r.ReadVUString() });
                r.BaseStream.Position = previousPosition + 8; // 8 is to account for string offset
            }
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine("ResourceExtRefList_t {"); w.Indent++;
            w.WriteLine($"Struct m_resourceRefInfoList[{RERLInfos.Count}] = ["); w.Indent++;
            foreach (var refInfo in RERLInfos) refInfo.WriteText(w);
            w.Indent--; w.WriteLine("]");
            w.Indent--; w.WriteLine("}");
        }
    }

    #endregion

    #region Block : SRMA

    /// <summary>
    /// "SRMA" block.
    /// </summary>
    public class SRMA : XKV3 { }

    #endregion

    #region Block : SNAP
    //was:Resource/Blocks/SNAP

    /// <summary>
    /// "SNAP" block.
    /// </summary>
    public class SNAP : Block
    {
        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            throw new NotImplementedException();
        }
    }

    #endregion

    #region Block : VBIB
    //was:Resource/Blocks/VBIB

    /// <summary>
    /// "VBIB" block.
    /// </summary>
    public class VBIB : Block, IVBIB
    {
        public List<OnDiskBufferData> VertexBuffers { get; }
        public List<OnDiskBufferData> IndexBuffers { get; }

        public VBIB()
        {
            VertexBuffers = [];
            IndexBuffers = [];
        }

        public VBIB(IDictionary<string, object> data) : this()
        {
            var vertexBuffers = data.GetArray("m_vertexBuffers");
            foreach (var vb in vertexBuffers)
            {
                var vertexBuffer = BufferDataFromDATA(vb);
                var decompressedSize = vertexBuffer.ElementCount * vertexBuffer.ElementSizeInBytes;
                if (vertexBuffer.Data.Length != decompressedSize) vertexBuffer.Data = MeshOptimizerVertexDecoder.DecodeVertexBuffer((int)vertexBuffer.ElementCount, (int)vertexBuffer.ElementSizeInBytes, vertexBuffer.Data);
                VertexBuffers.Add(vertexBuffer);
            }
            var indexBuffers = data.GetArray("m_indexBuffers");
            foreach (var ib in indexBuffers)
            {
                var indexBuffer = BufferDataFromDATA(ib);
                var decompressedSize = indexBuffer.ElementCount * indexBuffer.ElementSizeInBytes;
                if (indexBuffer.Data.Length != decompressedSize) indexBuffer.Data = MeshOptimizerIndexDecoder.DecodeIndexBuffer((int)indexBuffer.ElementCount, (int)indexBuffer.ElementSizeInBytes, indexBuffer.Data);
                IndexBuffers.Add(indexBuffer);
            }
        }

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            var vertexBufferOffset = r.ReadUInt32();
            var vertexBufferCount = r.ReadUInt32();
            var indexBufferOffset = r.ReadUInt32();
            var indexBufferCount = r.ReadUInt32();

            r.Seek(Offset + vertexBufferOffset);
            for (var i = 0; i < vertexBufferCount; i++)
            {
                var vertexBuffer = ReadOnDiskBufferData(r);
                var decompressedSize = vertexBuffer.ElementCount * vertexBuffer.ElementSizeInBytes;
                if (vertexBuffer.Data.Length != decompressedSize) vertexBuffer.Data = MeshOptimizerVertexDecoder.DecodeVertexBuffer((int)vertexBuffer.ElementCount, (int)vertexBuffer.ElementSizeInBytes, vertexBuffer.Data);
                VertexBuffers.Add(vertexBuffer);
            }

            r.Seek(Offset + 8 + indexBufferOffset); // 8 to take into account vertexOffset / count
            for (var i = 0; i < indexBufferCount; i++)
            {
                var indexBuffer = ReadOnDiskBufferData(r);
                var decompressedSize = indexBuffer.ElementCount * indexBuffer.ElementSizeInBytes;
                if (indexBuffer.Data.Length != decompressedSize) indexBuffer.Data = MeshOptimizerIndexDecoder.DecodeIndexBuffer((int)indexBuffer.ElementCount, (int)indexBuffer.ElementSizeInBytes, indexBuffer.Data);
                IndexBuffers.Add(indexBuffer);
            }
        }

        static OnDiskBufferData ReadOnDiskBufferData(BinaryReader r)
        {
            var buffer = default(OnDiskBufferData);

            buffer.ElementCount = r.ReadUInt32();            //0
            buffer.ElementSizeInBytes = r.ReadUInt32();      //4

            var refA = r.BaseStream.Position;
            var attributeOffset = r.ReadUInt32();  //8
            var attributeCount = r.ReadUInt32();   //12

            var refB = r.BaseStream.Position;
            var dataOffset = r.ReadUInt32();       //16
            var totalSize = r.ReadInt32();        //20

            r.Seek(refA + attributeOffset);
            buffer.Attributes = Enumerable.Range(0, (int)attributeCount)
                .Select(j =>
                {
                    var attribute = default(OnDiskBufferData.Attribute);
                    var previousPosition = r.BaseStream.Position;
                    attribute.SemanticName = r.ReadVUString().ToUpperInvariant(); //32 bytes long null-terminated string
                    r.BaseStream.Position = previousPosition + 32; // Offset is always 40 bytes from the start
                    attribute.SemanticIndex = r.ReadInt32();
                    attribute.Format = (DXGI_FORMAT)r.ReadUInt32();
                    attribute.Offset = r.ReadUInt32();
                    attribute.Slot = r.ReadInt32();
                    attribute.SlotType = (OnDiskBufferData.RenderSlotType)r.ReadUInt32();
                    attribute.InstanceStepRate = r.ReadInt32();
                    return attribute;
                }).ToArray();

            r.Seek(refB + dataOffset);
            buffer.Data = r.ReadBytes(totalSize); //can be compressed

            r.Seek(refB + 8); //Go back to the index array to read the next iteration.
            return buffer;
        }

        static OnDiskBufferData BufferDataFromDATA(IDictionary<string, object> data)
        {
            var buffer = new OnDiskBufferData
            {
                ElementCount = data.GetUInt32("m_nElementCount"),
                ElementSizeInBytes = data.GetUInt32("m_nElementSizeInBytes"),
            };

            var inputLayoutFields = data.GetArray("m_inputLayoutFields");
            buffer.Attributes = inputLayoutFields.Select(il => new OnDiskBufferData.Attribute
            {
                //null-terminated string
                SemanticName = Encoding.UTF8.GetString(il.Get<byte[]>("m_pSemanticName")).TrimEnd((char)0),
                SemanticIndex = il.GetInt32("m_nSemanticIndex"),
                Format = (DXGI_FORMAT)il.GetUInt32("m_Format"),
                Offset = il.GetUInt32("m_nOffset"),
                Slot = il.GetInt32("m_nSlot"),
                SlotType = (OnDiskBufferData.RenderSlotType)il.GetUInt32("m_nSlotType"),
                InstanceStepRate = il.GetInt32("m_nInstanceStepRate")
            }).ToArray();

            buffer.Data = data.Get<byte[]>("m_pData");
            return buffer;
        }

        public static float[] ReadVertexAttribute(int offset, OnDiskBufferData vertexBuffer, OnDiskBufferData.Attribute attribute)
        {
            offset = (int)(offset * vertexBuffer.ElementSizeInBytes) + (int)attribute.Offset;
            // Useful reference: https://github.com/apitrace/dxsdk/blob/master/Include/d3dx_dxgiformatconvert.inl
            float[] result;
            switch (attribute.Format)
            {
                case DXGI_FORMAT.R32G32B32_FLOAT:
                    {
                        result = new float[3];
                        Buffer.BlockCopy(vertexBuffer.Data, offset, result, 0, 12);
                        return result;
                    }
                case DXGI_FORMAT.R32G32B32A32_FLOAT:
                    {
                        result = new float[4];
                        Buffer.BlockCopy(vertexBuffer.Data, offset, result, 0, 16);
                        return result;
                    }
                case DXGI_FORMAT.R16G16_UNORM:
                    {
                        var shorts = new ushort[2];
                        Buffer.BlockCopy(vertexBuffer.Data, offset, shorts, 0, 4);
                        result = [(float)shorts[0] / ushort.MaxValue, (float)shorts[1] / ushort.MaxValue];
                        return result;
                    }
                case DXGI_FORMAT.R16G16_SNORM:
                    {
                        var shorts = new short[2];
                        Buffer.BlockCopy(vertexBuffer.Data, offset, shorts, 0, 4);
                        result = [(float)shorts[0] / short.MaxValue, (float)shorts[1] / short.MaxValue];
                        return result;
                    }
                case DXGI_FORMAT.R16G16_FLOAT:
                    {
                        result = [(float)BitConverterX.ToHalf(vertexBuffer.Data, offset), (float)BitConverterX.ToHalf(vertexBuffer.Data, offset + 2)];
                        return result;
                    }
                case DXGI_FORMAT.R32_FLOAT:
                    {
                        result = new float[1];
                        Buffer.BlockCopy(vertexBuffer.Data, offset, result, 0, 4);
                        return result;
                    }
                case DXGI_FORMAT.R32G32_FLOAT:
                    {
                        result = new float[2];
                        Buffer.BlockCopy(vertexBuffer.Data, offset, result, 0, 8);
                        return result;
                    }
                case DXGI_FORMAT.R16G16_SINT:
                    {
                        var shorts = new short[2];
                        Buffer.BlockCopy(vertexBuffer.Data, offset, shorts, 0, 4);
                        result = new float[2];
                        for (var i = 0; i < 2; i++) result[i] = shorts[i];
                        return result;
                    }
                case DXGI_FORMAT.R16G16B16A16_SINT:
                    {
                        var shorts = new short[4];
                        Buffer.BlockCopy(vertexBuffer.Data, offset, shorts, 0, 8);
                        result = new float[4];
                        for (var i = 0; i < 4; i++) result[i] = shorts[i];
                        return result;
                    }
                case DXGI_FORMAT.R8G8B8A8_UINT:
                case DXGI_FORMAT.R8G8B8A8_UNORM:
                    {
                        var bytes = new byte[4];
                        Buffer.BlockCopy(vertexBuffer.Data, offset, bytes, 0, 4);
                        result = new float[4];
                        for (var i = 0; i < 4; i++) result[i] = attribute.Format == DXGI_FORMAT.R8G8B8A8_UNORM ? (float)bytes[i] / byte.MaxValue : bytes[i];
                        return result;
                    }
                default: throw new NotImplementedException($"Unsupported \"{attribute.SemanticName}\" DXGI_FORMAT.{attribute.Format}");
            }
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine("Vertex buffers:");
            foreach (var vertexBuffer in VertexBuffers)
            {
                w.WriteLine($"Count: {vertexBuffer.ElementCount}");
                w.WriteLine($"Size: {vertexBuffer.ElementSizeInBytes}");
                for (var i = 0; i < vertexBuffer.Attributes.Length; i++)
                {
                    var vertexAttribute = vertexBuffer.Attributes[i];
                    w.WriteLine($"Attribute[{i}]"); w.Indent++;
                    w.WriteLine($"SemanticName = {vertexAttribute.SemanticName}");
                    w.WriteLine($"SemanticIndex = {vertexAttribute.SemanticIndex}");
                    w.WriteLine($"Offsets = {vertexAttribute.Offset}");
                    w.WriteLine($"Format = {vertexAttribute.Format}");
                    w.WriteLine($"Slot = {vertexAttribute.Slot}");
                    w.WriteLine($"SlotType = {vertexAttribute.SlotType}");
                    w.WriteLine($"InstanceStepRate = {vertexAttribute.InstanceStepRate}"); w.Indent--;
                }
                w.WriteLine();
            }
            w.WriteLine();
            w.WriteLine("Index buffers:");
            foreach (var indexBuffer in IndexBuffers)
            {
                w.WriteLine($"Count: {indexBuffer.ElementCount}");
                w.WriteLine($"Size: {indexBuffer.ElementSizeInBytes}");
                w.WriteLine();
            }
        }

        static (int ElementSize, int ElementCount) GetFormatInfo(OnDiskBufferData.Attribute attribute)
            => attribute.Format switch
            {
                DXGI_FORMAT.R32G32B32_FLOAT => (4, 3),
                DXGI_FORMAT.R32G32B32A32_FLOAT => (4, 4),
                DXGI_FORMAT.R16G16_UNORM => (2, 2),
                DXGI_FORMAT.R16G16_SNORM => (2, 2),
                DXGI_FORMAT.R16G16_FLOAT => (2, 2),
                DXGI_FORMAT.R32_FLOAT => (4, 1),
                DXGI_FORMAT.R32G32_FLOAT => (4, 2),
                DXGI_FORMAT.R16G16_SINT => (2, 2),
                DXGI_FORMAT.R16G16B16A16_SINT => (2, 4),
                DXGI_FORMAT.R8G8B8A8_UINT => (1, 4),
                DXGI_FORMAT.R8G8B8A8_UNORM => (1, 4),
                _ => throw new NotImplementedException($"Unsupported \"{attribute.SemanticName}\" DXGI_FORMAT.{attribute.Format}"),
            };

        public static int[] CombineRemapTables(int[][] remapTables)
        {
            remapTables = remapTables.Where(remapTable => remapTable.Length != 0).ToArray();
            var newRemapTable = remapTables[0].AsEnumerable();
            for (var i = 1; i < remapTables.Length; i++)
            {
                var remapTable = remapTables[i];
                newRemapTable = newRemapTable.Select(j => j != -1 ? remapTable[j] : -1);
            }
            return newRemapTable.ToArray();
        }

        public IVBIB RemapBoneIndices(int[] remapTable)
        {
            var res = new VBIB();
            res.VertexBuffers.AddRange(VertexBuffers.Select(buf =>
            {
                var blendIndices = Array.FindIndex(buf.Attributes, field => field.SemanticName == "BLENDINDICES");
                if (blendIndices != -1)
                {
                    var field = buf.Attributes[blendIndices];
                    var (formatElementSize, formatElementCount) = GetFormatInfo(field);
                    var formatSize = formatElementSize * formatElementCount;
                    buf.Data = buf.Data.ToArray();
                    var bufSpan = buf.Data.AsSpan();
                    for (var i = (int)field.Offset; i < buf.Data.Length; i += (int)buf.ElementSizeInBytes)
                        for (var j = 0; j < formatSize; j += formatElementSize)
                        {
                            switch (formatElementSize)
                            {
                                case 4:
                                    BitConverter.TryWriteBytes(bufSpan.Slice(i + j), remapTable[BitConverter.ToUInt32(buf.Data, i + j)]);
                                    break;
                                case 2:
                                    BitConverter.TryWriteBytes(bufSpan.Slice(i + j), (short)remapTable[BitConverter.ToUInt16(buf.Data, i + j)]);
                                    break;
                                case 1:
                                    buf.Data[i + j] = (byte)remapTable[buf.Data[i + j]];
                                    break;
                                default: throw new NotImplementedException();
                            }
                        }
                }
                return buf;
            }));
            res.IndexBuffers.AddRange(IndexBuffers);
            return res;
        }
    }

    #endregion

    #region Block : VXVS
    //was:Resource/Blocks/VXVS

    /// <summary>
    /// "VXVS" block.
    /// </summary>
    public class VXVS : Block
    {
        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            throw new NotImplementedException();
        }

        public override void WriteText(IndentedTextWriter w)
            => w.WriteLine("{0:X8}", Offset);
    }

    #endregion

    #region D_EntityLump
    //was:Resource/ResourceTypes/EntityLump

    public class D_EntityLump : XKV3_NTRO
    {
        public enum EntityFieldType : uint //was:Resource/Enums/EntityFieldType
        {
            Void = 0x0,
            Float = 0x1,
            String = 0x2,
            Vector = 0x3,
            Quaternion = 0x4,
            Integer = 0x5,
            Boolean = 0x6,
            Short = 0x7,
            Character = 0x8,
            Color32 = 0x9,
            Embedded = 0xa,
            Custom = 0xb,
            ClassPtr = 0xc,
            EHandle = 0xd,
            PositionVector = 0xe,
            Time = 0xf,
            Tick = 0x10,
            SoundName = 0x11,
            Input = 0x12,
            Function = 0x13,
            VMatrix = 0x14,
            VMatrixWorldspace = 0x15,
            Matrix3x4Worldspace = 0x16,
            Interval = 0x17,
            Unused = 0x18,
            Vector2d = 0x19,
            Integer64 = 0x1a,
            Vector4D = 0x1b,
            Resource = 0x1c,
            TypeUnknown = 0x1d,
            CString = 0x1e,
            HScript = 0x1f,
            Variant = 0x20,
            UInt64 = 0x21,
            Float64 = 0x22,
            PositiveIntegerOrNull = 0x23,
            HScriptNewInstance = 0x24,
            UInt = 0x25,
            UtlStringToken = 0x26,
            QAngle = 0x27,
            NetworkOriginCellQuantizedVector = 0x28,
            HMaterial = 0x29,
            HModel = 0x2a,
            NetworkQuantizedVector = 0x2b,
            NetworkQuantizedFloat = 0x2c,
            DirectionVectorWorldspace = 0x2d,
            QAngleWorldspace = 0x2e,
            QuaternionWorldspace = 0x2f,
            HScriptLightbinding = 0x30,
            V8_value = 0x31,
            V8_object = 0x32,
            V8_array = 0x33,
            V8_callback_info = 0x34,
            UtlString = 0x35,
            NetworkOriginCellQuantizedPositionVector = 0x36,
            HRenderTexture = 0x37,
        }

        public class Entity
        {
            public Dictionary<uint, EntityProperty> Properties { get; } = [];
            public List<IDictionary<string, object>> Connections { get; internal set; }
            public T Get<T>(string name) => Get<T>(StringToken.Get(name));
            public EntityProperty Get(string name) => Get(StringToken.Get(name));
            public T Get<T>(uint hash) => Properties.TryGetValue(hash, out var property) ? (T)property.Data : default;
            public EntityProperty Get(uint hash) => Properties.TryGetValue(hash, out var property) ? property : default;
        }

        public class EntityProperty
        {
            public EntityFieldType Type { get; set; }
            public string Name { get; set; }
            public object Data { get; set; }
        }

        public IEnumerable<string> GetChildEntityNames() => Data.Get<string[]>("m_childLumps");

        public IEnumerable<Entity> GetEntities() => Data.GetArray("m_entityKeyValues").Select(entity => ParseEntityProperties(entity.Get<byte[]>("m_keyValuesData"), entity.GetArray("m_connections"))).ToList();

        static Entity ParseEntityProperties(byte[] bytes, IDictionary<string, object>[] connections)
        {
            using var s = new MemoryStream(bytes);
            using var r = new BinaryReader(s);
            var a = r.ReadUInt32(); // always 1?
            if (a != 1) throw new NotImplementedException($"First field in entity lump is not 1");
            var hashedFieldsCount = r.ReadUInt32();
            var stringFieldsCount = r.ReadUInt32();
            var entity = new Entity();
            void ReadTypedValue(uint keyHash, string keyName)
            {
                var type = (EntityFieldType)r.ReadUInt32();
                var entityProperty = new EntityProperty
                {
                    Type = type,
                    Name = keyName,
                    Data = type switch
                    {
                        EntityFieldType.Boolean => r.ReadBoolean(),
                        EntityFieldType.Float => r.ReadSingle(),
                        EntityFieldType.Color32 => r.ReadBytes(4),
                        EntityFieldType.Integer => r.ReadInt32(),
                        EntityFieldType.UInt => r.ReadUInt32(),
                        EntityFieldType.Integer64 => r.ReadUInt64(),
                        var x when x == EntityFieldType.Vector || x == EntityFieldType.QAngle => new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()),
                        EntityFieldType.CString => r.ReadVUString(), // null term variable
                        _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unknown type {type}"),
                    }
                };
                entity.Properties.Add(keyHash, entityProperty);
            }
            for (var i = 0; i < hashedFieldsCount; i++) ReadTypedValue(r.ReadUInt32(), null); // murmur2 hashed field name (see EntityLumpKeyLookup)
            for (var i = 0; i < stringFieldsCount; i++) ReadTypedValue(r.ReadUInt32(), r.ReadVUString());
            if (connections.Length > 0) entity.Connections = connections.ToList();
            return entity;
        }

        public override string ToString()
        {
            var knownKeys = StringToken.InvertedTable;
            var b = new StringBuilder();
            var unknownKeys = new Dictionary<uint, uint>();

            var index = 0;
            foreach (var entity in GetEntities())
            {
                b.AppendLine($"===={index++}====");
                foreach (var property in entity.Properties)
                {
                    var value = property.Value.Data;
                    if (value.GetType() == typeof(byte[]))
                    {
                        var tmp = value as byte[];
                        value = $"Array [{string.Join(", ", tmp.Select(p => p.ToString(CultureInfo.InvariantCulture)).ToArray())}]";
                    }
                    string key;
                    if (knownKeys.TryGetValue(property.Key, out var knownKey)) key = knownKey;
                    else if (property.Value.Name != null) key = property.Value.Name;
                    else
                    {
                        key = $"key={property.Key}";
                        if (!unknownKeys.ContainsKey(property.Key)) unknownKeys.Add(property.Key, 1);
                        else unknownKeys[property.Key]++;
                    }
                    b.AppendLine($"{key,-30} {property.Value.Type.ToString(),-10} {value}");
                }

                if (entity.Connections != null)
                    foreach (var connection in entity.Connections)
                    {
                        b.Append('@'); b.Append(connection.Get<string>("m_outputName")); b.Append(' ');
                        var delay = connection.GetFloat("m_flDelay");
                        if (delay > 0) b.Append($"Delay={delay} ");
                        var timesToFire = connection.GetInt32("m_nTimesToFire");
                        if (timesToFire == 1) b.Append("OnlyOnce ");
                        else if (timesToFire != -1) throw new ArgumentOutOfRangeException(nameof(timesToFire), $"Unexpected times to fire {timesToFire}");
                        b.Append(connection.Get<string>("m_inputName")); b.Append(' '); b.Append(connection.Get<string>("m_targetName"));
                        var param = connection.Get<string>("m_overrideParam");
                        if (!string.IsNullOrEmpty(param) && param != "(null)") { b.Append(' '); b.Append(param); }
                        b.AppendLine();
                    }

                b.AppendLine();
            }

            if (unknownKeys.Count > 0)
            {
                b.AppendLine($"@@@@@ UNKNOWN KEY LOOKUPS:");
                b.AppendLine($"If you know what these are, add them to EntityLumpKnownKeys.cs");
                foreach (var unknownKey in unknownKeys) b.AppendLine($"key={unknownKey.Key} hits={unknownKey.Value}");
            }
            return b.ToString();
        }

        #region StringToken

        //was:Utils/StringToken
        public static class StringToken
        {
            public const uint MURMUR2SEED = 0x31415926; // PI
            static readonly ConcurrentDictionary<string, uint> Lookup = new();
            public static Dictionary<uint, string> InvertedTable
            {
                get
                {
                    var inverted = new Dictionary<uint, string>(Lookup.Count);
                    foreach (var (key, hash) in Lookup) inverted[hash] = key;
                    return inverted;
                }
            }
            public static uint Get(string key) => Lookup.GetOrAdd(key, s => MurmurHash2.Hash(s, MURMUR2SEED));
            static StringToken()
            {
                // MUST BE LOWERCASE!!
                foreach (var field in new List<string> {
                "_ambient",
                "_ambienthdr",
                "_ambientscalehdr",
                "_cone",
                "_constant_attn",
                "_distance",
                "_dotatilegrid_fogbounds_max",
                "_dotatilegrid_fogbounds_min",
                "_dotatilegrid_heightshift",
                "_dotatilegrid_maxheight",
                "_dotatilegrid_minheight",
                "_dotatilegrid_stepheight",
                "_exponent",
                "_fifty_percent_distance",
                "_hardfalloff",
                "_hole_far_r",
                "_inner_cone",
                "_light",
                "_lighthdr",
                "_lightscalehdr",
                "_linear_attn",
                "_minlight",
                "_quadratic_attn",
                "_template_lump_ent_index",
                "_zero_percent_distance",
                "aabbdirection",
                "abandonifenemyhides",
                "ability_remove_1",
                "ability_remove_2",
                "ability_remove_3",
                "ability_remove_4",
                "acceleration",
                "accelerationscalar",
                "accent",
                "acceptdamagefromheldobjects",
                "accumulate",
                "accumulatelevel",
                "achievementevent",
                "act_as_flyer",
                "action_point",
                "actionondeath",
                "actionscale",
                "activated",
                "activateprefab",
                "activatespawn",
                "activatespawnerprefab",
                "activatespawnprefab",
                "activationsettings",
                "activatorasuserid",
                "active_combo_name",
                "active",
                "activecamera",
                "activity_modifier",
                "activity_name",
                "activity",
                "actor",
                "actorinpvs",
                "actorinvehicle",
                "actorname",
                "actorname2",
                "actorname3",
                "actorseeplayer",
                "actorseetarget",
                "actortargetproximity",
                "actvationtags",
                "add_to_spatial_partition",
                "add",
                "additionalequipment",
                "additionaliterations",
                "addlength",
                "addon",
                "addonmapcommand",
                "addonmapcommandisaddonimplied",
                "addonname",
                "addonpoints",
                "addonschangedefaultwritepath",
                "addtospatialpartition",
                "adrenalinepresence",
                "affectedbywind",
                "affectsflow",
                "aggressiveness",
                "aggrotype",
                "ai_node_dont_drop",
                "aigeneric",
                "aihull_human",
                "aihull_large_centered",
                "aihull_large",
                "aihull_medium_tall",
                "aihull_medium",
                "aihull_small_centered",
                "aihull_tiny_centered",
                "aihull_tiny",
                "aihull_wide_human",
                "aim_offset",
                "aim_target",
                "aiproperty",
                "airboat_gun_model",
                "airctrlsupressiontime",
                "aiwalkable",
                "ajar_10deg",
                "ajar_20deg",
                "ajar_30deg",
                "ajar_40deg",
                "ajar",
                "ajarangle",
                "ajarangles",
                "alarm",
                "alert_delay",
                "alertspeed",
                "align",
                "aligntoaxis",
                "allcombinekilled",
                "allow_flip",
                "allow_grav_pull",
                "allow_overhead",
                "allow_pickup_script_func",
                "allow_procedural_handpose",
                "allow_removal",
                "allow_walk_move",
                "allowaddondependencies",
                "allowbark",
                "allowclasschanges",
                "allowdemoman",
                "allowdispenser",
                "allowdiversion",
                "allowdiversionradius",
                "allowengineer",
                "allowgenericnodes",
                "allowgravitygunpull",
                "allowhaunting",
                "allowheavy",
                "allowheightfog",
                "allowmedic",
                "allownewgibs",
                "allownp2textures",
                "allowpyro",
                "allowscout",
                "allowsentry",
                "allowskip",
                "allowsniper",
                "allowsoldier",
                "allowspy",
                "allowstatic",
                "allowteleport",
                "allowteleporters",
                "allowtreewind",
                "allowuse",
                "alpha",
                "alphahaze",
                "alphahdr",
                "alphascale",
                "alternateattachname",
                "alternatefovchange",
                "alternatemodel",
                "alternateparent",
                "alternateticksfix",
                "altitude",
                "altpath",
                "always_check_depth",
                "always_run",
                "always_use_showcase_tree",
                "alwaysanimate",
                "alwaysorientup",
                "alwaystransition",
                "alwaystransmittoclient",
                "ambient_color_dawn",
                "ambient_color_day",
                "ambient_color_dusk",
                "ambient_color_night",
                "ambient_color",
                "ambient_direction_dawn",
                "ambient_direction_day",
                "ambient_direction_dusk",
                "ambient_direction_night",
                "ambient_occlusion",
                "ambient_scale_dawn",
                "ambient_scale_day",
                "ambient_scale_dusk",
                "ambient_scale_night",
                "ambient_shadow_amount",
                "ambientangles",
                "ambientcolor1",
                "ambientcolor2",
                "ambientcolor3",
                "ambienteffect",
                "ambientfx",
                "ambientscale1",
                "ambientscale2",
                "ambulance",
                "ammo_count",
                "ammo_per_clip",
                "ammo",
                "ammoamount",
                "ammobalancing_removable",
                "ammomod",
                "ammosupply",
                "ammotype",
                "amount",
                "amplitude",
                "anchor_angles",
                "anchor_position",
                "angle",
                "angled_pipe_section",
                "angleoverride",
                "angles_left",
                "angles_relative",
                "angles_right",
                "angles",
                "anglespeedthreshold",
                "anglestoface",
                "angular_damping_ratio_x",
                "angular_damping_ratio_y",
                "angular_damping_ratio_z",
                "angular_fog_max_end",
                "angular_fog_max_start",
                "angular_fog_min_end",
                "angular_fog_min_start",
                "angular_frequency_x",
                "angular_frequency_y",
                "angular_frequency_z",
                "angular_motion_x",
                "angular_motion_y",
                "angular_motion_z",
                "angular_velocity_cp",
                "angulardampingratio",
                "angulardiameter",
                "angularfrequency",
                "angularlimit",
                "anim_body2",
                "anim_bounds_max",
                "anim_bounds_min",
                "anim_face2",
                "anim_fx2",
                "anim",
                "animamounttoscan",
                "animateeveryframe",
                "animateonserver",
                "animation",
                "animationduration",
                "animationforceattraction",
                "animationtarget",
                "animationvertexattraction",
                "animgraph_entry_cmd",
                "animgraph_entry_tag",
                "animgraph_exit_cmd",
                "animgraph_exit_tag",
                "animgraph_navlink_target",
                "animgraphparameters",
                "animgraphvalue",
                "animtag",
                "anisotropy",
                "ankle_l",
                "ankle_r",
                "antliondamaged",
                "antliondied",
                "antlionspawned",
                "ao_gradient_bottom_color_day",
                "ao_gradient_bottom_color_night",
                "ao_gradient_bottom_color",
                "apcvehiclename",
                "apex",
                "apply_carry_interactions_to_constraints",
                "applyangularimpulse",
                "applyentity",
                "approach_radius",
                "area_cap_point",
                "area_time_to_cap",
                "areamax",
                "areamin",
                "arearadius",
                "arm_bone1_l",
                "arm_bone1_r",
                "arm_bone2_l",
                "arm_bone2_r",
                "arm_lower_l",
                "arm_lower_r",
                "arm_upper_l",
                "arm_upper_r",
                "armor_to_give",
                "armored_headcrab",
                "armorrechargeenabled",
                "array_index",
                "arrivalconceptmodifier",
                "aspect",
                "aspectratio",
                "assaultcue",
                "assaultdelay",
                "assaultgroup",
                "assaultpoint",
                "assaulttimeout",
                "assaulttolerance",
                "asset_accept_paths",
                "asset_optional_paths",
                "asset_path",
                "asset_preview_thumbnail_format",
                "asset_preview_thumbnail",
                "asset_reject_paths",
                "associated_player_counter",
                "associated_team_entity",
                "associatedmodel",
                "asynchronous",
                "attach1",
                "attach2",
                "attachment_offset",
                "attachment_point_cp1",
                "attachment_point",
                "attachment_type_cp1",
                "attachment_type",
                "attachment",
                "attachmentname",
                "attachpoint",
                "attack_interval",
                "attenuation",
                "attenuation0",
                "attenuation1",
                "attenuation2",
                "attractplayerconceptmodifier",
                "attribute_name",
                "attributes",
                "authoredposition",
                "auto_advance",
                "auto_convert_back_from_debris",
                "auto_countdown",
                "auto_play",
                "auto_remove_timeout",
                "auto_spawn_entities_on_conveyor",
                "auto_spawn_probability",
                "auto_start_ambient_sound",
                "auto_unragdoll_duration",
                "autoaimradius",
                "autodisable",
                "autogen_irrads_fadeaxdist",
                "autogen_irrads_fademindist",
                "autogen_irrads_softness",
                "autogen_irrads_voxelsize",
                "autogen_irrads_width",
                "autogen_irrads",
                "autogrip",
                "automaterialize",
                "autoplay",
                "autoridespeed",
                "autotime",
                "avelocity",
                "avginterval",
                "avoid_previous_choices",
                "awardtext",
                "axis",
                "b_hidegland",
                "b_left",
                "b_right",
                "b_rightroot",
                "b_rightroot001",
                "b_rightroot002",
                "b_spawnlarge",
                "b_spawnmed",
                "b_spawnnormal",
                "b",
                "back_bag_jiggle",
                "back",
                "background_character",
                "background_clear_not_required",
                "background_image",
                "background_map",
                "background",
                "backgroundbmodel",
                "backgroundmap",
                "backhippouch_0_l",
                "backhippouch_0_r",
                "backleg_0_l",
                "backleg_0_r",
                "backleg_0",
                "backleg_1_l",
                "backleg_1_r",
                "backleg_1",
                "backleg_2",
                "backlegcenter",
                "backpack_0",
                "backpack_enabled",
                "backwards_disabled",
                "backwards",
                "bad_filler_10",
                "bad_filler_11",
                "bad_filler_12",
                "bad_filler_13",
                "bad_filler_14",
                "bad_filler_15",
                "bad_filler_2",
                "bad_filler_3",
                "bad_filler_4",
                "bad_filler_5",
                "bad_filler_6",
                "bad_filler_7",
                "bad_filler_8",
                "bad_filler_9",
                "bad_rax_melee_bot",
                "bad_rax_melee_mid",
                "bad_rax_melee_top",
                "bad_rax_range_bot",
                "bad_rax_range_mid",
                "bad_rax_range_top",
                "bake_path_buildcubemaps",
                "bake_path_previewlighting",
                "bakeambientlight",
                "bakeambientocclusion",
                "baked_light_index_max",
                "baked_light_index_min",
                "baked_light_indexing",
                "bakedshadowindex",
                "bakefarz",
                "bakelightdoublesided",
                "bakelightimportancevolume",
                "bakelightindex",
                "bakelightindexscale",
                "bakelighting",
                "bakelightoutput",
                "bakenearz",
                "bakeonlycubemaps",
                "bakeresource",
                "bakeskylight",
                "bakespeculartocubemaps_size",
                "bakespeculartocubemaps",
                "baketoworld",
                "balcony",
                "ball_spawn_countdown",
                "ballcount",
                "ballet_landscape",
                "balllifetime",
                "ballradius",
                "ballrespawntime",
                "balltype",
                "bank",
                "banner_dire_04",
                "banner_dire",
                "banner_radiant_01",
                "banner_radiant_02",
                "banner_radiant",
                "barbed",
                "barnlight",
                "barrel_smoke_effect",
                "barrel_volume",
                "barrel",
                "barrely",
                "barrelz",
                "barrier_size",
                "barrier_state",
                "base_scale",
                "base_type",
                "base",
                "baseitem",
                "basepullspeed",
                "basesize",
                "basespread",
                "baseunit",
                "basisnormal",
                "basisorigin",
                "basisu",
                "basisv",
                "batchlimit",
                "battery_level",
                "battery_placement_trigger",
                "bconstrainrotation",
                "beambrightness",
                "beamcount_max",
                "beamcount_min",
                "beampower",
                "beckon_approach_enabled",
                "beckon_radius",
                "begin_automatically",
                "begin_scripted_sequence",
                "beginsequence",
                "behaveaspropphysics",
                "behavior_tree_file",
                "beige",
                "beveragetype",
                "bias",
                "big_read",
                "bird",
                "bleft_down",
                "bleft_down001",
                "bleft_down002",
                "bleft_up",
                "bleft_up001",
                "bleft_up002",
                "bleftroot",
                "bleftroot001",
                "bleftroot002",
                "blendamount",
                "blenddeltamultiplier",
                "bloater_basement_trigger_1",
                "bloater_basement_trigger_2",
                "bloater_basement_trigger_2b",
                "bloater_bathroom_trigger",
                "bloater_closet_trigger_1",
                "bloater_crawlspace_trigger",
                "bloater_hall_collapse_trigger_1",
                "bloater_hall_trigger_1",
                "bloater_hall_trigger_2",
                "bloater_position",
                "block_fow",
                "blockdamage",
                "blocklos",
                "bloom_start_value",
                "bloom_strength",
                "blue_respawn_time",
                "blue_teleport",
                "blue_window_broken",
                "blue",
                "bluespawn",
                "board1",
                "board2",
                "board3",
                "board4",
                "board5",
                "board6",
                "board7",
                "boardmodel",
                "body_color",
                "body_skin",
                "body_tint_color",
                "body_type",
                "body_variant_1",
                "body_variant",
                "body",
                "bodygroup_choice",
                "bodygroup_name",
                "bodygroup",
                "bodygroupchoices",
                "bodygroups",
                "bolt_model",
                "boltwidth",
                "bomb_mount_target",
                "bombradius",
                "bone_001",
                "bone_002",
                "bone",
                "bone001",
                "bone002",
                "bone003",
                "bone004",
                "bone005",
                "bone006",
                "bone007",
                "bone008",
                "bone009",
                "bone010",
                "bone011",
                "bone012",
                "bone013",
                "boneld1",
                "boneld2",
                "bonelu1",
                "bonelu2",
                "bonename",
                "bonenames",
                "bonepositions",
                "boneprefix",
                "bonerd1",
                "bonerd2",
                "bonerotations",
                "boneru1",
                "boneru2",
                "bonetransforms",
                "bonuscrate",
                "boolean",
                "bot_class",
                "bot_difficulty",
                "bot_name",
                "bothcombinekilled",
                "botmaxvisiondistance",
                "bottom_grip_disengage_dist",
                "bottom_grip_max_dist",
                "bottom_grip_min_dist",
                "bottom_point",
                "bottom",
                "bottomtrack",
                "bounce",
                "bouncecolor",
                "bouncelight",
                "bouncelightenabled",
                "bouncescale",
                "bouncesound",
                "boundary_maxradius",
                "boundary_tracebackfaces",
                "boundary_tracebias",
                "boundary_traceoccluders",
                "box_inner_maxs",
                "box_inner_mins",
                "box_maxs",
                "box_mins",
                "box_oriented",
                "box_outer_maxs",
                "box_outer_mins",
                "box_size",
                "box_world_aligned",
                "box",
                "brace_attachment_up",
                "brace_up_flipped",
                "brackets_a",
                "brackets_b",
                "brackets",
                "branch_a_0",
                "branch_a_1",
                "branch_a_2",
                "branch_a_3",
                "branch_a_4",
                "branch_b_0",
                "branch_b_1",
                "branch_b_2",
                "branch_b_3",
                "branch_b_4",
                "branch_c_0",
                "branch_c_1",
                "branch_c_2",
                "branch_c_3",
                "branch_c_4",
                "branch_d_0",
                "branch_d_1",
                "branch_d_2",
                "branch_d_3",
                "branch_d_4",
                "branch_e_0",
                "branch_e_1",
                "branch_e_2",
                "branch_e_end",
                "branch_f_0",
                "branch_f_1",
                "branch_f_2",
                "branch_f_end",
                "branch_g_0",
                "branch_g_1",
                "branch_g_2",
                "branch_g_end",
                "branch_h_0",
                "branch_i_0",
                "branch_j_0",
                "branch_k_0",
                "branch_l_0",
                "branch01",
                "branch02",
                "branch03",
                "branch04",
                "branch05",
                "branch06",
                "branch07",
                "branch08",
                "branch09",
                "branch10",
                "branch11",
                "branch12",
                "branch13",
                "branch14",
                "branch15",
                "branch16",
                "branchingmethod",
                "breakable",
                "breakabletype",
                "breakaftertime_x",
                "breakaftertime_y",
                "breakaftertime_z",
                "breakaftertimethreshold_x",
                "breakaftertimethreshold_y",
                "breakaftertimethreshold_z",
                "breakboard",
                "breaklength",
                "breaklock",
                "breakmodelmessage",
                "breakpadappid_tools",
                "breakpadappid",
                "breakpieces_substring",
                "breakshardless",
                "breaksound",
                "brickbattype",
                "bricks_grp_0_skin",
                "bricks_grp_1_skin",
                "bricks_hollow_red",
                "bright_down",
                "bright_down001",
                "bright_down002",
                "bright_up",
                "bright_up001",
                "bright_up002",
                "brightness_candelas",
                "brightness_delta",
                "brightness_legacy",
                "brightness_lumens",
                "brightness_nits",
                "brightness_offset",
                "brightness_units",
                "brightness",
                "brightnessscale",
                "broken_door_handle_toggle",
                "broken_door_model",
                "broken_door_skin",
                "broken_pillar_cap_16_16_units",
                "broken_pillar_cap",
                "broken",
                "brokenmaterial",
                "bullet_count_anim_rate",
                "bullet_damage_vs_player",
                "bullet_damage",
                "bulletpack_0",
                "bullseyename",
                "bumper_front_base_jnt",
                "bumper_front_lplate_jnt",
                "bupdate",
                "burn_duration",
                "burst_max",
                "burst_min",
                "burst_randomize",
                "burst_scale",
                "burst_spawner",
                "burstcount",
                "busyactor",
                "busysearchrange",
                "button_box_skin",
                "button_box_tint_color",
                "button_initially_locked_choice",
                "button_initially_locked",
                "button_pusher_skin_choice",
                "button_pusher_skin",
                "button_pusher_tint_color",
                "button1",
                "button2",
                "button3",
                "button4",
                "button5",
                "button6",
                "button7",
                "button8",
                "buttondown",
                "buttonup",
                "buying",
                "buyzone",
                "bzisfree",
                "bzistrapped",
                "c",
                "cable_closed_end_cap_2_units_long_tile",
                "cablea0_jnt",
                "cablea1_jnt",
                "cablea2_jnt",
                "cablea3_jnt",
                "cablea4_jnt",
                "cablea5_jnt",
                "cablea6_jnt",
                "cablea7_jnt",
                "cablea8_jnt",
                "cablea9_jnt",
                "cableb0_jnt",
                "cableb1_jnt",
                "cableb2_jnt",
                "cableb3_jnt",
                "cableb4_jnt",
                "cableb5_jnt",
                "cableb6_jnt",
                "cableb7_jnt",
                "cableb8_jnt",
                "cableb9_jnt",
                "cablec0_jnt",
                "cablec1_jnt",
                "cablec2_jnt",
                "cablec3_jnt",
                "cablec4_jnt",
                "cablec5_jnt",
                "cablec6_jnt",
                "cablec7_jnt",
                "cablec8_jnt",
                "cablec9_jnt",
                "cabled0_jnt",
                "cabled1_jnt",
                "cabled2_jnt",
                "cabled3_jnt",
                "cabled4_jnt",
                "cabled5_jnt",
                "cabled6_jnt",
                "cabled7_jnt",
                "cabled8_jnt",
                "cabled9_jnt",
                "cablee0_jnt",
                "cablee1_jnt",
                "cablee2_jnt",
                "cablee3_jnt",
                "cablee4_jnt",
                "cablee5_jnt",
                "cablee6_jnt",
                "cablee7_jnt",
                "cablejgl_0",
                "cablejgl_1",
                "cablejgl_2",
                "cablejgl_3",
                "cablejgl_4",
                "cablejgl_5",
                "cablejgl_6",
                "cablejgl_end",
                "camera_name",
                "cameradistanceoverride",
                "cameraname",
                "cameras",
                "cameraspace",
                "canbebroken",
                "canbepickedup",
                "cancel",
                "cancelsequence",
                "candepositinitemholder",
                "canfireportal1",
                "canfireportal2",
                "cannotgravitygrab",
                "cannotgravitypunt",
                "canphyspull",
                "cantdie",
                "cap_pieces",
                "capenabledelay",
                "caplayout",
                "capsule_length",
                "capture_delay_offset",
                "capture_delay",
                "capture_ignore_continuous",
                "capture_on_interrupt",
                "capture_on_touch",
                "capturepoint",
                "cargovisible",
                "carry_type",
                "carrytype_override",
                "cascadecrossfade",
                "cascadedistancefade",
                "cascaderenderstaticobj",
                "case01",
                "case02",
                "case03",
                "case04",
                "case05",
                "case06",
                "case07",
                "case08",
                "case09",
                "case10",
                "case11",
                "case12",
                "case13",
                "case14",
                "case15",
                "case16",
                "case17",
                "case18",
                "case19",
                "case20",
                "case21",
                "case22",
                "case23",
                "case24",
                "case25",
                "case26",
                "case27",
                "case28",
                "case29",
                "case30",
                "case31",
                "case32",
                "castshadows",
                "cavernbreed",
                "ceiling_connector",
                "ceiling_flipped",
                "ceiling",
                "center_on_damage_point",
                "center",
                "centersize",
                "centeru",
                "centerv",
                "chainname",
                "chair_pose",
                "channel",
                "chaptertitle",
                "cheapwaterenddistance",
                "cheapwaterstartdistance",
                "cheat_code_strider_is_immediately_sent_to_arena",
                "cheat_code_strider_is_sent_to_heli_fight",
                "checkafkplayers",
                "checkcough",
                "checkdestifclearforplayer",
                "child_piece",
                "child",
                "childfiltername",
                "childmodelanimgraphparameter",
                "childphysicsmode",
                "children",
                "childspawngroup",
                "choices",
                "choosefile",
                "choosesoundevent",
                "citizen_name",
                "citizentype",
                "clampu",
                "clampv",
                "clampw",
                "class",
                "classname",
                "classnameoverride",
                "clavicle_l",
                "clavicle_r",
                "clean",
                "cleanup",
                "clear_color",
                "clearoncontact",
                "clientonlyentitybehavior",
                "clientsidechildmodel",
                "clientsideentity",
                "clip_3d_skybox_near_to_world_far_offset",
                "clip_3d_skybox_near_to_world_far",
                "clip_glow_effect",
                "clip_grab_dist",
                "clip_insert_sound",
                "clip_model_left_handed",
                "clip_model_right_handed",
                "clip_release_sound",
                "clipstyle",
                "close_delay",
                "close_sound",
                "close",
                "closecaptionnoattenuate",
                "closecompletesound",
                "closed_idle",
                "closed",
                "closesound",
                "closestto",
                "cloth",
                "clothscale",
                "cloud1direction",
                "cloud1speed",
                "cloud2direction",
                "cloud2speed",
                "cloudscale",
                "cmd",
                "coldworld",
                "collapsetoforcepoint",
                "collide",
                "collisionenabled",
                "collisiongroup",
                "collisiongroupoverride",
                "collisions",
                "color_bars",
                "color_dawn",
                "color_day",
                "color_dusk",
                "color_night",
                "color_tint",
                "color_warp_blend_to_full",
                "color_warp_texture",
                "color",
                "color1",
                "color2",
                "color255",
                "colorcorrectionname",
                "colormax",
                "colormin",
                "colormode",
                "colortemperature",
                "colortint",
                "colortransitiontime",
                "columns",
                "combat_enabled",
                "combine_citizen_walkie_talkie",
                "combine_cleanup",
                "combine_killed",
                "combine_model_variant",
                "combine_model_weapon_type",
                "comingbackconceptmodifier",
                "comingbackwaitforspeak",
                "command",
                "commentaryfile",
                "companion_trigger",
                "comparevalue",
                "compatibilitymode",
                "compileimagespassthroughmode",
                "componenttypeflags",
                "concave",
                "concept",
                "condition",
                "conditional",
                "cone_zombie_squad",
                "coneoffire",
                "config",
                "configfile",
                "conflict_response",
                "conform_fingers",
                "connection_0",
                "connection_1",
                "connection_2",
                "connection_3",
                "connectionsdata",
                "consoleratshot",
                "constant_speed",
                "constant",
                "constrain_angle",
                "constrainedeventid",
                "constrainrotation",
                "constraint",
                "constraintsystem",
                "constrainttype",
                "constrainvelocity",
                "contentsdeform",
                "contexttarget",
                "control_point_a_index",
                "control_point_b_index",
                "control_point_c_index",
                "control_point_d_index",
                "control_point",
                "control_volume",
                "controlpoint",
                "converttodebriswhenpossible",
                "convex_default_plane_softness",
                "convex_max_planes",
                "convex_volume",
                "convex",
                "convexityangle",
                "conveyor_entity_spawner",
                "conveyor_models",
                "coretype",
                "corner",
                "cornerpropertyvalues0",
                "cornerpropertyvalues1",
                "cornerpropertyvalues2",
                "cornerpropertyvalues3",
                "cosmetic_type",
                "count",
                "counter",
                "countryside_landscape",
                "cp0_model",
                "cp0_snapshot",
                "cpm_restrict_team_cap_win",
                "cpoint0",
                "cpoint1_parent",
                "cpoint1",
                "cpoint10",
                "cpoint11",
                "cpoint12",
                "cpoint13",
                "cpoint14",
                "cpoint15",
                "cpoint16",
                "cpoint17",
                "cpoint18",
                "cpoint19",
                "cpoint2_parent",
                "cpoint2",
                "cpoint20",
                "cpoint21",
                "cpoint22",
                "cpoint23",
                "cpoint24",
                "cpoint25",
                "cpoint26",
                "cpoint27",
                "cpoint28",
                "cpoint29",
                "cpoint3_parent",
                "cpoint3",
                "cpoint30",
                "cpoint31",
                "cpoint32",
                "cpoint33",
                "cpoint34",
                "cpoint35",
                "cpoint36",
                "cpoint37",
                "cpoint38",
                "cpoint39",
                "cpoint4_parent",
                "cpoint4",
                "cpoint40",
                "cpoint41",
                "cpoint42",
                "cpoint43",
                "cpoint44",
                "cpoint45",
                "cpoint46",
                "cpoint47",
                "cpoint48",
                "cpoint49",
                "cpoint5_parent",
                "cpoint5",
                "cpoint50",
                "cpoint51",
                "cpoint52",
                "cpoint53",
                "cpoint54",
                "cpoint55",
                "cpoint56",
                "cpoint57",
                "cpoint58",
                "cpoint59",
                "cpoint6_parent",
                "cpoint6",
                "cpoint60",
                "cpoint61",
                "cpoint62",
                "cpoint63",
                "cpoint7_parent",
                "cpoint7",
                "cpoint8",
                "cpoint9",
                "cpr_cp_names",
                "cpr_printname",
                "cpr_priority",
                "cpr_restrict_team_cap_win",
                "crabcount",
                "crafting_station_shack",
                "crafting_station_start",
                "crafting_station",
                "crateappearance",
                "cratetype",
                "create_tactical_connections",
                "createclientsidechild",
                "createnavobstacle",
                "createspores",
                "creepminimapiconscale",
                "criteria",
                "criteriondistance",
                "criterionvisibility",
                "crits",
                "cross_beam_section",
                "crowbar",
                "cspinup",
                "css_classes",
                "ctf_overtime",
                "cubemap_fog",
                "cubemap",
                "cubemapfogenddistance",
                "cubemapfogfalloffexponent",
                "cubemapfogheightexponent",
                "cubemapfogheightstart",
                "cubemapfogheightwidth",
                "cubemapfoglodbiase",
                "cubemapfogskyentity",
                "cubemapfogskymaterial",
                "cubemapfogsource",
                "cubemapfogstartdistance",
                "cubemapfogtexture",
                "cubemapname",
                "cubemapsize",
                "cubemaptexture",
                "cull_mode",
                "current",
                "curve",
                "custom_editor_widget",
                "custom_latitude",
                "custom_longitude",
                "custom_position_x",
                "custom_position_y",
                "custom_squad",
                "custom_timezone",
                "customcubemaptexture",
                "customnpcname",
                "customoutputvalue",
                "cycle",
                "cyclefrequency",
                "cycletype",
                "dac_dof_far_blurry",
                "dac_dof_far_crisp",
                "dac_dof_tilt_to_ground",
                "damage_direction_cp",
                "damage_percent_per_second",
                "damage_position_cp",
                "damage_table",
                "damage",
                "damagecap",
                "damaged_1",
                "damaged_2",
                "damaged_3",
                "damaged_4",
                "damaged_cap_64_units_long",
                "damaged_cap",
                "damaged_corner",
                "damaged_no_supports",
                "damaged",
                "damagedelay",
                "damagefilter",
                "damageforce",
                "damagefx_lvl1",
                "damagefx_lvl2",
                "damagemodel",
                "damagepositioningentity",
                "damagepositioningentity02",
                "damagepositioningentity03",
                "damagepositioningentity04",
                "damageradius",
                "damagescale",
                "damagetarget",
                "damagetoenablemotion",
                "damagetype",
                "damping_override",
                "damping_ratio",
                "damping",
                "dangeroustime",
                "dangeroustimer",
                "dangling",
                "dark",
                "data_cp_value",
                "data_cp",
                "data_name",
                "data",
                "datastateflags",
                "datatypes",
                "date",
                "daylength",
                "dead_end",
                "deaf",
                "debug_anim_source",
                "debug_draw",
                "debugdraw",
                "decalname",
                "decay_bias",
                "decay_duration",
                "deceleration",
                "default_behavior",
                "default_keys",
                "default_weight",
                "default",
                "defaultanim",
                "defaultcamera",
                "defaultdist1",
                "defaultdist2",
                "defaultdist3",
                "defaultgrassmaterial",
                "defaultgridtileset",
                "defaultgridtileset2",
                "defaultmap",
                "defaultpathentity",
                "defaultpointentity",
                "defaultsequencename",
                "defaultsolidentity",
                "defaultstyle",
                "defaulttarget",
                "defaulttexturescale",
                "defaulttilesize",
                "defaulttovr",
                "defaultupgrade",
                "defaultvalue",
                "defaultwelcome",
                "delay",
                "delaybetweenlines",
                "delaymax",
                "delaymin",
                "delete_zombie",
                "deleteafterspawn",
                "delta",
                "density",
                "densityrampspeed",
                "denycommandconcept",
                "depth_render_offset",
                "depth",
                "depthblurfocaldistance",
                "depthblurstrength",
                "depthmaptexture",
                "descriptions",
                "desired_distance",
                "desired_orientation",
                "desiredammo357",
                "desiredammoar2_altfire",
                "desiredammoar2",
                "desiredammobuckshot",
                "desiredammocrossbow",
                "desiredammogrenade",
                "desiredammopistol",
                "desiredammorpg_round",
                "desiredammosmg1_grenade",
                "desiredammosmg1",
                "desiredarmor",
                "desiredhealth",
                "desiredtimescale",
                "dest",
                "destdmgamnt_lvl1",
                "destdmgamnt_lvl2",
                "destinationgroup",
                "destroy_antlions",
                "destroybuildings",
                "destroyfx",
                "destroysound",
                "destruction_lvl1",
                "destruction_lvl2",
                "detach_angle",
                "detach_from_owner",
                "detail_01",
                "detail_crossbeam",
                "detail_inset_64",
                "detail_inset",
                "detail_level",
                "detail",
                "detailgeometry",
                "detailmaterial",
                "details",
                "detailvbsp",
                "developeronly",
                "dialog_layout_name",
                "dialog_sound",
                "dialog_string",
                "difficulty",
                "dimensions",
                "dimmer",
                "dire_end_camera",
                "directional",
                "directionalmarker",
                "directionentityname",
                "directionnoise",
                "directlight",
                "directory",
                "directsimulationmode",
                "disable_antlions",
                "disable_npc_maker",
                "disable_shadows",
                "disable_teleport",
                "disable",
                "disableallshadows",
                "disableannouncer",
                "disableautogenerateddmspawns",
                "disablebonefollowers",
                "disablecollisions",
                "disabled",
                "disabledinlowquality",
                "disabledodge",
                "disabledvalue",
                "disableflashlight",
                "disablefogofwar",
                "disablefrontofboardcheck",
                "disableheightdisplacement",
                "disableik",
                "disableinlowquality",
                "disablelowviolence",
                "disablemerging",
                "disablemotion",
                "disablephysics",
                "disableplug",
                "disablereceiveshadows",
                "disablesearch",
                "disableselfshadowing",
                "disableshadowdepth",
                "disableshadows",
                "disablestashpurchasing",
                "disablevertexlighting",
                "disablex360",
                "disappeardist",
                "disappearmaxdist",
                "disappearmindist",
                "disconnect_culler",
                "discoverable",
                "disenchatflowchild",
                "disengagedistance",
                "display_text",
                "display_to_team",
                "displaybaseface",
                "displaycolor",
                "displaytext",
                "displaytextoption",
                "disposition",
                "dissolveitemsdelay",
                "dissolvetype",
                "distance",
                "distancebias",
                "distancemapmax",
                "distancemapmin",
                "distancemax",
                "distancemin",
                "distancetoplayer",
                "distancetotarget",
                "distmax",
                "dk_blue_window_broken",
                "dk_blue",
                "dmg.bullets",
                "dmg.club",
                "dmg.explosive",
                "dmg.fire",
                "dmg",
                "dmglvl1sound",
                "dmglvl2sound",
                "doclientsideanimation",
                "dof_enabled",
                "dof_near_blurry",
                "dof_near_crisp",
                "dohapticsonbothhands",
                "donotdrop",
                "dont_teleport_at_end",
                "dontpickupweapons",
                "dontspeakstart",
                "dontusespeechsemaphore",
                "door_1_ajar_amount_0-1",
                "door_2_ajar_amount_0-1",
                "door_ajar_angle",
                "door_choices",
                "door_factory_interior",
                "door_fixup_lighting_origin",
                "door_frame_mesh_handle_off",
                "door_hidden",
                "door_initial_completion",
                "door_left_front_ajar_angle",
                "door_left_front_angle",
                "door_left_front_position",
                "door_left_front_spawn_position",
                "door_left_rear_ajar_angle",
                "door_left_rear_angle",
                "door_left_rear_position",
                "door_left_rear_spawn_position",
                "door_name",
                "door_rear_ajar_0-1",
                "door_right_front_ajar_angle",
                "door_right_front_angle",
                "door_right_front_position",
                "door_right_front_spawn_position",
                "door_right_rear_ajar_0-1",
                "door_right_rear_ajar_angle",
                "door_right_rear_angle",
                "door_right_rear_position",
                "door_right_rear_spawn_position",
                "door_skin",
                "door_spawn_position",
                "door_tint_color",
                "door_variant_1",
                "door_variant",
                "door",
                "door2",
                "doorcanbebroken",
                "doors_1_skin",
                "doors_2_skin",
                "dota_badguys_fort",
                "dota_badguys_tower1_bot",
                "dota_badguys_tower1_mid",
                "dota_badguys_tower1_top",
                "dota_badguys_tower2_bot",
                "dota_badguys_tower2_mid",
                "dota_badguys_tower2_top",
                "dota_badguys_tower3_bot",
                "dota_badguys_tower3_mid",
                "dota_badguys_tower3_top",
                "dota_badguys_tower4_bot",
                "dota_badguys_tower4_top",
                "dota_goodguys_fort",
                "dota_goodguys_tower1_bot",
                "dota_goodguys_tower1_mid",
                "dota_goodguys_tower1_top",
                "dota_goodguys_tower2_bot",
                "dota_goodguys_tower2_mid",
                "dota_goodguys_tower2_top",
                "dota_goodguys_tower3_bot",
                "dota_goodguys_tower3_mid",
                "dota_goodguys_tower3_top",
                "dota_goodguys_tower4_bot",
                "dota_goodguys_tower4_top",
                "dota_team",
                "dotamaxtrees",
                "dotatilegrid",
                "dotaworldtype",
                "dotproductmax",
                "down_arrow_off",
                "down_arrow_on",
                "down",
                "drag_override",
                "draw_3dskybox",
                "drawdistance",
                "drawer_1_ammo_hidden",
                "drawer_1_crate_hidden",
                "drawer_1_health_hidden",
                "drawer_1_hidden",
                "drawer_1_offset",
                "drawer_1_resin_hidden",
                "drawer_1_rubbish_hidden",
                "drawer_1_state",
                "drawer_2_ammo_hidden",
                "drawer_2_crate_hidden",
                "drawer_2_health_hidden",
                "drawer_2_hidden",
                "drawer_2_offset",
                "drawer_2_resin_hidden",
                "drawer_2_rubbish_hidden",
                "drawer_2_state",
                "drawer_3_ammo_hidden",
                "drawer_3_crate_hidden",
                "drawer_3_health_hidden",
                "drawer_3_hidden",
                "drawer_3_offset",
                "drawer_3_resin_hidden",
                "drawer_3_rubbish_hidden",
                "drawer_3_state",
                "drawer_4_ammo_hidden",
                "drawer_4_crate_hidden",
                "drawer_4_health_hidden",
                "drawer_4_hidden",
                "drawer_4_offset",
                "drawer_4_resin_hidden",
                "drawer_4_rubbish_hidden",
                "drawer_4_state",
                "drawer_color",
                "drawer_skin",
                "drawinfastreflection",
                "drive_to_weld",
                "drivermaxspeed",
                "driverminspeed",
                "drop_to_ground",
                "dst",
                "dud",
                "dummy001",
                "dummy002",
                "dummy003",
                "dummy004",
                "duration",
                "dustoff1",
                "dustoff2",
                "dustoff3",
                "dustoff4",
                "dustoff5",
                "dustoff6",
                "dustscale",
                "dynamicattachoffset",
                "dynamicentityname",
                "dynamicmaximumocclusion",
                "dynamicproxypoint",
                "dynamicresetcount",
                "dz_enabled",
                "dz_missioncontrolled",
                "dz_suppressbombalert",
                "ease_in",
                "ease_out",
                "easeanglestocamera",
                "edge_fade_dist",
                "edge_fade_dists",
                "edgedata",
                "edgedataindices",
                "edgefaceindices",
                "edgenextindices",
                "edgeoppositeindices",
                "edgepropertyvalues0",
                "edgepropertyvalues1",
                "edgepropertyvalues2",
                "edgepropertyvalues3",
                "edgevertexdataindices",
                "edgevertexindices",
                "editcommand_traceshape",
                "editor_only",
                "editorbuild",
                "editorgroupid",
                "editormodel",
                "editoronly",
                "editorversion",
                "effect_configuration",
                "effect_duration",
                "effect_interpenetrate_name",
                "effect_name",
                "effect_namess",
                "effect_sound_name",
                "effect_target_name",
                "effect_zap_name",
                "effect_zap_source",
                "effect",
                "effectduration",
                "effecthandling",
                "effectlight_brightness",
                "effectlight_enabled",
                "effectlight_hidden",
                "effectlight_tint_color",
                "effectradius",
                "eject_shell_model",
                "eject_shell_smoke_effect",
                "elbowpad_0_l",
                "elbowpad_0_r",
                "elem_name__ent_attr_types",
                "element",
                "eludedist",
                "emissive",
                "emitfromworld",
                "emittername",
                "emittime",
                "empty",
                "enable_antlions",
                "enable_limit",
                "enable_npc_maker",
                "enable_offscreen_indicator",
                "enable_separate_skybox_fog",
                "enable_shadows",
                "enable_swing_limit",
                "enable_twist_limit",
                "enable",
                "enableangularconstraint",
                "enableautostyles",
                "enablecollision",
                "enabled",
                "enabledchance",
                "enableexposure",
                "enablefog",
                "enablegun",
                "enablelightbounce",
                "enablelinearconstraint",
                "enablemotion",
                "enablemotionongravitygrab",
                "enablephysicsdelay",
                "enablepickrules",
                "enableplug",
                "enablereflection",
                "enablerefraction",
                "enableripples",
                "enableshadows",
                "enableshadowsfromlocallights",
                "enablesoundevent",
                "enablestopwastingammoline",
                "enableuseoutput",
                "end_action",
                "end_cap",
                "end_caps",
                "end_entity",
                "end_point_fadein",
                "end_point_fadeout",
                "end_point_scale",
                "end_scripted_sequence",
                "end",
                "endattachment",
                "endcap",
                "endcolor",
                "endframe",
                "endloop",
                "endmatch",
                "endnode",
                "endsize",
                "endsprite",
                "endtargetname",
                "endtime",
                "endu",
                "endv",
                "endwidth",
                "enemy_finder_zombie_fence",
                "enemy_team_score_sound",
                "enemyfilter",
                "energygun_loaded_ammo",
                "energygun",
                "engagedistance",
                "enginesound",
                "ensure_on_navmesh_on_finish",
                "ent_dota_fountain_bad",
                "ent_dota_fountain_good",
                "entity_01",
                "entity_02",
                "entity_03",
                "entity_04",
                "entity_05",
                "entity_06",
                "entity_07",
                "entity_08",
                "entity_09",
                "entity_10",
                "entity_count",
                "entity_name",
                "entity_properties",
                "entity",
                "entityfiltername",
                "entitylumpname",
                "entitytemplate",
                "entry_activity",
                "entry_sequence",
                "entryangletolerance",
                "entrytag",
                "env_spark_name",
                "equip_on_mapstart",
                "event_data_int",
                "event_delay",
                "event_name",
                "event_to_fire",
                "event",
                "event0",
                "event1",
                "event2",
                "event3",
                "eventgame",
                "eventid",
                "eventindex",
                "eventname",
                "events",
                "every_unit",
                "exactvelocitychoicetype",
                "excludednpc",
                "excludefromimpostors",
                "exclusive",
                "exit_activity",
                "exit_sequence",
                "exittags",
                "expdamage",
                "explode_particle",
                "explodedamage",
                "explodemagnitude",
                "explodeonspawn",
                "exploderadius",
                "exploitablebyplayer",
                "explosion_buildup",
                "explosion_custom_effect",
                "explosion_custom_sound",
                "explosion_delay",
                "explosion_magnitude",
                "explosion_radius",
                "explosion_type",
                "explosion",
                "explosionignoreentity",
                "explosive_damage",
                "explosive_force",
                "explosive_radius",
                "explosive_resist",
                "exposurecompensation",
                "exposuresmoothingrange",
                "exposurespeeddown",
                "exposurespeedup",
                "expradius",
                "expressiongroup",
                "expressionname",
                "expressionoverride",
                "expressiontype",
                "extension_map",
                "extent_origin_left",
                "extent_origin_min_left",
                "extent_origin_min_right",
                "extent_origin_right",
                "extent",
                "extra_vertex_data",
                "facade_element",
                "face_entity_fov",
                "face_entity",
                "face_forward",
                "facedata",
                "facedataindices",
                "faceedgeindices",
                "faceids",
                "faces",
                "facevertexdata",
                "fade_group_id",
                "fade_origin_offset",
                "fade_origin",
                "fade_out_mode",
                "fade_radius_end",
                "fade_radius",
                "fade_size_end",
                "fade_size_start",
                "fade_time",
                "fadedist",
                "fadeduration",
                "fadeenddist",
                "fadein",
                "fadeinduration",
                "fadeinend",
                "fadeinsecs",
                "fadeinstart",
                "fadeintime",
                "fademaxdist",
                "fademindist",
                "fadeout",
                "fadeoutduration",
                "fadeoutsecs",
                "fadeouttime",
                "fadeplayervisibilityfarz",
                "fadescale",
                "fadespeed",
                "fadestartdist",
                "fadetime",
                "fadetoblackstrength",
                "fadingtime",
                "failedhackattempt",
                "failureconceptmodifier",
                "fallback_asset",
                "fallbacktarget",
                "fallingspeedthreshold",
                "falloff",
                "falloffexponent",
                "fanfriction",
                "far_blue",
                "far_blur",
                "far_focus",
                "far_green",
                "far_radius",
                "far_red",
                "farm_portrait",
                "farmers_landscape",
                "farz_override",
                "farz",
                "farzscale",
                "fastretrigger",
                "features",
                "feeffectname",
                "fetcheventdata",
                "fgd",
                "fieldofview",
                "fighttarget",
                "file_is_kv3",
                "file",
                "filename",
                "filltime",
                "filmgrainstrength",
                "filter_exit_wheel_physics_socket",
                "filter_exit_wheel_physics",
                "filter_max_per_enemy",
                "filter_name",
                "filter_outer_radius",
                "filter_radius",
                "filter_wheel_physics_socket",
                "filter_wheel_physics",
                "filter01",
                "filter02",
                "filter03",
                "filter04",
                "filter05",
                "filter06",
                "filter07",
                "filter08",
                "filter09",
                "filter10",
                "filterattribute",
                "filterclass",
                "filtermass",
                "filtername",
                "filterteam",
                "filtertype",
                "final",
                "finale_length",
                "finaltime",
                "finger_index",
                "finger_middle",
                "finger_pinky",
                "finger_ring",
                "finger_thumb",
                "fire_output_immediately",
                "fire_time",
                "fire_times",
                "fireattack",
                "fireballsprite",
                "firedamage",
                "firedelay",
                "fireendsound",
                "fireinterval",
                "fireradius",
                "firerate",
                "firesize",
                "firesound",
                "firespread",
                "firesprite",
                "firestartsound",
                "firetype",
                "firevariance",
                "first_path_node",
                "fish_count",
                "fixedlength",
                "fixedpointdamping",
                "fixedrespawntime",
                "fixup_lighting_origin_to_player_side",
                "fixup_style",
                "fixupentitynames",
                "fixupnames",
                "flag_as_weather",
                "flag_icon",
                "flag_model",
                "flag_paper",
                "flag_reset_delay",
                "flag_trail",
                "flags",
                "flammable",
                "flap_back",
                "flap_front",
                "flap_left",
                "flap_right",
                "flashlight_enabled",
                "flashlight",
                "flat_01",
                "flat_no_axle",
                "flat_side",
                "flat",
                "flicker",
                "flightspeed",
                "flighttime",
                "flinch_chance",
                "flip_horizontal",
                "flipped",
                "flipvcoordinates",
                "float",
                "floatvalue",
                "flood_fill",
                "flow_map_texture",
                "flowers1_photo_landscape",
                "flowers2_photo_landscape",
                "flowers3_photo",
                "flyby_high1_hidden",
                "flyby_high1",
                "flyby_high2_hidden",
                "flyby_high2",
                "flyby1_hidden",
                "flyby1",
                "flyby2_hidden",
                "flying_courier",
                "flysound",
                "fmodamplitude",
                "fmodrate",
                "fmodtimeoffset",
                "fmodulationtype",
                "focus_range",
                "focus_target",
                "fog_color_dawn",
                "fog_color_day",
                "fog_color_dusk",
                "fog_color_night",
                "fog_end_dawn",
                "fog_end_day",
                "fog_end_dusk",
                "fog_end_night",
                "fog_flow_map_texture",
                "fog_height_day",
                "fog_height_night",
                "fog_hight_color_day",
                "fog_hight_color_night",
                "fog_lighting",
                "fog_start_dawn",
                "fog_start_day",
                "fog_start_dusk",
                "fog_start_night",
                "fog_type",
                "fog",
                "fogblend",
                "fogcolor",
                "fogcolor2",
                "fogcontributionstrength",
                "fogdir",
                "fogenable",
                "fogenabled",
                "fogend",
                "fogendheight",
                "fogexponent",
                "fogfalloffexponent",
                "fogirradiancevolume",
                "foglerptime",
                "fogmaxdensity",
                "fogmaxopacity",
                "fogname",
                "fogshadows",
                "fogstart",
                "fogstartheight",
                "fogstrength",
                "fogverticalexponent",
                "followparent",
                "followtarget",
                "followthelead",
                "font_name",
                "font_size",
                "font",
                "foot",
                "footstep_script",
                "force_hidden",
                "force_lod_body_ambulance_glass",
                "force_lod_body_ambulance",
                "force_lod_body_glass",
                "force_lod_body_utility_glass",
                "force_lod_body_utility",
                "force_lod_body",
                "force_lod_door_front_left",
                "force_lod_door_front_right",
                "force_lod_door_left_front",
                "force_lod_door_left_rear",
                "force_lod_door_rear_left",
                "force_lod_door_rear_right",
                "force_lod_door_rear",
                "force_lod_door_right_front",
                "force_lod_door_right_rear",
                "force_lod_level",
                "force_lod_tailgate",
                "force_lookat_path",
                "force_map_reset",
                "force_transmit_to_client",
                "force",
                "forcebc7",
                "forceclosed",
                "forcecrouch",
                "forcedefaultguide",
                "forcedroponteleport",
                "forcedslave",
                "forcedsubtype",
                "forcefullyopen",
                "forcelimit_x",
                "forcelimit_y",
                "forcelimit_z",
                "forcelimit",
                "forcenavignore",
                "forcenpcexclude",
                "forcescale",
                "forceselecthero",
                "forceshortmovements",
                "forcestate",
                "forceteleportacknowledge",
                "forcetime",
                "forcetoenablemotion",
                "forcetype",
                "forcevtxfileupconvert",
                "forgivedelay",
                "formation",
                "forwarding",
                "forwards_disabled",
                "fov_rate",
                "fov",
                "fov2d",
                "fow_color_b_day",
                "fow_color_b_night",
                "fow_color_day",
                "fow_color_g_day",
                "fow_color_g_night",
                "fow_color_night",
                "fow_color_r_day",
                "fow_color_r_night",
                "fow_darkess_day",
                "fow_darkess_night",
                "fow_darkness",
                "fps",
                "fraction",
                "fragility",
                "frame_mesh_handle_toggle",
                "frame_mesh_no_handle_toggle",
                "frame_mesh_type",
                "frame_only_no_supports",
                "frame_only",
                "frame",
                "framecount",
                "framerangesequence",
                "framerate",
                "framestart",
                "freepass_duration",
                "freepass_movetolerance",
                "freepass_peektime",
                "freepass_refillrate",
                "freepass_timetotrigger",
                "freezer_target_name",
                "frequency",
                "friction",
                "friendlyfire",
                "fronthiparmor_0",
                "fronthippouch_0_l",
                "fronthippouch_0_r",
                "frontleg_0_l",
                "frontleg_0_r",
                "frontleg_1_l",
                "frontleg_1_r",
                "frontlegcenter",
                "frontneck_l_clothnode",
                "frontneck_r_clothnode",
                "frozen",
                "frustum",
                "fuel",
                "full_luminositycolorvalue",
                "full_size",
                "fullanim",
                "fullbright",
                "fully_closed_sound",
                "fully_occluded_fraction",
                "fully_open_sound",
                "func_020",
                "func_040",
                "func_050",
                "func_100",
                "func_130",
                "func_200",
                "furniture_physics",
                "fx_disable",
                "fxtime",
                "gagleader",
                "game_data_list",
                "game",
                "gameendally",
                "gameeventitem",
                "gameeventname",
                "gamefeatureset",
                "gamemass",
                "gamematerial",
                "gametitle",
                "gametype",
                "gap_connector",
                "garagerolleropened",
                "garden_photo_landscape",
                "garden_portrait",
                "gassound",
                "gearratio",
                "generate_mips_for_matching_names",
                "generatelightmaps",
                "generatorstarted",
                "generic",
                "generichinttype",
                "generictype",
                "geometry_type",
                "gesture",
                "gesturename",
                "ggx_cubemap_blur_accumulation_pass_count",
                "gibangles",
                "gibanglevelocity",
                "gibdir",
                "gibgravityscale",
                "gibmodel",
                "gibs",
                "girder_style",
                "glass_glass",
                "glass_initial_state",
                "glass_material",
                "glass_thickness",
                "glassinframe",
                "glassnavignore",
                "glassthickness",
                "global_pose_template",
                "global",
                "globallightslot",
                "globalname",
                "globalstate",
                "gloves",
                "glow_effect",
                "glow_in_trigger_radius",
                "glow",
                "glowcolor",
                "glowdist",
                "glowdistancescale",
                "glowenabled",
                "glowproxysize",
                "glowrange",
                "glowrangemin",
                "glowstate",
                "glowstyle",
                "glowteam",
                "goal_node",
                "goal",
                "goalent",
                "goalradius",
                "goldpertick",
                "goldticktime",
                "good_filler_10",
                "good_filler_11",
                "good_filler_12",
                "good_filler_13",
                "good_filler_14",
                "good_filler_15",
                "good_filler_2",
                "good_filler_3",
                "good_filler_4",
                "good_filler_5",
                "good_filler_6",
                "good_filler_7",
                "good_filler_8",
                "good_filler_9",
                "good_rax_melee_bot",
                "good_rax_melee_mid",
                "good_rax_melee_top",
                "good_rax_range_bot",
                "good_rax_range_mid",
                "good_rax_range_top",
                "grab_radius",
                "graball",
                "grabattachmentname",
                "grabbitygloves",
                "grabentity",
                "graceperiod",
                "gradientfog",
                "gradientfogtexture",
                "grainstrength",
                "graphparameter",
                "grass_exclusion_radius",
                "grassparams",
                "grate_floor",
                "gravity_scale",
                "gravity",
                "gravitygrabignoremassandsize",
                "green_light",
                "green_window_broken",
                "green",
                "grenade_proclivity",
                "gridspacing",
                "grimey",
                "ground_cap",
                "groundscale",
                "group_number",
                "group",
                "group00",
                "group01",
                "group02",
                "group03",
                "group04",
                "group05",
                "group06",
                "group07",
                "group08",
                "group09",
                "group10",
                "group11",
                "group12",
                "group13",
                "group14",
                "group15",
                "groupbyprefab",
                "groupbyvolume",
                "groupid",
                "groupname",
                "groupnames",
                "groupothergroups",
                "guide_image",
                "gun_barrel_attach",
                "gun_base_attach",
                "gun_model",
                "gun_pitch_pose_center",
                "gun_pitch_pose_param",
                "gun_yaw_pose_center",
                "gun_yaw_pose_param",
                "gunrange",
                "gust_dir_change",
                "gust_duration",
                "gustdirchange",
                "gustduration",
                "hackdifficulty",
                "hackdifficultyname",
                "hacking_plug",
                "haloscale",
                "hammeruniqueid",
                "hammeruniqueidpath",
                "hand_l",
                "hand_r",
                "handindex",
                "handle_train_movement",
                "handlebars",
                "handpose_bone",
                "handpose_entity_name",
                "handpose_model",
                "handshake",
                "hanging_variation",
                "haptic_effect_name",
                "hapticstype",
                "harbour_landscape",
                "hardstopspeakevent",
                "hardware",
                "has_animated_face",
                "has_extent",
                "has_preferred_carryangles",
                "has_rotation",
                "has_spiral",
                "hascollisioninhand",
                "hasgun",
                "hasp_name",
                "hasscanners",
                "hazescale",
                "hdrcolorscale",
                "head_0",
                "head",
                "headcrab_nospawn",
                "headcrab_spawn",
                "headcrabcount",
                "headcrabtype",
                "headgun_0",
                "headlight_left_initial_counter",
                "headlight_left_initial_state",
                "headlight_right_initial_counter",
                "headlight_right_initial_state",
                "headlights_on_bool",
                "headlights_on_int",
                "headlights_state",
                "headtype",
                "headwear_color",
                "headwear_material",
                "headwear_model",
                "heal_distance",
                "health",
                "healthregenerateenabled",
                "heatlevel",
                "heattime",
                "height_fog_caustic_amplitude_scale",
                "height_fog_caustic_speed_scale",
                "height_fog_color_day",
                "height_fog_color_night",
                "height_fog_color",
                "height_fog_density_day",
                "height_fog_density_night",
                "height_fog_density",
                "height_fog_exclusion_height_bias",
                "height_fog_exclusion_inner_radius",
                "height_fog_falloff",
                "height_fog_max_z",
                "height_fog_rotation0",
                "height_fog_rotation1",
                "height_fog_scale0",
                "height_fog_scale1",
                "height_fog_scroll0u_day",
                "height_fog_scroll0u_night",
                "height_fog_scroll0u",
                "height_fog_scroll0v_day",
                "height_fog_scroll0v_night",
                "height_fog_scroll0v",
                "height_fog_scroll1u_day",
                "height_fog_scroll1u_night",
                "height_fog_scroll1u",
                "height_fog_scroll1v_day",
                "height_fog_scroll1v_night",
                "height_fog_scroll1v",
                "height_fog_texture0",
                "height_fog_texture1",
                "height_fog_textureopacity",
                "height_fog_type",
                "height",
                "heightfogadjustment",
                "heightfogdrawscale1",
                "heightfogdrawscale2",
                "heightfogmasktexture",
                "heightfogscale",
                "heightfogscale1",
                "heightfogscale2",
                "heightfogscrolldir1",
                "heightfogscrolldir2",
                "heightfogtexture",
                "heightfogworldscale",
                "heistbomb",
                "held_object",
                "heli_intro_skybox_flyby_high1",
                "heli_intro_skybox_flyby1",
                "heli_intro_skybox_flyby2",
                "helper_env_sun",
                "helpercolor",
                "helpername",
                "helperoffset",
                "hero_color_day",
                "hero_color_night",
                "hero_light_scale_day",
                "hero_light_scale_night",
                "hero_picker",
                "herocolor",
                "heroguidessupported",
                "herolightscale",
                "herominimapiconscale",
                "herorespawnenabled",
                "heroselectiontime",
                "hidden",
                "hiddenflags",
                "hide_ambulance_body",
                "hide_contents",
                "hide_door_1",
                "hide_door_2",
                "hide_door_left_front",
                "hide_door_left_rear",
                "hide_door_rear",
                "hide_door_right_front",
                "hide_door_right_rear",
                "hide_drawer_1",
                "hide_drawer_2",
                "hide_drawer_3",
                "hide_drawer_4",
                "hide_geo",
                "hide_hands",
                "hide_headlights",
                "hide_in_showcase",
                "hide_lid_left",
                "hide_lid_right",
                "hide_lid",
                "hide_lower_door",
                "hide_radius",
                "hide_rear_window",
                "hide_rear_windows",
                "hide_suitcases",
                "hide_tailgate",
                "hide_termite_mound_geo",
                "hide_truck_box",
                "hide_truck_empty",
                "hide_truck_flatbed",
                "hide_upper_door",
                "hide_utility_body",
                "hide_wheel_front_left",
                "hide_wheel_front_right",
                "hide_wheel_rear_left",
                "hide_wheel_rear_right",
                "hide_windscreen",
                "hide",
                "hideintools",
                "hidekillmessageheaders",
                "hierarchyattachname",
                "high",
                "hinge_start",
                "hingeaxis",
                "hingefriction",
                "hingeisbreakable",
                "hint_activator_caption",
                "hint_allow_nodraw_target",
                "hint_alphaoption",
                "hint_auto_start",
                "hint_binding",
                "hint_caption",
                "hint_color",
                "hint_custom_layoutfile",
                "hint_forcecaption",
                "hint_gamepad_binding",
                "hint_icon_offscreen",
                "hint_icon_offset",
                "hint_icon_onscreen",
                "hint_layoutfiletype",
                "hint_local_player_only",
                "hint_message",
                "hint_name",
                "hint_nooffscreen",
                "hint_pulseoption",
                "hint_range",
                "hint_replace_key",
                "hint_shakeoption",
                "hint_start_sound",
                "hint_static",
                "hint_target",
                "hint_timeout",
                "hint_vr_height_offset",
                "hint_vr_panel_type",
                "hint",
                "hintactivity",
                "hintgroup",
                "hintgroupchangereaction",
                "hintlimiting",
                "hinttype",
                "hip_0_l",
                "hip_0_r",
                "hitnormal",
                "hlvr_bloom_min_threshold",
                "hlvr_bloom_offset",
                "hlvr_bloom_scale",
                "hlvr_bloom_strength",
                "hlvr_disable_teleport",
                "hmd_brightness_level",
                "hold_on_stop",
                "holdanimation",
                "holdevent",
                "holdforever",
                "holdnoise",
                "holdtime",
                "hole_in_roof",
                "holiday_type",
                "holo_holo",
                "homingdelay",
                "homingduration",
                "homingrampdown",
                "homingrampup",
                "homingspeed",
                "homingstrength",
                "hood_initial_counter",
                "hood_initial_state",
                "hook_attachement",
                "hook_initial_completion",
                "horizontal_align",
                "horizontalglowsize",
                "horse_landscape",
                "hostagespawnexclusiongroup1",
                "hostagespawnexclusiongroup10",
                "hostagespawnexclusiongroup11",
                "hostagespawnexclusiongroup12",
                "hostagespawnexclusiongroup13",
                "hostagespawnexclusiongroup14",
                "hostagespawnexclusiongroup15",
                "hostagespawnexclusiongroup16",
                "hostagespawnexclusiongroup17",
                "hostagespawnexclusiongroup18",
                "hostagespawnexclusiongroup19",
                "hostagespawnexclusiongroup2",
                "hostagespawnexclusiongroup20",
                "hostagespawnexclusiongroup21",
                "hostagespawnexclusiongroup22",
                "hostagespawnexclusiongroup23",
                "hostagespawnexclusiongroup24",
                "hostagespawnexclusiongroup25",
                "hostagespawnexclusiongroup26",
                "hostagespawnexclusiongroup27",
                "hostagespawnexclusiongroup28",
                "hostagespawnexclusiongroup29",
                "hostagespawnexclusiongroup3",
                "hostagespawnexclusiongroup30",
                "hostagespawnexclusiongroup4",
                "hostagespawnexclusiongroup5",
                "hostagespawnexclusiongroup6",
                "hostagespawnexclusiongroup7",
                "hostagespawnexclusiongroup8",
                "hostagespawnexclusiongroup9",
                "hostagespawnrandomfactor",
                "hostagetype",
                "hourhand",
                "hud_icon",
                "hud_min_speed_level_1",
                "hud_min_speed_level_2",
                "hud_min_speed_level_3",
                "hud_res_file",
                "hud_type",
                "hue-rotate",
                "hull_name",
                "hullcheckmode",
                "hurt_me",
                "hurt_them",
                "icon",
                "iconsprite",
                "id",
                "idle_01",
                "idle_02",
                "idle_03",
                "idlemodifier",
                "idlespeed",
                "ignite_particle_name",
                "ignite_sound_name",
                "ignite",
                "ignitionpoint",
                "ignore_entity",
                "ignore_if_asset_has_extension",
                "ignore_if_key_present",
                "ignore_if_key_value_no_match",
                "ignore_input",
                "ignorebugbait",
                "ignoreclipbrushes",
                "ignoreconstraintonpickup",
                "ignoredclass",
                "ignoredebris",
                "ignoredentity",
                "ignoredname01",
                "ignoredname02",
                "ignoredname03",
                "ignoredname04",
                "ignoredname05",
                "ignoredname06",
                "ignoredname07",
                "ignoredname08",
                "ignoredname09",
                "ignoredname10",
                "ignoredname11",
                "ignoredname12",
                "ignoredname13",
                "ignoredname14",
                "ignoredname15",
                "ignoredname16",
                "ignorefacing",
                "ignoregraceupto",
                "ignorehand",
                "ignorehandposition",
                "ignorehandrotation",
                "ignoremoveparent",
                "ignorenormals",
                "ignoreplayer",
                "ignoresolid",
                "ignoreunseenenemies",
                "imagnitude",
                "impact_damage",
                "importance",
                "impulse_dir",
                "in_tangent_local",
                "in",
                "in1",
                "in2",
                "incavern",
                "include",
                "inclusive",
                "incomingsound",
                "indent",
                "index",
                "indirectenabled",
                "indirectlight",
                "indirectsimulationmode",
                "indirectsimulationscale",
                "indirectstrength",
                "indirectvoxeldim",
                "indirectvoxeldimx",
                "indirectvoxeldimy",
                "indirectvoxeldimz",
                "indoor_outdoor_level",
                "industrial",
                "inertia",
                "inertiafactor",
                "inertiascale",
                "infest_bloater1_trigger",
                "infinite_zombies",
                "infiniteenergy",
                "influence_cone",
                "influenceradius",
                "info",
                "initial_command",
                "initial_manhack_delay",
                "initial_orientation",
                "initial_position",
                "initial_rotation",
                "initial_state",
                "initial_time",
                "initial_waypoint",
                "initialanim",
                "initialcompletionamount",
                "initialdelay",
                "initialinputastexture",
                "initiallinkedportal",
                "initialmass",
                "initialoffset",
                "initialowner",
                "initialspeed",
                "initialstate",
                "initialvalue",
                "inmax",
                "inmin",
                "inner_angle",
                "inner_radius",
                "innerconeangle",
                "innerfardistance",
                "innerfareffect",
                "innerneardistance",
                "innerneareffect",
                "innerradius",
                "inpudoorsmash",
                "input_a_remap_param_s",
                "input_a_remap_param_t",
                "input_a_remap",
                "input_activate_button",
                "input_activate_console",
                "input_b_remap_param_s",
                "input_b_remap_param_t",
                "input_b_remap",
                "input_beginintro",
                "input_beginreviverdistantaudio",
                "input_beginsequence",
                "input_break_wall",
                "input_break_window_for_zombie_body",
                "input_c_remap_param_s",
                "input_c_remap_param_t",
                "input_c_remap",
                "input_cancel_footsteps",
                "input_change_level",
                "input_cheat_lever_pull",
                "input_cheat_start_ride",
                "input_cleanup",
                "input_close_door",
                "input_close_exit_doors",
                "input_close",
                "input_combine_killed",
                "input_crouchcancelpending",
                "input_crouchendhint",
                "input_crouchshowhint",
                "input_d_remap_param_s",
                "input_d_remap_param_t",
                "input_d_remap",
                "input_deactivate_console",
                "input_debug_enable_pt2",
                "input_debug_open_doors",
                "input_debug_open_the_plug",
                "input_debug_spawn_debris",
                "input_disable_initial_call",
                "input_disable_interaction",
                "input_disable",
                "input_disabled",
                "input_door_opened",
                "input_door_starts_closed",
                "input_dustfall",
                "input_elevator_door_noise_generator",
                "input_elevator_starts_sparking",
                "input_enable_antlion_to_unburrow_and_attack",
                "input_enable_door_teleport_blocker",
                "input_enable_interaction",
                "input_enable_teetertotter",
                "input_enable",
                "input_enabled",
                "input_end_credits",
                "input_extendlift",
                "input_first_credits",
                "input_footstep_impact",
                "input_force_drop_on_teleport_off",
                "input_force_drop_on_teleport_on",
                "input_force_player_release",
                "input_gate_opened",
                "input_gun_fired",
                "input_hc_killed",
                "input_imposter_elevator_arrives",
                "input_installcrank",
                "input_intro",
                "input_jumpcancelpending",
                "input_jumpendhint",
                "input_jumpshowhint",
                "input_kill_antlion_and_turret",
                "input_kill_antlion",
                "input_kill_combine",
                "input_kill_door",
                "input_kill_panel",
                "input_kill_strider",
                "input_kill_zombie",
                "input_killzombie",
                "input_light_blink",
                "input_light_off_red",
                "input_light_off",
                "input_light_on_red",
                "input_light_on",
                "input_lock_controls",
                "input_lock",
                "input_lower_exterior_solid",
                "input_lower_interior_nonsolid",
                "input_lowerpod",
                "input_mantlecancelpending",
                "input_mantleendhint",
                "input_mantleshowhint",
                "input_open_door",
                "input_open_lower_door",
                "input_open",
                "input_opencombinebarrierdoor",
                "input_outro",
                "input_pause_static_play_vo",
                "input_pause_static",
                "input_player_coughed_attack",
                "input_powerdown",
                "input_powerdownsubstation",
                "input_poweroff",
                "input_poweron",
                "input_powerup",
                "input_prevent_backtracking",
                "input_rat_lever",
                "input_reset_controls",
                "input_restart_footsteps",
                "input_retractlift",
                "input_reveal",
                "input_ride_pt2_begins",
                "input_scream_all",
                "input_set_branch_back_of_elevator_reached_to_true",
                "input_setopen",
                "input_setup_main_strider",
                "input_skin_dark",
                "input_skin_draw_dark",
                "input_skin_standard",
                "input_skip_to_end",
                "input_spark",
                "input_spawn_flashlight_guy",
                "input_spawn_items",
                "input_spawn_zombie",
                "input_spawn",
                "input_spawnheadcrab",
                "input_spawnnpcs",
                "input_spawnzombie",
                "input_speed_up_brick_timer_level_2",
                "input_speed_up_brick_timer_level_3",
                "input_standcancelpending",
                "input_standendhint",
                "input_standshowhint",
                "input_start_ajar",
                "input_start_background_strider",
                "input_start_choreo",
                "input_start_citizens",
                "input_start_crash",
                "input_start_foreground_strider",
                "input_start_hit_brick_timer",
                "input_start_incoming_call",
                "input_start_midground_strider",
                "input_start_strider",
                "input_start_van_ride",
                "input_startdistantreviverattracttimer",
                "input_stoplift",
                "input_suppressblindzombiesound",
                "input_toner_pt2_complete",
                "input_train_arrive",
                "input_train_crash",
                "input_train_move",
                "input_train_start",
                "input_train_stop",
                "input_trigger_action",
                "input_unblock_door_nav",
                "input_unlock_controls",
                "input_unlock",
                "input_unpause_static",
                "input_upper_exterior_nonsolid",
                "input_wakezombie",
                "input_zombie_small_brick_wall",
                "input",
                "inputactivatetonerpuzzle",
                "inputbreaklock",
                "inputclose",
                "inputclosea",
                "inputcloseb",
                "inputclosec",
                "inputclosecasing",
                "inputclosed",
                "inputclosedoors",
                "inputclosee",
                "inputclosestuck",
                "inputdebugopenall",
                "inputdooropenpigeon",
                "inputdopulse",
                "inputdopulseandstartloop",
                "inputelevatorcalled",
                "inputelevatorflicker",
                "inputelevatortravel",
                "inputfall",
                "inputfilter",
                "inputflickeroff",
                "inputflickeron",
                "inputflyaway",
                "inputgloveprep",
                "inputjostle",
                "inputkill",
                "inputkillpigeon",
                "inputkoolaidbreak",
                "inputlockdoor2",
                "inputname",
                "inputnotpowered",
                "inputoffset",
                "inputolgascenestart",
                "inputopen",
                "inputopena",
                "inputopenb",
                "inputopenc",
                "inputopencasing",
                "inputopend",
                "inputopendoors",
                "inputopene",
                "inputopenstuck",
                "inputpowered",
                "inputpoweroff",
                "inputpoweron",
                "inputrevealgloves",
                "inputsetupforkoolaid",
                "inputshardcleanup",
                "inputsilentopendoors",
                "inputsparkstart",
                "inputspawn",
                "inputspawncombine",
                "inputspawnflingtrigger",
                "inputspawnpigeon",
                "inputstartblinking",
                "inputstartblinkingred",
                "inputstartcyclists",
                "inputstartidle",
                "inputstartsequence",
                "inputstartstreetscene",
                "inputstopblinking",
                "inputtype",
                "inputunlockdoor2",
                "inrandompool",
                "inset_distance",
                "inside",
                "inspectioncombinedead",
                "inspector_specular_direction_day",
                "inspector_specular_direction_night",
                "inspector_view_fog_color_day",
                "inspector_view_fog_color_night",
                "instant_traversal",
                "int",
                "intact",
                "intangent",
                "intangenttype",
                "integer",
                "integervalue",
                "intensity",
                "interact_distance",
                "interact_shell",
                "interactas",
                "interaction_attachment_name",
                "interaction_inner_trigger",
                "interaction_trigger",
                "interactionattachmentname",
                "interactionbonename",
                "interactiondisabled",
                "interactive_close",
                "interactive_distance",
                "interactsas",
                "interactswith",
                "interactwith",
                "intercept_radius",
                "interior_trashcan_001_lid_body_hinge",
                "interior_trashcan_001_lid_hinge",
                "interp_time",
                "interpolatepositiontoplayer",
                "interpolationtime",
                "interpolationtype",
                "interpolationwrap",
                "interrupt_sound",
                "interruptability",
                "interval_max",
                "interval_min",
                "interval",
                "intro_dead_soldier",
                "intro_skybox_flyby_high1",
                "intro_skybox_flyby_high2",
                "intro_skybox_flyby1",
                "intro_skybox_flyby2",
                "intro",
                "introvariation",
                "inventory_enabled",
                "inventory_model",
                "inventory_name",
                "inventory_position",
                "invert_exclusion",
                "invert_filter_check",
                "invert_orientation",
                "invertallow",
                "invertstate1",
                "invertstate2",
                "invertstate3",
                "invuln_count",
                "invulnerable",
                "iradiusoverride",
                "irradiance_color_scale",
                "irradvolume",
                "is_agressive",
                "is_autoaim_target",
                "is_banner",
                "is_friendly_npc",
                "is_hidden",
                "is_medic",
                "is_powered",
                "is_quest_member",
                "is_security_door",
                "is_spline_node",
                "is2d",
                "isancient",
                "isarmed",
                "iscreep",
                "isdestination",
                "isdire",
                "isenabled",
                "isfirstbloodactive",
                "isfountainhand",
                "isgame",
                "ishero",
                "isillusion",
                "isinteractable",
                "islanecreep",
                "islocalplayer",
                "ismaster",
                "ismechanical",
                "isneutralunittype",
                "isnotarmed",
                "isphantom",
                "isplayable",
                "ispowered",
                "isprefab",
                "isproceduralentity",
                "isrealhero",
                "isrenderingenabled",
                "isrunning",
                "issafetoleave",
                "isshop",
                "issummoned",
                "istemplate",
                "istower",
                "istransparent",
                "item_1",
                "item_2",
                "item_3",
                "item_4",
                "item_def0",
                "item_def1",
                "item_def2",
                "item_def3",
                "item_def4",
                "item_def5",
                "item_def6",
                "item_def7",
                "itemclass",
                "itemcount",
                "itemfile",
                "itemholder",
                "itemname",
                "itemsfiles",
                "itemvelocity",
                "japanese_portrait",
                "jaw_0",
                "jetlength",
                "jiggle_front",
                "jiggle",
                "joint_a",
                "joint_b",
                "joint_c",
                "joint_d",
                "joint_e",
                "joint_f",
                "joint_g",
                "joint_h",
                "joint_i",
                "joint_j",
                "joint_k",
                "joint_l",
                "jointcount",
                "junction_orientation",
                "junction_toplogy",
                "justify_horizontal",
                "justify_vertical",
                "keep_realtime_radiosity_data",
                "keep_velocity",
                "keepupright",
                "key_light_target0",
                "key_light_target1",
                "key_light_target2",
                "key_light_target3",
                "key_light_target4",
                "key_light_target5",
                "key_light_target6",
                "key_light_target7",
                "key_name_strip_prefix",
                "key_path",
                "key_subkey",
                "key",
                "kill",
                "killcombine",
                "killweapons",
                "lab_whiteboard_stamp",
                "laddersurfaceproperties",
                "lagcompensate",
                "landing_entity_name",
                "landing_relative_offset",
                "landmark",
                "landtarget",
                "laneid",
                "large_fx_scale",
                "large_visible",
                "large",
                "laserentity",
                "lasertarget",
                "last_shot_chambered",
                "last_sniff",
                "latch",
                "latchisbreakable",
                "launchconenoise",
                "launchdelay",
                "launchdirection",
                "launchpositionname",
                "launchsmoke",
                "launchsound",
                "launchspeed",
                "launchtarget",
                "layeredonmod",
                "layername",
                "layersequence1",
                "layersequence2",
                "layout_file",
                "leaddistance",
                "leadduringcombat",
                "leadtarget",
                "leaf_a_1_l",
                "leaf_a_1_r",
                "leaf_a_3_l",
                "leaf_a_3_r",
                "leaf_b_1_l",
                "leaf_b_1_r",
                "leaf_b_3_l",
                "leaf_b_3_r",
                "leaf_c_1_l",
                "leaf_c_2_l",
                "leaf_c_2_r",
                "leaf_d_2_l",
                "leaf_d_2_r",
                "leaf_d_3_l",
                "leaf_d_3_r",
                "leaf_e_1_l",
                "leaf_e_1_r",
                "leaf_e_2_r",
                "leaf_f_1_r",
                "leaf_f_2_r",
                "leaf_g_1_r",
                "leaf_g_2_r",
                "leansphere",
                "left_down",
                "left_down002",
                "left_root",
                "left_root002",
                "left_up",
                "left_up002",
                "left",
                "leg_back_0_l",
                "leg_back_0_r",
                "leg_back_1_l",
                "leg_back_1_r",
                "leg_back_2_l",
                "leg_back_2_r",
                "leg_bone1_l",
                "leg_bone1_r",
                "leg_bone2_l",
                "leg_bone2_r",
                "leg_front_0_l",
                "leg_front_0_r",
                "leg_front_1_l",
                "leg_front_1_r",
                "leg_front_2_l",
                "leg_front_2_r",
                "leg_l_0",
                "leg_l_1",
                "leg_l_2",
                "leg_lower_l",
                "leg_lower_r",
                "leg_r_0",
                "leg_r_1",
                "leg_r_2",
                "leg_upper_l",
                "leg_upper_r",
                "legacy_source1_inverted_normal",
                "legarmora_0_l",
                "legarmora_0_r",
                "legarmorb_0_l",
                "legarmorb_0_r",
                "legs",
                "length",
                "lengthtexcoordend",
                "lengthtexcoordstart",
                "lerp_duration",
                "lerp_effect",
                "lerp_restore_movetype",
                "lerp_sound",
                "lerp_target_attachment",
                "lerp_target",
                "levitategoal_bottom",
                "levitategoal_top",
                "levitationarea",
                "lfomodpitch",
                "lfomodvol",
                "lforate",
                "lfotype",
                "lid_ajar_0-1",
                "lid_color",
                "lid_skin",
                "life",
                "lifetime_max",
                "lifetime_min",
                "lifetime",
                "lifetimemax",
                "lifetimemin",
                "light_class",
                "light_color",
                "light_covered_illuminated",
                "light_covered",
                "light_direction_dawn",
                "light_direction_day",
                "light_direction_dusk",
                "light_direction_night",
                "light_exposed_illuminated",
                "light_exposed",
                "light_level",
                "light_name",
                "light_names",
                "light_noise_interval",
                "light_noise_max",
                "light_noise_min",
                "light_probe_volume_from_cubemap",
                "light_radius",
                "light_scale_day",
                "light_scale_night",
                "light_style_output_event0",
                "light_style_output_event1",
                "light_style_output_event2",
                "light_style_output_event3",
                "light_style_radiance_var",
                "light_style_target0",
                "light_style_target1",
                "light_style_target2",
                "light_style_target3",
                "light_style_var",
                "light_style",
                "light_time_in",
                "light_time_out",
                "light",
                "light2_brightness",
                "light2_enabled",
                "lightchoice",
                "lightcolor",
                "lightcookie",
                "lightenabled",
                "lightfov",
                "lightgroup",
                "lightinglandmark",
                "lightingorigin",
                "lightingoriginname",
                "lightmap_queries",
                "lightmapresolutionx",
                "lightmapresolutiony",
                "lightmapscalebias",
                "lightmapstatic",
                "lightmaxdist",
                "lightning_color",
                "lightning_duration_max",
                "lightning_duration_min",
                "lightning_elevation",
                "lightning_fluctuation_max",
                "lightning_fluctuation_min",
                "lightning_intensity_max",
                "lightning_intensity_min",
                "lightning_period_max",
                "lightning_period_min",
                "lightning_sound",
                "lightning_spec_pow_scale_max",
                "lightning_spec_pow_scale_min",
                "lightning_specular_intensity",
                "lightningend",
                "lightningstart",
                "lightonlytarget",
                "lightoverride_brightness",
                "lightoverride_color",
                "lightoverride",
                "lightprobetexture_dli",
                "lightprobetexture_dls",
                "lightprobetexture_dlshd",
                "lightprobetexture",
                "lightradius",
                "lightscale",
                "lightsourceradius",
                "lightsourceshape",
                "lighttype",
                "lightworld",
                "limit_maxs",
                "limit_mins",
                "limit_to_material_group",
                "limit_to_piece",
                "limit_to_world",
                "limitbackward",
                "limitforward",
                "limitlocked",
                "limitstop",
                "limittoentity",
                "line",
                "linear_damping_ratio_x",
                "linear_damping_ratio_y",
                "linear_damping_ratio_z",
                "linear_frequency_x",
                "linear_frequency_y",
                "linear_frequency_z",
                "linear_motion_x",
                "linear_motion_y",
                "linear_motion_z",
                "linear",
                "lineardampingratio",
                "linearforcepointat",
                "linearfrequency",
                "link",
                "linkagegroupid",
                "linked_cp_1",
                "linked_cp_2",
                "linked_cp_3",
                "linked_cp_4",
                "linked_cp_5",
                "linked_cp_6",
                "linked_cp_7",
                "linked_cp_8",
                "linked_pathtrack_1",
                "linked_pathtrack_2",
                "linked_pathtrack_3",
                "linked_pathtrack_4",
                "linked_pathtrack_5",
                "linked_pathtrack_6",
                "linked_pathtrack_7",
                "linked_pathtrack_8",
                "linktype",
                "lip",
                "listen_entityspawns",
                "listener",
                "listenfilter",
                "lit",
                "loadatruntime",
                "loadifnested",
                "loadtime",
                "local_gravity_cp",
                "local.angles",
                "local.origin",
                "local.scales",
                "local",
                "localcontrastedgestrength",
                "localcontraststrength",
                "localforce",
                "localrotation",
                "location",
                "locationproxy",
                "lock",
                "lockbodyfacing",
                "locked_anim",
                "locked_sentence",
                "locked_sound",
                "locked",
                "lockedsound",
                "lockobjectrequirement",
                "lockpoint",
                "lockrotationalpha",
                "locksilently",
                "lod_level",
                "lodlevel",
                "long_bend",
                "long_elbow",
                "longuseactiontype",
                "longuseduration",
                "lookat",
                "lookatname",
                "lookspeed",
                "looktime",
                "looktype",
                "loop_break_on_damage",
                "loop_break_on_flashlight",
                "loop_in_action",
                "loop",
                "loopcount",
                "looping",
                "loopmovement",
                "loopmovesound",
                "looptime",
                "loopvideo",
                "loose_segment",
                "lootableitem",
                "loser_respawn_bonus_per_bot",
                "low",
                "lower_color",
                "lower_door_ajar_0-1",
                "lower_door_color",
                "lower_hemisphere_is_black",
                "lower_material",
                "lower_model",
                "lowerrandombound",
                "lowershirt_l",
                "lowershirt_mid",
                "lowershirt_r",
                "lowerthreshold",
                "lowpriority",
                "luminaire_anisotropy",
                "luminaire_shape",
                "luminaire_size",
                "lunar_phase",
                "m_activevalue",
                "m_baddifmissing",
                "m_ballowcameramovement",
                "m_ballowcustominterruptconditions",
                "m_bcordonsvisible",
                "m_bdisablenpccollisions",
                "m_bignoregravity",
                "m_biscordoning",
                "m_bkeepanimgraphlockedpost",
                "m_bloopactionsequence",
                "m_bsynchpostidles",
                "m_choicegroups",
                "m_choices",
                "m_choicevalues",
                "m_choicevariables",
                "m_color",
                "m_drawcalls",
                "m_extrastreams",
                "m_flgiblife",
                "m_flmoveinterptime",
                "m_flradius",
                "m_flrepeat",
                "m_flvariance",
                "m_flvelocity",
                "m_fmoveto",
                "m_igibs",
                "m_iszabilityname",
                "m_iszcustommove",
                "m_iszentity",
                "m_iszentry",
                "m_iszidle",
                "m_iszmodifiertoaddonplay",
                "m_isznewtarget",
                "m_isznextscript",
                "m_isznpc",
                "m_iszplay",
                "m_iszpostidle",
                "m_isztarget",
                "m_iteamnum",
                "m_material",
                "m_meshresourcename",
                "m_nabilitytargetbodyloc",
                "m_nabilitytargetorigin",
                "m_ndrawcallindex",
                "m_ngroundikpreference",
                "m_nhash",
                "m_nmeshindex",
                "m_normals",
                "m_positions",
                "m_pvertexdata",
                "m_referencedmeshsnapshots",
                "m_soundname",
                "m_sourceentityname",
                "m_texcoords",
                "magnet_radius",
                "magnetname",
                "magnitude",
                "mainsoundscapename",
                "manhack_proclivity",
                "manhacks",
                "manualaccelspeed",
                "manualdecelspeed",
                "manualspeedchanges",
                "map_asset_references",
                "map_extensions",
                "map_options",
                "map",
                "mapfarz",
                "mapname",
                "maps",
                "mapunitname",
                "mapusagetype",
                "mapvariables",
                "mapversion",
                "mark_as_removable",
                "markercolor",
                "markertipsize",
                "markertype",
                "maroon_window_broken",
                "maroon",
                "mass",
                "massbias",
                "massoverride",
                "massscale",
                "master",
                "match_summary",
                "matchbynameonly",
                "matchingteleporter",
                "matchlinkedangles",
                "matchsummary",
                "material_group_name",
                "material_param",
                "material",
                "materialgroup_name",
                "materialgroup",
                "materialname",
                "materialnames",
                "materialoverride",
                "materials",
                "materialsetassignments",
                "materialsets",
                "materialvar",
                "max_angle",
                "max_angular_velocity",
                "max_delay",
                "max_distance",
                "max_gust_delay",
                "max_gust",
                "max_health_category ",
                "max_health_category",
                "max_length",
                "max_lightmap_resolution",
                "max_occlusion_distance",
                "max_pass_range",
                "max_points",
                "max_range",
                "max_ride_speed",
                "max_rotation",
                "max_simulation_time",
                "max_twist_angle",
                "max_wind",
                "max_zombies",
                "max",
                "maxactive",
                "maxallies",
                "maxangaccel",
                "maxangvelocity",
                "maxanimtime",
                "maxballbounces",
                "maxburstdelay",
                "maxburstsize",
                "maxcount",
                "maxcount1",
                "maxcount2",
                "maxcount3",
                "maxcpulevel",
                "maxdelay",
                "maxdirectedspeed",
                "maxdist",
                "maxdxlevel",
                "maxexposure",
                "maxfactor",
                "maxfactor1",
                "maxfactor2",
                "maxfactor3",
                "maxfalloff",
                "maxgpulevel",
                "maxgust",
                "maxgustdelay",
                "maximum_value",
                "maximumchangepersecond",
                "maximumdistancefromline",
                "maximumstate",
                "maxlivechildren",
                "maxlogexposure",
                "maxmedics",
                "maxnpccount",
                "maxobjects",
                "maxoccludeearea_x360",
                "maxoccludeearea",
                "maxpieces",
                "maxpiecesdx8",
                "maxplayers",
                "maxpropscreenwidth",
                "maxragdollcount",
                "maxragdollcountdx8",
                "maxrange",
                "maxres",
                "maxresmobile",
                "maxs",
                "maxscale",
                "maxscore",
                "maxsearchdist",
                "maxshadowdistance",
                "maxskyboxrefiretime",
                "maxslidetime",
                "maxsoundthreshold",
                "maxspeed",
                "maxthenanydispatchdist",
                "maxthrust",
                "maxtimeout",
                "maxweight",
                "maxwind",
                "measurereference",
                "measuretarget",
                "measuretype",
                "med",
                "medium_visible",
                "medium",
                "melee_immune",
                "mesh_edge_connectivity",
                "mesh_edge_convexity_or_tileset",
                "mesh_edge_convexity",
                "mesh_face_world_z",
                "meshdata",
                "meshes",
                "message",
                "messageattenuation",
                "messagesound",
                "messagevolume",
                "method",
                "mild_damage",
                "mill_landscape",
                "min_advance_range_override",
                "min_angle",
                "min_delay",
                "min_gust_delay",
                "min_gust",
                "min_impact_damage_speed",
                "min_points",
                "min_rotation",
                "min_twist_angle",
                "min_use_angle",
                "min_wind",
                "min",
                "minangle",
                "minanimtime",
                "minblendrate",
                "minburstdelay",
                "minburstsize",
                "mincount",
                "mincount1",
                "mincount2",
                "mincount3",
                "mincpulevel",
                "mindirectedspeed",
                "mindist",
                "mindxlevel",
                "minexposure",
                "minfactor",
                "minfactor1",
                "minfactor2",
                "minfactor3",
                "minfalloff",
                "mingpulevel",
                "mingust",
                "mingustdelay",
                "minhealthdmg",
                "minhitpointstocommit",
                "mini_root",
                "minigametype",
                "minimalprecache",
                "minimum_encode_quality",
                "minimum_value",
                "minimumhitpoints",
                "minimumstate",
                "minlength",
                "minlifeafterportal",
                "minlogexposure",
                "minoccluderarea_x360",
                "minoccluderarea",
                "minplayers",
                "minpropscreenwidth",
                "minrange",
                "minroughness",
                "mins",
                "minscale",
                "minsearchdist",
                "minskyboxrefiretime",
                "minslidetime",
                "minsoundthreshold",
                "minspawndistance",
                "minspeed",
                "minthrust",
                "mintimeout",
                "minutehand",
                "minwind",
                "mip_algorithm",
                "mip_filter_strength",
                "mirror_player",
                "misses",
                "missilehint",
                "missilemodel",
                "missing",
                "missingweaponconceptmodifier",
                "mixed_1",
                "mixedshadows",
                "mode",
                "model_left_handed",
                "model_name",
                "model_right_handed",
                "model_state",
                "model_to_use",
                "model",
                "modeloverride",
                "modelpath",
                "models_gamedata",
                "modelscale",
                "modelstatechoices",
                "moderate_damage",
                "modification",
                "momentary",
                "momentummodifier",
                "momentumtype",
                "money",
                "moodbodyname",
                "moodname",
                "moodparam",
                "morph_name",
                "motordampingratio",
                "motorfrequency",
                "motormaxforcemultiplier",
                "mountain_portrait",
                "move_speed_reduction",
                "move_speed",
                "move",
                "moveable",
                "moveactivity",
                "movedir_islocal",
                "movedir_type",
                "movedir",
                "movedistance",
                "movementsound",
                "movementspeed",
                "movementstyle",
                "movementtype",
                "movepingsound",
                "movesnd",
                "movesound",
                "movesoundmaxpitch",
                "movesoundmaxtime",
                "movesoundminpitch",
                "movesoundmintime",
                "movespeed",
                "moveto",
                "movetopointdelay",
                "moviefilename",
                "moving_sound",
                "multitool_hack",
                "multitool_shock",
                "multitool_toner",
                "multitool",
                "mustreachfront",
                "mute_impact_effects",
                "muteradio",
                "muzzle_flash_effect",
                "name",
                "names",
                "nav_attribute_avoid",
                "nav_ignore",
                "navagentnumhulls",
                "navcellheight",
                "navcellsize",
                "navdata",
                "navdetailsampledistance",
                "navdetailsamplemaxerror",
                "navedgemaxerror",
                "navedgemaxlen",
                "navmarkupentity",
                "navprop",
                "navproperty_navattributes",
                "navproperty_navgen",
                "navproperty_navgenproj",
                "navregionmergesize",
                "navregionminsize",
                "navrestrictionvolume",
                "navsmallareaonedgeremovalsize",
                "navstepheight",
                "navtilesize",
                "navvertexattributevertexnormal",
                "navvertsperpoly",
                "near_blue",
                "near_blur",
                "near_focus",
                "near_green",
                "near_radius",
                "near_red",
                "near_weight",
                "nearclipplane",
                "nearz",
                "neck_0",
                "needs_weapon",
                "negated",
                "neutraltype",
                "neverinspectplayers",
                "neverleaveplayersquad",
                "neversayhello",
                "nevertimeout",
                "newchoicegroup",
                "newhintgroup",
                "newlevelunit",
                "newtarget",
                "newunit",
                "next_action_point",
                "next_dialog",
                "nextassaultpoint",
                "nextdecalid",
                "nextkey",
                "nextmap",
                "ngreplaceglobalregexpwithstrin",
                "no_ammo_sound",
                "no_reflection_fog",
                "no_save",
                "no",
                "noblend",
                "nocompress",
                "nodamageforces",
                "node_exit",
                "node_id",
                "node01",
                "node02",
                "node03",
                "node04",
                "node05",
                "node06",
                "node07",
                "node08",
                "node09",
                "node10",
                "node11",
                "node12",
                "node13",
                "node14",
                "node15",
                "node16",
                "node17",
                "node18",
                "node19",
                "node20",
                "nodefaultmodpath",
                "nodefov",
                "nodeheight",
                "nodeid",
                "nodeinstancedata",
                "nodes",
                "nodmgforce",
                "nogibshadows",
                "nohandle",
                "nointeriorseam",
                "nointerpolate",
                "noise_generator",
                "noise",
                "noise1",
                "noise2",
                "noiseamplitude",
                "noisecontrast",
                "noisedirectiona",
                "noisedirectionb",
                "noisemovementspeeda",
                "noisemovementspeedb",
                "noisescale",
                "noisestrength",
                "noisetype",
                "nolistrepeat",
                "nolod",
                "nomip",
                "noncombat",
                "none_glass",
                "none_holo",
                "none_none",
                "none",
                "normal",
                "normalizechildmodelupdates",
                "norotate",
                "northoffset",
                "not_visible_five",
                "not_visible_four",
                "not_visible_one",
                "not_visible_three",
                "not_visible_two",
                "note",
                "notifyforce_x",
                "notifyforce_y",
                "notifyforce_z",
                "notifyforcemintime_x",
                "notifyforcemintime_y",
                "notifyforcemintime_z",
                "notifynavfailblocked",
                "notsolid",
                "nowind",
                "npc_damaged",
                "npc_died",
                "npc_foot_sweep_enabled",
                "npc_lock_delay",
                "npc_man_point",
                "npcfirstwaypoint",
                "npchintgroup",
                "npcname",
                "npcpoints",
                "npcscriptname",
                "npcsquadname",
                "npcstate1",
                "npcstate2",
                "npcstate3",
                "npctargetname",
                "npctemplate",
                "npctemplate1",
                "npctemplate2",
                "npctemplate3",
                "npctemplate4",
                "npctemplate5",
                "npctemplate6",
                "npctype",
                "npctype1",
                "npctype2",
                "npctype3",
                "npcuniquename",
                "num_attached_items",
                "num_sections",
                "numcascades",
                "numframes",
                "numgrenades",
                "nummanhacks",
                "numsegments",
                "numsides",
                "numtimesteps",
                "object_culling",
                "object_type",
                "objective_model",
                "objectrequirement",
                "occlusion_exponent",
                "occlusion_scale",
                "occlusionmax",
                "occlusionmin",
                "occlusionradius",
                "off",
                "officer_reinforcements",
                "offset_angles",
                "offset_origin",
                "offset",
                "offsettype",
                "omnilight",
                "on",
                "onbarricadeunlocked",
                "onbatteryinserted",
                "onbatteryremoved",
                "onboardhealthchanged",
                "onboardremoved",
                "onbreak",
                "onbreakboard",
                "onbreaklock",
                "onbuttonpressed",
                "oncompletion",
                "ondoorhackcompleted",
                "ondoorhackstarted",
                "ondoorhackstopped",
                "ondoorunlocked",
                "one_handed",
                "one-way",
                "onenergypulled",
                "onfirstimpact",
                "onhologramstarted",
                "oninteractstart",
                "oninteractstop",
                "onlaunch",
                "onlockbreak",
                "onlyfallingplayers",
                "onlyinspectplayers",
                "onlyrunbackward",
                "onlyrunforward",
                "onlyvelocitycheck",
                "onnpcdeath",
                "onpickup",
                "onplayerdeath",
                "onplayerpickupflashlight",
                "onpryboard",
                "onpushed",
                "onpushedlocked",
                "onstartcombinecombatmusic",
                "onstartcombineintromusic",
                "onstopcombinecombatmusic",
                "onswitchedoff",
                "onswitchedon",
                "ontakedamage",
                "ontriggerchance1",
                "ontriggerchance2",
                "ontriggerchance3",
                "ontriggerchance4",
                "ontriggerchance5",
                "ontriggerchance6",
                "ontriggerchance7",
                "ontriggerchance8",
                "onwheelinserted",
                "onworldimpact",
                "opacity",
                "open_away",
                "open_ease",
                "open_idle",
                "open_or_different_tileset",
                "open_sound",
                "open",
                "opencompletesound",
                "opendir",
                "opened",
                "openthedoor",
                "operatorname",
                "optimizedheightfieldname",
                "opvararrayindex",
                "opvarindex",
                "opvarname",
                "opvaruseautocompare",
                "opvarvalue",
                "opvarvaluestring",
                "opvarvaluetype",
                "orientation",
                "orientationtype",
                "orientedwirebox",
                "origin_left",
                "origin_max_delta",
                "origin_relative",
                "origin_right",
                "origin",
                "originoverride",
                "ortholightheight",
                "ortholightwidth",
                "other_blocker",
                "other_doors_to_open",
                "out_tangent_local",
                "out",
                "out1",
                "out2",
                "outer_angle",
                "outer_radius",
                "outerconeangle",
                "outereffect",
                "outermaxdist",
                "outerradius",
                "outpostname",
                "output_advisor_seen",
                "output_allcablesbroken",
                "output_batteryinserted",
                "output_blinked",
                "output_brick_door_smashed",
                "output_bz_showtime",
                "output_cabinet_door_3_opened",
                "output_catwalk_choreo_finish",
                "output_change_level",
                "output_choreo_completed",
                "output_choreo_ended",
                "output_cleanup_outside",
                "output_close_train_door",
                "output_combine_speaks",
                "output_controls_complete",
                "output_controls_unlocked",
                "output_controlsused",
                "output_died",
                "output_disable",
                "output_door_closed",
                "output_elevatoratbottom",
                "output_enable",
                "output_enableexitlift",
                "output_format",
                "output_interact_start",
                "output_intro_complete",
                "output_npc_damaged",
                "output_npc_died",
                "output_on_disabled",
                "output_on_enabled",
                "output_on_fully_opened",
                "output_on_hack_started",
                "output_on_player_near",
                "output_on_roof_headcrab_death",
                "output_onalldead",
                "output_onbatteryplaced",
                "output_onbuttonpressed",
                "output_onbuttonpushed",
                "output_onchargerdeath",
                "output_onclosed",
                "output_oncompleted",
                "output_ondeath",
                "output_ondialogueend",
                "output_ondooropened",
                "output_onelevatoratstreet",
                "output_onelevatorcrash",
                "output_onfullyopened",
                "output_onhackfail",
                "output_onhackstarted",
                "output_onhacksuccess",
                "output_onheadcrabescape",
                "output_onheadcrabescapehitpipe",
                "output_onhideoutdoorclosed",
                "output_onhideoutdooropen",
                "output_onintrofinished",
                "output_onlock",
                "output_onopened",
                "output_onplaying",
                "output_onpodopen",
                "output_onpuzzlesuccess",
                "output_onrackmissingtankopened",
                "output_onreservoirbreak",
                "output_onreviverdeath",
                "output_onreviverspawned",
                "output_onrollerdoorclosed",
                "output_onrollerdooropen",
                "output_onsequenceend",
                "output_onstartplaying",
                "output_onstopplaying",
                "output_ontankadded",
                "output_ontimeout",
                "output_ontoiletbreak",
                "output_onunlock",
                "output_open_train_door",
                "output_openpitgateprefab",
                "output_pistol_gag",
                "output_plug_opened",
                "output_rescue_complete",
                "output_ride_pt1_ended",
                "output_ride_starts",
                "output_russell_safe_house_completed",
                "output_spawn_combine",
                "output_ss_end",
                "output_start_post_credit",
                "output_stop_train",
                "output_strider_chop",
                "output_strider_collapsed_from_building_debris",
                "output_strider_dies",
                "output_strider_has_bashed_the_bathroom_wall",
                "output_strider_taking_damage_from_gatling_gun_second_stage",
                "output_strider_taking_damage_from_gatling_gun",
                "output_suppressorspawned",
                "output_take_damage",
                "output_train_arrival",
                "output_traincar_entered",
                "output_traincar_exit",
                "output_traincar_opened",
                "output_tunnel_opened",
                "output_van_crash",
                "output_zombie_damaged",
                "output_zombie_died",
                "outputalarmstarts",
                "outputbarricadebroken",
                "outputbarricadeopened",
                "outputdetachonehose",
                "outputdoor3grabbed",
                "outputdoor3notgrabbed",
                "outputdoor4grabbed",
                "outputdoor4notgrabbed",
                "outputdoorbreakslam",
                "outputdoorslammedopencinematic",
                "outputdoorunlocked",
                "outputentity",
                "outputentity2",
                "outputentity3",
                "outputentity4",
                "outputexploded",
                "outputfence1closes",
                "outputfence2closes",
                "outputfence3closes",
                "outputfullyopen",
                "outputidlestartedorlooped",
                "outputinstalledbattery",
                "outputinstalledfuse",
                "outputinstalledgravitygloves",
                "outputname",
                "outputonallcablescut",
                "outputonallcableshacked",
                "outputonblast",
                "outputondestroyed",
                "outputonpulse",
                "outputonpuzzlecomplete",
                "outputonstartdestroy",
                "outputpoweroff",
                "outputpoweron",
                "outputpulsetriggered",
                "outputremovedbattery",
                "outputremovedfuse",
                "outputstaticstarted",
                "outputtouched",
                "outputtype",
                "outputuntouched",
                "outputvaultviewopened",
                "outro",
                "outside",
                "outtangent",
                "outtangenttype",
                "overlayboxsize",
                "overlaycolor",
                "overlaymaterial",
                "overlayname1",
                "overlayname10",
                "overlayname2",
                "overlayname3",
                "overlayname4",
                "overlayname5",
                "overlayname6",
                "overlayname7",
                "overlayname8",
                "overlayname9",
                "overlaysize",
                "overlaytime1",
                "overlaytime10",
                "overlaytime2",
                "overlaytime3",
                "overlaytime4",
                "overlaytime5",
                "overlaytime6",
                "overlaytime7",
                "overlaytime8",
                "overlaytime9",
                "overload_fx",
                "override_shadow_farz",
                "override_sound_event",
                "override_stats_panel",
                "overrideblocklos",
                "overridemodelname",
                "overrideparam",
                "overridescript",
                "overridesound",
                "overridewalktorunspeed",
                "owner_context",
                "packjgl",
                "padlock_name",
                "padlock_tripmines",
                "padlock",
                "paint",
                "painted_clean",
                "painted",
                "paintedcolor",
                "paintinterval",
                "paintintervalvariance",
                "panel_class_name",
                "panel_dpi",
                "panel_id",
                "panel_type",
                "panelname",
                "parametername",
                "parameters",
                "parameters2",
                "parameters3",
                "parent_bodygroup_name",
                "parent_bodygroup_value",
                "parent_bone_or_attachment",
                "parent_bone",
                "parent_map",
                "parent_piece",
                "parent",
                "parentattachment",
                "parentattachmentname",
                "parentattachname",
                "parentname",
                "parm1",
                "parm10",
                "parm2",
                "parm3",
                "parm4",
                "parm5",
                "parm6",
                "parm7",
                "parm8",
                "parm9",
                "part04_0",
                "part04_1",
                "part04_10",
                "part04_11",
                "part04_12",
                "part04_13",
                "part04_14",
                "part04_15",
                "part04_16",
                "part04_17",
                "part04_18",
                "part04_19",
                "part04_2",
                "part04_20",
                "part04_21",
                "part04_22",
                "part04_23",
                "part04_24",
                "part04_25",
                "part04_26",
                "part04_27",
                "part04_28",
                "part04_29",
                "part04_3",
                "part04_30",
                "part04_31",
                "part04_32",
                "part04_33",
                "part04_4",
                "part04_5",
                "part04_6",
                "part04_7",
                "part04_8",
                "part04_9",
                "part04_extraa_0",
                "part04_extraa_1",
                "part04_extraa_2",
                "part04_extraa_3",
                "part04_extrab_0",
                "part04_extrab_1",
                "part04_extrab_2",
                "part04_extrab_3",
                "part04_extrab_4",
                "part04_extrab_5",
                "part04_extrab_6",
                "part04_extrab_7",
                "part04_extrac_0",
                "part04_extrac_1",
                "part04_extrac_2",
                "part04_extrad_0",
                "part04_extrad_1",
                "part04_extrae_0",
                "part04_extrae_1",
                "part04_extrae_2",
                "part04_extrae_3",
                "part04_extraf_0",
                "part04_extraf_1",
                "part04_extraf_2",
                "part04_extrag_0",
                "part04_extrag_1",
                "part04_extrag_2",
                "part04_extrag_3",
                "part04_extrag_4",
                "part04_extrah_0",
                "part04_extrah_1",
                "part04_extrah_2",
                "part04_extrah_3",
                "part04_extrah_4",
                "part04_extrah_5",
                "part04_extraj_0",
                "part04_extraj_1",
                "part04_extraj_2",
                "part04_extraj_3",
                "part04_extraj_4",
                "part06_0",
                "part06_1",
                "part06_10",
                "part06_11",
                "part06_12",
                "part06_13",
                "part06_14",
                "part06_15",
                "part06_16",
                "part06_17",
                "part06_18",
                "part06_19",
                "part06_2",
                "part06_20",
                "part06_21",
                "part06_22",
                "part06_23",
                "part06_24",
                "part06_3",
                "part06_4",
                "part06_5",
                "part06_6",
                "part06_7",
                "part06_8",
                "part06_9",
                "partial_cap_points_rate",
                "partial_radius",
                "partial_spawner",
                "particle_attachment_type",
                "particle_effect",
                "particle_preroll",
                "particle_spacing",
                "particle_system_name",
                "particle_tint_color",
                "particledrawwidth",
                "particleeffect",
                "particleoverride",
                "particlespacingdistance",
                "particletint",
                "particletrailendsize",
                "particletraillifetime",
                "particletrailmaterial",
                "particletrailstartsize",
                "particletype",
                "passactivator",
                "passthroughcaller",
                "patch_date",
                "patch_name",
                "patch_version",
                "path_entity",
                "path_flyby_high1_hidden",
                "path_flyby1_hidden",
                "path_flyby2_hidden",
                "path_generic",
                "path_index",
                "path_start",
                "path_uniqueid",
                "path",
                "pathcornerentity",
                "pathcornerentityname",
                "pathcornername",
                "pathnodecolors",
                "pathnodename",
                "pathnodenames",
                "pathnodepinsenabled",
                "pathnoderadiusscales",
                "pathnodes",
                "pathnodesjson",
                "patrolspeed",
                "pattern",
                "pauseduration",
                "peak",
                "pedal_l",
                "pedal_r",
                "peephole_axis",
                "pelvis",
                "penaltiesenabled",
                "percent_bright_pixels",
                "percent_target",
                "performancemode",
                "persistence",
                "persistence2",
                "pervertexlighting",
                "pervertexlightingindices",
                "petpopulation",
                "photogrammetry_name",
                "phys_level_water_entity",
                "phys_start_asleep",
                "physdamagescale",
                "physicsgroup",
                "physicsimpactincrement",
                "physicsinteractsas",
                "physicsinteractsexclude",
                "physicsinteractswith",
                "physicsmode",
                "physicssimplificationerror",
                "physicssimplificationoverride",
                "physicsspeed",
                "physicstype",
                "pickbysize",
                "pickup_and_filled_sound",
                "pickup_filter_name",
                "pickup_particle",
                "pickup_particles",
                "pickup_radius_override",
                "pickup_script_func",
                "pickup_sound",
                "pin_enabled",
                "pinenabled",
                "pingtype",
                "pipe_section",
                "pipe_to_square_segments_transition",
                "pistol_upgrade_bullethopper",
                "pistol_upgrade_burstfire",
                "pistol_upgrade_lasersight",
                "pistol_upgrade_reflexsight",
                "pistol",
                "pitch",
                "pitchmax",
                "pitchmin",
                "pitchrange",
                "pitchrate",
                "pitchstart",
                "pitchtolerance",
                "pivot_max",
                "pivot_min",
                "play_all_rounds",
                "play_endcap",
                "player_fire_only",
                "player_id",
                "player_usable",
                "playeractorfov",
                "playeractorfovtruecone",
                "playeractorlos",
                "playeractorproximity",
                "playerbattleline",
                "playerblockingactor",
                "playergraceperiod",
                "playerindex",
                "playerinvehicle",
                "playerlocktimebeforefire",
                "playernumber",
                "playerowner",
                "playerreachedgun",
                "playerspeed",
                "playerstartinggold",
                "playertargetfov",
                "playertargetfovtruecone",
                "playertargetlos",
                "playertargetproximity",
                "playopenedsound",
                "playsequenceoverscript",
                "playsound",
                "playswitchonsound",
                "plugtypes",
                "poi",
                "point_a",
                "point_b",
                "point_default_owner",
                "point_group",
                "point_index",
                "point_prefab",
                "point_printname",
                "point_start_locked",
                "point_template_hidden",
                "point_warn_on_cap",
                "point_warn_sound",
                "point0",
                "point1",
                "pointcamera",
                "points_per_player",
                "points",
                "pointvalue",
                "policeradius",
                "policetarget",
                "pool_max",
                "pool_regen_amount",
                "pool_regen_time",
                "pool_start",
                "portal_collision_name",
                "portal_membrane_name",
                "portal_name",
                "portal_nav_blocker_name",
                "portaloo_hc",
                "portaltwo",
                "portalversion",
                "pose_control_type",
                "pose_special_type",
                "pose",
                "poseparametername",
                "poseparamx",
                "poseparamy",
                "posevalue",
                "position",
                "position0",
                "position1",
                "position2",
                "position3",
                "position4",
                "position5",
                "position6",
                "position7",
                "positionid",
                "positioninterpolator",
                "positiveresistance",
                "post",
                "postarrivalconceptmodifier",
                "postcommands",
                "postfiredelay",
                "postfix_lines",
                "postgametime",
                "postmaploadcommands",
                "postolerance",
                "postprocessing",
                "postprocessname",
                "postspawndirection",
                "postspawndirectionvariance",
                "postspawninheritangles",
                "postspawnspeed",
                "posture",
                "powered",
                "powerreceive",
                "powerreceivestop",
                "powersend",
                "powersendstop",
                "powerunit_has_battery",
                "powerup_model",
                "preciptype",
                "precisemovement",
                "precommands",
                "precomputedboundsmaxs",
                "precomputedboundsmins",
                "precomputedfieldsvalid",
                "precomputedirection",
                "precomputedmaxrange",
                "precomputedminrange",
                "precomputedobbangles",
                "precomputedobbextent",
                "precomputedobborigin",
                "precomputelightprobes",
                "precomputeposition",
                "precomputeup",
                "precreate_segment_spawn_targets",
                "preferred_carryangles",
                "preferred_catch_angles",
                "preferred_catch_origin",
                "preferredcarryangles",
                "pregametime",
                "preset",
                "pressuredelay",
                "prestige",
                "prevent_grenade_explosive_removal",
                "prevent_hand_to_hand_pickup",
                "prevent_movement",
                "prevent_update_yaw_on_finish",
                "preventpropcombine",
                "preventtripping",
                "preview_day_or_night",
                "preview_inspector_view",
                "primary_linked_ability_1",
                "primary_linked_ability_2",
                "primary_linked_ability_3",
                "primary_linked_ability_4",
                "primary_sky_transfer_count",
                "primary_transfer_count",
                "primary_transfer_num_directional_samples",
                "priority_energy_target",
                "priority_grab_name",
                "priority",
                "probability",
                "proboscis_0",
                "proboscis_2",
                "procedural",
                "projectionfar",
                "projectionmode",
                "projectiontargets",
                "projectonbackfaces",
                "projectoncharacters",
                "projectonwater",
                "projectonworld",
                "prop_drop_sound",
                "prop_model_name",
                "prop_pickup_sound",
                "propdata_override",
                "propdata",
                "properties",
                "propertynameindices",
                "propertynames",
                "propertystrings",
                "propertyvalueindices",
                "propertyvalues",
                "propname",
                "proximitydistance",
                "proximityoffset",
                "proxy",
                "proxyattachment",
                "proxyname",
                "psname",
                "pulltype",
                "pulsecolor",
                "pulsefiresound",
                "pulselag",
                "pulselife",
                "pulsespeed",
                "pulsewidth",
                "punchangle",
                "puntsound",
                "pushdir_islocal",
                "pushdir",
                "pushopenfence",
                "pushscale",
                "puzzle_type",
                "puzzlespawntarget",
                "puzzlespawntargetattachment",
                "puzzletype",
                "pvs_modify_entity",
                "pvstype",
                "quad_axis_u",
                "quad_axis_v",
                "quad_tex_scale",
                "quad_tex_size",
                "quad_vertex_a",
                "quad_vertex_b",
                "quad_vertex_c",
                "querysinglecontrollermode",
                "qurantine_plug_open",
                "rack0_active",
                "rack1_active",
                "rack2_active",
                "rack3_active",
                "rack4_active",
                "radiant_end_camera",
                "radio",
                "radius_scale",
                "radius",
                "radiusscale",
                "ragdoll_dead_closet_charger",
                "rain_fx",
                "rain_inner_amount",
                "rain_inner_radius",
                "rain_outer_radius",
                "rallypoint",
                "rallyselectmethod",
                "rallysequence",
                "rampruleset",
                "random_owner_on_restart",
                "random_rotation",
                "random_soundevent_01_timer_max",
                "randomanimation",
                "randomboard",
                "randomize_start",
                "randomizecycle",
                "range",
                "rank",
                "rank0_active",
                "rank1_active",
                "rank2_active",
                "rank3_active",
                "rapidfire_loaded_ammo",
                "rapidfire_upgrade_explodingclusters",
                "rapidfire_upgrade_extended_magazine",
                "rapidfire_upgrade_lasersight",
                "rapidfire_upgrade_reflexsight",
                "rapidfire",
                "rare_loadout_anim_chance",
                "ratchettype",
                "rate",
                "rateoffire",
                "reacttodynamicphysics",
                "rear_window_initial_state",
                "rear_window_skin",
                "rear_windows_skin",
                "rechargetime",
                "reciprocal",
                "rectlight",
                "red_light",
                "red_respawn_time",
                "red_teleport",
                "red_window_broken",
                "red",
                "redspawn",
                "ref_position",
                "reference_segment_size_maxs",
                "reference_segment_size_mins",
                "referenceid",
                "referencename",
                "refire",
                "refiretime",
                "refuge_whiteboard_stamp",
                "refuge_whiteboard_stamp2",
                "relative_fov",
                "relativedamping",
                "relay_flyby_high1_hidden",
                "relay_flyby1_hidden",
                "relay_flyby2_hidden",
                "relayplug",
                "relayplugdata",
                "releaseonplayerdamage",
                "releasepause",
                "remaplineend",
                "remaplinestart",
                "removable",
                "remove_for_lv",
                "remove_over_amount",
                "removecombine",
                "removeillusionsondeath",
                "removeonexplode",
                "renamenpc",
                "render_attr_name",
                "render_depth_map",
                "render_shadows",
                "renderamt",
                "rendercolor",
                "renderdiffuse",
                "renderfx",
                "rendermode",
                "renderorder",
                "renderspecular",
                "rendertocubemaps",
                "rendertransmissive",
                "renderwithdynamic",
                "reorient_mode",
                "repeat_dialog_sound",
                "repeat_dialog_string",
                "repeat",
                "repelradius",
                "replace01",
                "replace02",
                "replace03",
                "replace04",
                "replace05",
                "replace06",
                "replace07",
                "replace08",
                "replace09",
                "replace10",
                "require_all_tags",
                "required3dskyboxentities",
                "requiredgameentities",
                "requiredtime",
                "requiresusekey",
                "res_file",
                "reset_delay",
                "reset_time",
                "resilient",
                "resin",
                "resizablewindow",
                "resolution_x",
                "resolution_y",
                "resolution",
                "respawn_area",
                "respawn_interval",
                "respawn_reduction_scale",
                "respawn_time",
                "respawnname",
                "respawnroomname",
                "respawntime",
                "responsecontext",
                "restdist",
                "resumecondition",
                "resumepriority",
                "retainbuildings",
                "retainvelocity",
                "retrieve",
                "retrieveconceptmodifier",
                "retrievedistance",
                "retrievewaitforspeak",
                "retryfrequency",
                "returnbackwardmovesound",
                "returnbetweenwaves",
                "returndelay",
                "returnforwardmovesound",
                "returnspeed",
                "returntime",
                "returntocompletion",
                "returntocompletionamount",
                "returntocompletiondelay",
                "returntocompletionstyle",
                "returntocompletionthreshold",
                "reusedelay",
                "reveal_radius",
                "reversalsoundlarge",
                "reversalsoundmedium",
                "reversalsoundsmall",
                "reversalsoundthresholdlarge",
                "reversalsoundthresholdmedium",
                "reversalsoundthresholdsmall",
                "reversefadeduration",
                "revivable",
                "reviver_group",
                "right_down",
                "right_down002",
                "right_root",
                "right_root002",
                "right_up",
                "right_up002",
                "right",
                "rigid_hold",
                "rigidconstraint",
                "rocketspeed",
                "roll",
                "rollerminetemplate",
                "rollspeed",
                "roof_antlion_squad",
                "roof_type",
                "root",
                "rootname",
                "rootpreviewinstance",
                "rootselectionset",
                "ropematerial",
                "rotate_along_path",
                "rotatesound",
                "rotatestartsound",
                "rotatestopsound",
                "rotation_axis0",
                "rotation_axis1",
                "rotation_max",
                "rotation_min",
                "rotation",
                "rotationaxis",
                "rotationsnapping",
                "rotationspeed",
                "rotdamping",
                "rotortime",
                "rotortimevariance",
                "round_bluespawn",
                "round_redspawn",
                "round_window_white",
                "rows",
                "rubbish",
                "rulescript",
                "run",
                "runeminimapiconscale",
                "runeposition",
                "runeteam",
                "safezone",
                "sandbag_standing_root",
                "saveandrestore",
                "saveimportant",
                "scale",
                "scales",
                "scanspeed",
                "scanwhenidle",
                "scene0",
                "scene1",
                "scene10",
                "scene11",
                "scene12",
                "scene13",
                "scene14",
                "scene15",
                "scene2",
                "scene3",
                "scene4",
                "scene5",
                "scene6",
                "scene7",
                "scene8",
                "scene9",
                "scenefile",
                "scenetrigger",
                "schedule",
                "score_interval",
                "score_style",
                "scoretype",
                "scoringtype",
                "screen_0_attscale",
                "screen_0_body_group",
                "screen_0_hide",
                "screen_0_scale",
                "screen_0_skin",
                "screen_1_attscale",
                "screen_1_body_group",
                "screen_1_hide",
                "screen_1_scale",
                "screen_1_skin",
                "screen_bot_skin",
                "screen_bot_state",
                "screen_fade",
                "screen_top_skin",
                "screen_top_state",
                "screenblurstrength",
                "screenspacefade",
                "script",
                "scripted_sequence_name",
                "scriptedmovement",
                "scriptfile",
                "scriptstatus",
                "searchname",
                "searchtype",
                "seat_ajar_angle",
                "seat_choices",
                "seat_hidden",
                "secondary_material",
                "secondary_sky_transfer_count",
                "secondary_transfer_count",
                "secondary_transfer_num_directional_samples",
                "secondary",
                "secondhand",
                "sectionpause",
                "security_multi",
                "security_solo",
                "seeentity",
                "seeentitytimeout",
                "select_walk",
                "selected_line",
                "selectedobjects",
                "selectionsetdata",
                "selectionsetname",
                "selfillum",
                "selfillumscale",
                "semanticindex",
                "semanticname",
                "send_pass_outputs",
                "sensitivity",
                "sentence",
                "sentry_position_name",
                "sequence_name",
                "sequence_number",
                "sequence",
                "sequencename",
                "sequencename2",
                "set_ammo_rapidfire",
                "set_ammo_shotgun",
                "set_ammo",
                "set_bodygroup",
                "set_resin",
                "set_spawn_ammo",
                "setadditionalairdensity",
                "setangvelocitydamping",
                "setangvelocitylimit",
                "setangvelocityscale",
                "setbodygroup",
                "setbuttonpusherskin",
                "setbuttonpushertintcolor",
                "setdampingratio",
                "seteffectlightbrightness",
                "seteffectlighttintcolor",
                "setfluiddensity",
                "setfrequency",
                "setgravityamplitude",
                "setgravityfrequency",
                "setgravityscale",
                "setlinearforce",
                "setlinearforceangles",
                "setlockdefaultdamage",
                "setlockimmunetodamage",
                "setnavignore",
                "setoffset",
                "setonspawn",
                "settingsagentheight_0",
                "settingsagentheight_1",
                "settingsagentheight_2",
                "settingsagentmaxclimb_0",
                "settingsagentmaxclimb_1",
                "settingsagentmaxclimb_2",
                "settingsagentmaxjumpdowndist_0",
                "settingsagentmaxjumpdowndist_1",
                "settingsagentmaxjumpdowndist_2",
                "settingsagentmaxjumphorizdistbase_0",
                "settingsagentmaxjumphorizdistbase_1",
                "settingsagentmaxjumphorizdistbase_2",
                "settingsagentmaxjumpupdist_0",
                "settingsagentmaxjumpupdist_1",
                "settingsagentmaxjumpupdist_2",
                "settingsagentmaxslope_0",
                "settingsagentmaxslope_1",
                "settingsagentmaxslope_2",
                "settingsagentnumhulls",
                "settingsagentradius_0",
                "settingsagentradius_1",
                "settingsagentradius_2",
                "settingscellheight",
                "settingscellsize",
                "settingsdetailsampledist",
                "settingsdetailsamplemaxerror",
                "settingsedgemaxerror",
                "settingsedgemaxlen",
                "settingsregionmergesize",
                "settingsregionminsize",
                "settingssmallareaonedgeremovalsize",
                "settingstilesize",
                "settingsuseprojectdefaults",
                "settingsvertsperpoly",
                "settocombine",
                "settohuman",
                "settovalueondisable",
                "settype",
                "setup_length",
                "setvelocitydamping",
                "setvelocitylimit",
                "setvelocitylimitdelta",
                "setvelocityscale",
                "severe_damage",
                "shadow_color_dawn",
                "shadow_color_day",
                "shadow_color_dusk",
                "shadow_color_night",
                "shadow_ground_scale_dawn",
                "shadow_ground_scale_day",
                "shadow_ground_scale_dusk",
                "shadow_ground_scale_night",
                "shadow_scale_dawn",
                "shadow_scale_day",
                "shadow_scale_dusk",
                "shadow_scale_night",
                "shadow_secondary_color_day",
                "shadow_secondary_color_night",
                "shadowatlasheight",
                "shadowatlaswidth",
                "shadowcascadedistance0",
                "shadowcascadedistance1",
                "shadowcascadedistance2",
                "shadowcascadedistance3",
                "shadowcascaderesolution0",
                "shadowcascaderesolution1",
                "shadowcascaderesolution2",
                "shadowcascaderesolution3",
                "shadowcastdist",
                "shadowdepthnocache",
                "shadowfade_size_end",
                "shadowfade_size_start",
                "shadowfademaxdist",
                "shadowfademindist",
                "shadowmapsize",
                "shadowpriority",
                "shadowquality",
                "shadowtextureheight",
                "shadowtexturewidth",
                "shakecontinuous",
                "shakeeverywhere",
                "shakelength",
                "shakeonce",
                "shakerotation",
                "shakesize",
                "shakespeed",
                "shaketrigger",
                "shape_type",
                "shape",
                "shear",
                "shed_padlock_name",
                "shielddistance",
                "shieldradius",
                "shoes_color",
                "shoes_material",
                "shoes_model",
                "shoot_sound",
                "shootmodel",
                "shootsound",
                "shootsounds",
                "shootzombiesinchest",
                "shoptype",
                "short_bend",
                "shotclockmode",
                "shotgun_has_grenade",
                "shotgun_loaded_ammo",
                "shotgun_upgrade_autoloader",
                "shotgun_upgrade_grenade",
                "shotgun_upgrade_lasersight",
                "shotgun_upgrade_quickfire",
                "shotgun_upgrade_rollermine",
                "shotgun",
                "shouldblock",
                "shouldcomparetovalue",
                "shouldercableend_0",
                "shouldercableend_end",
                "shouldhaveemp",
                "shouldinspect",
                "shouldsetenemy",
                "shovetargets",
                "show_battery_level",
                "show_during_dynamic_weather",
                "show_extent_helper",
                "show_in_fog",
                "show_in_hud",
                "show_time_remaining",
                "show3dgrid",
                "showatday",
                "showatnight",
                "showgrid",
                "showintro",
                "showlight",
                "showpreview",
                "shutthedoor",
                "side",
                "sideneck_l_clothnode",
                "sideneck_r_clothnode",
                "sides",
                "sides2",
                "sightdist",
                "sightmethod",
                "signbang",
                "silenttozombies",
                "simple_low",
                "simpleprojection",
                "simulation",
                "simulationmode",
                "single_bullet_model",
                "size_params",
                "size_type_segments_transition",
                "size",
                "sizemax",
                "sizemin",
                "skewaccelerationforward",
                "skin_base",
                "skin_body2",
                "skin_cp",
                "skin_face",
                "skin_face2",
                "skin_type",
                "skin",
                "skinchoice",
                "skinnumber",
                "skintype",
                "skip_hq_shadow_trace",
                "skip_pet_spawn",
                "skippresettle",
                "skirt_near",
                "skirt",
                "skyambientbounce",
                "skybouncescale",
                "skybox_angle_day",
                "skybox_angle_night",
                "skybox_angular_fog_max_end",
                "skybox_angular_fog_max_start",
                "skybox_angular_fog_min_end",
                "skybox_angular_fog_min_start",
                "skybox_fog_type",
                "skybox_material_day",
                "skybox_material_night",
                "skybox_size",
                "skybox_tint_color_day",
                "skybox_tint_color_night",
                "skybox_tint_day",
                "skybox_tint_night",
                "skyboxcannistercount",
                "skyboxname",
                "skyboxslot",
                "skycolor",
                "skydirectlight",
                "skyintensity",
                "skyname",
                "skytexture",
                "skytexturescale",
                "slack",
                "slanted_flipped",
                "slanted",
                "slavename",
                "sleepstate",
                "slide_back_sound",
                "slide_close_sound",
                "slide_interact_max_dist",
                "slide_interact_min_dist",
                "slide_lock_sound",
                "slide_model_left_handed",
                "slide_model_right_handed",
                "slideaxis",
                "slidefriction",
                "slidesoundback",
                "slidesoundfwd",
                "slingshot",
                "slope_section",
                "small_fx_scale",
                "small_visible",
                "small",
                "smokegrenades",
                "smokelifetime",
                "smokematerial",
                "smokesprite",
                "smoketrail",
                "smoothfactor",
                "smoothingangle",
                "snap",
                "snaprotationangle",
                "snapshot_file",
                "snapshot_mesh",
                "snaptoent",
                "snapvalue",
                "soft_x",
                "soft_y",
                "soldier_01_died",
                "solid_to_enemies",
                "solid",
                "solidbsp",
                "solidity",
                "sortkey",
                "sosvar",
                "sound_base",
                "sound_effect",
                "sound_file_name",
                "sound",
                "soundareatype",
                "soundcloseoverride",
                "soundcontext",
                "sounddelayorskip",
                "sounddisengage",
                "soundengage",
                "soundevent_name",
                "soundevent",
                "soundeventcountmax",
                "soundeventname",
                "soundeventnameascent",
                "soundeventnamegallery",
                "soundjiggleoverride",
                "soundlatchoverride",
                "soundlevel",
                "soundlockedoverride",
                "soundmoveoverride",
                "soundmovingloop",
                "soundname",
                "soundopenoverride",
                "soundoverride",
                "soundreachedvalueone",
                "soundreachedvaluezero",
                "sounds",
                "soundscape",
                "soundstartuse",
                "soundtype",
                "soundunlockedoverride",
                "soundvolume",
                "source_folder",
                "source",
                "source1_brushmodel_index",
                "sourceentityattachment",
                "sourceentityname",
                "sourceproxy",
                "sparktype",
                "spawn_along_conveyor_on_startup",
                "spawn_along_conveyor_probability",
                "spawn_angles_target_override",
                "spawn_antlions",
                "spawn_background_models",
                "spawn_breakpieces",
                "spawn_combine_and_begin_sequence",
                "spawn_combine",
                "spawn_conveyor_path_node_override",
                "spawn_inside_manager_radius",
                "spawn_inside_player_radius",
                "spawn_manager_name",
                "spawn_motion_disabled",
                "spawn_on_start",
                "spawn_order",
                "spawn_outside_manager_radius",
                "spawn_outside_player_radius",
                "spawn_particles",
                "spawn_script_func",
                "spawn_sound",
                "spawn_wearable_item_defs",
                "spawn_zombie_npc",
                "spawn",
                "spawnasragdoll",
                "spawnautomatically",
                "spawnflags",
                "spawnfrequency",
                "spawngroup",
                "spawngrouptype",
                "spawninvulnerability",
                "spawnmode",
                "spawnneighbor",
                "spawnobject",
                "spawnonlywhentriggered",
                "spawnoptions",
                "spawnpos",
                "spawnprefab",
                "spawnradius",
                "spawnrate",
                "spawnsettings",
                "spawntarget",
                "spawntemplate",
                "spawnventheadcrab",
                "speaker_dsp_preset",
                "speakername",
                "speakers",
                "special_cap",
                "special_connection_pieces",
                "specificimpulse",
                "specificresupply",
                "spectateondeath",
                "specular_color_dawn",
                "specular_color_day",
                "specular_color_dusk",
                "specular_color_night",
                "specular_direction_day",
                "specular_direction_night",
                "specular_independence_day",
                "specular_independence_night",
                "specular_power_day",
                "specular_power_night",
                "specularangles",
                "specularcolor",
                "specularindependence",
                "specularpower",
                "speed_forward_modifier",
                "speed",
                "speedfactor",
                "speedin",
                "speedmax",
                "speedmin",
                "speednoise",
                "speedout",
                "sphere_center",
                "sphere",
                "sphericalvignette",
                "spike_height",
                "spindown",
                "spine_0",
                "spine_2",
                "spine",
                "spine1",
                "spine2",
                "spine3",
                "spine4",
                "spinmagnitude",
                "spinspeed",
                "spinup",
                "splashradius",
                "spot_light_distance",
                "spot_light_size",
                "spotlight_radius",
                "spotlightdisabled",
                "spotlightlength",
                "spotlightwidth",
                "spraydir",
                "spread",
                "spreadangle",
                "spreadspeed",
                "springaxis",
                "springstretchiness",
                "sprint_proclivity",
                "spriteflash",
                "spritename",
                "spritescale",
                "spritesmoke",
                "sprocket",
                "squad_long_hall_door_zombie",
                "squad_name",
                "squadname",
                "src_movie",
                "src",
                "stabilizeanim",
                "stackname",
                "stacktosave",
                "stage_health",
                "staging_ent_names",
                "stair",
                "stampname",
                "standard_colied",
                "standard_no_axle",
                "standard_no_supports",
                "standard",
                "standardattributename",
                "start_active",
                "start_angle",
                "start_asleep",
                "start_disabled",
                "start_enabled",
                "start_entity",
                "start_falloff",
                "start_hacked",
                "start_move_sound",
                "start_new_conveyor_section",
                "start_node",
                "start_on_bool",
                "start_on_int",
                "start_on_inverse_bool",
                "start_on_inverse_int",
                "start_on",
                "start_paused",
                "start_weapons_empty",
                "start_welded",
                "start_with_vial",
                "start",
                "startactivated",
                "startactive",
                "startapartmentgapcombat",
                "startattached",
                "startattachment",
                "startbroken",
                "startburied",
                "startburrowed",
                "startclosesound",
                "startcolor",
                "startdark",
                "startdebrisspawner",
                "startdirection",
                "startdisabled",
                "startdisarmed",
                "startdust",
                "startenabled",
                "startflying",
                "startframe",
                "starthacked",
                "starthintdisabled",
                "starting_dialog",
                "starting_tanks",
                "starting_target_budget",
                "startingheight",
                "startlocked",
                "startloop",
                "startmovesound",
                "startnode",
                "starton",
                "startonspawn",
                "startopen",
                "startpath",
                "startportvisible",
                "startposition",
                "startpulse",
                "startpulseloop",
                "startradio",
                "startradius",
                "starts_moving",
                "startsascurrent",
                "startscene",
                "startsequence",
                "startsize",
                "startsmoving",
                "startsound",
                "startspeed",
                "starttime",
                "startu",
                "startup_behavior",
                "startv",
                "startvalue",
                "startvisible",
                "startwidth",
                "startwithgrenade",
                "state",
                "static_collision",
                "static_pause_time_max",
                "static_pause_time_min",
                "static",
                "stay_time",
                "stayatcover",
                "steamappid",
                "steamaudio_export",
                "steamaudioenabled",
                "step_number",
                "sticky",
                "stop_immediately",
                "stop_instantly",
                "stop_move_sound",
                "stop_on_seq_change",
                "stop",
                "stopdebrisspawner",
                "stopdust",
                "stopmovesound",
                "stoponnew",
                "stoppulseloop",
                "stopradio",
                "stops",
                "stopsnd",
                "stopsound",
                "storage",
                "streams",
                "streamsourcetype",
                "stretch",
                "stretchforce",
                "strict",
                "strider_awakens_in_parking_garage",
                "strider_chases_the_elevator",
                "strider_feet_not_dangerous",
                "strider_is_not_targeting_the_player",
                "strider_is_targeting_the_player",
                "strider_send_to_the_arena",
                "striderdies",
                "striketime",
                "string",
                "stringdata",
                "structspringconstant",
                "structspringdamping",
                "stun_duration",
                "stun_effects",
                "stun_type",
                "style_index0",
                "style_index1",
                "style_index2",
                "style_index3",
                "style_index4",
                "style_index5",
                "style_index6",
                "style_index7",
                "style",
                "subclass_name",
                "subdiv",
                "subdivisionbinding",
                "subdivisiondata",
                "subdivisionlevels",
                "subject",
                "subtleeffects",
                "successconceptmodifier",
                "successdistance",
                "suck_position",
                "suddendeathtime",
                "suitcase_002b_body_hinge",
                "suitcase_002b_handle_hinge",
                "suitcase_002b_lid_hinge",
                "suitcase_005b_body_hinge",
                "suitcase_005b_lid_hinge",
                "suitcase_006b_body_hinge",
                "suitcase_006b_handle_hinge",
                "suitcase_006b_lid_hinge",
                "sunlightminbrightness",
                "sunspreadangle",
                "supportsffd",
                "suppress_anim_event_sounds",
                "suppress_intro_effects",
                "suppress",
                "suppressfire",
                "suppresstime",
                "surface_connector",
                "surface_properties",
                "surface_type",
                "surfaceprop",
                "surfacestretch",
                "surfacetype",
                "swaparenatubcollision",
                "swapbathroomcollision",
                "swapmodel",
                "swing_limit",
                "switch_teams",
                "switchidletoalert",
                "switchoff",
                "switchon",
                "sync_group",
                "synctofollowinggesture",
                "synodic_month",
                "systemloadscale",
                "t_junction",
                "tacticalvariant",
                "tag",
                "tagfieldnames",
                "tags",
                "tail",
                "tail1",
                "tail2",
                "tail3",
                "tail4",
                "tail5",
                "tail6",
                "tailgate_ajar_0-1",
                "tailgate_angle",
                "tailgate_initial_state",
                "tailgate_position",
                "tailgate_skin",
                "tailgate_tint_color",
                "tan",
                "tangent_in",
                "tangent_out",
                "tank0_start_missing",
                "tank1_missing",
                "tank1_start_missing",
                "tank2_missing",
                "tank2_start_missing",
                "tank3_missing",
                "tank3_start_missing",
                "tank4_missing",
                "tankjgl",
                "target_base_name",
                "target_destination",
                "target_entity",
                "target_path_corner",
                "target_point",
                "target_sound_entity_name",
                "target_source",
                "target_team",
                "target",
                "target1",
                "target2",
                "target3",
                "target4",
                "target5",
                "target6",
                "target7",
                "target8",
                "targetattachment",
                "targetbodyname",
                "targetcompletionthreshold",
                "targetcompletionvaluea",
                "targetcompletionvalueb",
                "targetcompletionvaluec",
                "targetcompletionvalued",
                "targetcompletionvaluee",
                "targetcompletionvaluef",
                "targetdatatype",
                "targetentityname",
                "targetgroupreferenceid",
                "targeting_entity_name",
                "targetmapname",
                "targetmappath",
                "targetmodel",
                "targetname",
                "targetnode",
                "targetoffset",
                "targetpoint",
                "targetpos",
                "targetreference",
                "targets",
                "targetscale",
                "targetstreamindex",
                "targettype",
                "targettypeflags",
                "tarproot",
                "task",
                "tauntinhell",
                "team_base_icon_2",
                "team_base_icon_3",
                "team_bodygroup_0",
                "team_bodygroup_2",
                "team_bodygroup_3",
                "team_cancap_2",
                "team_cancap_3",
                "team_capsound_0",
                "team_capsound_2",
                "team_capsound_3",
                "team_icon_0",
                "team_icon_2",
                "team_icon_3",
                "team_model_0",
                "team_model_2",
                "team_model_3",
                "team_number",
                "team_numcap_2",
                "team_numcap_3",
                "team_overlay_0",
                "team_overlay_2",
                "team_overlay_3",
                "team_previouspoint_2_0",
                "team_previouspoint_2_1",
                "team_previouspoint_2_2",
                "team_previouspoint_3_0",
                "team_previouspoint_3_1",
                "team_previouspoint_3_2",
                "team_spawn_2",
                "team_spawn_3",
                "team_startcap_2",
                "team_startcap_3",
                "team_timedpoints_2",
                "team_timedpoints_3",
                "team",
                "teamcount",
                "teamnum",
                "teamnumber",
                "teamtoblock",
                "teleport_origin",
                "teleport_parented_entities",
                "teleport_relative",
                "teleportertype",
                "teleportfollowdistance",
                "teleportgrenades",
                "teleportoffset",
                "template",
                "template01",
                "template02",
                "template03",
                "template04",
                "template05",
                "template06",
                "template07",
                "template08",
                "template09",
                "template10",
                "template11",
                "template12",
                "template13",
                "template14",
                "template15",
                "template16",
                "template17",
                "template18",
                "template19",
                "template20",
                "template21",
                "template22",
                "template23",
                "template24",
                "template25",
                "template26",
                "template27",
                "template28",
                "template29",
                "template30",
                "template31",
                "template32",
                "template33",
                "template34",
                "template35",
                "template36",
                "template37",
                "template38",
                "template39",
                "template40",
                "template41",
                "template42",
                "template43",
                "template44",
                "template45",
                "template46",
                "template47",
                "template48",
                "template49",
                "template50",
                "template51",
                "template52",
                "template53",
                "template54",
                "template55",
                "template56",
                "template57",
                "template58",
                "template59",
                "template60",
                "template61",
                "template62",
                "template63",
                "template64",
                "templatefixup",
                "templatename",
                "tentaclea_0",
                "tentaclea_1",
                "tentaclea_10",
                "tentaclea_11",
                "tentaclea_12",
                "tentaclea_13",
                "tentaclea_14",
                "tentaclea_15",
                "tentaclea_16",
                "tentaclea_17",
                "tentaclea_18",
                "tentaclea_19",
                "tentaclea_2",
                "tentaclea_20",
                "tentaclea_21",
                "tentaclea_22",
                "tentaclea_23",
                "tentaclea_24",
                "tentaclea_25",
                "tentaclea_26",
                "tentaclea_27",
                "tentaclea_28",
                "tentaclea_29",
                "tentaclea_3",
                "tentaclea_30",
                "tentaclea_31",
                "tentaclea_32",
                "tentaclea_33",
                "tentaclea_34",
                "tentaclea_35",
                "tentaclea_36",
                "tentaclea_37",
                "tentaclea_38",
                "tentaclea_4",
                "tentaclea_5",
                "tentaclea_6",
                "tentaclea_7",
                "tentaclea_8",
                "tentaclea_9",
                "tentaclea_end",
                "tentacleb_a_0",
                "tentacleb_a_1",
                "tentacleb_a_10",
                "tentacleb_a_11",
                "tentacleb_a_12",
                "tentacleb_a_13",
                "tentacleb_a_14",
                "tentacleb_a_15",
                "tentacleb_a_16",
                "tentacleb_a_17",
                "tentacleb_a_18",
                "tentacleb_a_19",
                "tentacleb_a_2",
                "tentacleb_a_20",
                "tentacleb_a_21",
                "tentacleb_a_22",
                "tentacleb_a_23",
                "tentacleb_a_24",
                "tentacleb_a_25",
                "tentacleb_a_26",
                "tentacleb_a_3",
                "tentacleb_a_4",
                "tentacleb_a_5",
                "tentacleb_a_6",
                "tentacleb_a_7",
                "tentacleb_a_8",
                "tentacleb_a_9",
                "tentacleb_b_0",
                "tentacleb_b_1",
                "tentacleb_b_10",
                "tentacleb_b_11",
                "tentacleb_b_12",
                "tentacleb_b_13",
                "tentacleb_b_14",
                "tentacleb_b_15",
                "tentacleb_b_16",
                "tentacleb_b_17",
                "tentacleb_b_18",
                "tentacleb_b_2",
                "tentacleb_b_3",
                "tentacleb_b_4",
                "tentacleb_b_5",
                "tentacleb_b_6",
                "tentacleb_b_7",
                "tentacleb_b_8",
                "tentacleb_b_9",
                "tentacleb_c_0",
                "tentacleb_c_1",
                "tentacleb_c_10",
                "tentacleb_c_11",
                "tentacleb_c_2",
                "tentacleb_c_3",
                "tentacleb_c_4",
                "tentacleb_c_5",
                "tentacleb_c_6",
                "tentacleb_c_7",
                "tentacleb_c_8",
                "tentacleb_c_9",
                "tentacleb_d_0",
                "tentacleb_d_1",
                "tentacleb_d_2",
                "tentacleb_e_0",
                "tentacleb_e_1",
                "tentacleb_e_2",
                "tentacleb_e_3",
                "tentacleb_e_4",
                "tentacleb_e_5",
                "tentacleb_e_6",
                "tentacleb_e_7",
                "tentacleb_f_0",
                "tentacleb_f_1",
                "tentacleb_f_2",
                "tentacleb_f_3",
                "tentaclec_a_0",
                "tentaclec_a_1",
                "tentaclec_a_10",
                "tentaclec_a_11",
                "tentaclec_a_12",
                "tentaclec_a_13",
                "tentaclec_a_14",
                "tentaclec_a_15",
                "tentaclec_a_16",
                "tentaclec_a_17",
                "tentaclec_a_18",
                "tentaclec_a_19",
                "tentaclec_a_2",
                "tentaclec_a_20",
                "tentaclec_a_3",
                "tentaclec_a_4",
                "tentaclec_a_5",
                "tentaclec_a_6",
                "tentaclec_a_7",
                "tentaclec_a_8",
                "tentaclec_a_9",
                "tentaclec_b_0",
                "tentaclec_b_1",
                "tentaclec_b_2",
                "tentaclec_c_0",
                "tentaclec_c_1",
                "tentaclec_c_2",
                "tentaclec_c_3",
                "tentaclec_d_0",
                "tentaclec_d_1",
                "tentaclec_d_2",
                "tentaclec_d_3",
                "tentaclec_d_4",
                "tentaclec_e_0",
                "tentaclec_e_1",
                "terrainlighting",
                "terraintools",
                "tessellationspacing",
                "test_angles_relative",
                "test_occlusion",
                "test_type",
                "testmode",
                "text_local",
                "text",
                "textcolor",
                "textpanelwidth",
                "textsize",
                "texture_based_animation_position_keys",
                "texture_based_animation_preview_sequence",
                "texture_based_animation_rotation_keys",
                "texture_resolution",
                "texture",
                "texturename",
                "textureoffsetalongpath",
                "textureoffsetcircumference",
                "textureorientation",
                "texturerepeatscircumference",
                "texturerepeatspersegment",
                "texturescale",
                "texturescroll",
                "tfclass",
                "thick_max",
                "thick_min",
                "thickness",
                "thigh_l",
                "thigh_r",
                "think_interval",
                "thinkalways",
                "thinkfunction",
                "threadstretch",
                "threshold",
                "thrust",
                "thumbnail_frame",
                "tier",
                "tileconfiguration",
                "tilegridblenddefaultcolor",
                "tilegridblendorderbgra",
                "tilegridsupportsblendheight",
                "tileheightscale",
                "tilemeshdata",
                "tilemeshesenabled",
                "tilesetassignments",
                "tilesetmapnames",
                "tiltfraction",
                "tilttime",
                "time_delay",
                "time_scale",
                "time",
                "timeofday",
                "timeout_particles",
                "timeout_script_func",
                "timeout_sound",
                "timeout",
                "timeoutinterval",
                "timer_length",
                "timeslicedshadowmaprendering",
                "timestofire",
                "timetohold",
                "timetotrigget",
                "timezone",
                "tint_color",
                "tint_cp_color",
                "tint_cp",
                "tint",
                "tintable_body",
                "tintable_damaged",
                "tintable_selfillum",
                "tintable_skin_color",
                "tintable_window_broken",
                "tintable",
                "tintcolor",
                "tire_anim_default",
                "tire_body_group",
                "tire_body_state",
                "tire_logic_anim",
                "title",
                "toggle",
                "toilet_lid_hinge",
                "toilet_seat_hinge",
                "token",
                "tolerance",
                "tolocalplayer",
                "tonemapname",
                "toolsappid",
                "top_point",
                "top",
                "topbarteamvaluesvisible",
                "toptrack",
                "topvignettestrength",
                "torquelimit_x",
                "torquelimit_y",
                "torquelimit_z",
                "torquelimit",
                "total_spending_limit",
                "totalmass",
                "touch_trigger",
                "touchoutputperentitydelay",
                "touchtype",
                "tp_suppress_remind_interval",
                "tp_suppress_sound",
                "tracedown",
                "tracer_effect",
                "tracertype",
                "traceup",
                "track_beam_scale",
                "trackspeed",
                "trail_effect",
                "traillength",
                "train_can_recede",
                "train_door_b_type",
                "train_door_b",
                "train_door_e_type",
                "train_door_e",
                "train_recede_time",
                "train_roof_b_type",
                "train_roof_c_type",
                "train_roof_type_b",
                "train_roof_type_c",
                "train_roof",
                "train",
                "transformlocked",
                "transition_time_in",
                "transition_time_out",
                "translucencylimit",
                "trap_target",
                "tree_portrait",
                "tree_root",
                "tree_shake_strength_override",
                "treeregrowtime",
                "trigger_buoyancy",
                "trigger_delay",
                "trigger_helicopter_flyby",
                "trigger_name",
                "trigger_radius",
                "trigger_sound",
                "trigger",
                "triggerhitpoints",
                "triggeronce",
                "triggeronstarttouch",
                "trim_type",
                "trims",
                "truck_type",
                "turn_rate",
                "turnoffeffectlight",
                "turnofflight",
                "turnoffsparks",
                "turnoneffectlight",
                "turnonsparks",
                "turret_position",
                "turretname",
                "turtle",
                "twist",
                "type",
                "typename",
                "unburroweffects",
                "undercarriage_detail_high",
                "undercarriage_detail_low",
                "undercarriage_detail",
                "undercarriage_high_detail",
                "undercarriage_low_detail",
                "uniformscale",
                "uniformsightdist",
                "unique_target",
                "unitname",
                "unitsfiles",
                "unknown",
                "unlatch",
                "unlock_point",
                "unlock",
                "unlockdoor",
                "unlocked_sentence",
                "unlocked_sound",
                "unmuteradio",
                "unusesentence",
                "up_arrow_off",
                "up_arrow_on",
                "up",
                "upclosemode",
                "updatechildmodels",
                "updatechildren",
                "updateonclient",
                "upgrade_level",
                "upper_color",
                "upper_door_ajar_0-1",
                "upper_door_color",
                "upper_material",
                "upper_model",
                "upperrandombound",
                "upperthreshold",
                "urgent",
                "use_angles",
                "use_animgraph",
                "use_collision_bounds",
                "use_landmark_angles",
                "use_layer_sequence",
                "use_ref_position",
                "use_secondary_color",
                "use_sound",
                "use_vrstealth_outside_combat",
                "useable",
                "useairlinkradius",
                "usealtnpcavoid",
                "useasoccluder",
                "usebasegoldbountyonheroes",
                "usecustomheroxpvalues",
                "useentitypivot",
                "useexactvelocity",
                "usefakeacceleration",
                "usehelper",
                "uselandmarkangles",
                "uselightenvangles",
                "uselocaloffset",
                "uselocaltime",
                "usemarch",
                "useneutralcreepbehavior",
                "usenormalspawnsfordm",
                "useproximitybone",
                "userandomtime",
                "usesbakedlighting",
                "usesbakedlightingpervertex",
                "usescreenaspectratio",
                "usesentence",
                "useteamspawnpoint",
                "usethresholdcheck",
                "useuncompressedvertices",
                "useuniversalshopmode",
                "useunseenfogofwar",
                "usewind",
                "utility",
                "uv0",
                "uv1",
                "uv2",
                "uv3",
                "value",
                "values",
                "valve",
                "var1",
                "var2",
                "var3",
                "variablea",
                "variableb",
                "variableeditoroverrides",
                "variablenames",
                "variableoverridenames",
                "variableoverridevalues",
                "variablesubtypes",
                "variabletargetkeys",
                "variabletypenames",
                "variabletypeparameters",
                "variabletypes",
                "variablevalues",
                "variationid",
                "varmint",
                "vaultbeat",
                "vecline_local",
                "vecpos",
                "vector",
                "vehicle_locked",
                "vehicle",
                "vehicledistance",
                "vehiclelocked",
                "vehiclescript",
                "velocity_cp",
                "velocity",
                "velocitytype",
                "veltolerance",
                "vent",
                "verbose",
                "vertexbufferlocation",
                "vertexdata",
                "vertexdataindices",
                "vertexedgeindices",
                "vertexformat",
                "vertexlightingdata",
                "vertexlightingpositions",
                "vertexnormalpaintenabled",
                "vertexpaintblendparams",
                "vertexpaintblendparamsindices",
                "vertexset",
                "vertical_align",
                "verticalfov",
                "verticalglowsize",
                "vertically_flipped_segment",
                "vial_level",
                "viewkick",
                "viewmode",
                "viewposition",
                "viewtarget",
                "vignetteblurstrength",
                "vignettecolor",
                "vignetteend",
                "vignettefalloffexponent",
                "vignettemaxopacity",
                "vignettestart",
                "vignettestrength",
                "visbility",
                "visexclude",
                "visibilitysamples",
                "visible_range_check",
                "visibleonly",
                "visibletime",
                "visiblewhendisabled",
                "visionrange",
                "visoccluder",
                "void_tile",
                "volstart",
                "volume_atten",
                "volume",
                "volumefogenabled",
                "volumematchesramp",
                "volumename",
                "volumetric_fog_controller",
                "volumetricfog",
                "vortigaunt",
                "voxel_size",
                "voxelize",
                "voxelsize",
                "vrad_brush_cast_shadows",
                "vrchaperone",
                "vrmovement",
                "vscripts",
                "vulnerableoncreepspawn",
                "wainscot_walls",
                "wait",
                "waitdistance",
                "waitforrevival",
                "waitingtorappel",
                "waitoverconceptmodifier",
                "waitpointname",
                "wakeradius",
                "wakesquad",
                "walk_a_hide",
                "walk_b_hide",
                "walk_c_hide",
                "walkie_talkie_source",
                "wall_cap",
                "wall_flipped",
                "wall",
                "warningtime",
                "water_flow_map_texture",
                "water_meter",
                "watermaterial",
                "waveheight",
                "weapon_ar2",
                "weapon_mine",
                "weapon_smg1",
                "weapon_theirs",
                "weaponclassname",
                "weapondrawn",
                "weaponname",
                "weapons_to_give",
                "weaponslot",
                "weapontype",
                "weather_effect",
                "weather_type",
                "web_a_0",
                "web_a_1",
                "web_a_2",
                "web_a_3",
                "web_a_4",
                "web_b_1",
                "web_b_2",
                "web_b_3",
                "web_b_4",
                "web_c_1",
                "web_c_2",
                "web_c_3",
                "web_c_4",
                "web_mida_0",
                "web_midb_0",
                "website_anchor",
                "website",
                "weight",
                "weightlist",
                "weighttoactivate",
                "weld_target",
                "wheel_back",
                "wheel_front",
                "wheelbaselength",
                "wheels",
                "white",
                "width",
                "width1",
                "width2",
                "widthtexcoordend",
                "widthtexcoordstart",
                "wind_amount_day",
                "wind_amount_night",
                "wind_angle",
                "wind_map_max",
                "wind_map_min",
                "wind_max",
                "wind_min",
                "windangle",
                "window_broken",
                "window_shatter",
                "window_tint_color",
                "window",
                "windowbroken",
                "windows_1_initial_state",
                "windows_2_initial_state",
                "windradius",
                "windscreen_initial_state",
                "windscreen_skin",
                "windspeed",
                "wing_0_l",
                "wing_0_r",
                "wirebox_local",
                "wiring",
                "woman_portrait",
                "wood_clean",
                "wood",
                "wooden",
                "workerspawnrate",
                "world_units_per_pixel",
                "world",
                "worldfriction",
                "worldgroupid",
                "worldname",
                "wrapped_pipe",
                "x",
                "xfade",
                "xfriction",
                "xmax",
                "xmin",
                "y",
                "yaw_end",
                "yaw_only",
                "yaw_speed",
                "yaw_start",
                "yaw",
                "yawrange",
                "yawrate",
                "yawtolerance",
                "yellow",
                "yes",
                "yfriction",
                "ymax",
                "ymin",
                "your_team_score_sound",
                "zappertype",
                "zfar",
                "zfriction",
                "zmax",
                "zmin",
                "znear",
                "zombie_already_spawned",
                "zombie_lifetime",
                "zombie_slapping_cone_path_1",
                "zombie_style",
                "zombie_type",
                "zone_id",
                "zoomfogscale"}) Get(field);
            }
        }

        #endregion
    }

    #endregion

    #region D_Material

    public class D_Material : XKV3_NTRO, IParamMaterial
    {
        public string Name { get; set; }
        public string ShaderName { get; set; }

        public Dictionary<string, long> IntParams { get; } = [];
        public Dictionary<string, float> FloatParams { get; } = [];
        public Dictionary<string, Vector4> VectorParams { get; } = [];
        public Dictionary<string, string> TextureParams { get; } = [];
        public Dictionary<string, long> IntAttributes { get; } = [];
        public Dictionary<string, float> FloatAttributes { get; } = [];
        public Dictionary<string, Vector4> VectorAttributes { get; } = [];
        public Dictionary<string, string> StringAttributes { get; } = [];

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            base.Read(parent, r);
            Name = Data.Get<string>("m_materialName");
            ShaderName = Data.Get<string>("m_shaderName");

            // TODO: Is this a string array?
            //RenderAttributesUsed = Data.Get<string>("m_renderAttributesUsed");

            foreach (var kv in Data.GetArray("m_intParams")) IntParams[kv.Get<string>("m_name")] = kv.GetInt64("m_nValue");
            foreach (var kv in Data.GetArray("m_floatParams")) FloatParams[kv.Get<string>("m_name")] = kv.GetFloat("m_flValue");
            foreach (var kv in Data.GetArray("m_vectorParams")) VectorParams[kv.Get<string>("m_name")] = kv.Get<Vector4>("m_value");
            foreach (var kv in Data.GetArray("m_textureParams")) TextureParams[kv.Get<string>("m_name")] = kv.Get<string>("m_pValue");

            // TODO: These 3 parameters
            //var textureAttributes = Data.GetArray("m_textureAttributes");
            //var dynamicParams = Data.GetArray("m_dynamicParams");
            //var dynamicTextureParams = Data.GetArray("m_dynamicTextureParams");

            foreach (var kv in Data.GetArray("m_intAttributes")) IntAttributes[kv.Get<string>("m_name")] = kv.GetInt64("m_nValue");
            foreach (var kv in Data.GetArray("m_floatAttributes")) FloatAttributes[kv.Get<string>("m_name")] = kv.GetFloat("m_flValue");
            foreach (var kv in Data.GetArray("m_vectorAttributes")) VectorAttributes[kv.Get<string>("m_name")] = kv.Get<Vector4>("m_value");
            foreach (var kv in Data.GetArray("m_stringAttributes")) StringAttributes[kv.Get<string>("m_name")] = kv.Get<string>("m_pValue");
        }

        public IDictionary<string, bool> GetShaderArgs()
        {
            var args = new Dictionary<string, bool>();
            if (Data == null) return args;
            foreach (var kv in Data.GetArray("m_intParams")) args.Add(kv.Get<string>("m_name"), kv.GetInt64("m_nValue") != 0);

            var specialDeps = (R_SpecialDependencies)Parent.REDI.Structs[REDI.REDIStruct.SpecialDependencies];
            var hemiOctIsoRoughness_RG_B = specialDeps.List.Any(dependancy => dependancy.CompilerIdentifier == "CompileTexture" && dependancy.String == "Texture Compiler Version Mip HemiOctIsoRoughness_RG_B");
            if (hemiOctIsoRoughness_RG_B) args.Add("HemiOctIsoRoughness_RG_B", true);

            var invert = specialDeps.List.Any(dependancy => dependancy.CompilerIdentifier == "CompileTexture" && dependancy.String == "Texture Compiler Version LegacySource1InvertNormals");
            if (invert) args.Add("LegacySource1InvertNormals", true);

            return args;
        }
    }

    #endregion

    #region D_Mesh
    //was:Resource/ResourceTypes/Mesh

    public class D_Mesh : XKV3_NTRO, IMesh, IHaveMetaInfo
    {
        IVBIB _vbib;
        public IVBIB VBIB
        {
            //new format has VBIB block, for old format we can get it from NTRO DATA block
            get => _vbib ??= Parent.VBIB ?? new VBIB(Data);
            set => _vbib = value;
        }
        public Vector3 MinBounds { get; private set; }
        public Vector3 MaxBounds { get; private set; }
        public D_Morph MorphData { get; set; }

        public D_Mesh(Binary_Src pak) : base("PermRenderMeshData_t") { }

        public void GetBounds()
        {
            var sceneObjects = Data.GetArray("m_sceneObjects");
            if (sceneObjects.Length == 0)
            {
                MinBounds = MaxBounds = new Vector3(0, 0, 0);
                return;
            }
            var minBounds = sceneObjects[0].GetVector3("m_vMinBounds"); //: sceneObjects[0].GetSub("m_vMinBounds").ToVector3();
            var maxBounds = sceneObjects[0].GetVector3("m_vMaxBounds"); //: sceneObjects[0].GetSub("m_vMaxBounds").ToVector3();
            for (var i = 1; i < sceneObjects.Length; ++i)
            {
                var localMin = sceneObjects[i].GetVector3("m_vMinBounds"); //: sceneObjects[i].GetSub("m_vMinBounds").ToVector3();
                var localMax = sceneObjects[i].GetVector3("m_vMaxBounds"); //: sceneObjects[i].GetSub("m_vMaxBounds").ToVector3();
                minBounds.X = Math.Min(minBounds.X, localMin.X);
                minBounds.Y = Math.Min(minBounds.Y, localMin.Y);
                minBounds.Z = Math.Min(minBounds.Z, localMin.Z);
                maxBounds.X = Math.Max(maxBounds.X, localMax.X);
                maxBounds.Y = Math.Max(maxBounds.Y, localMax.Y);
                maxBounds.Z = Math.Max(maxBounds.Z, localMax.Z);
            }
            MinBounds = minBounds;
            MaxBounds = maxBounds;
        }

        public async void LoadExternalMorphData(PakFile fileLoader)
        {
            if (MorphData == null)
            {
                var morphSetPath = Data.Get<string>("m_morphSet");
                if (!string.IsNullOrEmpty(morphSetPath))
                {
                    var morphSetResource = await fileLoader.LoadFileObject<Binary_Src>(morphSetPath + "_c");
                    if (morphSetResource != null)
                    {
                        //MorphData = morphSetResource.GetBlockByType<MRPH>() as DATAMorph;
                        var abc = morphSetResource.GetBlockByType<MRPH>();
                        MorphData = abc as object as D_Morph;
                    }
                }
            }

            await MorphData?.LoadFlexData(fileLoader);
        }

        public List<MetaInfo> GetInfoNodes(MetaManager resource, FileSource file, object tag) => (Parent as IHaveMetaInfo).GetInfoNodes(resource, file, tag);
    }

    #endregion

    #region D_Model
    //was:Resource/ResourceTypes/Model

    public class D_Model : XKV3_NTRO, IValveModel
    {
        public Skeleton Skeleton => CachedSkeleton ??= Skeleton.FromModelData(Data);

        List<Animation> CachedAnimations;
        Skeleton CachedSkeleton;
        readonly IDictionary<(IVBIB VBIB, int MeshIndex), IVBIB> remappedVBIBCache = new Dictionary<(IVBIB VBIB, int MeshIndex), IVBIB>();

        public int[] GetRemapTable(int meshIndex)
        {
            var remapTableStarts = Data.Get<int[]>("m_remappingTableStarts");
            if (remapTableStarts.Length <= meshIndex) return null;

            // Get the remap table and invert it for our construction method
            var remapTable = Data.Get<int[]>("m_remappingTable").Select(i => (int)i);
            var start = (int)remapTableStarts[meshIndex];
            return remapTable.Skip(start).Take(Skeleton.LocalRemapTable.Length).ToArray();
        }

        public IVBIB RemapBoneIndices(IVBIB vbib, int meshIndex)
        {
            if (Skeleton.Bones.Length == 0) return vbib;
            if (remappedVBIBCache.TryGetValue((vbib, meshIndex), out var res)) return res;
            res = vbib.RemapBoneIndices(VBIB.CombineRemapTables(new int[][] { GetRemapTable(meshIndex), Skeleton.LocalRemapTable }));
            remappedVBIBCache.Add((vbib, meshIndex), res);
            return res;
        }

        public IEnumerable<(int MeshIndex, string MeshName, long LoDMask)> GetReferenceMeshNamesAndLoD()
        {
            var refLODGroupMasks = Data.GetInt64Array("m_refLODGroupMasks");
            var refMeshes = Data.Get<string[]>("m_refMeshes");
            var result = new List<(int MeshIndex, string MeshName, long LoDMask)>();
            for (var meshIndex = 0; meshIndex < refMeshes.Length; meshIndex++)
            {
                var refMesh = refMeshes[meshIndex];
                if (!string.IsNullOrEmpty(refMesh)) result.Add((meshIndex, refMesh, refLODGroupMasks[meshIndex]));
            }
            return result;
        }

        public IEnumerable<(D_Mesh Mesh, int MeshIndex, string Name, long LoDMask)> GetEmbeddedMeshesAndLoD()
            => GetEmbeddedMeshes().Zip(Data.GetInt64Array("m_refLODGroupMasks"), (l, r) => (l.Mesh, l.MeshIndex, l.Name, r));

        public IEnumerable<(D_Mesh Mesh, int MeshIndex, string Name)> GetEmbeddedMeshes()
        {
            var meshes = new List<(D_Mesh Mesh, int MeshIndex, string Name)>();
            if (Parent.ContainsBlockType<CTRL>())
            {
                var ctrl = Parent.GetBlockByType<CTRL>() as XKV3;
                var embeddedMeshes = ctrl.Data.GetArray("embedded_meshes");
                if (embeddedMeshes == null) return meshes;

                foreach (var embeddedMesh in embeddedMeshes)
                {
                    var name = embeddedMesh.Get<string>("name");
                    var meshIndex = (int)embeddedMesh.Get<int>("mesh_index");
                    var dataBlockIndex = (int)embeddedMesh.Get<int>("data_block");
                    var vbibBlockIndex = (int)embeddedMesh.Get<int>("vbib_block");

                    var mesh = Parent.GetBlockByIndex<D_Mesh>(dataBlockIndex);
                    mesh.VBIB = Parent.GetBlockByIndex<VBIB>(vbibBlockIndex);

                    var morphBlockIndex = (int)embeddedMesh.Get<int>("morph_block");
                    if (morphBlockIndex >= 0) mesh.MorphData = Parent.GetBlockByIndex<D_Morph>(morphBlockIndex);

                    meshes.Add((mesh, meshIndex, name));
                }
            }
            return meshes;
        }

        public D_PhysAggregateData GetEmbeddedPhys()
        {
            if (!Parent.ContainsBlockType<CTRL>()) return null;

            var ctrl = Parent.GetBlockByType<CTRL>() as XKV3;
            var embeddedPhys = ctrl.Data.GetSub("embedded_physics");
            if (embeddedPhys == null) return null;

            var physBlockIndex = (int)embeddedPhys.Get<int>("phys_data_block");
            return Parent.GetBlockByIndex<D_PhysAggregateData>(physBlockIndex);
        }

        public IEnumerable<string> GetReferencedPhysNames()
            => Data.Get<string[]>("m_refPhysicsData");

        public IEnumerable<string> GetReferencedAnimationGroupNames()
            => Data.Get<string[]>("m_refAnimGroups");

        public IEnumerable<Animation> GetEmbeddedAnimations()
        {
            var embeddedAnimations = new List<Animation>();
            if (!Parent.ContainsBlockType<CTRL>()) return embeddedAnimations;

            var ctrl = Parent.GetBlockByType<CTRL>() as XKV3;
            var embeddedAnimation = ctrl.Data.GetSub("embedded_animation");
            if (embeddedAnimation == null) return embeddedAnimations;

            var groupDataBlockIndex = (int)embeddedAnimation.Get<int>("group_data_block");
            var animDataBlockIndex = (int)embeddedAnimation.Get<int>("anim_data_block");

            var animationGroup = Parent.GetBlockByIndex<XKV3_NTRO>(groupDataBlockIndex);
            var decodeKey = animationGroup.Data.GetSub("m_decodeKey");
            var animationDataBlock = Parent.GetBlockByIndex<XKV3_NTRO>(animDataBlockIndex);
            return Animation.FromData(animationDataBlock.Data, decodeKey, Skeleton);
        }

        public IEnumerable<Animation> GetAllAnimations(IOpenGfx gfx)
        {
            if (CachedAnimations != null) return CachedAnimations;

            var animGroupPaths = GetReferencedAnimationGroupNames();
            var animations = GetEmbeddedAnimations().ToList();

            // Load animations from referenced animation groups
            foreach (var animGroupPath in animGroupPaths)
            {
                var animGroup = gfx.LoadFileObject<Binary_Src>($"{animGroupPath}_c").Result;
                if (animGroup != default) animations.AddRange(AnimationGroupLoader.LoadAnimationGroup(animGroup, gfx, Skeleton));
            }

            CachedAnimations = animations.ToList();
            return CachedAnimations;
        }

        public IEnumerable<string> GetMeshGroups()
            => Data.Get<string[]>("m_meshGroups");

        public IEnumerable<string> GetMaterialGroups()
           => Data.Get<IDictionary<string, object>[]>("m_materialGroups").Select(group => group.Get<string>("m_name"));

        public IEnumerable<string> GetDefaultMeshGroups()
        {
            var defaultGroupMask = Data.GetUInt64("m_nDefaultMeshGroupMask");
            return GetMeshGroups().Where((group, index) => ((ulong)(1 << index) & defaultGroupMask) != 0);
        }

        public IEnumerable<bool> GetActiveMeshMaskForGroup(string groupName)
        {
            var groupIndex = GetMeshGroups().ToList().IndexOf(groupName);
            var meshGroupMasks = Data.GetInt64Array("m_refMeshGroupMasks");
            return groupIndex >= 0
                ? meshGroupMasks.Select(mask => (mask & 1 << groupIndex) != 0)
                : meshGroupMasks.Select(_ => false);
        }
    }

    #endregion

    #region D_Morph
    //was:Resource/ResourceTypes/Morph

    public class D_Morph : XKV3_NTRO
    {
        public enum MorphBundleType //was:Resource/Enum/MorphBundleType
        {
            None = 0,
            PositionSpeed = 1,
            NormalWrinkle = 2,
        }

        public Dictionary<string, Vector3[]> FlexData { get; private set; }

        public D_Morph() : base("MorphSetData_t") { }

        public async Task LoadFlexData(PakFile fileLoader)
        {
            var atlasPath = Data.Get<string>("m_pTextureAtlas");
            if (string.IsNullOrEmpty(atlasPath)) return;

            var textureResource = await fileLoader.LoadFileObject<D_Texture>(atlasPath + "_c");
            if (textureResource == null) return;

            LocalFunction();
            // Note the use of a local non-async function so you can use `Span<T>`
            void LocalFunction()
            {
                var width = Data.GetInt32("m_nWidth");
                var height = Data.GetInt32("m_nHeight");

                FlexData = new Dictionary<string, Vector3[]>();
                var texture = textureResource; // as ITexture;
                var texWidth = texture.Width;
                var texHeight = texture.Height;
                var texPixels = texture.ReadOne(0);
                // Some vmorf_c may be another old struct(NTROValue, eg: models/heroes/faceless_void/faceless_void_body.vmdl_c). the latest struct is IKeyValueCollection.
                var morphDatas = GetMorphKeyValueCollection(Data, "m_morphDatas");
                if (morphDatas == null || !morphDatas.Any()) return;

                var bundleTypes = GetMorphKeyValueCollection(Data, "m_bundleTypes").Select(kv => ParseBundleType(kv.Value)).ToArray();

                foreach (var pair in morphDatas)
                {
                    if (!(pair.Value is IDictionary<string, object> morphData)) continue;

                    var morphName = morphData.Get<string>("m_name");
                    if (string.IsNullOrEmpty(morphName)) continue; // Exist some empty names may need skip.

                    var rectData = new Vector3[height * width];
                    rectData.Initialize();

                    var morphRectDatas = morphData.GetSub("m_morphRectDatas");
                    foreach (var morphRectData in morphRectDatas)
                    {
                        var rect = morphRectData.Value as IDictionary<string, object>;
                        var xLeftDst = rect.GetInt32("m_nXLeftDst");
                        var yTopDst = rect.GetInt32("m_nYTopDst");
                        var rectWidth = (int)Math.Round(rect.GetFloat("m_flUWidthSrc") * texWidth, 0);
                        var rectHeight = (int)Math.Round(rect.GetFloat("m_flVHeightSrc") * texHeight, 0);
                        var bundleDatas = rect.GetSub("m_bundleDatas");

                        foreach (var bundleData in bundleDatas)
                        {
                            var bundleKey = int.Parse(bundleData.Key, CultureInfo.InvariantCulture);

                            // We currently only support Position. TODO: Add Normal support for gltf
                            if (bundleTypes[bundleKey] != MorphBundleType.PositionSpeed) continue;

                            var bundle = bundleData.Value as IDictionary<string, object>;
                            var rectU = (int)Math.Round(bundle.GetFloat("m_flULeftSrc") * texWidth, 0);
                            var rectV = (int)Math.Round(bundle.GetFloat("m_flVTopSrc") * texHeight, 0);
                            var ranges = bundle.Get<float[]>("m_ranges");
                            var offsets = bundle.Get<float[]>("m_offsets");

                            throw new NotImplementedException();
                            //for (var row = rectV; row < rectV + rectHeight; row++)
                            //    for (var col = rectU; col < rectU + rectWidth; col++)
                            //    {
                            //        var colorIndex = row * texWidth + col;
                            //        var color = texPixels[colorIndex];
                            //        var dstI = row - rectV + yTopDst;
                            //        var dstJ = col - rectU + xLeftDst;

                            //        rectData[dstI * width + dstJ] = new Vector3(
                            //            color.Red / 255f * ranges[0] + offsets[0],
                            //            color.Green / 255f * ranges[1] + offsets[1],
                            //            color.Blue / 255f * ranges[2] + offsets[2]
                            //        );
                            //    }
                        }
                    }
                    FlexData.Add(morphName, rectData);
                }
            }
        }

        static MorphBundleType ParseBundleType(object bundleType)
            => bundleType is uint bundleTypeEnum ? (MorphBundleType)bundleTypeEnum
            : bundleType is string bundleTypeString ? bundleTypeString switch
            {
                "MORPH_BUNDLE_TYPE_POSITION_SPEED" => MorphBundleType.PositionSpeed,
                "BUNDLE_TYPE_POSITION_SPEED" => MorphBundleType.PositionSpeed,
                "MORPH_BUNDLE_TYPE_NORMAL_WRINKLE" => MorphBundleType.NormalWrinkle,
                _ => throw new NotImplementedException($"Unhandled bundle type: {bundleTypeString}"),
            }
            : throw new NotImplementedException("Unhandled bundle type");

        static IDictionary<string, object> GetMorphKeyValueCollection(IDictionary<string, object> data, string name)
        {
            throw new NotImplementedException();
            //var kvObj = data.Get<object>(name);
            //if (kvObj is NTROStruct ntroStruct) return ntroStruct.ToKVObject();
            //if (kvObj is NTROValue[] ntroArray)
            //{
            //    var kv = new KVObject("root", true);
            //    foreach (var ntro in ntroArray) kv.AddProperty("", ntro.ToKVValue());
            //    return kv;
            //}
            //return kvObj as IDictionary<string, object>;
        }
    }

    #endregion

    #region D_NTRO

    //was:Resource/ResourceTypes/NTRO
    public class D_NTRO : DATA
    {
        protected Binary_Src Parent;
        public IDictionary<string, object> Data;
        public string StructName;

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            Parent = parent;
            Data = ReadStructure(r, StructName != null
                ? parent.NTRO.ReferencedStructs.Find(s => s.Name == StructName)
                : parent.NTRO.ReferencedStructs.First(), Offset);
        }

        IDictionary<string, object> ReadStructure(BinaryReader r, NTRO.ResourceDiskStruct refStruct, long startingOffset)
        {
            var structEntry = new Dictionary<string, object> {
                { "_name", refStruct.Name }
            };
            foreach (var field in refStruct.FieldIntrospection)
            {
                r.Seek(startingOffset + field.OnDiskOffset);
                ReadFieldIntrospection(r, field, ref structEntry);
            }

            // Some structs are padded, so all the field sizes do not add up to the size on disk
            r.Seek(startingOffset + refStruct.DiskSize);
            if (refStruct.BaseStructId != 0)
                r.Peek(z =>
                {
                    var newStruct = Parent.NTRO.ReferencedStructs.First(x => x.Id == refStruct.BaseStructId);
                    // Valve doesn't print this struct's type, so we can't just call ReadStructure *sigh*
                    foreach (var field in newStruct.FieldIntrospection)
                    {
                        z.Seek(startingOffset + field.OnDiskOffset);
                        ReadFieldIntrospection(z, field, ref structEntry);
                    }
                });
            return structEntry;
        }

        void ReadFieldIntrospection(BinaryReader r, NTRO.ResourceDiskStruct.Field field, ref Dictionary<string, object> structEntry)
        {
            var count = (uint)field.Count;
            if (count == 0) count = 1;
            var pointer = false;
            var prevOffset = 0L;

            if (field.Indirections.Count > 0)
            {
                if (field.Indirections.Count > 1) throw new NotImplementedException("More than one indirection, not yet handled.");
                if (field.Count > 0) throw new NotImplementedException("Indirection.Count > 0 && field.Count > 0");

                var indirection = (NTRO.SchemaIndirectionType)field.Indirections[0];
                var offset = r.ReadUInt32();
                if (indirection == NTRO.SchemaIndirectionType.ResourcePointer)
                {
                    pointer = true;
                    if (offset == 0)
                    {
                        structEntry.Add(field.FieldName, MakeValue<byte?>(field.Type, null, true)); // being byte shouldn't matter 
                        return;
                    }
                    prevOffset = r.Tell();
                    r.Skip(offset - 4);
                }
                else if (indirection == NTRO.SchemaIndirectionType.ResourceArray)
                {
                    count = r.ReadUInt32();
                    prevOffset = r.Tell();
                    if (count > 0) r.Skip(offset - 8);
                }
                else throw new ArgumentOutOfRangeException(nameof(indirection), $"Unsupported indirection {indirection}");
            }
            if (field.Count > 0 || field.Indirections.Count > 0)
            {
                //if (field.Type == NTRO.DataType.Byte) { }
                var values = new object[(int)count];
                for (var i = 0; i < count; i++) values[i] = ReadField(r, field, pointer);
                structEntry.Add(field.FieldName, values);
            }
            else for (var i = 0; i < count; i++) structEntry.Add(field.FieldName, ReadField(r, field, pointer));
            if (prevOffset > 0) r.Seek(prevOffset);
        }

        object ReadField(BinaryReader r, NTRO.ResourceDiskStruct.Field field, bool pointer)
        {
            switch (field.Type)
            {
                case NTRO.SchemaFieldType.Struct:
                    {
                        var newStruct = Parent.NTRO.ReferencedStructs.First(x => x.Id == field.TypeData);
                        return MakeValue<IDictionary<string, object>>(field.Type, ReadStructure(r, newStruct, r.BaseStream.Position), pointer);
                    }
                case NTRO.SchemaFieldType.Enum: return MakeValue<uint>(field.Type, r.ReadUInt32(), pointer);
                case NTRO.SchemaFieldType.SByte: return MakeValue<sbyte>(field.Type, r.ReadSByte(), pointer);
                case NTRO.SchemaFieldType.Byte: return MakeValue<byte>(field.Type, r.ReadByte(), pointer);
                case NTRO.SchemaFieldType.Boolean: return MakeValue<bool>(field.Type, r.ReadByte() == 1 ? true : false, pointer);
                case NTRO.SchemaFieldType.Int16: return MakeValue<short>(field.Type, r.ReadInt16(), pointer);
                case NTRO.SchemaFieldType.UInt16: return MakeValue<ushort>(field.Type, r.ReadUInt16(), pointer);
                case NTRO.SchemaFieldType.Int32: return MakeValue<int>(field.Type, r.ReadInt32(), pointer);
                case NTRO.SchemaFieldType.UInt32: return MakeValue<uint>(field.Type, r.ReadUInt32(), pointer);
                case NTRO.SchemaFieldType.Float: return MakeValue<float>(field.Type, r.ReadSingle(), pointer);
                case NTRO.SchemaFieldType.Int64: return MakeValue<long>(field.Type, r.ReadInt64(), pointer);
                case NTRO.SchemaFieldType.ExternalReference:
                    {
                        var id = r.ReadUInt64();
                        var value = id > 0 ? Parent.RERL?.RERLInfos.FirstOrDefault(c => c.Id == id)?.Name : null;
                        return MakeValue<string>(field.Type, value, pointer);
                    }
                case NTRO.SchemaFieldType.UInt64: return MakeValue<ulong>(field.Type, r.ReadUInt64(), pointer);
                case NTRO.SchemaFieldType.Vector3D: return MakeValue<Vector3>(field.Type, new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), pointer);
                case NTRO.SchemaFieldType.Quaternion: return MakeValue<Quaternion>(field.Type, new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), pointer);
                case NTRO.SchemaFieldType.Color: return MakeValue<Vector4<byte>>(field.Type, new Vector4<byte>(r.ReadByte(), r.ReadByte(), r.ReadByte(), r.ReadByte()), pointer);
                case NTRO.SchemaFieldType.Fltx4:
                case NTRO.SchemaFieldType.Vector4D:
                case NTRO.SchemaFieldType.FourVectors: return MakeValue<Vector4>(field.Type, new Vector4(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()), pointer);
                case NTRO.SchemaFieldType.Char:
                case NTRO.SchemaFieldType.ResourceString: return MakeValue<string>(field.Type, r.ReadO32UTF8(), pointer);
                case NTRO.SchemaFieldType.Vector2D: return MakeValue<float[]>(field.Type, new[] { r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle() }, pointer);
                case NTRO.SchemaFieldType.Matrix3x4:
                case NTRO.SchemaFieldType.Matrix3x4a:
                    return MakeValue<Matrix4x4>(field.Type, new Matrix4x4(
                        r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), 0,
                        r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), 0,
                        r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), 0,
                        r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), 0), pointer);
                case NTRO.SchemaFieldType.Transform:
                    return MakeValue<Matrix3x3>(field.Type, new Matrix4x4(
                        r.ReadSingle(), r.ReadSingle(), 0, 0,
                        r.ReadSingle(), r.ReadSingle(), 0, 0,
                        r.ReadSingle(), r.ReadSingle(), 0, 0,
                        r.ReadSingle(), r.ReadSingle(), 0, 0), pointer);
                default: throw new ArgumentOutOfRangeException(nameof(field.Type), $"Unknown data type: {field.Type} (name: {field.FieldName})");
            }
        }

        static object MakeValue<T>(NTRO.SchemaFieldType type, object data, bool pointer) => data;

        public override string ToString() => Data?.ToString() ?? "None";
    }

    #endregion

    #region D_Panorama
    //was:Resource/ResourceTypes/Panorama

    public class D_Panorama : DATA
    {
        public class NameEntry
        {
            public string Name { get; set; }
            public uint Unknown1 { get; set; }
            public uint Unknown2 { get; set; }
        }

        public List<NameEntry> Names { get; } = new List<NameEntry>();

        public byte[] Data { get; private set; }
        public uint Crc32 { get; private set; }

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            Crc32 = r.ReadUInt32();
            var size = r.ReadUInt16();
            for (var i = 0; i < size; i++)
                Names.Add(new NameEntry
                {
                    Name = r.ReadVUString(),
                    Unknown1 = r.ReadUInt32(),
                    Unknown2 = r.ReadUInt32(),
                });
            var headerSize = r.BaseStream.Position - Offset;
            Data = r.ReadBytes((int)Size - (int)headerSize);

            // Valve seemingly screwed up when they started minifying vcss and the crc no longer matches
            // See core/pak01 in Artifact Foundry for such files
            if (!parent.ContainsBlockType<SRMA>() && Crc32Digest.Compute(Data) != Crc32) throw new InvalidDataException("CRC32 mismatch for read data.");
        }

        public override string ToString() => Encoding.UTF8.GetString(Data);
    }

    #endregion

    #region D_PanoramaLayout
    //was:Resource/ResourceTypes/PanoramaLayout

    public class D_PanoramaLayout : D_Panorama
    {
        XKV3 _layoutContent;

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            base.Read(parent, r);
            _layoutContent = parent.GetBlockByType<LACO>();
        }

        public override string ToString() => _layoutContent == default
            ? base.ToString()
            : PanoramaLayoutPrinter.Print(_layoutContent.Data);

        static class PanoramaLayoutPrinter
        {
            public static string Print(IDictionary<string, object> layoutRoot)
            {
                using var w = new IndentedTextWriter();
                w.WriteLine("<!-- xml reconstructed by ValveResourceFormat: https://vrf.steamdb.info/ -->");
                var root = layoutRoot.GetSub("m_AST")?.GetSub("m_pRoot");
                if (root == default) throw new InvalidDataException("Unknown LaCo format, unable to format to XML");
                PrintNode(root, w);
                return w.ToString();
            }

            static void PrintNode(IDictionary<string, object> node, IndentedTextWriter writer)
            {
                var type = node.Get<string>("eType");
                switch (type)
                {
                    case "ROOT": PrintPanelBase("root", node, writer); break;
                    case "STYLES": PrintPanelBase("styles", node, writer); break;
                    case "INCLUDE": PrintInclude(node, writer); break;
                    case "PANEL": PrintPanel(node, writer); break;
                    case "SCRIPT_BODY": PrintScriptBody(node, writer); break;
                    case "SCRIPTS": PrintPanelBase("scripts", node, writer); break;
                    case "SNIPPET": PrintSnippet(node, writer); break;
                    case "SNIPPETS": PrintPanelBase("snippets", node, writer); break;
                    default: throw new ArgumentOutOfRangeException(nameof(type), $"Unknown node type {type}");
                };
            }

            static void PrintPanel(IDictionary<string, object> node, IndentedTextWriter w)
            {
                var name = node.Get<string>("name");
                PrintPanelBase(name, node, w);
            }

            static void PrintPanelBase(string name, IDictionary<string, object> node, IndentedTextWriter w)
            {
                var attributes = NodeAttributes(node);
                var nodeChildren = NodeChildren(node);
                if (!nodeChildren.Any()) { PrintOpenNode(name, attributes, " />", w); return; }
                PrintOpenNode(name, attributes, ">", w); w.Indent++;
                foreach (var child in nodeChildren) PrintNode(child, w);
                w.Indent--; w.WriteLine($"</{name}>");
            }

            static void PrintInclude(IDictionary<string, object> node, IndentedTextWriter w)
            {
                var reference = node.GetSub("child");
                w.Write($"<include src=");
                PrintAttributeOrReferenceValue(reference, w);
                w.WriteLine(" />");
            }

            static void PrintScriptBody(IDictionary<string, object> node, IndentedTextWriter w)
            {
                var content = node.Get<string>("name");
                w.Write("<script><![CDATA[");
                w.Write(content);
                w.WriteLine("]]></script>");
            }

            static void PrintSnippet(IDictionary<string, object> node, IndentedTextWriter w)
            {
                var nodeChildren = NodeChildren(node);
                var name = node.Get<string>("name");
                w.WriteLine($"<snippet name=\"{name}\">"); w.Indent++;
                foreach (var child in nodeChildren) PrintNode(child, w);
                w.Indent--; w.WriteLine("</snippet>");
            }

            static void PrintOpenNode(string name, IEnumerable<IDictionary<string, object>> attributes, string nodeEnding, IndentedTextWriter w)
            {
                w.Write($"<{name}");
                PrintAttributes(attributes, w);
                w.WriteLine(nodeEnding);
            }

            static void PrintAttributes(IEnumerable<IDictionary<string, object>> attributes, IndentedTextWriter w)
            {
                foreach (var attribute in attributes)
                {
                    var name = attribute.Get<string>("name");
                    var value = attribute.GetSub("child");
                    w.Write($" {name}=");
                    PrintAttributeOrReferenceValue(value, w);
                }
            }

            static void PrintAttributeOrReferenceValue(IDictionary<string, object> attributeValue, IndentedTextWriter w)
            {
                var value = attributeValue.Get<string>("name");
                var type = attributeValue.Get<string>("eType");
                value = type switch
                {
                    "REFERENCE_COMPILED" => "s2r://" + value,
                    "REFERENCE_PASSTHROUGH" => "file://" + value,
                    "PANEL_ATTRIBUTE_VALUE" => SecurityElement.Escape(value),
                    _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unknown node type {type}"),
                };
                w.Write($"\"{value}\"");
            }

            static bool IsAttribute(IDictionary<string, object> node) => node.Get<string>("eType") == "PANEL_ATTRIBUTE";
            static IEnumerable<IDictionary<string, object>> NodeAttributes(IDictionary<string, object> node) => SubNodes(node).Where(n => IsAttribute(n));
            static IEnumerable<IDictionary<string, object>> NodeChildren(IDictionary<string, object> node) => SubNodes(node).Where(n => !IsAttribute(n));
            static IEnumerable<IDictionary<string, object>> SubNodes(IDictionary<string, object> node)
                => node.ContainsKey("vecChildren") ? node.GetArray("vecChildren") : node.ContainsKey("child")
                    ? ([node.GetSub("child")])
                    : (IEnumerable<IDictionary<string, object>>)[];
        }
    }

    #endregion

    #region D_PanoramaStyle
    //was:Resource/ResourceTypes/PanoramaStyle

    public class D_PanoramaStyle : D_Panorama
    {
        XKV3 SourceMap;

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            base.Read(parent, r);
            SourceMap = parent.GetBlockByType<SRMA>();
        }

        public override string ToString() => ToString(true);

        public string ToString(bool applySourceMapIfPresent)
            => (applySourceMapIfPresent && SourceMap != default && !(SourceMap.Data.Get<object>("DBITSLC") is null))
                ? Encoding.UTF8.GetString(PanoramaSourceMapDecode(Data, SourceMap.Data))
                : base.ToString();

        static byte[] PanoramaSourceMapDecode(byte[] data, IDictionary<string, object> sourceMap)
        {
            var mapping = sourceMap.GetArray("DBITSLC", kvArray => (kvArray.GetInt32("0"), kvArray.GetInt32("1"), kvArray.GetInt32("2")));
            var results = new List<IEnumerable<byte>>();
            var currentCol = 0;
            var currentLine = 1;
            for (var i = 0; i < mapping.Length - 1; i++)
            {
                var (startIndex, sourceLine, sourceColumn) = mapping[i];
                var (nextIndex, _, _) = mapping[i + 1];

                // Prepend newlines if they are in front of this chunk according to sourceLineByteIndices
                if (currentLine < sourceLine) { results.Add(Enumerable.Repeat(Encoding.UTF8.GetBytes("\n")[0], sourceLine - currentLine)); currentCol = 0; currentLine = sourceLine; }
                // Referring back to an object higher in hierarchy, also add newline here
                else if (sourceLine < currentLine) { results.Add(Enumerable.Repeat(Encoding.UTF8.GetBytes("\n")[0], 1)); currentCol = 0; currentLine++; }
                // Prepend spaces until we catch up to the index we need to be at
                if (currentCol < sourceColumn) { results.Add(Enumerable.Repeat(Encoding.UTF8.GetBytes(" ")[0], sourceColumn - currentCol)); currentCol = sourceColumn; }
                // Copy destination
                var length = nextIndex - startIndex;
                results.Add(data.Skip(startIndex).Take(length));
                currentCol += length;
            }
            results.Add(Enumerable.Repeat(Encoding.UTF8.GetBytes("\n")[0], 1));
            results.Add(data.Skip(mapping[^1].Item1));
            return results.SelectMany(_ => _).ToArray();
        }
    }

    #endregion

    #region D_ParticleSystem
    //was:Resource/ResourceTypes/ParticleSystem

    public class D_ParticleSystem : XKV3_NTRO, IParticleSystem
    {
        public IEnumerable<IDictionary<string, object>> Renderers => Data.GetArray("m_Renderers") ?? [];

        public IEnumerable<IDictionary<string, object>> Operators => Data.GetArray("m_Operators") ?? [];

        public IEnumerable<IDictionary<string, object>> Initializers => Data.GetArray("m_Initializers") ?? [];

        public IEnumerable<IDictionary<string, object>> Emitters => Data.GetArray("m_Emitters") ?? [];

        public IEnumerable<string> GetChildParticleNames(bool enabledOnly = false)
        {
            IEnumerable<IDictionary<string, object>> children = Data.GetArray("m_Children");
            if (children == null) return [];
            if (enabledOnly) children = children.Where(c => !c.ContainsKey("m_bDisableChild") || !c.Get<bool>("m_bDisableChild"));
            return children.Select(c => c.Get<string>("m_ChildRef")).ToList();
        }
    }

    #endregion

    #region D_PhysAggregateData
    //was:Resource/ResourceTypes/PhysAggregateData

    public class D_PhysAggregateData : XKV3_NTRO
    {
        public D_PhysAggregateData() : base("VPhysXAggregateData_t") { }
    }

    #endregion

    #region D_Plaintext
    //was:Resource/ResourceTypes/Plaintext

    public class D_Plaintext : D_NTRO
    {
        public new string Data;

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            Data = Encoding.UTF8.GetString(r.ReadBytes((int)Size));
        }

        public override string ToString() => Data;
    }

    #endregion

    #region D_PostProcessing
    //was:Resource/ResourceTypes/PostProcessing

    public class D_PostProcessing : XKV3_NTRO
    {
        public D_PostProcessing() : base("PostProcessingResource_t") { }

        public IDictionary<string, object> GetTonemapParams() => Data.Get<bool>("m_bHasTonemapParams") ? Data.Get<IDictionary<string, object>>("m_toneMapParams") : null;
        public IDictionary<string, object> GetBloomParams() => Data.Get<bool>("m_bHasBloomParams") ? Data.Get<IDictionary<string, object>>("m_bloomParams") : null;
        public IDictionary<string, object> GetVignetteParams() => Data.Get<bool>("m_bHasVignetteParams") ? Data.Get<IDictionary<string, object>>("m_vignetteParams") : null;
        public IDictionary<string, object> GetLocalContrastParams() => Data.Get<bool>("m_bHasLocalContrastParams") ? Data.Get<IDictionary<string, object>>("m_localConstrastParams") : null;
        public bool HasColorCorrection() => Data.TryGetValue("m_bHasColorCorrection", out var value) ? (bool)value : true; // Assumed true pre Aperture Desk Job
        public int GetColorCorrectionLUTDimension() => Data.Get<int>("m_nColorCorrectionVolumeDim");
        public byte[] GetColorCorrectionLUT() => Data.Get<byte[]>("m_colorCorrectionVolumeData");

        public byte[] GetRawData()
        {
            var lut = GetColorCorrectionLUT().Clone() as byte[];
            var j = 0;
            for (var i = 0; i < lut.Length; i++)
            {
                if (((i + 1) % 4) == 0) continue; // Skip each 4th byte
                lut[j++] = lut[i];
            }
            return lut[..j];
        }

        public string ToValvePostProcessing(bool preloadLookupTable = false, string lutFileName = "")
        {
            var outKV3 = new Dictionary<string, object>
            {
                { "_class", "CPostProcessData" }
            };

            var layers = new List<object>();

            var tonemapParams = GetTonemapParams();
            var bloomParams = GetBloomParams();
            var vignetteParams = GetVignetteParams();
            var localContrastParams = GetLocalContrastParams();

            if (tonemapParams != null)
            {
                var tonemappingLayer = new Dictionary<string, object>
                {
                    { "_class", "CToneMappingLayer" },
                    { "m_nOpacityPercent", 100L },
                    { "m_bVisible", true },
                    { "m_pLayerMask", null },
                };
                var tonemappingLayerParams = new Dictionary<string, object>();
                foreach (var kv in tonemapParams) tonemappingLayerParams.Add(kv.Key, kv.Value);
                tonemappingLayer.Add("m_params", tonemappingLayerParams);
                layers.Add(tonemappingLayer);
            }

            if (bloomParams != null)
            {
                var bloomLayer = new Dictionary<string, object>
                {
                    { "_class", "CBloomLayer" },
                    { "m_name",  "Bloom" },
                    { "m_nOpacityPercent", 100L },
                    { "m_bVisible", true },
                    { "m_pLayerMask", null },
                };
                var bloomLayerParams = new Dictionary<string, object>();
                foreach (var kv in tonemapParams) bloomLayerParams.Add(kv.Key, kv.Value);
                bloomLayer.Add("m_params", bloomLayerParams);
                layers.Add(bloomLayer);
            }

            if (vignetteParams != null) { } // TODO: How does the vignette layer look like?
            if (localContrastParams != null) { } // TODO: How does the local contrast layer look like?

            // All other layers are compiled into a 3D lookup table
            if (HasColorCorrection())
            {
                var ccLayer = new Dictionary<string, object>
                {
                    { "_class", "CColorLookupColorCorrectionLayer" },
                    { "m_name",  "VRF Extracted Lookup Table" },
                    { "m_nOpacityPercent", 100L },
                    { "m_bVisible", true },
                    { "m_pLayerMask", null },
                    { "m_fileName", lutFileName },
                };
                var lut = new List<object>();
                if (preloadLookupTable) foreach (var b in GetRawData()) lut.Add(b / 255d);
                ccLayer.Add("m_lut", lut.ToArray());
                ccLayer.Add("m_nDim", GetColorCorrectionLUTDimension());
                layers.Add(ccLayer);
            }

            outKV3.Add("m_layers", layers.ToArray());

            return new KV3File(outKV3).ToString();
        }
    }

    #endregion

    #region D_ResourceManifest
    //was:Resource/ResourceTypes/ResourceManifest

    public class D_ResourceManifest : D_NTRO
    {
        public List<List<string>> Resources { get; private set; }

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            if (parent.ContainsBlockType<NTRO>())
            {
                var ntro = new D_NTRO { StructName = "ResourceManifest_t", Offset = Offset, Size = Size };
                ntro.Read(parent, r);
                Resources = new List<List<string>> { new List<string>(ntro.Data.Get<string[]>("m_ResourceFileNameList")) };
                return;
            }

            var version = r.ReadInt32();
            if (version != 8) throw new ArgumentOutOfRangeException(nameof(version), $"Unknown version {version}");

            Resources = new List<List<string>>();
            var blockCount = r.ReadInt32();
            for (var block = 0; block < blockCount; block++)
            {
                var strings = new List<string>();
                var originalOffset = r.BaseStream.Position;
                var offset = r.ReadInt32();
                var count = r.ReadInt32();
                r.Seek(originalOffset + offset);
                for (var i = 0; i < count; i++)
                {
                    var returnOffset = r.BaseStream.Position;
                    var stringOffset = r.ReadInt32();
                    r.Seek(returnOffset + stringOffset);
                    strings.Add(r.ReadVUString());
                    r.Seek(returnOffset + 4);
                }
                r.Seek(originalOffset + 8);
                Resources.Add(strings);
            }
        }

        public override string ToString()
        {
            using var w = new IndentedTextWriter();
            foreach (var block in Resources)
            {
                foreach (var entry in block) w.WriteLine(entry);
                w.WriteLine();
            }
            return w.ToString();
        }
    }

    #endregion

    #region D_Sound
    //was:Resource/ResourceTypes/Sound

    public struct EmphasisSample
    {
        public float Time;
        public float Value;
    }

    public struct PhonemeTag(float startTime, float endTime, ushort phonemeCode)
    {
        public float StartTime = startTime;
        public float EndTime = endTime;
        public ushort PhonemeCode = phonemeCode;
    }

    public class Sentence(PhonemeTag[] runTimePhonemes)
    {
        public bool ShouldVoiceDuck;
        public PhonemeTag[] RunTimePhonemes = runTimePhonemes;
        public EmphasisSample[] EmphasisSamples;
    }

    public class D_Sound : DATA
    {
        public enum AudioFileType
        {
            AAC = 0,
            WAV = 1,
            MP3 = 2,
        }

        public enum AudioFormatV4
        {
            PCM16 = 0,
            PCM8 = 1,
            MP3 = 2,
            ADPCM = 3,
        }

        // https://github.com/naudio/NAudio/blob/fb35ce8367f30b8bc5ea84e7d2529e172cf4c381/NAudio.Core/Wave/WaveFormats/WaveFormatEncoding.cs
        public enum WaveAudioFormat : uint
        {
            Unknown = 0,
            PCM = 1,
            ADPCM = 2,
        }

        /// <summary>
        /// Gets the audio file type.
        /// </summary>
        /// <value>The file type.</value>
        public AudioFileType SoundType { get; private set; }

        /// <summary>
        /// Gets the samples per second.
        /// </summary>
        /// <value>The sample rate.</value>
        public uint SampleRate { get; private set; }

        /// <summary>
        /// Gets the bit size.
        /// </summary>
        /// <value>The bit size.</value>
        public uint Bits { get; private set; }

        /// <summary>
        /// Gets the number of channels. 1 for mono, 2 for stereo.
        /// </summary>
        /// <value>The number of channels.</value>
        public uint Channels { get; private set; }

        /// <summary>
        /// Gets the bitstream encoding format.
        /// </summary>
        /// <value>The audio format.</value>
        public WaveAudioFormat AudioFormat { get; private set; }

        public uint SampleSize { get; private set; }

        public uint SampleCount { get; private set; }

        public int LoopStart { get; private set; }

        public int LoopEnd { get; private set; }

        public float Duration { get; private set; }

        public Sentence Sentence { get; private set; }

        public uint StreamingDataSize { get; private set; }

        protected Binary_Src Parent { get; private set; }

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            Parent = parent;
            r.Seek(Offset);
            if (parent.Version > 4) throw new InvalidDataException($"Invalid vsnd version '{parent.Version}'");
            if (parent.Version >= 4)
            {
                SampleRate = r.ReadUInt16();
                var soundFormat = (AudioFormatV4)r.ReadByte();
                Channels = r.ReadByte();
                switch (soundFormat)
                {
                    case AudioFormatV4.PCM8:
                        SoundType = AudioFileType.WAV;
                        Bits = 8;
                        SampleSize = 1;
                        AudioFormat = WaveAudioFormat.PCM;
                        break;
                    case AudioFormatV4.PCM16:
                        SoundType = AudioFileType.WAV;
                        Bits = 16;
                        SampleSize = 2;
                        AudioFormat = WaveAudioFormat.PCM;
                        break;
                    case AudioFormatV4.MP3:
                        SoundType = AudioFileType.MP3;
                        break;
                    case AudioFormatV4.ADPCM:
                        SoundType = AudioFileType.WAV;
                        Bits = 4;
                        SampleSize = 1;
                        AudioFormat = WaveAudioFormat.ADPCM;
                        throw new NotImplementedException("ADPCM is currently not implemented correctly.");
                    default: throw new ArgumentOutOfRangeException(nameof(soundFormat), $"Unexpected audio type {soundFormat}");
                }
            }
            else
            {
                var bitpackedSoundInfo = r.ReadUInt32();
                var type = ExtractSub(bitpackedSoundInfo, 0, 2);
                if (type > 2) throw new InvalidDataException($"Unknown sound type in old vsnd version: {type}");
                SoundType = (AudioFileType)type;
                Bits = ExtractSub(bitpackedSoundInfo, 2, 5);
                Channels = ExtractSub(bitpackedSoundInfo, 7, 2);
                SampleSize = ExtractSub(bitpackedSoundInfo, 9, 3);
                AudioFormat = (WaveAudioFormat)ExtractSub(bitpackedSoundInfo, 12, 2);
                SampleRate = ExtractSub(bitpackedSoundInfo, 14, 17);
            }
            LoopStart = r.ReadInt32();
            SampleCount = r.ReadUInt32();
            Duration = r.ReadSingle();

            var sentenceOffset = (long)r.ReadUInt32();
            r.Skip(4);
            if (sentenceOffset != 0) sentenceOffset = r.BaseStream.Position + sentenceOffset;

            r.Skip(4); // Skipping over m_pHeader
            StreamingDataSize = r.ReadUInt32();

            if (parent.Version >= 1)
            {
                var d = r.ReadUInt32();
                if (d != 0) throw new ArgumentOutOfRangeException(nameof(d), $"Unexpected {d}");
                var e = r.ReadUInt32();
                if (e != 0) throw new ArgumentOutOfRangeException(nameof(e), $"Unexpected {e}");
            }
            // v2 and v3 are the same?
            if (parent.Version >= 2)
            {
                var f = r.ReadUInt32();
                if (f != 0) throw new ArgumentOutOfRangeException(nameof(f), $"Unexpected {f}");
            }
            if (parent.Version >= 4) LoopEnd = r.ReadInt32();

            ReadPhonemeStream(r, sentenceOffset);
        }

        void ReadPhonemeStream(BinaryReader r, long sentenceOffset)
        {
            if (sentenceOffset == 0) return;
            r.Seek(sentenceOffset);
            var numPhonemeTags = r.ReadInt32();
            var a = r.ReadInt32(); // numEmphasisSamples ?
            var b = r.ReadInt32(); // Sentence.ShouldVoiceDuck ?
            // Skip sounds that have these
            if (a != 0 || b != 0) return;
            Sentence = new Sentence(new PhonemeTag[numPhonemeTags]);
            for (var i = 0; i < numPhonemeTags; i++)
            {
                Sentence.RunTimePhonemes[i] = new PhonemeTag(r.ReadSingle(), r.ReadSingle(), r.ReadUInt16());
                r.Skip(2);
            }
        }

        static uint ExtractSub(uint l, byte offset, byte nrBits)
        {
            var rightShifted = l >> offset;
            var mask = (1 << nrBits) - 1;
            return (uint)(rightShifted & mask);
        }

        /// <summary>
        /// Returns a fully playable sound data.
        /// In case of WAV files, header is automatically generated as Valve removes it when compiling.
        /// </summary>
        /// <returns>Byte array containing sound data.</returns>
        public byte[] GetSound()
        {
            using var sound = GetSoundStream();
            return sound.ToArray();
        }

        /// <summary>
        /// Returns a fully playable sound data.
        /// In case of WAV files, header is automatically generated as Valve removes it when compiling.
        /// </summary>
        /// <returns>Memory stream containing sound data.</returns>
        public MemoryStream GetSoundStream()
        {
            var r = Parent.Reader;
            r.Seek(Offset + Size);
            var s = new MemoryStream();
            if (SoundType == AudioFileType.WAV)
            {
                // http://soundfile.sapp.org/doc/WaveFormat/
                // http://www.codeproject.com/Articles/129173/Writing-a-Proper-Wave-File
                var headerRiff = new byte[] { 0x52, 0x49, 0x46, 0x46 };
                var formatWave = new byte[] { 0x57, 0x41, 0x56, 0x45 };
                var formatTag = new byte[] { 0x66, 0x6d, 0x74, 0x20 };
                var subChunkId = new byte[] { 0x64, 0x61, 0x74, 0x61 };

                var byteRate = SampleRate * Channels * (Bits / 8);
                var blockAlign = Channels * (Bits / 8);
                if (AudioFormat == WaveAudioFormat.ADPCM)
                {
                    byteRate = 1;
                    blockAlign = 4;
                }

                s.Write(headerRiff, 0, headerRiff.Length);
                s.Write(PackageInt(StreamingDataSize + 42, 4), 0, 4);

                s.Write(formatWave, 0, formatWave.Length);
                s.Write(formatTag, 0, formatTag.Length);
                s.Write(PackageInt(16, 4), 0, 4); // Subchunk1Size

                s.Write(PackageInt((uint)AudioFormat, 2), 0, 2);
                s.Write(PackageInt(Channels, 2), 0, 2);
                s.Write(PackageInt(SampleRate, 4), 0, 4);
                s.Write(PackageInt(byteRate, 4), 0, 4);
                s.Write(PackageInt(blockAlign, 2), 0, 2);
                s.Write(PackageInt(Bits, 2), 0, 2);
                //s.Write(PackageInt(0,2), 0, 2); // Extra param size
                s.Write(subChunkId, 0, subChunkId.Length);
                s.Write(PackageInt(StreamingDataSize, 4), 0, 4);
            }
            r.BaseStream.CopyTo(s, (int)StreamingDataSize);
            // Flush and reset position so that consumers can read it
            s.Flush();
            s.Seek(0, SeekOrigin.Begin);
            return s;
        }

        static byte[] PackageInt(uint source, int length)
        {
            var retVal = new byte[length];
            retVal[0] = (byte)(source & 0xFF);
            retVal[1] = (byte)((source >> 8) & 0xFF);
            if (length == 4)
            {
                retVal[2] = (byte)((source >> 0x10) & 0xFF);
                retVal[3] = (byte)((source >> 0x18) & 0xFF);
            }
            return retVal;
        }

        public override string ToString()
        {
            var b = new StringBuilder();
            b.AppendLine($"SoundType: {SoundType}");
            b.AppendLine($"Sample Rate: {SampleRate}");
            b.AppendLine($"Bits: {Bits}");
            b.AppendLine($"SampleSize: {SampleSize}");
            b.AppendLine($"SampleCount: {SampleCount}");
            b.AppendLine($"Format: {AudioFormat}");
            b.AppendLine($"Channels: {Channels}");
            b.AppendLine($"LoopStart: ({TimeSpan.FromSeconds(LoopStart)}) {LoopStart}");
            b.AppendLine($"LoopEnd: ({TimeSpan.FromSeconds(LoopEnd)}) {LoopEnd}");
            b.AppendLine($"Duration: {TimeSpan.FromSeconds(Duration)} ({Duration})");
            b.AppendLine($"StreamingDataSize: {StreamingDataSize}");
            if (Sentence != null)
            {
                b.AppendLine($"Sentence[{Sentence.RunTimePhonemes.Length}]:");
                foreach (var phoneme in Sentence.RunTimePhonemes) b.AppendLine($"\tPhonemeTag(StartTime={phoneme.StartTime}, EndTime={phoneme.EndTime}, PhonemeCode={phoneme.PhonemeCode})");
            }
            return b.ToString();
        }
    }

    #endregion

    #region D_SoundEventScript
    //was:Resource/ResourceTypes/SoundEventScript

    public class D_SoundEventScript : D_NTRO
    {
        public Dictionary<string, string> SoundEventScriptValue = [];

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            base.Read(parent, r);

            // Data is VSoundEventScript_t we need to iterate m_SoundEvents inside it.
            var soundEvents = Data.Get<IDictionary<string, object>>("m_SoundEvents");
            foreach (IDictionary<string, object> entry in soundEvents.Values)
            {
                // sound is VSoundEvent_t
                var soundName = entry.Get<string>("m_SoundName");
                var soundValue = entry.Get<string>("m_OperatorsKV").Replace("\n", Environment.NewLine); // make sure we have new lines
                if (SoundEventScriptValue.ContainsKey(soundName)) SoundEventScriptValue.Remove(soundName); // Duplicates last one wins
                SoundEventScriptValue.Add(soundName, soundValue);
            }
        }

        public override void WriteText(IndentedTextWriter w)
        {
            foreach (var entry in SoundEventScriptValue)
            {
                w.WriteLine($"\"{entry.Key}\" {{"); w.Indent++;
                // m_OperatorsKV wont be indented, so we manually indent it here, removing the last indent so we can close brackets later correctly.
                w.Write(entry.Value.Replace(Environment.NewLine, $"{Environment.NewLine}\t").TrimEnd('\t'));
                w.Indent--; w.WriteLine("}");
                w.WriteLine(string.Empty); // There is an empty line after every entry (including the last)
            }
        }
    }

    #endregion

    #region D_SoundStackScript
    //was:Resource/ResourceTypes/SoundStackScript

    public class D_SoundStackScript : DATA
    {
        public Dictionary<string, string> SoundStackScriptValue = [];

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            var version = r.ReadInt32();
            if (version != 8) throw new FormatException($"Unknown version: {version}");
            var count = r.ReadInt32();
            var offset = r.BaseStream.Position;
            for (var i = 0; i < count; i++)
            {
                var offsetToName = offset + r.ReadInt32(); offset += 4;
                var offsetToValue = offset + r.ReadInt32(); offset += 4;
                r.Seek(offsetToName);
                var name = r.ReadVUString();
                r.Seek(offsetToValue);
                var value = r.ReadVUString();
                r.Seek(offset);
                if (SoundStackScriptValue.ContainsKey(name)) SoundStackScriptValue.Remove(name); // duplicates last wins
                SoundStackScriptValue.Add(name, value);
            }
        }

        public override void WriteText(IndentedTextWriter w)
        {
            foreach (var entry in SoundStackScriptValue)
            {
                w.WriteLine($"// {entry.Key}");
                w.Write(entry.Value);
                w.WriteLine(string.Empty);
            }
        }
    }

    #endregion

    #region D_Texture
    //was:Resource/ResourceTypes/Texture

    public class D_Texture : DATA, ITexture
    {
        public enum VTexExtraData //was:Resource/Enums/VTexExtraData
        {
            UNKNOWN = 0,
            FALLBACK_BITS = 1,
            SHEET = 2,
            FILL_TO_POWER_OF_TWO = 3,
            COMPRESSED_MIP_SIZE = 4,
            CUBEMAP_RADIANCE_SH = 5,
        }

        [Flags]
        public enum VTexFlags //was:Resource/Enums/VTexFlags
        {
            SUGGEST_CLAMPS = 0x00000001,
            SUGGEST_CLAMPT = 0x00000002,
            SUGGEST_CLAMPU = 0x00000004,
            NO_LOD = 0x00000008,
            CUBE_TEXTURE = 0x00000010,
            VOLUME_TEXTURE = 0x00000020,
            TEXTURE_ARRAY = 0x00000040,
        }

        public enum VTexFormat : byte //was:Resource/Enums/VTexFlags
        {
            UNKNOWN = 0,
            DXT1 = 1,
            DXT5 = 2,
            I8 = 3,
            RGBA8888 = 4,
            R16 = 5,
            RG1616 = 6,
            RGBA16161616 = 7,
            R16F = 8,
            RG1616F = 9,
            RGBA16161616F = 10,
            R32F = 11,
            RG3232F = 12,
            RGB323232F = 13,
            RGBA32323232F = 14,
            JPEG_RGBA8888 = 15,
            PNG_RGBA8888 = 16,
            JPEG_DXT5 = 17,
            PNG_DXT5 = 18,
            BC6H = 19,
            BC7 = 20,
            ATI2N = 21,
            IA88 = 22,
            ETC2 = 23,
            ETC2_EAC = 24,
            R11_EAC = 25,
            RG11_EAC = 26,
            ATI1N = 27,
            BGRA8888 = 28,
        }

        public BinaryReader Reader { get; private set; }
        long DataOffset;
        public ushort Version { get; private set; }
        public ushort Width { get; private set; }
        public ushort Height { get; private set; }
        public ushort Depth { get; private set; }
        public float[] Reflectivity { get; private set; }
        public VTexFlags Flags { get; private set; }
        public VTexFormat Format { get; private set; }
        public byte NumMipMaps { get; private set; }
        public uint Picmip0Res { get; private set; }
        public Dictionary<VTexExtraData, byte[]> ExtraData { get; private set; } = [];
        public ushort NonPow2Width { get; private set; }
        public ushort NonPow2Height { get; private set; }

        int[] CompressedMips;
        bool IsActuallyCompressedMips;
        float[] RadianceCoefficients;

        public ushort ActualWidth => NonPow2Width > 0 ? NonPow2Width : Width;
        public ushort ActualHeight => NonPow2Height > 0 ? NonPow2Height : Height;

        #region ITextureInfo

        (VTexFormat type, object gl, object vulken, object unity, object unreal) TexFormat;
        byte[] Bytes;
        Range[] Mips;

        int ITexture.Width => Width;
        int ITexture.Height => Height;
        int ITexture.Depth => Depth;
        int ITexture.MipMaps => NumMipMaps;
        TextureFlags ITexture.TexFlags => (TextureFlags)Flags;

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
        {
            Reader.BaseStream.Position = Offset + Size;

            using (var b = new MemoryStream())
            {
                Mips = new Range[NumMipMaps];
                var lastLength = 0;
                for (var i = NumMipMaps - 1; i >= 0; i--)
                {
                    b.Write(ReadOne(i));
                    Mips[i] = new Range(lastLength, (int)b.Length);
                    lastLength = (int)b.Length;
                }
                Bytes = b.ToArray();
            }

            return (Bytes, (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => TexFormat.gl,
                Platform.Type.Unity => TexFormat.unity,
                Platform.Type.Unreal => TexFormat.unreal,
                Platform.Type.Vulken => TexFormat.vulken,
                Platform.Type.StereoKit => throw new NotImplementedException("StereoKit"),
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, Mips);
        }
        void ITexture.End() { }

        #endregion

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            Reader = r;
            Version = r.ReadUInt16();
            if (Version != 1) throw new FormatException($"Unknown vtex version. ({Version} != expected 1)");
            Flags = (VTexFlags)r.ReadUInt16();
            Reflectivity = [r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle()];
            Width = r.ReadUInt16();
            Height = r.ReadUInt16();
            Depth = r.ReadUInt16();
            NonPow2Width = 0;
            NonPow2Height = 0;
            Format = (VTexFormat)r.ReadByte();
            NumMipMaps = r.ReadByte();
            Picmip0Res = r.ReadUInt32();
            var extraDataOffset = r.ReadUInt32();
            var extraDataCount = r.ReadUInt32();
            if (extraDataCount > 0)
            {
                r.Skip(extraDataOffset - 8); // 8 is 2 uint32s we just read
                for (var i = 0; i < extraDataCount; i++)
                {
                    var type = (VTexExtraData)r.ReadUInt32();
                    var offset = r.ReadUInt32() - 8;
                    var size = r.ReadUInt32();
                    r.Peek(z =>
                    {
                        z.Skip(offset);
                        ExtraData.Add(type, r.ReadBytes((int)size));
                        z.Skip(-size);
                        if (type == VTexExtraData.FILL_TO_POWER_OF_TWO)
                        {
                            z.ReadUInt16();
                            var nw = z.ReadUInt16();
                            var nh = z.ReadUInt16();
                            if (nw > 0 && nh > 0 && Width >= nw && Height >= nh)
                            {
                                NonPow2Width = nw;
                                NonPow2Height = nh;
                            }
                        }
                        else if (type == VTexExtraData.COMPRESSED_MIP_SIZE)
                        {
                            var int1 = z.ReadUInt32(); // 1?
                            var mipsOffset = z.ReadUInt32();
                            var mips = z.ReadUInt32();
                            if (int1 != 1 && int1 != 0) throw new FormatException($"int1 got: {int1}");
                            IsActuallyCompressedMips = int1 == 1; // TODO: Verify whether this int is the one that actually controls compression
                            r.Skip(mipsOffset - 8);
                            CompressedMips = z.ReadPArray<int>("I", (int)mips);
                        }
                        else if (type == VTexExtraData.CUBEMAP_RADIANCE_SH)
                        {
                            var coeffsOffset = r.ReadUInt32();
                            var coeffs = r.ReadUInt32();
                            r.Skip(coeffsOffset - 8);
                            RadianceCoefficients = z.ReadPArray<float>("f", (int)coeffs); // Spherical Harmonics
                        }
                    });
                }
            }
            DataOffset = Offset + Size;

            TexFormat = Format switch
            {
                DXT1 => (DXT1, TextureGLFormat.CompressedRgbaS3tcDxt1Ext, TextureGLFormat.CompressedRgbaS3tcDxt1Ext, TextureUnityFormat.DXT1, TextureUnrealFormat.DXT1),
                //DXT3 => (DXT3, TextureGLFormat.CompressedRgbaS3tcDxt3Ext, TextureGLFormat.CompressedRgbaS3tcDxt3Ext, TextureUnityFormat.DXT3_POLYFILL, TextureUnrealFormat.DXT3),
                DXT5 => (DXT5, TextureGLFormat.CompressedRgbaS3tcDxt5Ext, TextureGLFormat.CompressedRgbaS3tcDxt5Ext, TextureUnityFormat.DXT5, TextureUnrealFormat.DXT5),
                ETC2 => (ETC2, TextureGLFormat.CompressedRgb8Etc2, TextureGLFormat.CompressedRgb8Etc2, TextureUnityFormat.ETC2_RGBA8Crunched, TextureUnrealFormat.ETC2RGB),
                ETC2_EAC => (ETC2_EAC, TextureGLFormat.CompressedRgba8Etc2Eac, TextureGLFormat.CompressedRgba8Etc2Eac, TextureUnityFormat.Unknown, TextureUnrealFormat.Unknown),
                ATI1N => (ATI1N, TextureGLFormat.CompressedRedRgtc1, TextureGLFormat.CompressedRedRgtc1, TextureUnityFormat.BC4, TextureUnrealFormat.BC4),
                ATI2N => (ATI2N, TextureGLFormat.CompressedRgRgtc2, TextureGLFormat.CompressedRgRgtc2, TextureUnityFormat.BC5, TextureUnrealFormat.BC5),
                BC6H => (BC6H, TextureGLFormat.CompressedRgbBptcUnsignedFloat, TextureGLFormat.CompressedRgbBptcUnsignedFloat, TextureUnityFormat.BC6H, TextureUnrealFormat.BC6H),
                BC7 => (BC7, TextureGLFormat.CompressedRgbaBptcUnorm, TextureGLFormat.CompressedRgbaBptcUnorm, TextureUnityFormat.BC7, TextureUnrealFormat.BC7),
                RGBA8888 => (RGBA8888, (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), TextureUnityFormat.RGBA32, TextureUnrealFormat.R8G8B8A8),
                RGBA16161616F => (RGBA16161616F, (TextureGLFormat.Rgba16f, TextureGLPixelFormat.Rgba, TextureGLPixelType.Float), (TextureGLFormat.Rgba16f, TextureGLPixelFormat.Rgba, TextureGLPixelType.Float), TextureUnityFormat.RGBAFloat, TextureUnrealFormat.FloatRGBA),
                I8 => (I8, TextureGLFormat.Intensity8, TextureGLFormat.Intensity8, TextureUnityFormat.Unknown, TextureUnityFormat.Unknown), //(TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte)
                R16 => (R16, (TextureGLFormat.R16, TextureGLPixelFormat.Red, TextureGLPixelType.UnsignedShort), (TextureGLFormat.R16, TextureGLPixelFormat.Red, TextureGLPixelType.UnsignedShort), TextureUnityFormat.R16, TextureUnrealFormat.R16UInt),
                R16F => (R16F, (TextureGLFormat.R16f, TextureGLPixelFormat.Red, TextureGLPixelType.Float), (TextureGLFormat.R16f, TextureGLPixelFormat.Red, TextureGLPixelType.Float), TextureUnityFormat.RFloat, TextureUnrealFormat.R16F),
                RG1616 => (RG1616, (TextureGLFormat.Rg16, TextureGLPixelFormat.Rg, TextureGLPixelType.UnsignedShort), (TextureGLFormat.Rg16, TextureGLPixelFormat.Rg, TextureGLPixelType.UnsignedShort), TextureUnityFormat.RG16, TextureUnrealFormat.R16G16UInt),
                RG1616F => (RG1616F, (TextureGLFormat.Rg16f, TextureGLPixelFormat.Rg, TextureGLPixelType.Float), (TextureGLFormat.Rg16f, TextureGLPixelFormat.Rg, TextureGLPixelType.Float), TextureUnityFormat.RGFloat, TextureUnrealFormat.R16G16UInt),
                _ => (Format, null, null, null, null),
            };
        }

        public byte[] ReadOne(int index)
        {
            var uncompressedSize = TextureHelper.GetMipmapTrueDataSize(TexFormat.gl, Width, Height, Depth, index);
            if (!IsActuallyCompressedMips) return Reader.ReadBytes(uncompressedSize);
            var compressedSize = CompressedMips[index];
            if (compressedSize >= uncompressedSize) return Reader.ReadBytes(uncompressedSize);
            return Reader.DecompressLz4(compressedSize, uncompressedSize);
        }

        public TextureSequences GetSpriteSheetData()
        {
            if (!ExtraData.TryGetValue(VTexExtraData.SHEET, out var bytes)) return null;
            var sequences = new TextureSequences();
            using var r = new BinaryReader(new MemoryStream(bytes));
            var version = r.ReadUInt32();
            if (version != 8) throw new ArgumentOutOfRangeException(nameof(version), $"Unknown version {version}");

            var numSequences = r.ReadUInt32();
            for (var i = 0; i < numSequences; i++)
            {
                var sequence = new TextureSequences.Sequence();
                var id = r.ReadUInt32();
                sequence.Clamp = r.ReadBoolean();
                sequence.AlphaCrop = r.ReadBoolean();
                sequence.NoColor = r.ReadBoolean();
                sequence.NoAlpha = r.ReadBoolean();
                var framesOffset = r.BaseStream.Position + r.ReadUInt32();
                var numFrames = r.ReadUInt32();
                sequence.FramesPerSecond = r.ReadSingle(); // Not too sure about this one
                var nameOffset = r.BaseStream.Position + r.ReadUInt32();
                var floatParamsOffset = r.BaseStream.Position + r.ReadUInt32();
                var floatParamsCount = r.ReadUInt32();
                r.Peek(z =>
                {
                    z.Seek(nameOffset);
                    sequence.Name = z.ReadVUString();

                    if (floatParamsCount > 0)
                    {
                        r.Seek(floatParamsOffset);
                        for (var p = 0; p < floatParamsCount; p++)
                        {
                            var floatParamNameOffset = r.BaseStream.Position + r.ReadUInt32();
                            var floatValue = r.ReadSingle();
                            var offsetNextParam = r.BaseStream.Position;
                            r.Seek(floatParamNameOffset);
                            var floatName = r.ReadVUString();
                            r.Seek(offsetNextParam);
                            sequence.FloatParams.Add(floatName, floatValue);
                        }
                    }

                    z.Seek(framesOffset);
                    sequence.Frames = new TextureSequences.Frame[numFrames];
                    for (var f = 0; f < numFrames; f++)
                    {
                        var displayTime = r.ReadSingle();
                        var imageOffset = r.BaseStream.Position + r.ReadUInt32();
                        var imageCount = r.ReadUInt32();
                        var originalOffset = r.BaseStream.Position;
                        var images = new TextureSequences.Image[imageCount];
                        sequence.Frames[f] = new TextureSequences.Frame
                        {
                            DisplayTime = displayTime,
                            Images = images,
                        };

                        r.Seek(imageOffset);
                        for (var i = 0; i < images.Length; i++)
                            images[i] = new TextureSequences.Image
                            {
                                CroppedMin = r.ReadVector2(),
                                CroppedMax = r.ReadVector2(),
                                UncroppedMin = r.ReadVector2(),
                                UncroppedMax = r.ReadVector2(),
                            };
                        r.Skip(originalOffset);
                    }
                });
                sequences.Add(sequence);
            }
            return sequences;
        }

        public override string ToString()
        {
            using var w = new IndentedTextWriter();
            w.WriteLine($"{"VTEX Version",-12} = {Version}");
            w.WriteLine($"{"Width",-12} = {Width}");
            w.WriteLine($"{"Height",-12} = {Height}");
            w.WriteLine($"{"Depth",-12} = {Depth}");
            w.WriteLine($"{"NonPow2W",-12} = {NonPow2Width}");
            w.WriteLine($"{"NonPow2H",-12} = {NonPow2Height}");
            w.WriteLine($"{"Reflectivity",-12} = ( {Reflectivity[0]:F6}, {Reflectivity[1]:F6}, {Reflectivity[2]:F6}, {Reflectivity[3]:F6} )");
            w.WriteLine($"{"NumMipMaps",-12} = {NumMipMaps}");
            w.WriteLine($"{"Picmip0Res",-12} = {Picmip0Res}");
            w.WriteLine($"{"Format",-12} = {(int)Format} (VTEX_FORMAT_{Format})");
            w.WriteLine($"{"Flags",-12} = 0x{(int)Flags:X8}");
            foreach (Enum value in Enum.GetValues(Flags.GetType())) if (Flags.HasFlag(value)) w.WriteLine($"{"",-12} | 0x{(Convert.ToInt32(value)):X8} = VTEX_FLAG_{value}");
            w.WriteLine($"{"Data Data",-12} = {ExtraData.Count} entries:");
            var entry = 0;
            foreach (var b in ExtraData)
            {
                w.WriteLine($"{"",-12}   [ Entry {entry++}: VTEX_EXTRA_DATA_{b.Key} - {b.Value.Length} bytes ]");
                if (b.Key == VTexExtraData.COMPRESSED_MIP_SIZE && CompressedMips != null) w.WriteLine($"{"",-16}   [ {CompressedMips.Length} mips, sized: {string.Join(", ", CompressedMips)} ]");
                else if (b.Key == VTexExtraData.CUBEMAP_RADIANCE_SH && RadianceCoefficients != null) w.WriteLine($"{"",-16}   [ {RadianceCoefficients.Length} coefficients, sized: {string.Join(", ", RadianceCoefficients)} ]");
                else if (b.Key == VTexExtraData.SHEET && CompressedMips != null) w.WriteLine($"{"",-16}   [ {CompressedMips.Length} mips, sized: {string.Join(", ", CompressedMips)} ]");
            }
            //if (Format is not JPEG_DXT5 and not JPEG_RGBA8888 and not PNG_DXT5 and not PNG_RGBA8888)
            if (!(Format is JPEG_DXT5 || Format is JPEG_RGBA8888 || Format is PNG_DXT5 || Format is PNG_RGBA8888))
                for (var j = 0; j < NumMipMaps; j++) w.WriteLine($"Mip level {j} - buffer size: {TextureHelper.GetMipmapTrueDataSize(TexFormat.gl, Width, Height, Depth, j)}");
            return w.ToString();
        }

        public int CalculateTextureDataSize()
        {
            if (Format == PNG_DXT5 || Format == PNG_RGBA8888) return TextureHelper.CalculatePngSize(Reader, DataOffset);
            var bytes = 0;
            if (CompressedMips != null) bytes = CompressedMips.Sum();
            else for (var j = 0; j < NumMipMaps; j++) bytes += CalculateBufferSizeForMipLevel(j);
            return bytes;
        }

        int CalculateBufferSizeForMipLevel(int mipLevel)
        {
            var (bytesPerPixel, _) = TextureHelper.GetBlockSize(TexFormat.gl);
            var width = TextureHelper.MipLevelSize(Width, mipLevel);
            var height = TextureHelper.MipLevelSize(Height, mipLevel);
            var depth = TextureHelper.MipLevelSize(Depth, mipLevel);
            if ((Flags & VTexFlags.CUBE_TEXTURE) != 0) bytesPerPixel *= 6;
            if (Format == DXT1 || Format == DXT5 || Format == BC6H || Format == BC7 ||
                Format == ETC2 || Format == ETC2_EAC || Format == ATI1N)
            {
                var misalign = width % 4;
                if (misalign > 0) width += 4 - misalign;
                misalign = height % 4;
                if (misalign > 0) height += 4 - misalign;
                if (width < 4 && width > 0) width = 4;
                if (height < 4 && height > 0) height = 4;
                if (depth < 4 && depth > 1) depth = 4;
                var numBlocks = (width * height) >> 4;
                numBlocks *= depth;
                return numBlocks * bytesPerPixel;
            }
            return width * height * depth * bytesPerPixel;
        }
    }

    #endregion

    #region D_World
    //was:Resource/ResourceTypes/World

    public class D_World : XKV3_NTRO
    {
        public IEnumerable<string> GetEntityLumpNames() => Data.Get<string[]>("m_entityLumps");
        public IEnumerable<string> GetWorldNodeNames() => Data.GetArray("m_worldNodes").Select(nodeData => nodeData.Get<string>("m_worldNodePrefix")).ToList();
    }

    #endregion

    #region D_WorldNode

    //was:Resource/ResourceTypes/WorldNode
    public class D_WorldNode : XKV3_NTRO { }

    #endregion

    #region R_AdditionalInputDependencies

    public class R_AdditionalInputDependencies : R_InputDependencies
    {
        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine($"Struct m_AdditionalInputDependencies[{List.Count}] = [");
            WriteList(w);
        }
    }

    #endregion

    #region R_AdditionalRelatedFiles

    public class R_AdditionalRelatedFiles : REDI
    {
        public class AdditionalRelatedFile
        {
            public string ContentRelativeFilename { get; set; }
            public string ContentSearchPath { get; set; }

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("ResourceAdditionalRelatedFile_t {"); w.Indent++;
                w.WriteLine($"CResourceString m_ContentRelativeFilename = \"{ContentRelativeFilename}\"");
                w.WriteLine($"CResourceString m_ContentSearchPath = \"{ContentSearchPath}\"");
                w.Indent--; w.WriteLine("}");
            }
        }

        public List<AdditionalRelatedFile> List = [];

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            for (var i = 0; i < Size; i++)
                List.Add(new AdditionalRelatedFile
                {
                    ContentRelativeFilename = r.ReadO32UTF8(),
                    ContentSearchPath = r.ReadO32UTF8()
                });
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine($"Struct m_AdditionalRelatedFiles[{List.Count}] = ["); w.Indent++;
            foreach (var dep in List) dep.WriteText(w);
            w.Indent--; w.WriteLine("]");
        }
    }

    #endregion

    #region R_ArgumentDependencies

    public class R_ArgumentDependencies : REDI
    {
        public class ArgumentDependency
        {
            public string ParameterName { get; set; }
            public string ParameterType { get; set; }
            public uint Fingerprint { get; set; }
            public uint FingerprintDefault { get; set; }

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("ResourceArgumentDependency_t {"); w.Indent++;
                w.WriteLine($"CResourceString m_ParameterName = \"{ParameterName}\"");
                w.WriteLine($"CResourceString m_ParameterType = \"{ParameterType}\"");
                w.WriteLine($"uint32 m_nFingerprint = 0x{Fingerprint:X8}");
                w.WriteLine($"uint32 m_nFingerprintDefault = 0x{FingerprintDefault:X8}");
                w.Indent--; w.WriteLine("}");
            }
        }

        public List<ArgumentDependency> List { get; } = [];

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            for (var i = 0; i < Size; i++)
                List.Add(new ArgumentDependency
                {
                    ParameterName = r.ReadO32UTF8(),
                    ParameterType = r.ReadO32UTF8(),
                    Fingerprint = r.ReadUInt32(),
                    FingerprintDefault = r.ReadUInt32()
                });
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine($"Struct m_ArgumentDependencies[{List.Count}] = ["); w.Indent++;
            foreach (var dep in List) dep.WriteText(w);
            w.Indent--; w.WriteLine("]");
        }
    }

    #endregion

    #region R_ChildResourceList

    public class R_ChildResourceList : REDI
    {
        public class ReferenceInfo
        {
            public ulong Id { get; set; }
            public string ResourceName { get; set; }
            public uint Unknown { get; set; }

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("ResourceReferenceInfo_t {"); w.Indent++;
                w.WriteLine($"uint64 m_nId = 0x{Id:X16}");
                w.WriteLine($"CResourceString m_pResourceName = \"{ResourceName}\"");
                w.Indent--; w.WriteLine("}");
            }
        }

        public List<ReferenceInfo> List = [];

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            for (var i = 0; i < Size; i++)
                List.Add(new ReferenceInfo
                {
                    Id = r.ReadUInt64(),
                    ResourceName = r.ReadO32UTF8(),
                    Unknown = r.ReadUInt32()
                });
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine($"Struct m_ChildResourceList[{List.Count}] = ["); w.Indent++;
            foreach (var dep in List) dep.WriteText(w);
            w.Indent--; w.WriteLine("]");
        }
    }

    #endregion

    #region R_CustomDependencies

    public class R_CustomDependencies : REDI
    {
        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            if (Size > 0) throw new NotImplementedException("CustomDependencies block is not handled.");
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine($"Struct m_CustomDependencies[{0}] = ["); w.Indent++;
            w.Indent--; w.WriteLine("]");
        }
    }

    #endregion

    #region R_ExtraFloatData

    public class R_ExtraFloatData : REDI
    {
        public class EditFloatData
        {
            public string Name { get; set; }
            public float Value { get; set; }

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("ResourceEditFloatData_t {"); w.Indent++;
                w.WriteLine($"CResourceString m_Name = \"{Name}\"");
                w.WriteLine($"float32 m_flFloat = {Value:F6}");
                w.Indent--; w.WriteLine("}");
            }
        }

        public List<EditFloatData> List { get; } = new List<EditFloatData>();

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            for (var i = 0; i < Size; i++) List.Add(new EditFloatData
            {
                Name = r.ReadO32UTF8(),
                Value = r.ReadSingle()
            });
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine($"Struct m_ExtraFloatData[{List.Count}] = ["); w.Indent++;
            foreach (var dep in List) dep.WriteText(w);
            w.Indent--; w.WriteLine("]");
        }
    }

    #endregion

    #region R_ExtraIntData

    public class R_ExtraIntData : REDI
    {
        public class EditIntData
        {
            public string Name { get; set; }
            public int Value { get; set; }

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("ResourceEditIntData_t {"); w.Indent++;
                w.WriteLine($"CResourceString m_Name = \"{Name}\"");
                w.WriteLine($"int32 m_nInt = {Value}");
                w.Indent--; w.WriteLine("}");
            }
        }

        public List<EditIntData> List { get; } = new List<EditIntData>();

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            for (var i = 0; i < Size; i++) List.Add(new EditIntData
            {
                Name = r.ReadO32UTF8(),
                Value = r.ReadInt32()
            });
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine($"Struct m_ExtraIntData[{List.Count}] = ["); w.Indent++;
            foreach (var dep in List) dep.WriteText(w);
            w.Indent--; w.WriteLine("]");
        }
    }

    #endregion

    #region R_ExtraStringData

    public class R_ExtraStringData : REDI
    {
        public class EditStringData
        {
            public string Name { get; set; }
            public string Value { get; set; }

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("ResourceEditStringData_t {"); w.Indent++;
                w.WriteLine($"CResourceString m_Name = \"{Name}\"");
                var lines = Value.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                if (lines.Length > 1)
                {
                    w.Indent++;
                    w.Write("CResourceString m_String = \"");
                    foreach (var line in lines) w.WriteLine(line);
                    w.WriteLine("\"");
                    w.Indent--;
                }
                else w.WriteLine($"CResourceString m_String = \"{Value}\"");
                w.Indent--; w.WriteLine("}");
            }
        }

        public List<EditStringData> List { get; } = new List<EditStringData>();

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            for (var i = 0; i < Size; i++) List.Add(new EditStringData
            {
                Name = r.ReadO32UTF8(),
                Value = r.ReadO32UTF8()
            });
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine($"Struct m_ExtraStringData[{List.Count}] = ["); w.Indent++;
            foreach (var dep in List) dep.WriteText(w);
            w.Indent--; w.WriteLine("]");
        }
    }

    #endregion

    #region R_InputDependencies

    public class R_InputDependencies : REDI
    {
        public class InputDependency
        {
            public string ContentRelativeFilename { get; set; }
            public string ContentSearchPath { get; set; }
            public uint FileCRC { get; set; }
            public uint Flags { get; set; }

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("ResourceInputDependency_t {"); w.Indent++;
                w.WriteLine($"CResourceString m_ContentRelativeFilename = \"{ContentRelativeFilename}\"");
                w.WriteLine($"CResourceString m_ContentSearchPath = \"{ContentSearchPath}\"");
                w.WriteLine($"uint32 m_nFileCRC = 0x{FileCRC:X8}");
                w.WriteLine($"uint32 m_nFlags = 0x{Flags:X8}");
                w.Indent--; w.WriteLine("}");
            }
        }

        public List<InputDependency> List = [];

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            for (var i = 0; i < Size; i++)
                List.Add(new InputDependency
                {
                    ContentRelativeFilename = r.ReadO32UTF8(),
                    ContentSearchPath = r.ReadO32UTF8(),
                    FileCRC = r.ReadUInt32(),
                    Flags = r.ReadUInt32()
                });
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine($"Struct m_InputDependencies[{List.Count}] = [");
            WriteList(w);
        }

        protected void WriteList(IndentedTextWriter w)
        {
            w.Indent++;
            foreach (var dep in List) dep.WriteText(w);
            w.Indent--; w.WriteLine("]");
        }
    }

    #endregion

    #region R_SpecialDependencies

    public class R_SpecialDependencies : REDI
    {
        public class SpecialDependency
        {
            public string String { get; set; }
            public string CompilerIdentifier { get; set; }
            public uint Fingerprint { get; set; }
            public uint UserData { get; set; }

            public void WriteText(IndentedTextWriter w)
            {
                w.WriteLine("ResourceSpecialDependency_t {"); w.Indent++;
                w.WriteLine($"CResourceString m_String = \"{String}\"");
                w.WriteLine($"CResourceString m_CompilerIdentifier = \"{CompilerIdentifier}\"");
                w.WriteLine($"uint32 m_nFingerprint = 0x{Fingerprint:X8}");
                w.WriteLine($"uint32 m_nUserData = 0x{UserData:X8}");
                w.Indent--; w.WriteLine("}");
            }
        }

        public List<SpecialDependency> List = [];

        public override void Read(Binary_Src parent, BinaryReader r)
        {
            r.Seek(Offset);
            for (var i = 0; i < Size; i++)
                List.Add(new SpecialDependency
                {
                    String = r.ReadO32UTF8(),
                    CompilerIdentifier = r.ReadO32UTF8(),
                    Fingerprint = r.ReadUInt32(),
                    UserData = r.ReadUInt32()
                });
        }

        public override void WriteText(IndentedTextWriter w)
        {
            w.WriteLine($"Struct m_SpecialDependencies[{List.Count}] = ["); w.Indent++;
            foreach (var dep in List) dep.WriteText(w);
            w.Indent--; w.WriteLine("]");
        }
    }

    #endregion
}

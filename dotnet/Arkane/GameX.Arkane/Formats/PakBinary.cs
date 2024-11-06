using GameX.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GameX.Arkane.Formats
{
    #region PakBinary_Danae

    public unsafe class PakBinary_Danae : PakBinary<PakBinary_Danae>
    {
        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            var files = source.Files = [];
            var key = Encoding.ASCII.GetBytes((string)source.Game.Key); int keyLength = key.Length, keyIndex = 0;

            // move to fat table
            r.Seek(r.ReadUInt32());
            var fatSize = (int)r.ReadUInt32();
            var fatBytes = r.ReadBytes(fatSize);

            // read int32
            int readInt32(ref byte* b)
            {
                var p = b;
                *(p + 0) = (byte)(*(p + 0) ^ key[keyIndex++]); if (keyIndex >= keyLength) keyIndex = 0;
                *(p + 1) = (byte)(*(p + 1) ^ key[keyIndex++]); if (keyIndex >= keyLength) keyIndex = 0;
                *(p + 2) = (byte)(*(p + 2) ^ key[keyIndex++]); if (keyIndex >= keyLength) keyIndex = 0;
                *(p + 3) = (byte)(*(p + 3) ^ key[keyIndex++]); if (keyIndex >= keyLength) keyIndex = 0;
                b += 4;
                return *(int*)p;
            }

            // read string
            string readString(ref byte* b)
            {
                var p = b;
                while (true)
                {
                    *p = (byte)(*p ^ key[keyIndex++]); if (keyIndex >= keyLength) keyIndex = 0;
                    if (*p == 0) break;
                    p++;
                }
                var length = (int)(p - b);
                var r = Encoding.ASCII.GetString(new ReadOnlySpan<byte>(b, length));
                b = p + 1;
                return r;
            }

            // while there are bytes
            fixed (byte* _ = fatBytes)
            {
                byte* c = _, end = _ + fatSize;
                while (c < end)
                {
                    var dirPath = readString(ref c).Replace('\\', '/');
                    var numFiles = readInt32(ref c);
                    for (var i = 0; i < numFiles; i++)
                    {
                        // get file
                        var file = new FileSource
                        {
                            Path = dirPath + readString(ref c).Replace('\\', '/'),
                            Offset = readInt32(ref c),
                            Compressed = readInt32(ref c),
                            FileSize = readInt32(ref c),
                            PackedSize = readInt32(ref c),
                        };
                        // special case
                        if (file.Path.EndsWith(".FTL")) file.Compressed = 1;
                        else if (file.Compressed == 0) file.FileSize = file.PackedSize;
                        // add file
                        files.Add(file);
                    }
                }
            }
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            r.Seek(file.Offset);
            return Task.FromResult((Stream)new MemoryStream((file.Compressed & 1) != 0
                ? r.DecompressBlast((int)file.PackedSize, (int)file.FileSize)
                : r.ReadBytes((int)file.PackedSize)));
        }
    }

    #endregion

    #region PakBinary_Void

    public unsafe class PakBinary_Void : PakBinary<PakBinary_Void>
    {
        #region Headers

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct V_File
        {
            public static (string, int) Struct = (">Q4IH", sizeof(V_File));
            public ulong Offset;
            public uint FileSize;
            public uint PackedSize;
            public uint Unknown1;
            public uint Flags;
            public ushort Flags2;
        }

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            // must be .index file
            if (!source.PakPath.EndsWith(".index")) throw new FormatException("must be a .index file");

            var files = source.Files = [];

            // master.index file
            if (source.PakPath == "master.index")
            {
                const uint MAGIC = 0x04534552;
                const uint SubMarker = 0x18000000;
                const uint EndMarker = 0x01000000;

                var magic = r.ReadUInt32E();
                if (magic != MAGIC) throw new FormatException("BAD MAGIC");
                r.Skip(4);
                var first = true;
                while (true)
                {
                    var pathSize = r.ReadUInt32();
                    if (pathSize == SubMarker) { first = false; pathSize = r.ReadUInt32(); }
                    else if (pathSize == EndMarker) break;
                    var path = r.ReadFAString((int)pathSize).Replace('\\', '/');
                    var packId = first ? 0 : r.ReadUInt16();
                    if (!path.EndsWith(".index")) continue;
                    files.Add(new FileSource
                    {
                        Path = path,
                        Pak = new SubPakFile(source, null, path),
                    });
                }
                return Task.CompletedTask;
            }

            // find files
            var fileSystem = source.FileSystem;
            var resourcePath = $"{source.PakPath[0..^6]}.resources";
            if (!fileSystem.FileExists(resourcePath)) throw new FormatException("Unable to find resources extension");
            var sharedResourcePath = new[] {
                "shared_2_3.sharedrsc",
                "shared_2_3_4.sharedrsc",
                "shared_1_2_3.sharedrsc",
                "shared_1_2_3_4.sharedrsc" }
                .FirstOrDefault(fileSystem.FileExists);

            // read
            r.Seek(4);
            var mainFileSize = r.ReadUInt32E();
            r.Skip(24);
            var numFiles = r.ReadUInt32E();
            files = source.Files = new FileSource[numFiles];
            for (var i = 0; i < numFiles; i++)
            {
                var id = r.ReadUInt32E();
                var tag1 = r.ReadL32Encoding();
                var tag2 = r.ReadL32Encoding();
                var path = (r.ReadL32Encoding() ?? "").Replace('\\', '/');
                var file = r.ReadS<V_File>();
                //var position = r.ReadUInt64E();
                //var fileSize = r.ReadUInt32E();
                //var packedSize = r.ReadUInt32E();
                //r.Skip(4);
                //var flags = r.ReadUInt32E();
                //var flags2 = r.ReadUInt16E();
                var useSharedResources = (file.Flags & 0x20) != 0 && file.Flags2 == 0x8000;
                if (useSharedResources && sharedResourcePath == null) throw new FormatException("sharedResourcePath not available");
                var newPath = useSharedResources ? sharedResourcePath : resourcePath;
                files[i] = new FileSource
                {
                    Id = (int)id,
                    Path = path,
                    Compressed = file.FileSize != file.PackedSize ? 1 : 0,
                    FileSize = file.FileSize,
                    PackedSize = file.PackedSize,
                    Offset = (long)file.Offset,
                    Tag = (newPath, tag1, tag2),
                };
            }
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            if (file.FileSize == 0 || _badPositions.Contains(file.Offset)) return Task.FromResult(System.IO.Stream.Null);
            var (path, tag1, tag2) = ((string, string, string))file.Tag;
            return Task.FromResult((Stream)source.ReaderT(r2 =>
            {
                r2.Seek(file.Offset);
                return new MemoryStream(file.Compressed != 0
                    ? r2.DecompressZlib((int)file.PackedSize, (int)file.FileSize)
                    : r2.ReadBytes((int)file.PackedSize));
            }, path));
        }

        // Bad Positions - Dishonored2
        static HashSet<long> _badPositions = [
            293, //: generated/decls/renderparm/atm/worldfog/artistscatteringcolor.decl
            917004, //: generated/decls/renderparm/ocean/patchtransform.decl
            9923823, //: generated/decls/soundevent/sound_events/bsp/bsp_physmat/bsp_foosteps/bsp_fs_player/emily/fs_e_metal_chandelier/fs_e_metal_chandelier_w.decl
            9924002, //: generated/decls/fx/contactsystem/w.emily_env.metal.chandelier.fx.decl
            32872162, //: generated/image/models/effects/textures/gameplay/blood/blood_leads_05_fl.bimage7
            32966564, //: generated/decls/material/models/effects/materials/gameplay/blood/blood_leads_05_bf.material.decl
            45704814, //: generated/decls/fx/contactsystem/pr.ar.venom_env.tile.fx.decl
        ];
    }

    #endregion
}
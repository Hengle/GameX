using GameX.Formats;
using GameX.ID.Formats.Q;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static System.IO.Polyfill;

namespace GameX.ID.Formats
{
    #region PakBinary_Bsp30
    // https://developer.valvesoftware.com/wiki/BSP_(Quake)
    // https://www.flipcode.com/archives/Quake_2_BSP_File_Format.shtml

    public unsafe class PakBinary_Bsp : PakBinary<PakBinary_Bsp>
    {
        #region Headers

        [StructLayout(LayoutKind.Sequential)]
        struct B_Header
        {
            public static (string, int) Struct = ("<31i", sizeof(B_Header));
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
            public X_LumpON MarkSurfaces; //: Faces
            public X_LumpON Edges;
            public X_LumpON SurfEdges; //: Ledges
            public X_LumpON Models;

            public void ForGameId(string id)
            {
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct B_Texture
        {
            public static (string, int) Struct = ("<16s6I", sizeof(B_Texture));
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
            var header = r.ReadS<B_Header>();
            //if (header.Version != 30) throw new FormatException("BAD VERSION");
            header.ForGameId(source.Game.Id);
            files.Add(new FileSource { Path = "entities.txt", Offset = header.Entities.Offset, FileSize = header.Entities.Num });
            files.Add(new FileSource { Path = "planes.dat", Offset = header.Planes.Offset, FileSize = header.Planes.Num });
            r.Seek(start = header.Textures.Offset);
            foreach (var o in r.ReadL32PArray<uint>("I"))
            {
                r.Seek(start + o);
                var tex = r.ReadS<B_Texture>();
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

    #region PakBinary_Pak

    public unsafe class PakBinary_Pak : PakBinary<PakBinary_Pak>
    {
        #region Factories

        public static (FileOption, Func<BinaryReader, FileSource, PakFile, Task<object>>) ObjectFactory(FileSource source, FamilyGame game)
            => source.Path.ToLowerInvariant() switch
            {
                _ => Path.GetExtension(source.Path).ToLowerInvariant() switch
                {
                    ".wav" => (0, Binary_Snd.Factory),
                    var x when x == ".jpg" => (0, Binary_Img.Factory),
                    ".tga" => (0, Binary_Tga.Factory),
                    var x when x == ".tex" || x == ".lmp" => (0, Binary_Lump.Factory),
                    ".dds" => (0, Binary_Dds.Factory),
                    ".pcx" => (0, Binary_Pcx.Factory),
                    ".bsp" => (0, Binary_Level.Factory),
                    ".mdl" => (0, Binary_Model.Factory),
                    ".spr" => (0, Binary_Sprite.Factory),
                    _ => (0, null),
                }
            };

        #endregion

        #region Headers

        const uint P_MAGIC = 0x4b434150; // PACK

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct P_Header
        {
            public static (string, int) Struct = ("<I2i", sizeof(P_Header));
            public uint Magic;
            public int DirOffset;
            public int DirLength;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct P_File
        {
            public static (string, int) Struct = ("<56s2i", sizeof(P_File));
            public fixed byte Path[56];
            public int Offset;
            public int FileSize;
        }

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            // read file
            var header = r.ReadS<P_Header>();
            if (header.Magic != P_MAGIC) throw new FormatException("BAD MAGIC");
            var numFiles = header.DirLength / sizeof(P_File);
            r.Seek(header.DirOffset);
            string path;
            source.Files = r.ReadSArray<P_File>(numFiles).Select(s =>
            {
                var file = new FileSource
                {
                    Path = path = UnsafeX.FixedAString(s.Path, 56).Replace('\\', '/'),
                    Offset = s.Offset,
                    FileSize = s.FileSize,
                };
                if (file.Path.EndsWith(".wad", StringComparison.OrdinalIgnoreCase)) file.Pak = new SubPakFile(source, file, file.Path, instance: PakBinary_Wad.Current);
                return file;
            }).ToArray();
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            r.Seek(file.Offset);
            return Task.FromResult<Stream>(new MemoryStream(r.ReadBytes((int)file.FileSize)));
        }
    }

    #endregion

    #region PakBinary_Wad

    public unsafe class PakBinary_Wad : PakBinary<PakBinary_Wad>
    {
        #region Headers

        const uint W_MAGIC = 0x32444157; //: WAD2

        [StructLayout(LayoutKind.Sequential)]
        struct W_Header
        {
            public static (string, int) Struct = ("<I2i", sizeof(W_Header));
            public uint Magic;
            public int LumpCount;
            public int LumpOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct W_Lump
        {
            public static (string, int) Struct = ("<3i4b16s", sizeof(W_Lump));
            public int Offset;
            public int PackedSize;
            public int FileSize;
            public byte Type;
            public byte Compression;
            public byte Padding0;
            public byte Padding1;
            public fixed byte Path[16];
        }

        //#define TYP_LUMPY		64				// 64 + grab command number
        //#define TYP_PALETTE		64
        //#define TYP_QTEX		65
        //#define TYP_QPIC		66
        //#define TYP_SOUND		67
        //#define TYP_MIPTEX		68

        #endregion

        public override Task Read(BinaryPakFile source, BinaryReader r, object tag)
        {
            // read file
            var header = r.ReadS<W_Header>();
            if (header.Magic != W_MAGIC) throw new FormatException("BAD MAGIC");
            r.Seek(header.LumpOffset);
            source.Files = r.ReadSArray<W_Lump>(header.LumpCount).Select(s => new FileSource
            {
                Path = $"{UnsafeX.FixedAString(s.Path, 16).Replace('\\', '/')}.tex",
                Hash = s.Type,
                Offset = s.Offset,
                PackedSize = s.PackedSize,
                FileSize = s.FileSize,
                Compressed = s.Compression,
            }).ToArray();
            return Task.CompletedTask;
        }

        public override Task<Stream> ReadData(BinaryPakFile source, BinaryReader r, FileSource file, FileOption option = default)
        {
            r.Seek(file.Offset);
            return Task.FromResult<Stream>(new MemoryStream(r.ReadBytes((int)file.FileSize)));
        }
    }

    #endregion
}
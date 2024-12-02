using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static System.IO.Polyfill;

// https://wiki.eternalmods.com/books/8-reverse-engineering-file-formats
namespace GameX.ID.Formats
{
    #region PakBinary_Bsp30
    // https://developer.valvesoftware.com/wiki/BSP_(Quake)
    // https://www.flipcode.com/archives/Quake_2_BSP_File_Format.shtml

    public unsafe class PakBinary_Bsp : PakBinary<PakBinary_Bsp>
    {
        #region Headers

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct D_Model
        {
            public X_BoundBox Bound;            // The bounding box of the Model
            public Vector3 Origin;              // origin of model, usually (0,0,0)
            public int NodeId0;                 // index of first BSP node
            public int NodeId1;                 // index of the first Clip node
            public int NodeId2;                 // index of the second Clip node
            public int NodeId3;                 // usually zero
            public int NumLeafs;                // number of BSP leaves
            public int FaceId;                  // index of Faces
            public int FaceNum;                 // number of Faces
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct D_TexInfo
        {
            public Vector3 VectorS;             // S vector, horizontal in texture space)
            public float DistS;                 // horizontal offset in texture space
            public Vector3 VectorT;             // T vector, vertical in texture space
            public float DistT;                 // vertical offset in texture space
            public uint TextureId;              // Index of Mip Texture must be in [0,numtex[
            public uint Animated;               // 0 for ordinary textures, 1 for water
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct D_Face
        {
            public ushort PlaneId;              // The plane in which the face lies: must be in [0,numplanes]
            public ushort Side;                 // 0 if in front of the plane, 1 if behind the plane
            public int LedgeId;                 // first edge in the List of edges: must be in [0,numledges]
            public ushort LedgeNum;             // number of edges in the List of edges
            public ushort TexinfoId;            // index of the Texture info the face is part of: must be in [0,numtexinfos]
            public byte TypeLight;              // type of lighting, for the face
            public byte BaseLight;              // from 0xFF (dark) to 0 (bright)
            public fixed byte Light[2];         // two additional light models
            public int LightMap;                // Pointer inside the general light map, or -1
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct D_Node
        {
            public long PlaneId;                // The plane that splits the node: must be in [0,numplanes[
            public ushort Front;                // If bit15==0, index of Front child node: If bit15==1, ~front = index of child leaf
            public ushort Back;                 // If bit15==0, id of Back child node: If bit15==1, ~back =  id of child leaf
            public Vector2<short> Box;          // Bounding box of node and all childs
            public ushort FaceId;               // Index of first Polygons in the node
            public ushort FaceNum;              // Number of faces in the node
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct D_Leaf
        {
            public int Type;                    // Special type of leaf
            public int VisList;                 // Beginning of visibility lists: must be -1 or in [0,numvislist[
            Vector2<short> Bound;               // Bounding box of the leaf
            public ushort LFaceId;              // First item of the list of faces: must be in [0,numlfaces[
            public ushort LFaceNum;             // Number of faces in the leaf
            public byte SndWater;               // level of the four ambient sounds:
            public byte SndSky;                 //   0    is no sound
            public byte SndSlime;               //   0xFF is maximum volume
            public byte SndLava;                //
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct D_Plane
        {
            public Vector3 Normal;              // Vector orthogonal to plane (Nx,Ny,Nz): with Nx2+Ny2+Nz2 = 1
            public float Dist;                  // Offset to plane, along the normal vector: Distance from (0,0,0) to the plane
            public int Type;                    // Type of plane, depending on normal vector.
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct D_ClipNode
        {
            public uint PlaneNum;               // The plane which splits the node
            public short Front;                 // If positive, id of Front child node: If -2, the Front part is inside the model: If -1, the Front part is outside the model
            public short Back;                  // If positive, id of Back child node: If -2, the Back part is inside the model: If -1, the Back part is outside the model
        }

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
            public X_LumpON MarkSurfaces; //: Faces
            public X_LumpON Edges;
            public X_LumpON SurfEdges; //: Ledges
            public X_LumpON Models;

            public void ForGameId(string id)
            {
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
            //if (header.Version != 30) throw new FormatException("BAD VERSION");
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

    #region PakBinary_Pak

    public unsafe class PakBinary_Pak : PakBinary<PakBinary_Pak>
    {
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
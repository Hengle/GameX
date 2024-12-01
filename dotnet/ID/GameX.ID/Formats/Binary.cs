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
using static System.IO.Polyfill;

// https://www.gamers.org/dEngine/quake/spec/quake-spec34/qkspec_3.htm
namespace GameX.ID.Formats
{
    #region Binary_Bsp
    public unsafe class Binary_Bsp : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Bsp(r, s));

        #region Headers

        #region Ent
        public unsafe class Ent
        {
            const int MAX_MAP_ENTITIES = 2048;

            public class epair
            {
                epair next;
                object key;
                object value;
            }

            public class Entity
            {
                Vector3 Origin;
                int FirstBrush;
                int NumBrushes;
                epair[] epairs;
                // only valid for func_areaportals
                int AreaPortalNum;
                int PortalAreas; //[2];
                int ModelNum;   //for bsp 2 map conversion
                bool WasDetail; //for SIN
            }

            //extern int num_entities;
            //extern entity_t entities[MAX_MAP_ENTITIES];
        }
        #endregion

        #region DHeader
        internal static T[] CopyLump<T>(BinaryReader r, int fileLength, X_LumpON lump, string pat, int size, int maxSize) where T : struct
        {
            int length = lump.Num, ofs = lump.Offset;
            if ((length % size) != 0) throw new FormatException("LoadBSPFile: odd lump size");
            // somehow things got out of range
            if ((length / size) > maxSize)
            {
                Console.WriteLine("WARNING: exceeded max size for lump %d size %d > maxSize %d\n", lump, length / size, maxSize);
                length = maxSize * size;
            }
            if (ofs + length > fileLength)
            {
                Console.WriteLine($"WARNING: exceeded file length for lump {lump}\n");
                length = fileLength - ofs;
                if (length <= 0) return default;
            }
            r.Seek(ofs);
            var count = length / size;
            return pat == null ? size > 1 ? r.ReadSArray<T>(count) : (T[])(object)r.ReadBytes(count)
                : r.ReadPArray<T>(pat, count);
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DHeader_H1
        {
            public static (string, int) Struct = ("<31i", sizeof(X_DHeader_H1));
            public int Version;
            public X_LumpON ENTITIES;
            public X_LumpON PLANES;
            public X_LumpON TEXTURES;
            public X_LumpON VERTEXES;
            public X_LumpON VISIBILITY;
            public X_LumpON NODES;
            public X_LumpON TEXINFO;
            public X_LumpON FACES;
            public X_LumpON LIGHTING;
            public X_LumpON CLIPNODES;
            public X_LumpON LEAFS;
            public X_LumpON MARKSURFACES;
            public X_LumpON EDGES;
            public X_LumpON SURFEDGES;
            public X_LumpON MODELS;

            public object Read(BinaryReader r, string id, bool h1)
            {
                if (id == "HL:BS") (ENTITIES, PLANES) = (PLANES, ENTITIES);
                if (Version != (h1 ? 30 : 29)) throw new FormatException("BAD VERSION");
                var fileLength = (int)r.BaseStream.Length;
                var dmodels = CopyLump<X_DModel_H1>(r, fileLength, MODELS, null, sizeof(X_DModel_H1), h1 ? 400 : 256);
                var dvertexes = CopyLump<Vector3>(r, fileLength, VERTEXES, "3f", sizeof(Vector3), 65535);
                var dplanes = CopyLump<X_DPlane_H1S2>(r, fileLength, PLANES, null, sizeof(X_DPlane_H1S2), h1 ? 32767 : 8192);
                var dleafs = CopyLump<X_DLeaf_H1>(r, fileLength, LEAFS, null, sizeof(X_DLeaf_H1), h1 ? 8192 : 32767);
                var dnodes = CopyLump<X_DNode_H1S2>(r, fileLength, NODES, null, sizeof(X_DNode_H1S2), 32767);
                var texinfo = CopyLump<X_Texinfo_H1>(r, fileLength, TEXINFO, null, sizeof(X_Texinfo_H1), h1 ? 8192 : 4096);
                var dclipnodes = CopyLump<X_DClipNode_H1>(r, fileLength, CLIPNODES, null, sizeof(X_DClipNode_H1), 32767);
                var dfaces = CopyLump<X_DFace_H12>(r, fileLength, FACES, null, sizeof(X_DFace_H12), 65535);
                var dmarksurfaces = CopyLump<ushort>(r, fileLength, MARKSURFACES, "H", sizeof(ushort), 65535);
                var dsurfedges = CopyLump<int>(r, fileLength, SURFEDGES, "i", sizeof(int), 512000);
                var dedges = CopyLump<X_DEdge_H1S2>(r, fileLength, EDGES, null, sizeof(X_DEdge_H1S2), 256000);
                var dtexdata = CopyLump<byte>(r, fileLength, TEXTURES, null, 1, 0x200000);
                var dvisdata = CopyLump<byte>(r, fileLength, VISIBILITY, null, 1, h1 ? 0x200000 : 0x100000);
                var dlightdata = CopyLump<byte>(r, fileLength, LIGHTING, null, 1, h1 ? 0x200000 : 0x100000);
                var dentdata = Encoding.ASCII.GetString(CopyLump<byte>(r, fileLength, ENTITIES, null, 1, h1 ? 131072 : 65536));
                return null;
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DHeader_S2
        {
            public static (string, int) Struct = ("<42i", sizeof(X_DHeader_S2));
            public int Magic, Version;
            public X_LumpON ENTITIES;
            public X_LumpON PLANES;
            public X_LumpON VERTEXES;
            public X_LumpON TEXTURES;
            public X_LumpON VISIBILITY;
            public X_LumpON NODES;
            public X_LumpON TEXINFO;
            public X_LumpON FACES;
            public X_LumpON LIGHTING;
            public X_LumpON LEAFS;
            public X_LumpON LEAFFACES;
            public X_LumpON LEAFBRUSHES;
            public X_LumpON EDGES;
            public X_LumpON SURFEDGES;
            public X_LumpON MODELS;
            public X_LumpON BRUSHES;
            public X_LumpON BRUSHSIDES;
            public X_LumpON POP;
            public X_LumpON AREAS;
            public X_LumpON AREAPORTALS;
            public X_LumpON LIGHTINFO_S;

            public object Read(BinaryReader r, string id, bool sin)
            {
                if (Magic != (sin ? 0x0 : 0x0)) throw new FormatException("BAD MAGIC");
                if (Version != (sin ? 30 : 38)) throw new FormatException("BAD VERSION");
                var fileLength = (int)r.BaseStream.Length;
                var dmodels = CopyLump<X_DModel_H1>(r, fileLength, MODELS, null, sizeof(X_DModel_S2), 1024);
                var dvertexes = CopyLump<Vector3>(r, fileLength, VERTEXES, "3f", sizeof(Vector3), 65535);
                var dplanes = CopyLump<X_DPlane_H1S2>(r, fileLength, PLANES, null, sizeof(X_DPlane_H1S2), 65535);
                var dleafs = CopyLump<X_DLeaf_S2>(r, fileLength, LEAFS, null, sizeof(X_DLeaf_S2), 65535);
                var dnodes = CopyLump<X_DNode_H1S2>(r, fileLength, NODES, null, sizeof(X_DNode_H1S2), 65535);
                var texinfo = sin ? CopyLump<X_Texinfo_S>(r, fileLength, TEXINFO, null, sizeof(X_Texinfo_S), 8192) : (object)CopyLump<X_Texinfo_2>(r, fileLength, TEXINFO, null, sizeof(X_Texinfo_2), 8192);
                var dfaces = sin ? CopyLump<X_DFace_S>(r, fileLength, FACES, null, sizeof(X_DFace_S), 65535) : (object)CopyLump<X_DFace_H12>(r, fileLength, FACES, null, sizeof(X_DFace_H12), 65535);
                var dleaffaces = CopyLump<ushort>(r, fileLength, LEAFFACES, "H", sizeof(ushort), 65535);
                var dleafbrushes = CopyLump<ushort>(r, fileLength, LEAFBRUSHES, "H", sizeof(ushort), 65535);
                var dsurfedges = CopyLump<int>(r, fileLength, SURFEDGES, "i", sizeof(int), 256000);
                var dedges = CopyLump<X_DEdge_H1S2>(r, fileLength, EDGES, null, sizeof(X_DEdge_H1S2), 128000);
                var dbrushes = CopyLump<X_DBrush_S23>(r, fileLength, BRUSHES, null, sizeof(X_DBrush_S23), 8192);
                var dbrushsides = sin ? CopyLump<X_DBrushside_S>(r, fileLength, BRUSHSIDES, null, sizeof(X_DBrushside_S), 65536) : (object)CopyLump<X_DBrushside_2>(r, fileLength, BRUSHSIDES, null, sizeof(X_DBrushside_2), 65536);
                var dareas = CopyLump<X_DArea_S2>(r, fileLength, AREAS, null, sizeof(X_DArea_S2), 256);
                var dareaportals = CopyLump<X_DAreaportal_S2>(r, fileLength, AREAPORTALS, null, sizeof(X_DAreaportal_S2), 1024);
                var lightinfo = sin ? CopyLump<X_Lightinfo_S>(r, fileLength, LIGHTINFO_S, null, sizeof(X_Lightinfo_S), 1024) : default;
                var dvisdata = CopyLump<byte>(r, fileLength, VISIBILITY, null, 1, 0x280000);
                var dlightdata = CopyLump<byte>(r, fileLength, LIGHTING, null, 1, sin ? 0x300000 : 0x320000);
                var dentdata = Encoding.ASCII.GetString(CopyLump<byte>(r, fileLength, ENTITIES, null, 1, 0x40000));
                return null;
            }
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DHeader_3
        {
            public static (string, int) Struct = ("<36i", sizeof(X_DHeader_3));
            public int Magic, Version;
            public X_LumpON ENTITIES;
            public X_LumpON SHADERS;
            public X_LumpON PLANES;
            public X_LumpON NODES;
            public X_LumpON LEAFS;
            public X_LumpON LEAFSURFACES;
            public X_LumpON LEAFBRUSHES;
            public X_LumpON MODELS;
            public X_LumpON BRUSHES;
            public X_LumpON BRUSHSIDES;
            public X_LumpON DRAWVERTS;
            public X_LumpON DRAWINDEXES;
            public X_LumpON FOGS;
            public X_LumpON SURFACES;
            public X_LumpON LIGHTMAPS;
            public X_LumpON LIGHTGRID;
            public X_LumpON VISIBILITY;

            public object Read(BinaryReader r, string id)
            {
                if (Magic != 0x0) throw new FormatException("BAD MAGIC");
                if (Version != 46) throw new FormatException("BAD VERSION");
                var fileLength = (int)r.BaseStream.Length;
                var dshaders = CopyLump<X_DShader_3>(r, fileLength, SHADERS, null, sizeof(X_DShader_3), 0x400);
                var dmodels = CopyLump<X_DModel_3>(r, fileLength, MODELS, null, sizeof(X_DModel_3), 0x400);
                var dplanes = CopyLump<X_DPlane_3>(r, fileLength, PLANES, null, sizeof(X_DPlane_3), 0x20000);
                var dleafs = CopyLump<X_DLeaf_3>(r, fileLength, LEAFS, null, sizeof(X_DLeaf_3), 0x20000);
                var dnodes = CopyLump<X_DNode_3>(r, fileLength, NODES, null, sizeof(X_DNode_3), 0x20000);
                var dleafsurfaces = CopyLump<int>(r, fileLength, LEAFSURFACES, "i", sizeof(int), 0x20000);
                var dleafbrushes = CopyLump<int>(r, fileLength, LEAFBRUSHES, "i", sizeof(int), 0x40000);
                var dbrushes = CopyLump<X_DBrush_S23>(r, fileLength, BRUSHES, null, sizeof(X_DBrush_S23), 0x8000);
                var dbrushsides = CopyLump<X_DBrushside_3>(r, fileLength, BRUSHSIDES, null, sizeof(X_DBrushside_3), 0x20000);
                var drawVerts = CopyLump<X_DrawVert_3>(r, fileLength, DRAWVERTS, null, sizeof(X_DrawVert_3), 0x80000);
                var drawSurfaces = CopyLump<X_DSurface_3>(r, fileLength, SURFACES, null, sizeof(X_DSurface_3), 0x20000);
                var dfogs = CopyLump<X_DFog_3>(r, fileLength, FOGS, null, sizeof(X_DFog_3), 0x100);
                var drawIndexes = CopyLump<int>(r, fileLength, DRAWINDEXES, "i", sizeof(int), 0x80000);
                var dvisData = CopyLump<byte>(r, fileLength, VISIBILITY, null, 1, 0x200000);
                var dlightData = CopyLump<byte>(r, fileLength, LIGHTMAPS, null, 1, 0x800000);
                var dentData = Encoding.ASCII.GetString(CopyLump<byte>(r, fileLength, ENTITIES, null, 1, 0x10000));
                var gridData = CopyLump<byte>(r, fileLength, LIGHTGRID, null, 1, 0x800000);
                return null;
            }
        }
        #endregion

        #region DModel
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DModel_H1
        {
            public static (string, int) Struct = ("<9f7i", sizeof(X_DModel_H1));
            public Vector3 Mins, Maxs;
            public Vector3 Origin;
            public fixed int HeadNodes[4];
            public int Visleafs; // not including the solid leaf 0
            public int FirstFace, NumFaces;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DModel_S2
        {
            public static (string, int) Struct = ("<9f3i", sizeof(X_DModel_S2));
            public Vector3 Mins, Maxs;
            public Vector3 Origin;
            public int HeadNode;
            public int FirstFace, NumFaces;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DModel_3
        {
            public static (string, int) Struct = ("<6f4i", sizeof(X_DModel_3));
            public Vector3 Mins, Maxs;
            public int FirstSurface, NumSurfaces;
            public int FirstBrush, NumBrushes;
        }
        #endregion

        #region DMiptex
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DMiptexLump_H1
        {
            public static (string, int) Struct = ("<5i", sizeof(X_DMiptexLump_H1));
            public int NumMiptex;
            public fixed int Dataofs[4]; // [nummiptex]
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Miptex_H1
        {
            public static (string, int) Struct = ("<16s6I", sizeof(X_Miptex_H1));
            public fixed byte Name[16];
            public uint Width, Height;
            public fixed uint Offsets[4]; // four mip maps stored
        }
        #endregion

        #region DPlane
        internal enum PLANE : int
        {
            // 0-2 are axial planes
            X = 0,
            Y = 1,
            Z = 2,
            // 3-5 are non-axial planes snapped to the nearest
            ANYX = 3,
            ANYY = 4,
            ANYZ = 5,
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DPlane_H1S2
        {
            public static (string, int) Struct = ("<4fi", sizeof(X_DPlane_H1S2));
            public Vector3 Normal;
            public float Dist;
            public int Type; // PLANE_X - PLANE_ANYZ
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DPlane_3
        {
            public static (string, int) Struct = ("<4f", sizeof(X_DPlane_3));
            public Vector3 Normal;
            public float Dist;
        }
        #endregion

        #region DNode
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DNode_H1S2
        {
            public static (string, int) Struct = ("<i8h2H", sizeof(X_DNode_H1S2));
            public int PlaneNum;
            public (short l, short r) Children; // negative numbers are -(leafs+1), not nodes
            public Vector3<short> Mins, Maxs; // for frustom culling
            public ushort FirstFace, NumFaces; // counting both sides
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DNode_3
        {
            public static (string, int) Struct = ("<9i", sizeof(X_DNode_3));
            public int PlaneNum;
            public (int l, int r) Children; // negative numbers are -(leafs+1), not nodes
            public Vector3<int> Mins, Maxs; // for frustom culling
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DClipNode_H1
        {
            public static (string, int) Struct = ("<I2h", sizeof(X_DClipNode_H1));
            public int PlaneNum;
            public (short l, short r) Children;// negative numbers are contents
        }
        #endregion

        #region Texinfo
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Texinfo_H1
        {
            public static (string, int) Struct = ("<8f2i", sizeof(X_Texinfo_H1));
            public Vector4 Vecs0, Vecs1; // [s/t][xyz offset]
            public int Miptex;
            public int Flags;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Lightinfo_S
        {
            public static (string, int) Struct = ("<i6fs32", sizeof(X_Lightinfo_S));
            public int Value; // light emission, etc
            public Vector3 Color;
            public float Direct;
            public float Directangle;
            public float Directstyle;
            public fixed byte DirectstyleName[32];
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Texinfo_S
        {
            public static (string, int) Struct = ("<8f2i", sizeof(X_Texinfo_S));
            public Vector4 Vecs0, Vecs1; // [s/t][xyz offset]
            public int Flags; // miptex flags + overrides
            public fixed byte Texture[64]; // texture name (textures/*.wal)
            public int NextTexinfo; // for animations, -1 = end of chain
            public float TransMag;
            public int TransAngle;
            public int BaseAngle;
            public float Animtime;
            public float Nonlit;
            public float Translucence;
            public float Friction;
            public float Restitution;
            public Vector3 Color;
            public fixed byte GroupName[32];

        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Texinfo_2
        {
            public static (string, int) Struct = ("<8f2i", sizeof(X_Texinfo_2));
            public Vector4 Vecs0, Vecs1; // [s/t][xyz offset]
            public int Flags; // miptex flags + overrides
            public int Value; // light emission, etc
            public fixed byte Texture[32]; // texture name (textures/*.wal)
            public int NextTexinfo; // for animations, -1 = end of chain
        }
        #endregion

        #region DEdge
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DEdge_H1S2
        {
            public static (string, int) Struct = ("<2H", sizeof(X_DEdge_H1S2));
            public Vector2<ushort> V; // vertex numbers
        }
        #endregion

        #region DSurface
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DFace_H12
        {
            public static (string, int) Struct = ("<Hhi2h4Bi", sizeof(X_DFace_H12));
            public ushort PlaneNum;
            public short Side;
            public int FirstEdge; public short NumEdges; // we must support > 64k edges
            public short Texinfo;
            // lighting info
            public fixed byte Styles[4];
            public int LightOfs; // start of [numstyles*surfsize] samples
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DFace_S
        {
            public static (string, int) Struct = ("<Hhi2h4B2i", sizeof(X_DFace_S));
            public ushort PlaneNum;
            public short Side;
            public int FirstEdge; public short NumEdges; // we must support > 64k edges
            public short Texinfo;
            // lighting info
            public fixed byte Styles[4];
            public int LightOfs; // start of [numstyles*surfsize] samples
            public int LightInfo;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DSurface_3
        {
            public static (string, int) Struct = ("<12i6f2i", sizeof(X_DSurface_3));
            public int ShaderNum;
            public int FogNum;
            public int SurfaceType;
            public int FirstVert, NumVerts;
            public int FirstIndex, NumIndexes;
            public int LightmapNum;
            public int LightmapX, LightmapY;
            public int LightmapWidth, LightmapHeight;
            public Vector3 LightmapOrigin;
            public Vector3 LightmapVecs0, LightmapVecs1, LightmapVecs2; // for patches, [0] and [1] are lodbounds
            public int PatchWidth, PatchHeight;
        }
        #endregion

        #region DLeaf
        internal enum AMBIENT_H1 : byte
        {
            WATER = 0,
            SKY = 1,
            SLIME = 2,
            LAVA = 3,
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DLeaf_H1
        {
            public static (string, int) Struct = ("<2i6h2H4B", sizeof(X_DLeaf_H1));
            public int Contents;
            public int VisOfs; // -1 = no visibility info
            public Vector3<short> Mins, Maxs; // for frustum culling
            public ushort FirstMarkSurface, NumMarkSurface;
            public fixed byte AmbientLevel[4]; // automatic ambient sounds
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DLeaf_S2
        {
            public static (string, int) Struct = ("<i8h8H", sizeof(X_DLeaf_S2));
            public int Contents; // -1 = no visibility info
            public short Cluster;
            public short Area;
            public Vector3<short> Mins, Maxs; // for frustum culling
            public ushort FirstLeafFace, NumLeafFaces;
            public ushort FirstLeafBrush, NumLeafBrushes;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DLeaf_3
        {
            public static (string, int) Struct = ("<12i", sizeof(X_DLeaf_3));
            public int Cluster;
            public int Area;
            public Vector3<int> Mins, Maxs; // for frustum culling
            public int FirstLeafSurface, NumLeafSurfaces;
            public int FirstLeafBrush, NumLeafBrushes;
        }
        #endregion

        #region DBrush
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DBrushside_2
        {
            public static (string, int) Struct = ("<Hh", sizeof(X_DBrushside_2));
            public ushort PlaneNum; // facing out of the leaf
            public short Texinfo;
        }
        internal struct X_DBrushside_S
        {
            public static (string, int) Struct = ("<Hhi", sizeof(X_DBrushside_S));
            public ushort PlaneNum; // facing out of the leaf
            public short Texinfo;
            public int Lightinfo;
        }
        internal struct X_DBrushside_3
        {
            public static (string, int) Struct = ("<2i", sizeof(X_DBrushside_3));
            public int PlaneNum; // positive plane side faces out of the leaf
            public int ShaderNum;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DBrush_S23
        {
            public static (string, int) Struct = ("<3i", sizeof(X_DBrush_S23));
            public int FirstSide, NumSides;
            public int Contents_ShaderNum; // the shader that determines the contents flags
        }
        #endregion

        #region DShader
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DShader_3
        {
            public static (string, int) Struct = ("<64s2i", sizeof(X_DShader_3));
            public fixed byte Shader[64];
            public int SurfaceFlags, ContentFlags;
        }
        #endregion

        #region DArea
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DAreaportal_S2
        {
            public static (string, int) Struct = ("<2i", sizeof(X_DAreaportal_S2));
            public int PortalNum, OtherArea;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DArea_S2
        {
            public static (string, int) Struct = ("<2i", sizeof(X_DAreaportal_S2));
            public int NumAreaportals, FirstAreaportal;
        }
        #endregion

        #region DFog
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DFog_3
        {
            public static (string, int) Struct = ("<64s3i", sizeof(X_DFog_3));
            public fixed byte Shader[64];
            public int BrushNum;
            public int VisibleSide; // the brush side that ray tests need to clip against (-1 == none)
        }
        #endregion

        #region DrawVert
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DrawVert_3
        {
            public static (string, int) Struct = ("<3i", sizeof(X_DBrush_S23));
            public int FirstSide, NumSides;
            public int Contents_ShaderNum;
        }
        #endregion

        #region CONTENTS
        internal enum CONTENTS_H1 : int
        {
            EMPTY = -1,
            SOLID = -2,
            WATER = -3,
            SLIME = -4,
            LAVA = -5,
            SKY = -6,
            // h1
            ORIGIN = -7,		// removed at csg time
            CLIP = -8,      // changed to contents_solid
            CURRENT_0 = -9,
            CURRENT_90 = -10,
            CURRENT_180 = -11,
            CURRENT_270 = -12,
            CURRENT_UP = -13,
            CURRENT_DOWN = -14,
            TRANSLUCENT = -15,
        }
        [Flags]
        internal enum CONTENTS_S2 : int
        {
            SOLID = 1, // an eye is never valid in a solid
            WINDOW = 2,
            AUX = 4,
            LAVA = 8,
            SLIME = 16,
            WATER = 32,
            MIST = 64, LAST_VISIBLE = 64,
            // remaining contents are non-visible, and don't eat brushes
            AREAPORTAL = 0x8000,
            PLAYERCLIP = 0x10000,
            MONSTERCLIP = 0x20000,
            // currents can be added to any other contents, and may be mixed
            CURRENT_0 = 0x40000,
            CURRENT_90 = 0x80000,
            CURRENT_180 = 0x100000,
            CURRENT_270 = 0x200000,
            CURRENT_UP = 0x400000,
            CURRENT_DOWN = 0x800000,
            ORIGIN = 0x1000000, // removed before bsping an entity
            MONSTER = 0x2000000, // should never be on a brush, only in game
            DEADMONSTER = 0x4000000,
            DETAIL = 0x8000000, // brushes to be added after vis leafs
            // renamed because it's in conflict with the Q3A translucent contents
            Q2TRANSLUCENT = 0x10000000, // auto set if any surface has trans
            LADDER = 0x20000000,
        }
        #endregion

        #endregion

        object h1;
        object s2;
        object x3;

        public Binary_Bsp(BinaryReader r, PakFile s)
        {
            var flag = false;
            var gameId = s.Game.Id; var engine = s.Game.Engine;
            h1 = engine.v == "2" || (flag = engine.n == "GoldSrc") ? r.ReadS<X_DHeader_H1>().Read(r, gameId, flag) : default;
            s2 = engine.v == "3" || (flag = gameId == "Sin") ? r.ReadS<X_DHeader_S2>().Read(r, gameId, flag) : default;
            x3 = engine.v == "3" ? r.ReadS<X_DHeader_3>().Read(r, gameId) : default;
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => [
                new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = this }),
                new("BSP", items: [
                    //new($"Planes: {Planes.Length}"),
                ])
            ];
    }

    #endregion

    #region Binary_Lmp

    public unsafe class Binary_Lmp : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Lmp(r, f, s));

        public static Binary_Lmp Palette;
        public static Binary_Lmp Colormap;
        public byte[][] PaletteRecords;
        public byte[][] ColormapRecords;
        public static byte[] ToLightPixel(int light, int pixel) => Palette.PaletteRecords[Colormap.ColormapRecords[(light >> 3) & 0x1F][pixel]];
        byte[] Pixels;

        // file: PAK0.PAK:gfx/bigbox.lmp
        public Binary_Lmp(BinaryReader r, FileSource f, PakFile s)
        {
            switch (Path.GetFileNameWithoutExtension(f.Path))
            {
                case "palette":
                    PaletteRecords = r.ReadFArray(s => s.ReadBytes(3).Concat(new byte[] { 0 }).ToArray(), 256);
                    Palette = this;
                    return;
                case "colormap":
                    ColormapRecords = r.ReadFArray(s => s.ReadBytes(256), 32);
                    Colormap = this;
                    return;
                default:
                    s.Game.Ensure();
                    var palette = Palette?.PaletteRecords ?? throw new NotImplementedException();
                    var width = Width = r.ReadInt32();
                    var height = Height = r.ReadInt32();
                    Pixels = r.ReadBytes(width * height).SelectMany(x => ToLightPixel(32 << 3, x)).ToArray();
                    return;
            }
        }

        #region ITexture
        static readonly object Format = (TextureFormat.BGRA32, TexturePixel.Unknown);
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags TexFlags { get; } = 0;

        public (byte[] bytes, object format, Range[] spans) Begin(string platform) => (Pixels, Format, null);
        public void End() { }
        #endregion

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "TextureName", Name = Path.GetFileName(file.Path), Value = this }),
            new("TextureName", items: [
                new($"Width: {Width}"),
                new($"Height: {Height}"),
            ])
        ];
    }

    #endregion

    #region Binary_Mdl
    // https://icculus.org/homepages/phaethon/q3a/formats/md2-schoenblum.html#:~:text=Quake2%20models%20are%20stored%20in,md2%20extension.

    public unsafe class Binary_Mdl : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Mdl(r));

        #region Headers

        #endregion

        // file: xxxx.mdl
        public Binary_Mdl(BinaryReader r)
        {
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "Model File" }),
            new("Model", items: [
                new($"Records: {1}"),
            ])
        ];
    }

    #endregion

    #region Binary_Spr
    // https://github.com/yuraj11/HL-Texture-Tools

    public unsafe class Binary_Spr : ITextureFrames, IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Spr(r, f));

        #region Headers

        const uint X_MAGIC = 0x50534449; //: IDSP

        /// <summary>
        /// Type of sprite.
        /// </summary>
        public enum Type : int
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
        public enum TextFormat : int
        {
            SPR_NORMAL,
            SPR_ADDITIVE,
            SPR_INDEXALPHA,
            SPR_ALPHTEST
        }

        /// <summary>
        /// Synch. type of sprite.
        /// </summary>
        public enum SynchType : int
        {
            Synchronized,
            Random
        }

        [StructLayout(LayoutKind.Sequential)]
        struct X_Header
        {
            public static (string, int) Struct = ("<I3if3ifi", sizeof(X_Header));
            public uint Magic;
            public int Version;
            public Type Type;
            public TextFormat TextFormat;
            public float BoundingRadius;
            public int MaxWidth;
            public int MaxHeight;
            public int NumFrames;
            public float BeamLen;
            public SynchType SynchType;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct X_Frame
        {
            public static (string, int) Struct = ("<5i", sizeof(X_Frame));
            public int Group;
            public int OriginX;
            public int OriginY;
            public int Width;
            public int Height;
        }

        #endregion

        int width;
        int height;
        X_Frame[] frames;
        byte[][] pixels;
        byte[] palette;
        int frame;
        byte[] bytes;

        public Binary_Spr(BinaryReader r, FileSource f)
        {
            // read file
            var header = r.ReadS<X_Header>();
            if (header.Magic != X_MAGIC) throw new FormatException("BAD MAGIC");

            // load palette
            palette = r.ReadBytes(r.ReadUInt16() * 3);

            // load frames
            frames = new X_Frame[header.NumFrames];
            pixels = new byte[header.NumFrames][];
            for (var i = 0; i < header.NumFrames; i++)
            {
                frames[i] = r.ReadS<X_Frame>();
                ref X_Frame frame = ref frames[i];
                pixels[i] = r.ReadBytes(frame.Width * frame.Height);
            }
            width = frames[0].Width;
            height = frames[0].Height;
            bytes = new byte[width * height << 2];
        }

        #region ITexture

        static readonly object Format = (TextureFormat.RGBA32, TexturePixel.Unknown);
        public int Width => width;
        public int Height => height;
        public int Depth => 0;
        public int MipMaps => 1;
        public TextureFlags TexFlags => 0;
        public int Fps { get; } = 60;

        public (byte[] bytes, object format, Range[] spans) Begin(string platform) => (bytes, Format, null);
        public void End() { }

        public bool HasFrames => frame < frames.Length;

        public bool DecodeFrame()
        {
            var p = pixels[frame];
            Rasterize.CopyPixelsByPalette(bytes, 4, p, palette, 3);
            frame++;
            return true;
        }

        #endregion

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
}

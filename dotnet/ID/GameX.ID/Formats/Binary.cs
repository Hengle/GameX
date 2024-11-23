using GameX.Platforms;
using Google.Protobuf.WellKnownTypes;
using OpenStack.Gfx.Textures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static GameX.Formats.Unknown.IUnknownTexture;
using static System.IO.Polyfill;

// https://www.gamers.org/dEngine/quake/spec/quake-spec34/qkspec_3.htm
namespace GameX.ID.Formats
{
    #region Binary_Bsp30
    // https://developer.valvesoftware.com/wiki/BSP_(Quake)
    // https://www.flipcode.com/archives/Quake_2_BSP_File_Format.shtml
    // https://www.gamers.org/dEngine/quake/spec/quake-spec34/qkspec_4.htm
    // https://www.mralligator.com/q3/
    // https://github.com/demoth/jake2/blob/main/info/BSP.md

    public unsafe class Binary_Bsp : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Bsp(r, s));

        #region Headers

        #region Vertex
        public enum Side
        {
            FRONT = 0,
            BACK = 1,
            ON = 2,
        }
        #endregion

        #region Plane
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Plane12 //was:dplane_t
        {
            public static (string, int) Struct = ("<4fi", sizeof(X_Plane12));
            public Vector3 Normal;
            public float Dist;
            public int Type; // PLANE_X - PLANE_ANYZ
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Plane3 //was:dplane_t
        {
            public static (string, int) Struct = ("<4f", sizeof(X_Plane3));
            public Vector3 Normal;
            public float Dist;
        }
        public class Plane
        {
            internal static Plane Create(X_Plane12 s) => new() { Normal = s.Normal, Dist = s.Dist, Type = (byte)s.Type, Signbits = ToBits(ref s.Normal) };
            internal static Plane Create(X_Plane3 s) => new() { Normal = s.Normal, Dist = s.Dist, Signbits = ToBits(ref s.Normal) };
            public Vector3 Normal;
            public float Dist;
            public byte Type; // for texture axis selection and fast side tests
            public byte Signbits; //calc: signx + signy<<1 + signz<<1
            public byte _0, _1;
            static byte ToBits(ref Vector3 s) => (byte)((s.X < 0 ? 1 << 0 : 0) | (s.Y < 0 ? 1 << 1 : 0) | (s.Z < 0 ? 1 << 2 : 0));
        }
        #endregion

        #region Texture
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Texture1 //was:miptex_t
        {
            public static (string, int) Struct = ("<16s6I", sizeof(X_Texture1)); //: 40
            public fixed byte Name[16];
            public uint Width, Height;
            public fixed uint Offsets[4]; // four mip maps stored
        }
        public class Texture
        {
            const int OFFSETX = 72 - 40;
            internal static Texture Create(BinaryReader r, X_Texture1 s) => new() { Name = UnsafeX.FixedAStringScan(s.Name, 16), Width = (int)s.Width, Height = (int)s.Height, Offsets = [s.Offsets[0] + OFFSETX, s.Offsets[1] + OFFSETX, s.Offsets[2] + OFFSETX, s.Offsets[3] + OFFSETX], Pixels = r.ReadBytes((int)s.Width * (int)s.Height / 64 * 85) };
            public string Name;
            public int Width, Height;
            public int AnimTotal; // total tenths in sequence (0 = no)
            public int AnimMin, AnimMax; // time for this frame min <= time<max
            public Texture AnimNext; // in the animation sequence
            public Texture AlternateAnims; // bmodels in frame 1 use these
            public uint[] Offsets; // four mip maps stored
            public byte[] Pixels;
        }
        #endregion

        #region Edge
        public struct Edge
        {
            internal static Edge Create(Vector2<ushort> s) => new() { V = s };
            public Vector2<ushort> V;
            //public int CachedEdgeOffset;
        }
        #endregion

        #region Texinfo
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Texinfo1 //was:texinfo_t
        {
            public static (string, int) Struct = ("<8f2i", sizeof(X_Texinfo1));
            public Vector4 Vec0, Vec1; // [s/t][xyz offset]
            public int Miptex;
            public int Flags;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Texinfo2 //was:texinfo_t
        {
            public static (string, int) Struct = ("<8f2i", sizeof(X_Texinfo1));
            public Vector4 Vec0, Vec1; // [s/t][xyz offset]
            public int Flags; // miptex flags + overrides
            public int Value; // light emission, etc
            public fixed byte Texture[32]; // texture name (textures/*.wal)
            public int NextTexinfo; // for animations, -1 = end of chain
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Texinfo3
        {
            public static (string, int) Struct = ("<?", sizeof(X_Texinfo3));
            public Vector3 VectorS;
            public float DistS;
            public Vector3 VectorT;
            public float DistT;
            public uint TextureId;
            public uint Animated;
        }
        public class Texinfo
        {
            internal static Texinfo Create(Binary_Bsp p, X_Texinfo1 s) => new Texinfo() { Vecs = [s.Vec0, s.Vec1], Flags = s.Flags, Mipadjust = ToMipadjust(ref s.Vec0, ref s.Vec1) }.Set(p, s.Miptex);
            internal static Texinfo Create(Binary_Bsp p, X_Texinfo2 s) => new() { Vecs = [s.Vec0, s.Vec1], Flags = s.Flags, Value = s.Value, TextureName = UnsafeX.FixedAString(s.Texture, 32), NextTexinfo = s.NextTexinfo };
            public Vector4[] Vecs;
            public int Flags;
            // q2
            public int Value;
            public string TextureName;
            public int NextTexinfo;
            // calc
            public int Mipadjust;
            public Texture Texture;
            static int ToMipadjust(ref Vector4 s0, ref Vector4 s1)
            {
                var len1 = s0.Length();
                var len2 = s1.Length();
                len1 = (len1 + len2) / 2;
                if (len1 < 0.32) return 4;
                else if (len1 < 0.49) return 3;
                else if (len1 < 0.99) return 2;
                else return 1;
            }
            Texinfo Set(Binary_Bsp p, int miptex)
            {
                var texs = p.Textures;
                Texture = texs == null ? null : miptex < texs.Length ? texs[miptex] : throw new FormatException("miptex >= numtextures");
                if (Texture == null) Flags = 0;
                return this;
            }
        }
        #endregion

        #region Surface
        [Flags]
        public enum SurfaceFlags
        {
            PLANEBACK = 2,
            DRAWSKY = 4,
            DRAWSPRITE = 8,
            DRAWTURB = 0x10,
            DRAWTILED = 0x20,
            DRAWBACKGROUND = 0x40,
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Surface12 //was:dface_t
        {
            public static (string, int) Struct = ("<Hhi2h4Bi", sizeof(X_Surface12));
            public ushort PlaneNum;
            public ushort Side;
            public int FirstEdge; public short NumEdges; // we must support > 64k edges
            public short Texinfo;
            public fixed byte Styles[4];
            public int LightOffset; // start of [numstyles*surfsize] samples
        }
        [StructLayout(LayoutKind.Sequential)]
        struct X_Surface3__
        {
            public ushort PlaneId;
            public ushort Side;
            public int LedgeId;
            public ushort LedgeNum;
            public ushort TexinfoId;
            public byte TypeLight;
            public byte BaseLight;
            public fixed byte Light[2];
            public int LightMap;
        }
        public class Surface
        {
            internal static Surface Create(Binary_Bsp p, X_Surface12 s) => new Surface() { FirstEdge = s.FirstEdge, NumEdges = s.NumEdges, Plane = p.Planes[s.PlaneNum], Texinfo = p.Texinfos[s.Texinfo], Styles = [s.Styles[0], s.Styles[1], s.Styles[2], s.Styles[3]], Samples = s.LightOffset != -1 ? p.Lightmaps.AsMemory(s.LightOffset) : null }.Set(p, s.Side);
            internal static Surface Get(Binary_Bsp p, short s) => s < p.Surfaces.Length ? p.Surfaces[s] : throw new FormatException("MarkSurfaces: bad surface number");
            public int VisFrame; // should be drawn when node is crossed
            public int DlightFrame, DlightBits;
            public Plane Plane;
            public SurfaceFlags Flags;
            public int FirstEdge, NumEdges; // look up in model->surfedges[] (negative numbers), are backwards edges
            // surface generation data
            //surfcache_s* cachespots[MIPLEVELS];
            public short[] TextureMins = new short[2];
            public short[] Extents = new short[2];
            public Texinfo Texinfo;
            // lighting info
            public int[] Styles;
            public Memory<byte> Samples; // [numstyles*surfsize]
            Surface Set(Binary_Bsp p, ushort side)
            {
                if (side != 0) Flags |= SurfaceFlags.PLANEBACK;
                if (Texinfo.Texture.Name.StartsWith("sky")) Flags |= SurfaceFlags.DRAWSKY | SurfaceFlags.DRAWTILED; // sky
                else if (Texinfo.Texture.Name.StartsWith("*"))
                {
                    Flags |= SurfaceFlags.DRAWTURB | SurfaceFlags.DRAWTILED; // turbulent
                    Extents[0] = Extents[1] = 16384;
                    TextureMins[0] = TextureMins[1] = -8192;
                }
                CalcSurfaceExtents(p);
                return this;
            }

            void CalcSurfaceExtents(Binary_Bsp p)
            {
                float val; var mins = stackalloc float[2]; var maxs = stackalloc float[2];
                int i, j, e; var bmins = stackalloc int[2]; var bmaxs = stackalloc int[2];
                mins[0] = mins[1] = 999999;
                maxs[0] = maxs[1] = -99999;
                var tex = Texinfo;
                for (i = 0; i < NumEdges; i++)
                {
                    e = p.SurfaceEdges[FirstEdge + i];
                    ref Vector3 v = ref p.Vertices[p.Edges[e >= 0 ? e : -e].V[0]];
                    for (j = 0; j < 2; j++)
                    {
                        val = v.X * tex.Vecs[j].X + v.Y * tex.Vecs[j].Y + v.Z * tex.Vecs[j].Z + tex.Vecs[j].W;
                        if (val < mins[j]) mins[j] = val;
                        if (val > maxs[j]) maxs[j] = val;
                    }
                }
                for (i = 0; i < 2; i++)
                {
                    bmins[i] = (int)Math.Floor(mins[i] / 16);
                    bmaxs[i] = (int)Math.Ceiling(maxs[i] / 16);
                    TextureMins[i] = (short)(bmins[i] * 16);
                    Extents[i] = (short)((bmaxs[i] - bmins[i]) * 16);
                    if ((tex.Flags & 1) == 0 && Extents[i] > 256) throw new FormatException("Bad surface extents");
                }
            }
        }
        #endregion

        #region Node
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Node12 //was:dnode_t
        {
            public static (string, int) Struct = ("<i28h2H", sizeof(X_Plane12));
            public int PlaneNum;
            public fixed short Children[2]; // negative numbers are -(leafs+1), not nodes
            public Vector3<short> Min, Max; // for frustom culling
            public ushort FirstSurface, NumSurfaces; // counting both sides
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Node3 //was:dnode_t
        {
            public static (string, int) Struct = ("<9i", sizeof(X_Node3));
            public int PlaneNum;
            public fixed int Children[2]; // negative numbers are -(leafs+1), not nodes
            public Vector3<int> Min, Max; // for frustom culling
        }

        public class Node
        {
            public int Contents; // 0, to differentiate from leafs
            public int VisFrame; // node needs to be traversed if current
            public Vector3<int> Min, Max; // for bounding box culling
            public Node Parent;
            internal static void Bind(Binary_Bsp p)
            {
                foreach (var s in p.Nodes) { s.Children = (ToNode(p, s.ChildrenId.l), ToNode(p, s.ChildrenId.r)); }
                SetParent(p.Nodes[0], null);
            }
            static Node ToNode(Binary_Bsp p, int id) => id >= 0 ? p.Nodes[id] : p.Leafs[-1 - id];
            static void SetParent(Node node, Node parent)
            {
                return;
                node.Parent = parent;
                if (node.Contents < 0) return;
                var nodex = (NodeX)node;
                SetParent(nodex.Children.l, node);
                SetParent(nodex.Children.r, node);
            }
        }

        public class NodeX : Node
        {
            internal static NodeX Create(Binary_Bsp p, X_Node12 s) => new()
            {
                Min = new Vector3<int>(s.Min.X, s.Min.Y, s.Min.Z),
                Max = new Vector3<int>(s.Max.X, s.Max.Y, s.Max.Z),
                Plane = p.Planes[s.PlaneNum],
                FirstSurface = s.FirstSurface,
                NumSurfaces = s.NumSurfaces,
                ChildrenId = (s.Children[0], s.Children[1])
            };
            internal static NodeX Create(Binary_Bsp p, X_Node3 s) => new()
            {
                Min = s.Min,
                Max = s.Max,
                Plane = p.Planes[s.PlaneNum],
                ChildrenId = (s.Children[0], s.Children[1])
            };
            public Plane Plane;
            public (int l, int r) ChildrenId;
            public (Node l, Node r) Children;
            public int FirstSurface, NumSurfaces;
        }
        #endregion

        #region Leaf
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Leaf12 //was:dleaf_t
        {
            public static (string, int) Struct = ("<2i6hH4B", sizeof(X_Leaf12));
            public int Contents;
            public int VisOffset; // -1 = no visibility info
            public Vector3<short> Min, Max; // for frustum culling
            public ushort FirstMarkSurface, NumMarkSurface;
            public fixed byte AmbientLevel[4]; // automatic ambient sounds
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Leaf3
        {
            public static (string, int) Struct = ("<", sizeof(X_Leaf3));
            public int Cluster; // -1 = opaque cluster (do I still store these?)
            public int Area;
            public Vector3<int> Min, Max; // for frustom culling
            public int FirstLeafSurface, NumLeafSurfaces;
            public int FirstLeafBrush, NumLeafBrushes;
        }
        public class Leaf : Node
        {
            internal static Leaf Create(Binary_Bsp p, X_Leaf12 s) => new()
            {
                Min = new Vector3<int>(s.Min.X, s.Min.Y, s.Min.Z),
                Max = new Vector3<int>(s.Max.X, s.Max.Y, s.Max.Z),
                Contents = s.Contents,
                FirstMarkSurface = p.MarkSurfaces[s.FirstMarkSurface],
                NumMarkSurface = s.NumMarkSurface,
                CompressedVis = s.VisOffset != -1 ? p.Visiblemap.AsMemory(s.VisOffset) : null,
                AmbientLevel = [s.AmbientLevel[0], s.AmbientLevel[1], s.AmbientLevel[2], s.AmbientLevel[3]]
            };
            public Memory<byte> CompressedVis;
            public object EFrags;
            public Surface FirstMarkSurface; public int NumMarkSurface;
            public int Key; // BSP sequence number for leaf's contents
            public byte[] AmbientLevel;
        }
        #endregion

        #region Hull
        public class Hull
        {
            public ClipNode ClipNodes;
            public Plane[] Planes;
            public int FirstClipNode, LastClipNode;
            public Vector3 ClipMins, ClipMaxs;
        }
        #endregion

        #region Model
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Model1
        {
            public static (string, int) Struct = ("<9f7i", sizeof(X_Model1));
            public Vector3 Min, Max;
            public Vector3 Origin;
            public fixed int Headnodes[4];
            public int Visleafs; // not including the solid leaf 0
            public int FirstSurface, NumSurfaces;
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Model2
        {
            public static (string, int) Struct = ("<9f3i", sizeof(X_Model2));
            public Vector3 Min, Max;
            public Vector3 Origin; // for sounds or lights
            public int Headnode;
            public int FirstSurface, NumSurfaces; // submodels just draw faces without walking the bsp tree
        }
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Model3
        {
            public static (string, int) Struct = ("<6f4i", sizeof(X_Model3));
            public Vector3 Min, Max;
            public int FirstSurface, NumSurfaces;
            public int FirstBrush, NumBrushes;
        }
        public class Model
        {
            internal static Model Create(X_Model1 s) => new()
            {
                Min = new Vector3(s.Min.X - 1, s.Min.Y - 1, s.Min.Z - 1), // spread the mins / maxs by a pixel
                Max = new Vector3(s.Max.X + 1, s.Max.Y + 1, s.Max.Z + 1), // spread the mins / maxs by a pixel
                Origin = s.Origin,
                Headnodes = [s.Headnodes[0], s.Headnodes[1], s.Headnodes[2], s.Headnodes[3]],
                Visleafs = s.Visleafs,
                FirstSurface = s.FirstSurface,
                NumSurfaces = s.NumSurfaces
            };
            internal static Model Create(X_Model2 s) => new() { Min = s.Min, Max = s.Max, Origin = s.Origin, Headnodes = [s.Headnode], FirstSurface = s.FirstSurface, NumSurfaces = s.NumSurfaces };
            internal static Model Create(X_Model3 s) => new() { Min = s.Min, Max = s.Max, FirstSurface = s.FirstSurface, NumSurfaces = s.NumSurfaces, FirstBrush = s.FirstBrush, NumBrushes = s.NumBrushes };
            public Vector3 Min, Max;
            public Vector3 Origin;
            public int[] Headnodes;
            public int Visleafs; // not including the solid leaf 0
            public int FirstSurface, NumSurfaces;
            public int FirstBrush, NumBrushes;
        }
        #endregion

        #region Brush
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Brush23
        {
            public static (string, int) Struct = ("<3i", sizeof(X_BrushSide3));
            public int FirstSide, NumSides;
            public int ContentsShaderNum; // the shader that determines the contents flags
        }
        public class Brush
        {
            internal static Brush Create(X_Brush23 s) => new() { FirstSide = s.FirstSide, NumSides = s.NumSides, ContentsShaderNum = s.ContentsShaderNum };
            public int FirstSide, NumSides;
            public int ContentsShaderNum;
        }
        #endregion

        #region BrushSide
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_BrushSide2 //was:dbrushside_t
        {
            public static (string, int) Struct = ("<2i", sizeof(X_BrushSide3));
            public ushort PlaneNum; // positive plane side faces out of the leaf
            public short Texinfo;
        }
        internal struct X_BrushSide3 //was:dbrushside_t
        {
            public static (string, int) Struct = ("<2i", sizeof(X_BrushSide3));
            public int PlaneNum; // positive plane side faces out of the leaf
            public int ShaderNum;
        }
        public class BrushSide
        {
            internal static BrushSide Create(X_BrushSide2 s) => new() { PlaneNum = s.PlaneNum, Texinfo = s.Texinfo };
            internal static BrushSide Create(X_BrushSide3 s) => new() { PlaneNum = s.PlaneNum, ShaderNum = s.ShaderNum };
            public int PlaneNum;
            public int Texinfo;
            public int ShaderNum;
        }
        #endregion

        #region DrawVert
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_DrawVert3
        {
            public static (string, int) Struct = ("<14f", sizeof(X_DrawVert3));
            public Vector3 Xyz;
            public fixed float St[2];
            public fixed float Lightmap[2];
            public Vector3 Normal;
            public fixed float Color[3];
        }
        public struct DrawVert
        {
            internal static DrawVert Create(X_DrawVert3 s) => new() { Xyz = s.Xyz, St = [s.St[0], s.St[1]], Lightmap = [s.Lightmap[0], s.Lightmap[1]], Normal = s.Normal, Color = [s.Color[0], s.Color[1], s.Color[2]] };
            public Vector3 Xyz;
            public float[] St;
            public float[] Lightmap;
            public Vector3 Normal;
            public float[] Color;
        }
        #endregion

        #region Fog
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Fog3
        {
            public static (string, int) Struct = ("<64s2i", sizeof(X_Fog3));
            public fixed byte Name[64];
            public int BrushNum;
            public int VisibleSide; // the brush side that ray tests need to clip against (-1 == none)
        }
        public struct Fogx
        {
            internal static Fogx Create(X_Fog3 s) => new() { Name = UnsafeX.FixedAString(s.Name, 64), BrushNum = s.BrushNum, VisibleSide = s.VisibleSide };
            public string Name;
            public int BrushNum;
            public int VisibleSide;
        }
        #endregion

        #region ClipNode
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_ClipNode1
        {
            public static (string, int) Struct = ("<I2h", sizeof(X_ClipNode1));
            public uint PlaneNum;
            public (short l, short r) Children;
        }
        public class ClipNode
        {
            internal static ClipNode Create(X_ClipNode1 s) => new() { PlaneNum = s.PlaneNum, Children = s.Children };
            public uint PlaneNum;
            public (short l, short r) Children; // negative numbers are contents
        }
        #endregion

        #region Area
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Area2 //was:darea_t
        {
            public static (string, int) Struct = ("<2i6hH4B", sizeof(X_Area2));
            public int NumAreaPortals;
            public int FirstAreaPortal;
        }
        public struct Area
        {
            internal static Area Create(X_Area2 s) => new() { NumAreaPortals = s.NumAreaPortals, FirstAreaPortal = s.FirstAreaPortal };
            public int NumAreaPortals;
            public int FirstAreaPortal;
        }
        #endregion

        #region AreaPortal
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_AreaPortal2 //was:dareaportal_t
        {
            public static (string, int) Struct = ("<2i6hH4B", sizeof(X_Area2));
            public int PortalNum;
            public int OtherArea;
        }
        public struct AreaPortal
        {
            internal static AreaPortal Create(X_AreaPortal2 s) => new() { PortalNum = s.PortalNum, OtherArea = s.OtherArea };
            public int PortalNum;
            public int OtherArea;
        }
        #endregion

        #region Shader
        [StructLayout(LayoutKind.Sequential)]
        internal struct X_Shader3
        {
            public static (string, int) Struct = ("<64s2I", sizeof(X_Shader3));
            public fixed byte Name[64];
            public int SurfaceFlags;
            public int ContentFlags;
        }
        public class Shader
        {
            internal static Shader Create(X_Shader3 s) => new() { Name = UnsafeX.FixedAString(s.Name, 64), SurfaceFlags = s.SurfaceFlags, ContentFlags = s.ContentFlags };
            public string Name;
            public int SurfaceFlags;
            public int ContentFlags;
        }
        #endregion

        [StructLayout(LayoutKind.Sequential)]
        struct X_Header
        {
            public static (string, int) Struct = ("<31i", sizeof(X_Header));
            public int Version;
            public X_LumpON Lump00;
            public X_LumpON Lump01;
            public X_LumpON Lump02;
            public X_LumpON Lump03;
            public X_LumpON Lump04;
            public X_LumpON Lump05;
            public X_LumpON Lump06;
            public X_LumpON Lump07;
            public X_LumpON Lump08;
            public X_LumpON Lump09;
            public X_LumpON Lump10;
            public X_LumpON Lump11;
            public X_LumpON Lump12;
            public X_LumpON Lump13;
            public X_LumpON Lump14;
            public X_LumpON Lump15;
            public X_LumpON Lump16;
            public X_LumpON Lump17;
            public X_LumpON Lump18;
        }

        #endregion

        /*
        Q1 - https://github.com/id-Software/Quake/blob/master/WinQuake/bspfile.h, https://github.com/id-Software/Quake/blob/master/WinQuake/model.c#L1143
        Q2 - https://github.com/id-Software/Quake-2/blob/master/qcommon/qfiles.h, https://github.com/id-Software/Quake-2/blob/master/qcommon/cmodel.c#L543
        Q3 - https://github.com/id-Software/Quake-III-Arena/blob/master/code/qcommon/qfiles.h
        */
        string Entities;        // | 00 | 00 | 00
        Shader[] Shaders;       // | -- | -- | 01
        Plane[] Planes;         // | 01 | 01 | 02
        Vector3[] Vertices;     // | 03 | 02 | --
        byte[] Visiblemap;      // | 04 | 03 | 16
        NodeX[] Nodes;          // | 05 | 04 | 03
        Texture[] Textures;     // | 02 | 05 | --
        Texinfo[] Texinfos;     // | 06 | -- | --
        Model[] SubModels;      // | 14 | 13 | 07
        Brush[] Brushes;        // | -- | 14 | 08
        BrushSide[] BrushSides; // | -- | 15 | 09
        DrawVert[] DrawVerts;   // | -- | -- | 10
        object[] DrawIndexes;   // | -- | -- | 11
        Fogx[] Fog;             // | -- | -- | 12
        Surface[] Surfaces;     // | 07 | 06 | 13
        byte[] Lightmaps;       // | 08 | 07 | 14
        ClipNode[] ClipNodes;   // | 09 | -- | --
        Leaf[] Leafs;           // | 10 | 08 | 04
        object[] LeafSurfaces;  // | -- | 09 | 05
        object[] LeafBrushs;    // | -- | 10 | 06
        Edge[] Edges;           // | 12 | 11 | --
        int[] SurfaceEdges;     // | 13 | 12 | --
        object[] Pop;           // | -- | 16 | --
        Area[] Areas;           // | -- | 17 | --
        AreaPortal[] AreaPortals; // | -- | 18 | --
        Surface[] MarkSurfaces; // | 11 | -- | --

        public Binary_Bsp(BinaryReader r, PakFile s)
        {
            var engineV = s.Game.Engine.v;
            // read file
            var magic = engineV == "2" ? 0 : r.ReadUInt32();
            var header = r.ReadS<X_Header>();

            static Texture[] LoadTextures(BinaryReader r, int offset)
            {
                int i, j, num, max, altmax;
                Texture tx, tx2;
                Texture[] anims = new Texture[10], altanims = new Texture[10];

                var offsets = r.ReadL32PArray<int>("i");
                var textures = new Texture[offsets.Length];
                for (i = 0; i < offsets.Length; i++)
                {
                    if (offsets[i] == -1) continue;
                    r.Seek(offset + offsets[i]); tx = Texture.Create(r, r.ReadS<X_Texture1>());
                    if ((tx.Width & 15) != 0 || (tx.Height & 15) != 0) throw new FormatException($"Texture {tx.Name} is not 16 aligned");
                    if (tx.Name.StartsWith("sky")) { Console.WriteLine("R_InitSky(tx)"); }
                    textures[i] = tx;
                }

                // sequence the animations
                for (i = 0; i < textures.Length; i++)
                {
                    tx = textures[i];
                    if (tx == null || tx.Name[0] == '+') continue;
                    if (tx.AnimNext == null) continue;   // already sequenced

                    // find the number of frames in the animation
                    Array.Clear(anims, 0, 10);
                    Array.Clear(altanims, 0, 10);

                    max = tx.Name[1];
                    altmax = 0;
                    if (max >= 'a' && max <= 'z') max -= 'a' - 'A';
                    if (max >= '0' && max <= '9') { max -= '0'; altmax = 0; anims[max] = tx; max++; }
                    else if (max >= 'A' && max <= 'J') { altmax = max - 'A'; max = 0; altanims[altmax] = tx; altmax++; }
                    else throw new FormatException($"Bad animating texture {tx.Name}");

                    for (j = i + 1; j < textures.Length; j++)
                    {
                        tx2 = textures[j];
                        if (tx2 == null || tx2.Name[0] != '+') continue;
                        if (tx2.Name[2..] != tx.Name[2..]) continue;

                        num = tx2.Name[1];
                        if (num >= 'a' && num <= 'z') num -= 'a' - 'A';
                        if (num >= '0' && num <= '9') { num -= '0'; anims[num] = tx2; if (num + 1 > max) max = num + 1; }
                        else if (num >= 'A' && num <= 'J') { num -= 'A'; altanims[num] = tx2; if (num + 1 > altmax) altmax = num + 1; }
                        else throw new FormatException($"Bad animating texture {tx.Name}");
                    }

                    // link them all together
                    const int ANIM_CYCLE = 2;
                    for (j = 0; j < max; j++)
                    {
                        tx2 = anims[j];
                        if (tx2 == null) throw new FormatException($"Missing frame {j} of {tx.Name}");
                        tx2.AnimTotal = max * ANIM_CYCLE;
                        tx2.AnimMin = j * ANIM_CYCLE;
                        tx2.AnimMax = (j + 1) * ANIM_CYCLE;
                        tx2.AnimNext = anims[(j + 1) % max];
                        if (altmax != 0) tx2.AlternateAnims = altanims[0];
                    }
                    for (j = 0; j < altmax; j++)
                    {
                        tx2 = altanims[j];
                        if (tx2 == null) throw new FormatException($"Missing frame {j} of {tx.Name}");
                        tx2.AnimTotal = altmax * ANIM_CYCLE;
                        tx2.AnimMin = j * ANIM_CYCLE;
                        tx2.AnimMax = (j + 1) * ANIM_CYCLE;
                        tx2.AnimNext = altanims[(j + 1) % altmax];
                        if (max != 0) tx2.AlternateAnims = anims[0];
                    }
                }
                return textures;
            }

            switch (header.Version)
            {
                case 29:
                    // https://github.com/id-Software/Quake/blob/master/WinQuake/bspfile.h, https://github.com/id-Software/Quake/blob/master/WinQuake/model.c#L1143
                    r.Seek(header.Lump03.Offset); Vertices = (header.Lump03.Num % sizeof(Vector3)) == 0 ? r.ReadPArray<Vector3>("3f", header.Lump03.Num / sizeof(Vector3)) : throw new FormatException("Vertices: bad lump size");
                    r.Seek(header.Lump12.Offset); Edges = (header.Lump12.Num % sizeof(Vector2<ushort>)) == 0 ? r.ReadPArray<Vector2<ushort>>("2H", header.Lump12.Num / sizeof(Vector2<ushort>)).Select(Edge.Create).ToArray() : throw new FormatException("Edges: bad lump size");
                    r.Seek(header.Lump13.Offset); SurfaceEdges = (header.Lump13.Num % sizeof(int)) == 0 ? r.ReadPArray<int>("i", header.Lump13.Num / sizeof(int)) : throw new FormatException("SurfaceEdges: bad lump size");
                    r.Seek(header.Lump02.Offset); Textures = header.Lump02.Num != 0 ? LoadTextures(r, header.Lump02.Offset) : null;
                    r.Seek(header.Lump08.Offset); Lightmaps = header.Lump08.Num != 0 ? r.ReadBytes(header.Lump08.Num) : null;
                    r.Seek(header.Lump01.Offset); Planes = (header.Lump01.Num % sizeof(X_Plane12)) == 0 ? r.ReadSArray<X_Plane12>(header.Lump01.Num / sizeof(X_Plane12)).Select(Plane.Create).ToArray() : throw new FormatException("Planes: bad lump size");
                    r.Seek(header.Lump06.Offset); Texinfos = (header.Lump06.Num % sizeof(X_Texinfo1)) == 0 ? r.ReadSArray<X_Texinfo1>(header.Lump06.Num / sizeof(X_Texinfo1)).Select(s => Texinfo.Create(this, s)).ToArray() : throw new FormatException("Texinfos: bad lump size");
                    r.Seek(header.Lump07.Offset); Surfaces = (header.Lump07.Num % sizeof(X_Surface12)) == 0 ? r.ReadSArray<X_Surface12>(header.Lump07.Num / sizeof(X_Surface12)).Select(s => Surface.Create(this, s)).ToArray() : throw new FormatException("Surfaces: bad lump size");
                    r.Seek(header.Lump11.Offset); MarkSurfaces = (header.Lump11.Num % sizeof(short)) == 0 ? r.ReadPArray<short>("h", header.Lump11.Num / sizeof(short)).Select(s => Surface.Get(this, s)).ToArray() : throw new FormatException("MarkSurfaces: bad lump size");
                    r.Seek(header.Lump04.Offset); Visiblemap = header.Lump04.Num != 0 ? r.ReadBytes(header.Lump04.Num) : null;
                    r.Seek(header.Lump10.Offset); Leafs = (header.Lump10.Num % sizeof(X_Leaf12)) == 0 ? r.ReadSArray<X_Leaf12>(header.Lump10.Num / sizeof(X_Leaf12)).Select(s => Leaf.Create(this, s)).ToArray() : throw new FormatException("Leafs: bad lump size");
                    r.Seek(header.Lump05.Offset); Nodes = (header.Lump05.Num % sizeof(X_Node12)) == 0 ? r.ReadSArray<X_Node12>(header.Lump05.Num / sizeof(X_Node12)).Select(s => NodeX.Create(this, s)).ToArray() : throw new FormatException("Nodes: bad lump size"); Node.Bind(this);
                    r.Seek(header.Lump09.Offset); ClipNodes = (header.Lump09.Num % sizeof(X_ClipNode1)) == 0 ? r.ReadSArray<X_ClipNode1>(header.Lump09.Num / sizeof(X_ClipNode1)).Select(ClipNode.Create).ToArray() : throw new FormatException("ClipNodes: bad lump size");
                    r.Seek(header.Lump00.Offset); Entities = r.ReadFAString(header.Lump00.Num);
                    r.Seek(header.Lump14.Offset); SubModels = (header.Lump14.Num % sizeof(X_Model1)) == 0 ? r.ReadSArray<X_Model1>(header.Lump14.Num / sizeof(X_Model1)).Select(Model.Create).ToArray() : throw new FormatException("SubModels: bad lump size");
                    break;
                case 38:
                    //r.Seek(header.Lump05.Offset); Texinfos = r.ReadSArray<X_Texinfo2>(header.Lump05.Num).Select(s => Texinfo.Create(this, s)).ToArray();
                    //r.Seek(header.Lump08.Offset); Leafs = r.ReadSArray<X_Leaf12>(header.Lump08.Num).Select(Leaf.Create).ToArray();
                    ////r.Seek(header.Lump10.Offset); LeafBrushs = r.ReadSArray<X_LeafBrush2>(header.Lump10.Num).Select(LeafBrush.Create).ToArray();
                    //r.Seek(header.Lump01.Offset); Planes = r.ReadSArray<X_Plane12>(header.Lump01.Num).Select(Plane.Create).ToArray();
                    //r.Seek(header.Lump14.Offset); Brushes = r.ReadSArray<X_Brush23>(header.Lump14.Num).Select(Brush.Create).ToArray();
                    //r.Seek(header.Lump15.Offset); BrushSides = r.ReadSArray<X_BrushSide2>(header.Lump15.Num).Select(BrushSide.Create).ToArray();
                    //r.Seek(header.Lump13.Offset); Models = r.ReadSArray<X_Model2>(header.Lump13.Num).Select(Model.Create).ToArray();
                    //r.Seek(header.Lump04.Offset); Nodes = r.ReadSArray<X_Node12>(header.Lump04.Num).Select(Node.Create).ToArray();
                    //r.Seek(header.Lump17.Offset); Areas = r.ReadSArray<X_Area2>(header.Lump17.Num).Select(Area.Create).ToArray();
                    //r.Seek(header.Lump18.Offset); AreaPortals = r.ReadSArray<X_AreaPortal2>(header.Lump18.Num).Select(AreaPortal.Create).ToArray();
                    //r.Seek(header.Lump03.Offset); Visiblemap = r.ReadBytes(header.Lump03.Num);
                    //r.Seek(header.Lump00.Offset); Entities = r.ReadFAString(header.Lump00.Num);

                    //r.Seek(header.Lump02.Offset); Vertices = r.ReadPArray<Vector3>("3f", header.Lump02.Num);
                    //r.Seek(header.Lump06.Offset); Surfaces = r.ReadSArray<X_Surface12>(header.Lump06.Num).Select(Surface.Create).ToArray();
                    //r.Seek(header.Lump07.Offset); Lightmaps = r.ReadBytes(header.Lump07.Num);
                    //r.Seek(header.Lump09.Offset); LeafFaces = r.ReadSArray<X_LeafFace2>(header.Lump09.Num).Select(LeafFace.Create).ToArray();
                    //r.Seek(header.Lump11.Offset); Edges = r.ReadPArray<Vector2<ushort>>("2H", header.Lump11.Num);
                    //r.Seek(header.Lump12.Offset); SurfaceEdges = r.ReadPArray<int>("i", header.Lump12.Num);
                    //r.Seek(header.Lump16.Offset); Pop = r.ReadSArray<X_Texture1>(header.Lump16.Num).Select(Texture.Create).ToArray();
                    break;
                case 12:
                    //r.Seek(header.Lump00.Offset); Entities = r.ReadFAString(header.Lump0.Num);
                    //r.Seek(header.Lump01.Offset); Textures = r.ReadSArray<X_Plane12>(header.Lump1.Num).Select(x => new Plane(x));
                    //r.Seek(header.Lump02.Offset); Planes = r.ReadSArray<X_Edges12>(header.Planes.Num);
                    //r.Seek(header.Lump03.Offset); Nodes = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump04.Offset); Leafs = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump05.Offset); LeafFaces = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump06.Offset); LeafBrushes = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump07.Offset); Models = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump08.Offset); Brushes = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump09.Offset); BrushSides = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump10.Offset); Vertices = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump11.Offset); MeshVerts = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump12.Offset); Effects = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump13.Offset); Faces = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump14.Offset); Lightmaps = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump14.Offset); LightVols = r.ReadSArray<X_Texture>(header.Planes.Num);
                    //r.Seek(header.Lump14.Offset); VisData = r.ReadSArray<X_Texture>(header.Planes.Num);
                    break;
                default: throw new FormatException("BAD VERSION");
            }
            //r.Seek(header.Textures.Offset); Textures = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.Vertices.Offset); Vertices = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.Visibility.Offset); Visibility = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.Nodes.Offset); Nodes = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.Texinfo.Offset); Texinfo = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.Faces.Offset); Faces = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.Lighting.Offset); Lighting = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.ClipNodes.Offset); ClipNodes = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.Leaves.Offset); Leaves = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.MarkSurfaces.Offset); MarkSurfaces = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.Edges.Offset); Edges = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.SurfEdges.Offset); SurfEdges = r.ReadSArray<X_Texture>(header.Planes.Num);
            //r.Seek(header.Models.Offset); Models = r.ReadSArray<X_Texture>(header.Planes.Num);
        }

        //std::vector<Q3BspTextureLump> textures;
        //std::vector<Q3BspPlaneLump> planes;
        //std::vector<Q3BspNodeLump> nodes;
        //std::vector<Q3BspLeafLump> leaves;
        //std::vector<Q3BspLeafFaceLump> leafFaces;
        //std::vector<Q3BspLeafBrushLump> leafBrushes;
        //std::vector<Q3BspModelLump> models;
        //std::vector<Q3BspBrushLump> brushes;
        //std::vector<Q3BspBrushSideLump> brushSides;
        //std::vector<Q3BspVertexLump> vertices;
        //std::vector<Q3BspMeshVertLump> meshVertices;
        //std::vector<Q3BspEffectLump> effects;
        //std::vector<Q3BspFaceLump> faces;
        //std::vector<Q3BspLightMapLump> lightMaps;
        //std::vector<Q3BspLightVolLump> lightVols;
        //Q3BspVisDataLump visData;

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => [
                new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = Entities }),
                new("BSP", items: [
                    new($"Planes: {Planes.Length}"),
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

        #region Records

        public byte[][] PaletteRecords;
        public byte[][] ColormapRecords;

        public static byte[] ToLightPixel(int light, int pixel) => Palette.PaletteRecords[Colormap.ColormapRecords[(light >> 3) & 0x1F][pixel]];

        byte[] Pixels;
        static (object gl, object vulken, object unity, object unreal) Format = (
            (TextureGLFormat.Rgba, TextureGLPixelFormat.Bgra, TextureGLPixelType.UnsignedInt8888),
            (TextureGLFormat.Rgba, TextureGLPixelFormat.Bgra, TextureGLPixelType.UnsignedInt8888),
            TextureUnityFormat.Unknown,
            TextureUnrealFormat.Unknown);

        #endregion

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

        public int Width { get; }
        public int Height { get; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags TexFlags { get; } = 0;

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
            => (Pixels, (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, null);
        public void End() { }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => [
                new(null, new MetaContent { Type = "TextureName", Name = Path.GetFileName(file.Path), Value = this }),
                new("TextureName", items: [
                    new($"Width: {Width}"),
                    new($"Height: {Height}"),
                ])
            ];
    }

    #endregion

    #region Binary_Level

    //public unsafe class Binary_Level : IHaveMetaInfo
    //{
    //    public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Level(r));

    //    #region Headers

    //    [StructLayout(LayoutKind.Sequential)]
    //    struct D_Entry
    //    {
    //        public int Offset;                // Offset to entry, in bytes, from start of file
    //        public int Size;                  // Size of entry in file, in bytes
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    struct BoundBox
    //    {
    //        public Vector3 Min;                // minimum values of X,Y,Z
    //        public Vector3 Max;                // maximum values of X,Y,Z
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    struct D_Header
    //    {
    //        public static (string, int) Struct = ("<I2i", sizeof(D_Header));
    //        public int Version;                 // Model version, must be 0x17 (23).
    //        public D_Entry Entities;            // List of Entities.
    //        public D_Entry Planes;              // Map Planes.
    //        public readonly int NumPlanes => Planes.Size / sizeof(D_Plane);
    //        public D_Entry Miptex;              // Wall Textures.
    //        public D_Entry Vertices;            // Map Vertices.
    //        public readonly int NumVertices => Vertices.Size / sizeof(Vector3);
    //        public D_Entry VisiList;            // Leaves Visibility lists.
    //        public D_Entry Nodes;               // BSP Nodes.
    //        public readonly int NumNodes => Nodes.Size / sizeof(D_Node);
    //        public D_Entry Texinfo;             // Texture Info for faces.
    //        public readonly int NumTexinfo => Texinfo.Size / sizeof(D_Texinfo);
    //        public D_Entry Faces;               // Faces of each surface.
    //        public readonly int NumFaces => Faces.Size / sizeof(D_Face);
    //        public D_Entry LightMaps;           // Wall Light Maps.
    //        public D_Entry ClipNodes;           // clip nodes, for Models.
    //        public readonly int NumClips => ClipNodes.Size / sizeof(D_ClipNode);
    //        public D_Entry Leaves;              // BSP Leaves.
    //        public readonly int NumLeaves => Leaves.Size / sizeof(D_Leaf);
    //        public D_Entry LFace;               // List of Faces.
    //        public D_Entry Edges;               // Edges of faces.
    //        public readonly int NumEdges => Edges.Size / sizeof(Vector2<ushort>);
    //        public D_Entry Ledges;              // List of Edges.
    //        public D_Entry Models;              // List of Models.
    //        public readonly int NumModels => Models.Size / sizeof(D_Model);
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    struct D_Model
    //    {
    //        public BoundBox Bound;              // The bounding box of the Model
    //        public Vector3 Origin;              // origin of model, usually (0,0,0)
    //        public int NodeId0;                 // index of first BSP node
    //        public int NodeId1;                 // index of the first Clip node
    //        public int NodeId2;                 // index of the second Clip node
    //        public int NodeId3;                 // usually zero
    //        public int NumLeafs;                // number of BSP leaves
    //        public int FaceId;                  // index of Faces
    //        public int FaceNum;                 // number of Faces
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    struct D_Texinfo
    //    {
    //        public Vector3 VectorS;             // S vector, horizontal in texture space)
    //        public float DistS;                 // horizontal offset in texture space
    //        public Vector3 VectorT;             // T vector, vertical in texture space
    //        public float DistT;                 // vertical offset in texture space
    //        public uint TextureId;              // Index of Mip Texture must be in [0,numtex[
    //        public uint Animated;               // 0 for ordinary textures, 1 for water 
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    struct D_Face
    //    {
    //        public ushort PlaneId;              // The plane in which the face lies: must be in [0,numplanes[ 
    //        public ushort Side;                 // 0 if in front of the plane, 1 if behind the plane
    //        public int LedgeId;                 // first edge in the List of edges: must be in [0,numledges[
    //        public ushort LedgeNum;             // number of edges in the List of edges
    //        public ushort TexinfoId;            // index of the Texture info the face is part of: must be in [0,numtexinfos[ 
    //        public byte TypeLight;              // type of lighting, for the face
    //        public byte BaseLight;              // from 0xFF (dark) to 0 (bright)
    //        public fixed byte Light[2];         // two additional light models  
    //        public int LightMap;                // Pointer inside the general light map, or -1
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    struct D_Node
    //    {
    //        public long PlaneId;                // The plane that splits the node: must be in [0,numplanes[
    //        public ushort Front;                // If bit15==0, index of Front child node: If bit15==1, ~front = index of child leaf
    //        public ushort Back;                 // If bit15==0, id of Back child node: If bit15==1, ~back =  id of child leaf
    //        public Vector2<short> Box;          // Bounding box of node and all childs
    //        public ushort FaceId;               // Index of first Polygons in the node
    //        public ushort FaceNum;              // Number of faces in the node
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    struct D_Leaf
    //    {
    //        public int Type;                    // Special type of leaf
    //        public int VisList;                 // Beginning of visibility lists: must be -1 or in [0,numvislist[
    //        Vector2<short> Bound;               // Bounding box of the leaf
    //        public ushort LFaceId;              // First item of the list of faces: must be in [0,numlfaces[
    //        public ushort LFaceNum;             // Number of faces in the leaf  
    //        public byte SndWater;               // level of the four ambient sounds:
    //        public byte SndSky;                 //   0    is no sound
    //        public byte SndSlime;               //   0xFF is maximum volume
    //        public byte SndLava;                //
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    struct D_Plane
    //    {
    //        public Vector3 Normal;              // Vector orthogonal to plane (Nx,Ny,Nz): with Nx2+Ny2+Nz2 = 1
    //        public float Dist;                  // Offset to plane, along the normal vector: Distance from (0,0,0) to the plane
    //        public int Type;                    // Type of plane, depending on normal vector.
    //    }

    //    [StructLayout(LayoutKind.Sequential)]
    //    struct D_ClipNode
    //    {
    //        public uint PlaneNum;               // The plane which splits the node
    //        public short Front;                 // If positive, id of Front child node: If -2, the Front part is inside the model: If -1, the Front part is outside the model
    //        public short Back;                  // If positive, id of Back child node: If -2, the Back part is inside the model: If -1, the Back part is outside the model
    //    }

    //    #endregion

    //    // file: xxxx.bsp
    //    public Binary_Level(BinaryReader r)
    //    {
    //    }

    //    // IHaveMetaInfo
    //    List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
    //        => new List<MetaInfo> {
    //            new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "BSP File" }),
    //            new MetaInfo("Level", items: new List<MetaInfo> {
    //                //new MetaInfo($"Records: {Records.Length}"),
    //            })
    //        };
    //}

    #endregion

    #region Binary_Mdl
    // https://icculus.org/homepages/phaethon/q3a/formats/md2-schoenblum.html#:~:text=Quake2%20models%20are%20stored%20in,md2%20extension.

    public unsafe class Binary_Mdl : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Mdl(r));

        #region Records

        #endregion

        // file: xxxx.mdl
        public Binary_Mdl(BinaryReader r)
        {
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => new List<MetaInfo> {
                new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "Model File" }),
                new MetaInfo("Model", items: new List<MetaInfo> {
                    //new MetaInfo($"Records: {Records.Length}"),
                })
            };
    }

    #endregion

    #region Binary_Spr

    public unsafe class Binary_Spr : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Spr(r));

        #region Records

        const uint SPR_MAGIC = 0x1122; // IDSP

        enum SpriteType
        {
            ParallelUpright = 0, // vp parallel upright
            FaceUpright = 1, // facing upright
            Parallel = 2, // vp parallel
            Oriented = 3, // oriented
            ParallelOriented = 4, // vp parallel oriented
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SPR_Header
        {
            public static (string, int) Struct = ("<I2i", sizeof(SPR_Header));
            public uint Magic;      // "IDSP"
            public int Version;     // Version = 1
            public int Type;        // See below
            public float Radius;           // Bounding Radius
            public int MaxWidth;           // Width of the largest frame
            public int MaxHeight;          // Height of the largest frame
            public int NumFrames;          // Number of frames
            public float BeamLength;       // 
            public int SynchType;          // 0=synchron 1=random
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SPR_Picture
        {
            public static (string, int) Struct = ("<I2i", sizeof(SPR_Picture));
            public int OfsX;        // horizontal offset, in 3D space
            public int OfsY;        // vertical offset, in 3D space
            public int Width;       // width of the picture
            public int Height;      // height of the picture
        }

        #endregion
        // R_GetSpriteFrame

        // file: xxxx.spr
        public Binary_Spr(BinaryReader r)
        {
            var header = r.ReadS<SPR_Header>();
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => new List<MetaInfo> {
                new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "Sprite File" }),
                new MetaInfo("Sprite", items: new List<MetaInfo> {
                    //new MetaInfo($"Records: {Records.Length}"),
                })
            };
    }

    #endregion
}

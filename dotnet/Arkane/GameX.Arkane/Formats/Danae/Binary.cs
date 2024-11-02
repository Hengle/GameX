using GameX.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static OpenStack.Debug;

namespace GameX.Arkane.Formats.Danae
{
    #region Binary_Ftl

    public unsafe class Binary_Ftl : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Ftl(r));

        #region Headers

        const int FTL_MAGIC = 0x004c5446;
        const float FTL_VERSION = 0.83257f;

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_HEADER
        {
            public static (string, int) Struct = ("<6i", sizeof(FTL_HEADER));
            public int Offset3Ddata;                // -1 = no
            public int OffsetCylinder;              // -1 = no
            public int OffsetProgressiveData;       // -1 = no
            public int OffsetClothesData;           // -1 = no
            public int OffsetCollisionSpheres;      // -1 = no
            public int OffsetPhysicsBox;            // -1 = no
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_PROGRESSIVEHEADER
        {
            public static (string, int) Struct = ("<i", sizeof(FTL_PROGRESSIVEHEADER));
            public int NumVertex;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_CLOTHESHEADER
        {
            public static (string, int) Struct = ("<2i", sizeof(FTL_CLOTHESHEADER));
            public int NumCvert;
            public int NumSprings;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_COLLISIONSPHERESHEADER
        {
            public static (string, int) Struct = ("<i", sizeof(FTL_COLLISIONSPHERESHEADER));
            public int NumSpheres;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_3DHEADER
        {
            public static (string, int) Struct = ("<7i256s", 28 + 256);
            public int NumVertex;
            public int NumFaces;
            public int NumMaps;
            public int NumGroups;
            public int NumAction;
            public int NumSelections;
            public int Origin;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_VERTEX
        {
            public static (string, int) Struct = ($"<{TLVERTEX.Struct.Item1}6f", sizeof(FTL_VERTEX));
            public TLVERTEX Vert;
            public Vector3 V;
            public Vector3 Norm;
            public static implicit operator E_VERTEX(FTL_VERTEX s) => new()
            {
                Vert = s.Vert,
                V = s.V,
                Norm = s.Norm,
                VWorld = default,
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_TEXTURE
        {
            public static (string, int) Struct = ("<256s", 256);
            public const int SizeOf = 256;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
            public static implicit operator E_TEXTURE(FTL_TEXTURE s)
            {
                var name = s.Name;
                POLY poly = 0;
                if (name.Contains("NPC_")) poly |= POLY.LATE_MIP;
                if (name.Contains("nocol")) poly |= POLY.NOCOL;
                if (name.Contains("climb")) poly |= POLY.CLIMB; // change string depending on GFX guys
                if (name.Contains("fall")) poly |= POLY.FALL;
                if (name.Contains("lava")) poly |= POLY.LAVA;
                if (name.Contains("water")) poly |= POLY.WATER | POLY.TRANS;
                else if (name.Contains("spider_web")) poly |= POLY.WATER | POLY.TRANS;
                else if (name.Contains("[metal]")) poly |= POLY.METAL;
                return new E_TEXTURE
                {
                    Path = s.Name,
                    Poly = poly,
                };
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_FACE
        {
            public static (string, int) Struct = ("<4i3Hh6f6h14f", sizeof(FTL_FACE));
            public int FaceType;  // 0 = flat, 1 = text, 2 = Double-Side
            public Vector3<int> Rgb;
            public Vector3<ushort> Vid;
            public short TexId;
            public Vector3 U;
            public Vector3 V;
            public Vector3<short> Ou;
            public Vector3<short> Ov;
            public float TransVal;
            public Vector3 Norm;
            public Vector3 Nrmls0; public Vector3 Nrmls1; public Vector3 Nrmls2;
            public float Temp;
            public static implicit operator E_FACE(FTL_FACE s) => new()
            {
                FaceType = s.FaceType,
                TexId = s.TexId,
                U = s.U,
                V = s.V,
                Ou = s.Ou,
                Ov = s.Ov,
                TransVal = s.TransVal,
                Norm = s.Norm,
                Nrmls = new[] { s.Nrmls0, s.Nrmls1, s.Nrmls2 },
                Temp = s.Temp,
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_GROUPLIST
        {
            public static (string, int) Struct = ("<256s3if", 256 + 16);
            public const int SizeOf = 256 + 16;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
            public int Origin;
            public int NumIndex;
            public int Trash; // Indexes;
            public float Size;
            public static implicit operator E_GROUPLIST(FTL_GROUPLIST s) => new()
            {
                Name = s.Name,
                Origin = s.Origin,
                NumIndex = s.NumIndex,
                Size = s.Size,
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_ACTIONLIST
        {
            public static (string, int) Struct = ("<256s3i", 256 + 12);
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Name;
            public int Idx; //index vertex;
            public int Act; //action
            public int Sfx; //sfx
            public static implicit operator E_ACTIONLIST(FTL_ACTIONLIST s) => new()
            {
                Name = s.Name,
                Idx = s.Idx,
                Act = s.Act,
                Sfx = s.Sfx,
            };
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTL_SELECTIONS
        {
            public static (string, int) Struct = ("<64s2i", 64 + 8);
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] string Name;
            public int NumSelected;
            public int Trash; //Selected;
            public static implicit operator E_SELECTIONS(FTL_SELECTIONS s) => new()
            {
                Name = s.Name,
                NumSelected = s.NumSelected,
            };
        }

        #endregion

        public readonly E_3DOBJ Obj;

        // https://github.com/OpenSourcedGames/Arx-Fatalis/blob/master/Sources/DANAE/ARX_FTL.cpp#L575
        public Binary_Ftl(BinaryReader r)
        {
            Obj = new E_3DOBJ();
            var magic = r.ReadUInt32();
            if (magic != FTL_MAGIC) throw new FormatException($"Invalid FTL magic: \"{magic}\".");
            var version = r.ReadSingle();
            if (version != FTL_VERSION) throw new FormatException($"Invalid FLT version: \"{version}\".");
            r.Skip(512); // skip checksum
            var header = r.ReadS<FTL_HEADER>();

            // Check For & Load 3D Data
            if (header.Offset3Ddata != -1)
            {
                r.Seek(header.Offset3Ddata);
                var _3Dh = r.ReadS<FTL_3DHEADER>();
                Obj.NumVertex = _3Dh.NumVertex;
                Obj.NumFaces = _3Dh.NumFaces;
                Obj.NumMaps = _3Dh.NumMaps;
                Obj.NumGroups = _3Dh.NumGroups;
                Obj.NumAction = _3Dh.NumAction;
                Obj.NumSelections = _3Dh.NumSelections;
                Obj.Origin = _3Dh.Origin;
                Obj.File = _3Dh.Name;

                // Alloc'n'Copy vertices
                if (_3Dh.NumVertex > 0)
                {
                    var vertexList = r.ReadSArray<FTL_VERTEX>(_3Dh.NumVertex);
                    Obj.VertexList = new E_VERTEX[_3Dh.NumVertex];
                    for (var i = 0; i < Obj.VertexList.Length; i++)
                    {
                        Obj.VertexList[i] = vertexList[i];
                        Obj.VertexList[i].Vert.Color = 0xFF000000;
                    }
                    Obj.Point0 = Obj.VertexList[Obj.Origin].V;
                }

                // Alloc'n'Copy faces
                if (_3Dh.NumFaces > 0)
                {
                    var faceList = r.ReadSArray<FTL_FACE>(_3Dh.NumFaces);
                    Obj.FaceList = new E_FACE[_3Dh.NumFaces];
                    for (var i = 0; i < Obj.FaceList.Length; i++)
                        Obj.FaceList[i] = faceList[i];
                }

                // Alloc'n'Copy textures
                if (_3Dh.NumMaps > 0)
                {
                    var textures = r.ReadTEach<FTL_TEXTURE>(FTL_TEXTURE.SizeOf, _3Dh.NumMaps);
                    Obj.Textures = new E_TEXTURE[_3Dh.NumMaps];
                    for (var i = 0; i < Obj.Textures.Length; i++)
                        Obj.Textures[i] = textures[i];
                }

                // Alloc'n'Copy groups
                if (_3Dh.NumGroups > 0)
                {
                    var groupList = r.ReadTEach<FTL_GROUPLIST>(FTL_GROUPLIST.SizeOf, _3Dh.NumGroups);
                    Obj.GroupList = new E_GROUPLIST[_3Dh.NumGroups];
                    for (var i = 0; i < Obj.GroupList.Length; i++)
                    {
                        Obj.GroupList[i] = groupList[i];
                        if (Obj.GroupList[i].NumIndex > 0) Obj.GroupList[i].Indexes = r.ReadPArray<int>("i", Obj.GroupList[i].NumIndex);
                    }
                }

                // Alloc'n'Copy action points
                if (_3Dh.NumAction > 0)
                {
                    var actionList = r.ReadTEach<FTL_ACTIONLIST>(FTL_ACTIONLIST.Struct.Item2, _3Dh.NumAction);
                    Obj.ActionList = new E_ACTIONLIST[_3Dh.NumAction];
                    for (var i = 0; i < Obj.ActionList.Length; i++)
                        Obj.ActionList[i] = actionList[i];
                }

                // Alloc'n'Copy selections
                if (_3Dh.NumSelections > 0)
                {
                    var selections = r.ReadFArray(x => r.ReadS<FTL_SELECTIONS>(), _3Dh.NumSelections);
                    Obj.Selections = new E_SELECTIONS[_3Dh.NumSelections];
                    for (var i = 0; i < Obj.Selections.Length; i++)
                    {
                        Obj.Selections[i] = selections[i];
                        Obj.Selections[i].Selected = r.ReadPArray<int>("i", Obj.Selections[i].NumSelected);
                    }
                }
            }

            // Alloc'n'Copy Collision Spheres Data
            if (header.OffsetCollisionSpheres != -1)
            {
                r.Seek(header.OffsetCollisionSpheres);
                var csh = r.ReadS<FTL_COLLISIONSPHERESHEADER>();
                Obj.Sdata = new COLLISION_SPHERES_DATA
                {
                    NumSpheres = csh.NumSpheres,
                    Spheres = r.ReadSArray<COLLISION_SPHERE>(csh.NumSpheres),
                };
            }

            // Alloc'n'Copy Progressive DATA
            if (header.OffsetProgressiveData != -1)
            {
                r.Seek(header.OffsetProgressiveData);
                var ph = r.ReadS<FTL_PROGRESSIVEHEADER>();
                r.Skip(sizeof(PROGRESSIVE_DATA) * ph.NumVertex);
            }

            // Alloc'n'Copy Clothes DATA
            if (header.OffsetClothesData != -1)
            {
                r.Seek(header.OffsetClothesData);
                var ch = r.ReadS<FTL_CLOTHESHEADER>();
                Obj.Cdata = new CLOTHES_DATA
                {
                    NumCvert = (short)ch.NumCvert,
                    NumSprings = (short)ch.NumSprings,
                    Cvert = r.ReadSArray<CLOTHESVERTEX>(ch.NumCvert),
                    Springs = r.ReadSArray<E_SPRINGS>(ch.NumSprings),
                };
            }
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => [
                new("BinaryFTL", items: [
                    new($"Obj: {Obj}"),
                ])
            ];
    }

    #endregion

    #region Binary_Fts

    public unsafe class Binary_Fts : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Fts(r));

        #region Headers : Struct

        struct ANCHOR_DATA
        {
            public static (string, int) Struct = ("<3f2h?2f", sizeof(ANCHOR_DATA));
            public Vector3 Pos;
            public short NumLinked;
            public short Flags;
            public int[] Linked;
            public float Radius;
            public float Height;
        }

        public struct E_BKG_INFO
        {
            public static (string, int) Struct = ("<?", sizeof(E_BKG_INFO));
            public byte Treat;
            public bool Nothing;
            public short NumPoly;
            public short NumIAnchors;
            public short NumPolyin;
            public float FrustrumMinY;
            public float FrustrumMaxY;
            public E_POLY[] Polydata;
            //public E_POLY[][] Polyin;
            public int[] IAnchors; // index on anchors list
            public int Flags;
            public float TileMinY;
            public float TileMaxY;
        }

        struct E_SMINMAX
        {
            public static (string, int) Struct = ("<2h", sizeof(E_SMINMAX));
            public short Min;
            public short Max;
        }

        //const int MAX_GOSUB = 10;
        //const int MAX_SHORTCUT = 80;
        //const int MAX_SCRIPTTIMERS = 5;
        //const int FBD_TREAT = 1;
        //const int FBD_NOTHING = 2;

        struct FAST_BKG_DATA
        {
            public static (string, int) Struct = ("<?", sizeof(FAST_BKG_DATA));
            public byte Treat;
            public byte Nothing;
            public short NumPoly;
            public short NumIAnchors;
            public short NumPolyin;
            public int Flags;
            public float FrustrumMinY;
            public float FrustrumMaxY;
            public E_POLY[] Polydata;
            public E_POLY[][] Polyin;
            public int[] IAnchors; // index on anchors list
        }

        const int MAX_BKGX = 160;
        const int MAX_BKGZ = 160;
        const int BKG_SIZX = 100;
        const int BKG_SIZZ = 100;

        class E_BACKGROUND
        {
            public FAST_BKG_DATA[,] fastdata = new FAST_BKG_DATA[MAX_BKGX, MAX_BKGZ];
            public int exist = 1;
            public short XSize;
            public short ZSize;
            public short Xdiv;
            public short Zdiv;
            public float Xmul;
            public float Zmul;
            public E_BKG_INFO[] Backg;
            public Vector3 Ambient;
            public Vector3 Ambient255;
            public E_SMINMAX[] MinMax;
            public int NumAnchors;
            public ANCHOR_DATA[] Anchors;
            public string Name;
            public E_BACKGROUND(short sx = MAX_BKGX, short sz = MAX_BKGZ, short xdiv = BKG_SIZX, short zdiv = BKG_SIZZ)
            {
                XSize = sx;
                ZSize = sz;
                if (xdiv < 0) xdiv = 1;
                if (zdiv < 0) zdiv = 1;
                Xdiv = xdiv;
                Zdiv = zdiv;
                Xmul = 1f / Xdiv;
                Zmul = 1f / Zdiv;
                Backg = new E_BKG_INFO[sx * sz];
                for (var i = 0; i < Backg.Length; i++) Backg[i].Nothing = true;
                MinMax = new E_SMINMAX[sz];
                for (var i = 0; i < MinMax.Length; i++)
                {
                    MinMax[i].Min = 9999;
                    MinMax[i].Max = -1;
                }
            }
        }

        #endregion

        #region Headers

        const float NON_PORTAL_VERSION = 0.136f;
        const float FTS_VERSION = 0.141f;

        [StructLayout(LayoutKind.Sequential)]
        struct FTS_HEADER
        {
            public static (string, int) Struct = ("<256sifi3i", 256 + 24);
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Path;
            public int Count;
            public float Version;
            public int Compressedsize;
            public fixed int Pad[3];
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FTS_HEADER2
        {
            public static (string, int) Struct = ("<256s", 256);
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Path;
        }

        const int SIZ_WRK = 10;

        public class FastLevel
        {
            public Vector3 PlayerPos;
            public Vector3 MscenePos;
            public E_TEXTURE[] Textures;
            public E_BKG_INFO[] Backg;
            public E_PORTAL_DATA Portals;
            public int NumRoomDistance;
            public ROOM_DIST_DATA[] RoomDistance;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FAST_VERTEX
        {
            public static (string, int) Struct = ("<5f", sizeof(FAST_VERTEX));
            public float sy;
            public float ssx;
            public float ssz;
            public float stu;
            public float stv;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FAST_EERIEPOLY
        {
            public static (string, int) Struct = ("<20fi20f?2h", sizeof(FAST_EERIEPOLY));
            public FAST_VERTEX V0; public FAST_VERTEX V1; public FAST_VERTEX V2; public FAST_VERTEX V3;
            public int TexPtr;
            public Vector3 Norm;
            public Vector3 Norm2;
            public Vector3 Nrml0; public Vector3 Nrml1; public Vector3 Nrml2; public Vector3 Nrml3;
            public float Transval;
            public float Area;
            public POLY Type;
            public short Room;
            public short Paddy;
        }

        struct FAST_SCENE_HEADER
        {
            public static (string, int) Struct = ("<f5i6f2i", sizeof(FAST_SCENE_HEADER));
            public float Version;
            public int SizeX;
            public int SizeZ;
            public int NumTextures;
            public int NumPolys;
            public int NumAnchors;
            public Vector3 PlayerPos;
            public Vector3 MscenePos;
            public int NumPortals;
            public int NumRooms;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FAST_TEXTURE_CONTAINER
        {
            public static (string, int) Struct = ("<2i256s", 8 + 256);
            public int TcPtr;
            public int TempPtr;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Fic;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FAST_ANCHOR_DATA
        {
            public static (string, int) Struct = ("<5f2h", sizeof(FAST_ANCHOR_DATA));
            public Vector3 Pos;
            public float Radius;
            public float Height;
            public short NumLinked;
            public short Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct FAST_SCENE_INFO
        {
            public static (string, int) Struct = ("<2I", sizeof(FAST_SCENE_INFO));
            public int NumPoly;
            public int NumIAnchors;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct ROOM_DIST_DATA_SAVE
        {
            public static (string, int) Struct = ("<7f", sizeof(ROOM_DIST_DATA_SAVE));
            public float Distance; // -1 means use truedist
            public Vector3 StartPos;
            public Vector3 EndPos;
        }

        public struct ROOM_DIST_DATA
        {
            public static (string, int) Struct = ("<7f", sizeof(ROOM_DIST_DATA));
            public float Distance; // -1 means use truedist
            public Vector3 StartPos;
            public Vector3 EndPos;
        }

        #endregion

        public readonly FastLevel Level;
        readonly E_BACKGROUND Bkg;

        // https://github.com/OpenSourcedGames/Arx-Fatalis/blob/master/Sources/EERIE/EERIEPoly.cpp#L3755
        public Binary_Fts(BinaryReader r)
        {
            int i, j, k, kk;
            var header = r.ReadS<FTS_HEADER>();
            if (header.Version != FTS_VERSION) throw new FormatException("BAD MAGIC");
            //Log($"Header1: {r.Position():x}, {header.Path}");
            if (header.Count > 0)
            {
                var count = 0;
                while (count < header.Count)
                {
                    r.ReadS<FTS_HEADER2>();
                    r.Skip(512); // skip check
                    //Log($"Unique[{count}]: {r.Position():x}");
                    count++;
                    if (count > 60) throw new FormatException("BAD HEADER");
                }
            }
            //Log($"Unique: {r.Position():x}");

            Level = new FastLevel();
            Bkg = new E_BACKGROUND();
            var s = new MemoryStream(r.DecompressBlast((int)(r.BaseStream.Length - r.BaseStream.Position), header.Compressedsize));
            using var r2 = new BinaryReader(s);

            // read
            var fsh = r2.ReadS<FAST_SCENE_HEADER>();
            if (fsh.Version != FTS_VERSION) throw new FormatException("BAD MAGIC");
            if (fsh.SizeX != Bkg.XSize) throw new FormatException("BAD HEADER");
            if (fsh.SizeZ != Bkg.ZSize) throw new FormatException("BAD HEADER");
            Level.PlayerPos = fsh.PlayerPos;
            Level.MscenePos = fsh.MscenePos;
            Log($"Header2: {r2.Tell():x}, {sizeof(FAST_SCENE_HEADER)}");

            // textures
            var textures = Level.Textures = new E_TEXTURE[fsh.NumTextures];
            for (k = 0; k < textures.Length; k++)
            {
                var ftc = r2.ReadS<FAST_TEXTURE_CONTAINER>();
                textures[k] = new E_TEXTURE { Id = ftc.TcPtr, Path = ftc.Fic };
            }
            //Log($"Texture: {r2.Position():x}");

            // backg
            var backg = Bkg.Backg;
            for (j = 0; j < fsh.SizeZ; j++)
                for (i = 0; i < fsh.SizeX; i++)
                {
                    ref E_BKG_INFO bi = ref backg[i + j * fsh.SizeX];
                    var fsi = r2.ReadS<FAST_SCENE_INFO>();
                    //if (fsi.NumPoly > 0) Log($"F[{j},{i}]: {r2.Position():x}, {fsi.NumPoly}, {fsi.NumIAnchors}");
                    bi.NumIAnchors = (short)fsi.NumIAnchors;
                    bi.NumPoly = (short)fsi.NumPoly;
                    bi.Polydata = fsi.NumPoly > 0 ? new E_POLY[fsi.NumPoly] : null;
                    bi.Treat = 0;
                    bi.Nothing = fsi.NumPoly == 0;
                    bi.FrustrumMaxY = -99999999f;
                    bi.FrustrumMinY = 99999999f;
                    for (k = 0; k < fsi.NumPoly; k++)
                    {
                        var ep = r2.ReadS<FAST_EERIEPOLY>();
                        var tex = ep.TexPtr != 0
                            ? textures.FirstOrDefault(x => x.Id == ep.TexPtr)
                            : null;
                        ref E_POLY ep2 = ref bi.Polydata[k];
                        ep2.memset();
                        ep2.Room = ep.Room;
                        ep2.Area = ep.Area;
                        ep2.Norm = ep.Norm;
                        ep2.Norm2 = ep.Norm2;
                        ep2.Nrml = [ep.Nrml0, ep.Nrml1, ep.Nrml2, ep.Nrml3];
                        ep2.Tex = tex;
                        ep2.TransVal = ep.Transval;
                        ep2.Type = ep.Type;
                        ep2.V = [
                            new() { Color = 0xFFFFFFFF, Rhw = 1, Specular = 1, S = new Vector3(ep.V0.ssx, ep.V0.sy, ep.V0.ssz), T = new Vector2(ep.V0.stu, ep.V0.stv) },
                            new() { Color = 0xFFFFFFFF, Rhw = 1, Specular = 1, S = new Vector3(ep.V1.ssx, ep.V1.sy, ep.V1.ssz), T = new Vector2(ep.V1.stu, ep.V1.stv) },
                            new() { Color = 0xFFFFFFFF, Rhw = 1, Specular = 1, S = new Vector3(ep.V2.ssx, ep.V2.sy, ep.V2.ssz), T = new Vector2(ep.V2.stu, ep.V2.stv) },
                            new() { Color = 0xFFFFFFFF, Rhw = 1, Specular = 1, S = new Vector3(ep.V3.ssx, ep.V3.sy, ep.V3.ssz), T = new Vector2(ep.V3.stu, ep.V3.stv) },
                        ];

                        // clone v
                        ep2.Tv = (TLVERTEX[])ep2.V.Clone();
                        for (kk = 0; kk < 4; kk++) ep2.Tv[kk].Color = 0xFF000000;

                        // re-center
                        int to; float div;
                        if ((ep.Type & POLY.QUAD) != 0) { to = 4; div = 0.25f; }
                        else { to = 3; div = 0.333333333333f; }
                        ep2.Center = Vector3.Zero;
                        for (var h = 0; h < to; h++)
                        {
                            ep2.Center.X += ep2.V[h].S.X;
                            ep2.Center.Y += ep2.V[h].S.Y;
                            ep2.Center.Z += ep2.V[h].S.Z;
                            if (h != 0)
                            {
                                ep2.Max.X = Math.Max(ep2.Max.X, ep2.V[h].S.X);
                                ep2.Min.X = Math.Min(ep2.Min.X, ep2.V[h].S.X);
                                ep2.Max.Y = Math.Max(ep2.Max.Y, ep2.V[h].S.Y);
                                ep2.Min.Y = Math.Min(ep2.Min.Y, ep2.V[h].S.Y);
                                ep2.Max.Z = Math.Max(ep2.Max.Z, ep2.V[h].S.Z);
                                ep2.Min.Z = Math.Min(ep2.Min.Z, ep2.V[h].S.Z);
                            }
                            else
                            {
                                ep2.Min.X = ep2.Max.X = ep2.V[0].S.X;
                                ep2.Min.Y = ep2.Max.Y = ep2.V[0].S.Y;
                                ep2.Min.Z = ep2.Max.Z = ep2.V[0].S.Z;
                            }
                        }
                        ep2.Center.X *= div;
                        ep2.Center.Y *= div;
                        ep2.Center.Z *= div;

                        // distance
                        var dist = 0f; for (var h = 0; h < to; h++) dist = Math.Max(dist, Vector3.Distance(ep2.V[h].S, ep2.Center));
                        ep2.V[0].Rhw = dist;

                        // declare
                        DeclareEGInfo(Bkg, ep2.Center.X, ep2.Center.Y, ep2.Center.Z);
                        DeclareEGInfo(Bkg, ep2.V[0].S.X, ep2.V[0].S.Y, ep2.V[0].S.Z);
                        DeclareEGInfo(Bkg, ep2.V[1].S.X, ep2.V[1].S.Y, ep2.V[1].S.Z);
                        DeclareEGInfo(Bkg, ep2.V[2].S.X, ep2.V[2].S.Y, ep2.V[2].S.Z);
                        if ((ep.Type & POLY.QUAD) != 0) DeclareEGInfo(Bkg, ep2.V[3].S.X, ep2.V[3].S.Y, ep2.V[3].S.Z);
                    }

                    bi.IAnchors = fsi.NumIAnchors <= 0 ? null : r2.ReadPArray<int>("i", fsi.NumIAnchors);
                }
            //Log($"Background: {r2.Position():x}");

            // anchors
            Bkg.NumAnchors = fsh.NumAnchors;
            var anchors = Bkg.Anchors = fsh.NumAnchors > 0 ? new ANCHOR_DATA[fsh.NumAnchors] : null;
            for (i = 0; i < fsh.NumAnchors; i++)
            {
                ref ANCHOR_DATA a = ref anchors[i];
                var fad = r2.ReadS<FAST_ANCHOR_DATA>();
                a.Flags = fad.Flags;
                a.Pos = fad.Pos;
                a.NumLinked = fad.NumLinked;
                a.Height = fad.Height;
                a.Radius = fad.Radius;
                a.Linked = fad.NumLinked > 0 ? r2.ReadPArray<int>("i", fad.NumLinked) : null;
            }
            //Log($"Anchors: {r2.Position():x}");

            // rooms
            E_PORTAL_DATA portals = null;
            if (fsh.NumRooms > 0)
            {
                portals = Level.Portals = new E_PORTAL_DATA();
                portals.NumRooms = fsh.NumRooms;
                portals.Room = new E_ROOM_DATA[portals.NumRooms + 1];
                portals.NumTotal = fsh.NumPortals;
                var levelPortals = portals.Portals = new E_PORTALS[portals.NumTotal];
                for (i = 0; i < portals.NumTotal; i++)
                {
                    ref E_PORTALS p = ref levelPortals[i];
                    var epo = r2.ReadS<E_SAVE_PORTALS>();
                    p.memset();
                    p.Room1 = epo.Room1;
                    p.Room2 = epo.Room2;
                    p.UsePortal = epo.UsePortal;
                    p.Paddy = epo.Paddy;
                    p.Poly.Area = epo.Poly.Area;
                    p.Poly.Type = epo.Poly.Type;
                    p.Poly.TransVal = epo.Poly.TransVal;
                    p.Poly.Room = epo.Poly.Room;
                    p.Poly.Misc = epo.Poly.Misc;
                    p.Poly.Center = epo.Poly.Center;
                    p.Poly.Max = epo.Poly.Max;
                    p.Poly.Min = epo.Poly.Min;
                    p.Poly.Norm = epo.Poly.Norm;
                    p.Poly.Norm2 = epo.Poly.Norm2;
                    p.Poly.Nrml = [epo.Poly.Nrml0, epo.Poly.Nrml1, epo.Poly.Nrml2, epo.Poly.Nrml3];
                    p.Poly.V = [epo.Poly.V0, epo.Poly.V1, epo.Poly.V2, epo.Poly.V3];
                    p.Poly.Tv = [epo.Poly.Tv0, epo.Poly.Tv1, epo.Poly.Tv2, epo.Poly.Tv3];
                }
                for (i = 0; i < portals.NumRooms + 1; i++)
                {
                    var rd = portals.Room[i] = new E_ROOM_DATA();
                    var erd = r2.ReadS<E_SAVE_ROOM_DATA>();
                    rd.NumPortals = erd.NumPortals;
                    rd.NumPolys = erd.NumPolys;
                    rd.Portals = rd.NumPortals > 0 ? r2.ReadPArray<int>("i", rd.NumPortals) : null;
                    rd.EpData = rd.NumPolys > 0 ? r2.ReadSArray<EP_DATA>(rd.NumPolys) : null;
                }
            }
            //Log($"Portals: {r2.Position():x}");

            if (portals != null)
            {
                var numRoomDistance = Level.NumRoomDistance = portals.NumRooms + 1;
                Level.RoomDistance = new ROOM_DIST_DATA[numRoomDistance * numRoomDistance];
                for (var n = 0; n < numRoomDistance; n++)
                    for (var m = 0; m < numRoomDistance; m++)
                    {
                        var rdds = r2.ReadS<ROOM_DIST_DATA_SAVE>();
                        SetRoomDistance(Level, m, n, rdds.Distance, ref rdds.StartPos, ref rdds.EndPos);
                    }
            }
            else
            {
                Level.NumRoomDistance = 0;
                Level.RoomDistance = null;
            }
            //Log($"RoomDistance: {r2.Position():x}");
            ComputePolyIn();
            //PATHFINDER_Create();
            //PORTAL_Blend_Portals_And_Rooms();
            //ComputePortalVertexBuffer();
        }

        static void DeclareEGInfo(E_BACKGROUND bkg, float x, float y, float z)
        {
            var posx = (int)(float)(x * bkg.Xmul);
            if (posx < 0) return;
            else if (posx >= bkg.XSize) return;

            var posz = (int)(float)(z * bkg.Zmul);
            if (posz < 0) return;
            else if (posz >= bkg.ZSize) return;

            ref E_BKG_INFO eg = ref bkg.Backg[posx + posz * bkg.XSize];
            eg.Nothing = false;
        }

        static void SetRoomDistance(FastLevel level, long i, long j, float val, ref Vector3 p1, ref Vector3 p2)
        {
            if (i < 0 || j < 0 || i >= level.NumRoomDistance || j >= level.NumRoomDistance || level.RoomDistance == null) return;
            var offs = i + j * level.NumRoomDistance;
            ref ROOM_DIST_DATA rd = ref level.RoomDistance[offs];
            rd.StartPos = p1;
            rd.EndPos = p2;
            rd.Distance = val;
        }

        static void ComputePolyIn()
        {
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => [
                new("BinaryFTS", items: [
                    new($"Level: {Level}"),
                    new($"Bkg: {Bkg}"),
                ])
            ];
    }

    #endregion

    #region Binary_Tea

    public unsafe class Binary_Tea : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Tea(r));

        // https://github.com/OpenSourcedGames/Arx-Fatalis/blob/master/Sources/EERIE/EERIEAnim.cpp#L355
        public Binary_Tea(BinaryReader r)
        {
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => [
                new("BinaryTEA", items: [
                    new($"Type: X"),
                ])
            ];
    }

    #endregion
}

using GameX.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Volition.Formats.Descent
{
    public unsafe class Binary_Rdl : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Rdl(r));

        #region Headers

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct X_Header
        {
            public static (string, int) Struct = ("<3I", sizeof(X_Header));
            public uint Magic;
            public uint Version;
            public uint GeoOffset;

        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct X_Geo
        {
            public static (string, int) Struct = ("<2H", sizeof(X_Geo));
            public ushort NumVerts;
            public ushort NumSegments;
        }

        public class Side
        {
            public int Bitmap;
            public int Bitmap2;
            public Vector2[] Uvs = new Vector2[4];
            public double[] Lights = new double[4];

            public Side(BinaryReader r)
            {
                Bitmap = r.ReadUInt16();
                if ((Bitmap & 0x8000) != 0)
                {
                    Bitmap &= 0x7fff;
                    Bitmap2 = r.ReadUInt16();
                }
                else Bitmap2 = -1;
                for (var j = 0; j < 4; j++)
                {
                    Uvs[j] = r.ReadVector2();
                    Lights[j] = ReadInt16Fixed(r);
                }
            }
            public override string ToString()
                => $"[t: {Bitmap} t2: {Bitmap2} uv: {string.Join(",", Array.ConvertAll(Uvs, x => x.ToString()))} l: {string.Join(",", Array.ConvertAll(Lights, x => x.ToString()))}]";
        }

        public class Segment
        {
            public int[] ChildIdxs = new int[6]; // left, top, right, bottom, back, front
            public int[] VertIdxs = new int[8];
            public byte[] WallIds = new byte[6];
            public bool IsSpecial;
            public byte Special;
            public byte EcNum;
            public int Value;
            public double StaticLight;
            public Side[] Sides = new Side[6];

            public Segment(BinaryReader r)
            {
                var mask = r.ReadByte();
                for (var i = 0; i < 6; i++) ChildIdxs[i] = (mask & (1 << i)) != 0 ? r.ReadInt16() : -1;
                for (var i = 0; i < 8; i++) VertIdxs[i] = r.ReadInt16();
                IsSpecial = (mask & 64) != 0;
                if (IsSpecial)
                {
                    Special = r.ReadByte();
                    EcNum = r.ReadByte();
                    Value = r.ReadInt16();
                }
                StaticLight = ReadInt16Fixed(r);
                var wallMask = r.ReadByte();
                for (var i = 0; i < 6; i++) WallIds[i] = (wallMask & (1 << i)) != 0 ? r.ReadByte() : (byte)255;
                for (var i = 0; i < 6; i++)
                    if (ChildIdxs[i] == -1 || WallIds[i] != 255)
                        Sides[i] = new Side(r);
            }
            public override string ToString()
                => $"v: {string.Join(",", Array.ConvertAll(VertIdxs, x => x.ToString()))} w: {string.Join(",", Array.ConvertAll(WallIds, x => x.ToString()))} l: {StaticLight} {string.Join(",", Array.ConvertAll(Sides, x => x == null ? "-" : x.ToString()))}";
        }

        #endregion

        public Vector3[] Vectors;
        public Segment[] Segments;

        public Binary_Rdl(BinaryReader r)
        {
            const uint MAGIC = 0x0;

            var header = r.ReadS<X_Header>();
            if (header.Magic == MAGIC) throw new FormatException("BAD MAGIC");
            r.Seek(header.GeoOffset);
            var geo = r.ReadS<X_Geo>();
            Vectors = r.ReadTArray<Vector3>(sizeof(Vector3), geo.NumVerts);
            Segments = r.ReadFArray<Segment>(r => new Segment(r), geo.NumSegments);
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = this }),
            new MetaInfo($"{nameof(Binary_Rdl)}", items: new List<MetaInfo> {
                new MetaInfo($"Vectors: {Vectors.Length}"),
                new MetaInfo($"Segments: {Segments.Length}"),
            })
        };

        public override string ToString() => "OK";

        static double ReadInt16Fixed(BinaryReader r) => r.ReadInt16() / 4096.0;
    }
}

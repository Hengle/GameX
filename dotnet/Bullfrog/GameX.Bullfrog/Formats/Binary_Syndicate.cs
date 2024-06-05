using GameX.Formats;
using GameX.Meta;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace GameX.Bullfrog.Formats
{
    public unsafe class Binary_Syndicate : IHaveMetaInfo
    {
        public enum Kind { Fli, Font, Game, MapColumn, MapData, MapTile, Mission, Palette, Raw, Req, SoundData, SoundTab, SpriteAnim, SpriteFrame, SpriteElement, SpriteTab, SpriteData };

        public static Task<object> Factory_Fli(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.Fli));
        public static Task<object> Factory_Font(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.Font));
        public static Task<object> Factory_Game(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.Game));
        public static Task<object> Factory_MapColumn(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.MapColumn));
        public static Task<object> Factory_MapData(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.MapData));
        public static Task<object> Factory_MapTile(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.MapTile));
        public static Task<object> Factory_Mission(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.Mission));
        public static Task<object> Factory_Palette(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.Palette));
        public static Task<object> Factory_Raw(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.Raw));
        public static Task<object> Factory_Req(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.Req));
        public static Task<object> Factory_SoundData(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.SoundData));
        public static Task<object> Factory_SoundTab(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.SoundTab));
        public static Task<object> Factory_SpriteAnim(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.SpriteAnim));
        public static Task<object> Factory_SpriteFrame(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.SpriteFrame));
        public static Task<object> Factory_SpriteElement(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.SpriteElement));
        public static Task<object> Factory_SpriteTab(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.SpriteTab));
        public static Task<object> Factory_SpriteData(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, Kind.SpriteData));

        #region Headers

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct X_Palette
        {
            public static (string, int) Struct = ("<3x", sizeof(X_Palette));
            public byte R;
            public byte G;
            public byte B;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        unsafe struct X_Font
        {
            public static (string, int) Struct = ("<H3x", sizeof(X_Font));
            public ushort Offset;
            public byte Width;
            public byte Height;
            public byte LineOffset;
        };

        #endregion

        public Binary_Syndicate(BinaryReader r, Kind kind)
        {
            using var r2 = new BinaryReader(new MemoryStream(Rnc.Read(r)));
            switch (kind)
            {
                case Kind.Palette:
                    var palette = r2.ReadSArray<X_Palette>(256);
                    break;
                case Kind.Font:
                    var fonts = r2.ReadSArray<X_Font>(128);
                    var data = r2.ReadToEnd();
                    break;
                case Kind.Req:
                    //var fonts = r.ReadSArray<X_Font>(128);
                    //var data = r.ReadToEnd();
                    break;
            }
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => new List<MetaInfo> {
                new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "Bullfrog" }),
                new MetaInfo("Bullfrog", items: new List<MetaInfo> {
                    //new MetaInfo($"Records: {Records.Length}"),
                })
            };
    }
}
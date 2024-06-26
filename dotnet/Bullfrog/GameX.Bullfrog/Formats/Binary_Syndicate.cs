using GameX.Meta;
using MathNet.Numerics;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static GameX.Bullfrog.Formats.Binary_Syndicate;

//using static GameX.Bullfrog.Formats.PakBinary_Bullfrog;
using static OpenStack.Debug;

namespace GameX.Bullfrog.Formats
{
    public unsafe class Binary_Syndicate : IHaveMetaInfo
    {
        public enum Kind { Fli, Font, Game, MapColumn, MapData, MapTile, Mission, Palette, Raw, Req, SoundData, SoundTab, SpriteAnim, SpriteFrame, SpriteElement, SpriteTab, SpriteData };

        public static Task<object> Factory_Fli(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.Fli));
        public static Task<object> Factory_Font(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.Font));
        public static Task<object> Factory_Game(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.Game));
        public static Task<object> Factory_MapColumn(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.MapColumn));
        public static Task<object> Factory_MapData(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.MapData));
        public static Task<object> Factory_MapTile(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.MapTile));
        public static Task<object> Factory_Mission(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.Mission));
        public static Task<object> Factory_Palette(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.Palette));
        public static Task<object> Factory_Raw(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.Raw));
        public static Task<object> Factory_Req(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.Req));
        public static Task<object> Factory_SoundData(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.SoundData));
        public static Task<object> Factory_SoundTab(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.SoundTab));
        public static Task<object> Factory_SpriteAnim(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.SpriteAnim));
        public static Task<object> Factory_SpriteFrame(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.SpriteFrame));
        public static Task<object> Factory_SpriteElement(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.SpriteElement));
        public static Task<object> Factory_SpriteTab(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.SpriteTab));
        public static Task<object> Factory_SpriteData(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Syndicate(r, f, s, Kind.SpriteData));

        #region Headers

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_Palette
        {
            public static (string, int) Struct = ("<3x", sizeof(X_Palette));
            public byte R;
            public byte G;
            public byte B;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_Font
        {
            public static (string, int) Struct = ("<H3x", sizeof(X_Font));
            public ushort Offset;
            public byte Width;
            public byte Height;
            public byte LineOffset;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_MapData
        {
            public static (string, int) Struct = ("<3I", sizeof(X_MapData));
            public uint MaxX;
            public uint MaxY;
            public uint MaxZ;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_SoundTab
        {
            public static (string, int) Struct = ("<I28x", sizeof(X_SoundTab));
            public uint Size;
            public fixed byte Unknown[28];

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public static void Patch(int i, byte[] sample)
            {
                // patch sample rates
                if (i == 13) sample[0x1e] = 0x9c;
                else if (i == 24) sample[0x1e] = 0x9c;
                else if (i == 25) sample[0x1e] = 0x38;
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_MapTile
        {
            public static (string, int) Struct = ("<6I", sizeof(X_MapTile));
            public uint Offsets0;
            public uint Offsets1;
            public uint Offsets2;
            public uint Offsets3;
            public uint Offsets4;
            public uint Offsets5;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_SpriteTab
        {
            public static (string, int) Struct = ("<I2x", sizeof(X_SpriteTab));
            public uint Offset;
            public byte Width;
            public byte Height;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Stride() => Width <= 0 ? 0 : (Width % 8) != 0 ? ((Width / 8) + 1) * 8 : Width;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_SpriteElement
        {
            public static (string, int) Struct = ("<5H", sizeof(X_SpriteElement));
            public ushort Sprite;
            public ushort OffsetX;
            public ushort OffsetY;
            public ushort Flipped;
            public ushort NextElement;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_SpriteFrame
        {
            public static (string, int) Struct = ("<H2x2H", sizeof(X_SpriteFrame));
            public ushort FirstElement;
            public byte Width;
            public byte Height;
            public ushort Flags;
            public ushort NextElement;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_GameHeader
        {
            public static (string, int) Struct = ("<8x16384HH", sizeof(X_GameHeader));
            public fixed byte Header[8];
            public fixed ushort Offsets[16384]; //: 128 * 128
            public ushort OffsetRef;
        }




        ///*! Constant for field Scenario::type :  Use vehicle to go somewhere.*/
        //static const int kScenarioTypeUseVehicle = 0x02;
        ///*! Constant for field Scenario::type :  Target has escape the map.*/
        //static const int kScenarioTypeEscape = 0x07;
        ///*! Constant for field Scenario::type : this is a trigger. 
        // * Agents will trigger it when they enter the circle defined by the center and a fixed radius.*/
        //static const int kScenarioTypeTrigger = 0x08;
        ///*! Constant for field Scenario::type : Reset all scripted action.*/
        //static const int kScenarioTypeReset = 0x09;


        public enum PedestrianLocation : byte
        {
            OnMap = 0x04,           // Ped is on the map
            InVehicle = 0x05,       // Ped is in a vehicle
            AboveWalkSurf = 0x0C,   // Located level above possible walking surface
            NotVisible = 0x0D,      // They are not visible/present on original map (on water located)
            // 0x0D and 0x0C are excluded from being loaded
        }

        public enum PedestrianState : byte
        {
            Standing = 0x0,          // Ped is standing
            Walking = 0x10,         // Ped is walking
            Dead = 0x11,            // Ped is dead
        }

        public enum PedestrianType : byte
        {
            Ped = 0x01,             // Ped
            Vehicle = 0x02,         // Vehicle
            Weapon = 0x04,          // Weapon
            Object = 0x05,          // Object; allow to display a target, a pickup, and for minimap
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_GamePedestrian
        {
            public static (string, int) Struct = ("<8x16384HH", sizeof(X_GamePedestrian));
            public ushort OffsetNext;           // 'offset + 32774' gives the offset in this file of the next object
            public ushort OffsetPrev;           // 'offset + 32774' gives the offset in this file of the previous object (sometimes weapon, or the next target for example ???)
            public ushort TileX;
            public ushort TileY;
            public ushort TileZ;                // tile = (uint16)/128, offz =(uint16)%128 or offz = mapposz[0] & 0x1F
            public PedestrianLocation Location;
            public PedestrianState State;
            public ushort Unkn3;                // nothing changes when this changes
            public ushort IndexBaseAnim;        // index in (HSTA-0.ANI)
            public ushort IndexCurrentFrame;    // index in (HFRA-0.ANI)
            public ushort IndexCurrentAnim;     // index in (HSTA-0.ANI)
            public ushort Health;
            public ushort OffsetLastEnemy;
            public PedestrianType Type;
            public byte Status;                 // this can be sub type(?)
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_GameVehicle
        {
            public static (string, int) Struct = ("<8x16384HH", sizeof(X_GameVehicle));
            public fixed byte Header[8];
            public fixed ushort Offsets[16384]; //: 128 * 128
            public ushort OffsetRef;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_GameObject
        {
            public static (string, int) Struct = ("<8x16384HH", sizeof(X_GameObject));
            public fixed byte Header[8];
            public fixed ushort Offsets[16384]; //: 128 * 128
            public ushort OffsetRef;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_GameWeapon
        {
            public static (string, int) Struct = ("<8x16384HH", sizeof(X_GameWeapon));
            public fixed byte Header[8];
            public fixed ushort Offsets[16384]; //: 128 * 128
            public ushort OffsetRef;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_GameSfx
        {
            public static (string, int) Struct = ("<8x16384HH", sizeof(X_GameSfx));
            public fixed byte Header[8];
            public fixed ushort Offsets[16384]; //: 128 * 128
            public ushort OffsetRef;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_GameScenario
        {
            public static (string, int) Struct = ("<8x16384HH", sizeof(X_GameScenario));
            public fixed byte Header[8];
            public fixed ushort Offsets[16384]; //: 128 * 128
            public ushort OffsetRef;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_GameMapinfo
        {
            public static (string, int) Struct = ("<8x16384HH", sizeof(X_GameMapinfo));
            public fixed byte Header[8];
            public fixed ushort Offsets[16384]; //: 128 * 128
            public ushort OffsetRef;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public unsafe struct X_GameObjective
        {
            public static (string, int) Struct = ("<8x16384HH", sizeof(X_GameObjective));
            public fixed byte Header[8];
            public fixed ushort Offsets[16384]; //: 128 * 128
            public ushort OffsetRef;
        }

        #endregion

        #region Objects

        public class Palette
        {
            public X_Palette[] Records;
        }

        public class Font
        {
            public X_Font[] Records;
            public byte[] Data;
        }

        public class MapData
        {
            public int MapId;
            public int MaxX;
            public int MaxY;
            public int MaxZ;
            public byte[] Tiles;
            public int Width;
            public int Height;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void PatchTile(int x, int y, int z, byte tile) => Tiles[(y * MaxX + x) * MaxZ + z] = tile;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public MapData Patch()
            {
                // patch for "YUKON" map
                if (MapId == 0x27)
                {
                    PatchTile(60, 63, 1, 0x27);
                    PatchTile(61, 63, 1, 0x27);
                    PatchTile(62, 63, 1, 0x27);
                    PatchTile(60, 64, 1, 0x27);
                    PatchTile(61, 64, 1, 0x27);
                    PatchTile(62, 64, 1, 0x27);
                    PatchTile(60, 65, 1, 0x27);
                    PatchTile(61, 65, 1, 0x27);
                    PatchTile(62, 65, 1, 0x27);
                    PatchTile(60, 66, 1, 0x27);
                    PatchTile(61, 66, 1, 0x27);
                    PatchTile(62, 66, 1, 0x27);
                    PatchTile(60, 67, 1, 0x27);
                    PatchTile(61, 67, 1, 0x27);
                    PatchTile(62, 67, 1, 0x27);
                }
                // patch for "INDONESIA" map
                else if (MapId == 0x5B)
                {
                    PatchTile(49, 27, 2, 0);
                    PatchTile(49, 28, 2, 0);
                    PatchTile(49, 29, 2, 0);
                }
                return this;
            }
        }

        public enum ColType : byte
        {
            None,
            SlopeSN,
            SlopeNS,
            SlopeEW,
            SlopeWE,
            Ground,
            RoadSideEW,
            RoadSideWE,
            RoadSideSN,
            RoadSideNS,
            Wall,
            RoadCurve,
            HandrailLight,
            Roof,
            RoadPedCross,
            RoadMark,
            Unknown
        }

        public class MapTile
        {
            public byte[][] Tiles;
            public ColType[] Types;
        }

        public class Sprite
        {
            public int Width;
            public int Height;
            public int Stride;
            public byte[] Pixels;
        }

        public class SpriteElement
        {
            public int Sprite;
            public int OffsetX;
            public int OffsetY;
            public bool Flipped;
            public int NextElement;
        }

        public class SpriteFrame
        {
            public int FirstElement;
            public int Width;
            public int Height;
            public uint Flags;
            public int NextElement;
        }

        public class Mission
        {
            public int[] InfoCosts;
            public int[] EnhtsCosts;
            public string[] Briefings;
        }

        public class Game
        {
            public X_GameHeader Header;
            public X_GamePedestrian[] Pedestrians;
            public X_GameVehicle[] Vehicles;
            public X_GameObject[] Objects;
            public X_GameWeapon[] Weapons;
            public X_GameSfx[] Sfxs;
            public X_GameScenario[] Scenarios;
            public X_GameMapinfo Mapinfo;
            public X_GameObjective[] Objectives;
        }

        #endregion

        public object Obj;

        public Binary_Syndicate(BinaryReader r, FileSource f, PakFile s, Kind kind)
        {
            const int NUMOFTILES = 256;
            const int PIXELS_PER_BLOCK = 8;
            const int COLOR_BYTES_PER_BLOCK = PIXELS_PER_BLOCK / 2, ALPHA_BYTES_PER_BLOCK = PIXELS_PER_BLOCK / 8;
            const int BLOCK_LENGTH = COLOR_BYTES_PER_BLOCK + ALPHA_BYTES_PER_BLOCK;
            const int TILE_WIDTH = 64, TILE_HEIGHT = 48, SUBTILE_WIDTH = 32, SUBTILE_HEIGHT = 16;
            const int SUBTILE_ROW_LENGTH = BLOCK_LENGTH * SUBTILE_WIDTH / PIXELS_PER_BLOCK;

            using var r2 = new BinaryReader(new MemoryStream(Rnc.Read(r)));
            var streamSize = r2.BaseStream.Length;
            switch (kind)
            {
                case Kind.SoundData: throw new FormatException("Please load the .TAB");
                case Kind.SoundTab: // Skips Kind.SoundData
                    {
                        var data = ((MemoryStream)s.LoadFileData(Path.ChangeExtension(f.Path, ".DAT")).Result).ToArray();

                        const int OFFSET = 58;
                        var sounds = new List<byte[]>();
                        r2.Seek(OFFSET);
                        var offset = 0;
                        var count = (int)((streamSize - OFFSET) / sizeof(X_SoundTab)) + 1;
                        Obj = r2.ReadSArray<X_SoundTab>(count).Select((t, i) =>
                        {
                            if (t.Size <= 144) return null;
                            var sample = data[offset..(offset + (int)t.Size)];
                            offset += (int)t.Size;
                            X_SoundTab.Patch(i, sample);
                            return sample;
                        }).Where(t => t != null).ToArray();
                        break;
                    }
                case Kind.Palette:
                    Obj = new Palette
                    {
                        Records = r2.ReadSArray<X_Palette>(256)
                    };
                    break;
                case Kind.Font:
                    Obj = new Font
                    {
                        Records = r2.ReadSArray<X_Font>(128),
                        Data = r2.ReadToEnd(),
                    };
                    break;
                case Kind.Req:
                    //var fonts = r2.ReadSArray<X_Font>(128);
                    //var data = r2.ReadToEnd();
                    Obj = null;
                    break;
                case Kind.MapData:
                    {
                        var mapId = int.Parse(Path.GetFileNameWithoutExtension(f.Path)[3..]);
                        var t = r2.ReadS<X_MapData>();
                        int maxX = (int)t.MaxX, maxY = (int)t.MaxY, maxZ = (int)t.MaxZ;
                        var width = (maxX + maxY) * (TILE_WIDTH / 2);
                        var height = (maxX + maxY + maxZ) * TILE_HEIGHT / 3;
                        var data = r2.ReadToEnd(); r2.Seek(12);
                        var lookups = r2.ReadTArray<uint>(sizeof(uint), maxX * maxY);
                        // get tiles
                        // note: increased map height by 1 to enable range check on higher tiles
                        var tiles = new byte[maxX * maxY * (maxZ + 1)];
                        for (int h = 0, zr = maxZ + 1; h < maxY; h++)
                            for (int w = 0; w < maxX; w++)
                                for (int z = 0, idx = h * maxX + w; z < maxZ; z++)
                                {
                                    var tileNum = data[lookups[idx] + z];
                                    tiles[idx * zr + z] = tileNum;
                                }
                        // add buffer
                        maxZ++;
                        for (int h = 0, z = maxZ - 1; h < maxY; h++)
                            for (int w = 0; w < maxX; w++)
                                tiles[(h * maxX + w) * maxZ + z] = 0;
                        // set obj
                        Obj = new MapData
                        {
                            MapId = mapId,
                            Width = width,
                            Height = height,
                            MaxX = maxX,
                            MaxY = maxY,
                            MaxZ = maxZ,
                            Tiles = tiles,
                        }.Patch();
                        break;
                    }
                case Kind.MapColumn: Obj = r2.ReadToEnd().Cast<ColType>().ToArray(); break;
                case Kind.MapTile: // Loads Kind.MapColumn
                    {
                        var types = (ColType[])s.LoadFileObject<Binary_Syndicate>(f.Path.Replace("HBLK", "COL")).Result.Obj;

                        static void UnpackBlock(byte[] data, Span<byte> pixels)
                        {
                            for (var j = 0; j < 4; ++j)
                                for (var i = 0; i < 8; ++i)
                                    pixels[j * 8 + i] = BitValue(data[j], 7 - i) == 0
                                        ? (byte)(
                                            (byte)((BitValue(data[4 + j], 7 - i) << 0) & 0xff) |
                                            (byte)((BitValue(data[8 + j], 7 - i) << 1) & 0xff) |
                                            (byte)((BitValue(data[12 + j], 7 - i) << 2) & 0xff) |
                                            (byte)((BitValue(data[16 + j], 7 - i) << 3) & 0xff))
                                        : (byte)0xff; // transparent
                        }

                        static void LoadSubTile(BinaryReader r2, uint offset, int index, int stride, byte[] pixels)
                        {
                            r2.Seek(offset);
                            for (var i = 0; i < SUBTILE_HEIGHT; i++)
                                UnpackBlock(r2.ReadBytes(SUBTILE_ROW_LENGTH), pixels.AsSpan(index + (SUBTILE_HEIGHT - 1 - i) * stride));
                        }

                        var tiles = r2.ReadSArray<X_MapTile>(NUMOFTILES).Select(t =>
                        {
                            var b = new byte[TILE_WIDTH * TILE_HEIGHT];
                            LoadSubTile(r2, t.Offsets0, 2 * SUBTILE_HEIGHT * TILE_WIDTH + 0 * SUBTILE_WIDTH, TILE_WIDTH, b);
                            LoadSubTile(r2, t.Offsets1, 1 * SUBTILE_HEIGHT * TILE_WIDTH + 0 * SUBTILE_WIDTH, TILE_WIDTH, b);
                            LoadSubTile(r2, t.Offsets2, 0 * SUBTILE_HEIGHT * TILE_WIDTH + 0 * SUBTILE_WIDTH, TILE_WIDTH, b);
                            LoadSubTile(r2, t.Offsets3, 2 * SUBTILE_HEIGHT * TILE_WIDTH + 1 * SUBTILE_WIDTH, TILE_WIDTH, b);
                            LoadSubTile(r2, t.Offsets4, 1 * SUBTILE_HEIGHT * TILE_WIDTH + 1 * SUBTILE_WIDTH, TILE_WIDTH, b);
                            LoadSubTile(r2, t.Offsets5, 0 * SUBTILE_HEIGHT * TILE_WIDTH + 1 * SUBTILE_WIDTH, TILE_WIDTH, b);
                            return b;
                        }).ToArray();
                        Obj = new MapTile
                        {
                            Tiles = tiles,
                            Types = types,
                        };
                        break;
                    }
                case Kind.SpriteData: Obj = new MemoryStream(r2.ReadToEnd()); break;
                case Kind.SpriteTab: // Loads Kind.SpriteData
                    {
                        using var r3 = new BinaryReader((MemoryStream)s.LoadFileObject<Binary_Syndicate>(Path.ChangeExtension(f.Path, ".DAT")).Result.Obj);

                        static void UnpackBlock(byte[] data, Span<byte> pixels)
                        {
                            for (var i = 0; i < 8; ++i)
                                pixels[i] = BitValue(data[0], 7 - i) == 0
                                        ? (byte)(
                                            (byte)((BitValue(data[1], 7 - i) << 0) & 0xff) |
                                            (byte)((BitValue(data[2], 7 - i) << 1) & 0xff) |
                                            (byte)((BitValue(data[3], 7 - i) << 2) & 0xff) |
                                            (byte)((BitValue(data[4], 7 - i) << 3) & 0xff))
                                        : (byte)0xff; // transparent
                        }

                        static Sprite LoadSprite(BinaryReader r, ref X_SpriteTab t, bool rle)
                        {
                            int width = t.Width, height = t.Height, stride = t.Stride();
                            if (width == 0 || height == 0) return new Sprite();
                            var pixels = new byte[stride * height];
                            r.Seek(t.Offset);
                            if (rle)
                                for (var i = 0; i < height; i++)
                                {
                                    var spriteWidth = width;
                                    var currentPixel = i * stride;

                                    // first
                                    var b = r.ReadByte();
                                    var runLength = b < 128 ? b : -(256 - b);
                                    while (runLength != 0)
                                    {
                                        spriteWidth -= runLength;

                                        // pixel run
                                        if (runLength > 0)
                                        {
                                            if (currentPixel < 0) currentPixel = 0;
                                            if (currentPixel + runLength > height * stride) runLength = height * stride - currentPixel;
                                            for (var j = 0; j < runLength; j++) pixels[currentPixel++] = r.ReadByte();
                                        }
                                        // transparent run
                                        else if (runLength < 0)
                                        {
                                            runLength *= -1;
                                            if (currentPixel < 0) currentPixel = 0;
                                            if (currentPixel + runLength > height * stride) runLength = height * stride - currentPixel;
                                            for (var j = 0; j < runLength; j++) pixels[currentPixel++] = 0xff;
                                            new Span<byte>(pixels, currentPixel, runLength).Fill(0xff);
                                            currentPixel += runLength;
                                        }
                                        // end of the row
                                        else if (runLength == 0)
                                            spriteWidth = 0;

                                        // next
                                        b = r.ReadByte();
                                        runLength = b < 128 ? b : -(256 - b);
                                    }
                                }
                            else
                                for (var j = 0; j < height; ++j)
                                {
                                    var currentPixel = j * stride;
                                    for (var i = 0; i < width; i += PIXELS_PER_BLOCK)
                                    {
                                        UnpackBlock(r.ReadBytes(BLOCK_LENGTH), pixels.AsSpan(currentPixel));
                                        currentPixel += PIXELS_PER_BLOCK;
                                    }
                                }
                            return new Sprite
                            {
                                Width = width,
                                Height = height,
                                Stride = stride,
                                Pixels = pixels,
                            };
                        }

                        var count = (int)(streamSize / sizeof(X_SpriteTab));
                        Obj = r2.ReadSArray<X_SpriteTab>(count).Select(t =>
                            LoadSprite(r3, ref t, false)
                        ).ToArray();
                        break;
                    }
                case Kind.SpriteElement:
                    {
                        var count = (int)(streamSize / sizeof(X_SpriteElement));
                        Obj = r2.ReadSArray<X_SpriteElement>(count).Select(t => new SpriteElement
                        {
                            Sprite = t.Sprite / 6,
                            OffsetX = (t.OffsetX & (1 << 15)) != 0 ? -(65536 - t.OffsetX) : t.OffsetX,
                            OffsetY = (t.OffsetY & (1 << 15)) != 0 ? -(65536 - t.OffsetY) : t.OffsetY,
                            Flipped = t.Flipped != 0,
                            NextElement = t.NextElement,
                        }).ToArray();
                        break;
                    }
                case Kind.SpriteFrame:
                    {
                        var count = (int)(streamSize / sizeof(X_SpriteFrame));
                        Obj = r2.ReadSArray<X_SpriteFrame>(count).Select(t => new SpriteFrame
                        {
                            FirstElement = t.FirstElement,
                            Width = t.Width,
                            Height = t.Height,
                            Flags = t.Flags,
                            NextElement = t.NextElement,
                        }).ToArray();
                        break;
                    }
                case Kind.SpriteAnim:
                    {
                        var count = (int)(streamSize / sizeof(ushort));
                        Obj = r2.ReadTArray<ushort>(sizeof(ushort), count);
                        break;
                    }
                case Kind.Mission:
                    {
                        var parts = Encoding.ASCII.GetString(r2.ReadToEnd()).Split("\n|\n");
                        Obj = new Mission
                        {
                            InfoCosts = parts[0].Split('\n').Select(UnsafeX.Atoi).ToArray(),
                            EnhtsCosts = parts[1].Split('\n').Select(UnsafeX.Atoi).ToArray(),
                            Briefings = parts[2..],
                        };
                        break;
                    }
                case Kind.Game:
                    {
                        Obj = new Game
                        {
                            Header = r2.ReadS<X_GameHeader>(),
                            Pedestrians = r2.ReadSArray<X_GamePedestrian>(256),
                            Vehicles = r2.ReadSArray<X_GameVehicle>(64),
                            Objects = r2.ReadSArray<X_GameObject>(400),
                            Weapons = r2.ReadSArray<X_GameWeapon>(512),
                            Sfxs = r2.ReadSArray<X_GameSfx>(256),
                            Scenarios = r2.ReadSArray<X_GameScenario>(2048),
                            Mapinfo = r2.Skip(448).ReadS<X_GameMapinfo>(),
                            Objectives = r2.ReadSArray<X_GameObjective>(10),
                        };
                        break;
                    }
            }
        }

        //using var F2 = File.CreateText("C:\\T_\\FROG\\Sprite2.txt");
        //F2.Write($"{currentPixel:000}\n");

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static uint BitValue(uint value, int index) => (value >> index) & 1;

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
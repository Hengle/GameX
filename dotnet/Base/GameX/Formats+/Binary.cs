using GameX.Meta;
using GameX.Platforms;
using OpenStack.Gfx.Renders;
using OpenStack.Gfx.Textures;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static OpenStack.Debug;

namespace GameX.Formats
{
    #region Binary_Bik

    public class Binary_Bik(BinaryReader r, int fileSize) : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Bik(r, (int)f.FileSize));

        public readonly byte[] Data = r.ReadBytes(fileSize);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "BIK Video" }),
        ];
    }

    #endregion

    #region Binary_Dds

    // https://github.com/paroj/nv_dds/blob/master/nv_dds.cpp
    public class Binary_Dds : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Dds(r));

        public Binary_Dds(BinaryReader r, bool readMagic = true)
        {
            (Header, HeaderDXT10, Format, Bytes) = DDS_HEADER.Read(r, readMagic);
            var mipMaps = Math.Max(1, (int)Header.dwMipMapCount);
            var offset = 0;
            Spans = new Range[mipMaps];
            int width = (int)Header.dwWidth, height = (int)Header.dwHeight;
            for (var i = 0; i < mipMaps; i++)
            {
                int w = width >> i, h = height >> i;
                if (w == 0 || h == 0) { Spans[i] = -1..; continue; }
                var size = ((w + 3) / 4) * ((h + 3) / 4) * Format.blockSize;
                var remains = Math.Min(size, Bytes.Length - offset);
                Spans[i] = remains > 0 ? offset..(offset + remains) : -1..;
                offset += remains;
            }
            Width = width;
            Height = height;
            MipMaps = mipMaps;
        }

        readonly DDS_HEADER Header;
        readonly DDS_HEADER_DXT10? HeaderDXT10;
        readonly (object type, int blockSize, object gl, object vulken, object unity, object unreal) Format;
        readonly byte[] Bytes;
        readonly Range[] Spans;

        public int Width { get; }
        public int Height { get; }
        public int Depth => 0;
        public int MipMaps { get; }
        public TextureFlags Flags => 0;

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
            => (Bytes, (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.StereoKit => throw new NotImplementedException("StereoKit"),
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, Spans);
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new("Texture", items: [
                new($"Format: {Format.type}"),
                new($"Width: {Width}"),
                new($"Height: {Height}"),
                new($"Mipmaps: {MipMaps}"),
            ]),
        ];
    }

    #endregion

    #region Binary_Fsb

    public class Binary_Fsb(BinaryReader r, int fileSize) : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Fsb(r, (int)f.FileSize));

        public readonly byte[] Data = r.ReadBytes(fileSize);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "FSB Audio" }),
        ];
    }

    #endregion

    #region Binary_Iif

    // https://en.wikipedia.org/wiki/Interchange_File_Format
    public unsafe class Binary_Iif : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Iif(r, (int)f.FileSize));

        #region Header

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct Chunk
        {
            public const uint FORM = 0x4d524f46;
            public const uint CAT_ = 0x20544143;
            public const uint XDIR = 0x52494458;
            public const uint XMID = 0x44494d58;
            public const uint INFO = 0x4f464e49;
            public const uint TIMB = 0x424d4954;
            public const uint EVNT = 0x544e5645;
            public static (string, int) Struct = (">4xI", sizeof(Chunk));
            public uint Id;        // 'FORM' | 'CAT '
            public int Size;      // Size of the chunk
            public int ActualSize => Size + (Size & 1);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct TIMB
        {
            public static (string, int) Struct = ("<2x", sizeof(TIMB));
            public byte PatchNumber;
            public byte TimbreBank;
        }

        public class MidiEvent
        {
            public int Time;
            public byte Status;
            public byte Data0;
            public byte Data1;
            public int Length;
            public byte[] Stream;
            public MidiEvent Next;
            public MidiEvent(int time) => Time = time;
        }

        public enum EV : byte
        {
            NOTE_OFF = 0x80,
            NOTE_ON = 0x90,
            POLY_PRESS = 0xa0,
            CONTROL = 0xb0,
            PROGRAM = 0xc0,
            CHAN_PRESS = 0xd0,
            PITCH = 0xe0,
            SYSEX = 0xf0,
            ESC = 0xf7, // SysEx event continuation
            META = 0xff  // MetaEvent
        }

        enum EVT : byte
        {
            OUTPUT_CABLE = 0x21,
            EOT = 0x2f, TRACK_END = EOT,
            TEMPO = 0x51, SET_TEMPO = TEMPO,
            TIME_SIG = 0x58,
            KEYSIG = 0x59
        }

        #endregion

        public Binary_Iif(BinaryReader r, int fileSize) => Read(r, fileSize);

        public int Tracks;
        public MidiEvent[] Events;
        public short[] Timing;
        public TIMB[][] Timbres;
        public MidiEvent TheList;
        int CurTrack; // used during load of multi-track XMI's (e.g. syngame.xmi)
        MidiEvent CurEvent;
        MidiEvent CurEventList;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = this }),
        ];

        #region Read

        void AllocData()
        {
            if (Events != null) return;
            Events = new MidiEvent[Tracks];
            Timing = new short[Tracks];
            Timbres = new TIMB[Tracks][];
        }

        //StreamWriter F;
        //FileStream F2;
        //static string Str(uint v) => Encoding.ASCII.GetString(BitConverter.GetBytes(v));

        bool Read(BinaryReader r, int fileSize)
        {
            //F = File.CreateText("C:\\T_\\FROG\\Proj2.txt");
            Tracks = 1; // default to 1 track, in case there is no XDIR chunk
            do
            {
                var chunk = r.ReadS<Chunk>();
                var position = r.BaseStream.Position;
                //F.Write($"\nCHUNK: {Str(chunk.Id)}\n");
                var result = chunk.Id switch
                {
                    Chunk.FORM => HandleChunkFORM(r, chunk.Size),
                    Chunk.CAT_ => HandleChunkCAT(r, chunk.Size),
                    _ => false,
                };
                if (!result) return false; // something failed

                // seek
                var newPosition = position + chunk.ActualSize;
                if (r.BaseStream.Position != newPosition) r.Seek(position + chunk.ActualSize);
            } while (r.BaseStream.Position < fileSize);
            //F.Close();

            //WriteAll();
            //Environment.Exit(0);
            return true;
        }

        bool HandleChunkFORM(BinaryReader r, int chunkSize)
        {
            //F.Write($" FORM: {r.Peek(_ => Str(_.ReadUInt32()))}\n");
            return r.ReadUInt32() switch
            {
                Chunk.XDIR => HandleChunkXDIR(r, chunkSize - 4),
                Chunk.XMID => HandleChunkXMID(r, chunkSize - 4),
                _ => false,
            };
        }

        bool HandleChunkCAT(BinaryReader r, int chunkSize)
        {
            var basePosition = r.BaseStream.Position;
            var endPosition = r.BaseStream.Position + chunkSize;
            do
            {
                var chunk = r.ReadS<Chunk>();
                var position = r.BaseStream.Position;
                if (chunk.Id == Chunk.XMID)
                {
                    r.Skip(-4); position -= 4;
                    chunk.Size = (int)(chunkSize - (position - basePosition));
                }
                //F.Write($" CAT_: {Str(chunk.Id)}\n");
                var result = chunk.Id switch
                {
                    Chunk.FORM => HandleChunkFORM(r, chunk.Size),
                    Chunk.XMID => HandleChunkXMID(r, chunk.Size),
                    _ => false,
                };
                if (!result) return false; // something failed

                // seek
                var newPosition = position + chunk.ActualSize;
                if (r.BaseStream.Position != newPosition) r.Seek(position + chunk.ActualSize);
            } while (r.BaseStream.Position < endPosition);
            return true;
        }

        bool HandleChunkXDIR(BinaryReader r, int chunkSize)
        {
            if (chunkSize != 10) return false;
            var chunk = r.ReadS<Chunk>();
            //F.Write($" XDIR: {Str(chunk.Id)}\n");
            if (chunk.Size == Chunk.INFO || chunk.Size != 2) return false;
            Tracks = r.ReadUInt16();
            AllocData();
            return true;
        }

        bool HandleChunkXMID(BinaryReader r, int chunkSize)
        {
            var endPosition = r.BaseStream.Position + chunkSize;
            do
            {
                var chunk = r.ReadS<Chunk>();
                var position = r.BaseStream.Position;
                //F.Write($" XMID: {Str(chunk.Id)}\n");
                var result = chunk.Id switch
                {
                    Chunk.FORM => HandleChunkFORM(r, chunk.Size),
                    Chunk.TIMB => HandleChunkTIMB(r, chunk.Size),
                    Chunk.EVNT => HandleChunkEVNT(r, chunk.Size),
                    _ => false,
                };
                if (!result) return false; // something failed

                // seek
                var newPosition = position + chunk.ActualSize;
                if (r.BaseStream.Position != newPosition) r.Seek(position + chunk.ActualSize);
            } while (r.BaseStream.Position < endPosition);
            return true;
        }

        bool HandleChunkTIMB(BinaryReader r, int chunkSize)
        {
            var numTimbres = r.ReadUInt16();
            if ((numTimbres << 1) + 2 != chunkSize) return false;
            Timbres[CurTrack] = r.ReadSArray<TIMB>(numTimbres);
            return true;
        }

        bool HandleChunkEVNT(BinaryReader r, int chunkSize)
        {
            AllocData(); // precaution, in case XDIR wasn't found
            CurEventList = null;
            var timing = ReadEventList(r);
            if (timing == 0) { Log("Unable to convert data\n"); return false; }
            Timing[CurTrack] = timing;
            Events[CurTrack] = CurEventList;
            CurTrack++;
            return true; // Return how many tracks were converted
        }

        MidiEvent CreateNewEvent(int time)
        {
            if (CurEventList == null) return CurEvent = CurEventList = new MidiEvent(time);
            if (CurEvent.Time > time) CurEvent = CurEventList;
            while (CurEvent.Next != null)
            {
                if (CurEvent.Next.Time > time) return CurEvent = CurEvent.Next = new MidiEvent(time) { Next = CurEvent.Next };
                CurEvent = CurEvent.Next;
            }
            CurEvent.Next = new MidiEvent(time);
            return CurEvent = CurEvent.Next;
        }

        void ConvertEvent(BinaryReader r, int time, byte status, int size)
        {
            var current = CreateNewEvent(time);
            current.Status = status;
            current.Data0 = r.ReadByte(); if (size == 1) return;
            current.Data1 = r.ReadByte(); if (size == 2) return;
            // save old
            var prev = current;
            GetVariableLengthQuantity(r, out var delta);
            current = CreateNewEvent(time + delta * 3);
            current.Status = status;
            current.Data0 = prev.Data0;
            current.Data1 = 0;
            CurEvent = prev; // restore old
        }

        void ConvertSystemMessage(BinaryReader r, int time, byte status)
        {
            var current = CreateNewEvent(time);
            current.Status = status;
            // handling of Meta events
            if ((EV)current.Status == EV.META) CurEvent.Data0 = r.ReadByte();
            GetVariableLengthQuantity(r, out CurEvent.Length);
            if (CurEvent.Length == 0) return;
            CurEvent.Stream = r.ReadBytes(CurEvent.Length);
        }

        short ReadEventList(BinaryReader r)
        {
            var time = 0;
            var tempo = 500000;
            var tempoSet = false;
            while (true)
            {
                var status = r.ReadByte();
                //F.Write($"  {status:x}\n");
                switch ((EV)(status & 0xF0))
                {
                    // Note On/Off
                    case EV.NOTE_OFF: Log("ERROR: Note off not valid in XMidiFile\n"); return 0;
                    case EV.NOTE_ON: ConvertEvent(r, time, status, 3); break;
                    // 2 byte data, Aftertouch, Controller and Pitch Wheel
                    case EV.POLY_PRESS:
                    case EV.CONTROL:
                    case EV.PITCH: ConvertEvent(r, time, status, 2); break;
                    // 1 byte data, Program Change and Channel Pressure
                    case EV.PROGRAM:
                    case EV.CHAN_PRESS: ConvertEvent(r, time, status, 1); break;
                    // SysEx
                    case EV.SYSEX:
                        if ((EV)status == EV.META)
                        {
                            var evt = (EVT)r.ReadByte();
                            switch (evt)
                            {
                                case EVT.OUTPUT_CABLE: break;
                                case EVT.TRACK_END: return (short)((tempo * 9) / 25000); ; // End Of Track
                                case EVT.SET_TEMPO: if (!tempoSet) { tempoSet = true; r.Skip(1); tempo = r.ReadByte() << 16 | r.ReadByte() << 8 | r.ReadByte(); r.Skip(-4); } break; // Tempo. Need it for PPQN
                                case EVT.TIME_SIG: break;
                                case EVT.KEYSIG: break;
                                default: break;
                            }
                            r.Skip(-1);
                        }
                        ConvertSystemMessage(r, time, status);
                        break;
                    default: // Delta T, also known as interval count
                        r.Skip(-1);
                        GetVariableLengthQuantity2(r, out var delta);
                        time += delta * 3;
                        break;
                }
            }
        }

        #endregion

        #region Write

        public void WriteAll()
        {
            for (var i = 0; i < Tracks; i++)
            {
                //F2 = File.Create($"C:\\T_\\FROG\\SYNGAME2-{i}.mid");
                //var w = new BinaryWriter(F2);
                //WriteMidi(w, i);
                //F2.Close();
            }
        }

        bool WriteMidi(BinaryWriter w, int track)
        {
            const uint MIDI_MThd = 0x6468544d;
            if (Events == null || track > Tracks) return false;

            // write header
            w.Write(MIDI_MThd);
            w.WriteE(6);
            w.WriteE((short)0);
            w.WriteE((short)1);
            w.WriteE(Timing[track]);

            // write tracks
            return WriteMidiMTrk(w, Events[track]);
        }

        bool WriteMidiMTrk(BinaryWriter w, MidiEvent evnts)
        {
            const uint MIDI_MTrk = 0x6b72544d;
            const byte XMIDI_CONTROLLER_NEXT_BREAK = 117;

            int delta, time = 0;
            byte lastStatus = 0;

            // This is set true to make the song end when an XMidiFile break is hit.
            var sshockBreak = false;
            w.Write(MIDI_MTrk);
            var sizePosition = w.BaseStream.Position;
            w.Write(0);
            for (var evnt = evnts; evnt != null; evnt = evnt.Next)
            {
                // If sshock_break is set, the delta is only 0
                delta = sshockBreak ? 0 : evnt.Time - time;
                time = evnt.Time;

                // write delta
                PutVariableLengthQuantity(w, delta);

                // write status
                if ((evnt.Status != lastStatus) || (evnt.Status >= (byte)EV.SYSEX)) w.Write(evnt.Status);

                // write event
                lastStatus = evnt.Status;
                switch ((EV)(evnt.Status & 0xF0))
                {
                    // 2 bytes data, Note off, Note on, Aftertouch and Pitch Wheel
                    case EV.NOTE_OFF: // invalid in XMID
                    case EV.NOTE_ON:
                    case EV.POLY_PRESS:
                    case EV.PITCH: w.Write(evnt.Data0); w.Write(evnt.Data1); break;
                    // Controller, we need to catch XMIXI Breaks
                    case EV.CONTROL:
                        w.Write(evnt.Data0); w.Write(evnt.Data1);
                        if (evnt.Data0 == XMIDI_CONTROLLER_NEXT_BREAK) sshockBreak = true; // XMidiFile Break
                        break;
                    // 1 bytes data, Program Change and Channel Pressure
                    case EV.PROGRAM:
                    case EV.CHAN_PRESS: w.Write(evnt.Data0); break;
                    // Variable length, SysEx
                    case EV.SYSEX:
                        if (evnt.Status == (byte)EV.META) w.Write(evnt.Data0);
                        PutVariableLengthQuantity(w, evnt.Length);
                        if (evnt.Length != 0) w.Write(evnt.Stream);
                        break;
                    // Never occur
                    default: Log("Not supposed to see this"); break;
                }
            }

            // write size
            var position = w.BaseStream.Position;
            var size = position - sizePosition;
            w.Seek(sizePosition);
            w.WriteE((uint)size);
            w.Seek(position);
            return true;
        }

        #endregion

        #region Utils

        // Get the MIDI variable-length quantity. A string of 7-bits/byte, terminated by a byte not having MSB set
        static void GetVariableLengthQuantity(BinaryReader r, out int value)
        {
            value = 0;
            byte b;
            for (var i = 0; i < 4; i++)
            {
                value <<= 7;
                value |= (b = r.ReadByte()) & 0x7F;
                if ((b & 0x80) == 0) break;
            }
        }

        // Instead of treating consecutive delta/interval counts as separate counts, just sum them up until we hit a MIDI event.
        static void GetVariableLengthQuantity2(BinaryReader r, out int value)
        {
            value = 0;
            byte b;
            for (var i = 0; i < 4 && ((b = r.ReadByte()) & 0x80) == 0; i++) value += b;
            r.Skip(-1);
        }

        // Write a MIDI variable-length quantity (see getVlc) into 'stream'.
        // Returns # of bytes used to store 'value'.
        // Note: stream can be NULL (useful to count how much space a value would need)
        static void PutVariableLengthQuantity(BinaryWriter w, int value)
        {
            var buffer = value & 0x7F;
            while ((value >>= 7) != 0)
            {
                buffer <<= 8;
                buffer |= (value & 0x7F) | 0x80;
            }
            while (true)
            {
                w.Write((byte)(buffer & 0xFF));
                if ((buffer & 0x80) != 0) buffer >>= 8;
                else break;
            }
        }

        #endregion
    }

    #endregion

    #region Binary_Img

    public unsafe class Binary_Img : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Img(r, f));

        (string type, object gl, object vulken, object unity, object unreal) Format;
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags Flags { get; } = 0;

        #region Headers

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BmpHeader
        {
            public static (string, int) Struct = ("<H3i", sizeof(BmpHeader));
            public ushort Type;             // 'BM'
            public uint Size;               // File size in bytes
            public uint Reserved;           // unused (=0)
            public uint OffBits;            // Offset from beginning of file to the beginning of the bitmap data
            public BmpInfoHeader Info;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BmpInfoHeader
        {
            public static (string, int) Struct = ("<3I2H6I", sizeof(BmpInfoHeader));
            public uint Size;               // Size of InfoHeader =40 
            public uint Width;              // Horizontal width of bitmap in pixels
            public uint Height;             // Vertical height of bitmap in pixels
            public ushort Planes;           // Number of Planes (=1)
            public ushort BitCount;         // Bits per Pixel used to store palette entry information.
            public uint Compression;        // Type of Compression: 0 = BI_RGB no compression, 1 = BI_RLE8 8bit RLE encoding, 2 = BI_RLE4 4bit RLE encoding
            public uint SizeImage;          // (compressed) Size of Image - It is valid to set this =0 if Compression = 0
            public uint XPixelsPerM;        // orizontal resolution: Pixels/meter
            public uint YPixelsPerM;        // vertical resolution: Pixels/meter
            public uint ColorsUsed;         // Number of actually used colors. For a 8-bit / pixel bitmap this will be 100h or 256.
            public uint ColorsImportant;    // Number of important colors 
        }

        #endregion

        //enum Formats { Bmp, Gif, Exif, Jpg, Png, Tiff }

        readonly byte[] Bytes;
        readonly Bitmap Image;

        public Binary_Img(BinaryReader r, FileSource f)
        {
            Image = new Bitmap(new MemoryStream(r.ReadBytes((int)f.FileSize)));
            Width = Image.Width; Height = Image.Height;
            var data = Image.LockBits(new Rectangle(0, 0, Image.Width, Image.Height), ImageLockMode.ReadOnly, Image.PixelFormat);
            var bytes = new byte[data.Stride * data.Height];
            Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
            Image.UnlockBits(data);
            var palette = Image.Palette?.Entries;
            switch (Image.PixelFormat)
            {
                case PixelFormat.Format16bppGrayScale:
                    Format = (Image.RawFormat.ToString(),
                        (TextureGLFormat.Luminance, TextureGLPixelFormat.Luminance, TextureGLPixelType.UnsignedByte),
                        (TextureGLFormat.Luminance, TextureGLPixelFormat.Luminance, TextureGLPixelType.UnsignedByte),
                        TextureUnityFormat.Unknown,
                        TextureUnrealFormat.Unknown);
                    break;
                case PixelFormat.Format8bppIndexed:
                    Format = (Image.RawFormat.ToString(),
                        (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                        (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                        TextureUnityFormat.RGB24,
                        TextureUnrealFormat.Unknown);
                    break;
                case PixelFormat.Format24bppRgb:
                    Format = (Image.RawFormat.ToString(),
                        (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                        (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                        TextureUnityFormat.RGB24,
                        TextureUnrealFormat.Unknown);
                    break;
                case PixelFormat.Format32bppArgb:
                    Format = (Image.RawFormat.ToString(),
                        (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                        (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                        TextureUnityFormat.RGBA32,
                        TextureUnrealFormat.Unknown);
                    break;
            }

            // decode
            if (palette == null || palette.Length == 0) Bytes = bytes;
            else
            {
                var pal = palette.SelectMany<Color, byte>(x => [x.R, x.G, x.B]).ToArray();
                Bytes = new byte[Width * Height * 3];
                Rasterize.CopyPixelsByPalette(Bytes, 3, bytes, pal, 3);
            }
        }

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
            => (Bytes, (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, null);
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new($"{nameof(Binary_Img)}", items: [
                new($"Format: {Format.type}"),
                new($"Width: {Width}"),
                new($"Height: {Height}"),
            ])
        ];
    }

    #endregion

    #region Binary_Msg

    public class Binary_Msg(string message) : IHaveMetaInfo
    {
        public static Func<BinaryReader, FileSource, PakFile, Task<object>> Factory(string message) => (BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Msg(message));

        public readonly string Message = message;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = Message }),
        ];
    }

    #endregion

    #region Binary_Pal

    public unsafe class Binary_Pal(BinaryReader r, byte bpp) : IHaveMetaInfo
    {
        public static Task<object> Factory_3(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Pal(r, 3));
        public static Task<object> Factory_4(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Pal(r, 4));

        #region Palette

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct RGB { public byte R; public byte G; public byte B; }

        #endregion

        public readonly byte Bpp = bpp;
        public readonly byte[][] Records = bpp switch
        {
            3 => r.ReadPArray<RGB>("3B", 256).Select(s => new[] { s.R, s.G, s.B, (byte)255 }).ToArray(),
            4 => r.ReadPArray<uint>("I", 256).Select(BitConverter.GetBytes).ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(bpp), $"{bpp}"),
        };

        public Binary_Pal ConvertVgaPalette()
        {
            switch (Bpp)
            {
                case 3:
                    for (var i = 0; i < 256; i++)
                    {
                        var p = Records[i];
                        p[0] = (byte)((p[0] << 2) | (p[0] >> 4));
                        p[1] = (byte)((p[1] << 2) | (p[1] >> 4));
                        p[2] = (byte)((p[2] << 2) | (p[2] >> 4));
                    }
                    break;
            }
            return this;
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => [
                new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "Pallet" }),
                new("Pallet", items: [
                    new($"Records: {Records.Length}"),
                ])
            ];
    }

    #endregion

    #region Binary_Pcx

    // https://en.wikipedia.org/wiki/PCX
    // https://github.com/warpdesign/pcx-js/blob/master/js/pcx.js
    public unsafe class Binary_Pcx : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Pcx(r, f));

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct X_Header
        {
            public static (string, int) Struct = ("<4B6H48c2B4H54c", sizeof(X_Header));
            public byte Manufacturer;       // Fixed header field valued at a hexadecimal
            public byte Version;            // Version number referring to the Paintbrush software release
            public byte Encoding;           // Method used for encoding the image data
            public byte Bpp;                // Number of bits constituting one plane
            public ushort XMin;             // Minimum x co-ordinate of the image position
            public ushort YMin;             // Minimum y co-ordinate of the image position
            public ushort XMax;             // Maximum x co-ordinate of the image position
            public ushort YMax;             // Maximum y co-ordinate of the image position
            public ushort HDpi;             // Horizontal image resolution in DPI
            public ushort VDpi;             // Vertical image resolution in DPI
            public fixed byte Palette[48];  // EGA palette for 16-color images
            public byte Reserved1;          // First reserved field
            public byte NumPlanes;          // Number of color planes constituting the pixel data
            public ushort Bpl;              // Number of bytes of one color plane representing a single scan line
            public ushort Mode;             // Mode in which to construe the palette
            public ushort HRes;             // horizontal resolution of the source system's screen
            public ushort VRes;             // vertical resolution of the source system's screen
            public fixed byte Reserved2[54]; // Second reserved field, intended for future extension
        }

        public Binary_Pcx(BinaryReader r, FileSource f)
        {
            Format = (
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGBA32,
                TextureUnrealFormat.Unknown);
            Header = r.ReadS<X_Header>();
            if (Header.Manufacturer != 0x0a) throw new FormatException("BAD MAGIC");
            else if (Header.Encoding == 0) throw new FormatException("NO COMPRESSION");
            Body = r.ReadToEnd();
            Planes = Header.NumPlanes;
            Width = Header.XMax - Header.XMin + 1;
            Height = Header.YMax - Header.YMin + 1;
        }

        readonly X_Header Header;
        readonly int Planes;
        readonly byte[] Body;
        readonly (object gl, object vulken, object unity, object unreal) Format;

        public int Width { get; }
        public int Height { get; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags Flags { get; } = 0;

        /// <summary>
        /// Gets the palette either from the header (< 8 bit) or at the bottom of the file (8bit)
        /// </summary>
        /// <returns></returns>
        /// <exception cref="FormatException"></exception>
        public Span<byte> GetPalette()
        {
            if (Header.Bpp == 8 && Body[^769] == 12) return Body.AsSpan(Body.Length - 768);
            else if (Header.Bpp == 1) fixed (byte* _ = Header.Palette) return new Span<byte>(_, 48);
            else throw new FormatException("Could not find 256 color palette.");
        }

        /// <summary>
        /// Set a color using palette index
        /// </summary>
        /// <param name="palette"></param>
        /// <param name="pixels"></param>
        /// <param name="pos"></param>
        /// <param name="index"></param>
        static void SetPixel(Span<byte> palette, byte[] pixels, int pos, int index)
        {
            var start = index * 3;
            pixels[pos + 0] = palette[start];
            pixels[pos + 1] = palette[start + 1];
            pixels[pos + 2] = palette[start + 2];
            pixels[pos + 3] = 255; // alpha channel
        }

        /// <summary>
        /// Returns true if the 2 most-significant bits are set
        /// </summary>
        /// <param name="body"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        static bool Rle(byte[] body, int offset) => (body[offset] >> 6) == 3;

        /// <summary>
        /// Returns the length of the RLE run.
        /// </summary>
        /// <param name="body"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        static int RleLength(byte[] body, int offset) => body[offset] & 63;

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
        {
            // Decodes 4bpp pixel data
            byte[] Decode4bpp()
            {
                var palette = GetPalette();
                var temp = new byte[Width * Height];
                var pixels = new byte[Width * Height * 4];
                int offset = 0, p, pos, length = 0, val = 0;

                // Simple RLE decoding: if 2 msb == 1 then we have to mask out count and repeat following byte count times
                var b = Body;
                for (var y = 0; y < Height; y++)
                    for (p = 0; p < Planes; p++)
                    {
                        // bpr holds the number of bytes needed to decode a row of plane: we keep on decoding until the buffer is full
                        pos = Width * y;
                        for (var _ = 0; _ < Header.Bpl; _++)
                        {
                            if (length == 0)
                                if (Rle(b, offset)) { length = RleLength(b, offset); val = b[offset + 1]; offset += 2; }
                                else { length = 1; val = b[offset++]; }
                            length--;

                            // Since there may, or may not be blank data at the end of each scanline, we simply check we're not out of bounds
                            if ((_ * 8) < Width)
                            {
                                for (var i = 0; i < 8; i++)
                                {
                                    var bit = (val >> (7 - i)) & 1;
                                    temp[pos + i] |= (byte)(bit << p);
                                    // we have all planes: we may set color using the palette
                                    if (p == Planes - 1) SetPixel(palette, pixels, (pos + i) * 4, temp[pos + i]);
                                }
                                pos += 8;
                            }
                        }
                    }
                return pixels;
            }

            // Decodes 8bpp (depth = 8/24bit) data
            byte[] Decode8bpp()
            {
                var palette = Planes == 1 ? GetPalette() : null;
                var pixels = new byte[Width * Height * 4];
                int offset = 0, p, pos, length = 0, val = 0;

                // Simple RLE decoding: if 2 msb == 1 then we have to mask out count and repeat following byte count times
                var b = Body;
                for (var y = 0; y < Height; y++)
                    for (p = 0; p < Planes; p++)
                    {
                        // bpr holds the number of bytes needed to decode a row of plane: we keep on decoding until the buffer is full
                        pos = 4 * Width * y + p;
                        for (var _ = 0; _ < Header.Bpl; _++)
                        {
                            if (length == 0)
                                if (Rle(b, offset)) { length = RleLength(b, offset); val = b[offset + 1]; offset += 2; }
                                else { length = 1; val = b[offset++]; }
                            length--;

                            // Since there may, or may not be blank data at the end of each scanline, we simply check we're not out of bounds
                            if (_ < Width)
                            {
                                if (Planes == 3)
                                {
                                    pixels[pos] = (byte)val;
                                    if (p == Planes - 1) pixels[pos + 1] = 255; // add alpha channel
                                }
                                else SetPixel(palette, pixels, pos, val);
                                pos += 4;
                            }
                        }
                    }
                return pixels;
            }

            return (Header.Bpp switch
            {
                8 => Decode8bpp(),
                1 => Decode4bpp(),
                _ => throw new FormatException($"Unsupported bpp: {Header.Bpp}"),
            }, (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, null);
        }
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new($"{nameof(Binary_Pcx)}", items: new List<MetaInfo> {
                new($"Width: {Width}"),
                new($"Height: {Height}"),
            })
        ];
    }

    #endregion

    #region Binary_Raw

    public class Binary_Raw : IHaveMetaInfo, ITexture
    {
        public static Func<BinaryReader, FileSource, PakFile, Task<object>> FactoryMethod(Action<Binary_Raw, BinaryReader, FileSource> action, Func<string, string, Binary_Pal> palleteFunc)
            => (BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Raw(r, s.Game, f, action, palleteFunc));

        public struct Tag
        {
            public string Palette;
            public int Width;
            public int Height;
        }

        public Binary_Raw(BinaryReader r, FamilyGame game, FileSource source, Action<Binary_Raw, BinaryReader, FileSource> action, Func<string, string, Binary_Pal> palleteFunc)
        {
            game.Ensure();
            Format = (
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGBA32,
                TextureUnrealFormat.Unknown);
            action(this, r, source);
            Body ??= r.ReadToEnd();
            if (source.Tag is Tag c)
            {
                Palette = c.Palette;
                Width = c.Width;
                Height = c.Height;
            }
            PaletteData = palleteFunc(game.Id, Palette).Records;
        }

        public byte[] Body;
        readonly byte[][] PaletteData;
        public string Palette;
        readonly (object gl, object vulken, object unity, object unreal) Format;

        public int Width { get; set; }
        public int Height { get; set; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags Flags { get; } = 0;

        /// <summary>
        /// Set a color using palette index
        /// </summary>
        /// <param name="palette"></param>
        /// <param name="pixels"></param>
        /// <param name="pixel"></param>
        /// <param name="color"></param>
        //static void SetPixel(byte[][] palette, byte[] pixels, ref int pixel, int color)
        //{
        //    var record = palette[color];
        //    pixels[pixel + 0] = record[0];
        //    pixels[pixel + 1] = record[1];
        //    pixels[pixel + 2] = record[2];
        //    pixels[pixel + 3] = 255; // alpha channel
        //    pixel += 4;
        //}

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
        {
            byte[] DecodeRaw() => Body.SelectMany(s => PaletteData[s]).ToArray();

            return (DecodeRaw(), (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, null);
        }
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new($"{nameof(Binary_Raw)}", items: [
                new($"Palette: {Palette}"),
                new($"Width: {Width}"),
                new($"Height: {Height}"),
            ])
        ];
    }

    #endregion

    #region Binary_Snd

    public unsafe class Binary_Snd(BinaryReader r, int fileSize, object tag) : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Snd(r, (int)f.FileSize, null));
        public static Task<object> Factory_Wav(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Snd(r, (int)f.FileSize, ".wav"));

        #region Header

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct WavHeader
        {
            public const int RIFF = 0x46464952;
            public const int WAVE = 0x45564157;
            public static (string, int) Struct = ("<3I", sizeof(WavHeader));
            public uint ChunkId;                // 'RIFF'
            public int ChunkSize;               // Size of the overall file - 8 bytes, in bytes (32-bit integer)
            public uint Format;                 // 'WAVE'
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct WavFmt
        {
            public const int FMT_ = 0x20746d66;
            public static (string, int) Struct = ("<2I2H2I2H", sizeof(WavFmt));
            public uint ChunkId;                // 'fmt '
            public int ChunkSize;               // Length of format data (16)
            public ushort AudioFormat;          // Type of format (1 is PCM)
            public ushort NumChannels;          // Number of Channels
            public uint SampleRate;             // Sample Rate
            public uint ByteRate;               // (Sample Rate * BitsPerSample * Channels) / 8
            public ushort BlockAlign;           // (BitsPerSample * Channels) / 8.1 - 8 bit mono2 - 8 bit stereo/16 bit mono4 - 16 bit stereo
            public ushort BitsPerSample;        // Bits per sample
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct WavData
        {
            public const int DATA = 0x61746164;
            public static (string, int) Struct = ("<3I2H6I", sizeof(WavData));
            public uint ChunkId;                // 'data'
            public int ChunkSize;               // Size of the data section
        }

        #endregion

        public byte[] Data = r.ReadBytes(fileSize);
        public object Tag = tag;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "AudioPlayer", Name = Path.GetFileName(file.Path), Value = new MemoryStream(Data), Tag = Tag ?? Path.GetExtension(file.Path) }),
        ];
    }

    #endregion

    #region Binary_Tga

    // https://en.wikipedia.org/wiki/Truevision_TGA
    // https://github.com/cadenji/tgafunc/blob/main/tgafunc.c
    // https://www.dca.fee.unicamp.br/~martino/disciplinas/ea978/tgaffs.pdf
    // https://www.conholdate.app/viewer/view/rVqTeZPLAL/tga-file-format-specifications.pdf?default=view&preview=
    public unsafe class Binary_Tga : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Tga(r, f));

        (object gl, object vulken, object unity, object unreal) Format;
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags Flags { get; } = 0;

        #region Headers

        // Image pixel format
        // The pixel data are all in little-endian. E.g. a PIXEL_ARGB32 format image, a single pixel is stored in the memory in the order of BBBBBBBB GGGGGGGG RRRRRRRR AAAAAAAA.
        enum PIXEL
        {
            BW8,    // Single channel format represents grayscale, 8-bit integer.
            BW16,   // Single channel format represents grayscale, 16-bit integer.
            RGB555, // A 16-bit pixel format. The topmost bit is assumed to an attribute bit, usually ignored. Because of little-endian, this format pixel is stored in the memory in the order of GGGBBBBB ARRRRRGG.
            RGB24,  // RGB color format, 8-bit per channel.
            ARGB32  // RGB color with alpha format, 8-bit per channel.
        };

        enum TYPE : byte
        {
            NO_DATA = 0,
            COLOR_MAPPED = 1,
            TRUE_COLOR = 2,
            GRAYSCALE = 3,
            RLE_COLOR_MAPPED = 9,
            RLE_TRUE_COLOR = 10,
            RLE_GRAYSCALE = 11,
        };

        // Gets the bytes per pixel by pixel format
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static int PixelFormatToPixelSize(PIXEL format)
            => format switch
            {
                PIXEL.BW8 => 1,
                PIXEL.BW16 => 2,
                PIXEL.RGB555 => 2,
                PIXEL.RGB24 => 3,
                PIXEL.ARGB32 => 4,
                _ => throw new FormatException("UNSUPPORTED_PIXEL_FORMAT"),
            };

        // Convert bits to integer bytes. E.g. 8 bits to 1 byte, 9 bits to 2 bytes.
        [MethodImpl(MethodImplOptions.AggressiveInlining)] static byte BitsToBytes(byte bits) => (byte)((bits - 1) / 8 + 1);

        class ColorMap
        {
            public ushort FirstIndex;
            public ushort EntryCount;
            public byte BytesPerEntry;
            public byte[] Pixels;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct X_Header
        {
            public static (string, int) Struct = ("<3b2Hb4H2b", sizeof(X_Header));
            public byte IdLength;
            public byte MapType;
            public TYPE ImageType;
            // color map specification
            public ushort MapFirstEntry;
            public ushort MapLength;
            public byte MapEntrySize;
            // image specification
            public ushort ImageXOrigin;
            public ushort ImageYOrigin;
            public ushort ImageWidth;
            public ushort ImageHeight;
            public byte PixelDepth;
            public byte ImageDescriptor;

            public bool IS_SUPPORTED_IMAGE_TYPE =>
                ImageType == TYPE.COLOR_MAPPED ||
                ImageType == TYPE.TRUE_COLOR ||
                ImageType == TYPE.GRAYSCALE ||
                ImageType == TYPE.RLE_COLOR_MAPPED ||
                ImageType == TYPE.RLE_TRUE_COLOR ||
                ImageType == TYPE.RLE_GRAYSCALE;
            public bool IS_COLOR_MAPPED =>
                ImageType == TYPE.COLOR_MAPPED ||
                ImageType == TYPE.RLE_COLOR_MAPPED;
            public bool IS_TRUE_COLOR =>
                ImageType == TYPE.TRUE_COLOR ||
                ImageType == TYPE.RLE_TRUE_COLOR;
            public bool IS_GRAYSCALE =>
                ImageType == TYPE.GRAYSCALE ||
                ImageType == TYPE.RLE_GRAYSCALE;
            public bool IS_RLE =>
                ImageType == TYPE.RLE_COLOR_MAPPED ||
                ImageType == TYPE.RLE_TRUE_COLOR ||
                ImageType == TYPE.RLE_GRAYSCALE;

            public void Check()
            {
                const int MAX_IMAGE_DIMENSIONS = 65535;
                if (MapType > 1) throw new FormatException("UNSUPPORTED_COLOR_MAP_TYPE");
                else if (ImageType == TYPE.NO_DATA) throw new FormatException("NO_DATA");
                else if (!IS_SUPPORTED_IMAGE_TYPE) throw new FormatException("UNSUPPORTED_IMAGE_TYPE");
                else if (ImageWidth <= 0 || ImageWidth > MAX_IMAGE_DIMENSIONS || ImageHeight <= 0 || ImageHeight > MAX_IMAGE_DIMENSIONS) throw new FormatException("INVALID_IMAGE_DIMENSIONS");
            }

            public ColorMap GetColorMap(BinaryReader r)
            {
                var mapSize = MapLength * BitsToBytes(MapEntrySize);
                var s = new ColorMap();
                if (IS_COLOR_MAPPED)
                {
                    s.FirstIndex = MapFirstEntry;
                    s.EntryCount = MapLength;
                    s.BytesPerEntry = BitsToBytes(MapEntrySize);
                    s.Pixels = r.ReadBytes(mapSize);
                }
                else if (MapType == 1) r.Skip(mapSize); // The image is not color mapped at this time, but contains a color map. So skips the color map data block directly.
                return s;
            }

            public PIXEL GetPixelFormat()
            {
                if (IS_COLOR_MAPPED)
                {
                    if (PixelDepth == 8)
                        switch (MapEntrySize)
                        {
                            case 15: case 16: return PIXEL.RGB555;
                            case 24: return PIXEL.RGB24;
                            case 32: return PIXEL.ARGB32;
                        }
                }
                else if (IS_TRUE_COLOR)
                    switch (PixelDepth)
                    {
                        case 16: return PIXEL.RGB555;
                        case 24: return PIXEL.RGB24;
                        case 32: return PIXEL.ARGB32;
                    }
                else if (IS_GRAYSCALE)
                    switch (PixelDepth)
                    {
                        case 8: return PIXEL.BW8;
                        case 16: return PIXEL.BW16;
                    }
                throw new FormatException("UNSUPPORTED_PIXEL_FORMAT");
            }
        }

        #endregion

        readonly X_Header Header;
        readonly ColorMap Map;
        readonly PIXEL PixelFormat;
        readonly int PixelSize;
        readonly MemoryStream Body;

        public Binary_Tga(BinaryReader r, FileSource f)
        {
            Header = r.ReadS<X_Header>();
            Header.Check();
            r.Skip(Header.IdLength);
            Map = Header.GetColorMap(r);
            Width = Header.ImageWidth;
            Height = Header.ImageHeight;
            Body = new MemoryStream(r.ReadToEnd());
            PixelFormat = Header.GetPixelFormat();
            PixelSize = PixelFormatToPixelSize(PixelFormat);
            Format = PixelFormat switch
            {
                PIXEL.BW8 => throw new NotSupportedException(),
                PIXEL.BW16 => throw new NotSupportedException(),
                PIXEL.RGB555 => (
                    (TextureGLFormat.Rgb5, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                    (TextureGLFormat.Rgb5, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                    TextureUnityFormat.RGB565,
                    TextureUnrealFormat.Unknown),
                PIXEL.RGB24 => (
                    (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                    (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                    TextureUnityFormat.RGB24,
                    TextureUnrealFormat.Unknown),
                PIXEL.ARGB32 => (
                    (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                    (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                    TextureUnityFormat.RGBA32,
                    TextureUnrealFormat.Unknown),
                _ => throw new ArgumentOutOfRangeException(nameof(PixelFormat), $"{PixelFormat}")
            };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ushort PixelToMapIndex(byte[] pixelPtr, int offset) => pixelPtr[offset];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void GetColorFromMap(byte[] dest, int offset, ushort index, ColorMap map)
        {
            index -= map.FirstIndex;
            if (index < 0 && index >= map.EntryCount) throw new FormatException("COLOR_MAP_INDEX_FAILED");
            Buffer.BlockCopy(map.Pixels, map.BytesPerEntry * index, dest, offset, map.BytesPerEntry);
        }

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
        {
            // decodeRle
            void decodeRle(byte[] data)
            {
                var isColorMapped = Header.IS_COLOR_MAPPED;
                var pixelSize = PixelSize;
                var s = Body; var o = 0;
                var pixelCount = Width * Height;

                var isRunLengthPacket = false;
                var packetCount = 0;
                var pixelBuffer = new byte[isColorMapped ? Map.BytesPerEntry : pixelSize];

                for (; pixelCount > 0; --pixelCount)
                {
                    if (packetCount == 0)
                    {
                        var repetitionCountField = s.ReadByte();
                        isRunLengthPacket = (repetitionCountField & 0x80) != 0;
                        packetCount = (repetitionCountField & 0x7F) + 1;
                        if (isRunLengthPacket)
                        {
                            s.Read(pixelBuffer, 0, pixelSize);
                            // in color mapped image, the pixel as the index value of the color map. The actual pixel value is found from the color map.
                            if (isColorMapped) GetColorFromMap(pixelBuffer, 0, PixelToMapIndex(pixelBuffer, o), Map);
                        }
                    }

                    if (isRunLengthPacket) Buffer.BlockCopy(pixelBuffer, 0, data, o, pixelSize);
                    else
                    {
                        s.Read(data, o, pixelSize);
                        // In color mapped image, the pixel as the index value of the color map. The actual pixel value is found from the color map.
                        if (isColorMapped) GetColorFromMap(data, o, PixelToMapIndex(data, o), Map);
                    }

                    --packetCount;
                    o += pixelSize;
                }
            }

            // decode
            void decode(byte[] data)
            {
                var isColorMapped = Header.IS_COLOR_MAPPED;
                var pixelSize = PixelSize;
                var s = Body; var o = 0;
                var pixelCount = Width * Height;

                // in color mapped image, the pixel as the index value of the color map. The actual pixel value is found from the color map.
                if (isColorMapped)
                {
                    for (; pixelCount > 0; --pixelCount)
                    {
                        s.Read(data, o, pixelSize);
                        GetColorFromMap(data, o, PixelToMapIndex(data, o), Map);
                        o += Map.BytesPerEntry;
                    }
                }
                else s.Read(data, 0, pixelCount * pixelSize);
            }

            var bytes = new byte[Width * Height * PixelSize];
            if (Header.IS_RLE) decodeRle(bytes);
            else decode(bytes);
            Map.Pixels = null;

            // flip the image if necessary, to keep the origin in upper left corner.
            var flipH = (Header.ImageDescriptor & 0x10) != 0;
            var flipV = (Header.ImageDescriptor & 0x20) == 0;
            if (flipH) FlipH(bytes);
            if (flipV) FlipV(bytes);

            return (bytes, (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, null);
        }
        public void End() { }

        // Returns the pixel at coordinates (x,y) for reading or writing.
        // If the pixel coordinates are out of bounds (larger than width/height or small than 0), they will be clamped.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int GetPixel(int x, int y)
        {
            if (x < 0) x = 0;
            else if (x >= Width) x = Width - 1;
            if (y < 0) y = 0;
            else if (y >= Height) y = Height - 1;
            return (y * Width + x) * PixelSize;
        }

        void FlipH(byte[] data)
        {
            var pixelSize = PixelSize;
            var temp = new byte[pixelSize];
            var flipNum = Width / 2;
            for (var i = 0; i < flipNum; ++i)
                for (var j = 0; j < Height; ++j)
                {
                    var p1 = GetPixel(i, j);
                    var p2 = GetPixel(Width - 1 - i, j);
                    // swap two pixels
                    Buffer.BlockCopy(data, p1, temp, 0, pixelSize);
                    Buffer.BlockCopy(data, p2, data, p1, pixelSize);
                    Buffer.BlockCopy(temp, 0, data, p2, pixelSize);
                }
        }

        void FlipV(byte[] data)
        {
            var pixelSize = PixelSize;
            var temp = new byte[pixelSize];
            var flipNum = Height / 2;
            for (var i = 0; i < flipNum; ++i)
                for (var j = 0; j < Width; ++j)
                {
                    var p1 = GetPixel(j, i);
                    var p2 = GetPixel(j, Height - 1 - i);
                    // swap two pixels
                    Buffer.BlockCopy(data, p1, temp, 0, pixelSize);
                    Buffer.BlockCopy(data, p2, data, p1, pixelSize);
                    Buffer.BlockCopy(temp, 0, data, p2, pixelSize);
                }
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new($"{nameof(Binary_Tga)}", items: [
                new($"Format: {PixelFormat}"),
                new($"Width: {Width}"),
                new($"Height: {Height}"),
            ])
        ];
    }

    #endregion

    #region Binary_Txt

    public class Binary_Txt(BinaryReader r, int fileSize) : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Txt(r, (int)f.FileSize));

        public readonly string Data = r.ReadEncoding(fileSize);

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = Data }),
        ];
    }

    #endregion

    #region Binary_Xga

    public unsafe class Binary_Xga(BinaryReader r, object tag) : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Xga(r, s.Tag));

        readonly int Type;
        readonly byte[] Body = r.ReadToEnd();
        readonly (object gl, object vulken, object unity, object unreal) Format = (
            (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
            (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
            TextureUnityFormat.RGBA32,
            TextureUnrealFormat.Unknown);

        public int Width { get; } = 64;
        public int Height { get; } = 64;
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags Flags { get; } = 0;

        public (byte[] bytes, object format, Range[] spans) Begin(int platform)
        {
            static byte[] Decode1() => null;
            return (Type switch
            {
                1 => Decode1(),
                _ => throw new FormatException($"Unsupported type: {Type}"),
            }, (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            }, null);
        }
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new($"{nameof(Binary_Xga)}", items: [
                new($"Type: {Type}"),
                new($"Width: {Width}"),
                new($"Height: {Height}"),
            ])
        ];
    }

    #endregion
}

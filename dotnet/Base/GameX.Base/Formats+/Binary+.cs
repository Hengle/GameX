using GameX.Meta;
using GameX.Platforms;
using OpenStack.Graphics;
using OpenStack.Graphics.DirectX;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using static Compression.Doboz.DobozDecoder;
using static GameX.Formats.Unknown.IUnknownTexture;
using static OpenStack.Graphics.GXColor;

namespace GameX.Formats
{
    #region Binary_Pal

    public unsafe class Binary_Pal : IHaveMetaInfo
    {
        public static Task<object> Factory_3(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Pal(r, 3));
        public static Task<object> Factory_4(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Pal(r, 4));

        #region Palette

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct RGB
        {
            public static (string, int) Struct = ("<3x", sizeof(RGB));
            public byte R;
            public byte G;
            public byte B;
        }

        public byte Bpp;
        public byte[][] Records;

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

        #endregion

        public Binary_Pal(BinaryReader r, byte bpp)
        {
            Bpp = bpp;
            Records = bpp switch
            {
                3 => r.ReadTArray<RGB>(sizeof(RGB), 256).Select(s => new[] { s.R, s.G, s.B, (byte)255 }).ToArray(),
                4 => r.ReadTArray<uint>(sizeof(uint), 256).Select(s => BitConverter.GetBytes(s)).ToArray(),
                _ => throw new ArgumentOutOfRangeException(nameof(bpp), $"{bpp}"),
            };
        }

        // IHaveMetaInfo
        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag)
            => new List<MetaInfo> {
                new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "Pallet" }),
                new MetaInfo("Pallet", items: new List<MetaInfo> {
                    new MetaInfo($"Records: {Records.Length}"),
                })
            };
    }

    #endregion

    #region Binary_Bik

    public class Binary_Bik : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Bik(r, (int)f.FileSize));

        public Binary_Bik(BinaryReader r, int fileSize) => Data = r.ReadBytes(fileSize);

        public byte[] Data;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "BIK Video" }),
        };
    }

    #endregion

    #region Binary_Dds

    // https://github.com/paroj/nv_dds/blob/master/nv_dds.cpp
    public class Binary_Dds : ITexture, IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Dds(r));

        public Binary_Dds(BinaryReader r, bool readMagic = true)
        {
            Bytes = DDS_HEADER.Read(r, readMagic, out Header, out HeaderDXT10, out Format);
            var numMipMaps = Math.Max(1, Header.dwMipMapCount);
            var offset = 0;
            Mips = new Range[numMipMaps];
            for (var i = 0; i < numMipMaps; i++)
            {
                int w = (int)Header.dwWidth >> i, h = (int)Header.dwHeight >> i;
                if (w == 0 || h == 0) { Mips[i] = -1..; continue; }
                var size = ((w + 3) / 4) * ((h + 3) / 4) * Format.blockSize;
                var remains = Math.Min(size, Bytes.Length - offset);
                Mips[i] = remains > 0 ? offset..(offset + remains) : -1..;
                offset += remains;
            }
        }

        DDS_HEADER Header;
        DDS_HEADER_DXT10? HeaderDXT10;
        (object type, int blockSize, object gl, object vulken, object unity, object unreal) Format;
        byte[] Bytes;
        Range[] Mips;

        public IDictionary<string, object> Data => null;
        public int Width => (int)Header.dwWidth;
        public int Height => (int)Header.dwHeight;
        public int Depth => 0;
        public int MipMaps => (int)Header.dwMipMapCount;
        public TextureFlags Flags => 0;

        public void Select(int id) { }
        public byte[] Begin(int platform, out object format, out Range[] mips)
        {
            format = (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.StereoKit => throw new NotImplementedException("StereoKit"),
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            };
            mips = Mips;
            return Bytes;
        }
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new MetaInfo("Texture", items: new List<MetaInfo> {
                new MetaInfo($"Format: {Format.type}"),
                new MetaInfo($"Width: {Width}"),
                new MetaInfo($"Height: {Height}"),
                new MetaInfo($"Mipmaps: {MipMaps}"),
            }),
        };
    }

    #endregion

    #region Binary_Fsb

    public class Binary_Fsb : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Fsb(r, (int)f.FileSize));

        public Binary_Fsb(BinaryReader r, int fileSize) => Data = r.ReadBytes(fileSize);

        public byte[] Data;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = "FSB Audio" }),
        };
    }

    #endregion

    #region Binary_Img

    public unsafe class Binary_Img : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Img(r, f));

        #region BMP

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        public struct BmpHeader
        {
            public static (string, int) Struct = ("<H3i", sizeof(BmpHeader));
            public ushort Type;             // 'BM'
            public uint Size;               // File size in bytes
            public uint Reserved;           // unused (=0)
            public uint OffBits;            // Offset from beginning of file to the beginning of the bitmap data
            public BmpInfoHeader Info;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
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

        enum Formats { Bmp, Gif, Exif, Jpg, Png, Tiff }

        public Binary_Img(BinaryReader r, FileSource f)
        {
            var formatType = Path.GetExtension(f.Path).ToLowerInvariant() switch
            {
                ".bmp" => Formats.Bmp,
                ".gif" => Formats.Gif,
                ".exif" => Formats.Exif,
                ".jpg" => Formats.Jpg,
                ".png" => Formats.Png,
                ".tiff" => Formats.Tiff,
                _ => throw new ArgumentOutOfRangeException(nameof(f.Path), Path.GetExtension(f.Path)),
            };
            Format = (formatType,
                (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGB24,
                TextureUnrealFormat.Unknown);
            Image = new Bitmap(new MemoryStream(r.ReadBytes((int)f.FileSize)));
            Width = Image.Width;
            Height = Image.Height;
        }

        byte[] Bytes;
        Bitmap Image;
        (Formats type, object gl, object vulken, object unity, object unreal) Format;

        public IDictionary<string, object> Data { get; } = null;
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags Flags { get; } = 0;

        public void Select(int id) { }
        public byte[] Begin(int platform, out object format, out Range[] ranges)
        {
            unsafe byte[] BmpToBytes()
            {
                var d = new byte[Width * Height * 3];
                var data = Image.LockBits(new Rectangle(0, 0, Image.Width, Image.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                var s = (byte*)data.Scan0.ToPointer();
                for (var i = 0; i < d.Length; i += 3) { d[i + 0] = s[i + 0]; d[i + 1] = s[i + 1]; d[i + 2] = s[i + 2]; }
                Image.UnlockBits(data);
                return d;
            }

            Bytes = BmpToBytes();
            format = (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            };
            ranges = null;
            return Bytes;
        }
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new MetaInfo($"{nameof(Binary_Img)}", items: new List<MetaInfo> {
                new MetaInfo($"Format: {Format.type}"),
                new MetaInfo($"Width: {Width}"),
                new MetaInfo($"Height: {Height}"),
            })
        };
    }

    #endregion

    #region Binary_Msg

    public class Binary_Msg : IHaveMetaInfo
    {
        public static Func<BinaryReader, FileSource, PakFile, Task<object>> Factory(string message) => (BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Msg(message));

        public Binary_Msg(string message) => Message = message;

        public string Message;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = Message }),
        };
    }

    #endregion

    #region Binary_Pcx

    // https://en.wikipedia.org/wiki/PCX
    // https://github.com/warpdesign/pcx-js/blob/master/js/pcx.js
    public unsafe class Binary_Pcx : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Pcx(r, f));

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
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

        X_Header Header;
        int Planes;
        byte[] Body;
        (object gl, object vulken, object unity, object unreal) Format;

        public IDictionary<string, object> Data { get; } = null;
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

        public void Select(int id) { }
        public byte[] Begin(int platform, out object format, out Range[] ranges)
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

            var bytes = Header.Bpp switch
            {
                8 => Decode8bpp(),
                1 => Decode4bpp(),
                _ => throw new FormatException($"Unsupported bpp: {Header.Bpp}"),
            };
            format = (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            };
            ranges = null;
            return bytes;
        }
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new MetaInfo($"{nameof(Binary_Pcx)}", items: new List<MetaInfo> {
                new MetaInfo($"Width: {Width}"),
                new MetaInfo($"Height: {Height}"),
            })
        };
    }

    #endregion

    #region Binary_Snd

    public unsafe class Binary_Snd : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Snd(r, (int)f.FileSize, null));
        public static Task<object> Factory_Wav(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Snd(r, (int)f.FileSize, ".wav"));

        #region WAV

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        public struct WavHeader
        {
            public const int RIFF = 0x46464952;
            public const int WAVE = 0x45564157;
            public static (string, int) Struct = ("<3I", sizeof(WavHeader));
            public uint ChunkId;                // 'RIFF'
            public int ChunkSize;               // Size of the overall file - 8 bytes, in bytes (32-bit integer)
            public uint Format;                 // 'WAVE'
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
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
            public ushort BlockAlign;             // (BitsPerSample * Channels) / 8.1 - 8 bit mono2 - 8 bit stereo/16 bit mono4 - 16 bit stereo
            public ushort BitsPerSample;          // Bits per sample
        }

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        public struct WavData
        {
            public const int DATA = 0x61746164;
            public static (string, int) Struct = ("<3I2H6I", sizeof(WavData));
            public uint ChunkId;                // 'data'
            public int ChunkSize;               // Size of the data section
        }

        #endregion

        public Binary_Snd(BinaryReader r, int fileSize, object tag)
        {
            Data = r.ReadBytes(fileSize);
            Tag = tag;
        }

        public byte[] Data;
        public object Tag;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "AudioPlayer", Name = Path.GetFileName(file.Path), Value = new MemoryStream(Data), Tag = Tag ?? Path.GetExtension(file.Path) }),
        };
    }

    #endregion

    #region Binary_Txt

    public class Binary_Txt : IHaveMetaInfo
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Txt(r, (int)f.FileSize));

        public Binary_Txt(BinaryReader r, int fileSize) => Data = r.ReadEncoding(fileSize);

        public string Data;

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Text", Name = Path.GetFileName(file.Path), Value = Data }),
        };
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

        #region Header

        // Image pixel format.
        // The pixel data are all in little-endian. E.g. a PIXEL_ARGB32 format image, a single pixel is stored in the memory in the order of BBBBBBBB GGGGGGGG RRRRRRRR AAAAAAAA.
        enum PIXEL
        {
            BW8, // Single channel format represents grayscale, 8-bit integer.
            BW16, // Single channel format represents grayscale, 16-bit integer.
            RGB555, // A 16-bit pixel format. The topmost bit is assumed to an attribute bit, usually ignored. Because of little-endian, this format pixel is stored in the memory in the order of GGGBBBBB ARRRRRGG.
            RGB24, // RGB color format, 8-bit per channel.
            ARGB32 // RGB color with alpha format, 8-bit per channel.
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

        // Gets the bytes per pixel by pixel format.
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

        [StructLayout(LayoutKind.Sequential, Pack = 0x1)]
        struct X_Header
        {
            public static (string, int) Struct = ("<3x2Hx4H2x", sizeof(X_Header));
            public byte IdLength;
            public byte MapType;
            public TYPE ImageType;
            // Color map specification
            public ushort MapFirstEntry;
            public ushort MapLength;
            public byte MapEntrySize;
            // Image specification.
            public ushort ImageXOrigin;
            public ushort ImageYOrigin;
            public ushort ImageWidth;
            public ushort ImageHeight;
            public byte PixelDepth;
            public byte ImageDescriptor;

            public readonly bool IS_SUPPORTED_IMAGE_TYPE =>
                ImageType == TYPE.COLOR_MAPPED ||
                ImageType == TYPE.TRUE_COLOR ||
                ImageType == TYPE.GRAYSCALE ||
                ImageType == TYPE.RLE_COLOR_MAPPED ||
                ImageType == TYPE.RLE_TRUE_COLOR ||
                ImageType == TYPE.RLE_GRAYSCALE;
            public readonly bool IS_COLOR_MAPPED =>
                ImageType == TYPE.COLOR_MAPPED ||
                ImageType == TYPE.RLE_COLOR_MAPPED;
            public readonly bool IS_TRUE_COLOR =>
                ImageType == TYPE.TRUE_COLOR ||
                ImageType == TYPE.RLE_TRUE_COLOR;
            public readonly bool IS_GRAYSCALE =>
                ImageType == TYPE.GRAYSCALE ||
                ImageType == TYPE.RLE_GRAYSCALE;
            public readonly bool IS_RLE =>
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
                {
                    switch (PixelDepth)
                    {
                        case 16: return PIXEL.RGB555;
                        case 24: return PIXEL.RGB24;
                        case 32: return PIXEL.ARGB32;
                    }
                }
                else if (IS_GRAYSCALE)
                {
                    switch (PixelDepth)
                    {
                        case 8: return PIXEL.BW8;
                        case 16: return PIXEL.BW16;
                    }
                }
                throw new FormatException("UNSUPPORTED_PIXEL_FORMAT");
            }
        }

        #endregion

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

        X_Header Header;
        ColorMap Map;
        PIXEL PixelFormat;
        int PixelSize;
        MemoryStream Body;
        (object gl, object vulken, object unity, object unreal) Format;

        public IDictionary<string, object> Data { get; } = null;
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags Flags { get; } = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static ushort PixelToMapIndex(byte[] pixelPtr, int offset) => pixelPtr[offset];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void GetColorFromMap(byte[] dest, int offset, ushort index, ColorMap map)
        {
            index -= map.FirstIndex;
            if (index < 0 && index >= map.EntryCount) throw new FormatException("COLOR_MAP_INDEX_FAILED");
            Buffer.BlockCopy(map.Pixels, map.BytesPerEntry * index, dest, offset, map.BytesPerEntry);
        }

        public void Select(int id) { }
        public byte[] Begin(int platform, out object format, out Range[] ranges)
        {
            // DecodeRle
            void DecodeRle(byte[] data)
            {
                var isColorMapped = Header.IS_COLOR_MAPPED;
                var pixelSize = PixelSize;
                var s = Body; var o = 0;
                var pixelCount = Width * Height;
                var isRunLengthPacket = false;
                var packetCount = 0;
                var pixelBuffer = new byte[isColorMapped ? Map.BytesPerEntry : pixelSize];
                // The actual pixel size of the image, In order not to be confused with the name of the parameter pixel_size, named data element.
                var dataElementSize = pixelSize;

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
                            if (isColorMapped)
                                // In color mapped image, the pixel as the index value of the color map. The actual pixel value is found from the color map.
                                GetColorFromMap(pixelBuffer, 0, PixelToMapIndex(pixelBuffer, o), Map);
                        }
                    }

                    if (isRunLengthPacket)
                        Buffer.BlockCopy(pixelBuffer, 0, data, o, dataElementSize);
                    else
                    {
                        s.Read(data, o, pixelSize);
                        if (isColorMapped)
                            // In color mapped image, the pixel as the index value of the color map. The actual pixel value is found from the color map.
                            GetColorFromMap(data, o, PixelToMapIndex(data, o), Map);
                    }

                    --packetCount;
                    o += dataElementSize;
                }
            }

            // Decode
            void Decode(byte[] data)
            {
                var isColorMapped = Header.IS_COLOR_MAPPED;
                var pixelSize = PixelSize;
                var s = Body; var o = 0;
                var pixelCount = Width * Height;
                if (isColorMapped)
                    for (; pixelCount > 0; --pixelCount)
                    {
                        s.Read(data, o, pixelSize);
                        // In color mapped image, the pixel as the index value of the color map. The actual pixel value is found from the color map.
                        GetColorFromMap(data, o, PixelToMapIndex(data, o), Map);
                        o += Map.BytesPerEntry;
                    }
                else s.Read(data, o, pixelCount * pixelSize);
            }

            var bytes = new byte[Width * Height * PixelSize];
            if (Header.IS_RLE) DecodeRle(bytes);
            else Decode(bytes);
            Map.Pixels = null;

            // Flip the image if necessary, to keep the origin in upper left corner.
            var flipH = (Header.ImageDescriptor & 0x10) != 0;
            var flipV = (Header.ImageDescriptor & 0x20) == 0;
            if (flipH) FlipH(bytes);
            if (flipV) FlipV(bytes);

            format = (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            };
            ranges = null;
            return bytes;
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
                    // Swap two pixels.
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
                    // Swap two pixels.
                    Buffer.BlockCopy(data, p1, temp, 0, pixelSize);
                    Buffer.BlockCopy(data, p2, data, p1, pixelSize);
                    Buffer.BlockCopy(temp, 0, data, p2, pixelSize);
                }
        }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new MetaInfo($"{nameof(Binary_Tga)}", items: new List<MetaInfo> {
                new MetaInfo($"PixelFormat: {PixelFormat}"),
                new MetaInfo($"Width: {Width}"),
                new MetaInfo($"Height: {Height}"),
            })
        };
    }
    #endregion

    #region Binary_Xga

    public unsafe class Binary_Xga : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Xga(r, s.Tag));

        public Binary_Xga(BinaryReader r, object tag)
        {
            Format = (
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGBA32,
                TextureUnrealFormat.Unknown);
            Body = r.ReadToEnd();
            Width = 64;
            Height = 64;
        }

        int Type;
        byte[] Body;
        (object gl, object vulken, object unity, object unreal) Format;

        public IDictionary<string, object> Data { get; } = null;
        public int Width { get; }
        public int Height { get; }
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags Flags { get; } = 0;

        public void Select(int id) { }
        public byte[] Begin(int platform, out object format, out Range[] ranges)
        {
            byte[] Decode1()
            {
                return null;
            }

            var bytes = Type switch
            {
                1 => Decode1(),
                _ => throw new FormatException($"Unsupported type: {Type}"),
            };
            format = (Platform.Type)platform switch
            {
                Platform.Type.OpenGL => Format.gl,
                Platform.Type.Vulken => Format.vulken,
                Platform.Type.Unity => Format.unity,
                Platform.Type.Unreal => Format.unreal,
                _ => throw new ArgumentOutOfRangeException(nameof(platform), $"{platform}"),
            };
            ranges = null;
            return bytes;
        }
        public void End() { }

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => new List<MetaInfo> {
            new MetaInfo(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new MetaInfo($"{nameof(Binary_Xga)}", items: new List<MetaInfo> {
                new MetaInfo($"Type: {Type}"),
                new MetaInfo($"Width: {Width}"),
                new MetaInfo($"Height: {Height}"),
            })
        };
    }

    #endregion
}

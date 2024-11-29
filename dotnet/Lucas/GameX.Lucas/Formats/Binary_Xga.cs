using OpenStack.Gfx.Textures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

// https://en.wikipedia.org/wiki/Color_Graphics_Adapter
// https://www.quora.com/What-is-the-difference-between-an-EGA-and-VGA-card-What-are-the-benefits-of-using-an-EGA-or-VGA-card-over-a-standard-VGA-card#:~:text=EGA%20(Enhanced%20Graphics%20Adapter)%20was,graphics%20and%20more%20detailed%20images.
namespace GameX.Lucas.Formats
{
    #region Binary_Xga
    public unsafe class Binary_Xga : IHaveMetaInfo, ITexture
    {
        public static Task<object> Factory(BinaryReader r, FileSource f, PakFile s) => Task.FromResult((object)new Binary_Xga(r, f));

        #region Headers

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct X_Header
        {
            public static (string, int) Struct = ("<4B6H48c2B4H54c", sizeof(X_Header));
            public byte Magic;              // Fixed header field valued at a hexadecimal
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
            public byte BitPlanes;          // Number of color planes constituting the pixel data
            public ushort Bpr;              // Number of bytes of one color plane representing a single scan line
            public ushort Mode;             // Mode in which to construe the palette
            public ushort HRes;             // horizontal resolution of the source system's screen
            public ushort VRes;             // vertical resolution of the source system's screen
            public fixed byte Reserved2[54]; // Second reserved field, intended for future extension
        }

        #endregion

        public Binary_Xga(BinaryReader r, FileSource f)
        {
            Header = r.ReadS<X_Header>();
            if (Header.Magic != 0x0a) throw new FormatException("BAD MAGIC");
            Body = r.ReadToEnd();
        }

        X_Header Header;
        byte[] Body;

        #region ITexture
        static readonly object Format = (TextureFormat.RGBA32, TexturePixel.Unknown);
        //(TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
        //(TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
        //TextureUnityFormat.RGBA32,
        //TextureUnrealFormat.Unknown);
        public int Width { get; } = 320;
        public int Height { get; } = 200;
        public int Depth { get; } = 0;
        public int MipMaps { get; } = 1;
        public TextureFlags TexFlags { get; } = 0;

        public (byte[] bytes, object format, Range[] spans) Begin(string platform)
        {
            //var bytes = Header.Bpp switch
            //{
            //    //8 => Decode8bpp(),
            //    //1 => Decode4bpp(),
            //    _ => throw new FormatException($"Unsupported bpp: {Header.Bpp}"),
            //};
            return (null, Format, null); // bytes;
        }
        public void End() { }
        #endregion

        List<MetaInfo> IHaveMetaInfo.GetInfoNodes(MetaManager resource, FileSource file, object tag) => [
            new(null, new MetaContent { Type = "Texture", Name = Path.GetFileName(file.Path), Value = this }),
            new($"{nameof(Binary_Xga)}", items: [
                new($"Width: {Width}"),
                new($"Height: {Height}"),
            ])
        ];
    }
    #endregion
}

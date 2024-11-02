import os, numpy as np
from io import BytesIO
from PIL import Image
from enum import Enum
from openstk.gfx.gfx_render import Rasterize
from openstk.gfx.gfx_texture import DDS_HEADER, ITexture, TextureGLFormat, TextureGLPixelFormat, TextureGLPixelType, TextureUnityFormat, TextureUnrealFormat
from gamex import PakBinary, FileSource, MetaManager, MetaInfo, MetaContent, IHaveMetaInfo
from gamex.platform import Platform
from gamex.util import _pathExtension

# typedefs
class PakFile: pass
class Reader: pass
class TextureFlags: pass
class MetaManager: pass
class MetaManager: pass

#region Binary_Bik

# Binary_Bik
class Binary_Bik(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Bik(r, f.fileSize)

    def __init__(self, r: Reader, fileSize: int):
        self.data = r.readBytes(fileSize)

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = 'BIK Video'))
        ]

#endregion

#region Binary_Dds

# Binary_Dds
class Binary_Dds(IHaveMetaInfo, ITexture):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Dds(r)

    data: dict[str, object] = None
    width: int = 0
    height: int = 0
    depth: int = 0
    mipMaps: int = 1
    flags: TextureFlags = 0

    def __init__(self, r: Reader, readMagic: bool = True):
        self.header, self.headerDXT10, self.format, self.bytes = DDS_HEADER.read(r, readMagic)
        width = self.header.dwWidth; height = self.header.dwHeight; mipMaps = max(1, self.header.dwMipMapCount)
        offset = 0
        self.spans = [range(-1, 0)] * mipMaps
        for i in range(mipMaps):
            w = width >> i; h = height >> i
            if w == 0 or h == 0: self.spans[i] = range(-1, 0); continue
            size = int(((w + 3) / 4)) * int((h + 3) / 4) * self.format[1]
            remains = min(size, len(self.bytes) - offset)
            # print(f'w: {w}, h: {h}, s: {size}, r: {remains}')
            self.spans[i] = range(offset, (offset + remains)) if remains > 0 else range(-1, 0)
            offset += remains
        self.width = width
        self.height = height
        self.mipMaps = mipMaps

    def begin(self, platform: int) -> (bytes, object, list[object]):
        match platform:
            case Platform.Type.OpenGL: format = self.format[2]
            case Platform.Type.Vulken: format = self.format[3]
            case Platform.Type.Unity: format = self.format[4]
            case Platform.Type.Unreal: format = self.format[5]
            case _: raise Exception('Unknown {platform}')
        return self.bytes, format, self.spans
    def end(self): pass

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Texture', name = os.path.basename(file.path), value = self)),
        MetaInfo('Texture', items = [
            MetaInfo(f'Format: {self.format[0]}'),
            MetaInfo(f'Width: {self.width}'),
            MetaInfo(f'Height: {self.height}'),
            MetaInfo(f'Mipmaps: {self.mipMaps}')
            ])
        ]

#endregion

#region Binary_Fsb

# Binary_Fsb
class Binary_Fsb(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Fsb(r, f.fileSize)

    def __init__(self, r: Reader, fileSize: int):
        self.data = r.readBytes(fileSize)

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = 'FSB Audio'))
        ]

#endregion

#region Binary_Img

# Binary_Img
class Binary_Img(IHaveMetaInfo, ITexture):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Img(r, f)

    data: dict[str, object] = None
    width: int = 0
    height: int = 0
    depth: int = 0
    mipMaps: int = 1
    flags: TextureFlags = 0

    def __init__(self, r: Reader, f: FileSource):
        self.image = Image.open(r.f)
        self.width, self.height = self.image.size
        bytes = self.image.tobytes(); palette = self.image.getpalette()
        # print(f'mode: {self.image.mode}')
        match self.image.mode:
            case '1': # 1-bit pixels, black and white
                self.format = (self.image.format,
                (TextureGLFormat.Luminance, TextureGLPixelFormat.Luminance, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Luminance, TextureGLPixelFormat.Luminance, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGB24,
                TextureUnrealFormat.Unknown)
            case 'P' | 'L': # 8-bit pixels, mapped to any other mode using a color palette
                self.format = (self.image.format,
                (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGB24,
                TextureUnrealFormat.Unknown)
                # 8-bit pixels, Grayscale
                if self.image.mode == 'L': palette = [x for xs in [[x, x, x] for x in range(255)] for x in xs]
            case 'RGB': # 3×8-bit pixels, true color
                self.format = (self.image.format,
                (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGB24,
                TextureUnrealFormat.Unknown)
            case 'RGBA': # 4×8-bit pixels, true color with transparency mask
                self.format = (self.image.format,
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGBA32,
                TextureUnrealFormat.Unknown)

        # decode
        if not palette: self.bytes = bytes
        else:
            self.bytes = bytearray(self.width * self.height * 3)
            Rasterize.copyPixelsByPalette(self.bytes, 3, bytes, palette, 3)

    def begin(self, platform: int) -> (bytes, object, list[object]):
        match platform:
            case Platform.Type.OpenGL: format = self.format[1]
            case Platform.Type.Vulken: format = self.format[2]
            case Platform.Type.Unity: format = self.format[3]
            case Platform.Type.Unreal: format = self.format[4]
            case _: raise Exception(f'Unknown {platform}')
        return self.bytes, format, None
    def end(self): pass

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Texture', name = os.path.basename(file.path), value = self)),
        MetaInfo('Texture', items = [
            MetaInfo(f'Format: {self.format[0]}'),
            MetaInfo(f'Width: {self.width}'),
            MetaInfo(f'Height: {self.height}'),
            MetaInfo(f'Mipmaps: {self.mipMaps}')
            ])
        ]

#endregion

#region Binary_Msg

# Binary_Msg
class Binary_Msg(IHaveMetaInfo):
    @staticmethod
    def factory(message: str): return Binary_Msg(message)

    def __init__(self, message: str):
        self.message = message

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = self.message))
        ]

#endregion

#region Binary_Snd
    
# Binary_Snd
class Binary_Snd(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Snd(r, f.fileSize)

    def __init__(self, r: Reader, fileSize: int):
        self.data = r.readBytes(fileSize)

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'AudioPlayer', name = os.path.basename(file.path), value = self.data, tag = _pathExtension(file.path)))
        ]

#endregion

#region Binary_Tga

# typedefs
class ColorMap: pass
class PIXEL: pass

# Binary_Tga
class Binary_Tga(IHaveMetaInfo, ITexture):

    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Tga(r, f)

    data: dict[str, object] = None
    width: int = 0
    height: int = 0
    depth: int = 0
    mipMaps: int = 1
    flags: TextureFlags = 0

    #region Headers

    # Image pixel format
    # The pixel data are all in little-endian. E.g. a PIXEL_ARGB32 format image, a single pixel is stored in the memory in the order of BBBBBBBB GGGGGGGG RRRRRRRR AAAAAAAA.
    class PIXEL(Enum):
        BW8 = 0     # Single channel format represents grayscale, 8-bit integer.
        BW16 = 1    # Single channel format represents grayscale, 16-bit integer.
        RGB555 = 2  # A 16-bit pixel format. The topmost bit is assumed to an attribute bit, usually ignored. Because of little-endian, this format pixel is stored in the memory in the order of GGGBBBBB ARRRRRGG.
        RGB24 = 3   # RGB color format, 8-bit per channel.
        ARGB32 = 4  # RGB color with alpha format, 8-bit per channel.

    class TYPE(Enum):
        NO_DATA = 0
        COLOR_MAPPED = 1
        TRUE_COLOR = 2
        GRAYSCALE = 3
        RLE_COLOR_MAPPED = 9
        RLE_TRUE_COLOR = 10
        RLE_GRAYSCALE = 11

    # gets the bytes per pixel by pixel format
    @staticmethod
    def pixelFormatToPixelSize(format: PIXEL) -> int:
        match format:
            case Binary_Tga.PIXEL.BW8: return 1
            case Binary_Tga.PIXEL.BW16: return 2
            case Binary_Tga.PIXEL.RGB555: return 2
            case Binary_Tga.PIXEL.RGB24: return 3
            case Binary_Tga.PIXEL.ARGB32: return 4
            case _: raise Exception('UNSUPPORTED_PIXEL_FORMAT')

    # gets the mode by pixel format
    @staticmethod
    def pixelFormatToMode(format: PIXEL) -> str:
        match format:
            case Binary_Tga.PIXEL.BW8: return 'L'
            case Binary_Tga.PIXEL.BW16: return 'I'
            # case Binary_Tga.PIXEL.RGB555: return ''
            case Binary_Tga.PIXEL.RGB24: return 'RGB'
            case Binary_Tga.PIXEL.ARGB32: return 'RGBA'
            case _: raise Exception('UNSUPPORTED_PIXEL_FORMAT')

    # convert bits to integer bytes. E.g. 8 bits to 1 byte, 9 bits to 2 bytes.
    @staticmethod
    def bitsToBytes(bits: int) -> int: return ((bits - 1) // 8 + 1) & 0xFF

    class ColorMap:
        firstIndex: int = 0
        entryCount: int = 0
        bytesPerEntry: int = 0
        pixels: bytearray = 0

    class X_Header:
        struct = ('<3b2Hb4H2b', 18)
        def __init__(self, tuple):
            self.idLength, \
            self.mapType, \
            self.imageType, \
            self.mapFirstEntry, \
            self.mapLength, \
            self.mapEntrySize, \
            self.imageXOrigin, \
            self.imageYOrigin, \
            self.imageWidth, \
            self.imageHeight, \
            self.pixelDepth, \
            self.imageDescriptor = tuple
            # remap
            self.imageType = Binary_Tga.TYPE(self.imageType)

        @property
        def IS_SUPPORTED_IMAGE_TYPE(self) -> bool: return \
            self.imageType == Binary_Tga.TYPE.COLOR_MAPPED or \
            self.imageType == Binary_Tga.TYPE.TRUE_COLOR or \
            self.imageType == Binary_Tga.TYPE.GRAYSCALE or \
            self.imageType == Binary_Tga.TYPE.RLE_COLOR_MAPPED or \
            self.imageType == Binary_Tga.TYPE.RLE_TRUE_COLOR or \
            self.imageType == Binary_Tga.TYPE.RLE_GRAYSCALE
        @property
        def IS_COLOR_MAPPED(self) -> bool: return \
            self.imageType == Binary_Tga.TYPE.COLOR_MAPPED or \
            self.imageType == Binary_Tga.TYPE.RLE_COLOR_MAPPED
        @property
        def IS_TRUE_COLOR(self) -> bool: return \
            self.imageType == Binary_Tga.TYPE.TRUE_COLOR or \
            self.imageType == Binary_Tga.TYPE.RLE_TRUE_COLOR
        @property
        def IS_GRAYSCALE(self) -> bool: return \
            self.imageType == Binary_Tga.TYPE.GRAYSCALE or \
            self.imageType == Binary_Tga.TYPE.RLE_GRAYSCALE
        @property
        def IS_RLE(self) -> bool: return \
            self.imageType == Binary_Tga.TYPE.RLE_COLOR_MAPPED or \
            self.imageType == Binary_Tga.TYPE.RLE_TRUE_COLOR or \
            self.imageType == Binary_Tga.TYPE.RLE_GRAYSCALE

        def check(self) -> None:
            MAX_IMAGE_DIMENSIONS = 65535
            if self.mapType > 1: raise Exception('UNSUPPORTED_COLOR_MAP_TYPE')
            elif self.imageType == Binary_Tga.TYPE.NO_DATA: raise Exception('NO_DATA')
            elif not self.IS_SUPPORTED_IMAGE_TYPE: raise Exception('UNSUPPORTED_IMAGE_TYPE')
            elif self.imageWidth <= 0 or self.imageWidth > MAX_IMAGE_DIMENSIONS or self.imageHeight <= 0 or self.imageHeight > MAX_IMAGE_DIMENSIONS: raise Exception('INVALID_IMAGE_DIMENSIONS')

        def getColorMap(self, r: Reader) -> object: #ColorMap
            mapSize = self.mapLength * Binary_Tga.bitsToBytes(self.mapEntrySize)
            s = ColorMap()
            if self.IS_COLOR_MAPPED:
                s.firstIndex = self.mapFirstEntry
                s.entryCount = self.mapLength
                s.bytesPerEntry = Binary_Tga.bitsToBytes(self.mapEntrySize)
                s.pixels = r.readBytes(mapSize)
            elif self.mapType == 1: r.skip(mapSize)  # The image is not color mapped at this time, but contains a color map. So skips the color map data block directly.
            return s

        def getPixelFormat(self) -> PIXEL:
            if self.IS_COLOR_MAPPED:
                if self.pixelDepth == 8:
                    match self.mapEntrySize:
                        case x if x == 15 or x == 16: return Binary_Tga.PIXEL.RGB555
                        case 24: return Binary_Tga.PIXEL.RGB24
                        case 32: return Binary_Tga.PIXEL.ARGB32
            elif self.IS_TRUE_COLOR:
                match self.pixelDepth:
                    case 16: return Binary_Tga.PIXEL.RGB555
                    case 24: return Binary_Tga.PIXEL.RGB24
                    case 32: return Binary_Tga.PIXEL.ARGB32
            elif self.IS_GRAYSCALE:
                match self.pixelDepth:
                    case 8: return Binary_Tga.PIXEL.BW8
                    case 16: return Binary_Tga.PIXEL.BW16
            else: raise Exception('UNSUPPORTED_PIXEL_FORMAT')

    #endregion

    def __init__(self, r: Reader, f: FileSource):
        header = self.header = r.readS(self.X_Header)
        header.check()
        r.skip(header.idLength)
        self.map = header.getColorMap(r)
        self.width = header.imageWidth
        self.height = header.imageHeight
        self.body = BytesIO(r.readToEnd())
        self.pixelFormat = header.getPixelFormat()
        self.pixelSize = Binary_Tga.pixelFormatToPixelSize(self.pixelFormat)
        match self.pixelFormat:
            case Binary_Tga.PIXEL.BW8: raise Exception('Not Supported')
            case Binary_Tga.PIXEL.BW16: raise Exception('Not Supported')
            case Binary_Tga.PIXEL.RGB555:
                self.format = (TextureGLFormat.Rgb5, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), \
                    (TextureGLFormat.Rgb5, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), \
                    TextureUnityFormat.RGB565, \
                    TextureUnrealFormat.Unknown
            case Binary_Tga.PIXEL.RGB24:
                self.format = (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), \
                    (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), \
                    TextureUnityFormat.RGB24, \
                    TextureUnrealFormat.Unknown
            case Binary_Tga.PIXEL.ARGB32:
                self.format = (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), \
                    (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), \
                    TextureUnityFormat.RGBA32, \
                    TextureUnrealFormat.Unknown
            case _: raise Exception(f'Unknown {self.pixelFormat}')

    @staticmethod
    def pixelToMapIndex(pixelPtr: bytearray, offfset: int) -> int: return pixelPtr[offset]

    @staticmethod
    def getColorFromMap(dest: bytearray, offset: int, index: int, map: ColorMap) -> None:
        index -= map.firstIndex
        if index < 0 and index >= map.entryCount: raise Exception('COLOR_MAP_INDEX_FAILED')
        # Buffer.BlockCopy(map.pixels, map.bytesPerEntry * index, dest, offset, map.bytesPerEntry)

    def begin(self, platform: int) -> (bytes, object, list[object]):
        # decodeRle
        def decodeRle(data: bytearray):
            isColorMapped = self.header.IS_COLOR_MAPPED
            pixelSize = self.pixelSize
            s = self.body; o = 0
            pixelCount = self.width * self.height

            isRunLengthPacket = False
            packetCount = 0
            pixelBuffer = bytearray(map.bytesPerEntry if isColorMapped else pixelSize); mv = memoryview(pixelBuffer)
            
            for _ in range(pixelCount, 0, -1):
                if packetCount == 0:
                    repetitionCountField = int.from_bytes(s.read(1), 'little', signed=False)
                    isRunLengthPacket = (repetitionCountField & 0x80) != 0
                    packetCount = (repetitionCountField & 0x7F) + 1
                    if isRunLengthPacket:
                        s.readinto(mv[0:pixelSize])
                        # in color mapped image, the pixel as the index value of the color map. The actual pixel value is found from the color map.
                        if isColorMapped: getColorFromMap(pixelBuffer, 0, pixelToMapIndex(pixelBuffer, o), map)
                if isRunLengthPacket: data[o:o+pixelSize] = pixelBuffer[0:pixelSize]
                else:
                    s.readinto(data[o:o+pixelSize])
                    # in color mapped image, the pixel as the index value of the color map. The actual pixel value is found from the color map.
                    if isColorMapped: getColorFromMap(data, o, pixelToMapIndex(data, o), map)
                packetCount -= 1
                o += pixelSize

        # decode
        def decode(data: bytearray):
            isColorMapped = self.header.IS_COLOR_MAPPED
            pixelSize = self.pixelSize
            s = self.body; o = 0
            pixelCount = self.width * self.height

            # in color mapped image, the pixel as the index value of the color map. The actual pixel value is found from the color map
            if isColorMapped:
                for _ in range(pixelCount, 0, -1):
                    s.readinto(data[o:o+pixelSize])
                    getColorFromMap(data, o, pixelToMapIndex(data, o), map)
                    o += map.bytesPerEntry
            else: s.readinto(data[:pixelCount*pixelSize])

        header = self.header
        bytes = bytearray(self.width * self.height * self.pixelSize); mv = memoryview(bytes)
        if header.IS_RLE: decodeRle(mv)
        else: decode(mv)
        self.map.pixels = None

        # flip the image if necessary, to keep the origin in upper left corner.
        flipH = (header.imageDescriptor & 0x10) != 0
        flipV = (header.imageDescriptor & 0x20) == 0
        if flipH: self.flipH(bytes)
        if flipV: self.flipV(bytes)
        
        match platform:
            case Platform.Type.OpenGL: format = self.format[1]
            case Platform.Type.Vulken: format = self.format[2]
            case Platform.Type.Unity: format = self.format[3]
            case Platform.Type.Unreal: format = self.format[4]
            case _: raise Exception(f'Unknown {platform}')
        return bytes, format, None
    def end(self): pass

    # returns the pixel at coordinates (x,y) for reading or writing.
    # if the pixel coordinates are out of bounds (larger than width/height or small than 0), they will be clamped.
    # def getPixel(self, x: int, y: int) -> int:
    #     if x < 0: x = 0
    #     elif x >= self.width: x = self.width - 1
    #     if y < 0: y = 0
    #     elif y >= self.height: y = self.height - 1
    #     return (y * self.width + x) * self.pixelSize

    def flipH(self, data: bytearray) -> None:
        mode = Binary_Tga.pixelFormatToMode(self.pixelFormat)
        img = Image.frombuffer(mode, (self.width, self.height), data, 'raw')
        data[0:] = img.transpose(Image.FLIP_LEFT_RIGHT).tobytes('raw')
        # pixelSize = self.pixelSize
        # temp = bytearray(pixelSize)
        # flipNum = self.width // 2
        # for i in range(flipNum):
        #     for j in range(self.height):
        #         p1 = self.getPixel(i, j)
        #         p2 = self.getPixel(self.width - 1 - i, j)
        #         # swap two pixels
        #         # Buffer.BlockCopy(data, p1, temp, 0, pixelSize)
        #         # Buffer.BlockCopy(data, p2, data, p1, pixelSize)
        #         # Buffer.BlockCopy(temp, 0, data, p2, pixelSize)
        pass

    def flipV(self, data: bytearray) -> None:
        mode = Binary_Tga.pixelFormatToMode(self.pixelFormat)
        img = Image.frombuffer(mode, (self.width, self.height), data, 'raw')
        data[0:] = img.transpose(Image.FLIP_TOP_BOTTOM).tobytes('raw')
        # pixelSize = self.pixelSize
        # temp = bytearray(pixelSize)
        # flipNum = self.height // 2
        # for i in range(flipNum):
        #     for j in range(self.width):
        #         p1 = self.getPixel(j, i)
        #         p2 = self.getPixel(j, self.height - 1 - i)
        #         # swap two pixels
        #         # Buffer.BlockCopy(data, p1, temp, 0, pixelSize)
        #         # Buffer.BlockCopy(data, p2, data, p1, pixelSize)
        #         # Buffer.BlockCopy(temp, 0, data, p2, pixelSize)
        pass

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Texture', name = os.path.basename(file.path), value = self)),
        MetaInfo('Binary_Tga', items = [
            MetaInfo(f'Format: {self.pixelFormat}'),
            MetaInfo(f'Width: {self.width}'),
            MetaInfo(f'Height: {self.height}')
            ])
        ]

#endregion

#region Binary_Txt

# Binary_Txt
class Binary_Txt(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Txt(r, f.fileSize)

    def __init__(self, r: Reader, fileSize: int):
        self.data = r.readBytes(fileSize).decode('utf8', 'ignore')

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = self.data))
        ]

#endregion
import os, numpy as np
from PIL import Image
from enum import Enum
from openstk.gfx.gfx_dds import DDS_HEADER
from openstk.gfx.gfx_texture import ITexture, TextureGLFormat, TextureGLPixelFormat, TextureGLPixelType, TextureUnityFormat, TextureUnrealFormat
from gamex.filesrc import FileSource
from gamex.pak import PakBinary
from gamex.meta import MetaManager, MetaInfo, MetaContent, IHaveMetaInfo
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
        self.data = r.read(fileSize)

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
        self.data = r.read(fileSize)

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
        self.image = Image.open(r)
        self.width, self.height = self.image.size
        formatType = self.image.format
        match self.image.mode:
            case '1': # 1-bit pixels, black and white
                self.format = (formatType,
                (TextureGLFormat.Luminance, TextureGLPixelFormat.Luminance, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Luminance, TextureGLPixelFormat.Luminance, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGB24,
                TextureUnrealFormat.Unknown)
            case 'L': # 8-bit pixels, Grayscale
                self.format = (formatType,
                (TextureGLFormat.Luminance, TextureGLPixelFormat.Luminance, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Luminance, TextureGLPixelFormat.Luminance, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGB24,
                TextureUnrealFormat.Unknown)
            case 'P': # 8-bit pixels, mapped to any other mode using a color palette
                self.format = (formatType,
                (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGB24,
                TextureUnrealFormat.Unknown)
            case 'RGB': # 3×8-bit pixels, true color
                self.format = (formatType,
                (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGB24,
                TextureUnrealFormat.Unknown)
            case 'RGBA': # 4×8-bit pixels, true color with transparency mask
                self.format = (formatType,
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
                TextureUnityFormat.RGBA32,
                TextureUnrealFormat.Unknown)

    def begin(self, platform: int) -> (bytes, object, list[object]):
        match platform:
            case Platform.Type.OpenGL: format = self.format[1]
            case Platform.Type.Vulken: format = self.format[2]
            case Platform.Type.Unity: format = self.format[3]
            case Platform.Type.Unreal: format = self.format[4]
            case _: raise Exception(f'Unknown {platform}')
        return self.image.tobytes(), format, None
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

    # print(r.read(f.fileSize))
    # self.image = iio.imread(r.read(f.fileSize))
    # self.image = iio.imread('imageio:chelsea.bsdf')
    # match len(self.image.shape):
    #     case 2: self.width, self.height = self.image.shape; self.channels = 1
    #     case 3: self.width, self.height, self.channels = self.image.shape
    #     case 4: _, self.width, self.height, self.channels = self.image.shape
    # match self.channels:
    #     case 1: self.format = (formatType,
    #         (TextureGLFormat.Luminance, TextureGLPixelFormat.Luminance, TextureGLPixelType.UnsignedByte),
    #         (TextureGLFormat.Luminance, TextureGLPixelFormat.Luminance, TextureGLPixelType.UnsignedByte),
    #         TextureUnityFormat.RGB24,
    #         TextureUnrealFormat.Unknown)
    #     case 3: self.format = (formatType,
    #         (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
    #         (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
    #         TextureUnityFormat.RGB24,
    #         TextureUnrealFormat.Unknown)
    #     case 4: self.format = (formatType,
    #         (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
    #         (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
    #         TextureUnityFormat.RGB24,
    #         TextureUnrealFormat.Unknown)

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
        self.data = r.read(fileSize)

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'AudioPlayer', name = os.path.basename(file.path), value = self.data, tag = _pathExtension(file.path)))
        ]

#endregion

#region Binary_Txt

# Binary_Txt
class Binary_Txt(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Txt(r, f.fileSize)

    def __init__(self, r: Reader, fileSize: int):
        self.data = r.read(fileSize).decode('utf8', 'ignore')

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = self.data))
        ]

#endregion
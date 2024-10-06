import os
from io import BytesIO
from enum import Enum
from openstk.gfx.gfx_render import Rasterize
from openstk.gfx.gfx_texture import ITexture, TextureGLFormat, TextureGLPixelFormat, TextureGLPixelType, TextureUnityFormat, TextureUnrealFormat
from gamex.filesrc import FileSource
from gamex.pak import PakBinary
from gamex.meta import MetaInfo, MetaContent, IHaveMetaInfo
from gamex.platform import Platform
from gamex.util import _pathExtension

# typedefs
class Reader: pass
class TextureFlags: pass
class BinaryPakFile: pass
class PakFile: pass
class MetaManager: pass

#region Binary_Bsp

# Binary_Bsp
class Binary_Bsp(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Bsp(r, f)

    #region Header

    class BSP_Lump:
        offset: int
        length: int

    class BSP_Header:
        struct = ('<31i', 124)
        def __init__(self, tuple):
            entities = self.entities = Binary_Bsp.BSP_Lump()
            planes = self.planes = Binary_Bsp.BSP_Lump()
            textures = self.textures = Binary_Bsp.BSP_Lump()
            vertices = self.vertices = Binary_Bsp.BSP_Lump()
            visibility = self.visibility = Binary_Bsp.BSP_Lump()
            nodes = self.nodes = Binary_Bsp.BSP_Lump()
            texInfo = self.texInfo = Binary_Bsp.BSP_Lump()
            faces = self.faces = Binary_Bsp.BSP_Lump()
            lighting = self.lighting = Binary_Bsp.BSP_Lump()
            clipNodes = self.clipNodes = Binary_Bsp.BSP_Lump()
            leaves = self.leaves = Binary_Bsp.BSP_Lump()
            markSurfaces = self.markSurfaces = Binary_Bsp.BSP_Lump()
            edges = self.edges = Binary_Bsp.BSP_Lump()
            surfEdges = self.surfEdges = Binary_Bsp.BSP_Lump()
            models = self.models = Binary_Bsp.BSP_Lump()
            self.version, \
            entities.offset, entities.length, \
            planes.offset, planes.length, \
            textures.offset, textures.length, \
            vertices.offset, vertices.length, \
            visibility.offset, visibility.length, \
            nodes.offset, nodes.length, \
            texInfo.offset, texInfo.length, \
            faces.offset, faces.length, \
            lighting.offset, lighting.length, \
            clipNodes.offset, clipNodes.length, \
            leaves.offset, leaves.length, \
            markSurfaces.offset, markSurfaces.length, \
            edges.offset, edges.length, \
            surfEdges.offset, surfEdges.length, \
            models.offset, models.length = tuple

    class SPR_Frame:
        struct = ('<5i', 20)
        def __init__(self, tuple):
            self.group, \
            self.originX, \
            self.originY, \
            self.width, \
            self.height = tuple

    MAX_MAP_HULLS = 4

    MAX_MAP_MODELS = 400
    MAX_MAP_BRUSHES = 4096
    MAX_MAP_ENTITIES = 1024
    MAX_MAP_ENTSTRING = (128 * 1024)

    MAX_MAP_PLANES = 32767
    MAX_MAP_NODES = 32767
    MAX_MAP_CLIPNODES = 32767
    MAX_MAP_LEAFS = 8192
    MAX_MAP_VERTS = 65535
    MAX_MAP_FACES = 65535
    MAX_MAP_MARKSURFACES = 65535
    MAX_MAP_TEXINFO = 8192
    MAX_MAP_EDGES = 256000
    MAX_MAP_SURFEDGES = 512000
    MAX_MAP_TEXTURES = 512
    MAX_MAP_MIPTEX = 0x200000
    MAX_MAP_LIGHTING = 0x200000
    MAX_MAP_VISIBILITY = 0x200000

    MAX_MAP_PORTALS = 65536

    #endregion

    def __init__(self, r: Reader, f: FileSource):
        # read file
        header = r.readS(self.BSP_Header)
        if header.version != 30: raise Exception('BAD VERSION')

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        # MetaInfo(None, MetaContent(type = 'Texture', name = os.path.basename(file.path), value = self)),
        MetaInfo('Bsp', items = [
            # MetaInfo(f'Width: {self.width}'),
            # MetaInfo(f'Height: {self.height}'),
            # MetaInfo(f'Mipmaps: {self.mipMaps}')
            ])
        ]

#endregion

#region Binary_Pak

# Binary_Pak
class Binary_Pak(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Pak(r)

    def __init__(self, r: Reader):
        pass

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        # MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = self.data))
        ]

#endregion

#region Binary_Spr

# Binary_Spr
class Binary_Spr(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Spr(r)

    data: dict[str, object] = None
    width: int = 0
    height: int = 0
    depth: int = 0
    mipMaps: int = 1
    flags: TextureFlags = 0

    #region Header

    SPR_MAGIC = 0x50534449 #: IDSP

    class SprType(Enum):
        VP_PARALLEL_UPRIGHT = 0
        FACING_UPRIGHT = 1
        VP_PARALLEL = 2
        ORIENTED = 3
        VP_PARALLEL_ORIENTED = 4

    class SprTextFormat(Enum):
        SPR_NORMAL = 0
        SPR_ADDITIVE = 1
        SPR_INDEXALPHA = 2
        SPR_ALPHTEST = 3

    class SprSynchType(Enum):
        Synchronized = 0
        Random = 1

    class SPR_Header:
        struct = ('<I3if3ifi', 40)
        def __init__(self, tuple):
            self.signature, \
            self.version, \
            self.type, \
            self.textFormat, \
            self.boundingRadius, \
            self.maxWidth, \
            self.maxHeight, \
            self.numFrames, \
            self.beamLen, \
            self.synchType = tuple

    class SPR_Frame:
        struct = ('<5i', 20)
        def __init__(self, tuple):
            self.group, \
            self.originX, \
            self.originY, \
            self.width, \
            self.height = tuple

    #endregion

    def __init__(self, r: Reader):
        self.format = (
              (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
              (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte),
              TextureUnityFormat.RGBA32,
              TextureUnityFormat.RGBA32)

        # read file
        header = r.readS(self.SPR_Header)
        if header.signature != self.SPR_MAGIC: raise Exception('BAD MAGIC')

        # load palette
        self.palette = r.readBytes(r.readUInt16() * 3)

        # load frames
        frames = self.frames = [self.SPR_Frame] * header.numFrames
        pixels = self.pixels = [bytearray] * header.numFrames
        for i in range(header.numFrames):
            frame = frames[i] = r.readS(self.SPR_Frame)
            pixelSize = frame.width * frame.height
            pixels[i] = r.readBytes(pixelSize)
        self.width = frames[0].width
        self.height = frames[0].height
        self.mipMaps = len(pixels)

    def begin(self, platform: int) -> (bytes, object, list[object]):
        buf = bytearray(sum([len(x) for x in self.pixels]) * 4); mv = memoryview(buf)
        spans = [range(0, 0)] * len(self.pixels); offset = 0
        for i, p in enumerate(self.pixels):
            size = len(p) * 4; span = spans[i] = range(offset, offset + size); offset += size
            Rasterize.copyPixelsByPalette(mv[span.start:span.stop], 4, p, self.palette)
        match platform:
            case Platform.Type.OpenGL: format = self.format[1]
            case Platform.Type.Vulken: format = self.format[2]
            case Platform.Type.Unity: format = self.format[3]
            case Platform.Type.Unreal: format = self.format[4]
            case _: raise Exception(f'Unknown {platform}')
        return buf, format, spans
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

#region Binary_Wad3

# Binary_Wad3
class Binary_Wad3(IHaveMetaInfo, ITexture):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Wad3(r, f)

    data: dict[str, object] = None
    width: int = 0
    height: int = 0
    depth: int = 0
    mipMaps: int = 1
    flags: TextureFlags = 0

    #region Header

    class CharInfo:
        struct = ('<2H', 4)
        def __init__(self, tuple):
            self.startOffset, \
            self.charWidth = tuple

    class Formats(Enum):
        Nonex = 0
        Tex2 = 0x40
        Pic = 0x42
        Tex = 0x43
        Fnt = 0x46

    #endregion

    def __init__(self, r: Reader, f: FileSource):
        match _pathExtension(f.path):
            case '.pic': type = self.Formats.Pic
            case '.tex': type = self.Formats.Tex
            case '.tex2': type = self.Formats.Tex2
            case '.fnt': type = self.Formats.Fnt
            case _: type = self.Formats.Nonex
        self.transparent = os.path.basename(f.path).startswith('{')
        self.format = (type, (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), (TextureGLFormat.Rgba8, TextureGLPixelFormat.Rgba, TextureGLPixelType.UnsignedByte), TextureUnityFormat.RGBA32, TextureUnityFormat.RGBA32) if self.transparent \
            else (type, (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte), TextureUnityFormat.RGB24, TextureUnityFormat.RGB24)
        self.name = r.readFUString(16) if type == self.Formats.Tex2 or type == self.Formats.Tex else None
        self.width = r.readUInt32()
        self.height = r.readUInt32()

        # validate
        if self.width > 0x1000 or self.height > 0x1000: raise Exception('Texture width or height exceeds maximum size!')
        elif self.width == 0 or self.height == 0: raise Exception('Texture width and height must be larger than 0!')

        # read pixel offsets
        if type == self.Formats.Tex2 or type == self.Formats.Tex:
            offsets = [r.readUInt32(), r.readUInt32(), r.readUInt32(), r.readUInt32()]
            if r.tell() != offsets[0]: raise Exception('BAD OFFSET')
        elif type == self.Formats.Fnt:
            self.width = 0x100
            rowCount = r.readUInt32()
            rowHeight = r.readUInt32()
            charInfos = r.readSArray(self.CharInfo, 0x100)

        # read pixels
        pixelSize = self.width * self.height
        self.pixels = [r.readBytes(pixelSize), r.readBytes(pixelSize >> 2), r.readBytes(pixelSize >> 4), r.readBytes(pixelSize >> 8)] if type == self.Formats.Tex2 or type == self.Formats.Tex \
            else [r.readBytes(pixelSize)]
        self.mipMaps = len(pixels)

        # read pallet
        r.skip(2)
        self.palette = r.readBytes(0x100 * 3)

    def begin(self, platform: int) -> (bytes, object, list[object]):
        bbp = 4 if self.transparent else 3
        buf = bytearray(sum([len(x) for x in self.pixels]) * bbp); mv = memoryview(buf)
        spans = [range(0, 0)] * len(self.pixels); offset = 0
        for i, p in enumerate(self.pixels):
            size = len(p) * bbp; span = spans[i] = range(offset, offset + size); offset += size
            Rasterize.copyPixelsByPalette(mv[span.start:span.stop], bbp, p, self.palette)
        match platform:
            case Platform.Type.OpenGL: format = self.format[1]
            case Platform.Type.Vulken: format = self.format[2]
            case Platform.Type.Unity: format = self.format[3]
            case Platform.Type.Unreal: format = self.format[4]
            case _: raise Exception(f'Unknown {platform}')
        return buf, format, spans
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
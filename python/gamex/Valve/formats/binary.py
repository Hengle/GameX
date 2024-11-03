import os
import numpy as np
from io import BytesIO
from enum import Enum, Flag
from openstk.gfx.gfx_render import Rasterize
from openstk.gfx.gfx_texture import ITexture, ITextureFrames, TextureGLFormat, TextureGLPixelFormat, TextureGLPixelType, TextureUnityFormat, TextureUnrealFormat
from openstk.poly import unsafe
from gamex import PakBinary, FileSource, MetaInfo, MetaContent, IHaveMetaInfo
from gamex.platform import Platform
from gamex.util import _pathExtension

# typedefs
class Reader: pass
class TextureFlags: pass
class BinaryPakFile: pass
class PakFile: pass
class MetaManager: pass

#region Binary_Src

# Binary_Src
class Binary_Src(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile):
        pass
        
    def __init__(self, r: Reader):
        pass

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        # MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = self.data))
        ]

#endregion

#region Binary_Spr

# Binary_Spr
class Binary_Spr(IHaveMetaInfo, ITextureFrames):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Spr(r)

    data: dict[str, object] = None
    width: int = 0
    height: int = 0
    depth: int = 0
    mipMaps: int = 1
    flags: TextureFlags = 0
    fps: int = 60

    #region Headers

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
            self.magic, \
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
        if header.magic != self.SPR_MAGIC: raise Exception('BAD MAGIC')

        # load palette
        self.palette = r.readBytes(r.readUInt16() * 3)

        # load frames
        frames = self.frames = [self.SPR_Frame] * header.numFrames
        pixels = self.pixels = [bytearray] * header.numFrames
        for i in range(header.numFrames):
            frame = frames[i] = r.readS(self.SPR_Frame)
            pixels[i] = r.readBytes(frame.width * frame.height)
        self.width = frames[0].width
        self.height = frames[0].height
        self.bytes = bytearray(self.width * self.height << 4)
        self.frame = 0

    def begin(self, platform: int) -> (bytes, object, list[object]):
        match platform:
            case Platform.Type.OpenGL: format = self.format[1]
            case Platform.Type.Vulken: format = self.format[2]
            case Platform.Type.Unity: format = self.format[3]
            case Platform.Type.Unreal: format = self.format[4]
            case _: raise Exception(f'Unknown {platform}')
        return self.bytes, format, None
    def end(self): pass

    def hasFrames(self) -> bool: return self.frame < len(self.frames)

    def decodeFrame(self) -> bool:
        p = self.pixels[self.frame]
        Rasterize.copyPixelsByPalette(self.bytes, 4, p, self.palette, 3)
        self.frame += 1
        return True

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'VideoTexture', name = os.path.basename(file.path), value = self)),
        MetaInfo('Sprite', items = [
            MetaInfo(f'Frames: {len(self.frames)}'),
            MetaInfo(f'Width: {self.width}'),
            MetaInfo(f'Height: {self.height}'),
            MetaInfo(f'Mipmaps: {self.mipMaps}')
            ])
        ]

#endregion

#region Binary_Mdl

# Binary_Mdl
class Binary_Mdl(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Mdl(r, f, s)

    #region Headers

    M_MAGIC = 0x54534449 #: IDST
    M_MAGIC2 = 0x51534449 #: IDSQ
    CoordinateAxes = 6
    SequenceBlendCount = 2
    ControllerCount = 4

    # header flags
    class HeaderFlags(Flag):
        ROCKET = 1              # leave a trail
        GRENADE = 2             # leave a trail
        GIB = 4                 # leave a trail
        ROTATE = 8              # rotate (bonus items)
        TRACER = 16             # green split trail
        ZOMGIB = 32             # small blood trail
        TRACER2 = 64            # orange split trail + rotate
        TRACER3 = 128           # purple trail
        NOSHADELIGHT = 256      # No shade lighting
        HITBOXCOLLISIONS = 512  # Use hitbox collisions
        FORCESKYLIGHT = 1024	# Forces the model to be lit by skybox lighting

    # lighting flags
    class LightFlags(Flag):
        FLATSHADE = 0x0001
        CHROME = 0x0002
        FULLBRIGHT = 0x0004
        MIPMAPS = 0x0008
        ALPHA = 0x0010
        ADDITIVE = 0x0020
        MASKED = 0x0040
        RENDER_FLAGS = CHROME | ADDITIVE | MASKED | FULLBRIGHT

    # motion flags
    class MotionFlags(Flag):
        X = 0x0001
        Y = 0x0002
        Z = 0x0004
        XR = 0x0008
        YR = 0x0010
        ZR = 0x0020
        LX = 0x0040
        LY = 0x0080
        LZ = 0x0100
        AX = 0x0200
        AY = 0x0400
        AZ = 0x0800
        AXR = 0x1000
        AYR = 0x2000
        AZR = 0x4000
        BONECONTROLLER_TYPES = X | Y | Z | XR | YR | ZR
        TYPES = 0x7FFF
        CONTROL_FIRST = X
        CONTROL_LAST = AZR
        RLOOP = 0x8000 # controller that wraps shortest distance

    # sequence flags
    class SeqFlags(Flag):
        LOOPING = 0x0001

    # bone flags
    class BoneFlags(Flag):
        NORMALS = 0x0001
        VERTICES = 0x0002
        BBOX = 0x0004
        CHROME = 0x0008 # if any of the textures have chrome on them

    # lumps
    class M_Lump:
        num: int
        offset: int
    class M_Lump2:
        num: int
        offset: int
        offset2: int

    # sequence header
    class M_SeqHeader:
        struct = ('<2I64sI', 4)
        def __init__(self, tuple):
            self.magic, \
            self.version, \
            self.name, \
            self.length = tuple

    # bones
    class M_Bone:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.name, \
            self.parent, \
            self.flags, \
            self.boneController, \
            self.value, \
            self.scale = tuple

    # bone controllers
    class M_BoneController:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.bone, \
            self.type, \
            self.start, self.end, \
            self.rest, \
            self.index = tuple

    # intersection boxes
    class M_BBox:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.bone, \
            self.group, \
            self.bbMin, self.bbMax = tuple

    # sequence groups
    class M_SeqGroup:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.label, \
            self.name, \
            self.unused1, \
            self.unused2 = tuple

    # sequence descriptions
    class M_SeqDesc:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.label, \
            self.fps, \
            self.flags, \
            self.activity, \
            self.actWeight, \
            self.events, \
            self.numFrames, \
            self.pivots, \
            self.motionType, \
            self.motionBone, \
            self.linearMovement, \
            self.automovePosIndex, \
            self.automoveAngleIndex, \
            self.bbMin, self.bbMax, \
            self.numBlends, \
            self.animIndex, \
            self.blendType, \
            self.blendStart, \
            self.blendEnd, \
            self.blendParent, \
            self.seqGroup, \
            self.entryNode, \
            self.exitNode, \
            self.nodeFlags, \
            self.nextSeq = tuple

    # events
    class M_Event:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.frame, \
            self.event, \
            self.type, \
            self.options = tuple

    # pivots
    class M_Pivot:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.org, \
            self.start, self.end = tuple

    # attachments
    class M_Attachment:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.name, \
            self.type, \
            self.bone, \
            self.org, \
            self.vectors = tuple

    # animations
    class M_Anim:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.offset = tuple

    # body part index
    class M_Bodypart:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.name, \
            self.numModels, \
            self.base, \
            self.modelIndex = tuple

    # skin info
    class M_Texture:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.name, \
            self.flags, \
            self.width, self.height, \
            self.index = tuple

    # studio models
    class M_Model:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.name, \
            self.type, \
            self.boundingRadius, \
            self.meshs, \
            self.verts, \
            self.norms, \
            self.groups = tuple

    # meshes
    class M_Mesh:
        struct = ('<?', 4)
        def __init__(self, tuple):
            self.tris, \
            self.skinRef, \
            self.norms = tuple

    # header
    class M_Header:
        struct = ('<2I64sI15f27I', 244)
        def __init__(self, tuple):
            eyePosition = self.eyePosition = np.array([0,0,0])
            min = self.min = np.array([0,0,0]); max = self.max = np.array([0,0,0])
            bbMin = self.bbMin = np.array([0,0,0]); bbMax = self.bbMax = np.array([0,0,0])
            bones = self.bones = Binary_Mdl.M_Lump()
            boneControllers = self.boneControllers = Binary_Mdl.M_Lump()
            hitboxs = self.hitboxs = Binary_Mdl.M_Lump()
            seqs = self.seqs = Binary_Mdl.M_Lump()
            seqGroups = self.seqGroups = Binary_Mdl.M_Lump()
            textures = self.textures = Binary_Mdl.M_Lump2()
            skins = self.skins = Binary_Mdl.M_Lump()
            bodyParts = self.bodyParts = Binary_Mdl.M_Lump()
            attachments = self.attachments = Binary_Mdl.M_Lump()
            sounds = self.sounds = Binary_Mdl.M_Lump()
            soundGroups = self.soundGroups = Binary_Mdl.M_Lump()
            transitions = self.transitions = Binary_Mdl.M_Lump()
            self.magic, \
            self.version, \
            self.name, \
            self.length, \
            eyePosition[0], eyePosition[1], eyePosition[2], \
            min[0], min[1], min[2], max[0], max[1], max[2], \
            bbMin[0], bbMin[1], bbMin[2], bbMax[0], bbMax[1], bbMax[2], \
            self.flags, \
            bones.num, bones.offset, \
            boneControllers.num, boneControllers.offset, \
            hitboxs.num, hitboxs.offset, \
            seqs.num, seqs.offset, \
            seqGroups.num, seqGroups.offset, \
            textures.num, textures.offset, textures.offset2, \
            self.numSkinRef, \
            skins.num, skins.offset, \
            bodyParts.num, bodyParts.offset, \
            attachments.num, attachments.offset, \
            sounds.num, sounds.offset, \
            soundGroups.num, soundGroups.offset, \
            transitions.num, transitions.offset = tuple
            self.flags = Binary_Mdl.HeaderFlags(self.flags)

    #endregion

    header: M_Header
    texture: M_Header
    sequences: list[M_SeqHeader]
    headerName: str
    isDol: bool

    def __init__(self, r: Reader, f: FileSource, s: BinaryPakFile):
        # read file
        header = self.header = r.readS(self.M_Header)
        if header.magic != self.M_MAGIC: raise Exception('BAD MAGIC')
        elif header.version != 10: raise Exception('BAD VERSION')
        self.headerName = unsafe.fixedAString(header.name, 64)
        if not self.headerName: raise Exception(f'The file "{self.headerName}" is not a model main header file')
        pathExt = _pathExtension(f.path); pathName = f.path[:-len(pathExt)]
        self.isDol = pathExt == '.dol'

        # load texture
        if header.textures.offset == 0:
            path = f'{pathName}T{pathExt}'
            print(path)
            # self.texture = s.reader(r2 =>
            # {
            #     if (r2 == null) throw new Exception($"External texture file '{path}' does not exist");
            #     var header = r2.ReadS<M_Header>();
            #     if (header.Magic != M_MAGIC) throw new FormatException("BAD MAGIC");
            #     else if (header.Version != 10) throw new FormatException("BAD VERSION");
            #     return Task.FromResult(header);
            # }, path)

        # load animations
        if header.seqGroups.num > 1:
            self.sequences = [M_SeqHeader] * header.seqGroups.num - 1
            for i in range(len(sequences)):
                path = f'{pathName}{i + 1:00}{pathExt}'
                # self.sequences[i] = s.reader(r2 =>
                # {
                #     if (r2 == null) throw new Exception($"Sequence group file '{path}' does not exist");
                #     var header = r2.ReadS<M_SeqHeader>();
                #     if (header.Magic != M_MAGIC2) throw new FormatException("BAD MAGIC");
                #     else if (header.Version != 10) throw new FormatException("BAD VERSION");
                #     return Task.FromResult(header);
                # }, path).Result;

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = self)),
        MetaInfo('Model', items = [
            MetaInfo(f'Name: {self.headerName}')
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

    #region Headers

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
        pixels = self.pixels = [r.readBytes(pixelSize), r.readBytes(pixelSize >> 2), r.readBytes(pixelSize >> 4), r.readBytes(pixelSize >> 6)] if type == self.Formats.Tex2 or type == self.Formats.Tex \
            else [r.readBytes(pixelSize)]
        self.mipMaps = len(pixels)

        # read pallet
        r.skip(2)
        p = self.palette = r.readBytes(0x100 * 3); j = 0
        if type == self.Formats.Tex2:
            for i in range(0x100):
                p[j + 0] = i
                p[j + 1] = i
                p[j + 2] = i
                j += 3

    def begin(self, platform: int) -> (bytes, object, list[object]):
        bbp = 4 if self.transparent else 3
        buf = bytearray(sum([len(x) for x in self.pixels]) * bbp); mv = memoryview(buf)
        spans = [range(0, 0)] * len(self.pixels); offset = 0
        for i, p in enumerate(self.pixels):
            size = len(p) * bbp; span = spans[i] = range(offset, offset + size); offset += size
            if self.transparent: Rasterize.copyPixelsByPaletteWithAlpha(mv[span.start:span.stop], bbp, p, self.palette, 3, 0xFF)
            else: Rasterize.copyPixelsByPalette(mv[span.start:span.stop], bbp, p, self.palette, 3)

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
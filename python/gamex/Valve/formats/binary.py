from __future__ import annotations
import os
import numpy as np
from io import BytesIO
from enum import Enum, Flag
from openstk.gfx.gfx_render import Rasterize
from openstk.gfx.gfx_texture import ITexture, ITextureFrames, TextureFlags, TextureFormat, TexturePixel
from openstk.poly import Reader, unsafe, X_LumpNO, X_LumpNO2, X_Lump2NO
from gamex import PakFile, BinaryPakFile, PakBinary, FileSource, MetaInfo, MetaManager, MetaContent, IHaveMetaInfo
from gamex.platform import Platform
from gamex.util import _pathExtension

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

    #region Headers

    S_MAGIC = 0x50534449 #: IDSP

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

    class S_Header:
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

    class S_Frame:
        struct = ('<5i', 20)
        def __init__(self, tuple):
            self.group, \
            self.originX, \
            self.originY, \
            self.width, \
            self.height = tuple

    #endregion

    def __init__(self, r: Reader):
        self.

        # read file
        header = r.readS(self.S_Header)
        if header.magic != self.S_MAGIC: raise Exception('BAD MAGIC')

        # load palette
        self.palette = r.readBytes(r.readUInt16() * 3)

        # load frames
        frames = self.frames = [self.S_Frame] * header.numFrames
        pixels = self.pixels = [bytearray] * header.numFrames
        for i in range(header.numFrames):
            frame = frames[i] = r.readS(self.S_Frame)
            pixels[i] = r.readBytes(frame.width * frame.height)
        self.width = frames[0].width
        self.height = frames[0].height
        self.bytes = bytearray(self.width * self.height << 4)
        self.frame = 0

    #region ITexture

    format: tuple = (TextureFormat.RGBA32, TexturePixel.Unknown)
    width: int = 0
    height: int = 0
    depth: int = 0
    mipMaps: int = 1
    texFlags: TextureFlags = 0
    fps: int = 60

    def begin(self, platform: str) -> (bytes, object, list[object]): return self.bytes, format, None
    def end(self): pass

    def hasFrames(self) -> bool: return self.frame < len(self.frames)

    def decodeFrame(self) -> bool:
        p = self.pixels[self.frame]
        Rasterize.copyPixelsByPalette(self.bytes, 4, p, self.palette, 3)
        self.frame += 1
        return True

    #endregion

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

#region Binary_Mdl10

# Binary_Mdl10
class Binary_Mdl10(IHaveMetaInfo, ITexture):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Mdl10(r, f, s)

    #region Headers

    M_MAGIC = 0x54534449 #: IDST
    M_MAGIC2 = 0x51534449 #: IDSQ
    CoordinateAxes = 6
    # SequenceBlendCount = 2

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

    # sequence header
    class M_SeqHeader:
        struct = ('<2i64si', 76)
        def __init__(self, tuple):
            self.magic, \
            self.version, \
            self.name, \
            self.length = tuple

    # bones
    class M_Bone:
        struct = ('<32s8i12f', 112)
        def __init__(self, tuple):
            boneController = self.boneController = np.array([0,0,0,0,0,0])
            value = self.value = np.array([0,0,0,0,0,0])
            scale = self.scale = np.array([0,0,0,0,0,0])
            self.name, \
            self.parent, \
            self.flags, \
            boneController[0], boneController[1], boneController[2], boneController[3], boneController[4], boneController[5], \
            value[0], value[1], value[2], value[3], value[4], value[5], \
            scale[0], scale[1], scale[2], scale[3], scale[4], scale[5] = tuple

    class BoneAxis:
        def __init__(self, controller: BoneController, value: float, scale: float):
            self.controller = controller
            self.value = value
            self.scale = scale

    class Bone:
        def __init__(self, s: M_Bone, id: int, controllers: list[BoneController]):
            self.name = unsafe.fixedAString(s.name, 32)
            self.parent = None
            self.parentId = s.parent
            self.flags = s.flags
            self.axes = [
                Binary_Mdl10.BoneAxis(controllers[s.boneController[0]] if s.boneController[0] != -1 else None, s.value[0], s.scale[0]),
                Binary_Mdl10.BoneAxis(controllers[s.boneController[1]] if s.boneController[1] != -1 else None, s.value[1], s.scale[1]),
                Binary_Mdl10.BoneAxis(controllers[s.boneController[2]] if s.boneController[2] != -1 else None, s.value[2], s.scale[2]),
                Binary_Mdl10.BoneAxis(controllers[s.boneController[3]] if s.boneController[3] != -1 else None, s.value[3], s.scale[3]),
                Binary_Mdl10.BoneAxis(controllers[s.boneController[4]] if s.boneController[4] != -1 else None, s.value[4], s.scale[4]),
                Binary_Mdl10.BoneAxis(controllers[s.boneController[5]] if s.boneController[5] != -1 else None, s.value[5], s.scale[5])]
            self.id = id
        def remap(bones: list[Bone]) -> None:
            for bone in bones:
                if bone.parentId != -1: bone.parent = bones[bone.parentId]

    # bone controllers
    class M_BoneController:
        struct = ('<2i2f2i', 24)
        def __init__(self, tuple):
            self.bone, \
            self.type, \
            self.start, self.end, \
            self.rest, \
            self.index = tuple

    class BoneController:
        def __init__(self, s: M_BoneController, id: int):
            self.type = s.type
            self.start = s.start; self.end = s.end
            self.rest = s.rest
            self.index = s.index
            self.id = id

    # intersection boxes
    class M_BBox:
        struct = ('<2i6f', 32)
        def __init__(self, tuple):
            bbMin = self.bbMin = np.array([0,0,0]); bbMax = self.bbMax = np.array([0,0,0])
            self.bone, \
            self.group, \
            bbMin[0], bbMin[1], bbMin[2], bbMax[0], bbMax[1], bbMax[2] = tuple

    class BBox:
        def __init__(self, s: M_BBox, bones: list[Bone]):
            self.bone = bones[s.bone]
            self.group = s.group
            self.bbMin = s.bbMin; self.bbMax = s.bbMax

    # sequence groups
    class M_SeqGroup:
        struct = ('<32s64s2i', 104)
        def __init__(self, tuple):
            self.label, \
            self.name, \
            self.unused1, \
            self.unused2 = tuple

    class SeqGroup:
        def __init__(self, s: M_SeqGroup):
            self.label = unsafe.fixedAString(s.label, 32)
            self.name = unsafe.fixedAString(s.name, 64)
            self.offset = s.unused2

    # sequence descriptions
    class M_Seq:
        struct = ('<32sf10i3f2i6f4i4f6i', 176)
        def __init__(self, tuple):
            linearMovement = self.linearMovement = np.array([0,0,0])
            bbMin = self.bbMin = np.array([0,0,0]); bbMax = self.bbMax = np.array([0,0,0])
            events = self.events = X_LumpNO()
            pivots = self.pivots = X_LumpNO()
            blendType = self.blendType = np.array([0,0])
            blendStart = self.blendStart = np.array([0,0])
            blendEnd = self.blendEnd = np.array([0,0])
            self.label, \
            self.fps, \
            self.flags, \
            self.activity, \
            self.actWeight, \
            events.num, events.offset, \
            self.numFrames, \
            pivots.num, pivots.offset, \
            self.motionType, \
            self.motionBone, \
            linearMovement[0], linearMovement[1], linearMovement[2], \
            self.automovePosIndex, \
            self.automoveAngleIndex, \
            bbMin[0], bbMin[1], bbMin[2], bbMax[0], bbMax[1], bbMax[2], \
            self.numBlends, \
            self.animIndex, \
            blendType[0], blendType[1], \
            blendStart[0], blendStart[1], \
            blendEnd[0], blendEnd[1], \
            self.blendParent, \
            self.seqGroup, \
            self.entryNode, \
            self.exitNode, \
            self.nodeFlags, \
            self.nextSeq = tuple

    class SeqBlend:
        def __init__(self, type: int, start: float, end: float):
            self.type = type
            self.start = start
            self.end = end

    class SeqPivot:
        def __init__(self, origin: np.ndarray, start: int, end: int):
            self.origin = origin
            self.start = start
            self.end = end
    
    class SeqAnimation:
        def __init__(self, axis: list[list[self.M_AnimValue]]):
            self.axis = axis

    class Seq:
        def __init__(self, r: Reader, s: M_Seq, sequences: list[M_SeqHeader], zeroGroupOffset: int, isXashModel: bool, bones: list[Bone]):
            if s.seqGroup < 0 or (s.seqGroup != 0 and (s.seqGroup - 1) >= len(sequences)): raise Exception('Invalid seqgroup value')
            self.label = unsafe.fixedAString(s.label, 32)
            self.fps = s.fps
            self.flags = s.flags
            self.activity = s.activity
            self.actWeight = s.actWeight
            r.seek(s.events.offset); self.events = r.readSArray(Binary_Mdl10.M_Event, s.events.num)
            self.sortedEvents = self.events
            self.numFrames = s.numFrames
            if isXashModel: self.pivots = []
            else: r.seek(s.pivots.offset); self.pivots = r.readSArray(Binary_Mdl10.M_Pivot, s.pivots.num)
            self.motionType = s.motionType
            self.motionBone = s.motionBone
            self.bbMin = s.bbMin; self.bbMax = s.bbMax
            self.animationBlends = Binary_Mdl10.Seq.getAnimationBlends(r, s, sequences, zeroGroupOffset, len(bones))
            self.blend = [Binary_Mdl10.SeqBlend(s.blendType[0], s.blendStart[0], s.blendEnd[0]), Binary_Mdl10.SeqBlend(s.blendType[1], s.blendStart[1], s.blendEnd[1])]
            self.entryNode = s.entryNode
            self.exitNode = s.exitNode
            self.nodeFlags = s.nodeFlags
            self.nextSequence = s.nextSeq
        @staticmethod
        def getAnimationBlends(r: Reader, s: M_Seq, sequences: list[M_SeqHeader], zeroGroupOffset: int, numBones: int) -> list[SeqAnimation]:
            (sr, so) = (r, zeroGroupOffset + s.animIndex) if s.seqGroup == 0 else (sequences[s.seqGroup - 1][0], s.animIndex)
            sr.seek(so)
            anim = sr.readS(Binary_Mdl10.M_Anim)
            blends = [list[Binary_Mdl10.SeqAnimation]] * s.numBlends
            for i in range(s.numBlends):
                animations = [Binary_Mdl10.SeqAnimation] * numBones
                for b in range(numBones):
                    animation = Binary_Mdl10.SeqAnimation([Binary_Mdl10.M_AnimValue] * Binary_Mdl10.CoordinateAxes)
                    for j in range(Binary_Mdl10.CoordinateAxes):
                        if anim.offsets[j] != 0:
                            sr.seek(so + anim.offsets[j])
                            values = [sr.readS(Binary_Mdl10.M_AnimValue)]
                            if s.numFrames > 0:
                                for f in range(s.numFrames):
                                    v = values[-1]
                                    f += v.total
                                    values.extend(sr.readSArray(Binary_Mdl10.M_AnimValue, 1 + v.valid))
                            animation.axis[j] = values
                    animations[b] = animation
                blends[i] = animations
            return blends

    # events
    class M_Event:
        struct = ('<3i64s', 76)
        def __init__(self, tuple):
            self.frame, \
            self.event, \
            self.type, \
            self.options = tuple

    # pivots
    class M_Pivot:
        struct = ('<3f2i', 4)
        def __init__(self, tuple):
            self.org, \
            self.start, self.end = tuple

    # attachments
    class M_Attachment:
        struct = ('<32s2i12f', 88)
        def __init__(self, tuple):
            vector0 = self.vector0 = np.array([0,0,0])
            vector1 = self.vector1 = np.array([0,0,0])
            vector2 = self.vector2 = np.array([0,0,0])
            org = self.org = np.array([0,0,0])
            self.name, \
            self.type, \
            self.bone, \
            org[0], org[1], org[2], \
            vector0[0], vector0[1], vector0[2], \
            vector1[0], vector1[1], vector1[2], \
            vector2[0], vector2[1], vector2[2] = tuple

    class Attachment:
        def __init__(self, s: M_Attachment, bones: list[Bone]):
            self.name = unsafe.fixedAString(s.name, 32)
            self.type = s.type
            self.bone = bones[s.bone]
            self.org = s.org
            self.vectors = [s.vector0, s.vector1, s.vector2]

    # animations
    class M_Anim:
        struct = ('<6H', 12)
        def __init__(self, tuple):
            offsets = self.offsets = np.array([0,0,0,0,0,0])
            offsets[0], offsets[1], offsets[2], offsets[3], offsets[4], offsets[5] = tuple

    class M_AnimValue:
        struct = ('<2B', 2)
        def __init__(self, tuple):
            self.valid, \
            self.total = tuple

    # body part index
    class M_Bodypart:
        struct = ('<64s3i', 76)
        def __init__(self, tuple):
            models = self.models = X_LumpNO2()
            self.name, \
            models.num, models.offset, models.offset2 = tuple

    class Bodypart:
        def __init__(self, r: Reader, s: M_Bodypart, bones: list[Bone]):
            self.name = unsafe.fixedAString(s.name, 32)
            self.base = s.models.offset
            r.seek(s.models.offset2); self.models = [Binary_Mdl10.Model(r, x, bones) for x in r.readSArray(Binary_Mdl10.M_Model, s.models.num)]

    # skin info
    class M_Texture:
        struct = ('<64s4i', 80)
        def __init__(self, tuple):
            self.name, \
            self.flags, \
            self.width, self.height, \
            self.index = tuple

    class Texture:
        def __init__(self, r: Reader, s: M_Texture):
            self.name = unsafe.fixedAString(s.name, 64)
            self.flags = s.flags
            self.width = s.width; self.height = s.height
            r.seek(s.index); self.pixels = r.readBytes(self.width * self.height)
            self.palette = r.readBytes(3 * 256)

    # studio models
    class M_Model:
        struct = ('<64sif10i', 112)
        def __init__(self, tuple):
            meshs = self.meshs = X_LumpNO()
            verts = self.verts = X_LumpNO2()
            norms = self.norms = X_LumpNO2()
            groups = self.groups = X_LumpNO()
            self.name, \
            self.type, \
            self.boundingRadius, \
            meshs.num, meshs.offset, \
            verts.num, verts.offset, verts.offset2, \
            norms.num, norms.offset, norms.offset2, \
            groups.num, groups.offset = tuple

    class ModelVertex:
        def __init__(self, bone: Bone, vertex: np.ndarray):
            self.bone = bone
            self.vertex = vertex
        @staticmethod
        def create(r: Reader, s: M_Lump2, bones: list[Bone]) -> list[ModelVertex]:
            r.seek(s.offset); boneIds = r.readPArray(None, 'B', s.num)
            r.seek(s.offset2); verts = r.readPArray(np.array, '3f', s.num)
            return [Binary_Mdl10.ModelVertex(bones[boneIds[i]], verts[i]) for i in range(s.num)]

    class Model:
        def __init__(self, r: Reader, s: M_Model, bones: list[Bone]):
            self.name = unsafe.fixedAString(s.name, 64)
            self.type = s.type
            self.boundingRadius = s.boundingRadius
            r.seek(s.meshs.offset); self.meshs = [Binary_Mdl10.Mesh(r, x) for x in r.readSArray(Binary_Mdl10.M_Mesh, s.meshs.num)]
            self.vertices = Binary_Mdl10.ModelVertex.create(r, s.verts, bones)
            self.normals = Binary_Mdl10.ModelVertex.create(r, s.norms, bones)

    # meshes
    class M_Mesh:
        struct = ('<5i', 20)
        def __init__(self, tuple):
            tris = self.tris = X_LumpNO()
            norms = self.norms = X_LumpNO()
            tris.num, tris.offset, \
            self.skinRef, \
            norms.num, norms.offset = tuple

    class Mesh:
        def __init__(self, r: Reader, s: M_Mesh):
            r.seek(s.tris.offset); self.triangles = r.readPArray(None, 'H', s.tris.num) #TODO
            self.numTriangles = s.tris.num
            self.numNorms = s.norms.num
            self.skinRef = s.skinRef

    # header
    class M_Header:
        struct = ('<2I64sI15f27I', 244)
        def __init__(self, tuple):
            eyePosition = self.eyePosition = np.array([0,0,0])
            min = self.min = np.array([0,0,0]); max = self.max = np.array([0,0,0])
            bbMin = self.bbMin = np.array([0,0,0]); bbMax = self.bbMax = np.array([0,0,0])
            bones = self.bones = X_LumpNO()
            boneControllers = self.boneControllers = X_LumpNO()
            hitboxs = self.hitboxs = X_LumpNO()
            seqs = self.seqs = X_LumpNO()
            seqGroups = self.seqGroups = X_LumpNO()
            textures = self.textures = X_LumpNO2()
            skinFamilies = self.skinFamilies = X_LumpNO()
            bodyparts = self.bodyparts = X_LumpNO()
            attachments = self.attachments = X_LumpNO()
            sounds = self.sounds = X_LumpNO()
            soundGroups = self.soundGroups = X_LumpNO()
            transitions = self.transitions = X_LumpNO()
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
            skinFamilies.num, skinFamilies.offset, \
            bodyparts.num, bodyparts.offset, \
            attachments.num, attachments.offset, \
            sounds.num, sounds.offset, \
            soundGroups.num, soundGroups.offset, \
            transitions.num, transitions.offset = tuple
            self.flags = Binary_Mdl10.HeaderFlags(self.flags)

    #endregion

    name: str
    isDol: bool
    isXashModel: bool
    eyePosition: np.ndarray
    boundingMin: np.ndarray
    boundingMax: np.ndarray
    clippingMin: np.ndarray
    clippingMax: np.ndarray
    flags: HeaderFlags
    boneControllers: list[BoneController]
    bones: list[Bone]
    hitboxes: list[BBox]
    sequenceGroups: list[SeqGroup]
    sequences: list[Seq]
    attachments: list[Attachment]
    bodyparts: list[Bodypart]
    textures: list[Texture]
    skinFamilies: list[list[int]]
    transitions: list[bytearray]

    def __init__(self, r: Reader, f: FileSource, s: BinaryPakFile):
        # read file
        header = r.readS(self.M_Header)
        if header.magic != self.M_MAGIC: raise Exception('BAD MAGIC')
        elif header.version != 10: raise Exception('BAD VERSION')
        self.name = unsafe.fixedAString(header.name, 64)
        if not self.name: raise Exception(f'The file "{self.name}" is not a model main header file')
        path = f.path; pathExt = _pathExtension(path); pathName = path[:-len(pathExt)]
        self.isDol = pathExt == '.dol'
        if self.isDol: raise Exception('Not Implemented')
        # Xash models store the offset to the second header in this variable.
        self.isXashModel = header.sounds.num != 0 # If it's not zero this is a Xash model.
        self.hasTextureFile = header.textures.offset == 0

        # load texture
        def _texture(r2: Reader):
            if not r2: raise Exception(f'External texture file "{path}" does not exist')
            header = r2.readS(self.M_Header)
            if header.magic != self.M_MAGIC: raise Exception('BAD MAGIC')
            elif header.version != 10: raise Exception('BAD VERSION')
            return (r2, header)
        (tr, theader) = s.readerT(_texture, path := f'{pathName}T{pathExt}', True) if self.hasTextureFile else (r, header)

        # load animations
        sequences: list[(Reader, M_SeqHeader)] = None
        if header.seqGroups.num > 1:
            sequences = [(Reader, self.M_SeqHeader)] * (header.seqGroups.num - 1)
            for i in range(len(sequences)):
                def _sequences(r2: Reader):
                    if not r2: raise Exception(f'Sequence group file "{path}" does not exist')
                    header = r2.readS(self.M_SeqHeader)
                    if header.magic != self.M_MAGIC2: raise Exception('BAD MAGIC')
                    elif header.version != 10: raise Exception('BAD VERSION')
                    return (r2, header)
                sequences[i] = s.readerT(_sequences, path := f'{pathName}{i + 1:02}{pathExt}', True)

        # validate
        if header.bones.num < 0 \
            or header.boneControllers.num < 0 \
            or header.hitboxs.num < 0 \
            or header.seqs.num < 0 \
            or header.seqGroups.num < 0 \
            or header.bodyparts.num < 0 \
            or header.attachments.num < 0 \
            or header.transitions.num < 0 \
            or theader.textures.num < 0 \
            or theader.skinFamilies.num < 0 \
            or theader.numSkinRef < 0: raise Exception('Negative data chunk count value')

        # build
        self.eyePosition = header.eyePosition
        self.boundingMin = header.min
        self.boundingMax = header.max
        self.clippingMin = header.bbMin
        self.clippingMax = header.bbMax
        self.flags = header.flags
        r.seek(header.boneControllers.offset); self.boneControllers = [self.BoneController(x, i) for i,x in enumerate(r.readSArray(self.M_BoneController, header.boneControllers.num))]
        r.seek(header.bones.offset); self.bones = [self.Bone(x, i, self.boneControllers) for i,x in enumerate(r.readSArray(self.M_Bone, header.bones.num))]; self.Bone.remap(self.bones)
        r.seek(header.hitboxs.offset); self.hitboxes = [self.BBox(x, self.bones) for x in r.readSArray(self.M_BBox, header.hitboxs.num)]
        r.seek(header.seqGroups.offset); self.sequenceGroups = [self.SeqGroup(x) for x in r.readSArray(self.M_SeqGroup, header.seqGroups.num)]
        zeroGroupOffset = self.sequenceGroups[0].offset if self.sequenceGroups else 0
        r.seek(header.seqs.offset); self.sequences = [self.Seq(r, x, sequences, zeroGroupOffset, self.isXashModel, self.bones) for x in r.readSArray(self.M_Seq, header.seqs.num)]
        r.seek(header.attachments.offset); self.attachments = [self.Attachment(x, self.bones) for x in r.readSArray(self.M_Attachment, header.attachments.num)]
        r.seek(header.bodyparts.offset); self.bodyparts = [self.Bodypart(r, x, self.bones) for x in r.readSArray(self.M_Bodypart, header.bodyparts.num)]
        r.seek(header.transitions.offset); self.transitions = tr.readFArray(lambda x: x.readBytes(1), header.transitions.num)
        tr.seek(theader.textures.offset); self.textures = [self.Texture(tr, x) for x in tr.readSArray(self.M_Texture, theader.textures.num)]
        tr.seek(theader.skinFamilies.offset); self.skinFamilies = tr.readFArray(lambda x: x.readPArray(None, 'H', theader.numSkinRef), theader.skinFamilies.num)

        # texture

    #region ITexture

    format: tuple = (TextureFormat.RGB24, TexturePixel.Unknown)
    width: int = 0
    height: int = 0
    depth: int = 0
    mipMaps: int = 1
    texFlags: TextureFlags = 0

    def begin(self, platform: str) -> (bytes, object, list[object]):
        tex = self.textures[0]
        self.width = tex.width; self.height = tex.height
        buf = bytearray(self.width * self.height * 3); mv = memoryview(buf)
        Rasterize.copyPixelsByPalette(mv, 3, tex.pixels, tex.palette, 3)
        return buf, self.format, None
    def end(self): pass

    #endregion

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Texture', name = os.path.basename(file.path), value = self)),
        MetaInfo('Model', items = [
            MetaInfo(f'Name: {self.name}')
            ])
        ]

#endregion

#region Binary_Mdl40

# Binary_Mdl40
class Binary_Mdl40(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Mdl40(r, f, s)

    #region Headers

    M_MAGIC = 0x54534449 #: IDST

    # header flags
    class HeaderFlags(Flag):
        AUTOGENERATED_HITBOX = 0x00000001           # This flag is set if no hitbox information was specified
        USES_ENV_CUBEMAP = 0x00000002               # This flag is set at loadtime, not mdl build time so that we don't have to rebuild models when we change materials.
        FORCE_OPAQUE = 0x00000004                   # Use this when there are translucent parts to the model but we're not going to sort it
        TRANSLUCENT_TWOPASS = 0x00000008            # Use this when we want to render the opaque parts during the opaque pass and the translucent parts during the translucent pass
        STATIC_PROP = 0x00000010                    # This is set any time the .qc files has $staticprop in it
        USES_FB_TEXTURE = 0x00000020                # This flag is set at loadtime, not mdl build time so that we don't have to rebuild models when we change materials.
        HASSHADOWLOD = 0x00000040                   # This flag is set by studiomdl.exe if a separate "$shadowlod" entry was present for the .mdl (the shadow lod is the last entry in the lod list if present)
        USES_BUMPMAPPING = 0x00000080               # This flag is set at loadtime, not mdl build time so that we don't have to rebuild models when we change materials.	S
        USE_SHADOWLOD_MATERIALS = 0x00000100        # This flag is set when we should use the actual materials on the shadow LOD instead of overriding them with the default one (necessary for translucent shadows)
        OBSOLETE = 0x00000200                       # This flag is set when we should use the actual materials on the shadow LOD instead of overriding them with the default one (necessary for translucent shadows)
        UNUSED = 0x00000400 	                    # N/A
        NO_FORCED_FADE = 0x00000800 	            # This flag is set at mdl build time
        FORCE_PHONEME_CROSSFADE = 0x00001000	    # The npc will lengthen the viseme check to always include two phonemes
        CONSTANT_DIRECTIONAL_LIGHT_DOT = 0x00002000 # This flag is set when the .qc has $constantdirectionallight in it
        FLEXES_CONVERTED = 0x00004000	            # Flag to mark delta flexes as already converted from disk format to memory format
        BUILT_IN_PREVIEW_MODE = 0x00008000	        # Indicates the studiomdl was built in preview mode
        AMBIENT_BOOST = 0x00010000	                # Ambient boost (runtime flag)
        DO_NOT_CAST_SHADOWS = 0x00010000	        # Forces the model to be lit by skybox lighting
        CAST_TEXTURE_SHADOWS = 0x00010000 	        # Don't cast shadows from this model (useful on first-person models)
        NA_1 = 0x00080000 	                        # N/A (undefined)
        NA_2 = 0x00100000 	                        # N/A (undefined)
        VERT_ANIM_FIXED_POINT_SCALE = 0x00200000    # flagged on load to indicate no animation events on this model

    class M_Texture:
        struct = ('<6i10s', 80)
        def __init__(self, tuple):
            self.nameOffset, \
            self.flags, \
            self.used, \
            self.unused, \
            self.material, \
            self.clientMaterial, \
            self.unused2 = tuple

    class M_Header:
        struct = ('<3i64si18f44if11i4B3if3i', 408)
        def __init__(self, tuple):
            eyePosition = self.eyePosition = np.array([0,0,0])
            illumPosition = self.illumPosition = np.array([0,0,0])
            min = self.min = np.array([0,0,0]); max = self.max = np.array([0,0,0])
            bbMin = self.bbMin = np.array([0,0,0]); bbMax = self.bbMax = np.array([0,0,0])
            bones = self.bones = X_LumpNO()
            boneControllers = self.boneControllers = X_LumpNO()
            hitboxs = self.hitboxs = X_LumpNO()
            localAnims = self.localAnims = X_LumpNO()
            localSeqs = self.localSeqs = X_LumpNO()
            events = self.events = X_LumpNO()
            textures = self.textures = X_LumpNO()
            texturesDirs = self.texturesDirs = X_LumpNO()
            skinFamilies = self.skinFamilies = X_LumpNO2()
            bodyparts = self.bodyparts = X_LumpNO()
            attachments = self.attachments = X_LumpNO()
            localNodes = self.localNodes = X_LumpNO2()
            flexs = self.flexs = X_LumpNO()
            flexControllers = self.flexControllers = X_LumpNO()
            flexRules = self.flexRules = X_LumpNO()
            ikChains = self.ikChains = X_LumpNO()
            mouths = self.mouths = X_LumpNO()
            localPoseParams = self.localPoseParams = X_LumpNO()
            keyValues = self.keyValues = X_LumpNO()
            ikLocks = self.ikLocks = X_LumpNO()
            includeModel = self.includeModel = X_LumpNO()
            animBlocks = self.animBlocks = X_LumpNO()
            flexControllerUI = self.flexControllerUI = X_LumpNO()
            self.magic, \
            self.version, \
            self.checksum, \
            self.name, \
            self.length, \
            eyePosition[0], eyePosition[1], eyePosition[2], \
            illumPosition[0], illumPosition[1], illumPosition[2], \
            min[0], min[1], min[2], max[0], max[1], max[2], \
            bbMin[0], bbMin[1], bbMin[2], bbMax[0], bbMax[1], bbMax[2], \
            self.flags, \
            bones.num, bones.offset, \
            boneControllers.num, boneControllers.offset, \
            hitboxs.num, hitboxs.offset, \
            localAnims.num, localAnims.offset, \
            localSeqs.num, localSeqs.offset, \
            events.num, events.offset, \
            textures.num, textures.offset, \
            texturesDirs.num, texturesDirs.offset, \
            skinFamilies.num, skinFamilies.offset, skinFamilies.offset2, \
            bodyparts.num, bodyparts.offset, \
            attachments.num, attachments.offset, \
            localNodes.num, localNodes.offset, localNodes.offset2, \
            flexs.num, flexs.offset, \
            flexControllers.num, flexControllers.offset, \
            flexRules.num, flexRules.offset, \
            ikChains.num, ikChains.offset, \
            mouths.num, mouths.offset, \
            localPoseParams.num, localPoseParams.offset, \
            self.surfacePropIndex, \
            keyValues.num, keyValues.offset, \
            ikLocks.num, ikLocks.offset, \
            self.mass, \
            self.contents, \
            includeModel.num, includeModel.offset, \
            self.virtualModel, \
            self.animBlockNameIndex, \
            animBlocks.num, animBlocks.offset, \
            self.animBlockModel, \
            self.boneNameIndex, \
            self.vertexBase, \
            self.offsetBase, \
            self.directionalDotProduct, \
            self.rootLod, \
            self.numAllowedRootLods, \
            self.unused0, \
            self.unused1, \
            flexControllerUI.num, flexControllerUI.offset, \
            self.vertAnimFixedPointScale, \
            self.unused2, \
            self.header2Index, \
            self.unused3 = tuple
            self.flags = Binary_Mdl40.HeaderFlags(self.flags)

    class M_Header2:
        struct = ('<3ifi64s', 244)
        def __init__(self, tuple):
            srcBoneTransform = self.srcBoneTransform = X_LumpNO()
            srcBoneTransform.num, srcBoneTransform.offset, \
            self.illumPositionAttachmentIndex, \
            self.maxEyeDeflection, \
            self.linearBoneIndex, \
            self.unknown = tuple

    #endregion

    name: str
    eyePosition: np.ndarray
    illumPosition: np.ndarray
    boundingMin: np.ndarray
    boundingMax: np.ndarray
    clippingMin: np.ndarray
    clippingMax: np.ndarray
    flags: HeaderFlags

    def __init__(self, r: Reader, f: FileSource, s: BinaryPakFile):
        # read file
        header = r.readS(self.M_Header)
        if header.magic != self.M_MAGIC: raise Exception('BAD MAGIC')
        elif header.version < 40: raise Exception('BAD VERSION')
        self.name = unsafe.fixedAString(header.name, 64)
        if not self.name: raise Exception(f'The file "{self.name}" is not a model main header file')
        path = f.path; pathExt = _pathExtension(path); pathName = path[:-len(pathExt)]

        # # load texture
        # def _texture(r2: Reader):
        #     if not r2: raise Exception(f'External texture file "{path}" does not exist')
        #     header = r2.readS(self.M_Header)
        #     if header.magic != self.M_MAGIC: raise Exception('BAD MAGIC')
        #     elif header.version != 10: raise Exception('BAD VERSION')
        #     return (r2, header)
        # (tr, theader) = s.readerT(_texture, path := f'{pathName}T{pathExt}', True) if self.hasTextureFile else (r, header)

        # # load animations
        # sequences: list[(Reader, M_SeqHeader)] = None
        # if header.seqGroups.num > 1:
        #     sequences = [(Reader, self.M_SeqHeader)] * (header.seqGroups.num - 1)
        #     for i in range(len(sequences)):
        #         def _sequences(r2: Reader):
        #             if not r2: raise Exception(f'Sequence group file "{path}" does not exist')
        #             header = r2.readS(self.M_SeqHeader)
        #             if header.magic != self.M_MAGIC2: raise Exception('BAD MAGIC')
        #             elif header.version != 10: raise Exception('BAD VERSION')
        #             return (r2, header)
        #         sequences[i] = s.readerT(_sequences, path := f'{pathName}{i + 1:02}{pathExt}', True)

        # # validate
        # if header.bones.num < 0 \
        #     or header.boneControllers.num < 0 \
        #     or header.hitboxs.num < 0 \
        #     or header.seqs.num < 0 \
        #     or header.seqGroups.num < 0 \
        #     or header.bodyparts.num < 0 \
        #     or header.attachments.num < 0 \
        #     or header.transitions.num < 0 \
        #     or theader.textures.num < 0 \
        #     or theader.skinFamilies.num < 0 \
        #     or theader.numSkinRef < 0: raise Exception('Negative data chunk count value')

        # build
        self.eyePosition = header.eyePosition
        self.illumPosition = header.illumPosition
        self.boundingMin = header.min
        self.boundingMax = header.max
        self.clippingMin = header.bbMin
        self.clippingMax = header.bbMax
        self.flags = header.flags
        # r.seek(header.boneControllers.offset); self.boneControllers = [self.BoneController(x, i) for i,x in enumerate(r.readSArray(self.M_BoneController, header.boneControllers.num))]
        # r.seek(header.bones.offset); self.bones = [self.Bone(x, i, self.boneControllers) for i,x in enumerate(r.readSArray(self.M_Bone, header.bones.num))]; self.Bone.remap(self.bones)
        # r.seek(header.hitboxs.offset); self.hitboxes = [self.BBox(x, self.bones) for x in r.readSArray(self.M_BBox, header.hitboxs.num)]
        # r.seek(header.seqGroups.offset); self.sequenceGroups = [self.SeqGroup(x) for x in r.readSArray(self.M_SeqGroup, header.seqGroups.num)]
        # zeroGroupOffset = self.sequenceGroups[0].offset if self.sequenceGroups else 0
        # r.seek(header.seqs.offset); self.sequences = [self.Seq(r, x, sequences, zeroGroupOffset, self.isXashModel, self.bones) for x in r.readSArray(self.M_Seq, header.seqs.num)]
        # r.seek(header.attachments.offset); self.attachments = [self.Attachment(x, self.bones) for x in r.readSArray(self.M_Attachment, header.attachments.num)]
        # r.seek(header.bodyparts.offset); self.bodyparts = [self.Bodypart(r, x, self.bones) for x in r.readSArray(self.M_Bodypart, header.bodyparts.num)]
        # r.seek(header.transitions.offset); self.transitions = tr.readFArray(lambda x: x.readBytes(1), header.transitions.num)
        # tr.seek(theader.textures.offset); self.textures = [self.Texture(tr, x) for x in tr.readSArray(self.M_Texture, theader.textures.num)]
        # tr.seek(theader.skinFamilies.offset); self.skinFamilies = tr.readFArray(lambda x: x.readPArray(None, 'H', theader.numSkinRef), theader.skinFamilies.num)

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = 'self')),
        MetaInfo('Model', items = [
            MetaInfo(f'Name: {self.name}')
            ])
        ]

#endregion

#region Binary_Wad3

# Binary_Wad3
class Binary_Wad3(IHaveMetaInfo, ITexture):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Wad3(r, f)

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
        self.format = (type, (TextureFormat.RGBA32, TexturePixel.Unknown)) if self.transparent \
            else (type, (TextureFormat.RGB24, TexturePixel.Unknown))
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

    #region ITexture

    width: int = 0
    height: int = 0
    depth: int = 0
    mipMaps: int = 1
    texFlags: TextureFlags = 0

    def begin(self, platform: str) -> (bytes, object, list[object]):
        bbp = 4 if self.transparent else 3
        buf = bytearray(sum([len(x) for x in self.pixels]) * bbp); mv = memoryview(buf)
        spans = [range(0, 0)] * len(self.pixels); offset = 0
        for i, p in enumerate(self.pixels):
            size = len(p) * bbp; span = spans[i] = range(offset, offset + size); offset += size
            if self.transparent: Rasterize.copyPixelsByPaletteWithAlpha(mv[span.start:span.stop], bbp, p, self.palette, 3, 0xFF)
            else: Rasterize.copyPixelsByPalette(mv[span.start:span.stop], bbp, p, self.palette, 3)
        return buf, self.format[1], spans
    def end(self): pass

    #endregion

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
import os, ctypes
from enum import Enum
from io import BytesIO
from openstk.gfx.gfx_render import Rasterize
from openstk.gfx.gfx_texture import ITextureFrames, TextureGLFormat, TextureGLPixelFormat, TextureGLPixelType, TextureUnityFormat, TextureUnrealFormat
from gamex.filesrc import FileSource
from gamex.pak import PakBinary
from gamex.meta import MetaInfo, MetaContent, IHaveMetaInfo
from gamex.platform import Platform

# typedefs
class Reader: pass
class TextureFlags: pass
class BinaryPakFile: pass
class PakFile: pass
class MetaManager: pass

#region Binary_Fli

# Binary_Fli
class Binary_Fli(IHaveMetaInfo, ITextureFrames): #IDisposable
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Fli(r, f)

    format: tuple
    width: int = 0
    height: int = 0
    depth: int = 0
    mipMaps: int = 1
    flags: TextureFlags = 0
    Fps: int = 1

    #region Headers

    MAGIC = 0xAF12

    class X_Header:
        struct = ('<I4H', 12)
        def __init__(self, tuple):
            self.size, \
            self.type, \
            self.numFrames, \
            self.width, \
            self.height = tuple

    class ChunkType(Enum):
        COLOR_256 = 0x4     # COLOR_256
        DELTA_FLC = 0x7     # DELTA_FLC (FLI_SS2)
        BYTE_RUN = 0xF      # BYTE_RUN
        FRAME = 0xF1FA      # FRAME_TYPE

    class X_ChunkHeader:
        struct = ('<IH', 6)
        def __init__(self, tuple):
            self.size, \
            self.type = tuple
            # remap
            self.type = Binary_Fli.ChunkType(self.type)
        def isValid(self) -> bool: return self.type == ChunkType.COLOR_256 or self.Type == ChunkType.DELTA_FLC or self.Type == ChunkType.BYTE_RUN

    class X_FrameHeader:
        struct = ('<5H', 10)
        def __init__(self, tuple):
            self.numChunks, \
            self.delay, \
            self.reserved, \
            self.widthOverride, \
            self.heightOverride = tuple

    class OpCode(Enum):
        PACKETCOUNT = 0
        UNDEFINED = 1
        LASTPIXEL = 2
        LINESKIPCOUNT = 3

    #endregion

    frames: int = 1

    def __init__(self, r: Reader, f: FileSource):
        # read events
        # self.events = S.GetEvents(f'{Path.GetFileNameWithoutExtension(f.Path).ToLowerInvariant()}.evt')

        # read header
        header = r.readS(self.X_Header)
        if header.type != self.MAGIC: raise Exception('BAD MAGIC')
        self.format = (
            (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
            (TextureGLFormat.Rgb8, TextureGLPixelFormat.Rgb, TextureGLPixelType.UnsignedByte),
            TextureUnityFormat.RGB24,
            TextureUnrealFormat.Unknown)
        self.width = header.width
        self.height = header.height
        self.frames = self.numFrames = header.numFrames

        # set values
        self.r = r
        self.fps = 20 if os.path.basename(f.path).lower().startswith('mscren') else 15
        self.pixels = bytearray(self.width * self.height)
        self.palette = bytearray(256 * 3)
        self.bytes = bytearray(self.width * self.height * 3)

    def dispose(self) -> None: self.r.close()

    def begin(self, platform: int) -> (bytes, object, list[object]):
        match platform:
            case Platform.Type.OpenGL: format = self.format[1]
            case Platform.Type.Vulken: format = self.format[2]
            case Platform.Type.Unity: format = self.format[3]
            case Platform.Type.Unreal: format = self.format[4]
            case _: raise Exception(f'Unknown {platform}')
        return self.bytes, format, None
    def end(self): pass

    def hasFrames(self) -> bool: return self.numFrames > 0

    def decodeFrame(self) -> bool:
        r = self.r
        frameHeader: X_FrameHeader
        header = r.readS(self.X_ChunkHeader)
        while True:
            nextPosition = r.tell() + (header.size - 6)
            match header.type:
                case self.ChunkType.COLOR_256: self.setPalette(r)
                case self.ChunkType.DELTA_FLC: self.decodeDeltaFLC(r)
                case self.ChunkType.BYTE_RUN: self.decodeByteRun(r)
                case self.ChunkType.FRAME:
                    frameHeader = r.readS(self.X_FrameHeader)
                    self.numFrames -= 1
                    # print(f'Frames Remaining: {self.numFrames}, Chunks: {frameHeader.numChunks}')
                case _:
                    print(f'Unknown Type: {header.type}')
                    r.skip(header.size)
            if header.type != self.ChunkType.FRAME and r.tell() != nextPosition: r.seek(nextPosition)
            header = r.readS(self.X_ChunkHeader)
            if not header.isValid or header.type == self.ChunkType.FRAME: break
        Rasterize.copyPixelsByPalette(self.bytes, 3, self.pixels, self.palette, 3)
        if header.type == self.ChunkType.FRAME: r.skip(-self.X_ChunkHeader.struct[1])
        return header.isValid

    def setPalette(self, r: Reader) -> None:
        palette = self.palette
        numPackets = r.readUInt16()
        if r.readUInt16() == 0: # special case
            data = r.readBytes(256 * 3)
            for i in range(0, len(data), 3):
                palette[i + 0] = ((data[i + 0] << 2) | (data[i + 0] & 3))
                palette[i + 1] = ((data[i + 1] << 2) | (data[i + 1] & 3))
                palette[i + 2] = ((data[i + 2] << 2) | (data[i + 2] & 3))
            return
        r.skip(-2)
        palPos = 0
        while numPackets != 0:
            numPackets -= 1
            palPos += r.readByte() * 3
            change = r.readByte()
            data = r.readBytes(change * 3)
            for i in range(0, len(data), 3):
                palette[palPos + i + 0] = ((data[i + 0] << 2) | (data[i + 0] & 3))
                palette[palPos + i + 1] = ((data[i + 1] << 2) | (data[i + 1] & 3))
                palette[palPos + i + 2] = ((data[i + 2] << 2) | (data[i + 2] & 3))
            palPos += change

    def decodeDeltaFLC(self, r: Reader) -> None:
        linesInChunk = r.readUInt16()
        curLine = 0; numPackets = 0; value = 0
        while linesInChunk > 0:
            linesInChunk -= 1
            # first process all the opcodes.
            opcode: self.OpCode
            while True:
                value = r.readUInt16()
                opcode = self.OpCode((value >> 14) & 3)
                match opcode:
                    case self.OpCode.PACKETCOUNT: numPackets = value
                    case self.OpCode.UNDEFINED: pass
                    case self.OpCode.LASTPIXEL: self.pixels[(curLine * self.width) + (self.width - 1)] = (value & 0xFF)
                    case self.OpCode.LINESKIPCOUNT: curLine += -ctypes.c_short(value).value
                if opcode == self.OpCode.PACKETCOUNT: break

            # now interpret the RLE data
            value = 0
            pixels = self.pixels
            while numPackets > 0:
                numPackets -= 1
                value += r.readByte()
                _ = (curLine * self.width) + value
                count = r.readSByte()
                if count > 0:
                    size = count << 1
                    pixels[_:_+size] = r.readBytes(size)
                    value += size
                elif count < 0:
                    count = -count
                    size = count << 1
                    data = r.readBytes(2)
                    for i in range(_, _+size, 2): pixels[i:i+2] = data
                    value += size
                else: return
            curLine += 1

    def decodeByteRun(self, r: Reader):
        _ = 0; end_ = self.width * self.height
        # pixels = self.pixels
        # while _ < end_:
        #     numChunks = r.readByte()
        #     while numChunks != 0:
        #         numChunks -= 1
        #         count = r.readSByte()
        #         if count > 0: _ += count #Unsafe.InitBlock(ref *ptr, r.ReadByte(), (uint)count); _ += count
        #         else: count = -count; pixels[_:_+count] = r.readBytes(count); _ += count

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        MetaInfo(None, MetaContent(type = 'VideoTexture', name = os.path.basename(file.path), value = self)),
        MetaInfo('Video', items = [
            MetaInfo(f'Width: {self.width}'),
            MetaInfo(f'Height: {self.height}'),
            MetaInfo(f'Frames: {self.frames}')
            ])
        ]

#endregion

#region Binary_Syndicate

# Binary_Syndicate
class Binary_Syndicate(IHaveMetaInfo):
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Ftl(r)

    def __init__(self, r: Reader):
        pass

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        # MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = self.data))
        ]

#endregion
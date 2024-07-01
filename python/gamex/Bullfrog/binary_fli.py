import os
from io import BytesIO
from gamex.filesrc import FileSource
from gamex.pak import PakBinary
from gamex.meta import MetaInfo, MetaContent, IHaveMetaInfo

# typedefs
class Reader: pass
class BinaryPakFile: pass
class PakFile: pass
class MetaManager: pass

# Binary_Fli
class Binary_Fli(IHaveMetaInfo): #IDisposable
    @staticmethod
    def factory(r: Reader, f: FileSource, s: PakFile): return Binary_Fli(r, f)

    #region Headers

    class X_Header:
        MAGIC = 0xaf12
        struct = ('<I4H', 24)
        def __init__(self, tuple):
            self.size, \
            self.type, \
            self.numFrames, \
            self.width, \
            self.height = tuple

    class ChunkType(Enum):
        COLOR_256 = 0x4
        DELTA_FLC = 0x7
        BYTE_RUN = 0xF
        FRAME = 0xF1FA

    class X_ChunkHeader:
        struct = ('<IH', 6)
        def __init__(self, tuple):
            self.size, \
            self.type = tuple
        def isValid(self) -> bool: return self.type == ChunkType.COLOR_256 or self.Type == ChunkType.DELTA_FLC or self.Type == ChunkType.BYTE_RUN

  class OpCode(Enum):
        PACKETCOUNT = 0
        UNDEFINED = 1
        LASTPIXEL = 2
        LINESKIPCOUNT = 3

    #endregion

    def __init__(self, r: Reader, f: FileSource):
        # read header
        header = r.readS(self.X_Header)
        if header.Type != X_Header.MAGIC: raise Exception('BAD MAGIC')
        self.width = header.Width
        self.height = header.Height
        self.numFrames = header.NumFrames

        # set values
        self.r = r
        self.fps = 10 if os.path.basename(f.path) == 'intro' else 15
        self.pixels = bytearray(self.width * self.height)

    def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
        # MetaInfo(None, MetaContent(type = 'Text', name = os.path.basename(file.path), value = self.data))
        ]

import os
from io import BytesIO
from gamex.filesrc import FileSource
from gamex.pak import PakBinaryT
from gamex.compression import decompressBlast
from gamex.util import _throw, _pathExtension
from openstk.poly import unsafe

# typedefs
class Reader: pass
class BinaryPakFile: pass

#region PakBinary_Vpk

# PakBinary_Vpk
class PakBinary_Vpk(PakBinaryT):

    # read
    def read(self, source: BinaryPakFile, r: Reader, tag: object = None) -> None:
        source.files = files = []
        key = source.game.key.encode('ascii'); keyLength = len(key); keyIndex = 0

        # move to fat table
        r.seek(r.readUInt32())
        fatSize = r.readUInt32()
        fatBytes = bytearray(r.readBytes(fatSize)); b = 0

        # read int32
        def readInt32() -> int:
            nonlocal b, keyIndex
            p = b
            fatBytes[p + 0] = fatBytes[p + 0] ^ key[keyIndex]; keyIndex += 1
            if keyIndex >= keyLength: keyIndex = 0
            fatBytes[p + 1] = fatBytes[p + 1] ^ key[keyIndex]; keyIndex += 1
            if keyIndex >= keyLength: keyIndex = 0
            fatBytes[p + 2] = fatBytes[p + 2] ^ key[keyIndex]; keyIndex += 1
            if keyIndex >= keyLength: keyIndex = 0
            fatBytes[p + 3] = fatBytes[p + 3] ^ key[keyIndex]; keyIndex += 1
            if keyIndex >= keyLength: keyIndex = 0
            b += 4
            return int.from_bytes(fatBytes[p:p+4], 'little', signed=True)

        # read string
        def readString() -> str:
            nonlocal b, keyIndex
            p = b
            while True:
                fatBytes[p] = fatBytes[p] ^ key[keyIndex]; keyIndex += 1
                if keyIndex >= keyLength: keyIndex = 0
                if fatBytes[p] == 0: break
                p += 1
            length = p - b
            r = fatBytes[b:p].decode('ascii', 'replace')
            b = p + 1
            return r

        # while there are bytes
        while b < fatSize:
            dirPath = readString().replace('\\', '/')
            numFiles = readInt32()
            for _ in range(numFiles):
                # get file
                file = FileSource(
                    path = dirPath + readString().replace('\\', '/'),
                    offset = readInt32(),
                    compressed = readInt32(),
                    fileSize = readInt32(),
                    packedSize = readInt32()
                    )
                # special case
                if file.path.endswith('.FTL'): file.compressed = 1
                elif file.compressed == 0: file.fileSize = file.packedSize
                # add file
                files.append(file)

    # readData
    def readData(self, source: BinaryPakFile, r: Reader, file: FileSource) -> BytesIO:
        r.seek(file.offset)
        return BytesIO(
            decompressBlast(r, file.packedSize, file.fileSize) if (file.compressed & 1) != 0 else \
            r.readBytes(file.packedSize)
            )

#endregion

#region PakBinary_Wad

# PakBinary_Wad
class PakBinary_Wad(PakBinaryT):

    #region Headers

    W_MAGIC = 0x33444157 #: WAD3

    class W_Header:
        struct = ('<3I', 12)
        def __init__(self, tuple):
            self.magic, \
            self.lumpCount, \
            self.lumpOffset = tuple

    class W_Lump:
        struct = ('<3I2bH16s', 32)
        def __init__(self, tuple):
            self.offset, \
            self.diskSize, \
            self.size, \
            self.type, \
            self.compression, \
            self.padding, \
            self.name = tuple

    class W_LumpInfo:
        struct = ('<3I', 12)
        def __init__(self, tuple):
            self.width, \
            self.height, \
            self.paletteSize = tuple

    #endregion

    # read
    def read(self, source: BinaryPakFile, r: Reader, tag: object = None) -> None:
        source.files = files = []

        # read file
        header = r.readS(self.W_Header)
        if header.magic != self.W_MAGIC: raise Exception('BAD MAGIC')
        r.seek(header.lumpOffset)
        lumps = r.readSArray(self.W_Lump, header.lumpCount)
        for lump in lumps:
            name = unsafe.fixedAString(lump.name, 16)
            path = None
            match lump.type:
                case 0x40: path = f'{name}.tex2'
                case 0x42: path = f'{name}.pic'
                case 0x43: path = f'{name}.tex'
                case 0x46: path = f'{name}.fnt'
                case _: path = f'{name}.{lump.type:x}'
            files.append(FileSource(
                path = path,
                offset = lump.offset,
                compressed = lump.compression,
                fileSize = lump.diskSize,
                packedSize = lump.size,
                ))

    # readData
    def readData(self, source: BinaryPakFile, r: Reader, file: FileSource) -> BytesIO:
        r.seek(file.offset)
        return BytesIO(
            r.readBytes(file.fileSize) if file.compressed == 0 else \
            _throw('NotSupportedException')
            )

#endregion
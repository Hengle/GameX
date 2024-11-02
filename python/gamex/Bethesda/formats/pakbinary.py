import os
from io import BytesIO
from enum import Enum
from gamex.filesrc import FileSource
from gamex.pak import FileOption, PakBinaryT
from gamex.compression import decompressLz4, decompressZlib2
from gamex.Bethesda.formats.records import FormType, Header

# typedefs
class Reader: pass
class BinaryPakFile: pass

#region PakBinary_Ba2

# PakBinary_Ba2
class PakBinary_Ba2(PakBinaryT):

    #region Header : TES5

    # Default header data
    F4_BSAHEADER_FILEID = 0x58445442    # Magic for Fallout 4 BA2, the literal string "BTDX".
    F4_BSAHEADER_VERSION1 = 0x01      # Version number of a Fallout 4 BA2
    F4_BSAHEADER_VERSION2 = 0x02        # Version number of a Starfield BA2

    class F4_HeaderType(Enum):
        GNRL = 0x4c524e47
        DX10 = 0x30315844
        GNMF = 0x464d4e47
        Unknown = 0

    class F4_Header:
        struct = ('<3IQ', 20)
        def __init__(self, tuple):
            self.version, \
            self.type, \
            self.numFiles, \
            self.nameTableOffset = tuple
            self.type = PakBinary_Ba2.F4_HeaderType(self.type)

    class F4_File:
        struct = ('<4IQ3I', 36)
        def __init__(self, tuple):
            self.nameHash, \
            self.ext, \
            self.dirHash, \
            self.flags, \
            self.offset, \
            self.packedSize, \
            self.fileSize, \
            self.align = tuple

    class F4_Texture:
        struct = ('<3I2B3H4B', 24)
        def __init__(self, tuple):
            self.nameHash, \
            self.ext, \
            self.dirHash, \
            self.unk0C, \
            self.numChunks, \
            self.chunkHeaderSize, \
            self.height, \
            self.width, \
            self.numMips, \
            self.format, \
            self.isCubemap, \
            self.tileMode = tuple

    class F4_GNMF:
        struct = ('<3I2BH32sQ4I', 72)
        def __init__(self, tuple):
            self.nameHash, \
            self.ext, \
            self.dirHash, \
            self.unk0C, \
            self.numChunks, \
            self.unk0E, \
            self.header, \
            self.offset, \
            self.packedSize, \
            self.fileSize, \
            self.unk40, \
            self.align = tuple

    class F4_TextureChunk:
        struct = ('<Q2I2HI', 24)
        def __init__(self, tuple):
            self.offset, \
            self.packedSize, \
            self.fileSize, \
            self.startMip, \
            self.endMip, \
            self.align = tuple

    #endregion

    # read
    def read(self, source: BinaryPakFile, r: Reader, tag: object = None) -> None:
        source.magic = magic = r.readUInt32()

        # Fallout 4 - Starfield
        if magic == self.F4_BSAHEADER_FILEID:
            header = r.readS(self.F4_Header)
            if header.version > self.F4_BSAHEADER_VERSION2:
                raise Exception('BAD MAGIC')
            source.version = header.version
            source.files = files = [None] * header.numFiles
            # version2
            # if header.version == self.F4_BSAHEADER_VERSION2: r.skip(8)

            # General BA2 Format
            match header.type:
                # General BA2 Format
                case self.F4_HeaderType.GNRL:
                    headerFiles = r.readTArray(self.F4_File, header.numFiles)
                    for i in range(header.numFiles):
                        headerFile = headerFiles[i]
                        files[i] = FileSource(
                            compressed = 1 if headerFile.packedSize != 0 else 0,
                            packedSize = headerFile.packedSize,
                            fileSize = headerFile.fileSize,
                            offset = headerFile.offset
                            )
                # Texture BA2 Format
                case self.F4_HeaderType.DX10:
                    for i in range(header.numFiles):
                        headerTexture = r.readS(self.F4_Texture)
                        headerTextureChunks = r.readTArray(self.F4_TextureChunk, headerTexture.numChunks)
                        firstChunk = headerTextureChunks[0]
                        files[i] = FileSource(
                            fileInfo = headerTexture,
                            packedSize = firstChunk.packedSize,
                            fileSize = firstChunk.fileSize,
                            offset = firstChunk.offset,
                            tag = headerTextureChunks
                            )
                # GNMF BA2 Format
                case self.F4_HeaderType.GNMF:
                    for i in range(header.numFiles):
                        headerGNMF = r.readS(self.F4_GNMF)
                        headerTextureChunks = r.readTArray(self.F4_TextureChunk, headerGNMF.numChunks)
                        files[i] = FileSource(
                            fileInfo = headerGNMF,
                            packedSize = headerGNMF.packedSize,
                            fileSize = headerGNMF.fileSize,
                            offset = headerGNMF.offset,
                            tag = headerTextureChunks
                            )
                case _: raise Exception(f'Unknown: {header.type}')

            # assign full names to each file
            if header.nameTableOffset > 0:
                r.seek(header.nameTableOffset)
                path = r.readL16Encoding().replace('\\', '/')
                for file in files: file.path = path

    # readData
    def readData(self, source: BinaryPakFile, r: Reader, file: FileSource, option: FileOption = None) -> BytesIO:
        r.seek(file.offset)

        # General BA2 Format
        if file.fileInfo == None:
            return BytesIO(
                decompressZlib2(r, file.packedSize, file.fileSize) if file.compressed != 0 else \
                r.readBytes(file.fileSize)
                )

        # Texture BA2 Format
        elif file.fileInfo is self.F4_Texture:
            pass

        # GNMF BA2 Format
        elif file.fileInfo is self.F4_GNMF:
            pass

        else: raise Exception(f'Unknown fileInfo: {file.fileInfo}')

#endregion

#region PakBinary_Bsa

# PakBinary_Bsa
class PakBinary_Bsa(PakBinaryT):

    #region Header : TES4

    OB_BSAHEADER_FILEID = 0x00415342    # Magic for Oblivion BSA, the literal string "BSA\0".
    OB_BSAHEADER_VERSION = 0x67         # Version number of an Oblivion BSA
    F3_BSAHEADER_VERSION = 0x68         # Version number of a Fallout 3 BSA
    SSE_BSAHEADER_VERSION = 0x69        # Version number of a Skyrim SE BSA

    # Archive flags
    OB_BSAARCHIVE_PATHNAMES = 0x0001    # Whether the BSA has names for paths
    OB_BSAARCHIVE_FILENAMES = 0x0002    # Whether the BSA has names for files
    OB_BSAARCHIVE_COMPRESSFILES = 0x0004 # Whether the files are compressed
    F3_BSAARCHIVE_PREFIXFULLFILENAMES = 0x0100 # Whether the name is prefixed to the data?

    # Bitmasks for the size field in the header
    OB_BSAFILE_SIZEMASK = 0x3fffffff    # Bit mask with OB_HeaderFile:SizeFlags to get the compression status
    OB_BSAFILE_SIZECOMPRESS = 0xC0000000 # Bit mask with OB_HeaderFile:SizeFlags to get the compression status

    class OB_Header:
        struct = ('<8I', 32)
        def __init__(self, tuple):
            self.version, \
            self.folderRecordOffset, \
            self.archiveFlags, \
            self.folderCount, \
            self.fileCount, \
            self.folderNameLength, \
            self.fileNameLength, \
            self.fileFlags = tuple

    class OB_Folder:
        struct = ('<Q2I', 16)
        def __init__(self, tuple):
            self.hash, \
            self.fileCount, \
            self.offset = tuple

    class OB_FolderSSE:
        struct = ('<Q2IQ', 24)
        def __init__(self, tuple):
            self.hash, \
            self.fileCount, \
            self.unk, \
            self.offset = tuple

    class OB_File:
        struct = ('<Q2I', 16)
        def __init__(self, tuple):
            self.hash, \
            self.size, \
            self.offset = tuple

    #endregion

    #region Header : TES3

    MW_BSAHEADER_FILEID = 0x00000100    # Magic for Morrowind BSA

    class MW_Header:
        struct = ('<2I', 8)
        def __init__(self, tuple):
            self.hashOffset, \
            self.fileCount = tuple

    class MW_File:
        struct = ('<2I', 8)
        def __init__(self, tuple):
            self.fileSize, \
            self.fileOffset = tuple
        @property
        def size(self): return self.fileSize & 0x3FFFFFFF if self.fileSize > 0 else 0

    #endregion

    # read
    def read(self, source: BinaryPakFile, r: Reader, tag: object = None) -> None:
        files: list[FileSource]
        magic = source.magic = r.readUInt32()

        # Oblivion - Skyrim
        if magic == self.OB_BSAHEADER_FILEID:
            header = r.readS(self.OB_Header)
            if header.version != self.OB_BSAHEADER_VERSION \
                and header.version != self.F3_BSAHEADER_VERSION \
                and header.version != self.SSE_BSAHEADER_VERSION:
                raise Exception('BAD MAGIC')
            if (header.archiveFlags & self.OB_BSAARCHIVE_PATHNAMES) == 0 \
                or (header.archiveFlags & self.OB_BSAARCHIVE_FILENAMES) == 0:
                raise Exception('HEADER FLAGS')
            source.version = header.version

            # calculate some useful values
            compressedToggle = (header.archiveFlags & self.OB_BSAARCHIVE_COMPRESSFILES) > 0
            if header.version == self.F3_BSAHEADER_VERSION \
                or header.version == self.SSE_BSAHEADER_VERSION:
                source.tag = (header.archiveFlags & self.F3_BSAARCHIVE_PREFIXFULLFILENAMES) > 0

            # read-all folders
            foldersFiles = [x.fileCount for x in r.readSArray(self.OB_FolderSSE, header.folderCount)] if header.version == self.SSE_BSAHEADER_VERSION else \
                [x.fileCount for x in r.readSArray(self.OB_Folder, header.folderCount)]

            # read-all folder files
            fileX = 0
            source.files = files = [None] * header.fileCount
            for i in range(header.folderCount):
                folderName = r.readFAString(r.readByte() - 1).replace('\\', '/')
                r.skip(1)
                headerFiles = r.readSArray(self.OB_File, foldersFiles[i])
                for headerFile in headerFiles:
                    compressed = (headerFile.size & self.OB_BSAFILE_SIZECOMPRESS) != 0
                    packedSize = headerFile.size ^ self.OB_BSAFILE_SIZECOMPRESS if compressed else headerFile.size
                    files[fileX] = FileSource(
                        path = folderName,
                        offset = headerFile.offset,
                        compressed = 1 if compressed ^ compressedToggle else 0,
                        packedSize = packedSize,
                        fileSize = packedSize & self.OB_BSAFILE_SIZEMASK if source.version == self.SSE_BSAHEADER_VERSION else packedSize)
                    fileX += 1

            # read-all names
            for file in files: file.path = f'{file.path}/{r.readCString()}'

        # Morrowind
        elif magic == self.MW_BSAHEADER_FILEID:
            header = r.readS(self.MW_Header)
            dataOffset = 12 + header.hashOffset + (header.fileCount << 3)

            # create filesources
            source.files = files = [None] * header.fileCount
            headerFiles = r.readSArray(self.MW_File, header.fileCount)
            for i in range(header.fileCount):
                headerFile = headerFiles[i]
                size = headerFile.size
                files[i] = FileSource(
                    offset = dataOffset + headerFile.fileOffset,
                    compressed = 0,
                    fileSize = size,
                    packedSize = size)

            # read filename offsets
            filenameOffsets = r.readPArray('I', header.fileCount) # relative offset in filenames section

            # read filenames
            filenamesPosition = r.tell()
            for i in range(header.fileCount):
                r.seek(filenamesPosition + filenameOffsets[i])
                files[i].path = r.readVAString(1000).replace('\\', '/')
        else: raise Exception('BAD MAGIC')
    
    # readData
    def readData(self, source: BinaryPakFile, r: Reader, file: FileSource, option: FileOption = None) -> BytesIO:
        # position
        fileSize = file.fileSize
        r.seek(file.offset)
        if source.tag:
            prefixLength = r.readByte() + 1
            if source.version == self.SSE_BSAHEADER_VERSION: fileSize -= prefixLength
            r.seek(file.offset + prefixLength)

        # not compressed
        if fileSize <= 0 or file.compressed == 0:
            return BytesIO(r.readBytes(fileSize))

        # compressed
        newFileSize = r.readUInt32(); fileSize -= 4
        return BytesIO(
            decompressLz4(r, fileSize, newFileSize) if source.version == self.SSE_BSAHEADER_VERSION else \
            decompressZlib2(r, fileSize, newFileSize))

#endregion

#region PakBinary_Esm

# typedefs
class RecordGroup: pass

# PakBinary_Esm
class PakBinary_Esm(PakBinaryT):
    RecordHeaderSizeInBytes: int = 16
    format: FormType
    groups: dict[FormType, RecordGroup]

    @staticmethod
    def getFormat(game: str) -> FormType:
        match game:
            # tes
            case 'Morrowind': return FormType.TES3
            case 'Oblivion': return FormType.TES4
            case 'Skyrim' | 'SkyrimSE' | 'SkyrimVR': return FormType.TES5
            # fallout
            case 'Fallout3' | 'FalloutNV': return FormType.TES4
            case 'Fallout4' | 'Fallout4VR': return FormType.TES5
            case 'Starfield': return FormType.TES6
            case _: raise Exception(f'Unknown: {game}')

    # read
    def read(self, source: BinaryPakFile, r: Reader, tag: object = None) -> None:
        format = self.getFormat(source.game.id)
        recordLevel = 1
        filePath = source.pakPath
        poolAction = None #(GenericPoolAction<BinaryReader>)source.GetReader().Action; //: Leak
        rootHeader = Header(r, format, None)
        rootRecord = rootHeader.createRecord(rootHeader.position, recordLevel)
        rootRecord.read(r, filePath, format)

        # morrowind hack
        if format == FormType.TES3:
            group = RecordGroup(poolAction, filePath, format, recordLevel)
            group.addHeader(Header(label = 0, dataSize = (r.length - r.tell()), position = r.tell()))
            group.load()
            groups = self.groups = {}
            for k, g in groupby(group.records, lambda x: x.header.type):
                s = RecordGroup(None, filePath, format, recordLevel)
                s.records = list(g)
                s.addHeader(Header(label = x.key), False)
                groups.add(k, s)
            return
        
        # read groups
        groups = self.groups = {}
        endPosition = r.length
        while r.position < endPosition:
            header = Header(r, format, None)
            if header.Type != FormType.GRUP: raise Exception(f'{header.type} not GRUP')
            nextPosition = r.tell() + header.dataSize
            if not (group := groups.get(header.label)): group = RecordGroup(poolAction, filePath, format, recordLevel); groups.add(header.label, group)
            group.addHeader(header)
            r.seek(nextPosition)
    
#endregion
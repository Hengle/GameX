import os
from io import BytesIO
from gamex.filesrc import FileSource
from gamex.pak import PakBinaryT
from gamex.util import _pathExtension
from gamex.Bullfrog.formats.binary import Binary_Fli

# typedefs
class Reader: pass
class BinaryPakFile: pass
class FamilyGame: pass
class IFileSystem: pass
class FileOption: pass

#region PakBinary_Bullfrog

# PakBinary_Bullfrog
class PakBinary_Bullfrog(PakBinaryT):
    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match game.id:
            case _: return (0, None)

    #region Headers

    class V_File:
        struct = ('>QIIIIH', 26)
        def __init__(self, tuple):
            self.offset, \
            self.fileSize, \
            self.packedSize, \
            self.unknown1, \
            self.flags, \
            self.flags2 = tuple

    #endregion

    # read
    def read(self, source: BinaryPakFile, r: Reader, tag: object = None) -> None:
        # must be .index file
        if _pathExtension(source.filePath) != '.index':
            raise Exception('must be a .index file')
        source.files = files = []

        # master.index file
        if source.filePath == 'master.index':
            MAGIC = 0x04534552
            SubMarker = 0x18000000
            EndMarker = 0x01000000
            
            magic = r.readUInt32E()
            if magic != MAGIC:
                raise Exception('BAD MAGIC')
            r.skip(4)
            first = True
            while True:
                pathSize = r.readUInt32()
                if pathSize == SubMarker: first = False; pathSize = r.readUInt32()
                elif pathSize == EndMarker: break
                path = r.readFAString(pathSize).replace('\\', '/')
                packId = 0 if first else r.readUInt16()
                if not path.endswith('.index'): continue
                files.append(FileSource(
                    path = path,
                    pak = self.SubPakFile(self, None, source, source.game, source.fileSystem, path)
                    ))
            return

        # find files
        fileSystem = source.fileSystem
        resourcePath = f'{source.filePath[:-6]}.resources'
        if not fileSystem.fileExists(resourcePath):
            raise Exception('Unable to find resources extension')
        sharedResourcePath = next((x for x in ['shared_2_3.sharedrsc',
            'shared_2_3_4.sharedrsc',
            'shared_1_2_3.sharedrsc',
            'shared_1_2_3_4.sharedrsc'] if fileSystem.fileExists(x)), None)
        source.files = files = []
        r.seek(4)
        mainFileSize = r.readUInt32E()
        r.skip(24)
        numFiles = r.readUInt32E()
        for _ in range(numFiles):
            id = r.readUInt32E()
            tag1 = r.readL32Encoding()
            tag2 = r.readL32Encoding()
            path = (r.readL32Encoding() or '').replace('\\', '/')
            file = r.readS(self.V_File)
            useSharedResources = (file.flags & 0x20) != 0 and file.flags2 == 0x8000
            if useSharedResources and not sharedResourcePath:
                raise Exception('sharedResourcePath not available')
            newPath = sharedResourcePath if useSharedResources else resourcePath
            files.append(FileSource(
                id = id,
                path = path,
                compressed = 1 if file.fileSize != file.packedSize else 0,
                fileSize = file.fileSize,
                packedSize = file.packedSize,
                offset = file.offset,
                tag = (newPath, tag1, tag2)
                ))

    # readData
    def readData(self, source: BinaryPakFile, r: Reader, file: FileSource) -> BytesIO:
        pass

#endregion

#region PakBinary_Populus

# PakBinary_Populus
class PakBinary_Populus(PakBinaryT):
    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match game.id:
            case _: return (0, None)

    #region Headers

    MAGIC_SPR = 0x42465350

    class SPR_Record:
        struct = ('<2HI', 12)
        def __init__(self, tuple):
            self.width, \
            self.height, \
            self.offset = tuple

    class DAT_Sprite:
        struct = ('<2bI', 10)
        def __init__(self, tuple):
            self.width, \
            self.height, \
            self.unknown, \
            self.offset = tuple

    #endregion

    # read
    def read(self, source: BinaryPakFile, r: Reader, tag: object = None) -> None:
        source.files = files = []

    # readData
    def readData(self, source: BinaryPakFile, r: Reader, file: FileSource) -> BytesIO:
        pass

#endregion

#region PakBinary_Syndicate

S_FLIFILES = ['INTRO.DAT', 'MBRIEF.DAT', 'MBRIEOUT.DAT', 'MCONFOUT.DAT', 'MCONFUP.DAT', 'MDEBRIEF.DAT', 'MDEOUT.DAT', 'MENDLOSE.DAT', 'MENDWIN.DAT', 'MGAMEWIN.DAT', 'MLOSA.DAT', 'MLOSAOUT.DAT', 'MLOSEGAM.DAT', 'MMAP.DAT', 'MMAPOUT.DAT', 'MOPTION.DAT', 'MOPTOUT.DAT', 'MRESOUT.DAT', 'MRESRCH.DAT', 'MSCRENUP.DAT', 'MSELECT.DAT', 'MSELOUT.DAT', 'MTITLE.DAT', 'MMULTI.DAT', 'MMULTOUT.DAT']

# PakBinary_Syndicate
class PakBinary_Syndicate(PakBinaryT):
    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match os.path.basename(source.path).upper():
            case x if x in S_FLIFILES: return (0, Binary_Fli.factory)
            ## case 'MCONSCR.DAT': return (0, Binary_Raw.FactoryMethod()),
            ## case 'MLOGOS.DAT': return (0, Binary_Raw.FactoryMethod()),
            ## case 'MMAPBLK.DAT': return (0, Binary_Raw.FactoryMethod()),
            ## case 'MMINLOGO.DAT': return (0, Binary_Raw.FactoryMethod()),
            # case 'HFNT01.DAT': return (0, Binary_Syndicate.Factory_Font),
            case _: return (0, None)

    # read
    def read(self, source: BinaryPakFile, r: Reader, tag: object = None) -> None:
        source.files = files = []

    # readData
    def readData(self, source: BinaryPakFile, r: Reader, file: FileSource) -> BytesIO:
        pass

#endregion
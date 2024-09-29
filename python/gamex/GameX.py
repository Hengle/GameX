import os
from gamex.filesrc import FileSource
from gamex import Family
from gamex.pak import PakFile
from gamex.meta import MetaManager, MetaInfo, MetaContent, IHaveMetaInfo

# typedefs
class Reader: pass
class FileOption: pass
class FamilyGame: pass
class PakState: pass

# UnknownFamily
class UnknownFamily(Family):
    def __init__(self, elem: dict[str, object]):
        super().__init__(elem)

# UnknownPakFile
class UnknownPakFile(PakFile):
    def __init__(self, state: PakState):
        super().__init__(state)
        self.name = 'Unknown'
        self.objectFactoryFunc = self.objectFactoryFactory

    #region Factories

    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match source.path:
            case 'testtri.gfx': return (0, UnknownPakFile.Binary_TestTri.factory)
            case _: return (0, None)

    #endregion

    #region Binary

    class Binary_TestTri(IHaveMetaInfo):
        @staticmethod
        def factory(r: Reader, f: FileSource, s: PakFile): return UnknownPakFile.Binary_TestTri(r)

        def __init__(self, r: Reader):
            pass

        def getInfoNodes(self, resource: MetaManager = None, file: FileSource = None, tag: object = None) -> list[MetaInfo]: return [
            MetaInfo(None, MetaContent(type = 'TestTri', name = os.path.basename(file.path), value = self))
            ]

    #endregion


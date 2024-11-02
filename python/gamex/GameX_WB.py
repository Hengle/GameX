import os
from gamex import FamilyGame, BinaryPakFile
from gamex.GameX import UnknownPakFile
from gamex.util import _pathExtension

# typedefs
class Family: pass
class PakBinary: pass
class IFileSystem: pass
class FileSource: pass
class FileOption: pass

# WbBGame
class WbBGame(FamilyGame):
    def __init__(self, family: Family, id: str, elem: dict[str, object], dgame: FamilyGame):
        super().__init__(family, id, elem, dgame)

# WbBPakFile
class WbBPakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, self.getPakBinary(state.game, _pathExtension(state.path).lower()))

    #region Factories
    @staticmethod
    def getPakBinary(game: FamilyGame, extension: str) -> object:
        pass

    @staticmethod
    def objectFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match _pathExtension(source.path).lower():
            case _: return UnknownPakFile.objectFactory(source, game)

    #endregion
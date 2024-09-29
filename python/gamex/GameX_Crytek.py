import os
from gamex.pak import BinaryPakFile
from .util import _pathExtension

# typedefs
class FamilyGame: pass
class PakBinary: pass
class PakState: pass
class FileSource: pass
class FileOption: pass

# CrytekPakFile
class CrytekPakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, self.getPakBinary(state.game, _pathExtension(state.path).lower()))

    #region Factories
    @staticmethod
    def getPakBinary(game: FamilyGame, extension: str) -> PakBinary:
        pass

    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match _pathExtension(source.path).lower():
            case _: return (0, None)
    #endregion
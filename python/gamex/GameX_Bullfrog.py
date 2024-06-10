import os
from typing import Callable
from gamex import Family, FamilyGame
from gamex.pak import BinaryPakFile
from .util import _pathExtension

# typedefs
class PakBinary: pass
class PakState: pass
class FileSource: pass
class FileOption: pass

# BullfrogGame
class BullfrogGame(FamilyGame):
    def __init__(self, family: Family, id: str, elem: dict[str, object], dgame: FamilyGame):
        super().__init__(family, id, elem, dgame)

# BullfrogPakFile
class BullfrogPakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, self.getPakBinary(state.game, _pathExtension(state.path).lower()))

    #region Factories
    @staticmethod
    def getPakBinary(game: FamilyGame, extension: str) -> PakBinary:
        pass

    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, Callable):
        match _pathExtension(source.path).lower():
            case _: return (0, None)
    #endregion
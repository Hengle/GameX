import os
from typing import Callable
from gamex import Family, FamilyGame
from gamex.pak import BinaryPakFile
from .util import _pathExtension
from .Bullfrog.pakbinary_bullfrog import PakBinary_Bullfrog

# typedefs
class PakBinary: pass
class PakState: pass
class FileSource: pass
class FileOption: pass

# BullfrogGame
class BullfrogGame(FamilyGame):
    def __init__(self, family: Family, id: str, elem: dict[str, object], dgame: FamilyGame):
        super().__init__(family, id, elem, dgame)

    def ensure(self):
        match self.id:
            case 'DK': return self #DK_Database.ensure(self)
            case 'DK2': return self #DK2_Database.ensure(self)
            case 'P2': return self #PK_Database.ensure(self)
            case 'S': return self #S_Database.ensure(self)
            case _: return self

# BullfrogPakFile
class BullfrogPakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, self.getPakBinary(state.game, state.path))
        self.objectFactoryFactoryMethod = self.objectFactoryFactory

    #region Factories
    @staticmethod
    def getPakBinary(game: FamilyGame, filePath: str) -> PakBinary:
        match game.id:
            case _: return PakBinary_Bullfrog()

    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, Callable):
        match game.id:
            case _: return PakBinary_Bullfrog.objectFactoryFactory(source, game)
    #endregion
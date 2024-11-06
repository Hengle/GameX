from __future__ import annotations
import os
from gamex import Family, FamilyGame, BinaryPakFile
from gamex.Bullfrog.formats.pakbinary import PakBinary_Bullfrog, PakBinary_Populus, PakBinary_Syndicate
from gamex.GameX import UnknownPakFile
from gamex.util import _pathExtension

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
        self.objectFactoryFunc = self.objectFactory

    #region Factories
    @staticmethod
    def getPakBinary(game: FamilyGame, filePath: str) -> PakBinary:
        match game.id:
            case x if x == 'DK' or x == 'DK2': return PakBinary_Bullfrog()            # Keeper
            case x if x == 'P' or x == 'P2' or x == 'P3': return PakBinary_Populus()  # Populs
            case x if x == 'S' or x == 'S2': return PakBinary_Syndicate()             # Syndicate
            case x if x == 'MC' or x == 'MC2': return PakBinary_Bullfrog()            # Carpet
            case x if x == 'TP' or x == 'TH': return PakBinary_Bullfrog()             # Theme
            case _: raise Exception(f'Unknown: {game.id}')

    @staticmethod
    def objectFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match game.id:
            case x if x == 'DK' or x == 'DK2': return PakBinary_Bullfrog.objectFactory(source, game)
            case x if x == 'P' or x == 'P2' or x == 'P3': return PakBinary_Populus.objectFactory(source, game)
            case x if x == 'S' or x == 'S2': return PakBinary_Syndicate.objectFactory(source, game)
            case _: raise Exception(f'Unknown: {game.id}')

    #endregion
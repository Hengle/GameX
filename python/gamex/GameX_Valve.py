import os
from gamex.pak import BinaryPakFile
from .Valve.pakbinary import PakBinary_Vpk, PakBinary_Wad
from .util import _pathExtension

# typedefs
class FamilyGame: pass
class PakBinary: pass
class PakState: pass
class FileSource: pass
class FileOption: pass

#region ValvePakFile

# ValvePakFile
class ValvePakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, self.getPakBinary(state.game, _pathExtension(state.path).lower()))
        self.objectFactoryFunc = self.objectFactoryFactory
        # self.pathFinders.add(typeof(object), FindBinary)

    #region Factories
    @staticmethod
    def getPakBinary(game: FamilyGame, extension: str) -> object:
        match game.engine:
            # case 'Unity': return PakBinary_Unity()
            case 'Source': return PakBinary_Vpk()
            case 'HL': return PakBinary_Wad()
            case _: raise Exception(f'Unknown: {game.engine}')

    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match _pathExtension(source.path).lower():
            case _: return (0, None)
    #endregion

#endregion
import os
from gamex.pak import BinaryPakFile
from gamex.Base.formats.pakbinary import PakBinary_Zip
from gamex.Bioware.formats.pakbinary import PakBinary_Aurora, PakBinary_Myp
from gamex.GameX import UnknownPakFile
from gamex.util import _pathExtension

# typedefs
class FamilyGame: pass
class PakBinary: pass
class PakState: pass
class FileSource: pass
class FileOption: pass

# BiowarePakFile
class BiowarePakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, self.getPakBinary(state.game, _pathExtension(state.path).lower()))

    #region Factories
    @staticmethod
    def getPakBinary(game: FamilyGame, extension: str) -> PakBinary:
        if extension == '.zip': return PakBinary_Zip()
        match game.engine:
            case 'Aurora': return PakBinary_Aurora()
            case 'HeroEngine': return PakBinary_Myp()
            case _: raise Exception(f'Unknown: {game.engine}')

    @staticmethod
    def objectFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match _pathExtension(source.path).lower():
            case _: return UnknownPakFile.objectFactory(source, game)

    #endregion

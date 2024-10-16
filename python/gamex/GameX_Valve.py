import os
from gamex.pak import BinaryPakFile
from gamex.Valve.formats.pakbinary import PakBinary_Bsp30, PakBinary_Vpk, PakBinary_Wad
from gamex.Valve.formats.binary import Binary_Wad3, Binary_Src, Binary_Spr
from gamex.GameX import UnknownPakFile
from gamex.util import _pathExtension

# typedefs
class Reader: pass
class FamilyGame: pass
class PakBinary: pass
class PakState: pass
class PakFile: pass
class FileSource: pass
class FileOption: pass

#region ValvePakFile

# ValvePakFile
class ValvePakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, self.getPakBinary(state.game, _pathExtension(state.path).lower()))
        self.objectFactoryFunc = self.objectFactory
        # self.pathFinders.add(typeof(object), FindBinary)

    #region Factories
    @staticmethod
    def getPakBinary(game: FamilyGame, extension: str) -> object:
        if extension == '.bsp': return PakBinary_Bsp30()
        match game.engine:
            # case 'Unity': return PakBinary_Unity()
            case 'GoldSrc': return PakBinary_Wad()
            case 'Source': return PakBinary_Vpk()
            case _: raise Exception(f'Unknown: {game.engine}')

    @staticmethod
    def objectFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match game.engine:
            case 'GoldSrc':
                match _pathExtension(source.path).lower():
                    case x if x == '.pic' or x == '.tex' or x == '.tex2' or x == '.fnt': return (0, Binary_Wad3.factory)
                    case '.bsp': return (0, Binary_Bsp.factory)
                    case '.spr': return (0, Binary_Spr.factory)
                    case _: return UnknownPakFile.objectFactory(source, game)
            case 'Source': return (0, Binary_Src.factory)
            case _: raise Exception(f'Unknown: {game.engine}')

    #endregion

#endregion
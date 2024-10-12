import os
from gamex.pak import BinaryPakFile
from .Base.formats.binary import Binary_Img, Binary_Snd, Binary_Txt
from .Valve.formats.pakbinary import PakBinary_Vpk, PakBinary_Wad
from .Valve.formats.binary import Binary_Wad3, Binary_Src, Binary_Bsp, Binary_Spr
from .util import _pathExtension

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
        self.objectFactoryFunc = self.objectFactoryFactory
        # self.pathFinders.add(typeof(object), FindBinary)

    #region Factories
    @staticmethod
    def getPakBinary(game: FamilyGame, extension: str) -> object:
        match game.engine:
            # case 'Unity': return PakBinary_Unity()
            case 'GoldSrc': return PakBinary_Wad()
            case 'Source': return PakBinary_Vpk()
            case _: raise Exception(f'Unknown: {game.engine}')

    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match game.engine:
            case 'GoldSrc':
                match _pathExtension(source.path).lower():
                    case x if x == '.txt' or x == '.ini' or x == '.asl': return (0, Binary_Txt.factory)
                    case '.wav': return (0, Binary_Snd.factory)
                    case x if x == '.bmp' or x == '.jpg': return (0, Binary_Img.factory)
                    case x if x == '.pic' or x == '.tex' or x == '.tex2' or x == '.fnt': return (0, Binary_Wad3.factory)
                    case '.bsp': return (0, Binary_Bsp.factory)
                    case '.spr': return (0, Binary_Spr.factory)
                    case _: None
            case 'Source': return (0, Binary_Src.factory)
            case _: raise Exception(f'Unknown: {game.engine}')
    #endregion

#endregion
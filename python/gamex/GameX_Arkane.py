import os
from gamex.pak import BinaryPakFile
from .Base.formats.binary import Binary_Dds, Binary_Img, Binary_Snd, Binary_Txt
from .Arkane.formats.danae.binary import Binary_Ftl, Binary_Fts, Binary_Tea
from .Arkane.formats.pakbinary import PakBinary_Danae, PakBinary_Void
from .Valve.formats.pakbinary import PakBinary_Vpk
from .GameX_Valve import ValvePakFile
from .util import _pathExtension

# typedefs
class FamilyGame: pass
class PakBinary: pass
class PakState: pass
class FileSource: pass
class FileOption: pass

#region ArkanePakFile

# ArkanePakFile
class ArkanePakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, self.getPakBinary(state.game, _pathExtension(state.path).lower()))
        match state.game.engine:
            # case 'CryEngine': self.objectFactoryFunc = Crytek.CrytekPakFile.ObjectFactoryFactory
            # case 'Unreal': self.objectFactoryFunc = Epic.EpicPakFile.ObjectFactoryFactory
            case 'Valve': self.objectFactoryFunc = ValvePakFile.ObjectFactoryFactory
            # case 'idTech7': self.objectFactoryFunc = Id.IdPakFile.ObjectFactoryFactory
            case _: self.objectFactoryFunc = self.objectFactoryFactory
        self.useFileId = True

    #region Factories
        
    @staticmethod
    def getPakBinary(game: FamilyGame, extension: str) -> PakBinary:
        match game.engine:
            case 'Danae': return PakBinary_Danae()
            case 'Void': return PakBinary_Void()
            # case 'CryEngine': return PakBinary_Void()
            # case 'Unreal': return PakBinary_Void()
            case 'Valve': return PakBinary_Vpk()
            # case 'idTech7': return PakBinary_Void()
            case _: raise Exception(f'Unknown: {game.engine}')

    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match _pathExtension(source.path).lower():
            case x if x == '.txt' or x == '.ini' or x == '.asl': return (0, Binary_Txt.factory)
            case '.wav': return (0, Binary_Snd.factory)
            case x if x == '.bmp' or x == '.jpg' or x == '.tga': return (0, Binary_Img.factory)
            case '.dds': return (0, Binary_Dds.factory)
            # Danae (AF)
            case '.ftl': return (0, Binary_Ftl.factory)
            case '.fts': return (0, Binary_Fts.factory)
            case '.tea': return (0, Binary_Tea.factory)
            #
            #case ".llf": return (0, Binary_Flt.factory)
            #case ".dlf": return (0, Binary_Flt.factory)
            case _: return (0, None)

    #endregion

#endregion
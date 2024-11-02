import os
from gamex import BinaryPakFile
from gamex.Arkane.formats.danae.binary import Binary_Ftl, Binary_Fts, Binary_Tea
from gamex.Arkane.formats.pakbinary import PakBinary_Danae, PakBinary_Void
from gamex.Valve.formats.pakbinary import PakBinary_Vpk
from gamex.GameX_Valve import ValvePakFile
from gamex.GameX import UnknownPakFile
from gamex.util import _pathExtension

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
            # case 'CryEngine': self.objectFactoryFunc = Crytek.CrytekPakFile.ObjectFactory
            # case 'Unreal': self.objectFactoryFunc = Epic.EpicPakFile.ObjectFactory
            case 'Valve': self.objectFactoryFunc = ValvePakFile.ObjectFactory
            # case 'idTech7': self.objectFactoryFunc = Id.IdPakFile.ObjectFactory
            case _: self.objectFactoryFunc = self.objectFactory
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
    def objectFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match _pathExtension(source.path).lower():
            case x if x == '.asl': return (0, Binary_Txt.factory)
            # Danae (AF)
            case '.ftl': return (0, Binary_Ftl.factory)
            case '.fts': return (0, Binary_Fts.factory)
            case '.tea': return (0, Binary_Tea.factory)
            #
            #case ".llf": return (0, Binary_Flt.factory)
            #case ".dlf": return (0, Binary_Flt.factory)
            case _: return UnknownPakFile.objectFactory(source, game)

    #endregion

#endregion
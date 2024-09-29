import os
from gamex import Family, FamilyGame
from gamex.pak import BinaryPakFile
from .Base.binary import Binary_Dds
from .Bethesda.pakbinary import PakBinary_Ba2, PakBinary_Bsa, PakBinary_Esm
from .util import _pathExtension

# typedefs
class PakBinary: pass
class PakState: pass
class FileSource: pass
class FileOption: pass

#region BethesdaFamily

# BethesdaFamily
class BethesdaFamily(Family):
    def __init__(self, elem: dict[str, object]):
        super().__init__(elem)

#endregion

#region BethesdaGame

# BethesdaGame
class BethesdaGame(FamilyGame):
    def __init__(self, family: Family, id: str, elem: dict[str, object], dgame: FamilyGame):
        super().__init__(family, id, elem, dgame)

#endregion

#region BethesdaPakFile

# BethesdaPakFile
class BethesdaPakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, self.getPakBinary(state.game, _pathExtension(state.path).lower()))
        self.objectFactoryFunc = self.objectFactoryFactory

    #region Factories
    @staticmethod
    def getPakBinary(game: FamilyGame, extension: str) -> PakBinary:
        match extension:
            case '': return PakBinary_Bsa()
            case '.bsa': return PakBinary_Bsa()
            case '.ba2': return PakBinary_Ba2()
            case '.esm': return PakBinary_Esm()
            case _: raise Exception(f'Unknown: {extension}')

    # @staticmethod
    # def NiFactory(r: Reader, f: FileSource, s: PakFile): file = NiFile(Path.GetFileNameWithoutExtension(f.Path)); file.Read(r); return file

    @staticmethod
    def objectFactoryFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match _pathExtension(source.path).lower():
            case '.dds': return (0, Binary_Dds.factory)
            # case '.nif': return (0, NiFactory)
            case _: return (0, None)
    #endregion

#endregion
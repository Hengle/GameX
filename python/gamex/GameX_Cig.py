import os
from gamex import BinaryPakFile
from gamex.Cig.formats.pakbinary import PakBinary_P4k
from gamex.GameX import UnknownPakFile
from gamex.util import _pathExtension

# typedefs
class FamilyGame: pass
class PakBinary: pass
class PakState: pass
class FileSource: pass
class FileOption: pass

# CigPakFile
class CigPakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, PakBinary_P4k())

    #region Factories
    @staticmethod
    def objectFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match _pathExtension(source.path).lower():
            case _: return UnknownPakFile.objectFactory(source, game)

    #endregion

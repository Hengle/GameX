import os
from io import BytesIO
from gamex.pak import PakBinaryT
from gamex.meta import FileSource

# typedefs
class Reader: pass
class BinaryPakFile: pass

# PakBinary_Plist
class PakBinary_Plist(PakBinaryT):

    # read
    def read(self, source: BinaryPakFile, r: Reader, tag: object = None) -> None:
        raise NotImplementedError()

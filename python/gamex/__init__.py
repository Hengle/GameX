from ._config import __title__, __version__, option, familyKeys
from .family import *
from .meta import *
from .pak import *
from .platform import Platform
# from .util import _value

init()
# print(family.families)

unknown = getFamily('Unknown')
unknownPakFile = unknown.openPakFile('game:/#APP', throwOnError = False)

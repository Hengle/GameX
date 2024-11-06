from __future__ import annotations
import os
from gamex import BinaryPakFile
from gamex.Origin.UO.binary import Binary_Animdata, Binary_AsciiFont, Binary_BodyConverter, Binary_BodyTable, Binary_CalibrationInfo, Binary_Gump, Binary_GumpDef, Binary_Hues, Binary_Land, Binary_Light, Binary_MobType, Binary_MultiMap, Binary_MusicDef, Binary_Multi, Binary_RadarColor, Binary_SkillGroups, Binary_Skills, Binary_Sound, Binary_SpeechList, Binary_Static, Binary_StringTable, Binary_TileData, Binary_UnicodeFont, Binary_Verdata
from gamex.Origin.pakbinary_u8 import PakBinary_U8
from gamex.Origin.pakbinary_uo import PakBinary_UO
from gamex.Origin.pakbinary_u9 import PakBinary_U9
from gamex.util import _pathExtension

# OriginPakFile
class OriginPakFile(BinaryPakFile):
    def __init__(self, state: PakState):
        super().__init__(state, self.getPakBinary(state.game))
        self.objectFactoryFunc = self.objectFactory

    #region Factories

    @staticmethod
    def getPakBinary(game: FamilyGame) -> PakBinary:
        match game.id:
            case 'U8': return PakBinary_U8()
            case 'UO': return PakBinary_UO()
            case 'U9': return PakBinary_U9()
            case _: raise Exception(f'Unknown: {game.id}')
        
    @staticmethod
    def objectFactory(source: FileSource, game: FamilyGame) -> (FileOption, callable):
        match game.id:
            case 'U8': return PakBinary_U8.objectFactory(source, game)
            case 'UO': return PakBinary_UO.objectFactory(source, game)
            case 'U9': return PakBinary_U9.objectFactory(source, game)
            case _: raise Exception(f'Unknown: {game.id}')

    #endregion
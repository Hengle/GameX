
from __future__ import annotations
import os
from openstk.sfx.sys import ISystemSfx
from gamex.platform import AudioBuilderBase, AudioManager

# SystemAudioBuilder
class SystemAudioBuilder(AudioBuilderBase):
    def createAudio(self, path: object) -> object: raise NotImplementedError()
    def deleteAudio(self, audio: object) -> None: raise NotImplementedError()

# SystemSfx
class SystemSfx(ISystemSfx):
    source: PakFile
    audioManager: AudioManager

    def __init__(self, source: PakFile):
        self.source = source
        self.audioManager = AudioManager(source, SystemAudioBuilder())

    def createAudio(self, path: object) -> int: return self.audioManager.createAudio(path)[0]

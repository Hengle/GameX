import os
from .platform import AudioBuilderBase

# SystemAudioBuilder
class SystemAudioBuilder(AudioBuilderBase):
    def createAudio(self, path: object) -> object: raise NotImplementedError()
    def deleteAudio(self, audio: object) -> None: raise NotImplementedError()

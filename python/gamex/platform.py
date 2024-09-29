import os
from enum import Enum
from gamex.pak import PakFile
from openstk.gfx.gfx import IObjectManager, IMaterialManager, IShaderManager, ITextureManager, PlatformStats
from openstk.gfx.gfx_render import IMaterial
from openstk.gfx.gfx_texture import ITexture
from openstk.sfx.sfx import IAudioManager

# typedefs
class Audio: pass
class Texture: pass
class Shader: pass
class Material: pass

# AudioBuilderBase
class AudioBuilderBase:
    def createAudio(self, path: object) -> Audio: pass
    def deleteAudio(self, audio: Audio) -> None: pass

# AudioManager
class AudioManager(IAudioManager):
    _pakFile: PakFile
    _builder: AudioBuilderBase
    _cachedAudios: dict[object, (Audio, object)] = {}
    _preloadTasks: dict[object, object] = {}

    def __init__(self, pakFile: PakFile, builder: AudioBuilderBase):
        self._pakFile = pakFile
        self._builder = builder

    def createAudio(self, key: object) -> (Audio, object):
        if path in self._cachedAudios: return self._cachedAudios[path]
        # load & cache the audio.
        tag = self._loadAudio(path)
        audio = self._builder.createAudio(tag) if tag else None
        self._cachedAudios[path] = (audio, tag)
        return (audio, tag)

    def preloadAudio(self, path: object) -> None:
        if path in self._cachedAudios: return
        # start loading the audio file asynchronously if we haven't already started.
        if not path in self._preloadTasks: self._preloadTasks[path] = self._pakFile.loadFileObject(object, path)

    def deleteAudio(self, path: object) -> None:
        if not path in self._cachedAudios: return
        self._builder.deleteAudio(self._cachedAudios[0])
        self._cachedAudios.remove(path)

    async def _loadAudio(self, path: object) -> ITexture:
        assert(not path in self._cachedAudios)
        self.preloadAudio(s)
        source = await self._preloadTasks[path]
        self._preloadTasks.remove(path)
        return source

# TextureBuilderBase
class TextureBuilderBase:
    maxTextureMaxAnisotropy: int = PlatformStats.maxTextureMaxAnisotropy
    defaultTexture: Texture
    def createTexture(self, reuse: Texture, source: ITexture, level: range = None) -> Texture: pass
    def createSolidTexture(self, width: int, height: int, rgba: list[float]) -> Texture: pass
    def createNormalMap(self, texture: Texture, strength: float) -> Texture: pass
    def deleteTexture(self, texture: Texture) -> None: pass

# TextureManager
class TextureManager(ITextureManager):
    _pakFile: PakFile
    _builder: TextureBuilderBase
    _cachedTextures: dict[object, (Texture, object)] = {}
    _preloadTasks: dict[object, ITexture] = {}

    def __init__(self, pakFile: PakFile, builder: TextureBuilderBase):
        self._pakFile = pakFile
        self._builder = builder

    def createSolidTexture(self, width: int, height: int, rgba: list[float] = None) -> Texture: return self._builder.createSolidTexture(width, height, rgba)

    def createNormalMap(self, source: Texture, strength: float) -> Texture: return self._builder.createNormalMap(source, strength)

    @property
    def defaultTexture(self) -> Texture: return self._builder.defaultTexture

    def createTexture(self, path: object, level: range = None) -> (Texture, object):
        if path in self._cachedTextures: return self._cachedTextures[path]
        # load & cache the texture.
        tag = path if isinstance(path, ITexture) else self._loadTexture(path)
        texture = self._builder.createTexture(None, tag, level) if tag else self._builder.defaultTexture
        self._cachedTextures[path] = (texture, tag)
        return (texture, tag)

    def reloadTexture(self, path: object, level: range = None) -> (Texture, object):
        if path not in self._cachedTextures: return (None, None)
        c = self._cachedTextures[path]
        self._builder.createTexture(c[0], c[1], level)
        return c

    def preloadTexture(self, path: object) -> None:
        if path in self._cachedTextures: return
        # start loading the texture file asynchronously if we haven't already started.
        if not path in self._preloadTasks: self._preloadTasks[path] = self._pakFile.loadFileObject(object, path)

    def deleteTexture(self, path: object) -> None:
        if not path in self._cachedTextures: return
        self._builder.deleteTexture(self._cachedTextures[0])
        self._cachedTextures.remove(path)

    async def _loadTexture(self, path: object) -> ITexture:
        assert(not path in self._cachedTextures)
        self.preloadTexture(s)
        source = await self._preloadTasks[path]
        self._preloadTasks.remove(path)
        return source

# ShaderBuilderBase
class ShaderBuilderBase:
    def createShader(self, path: object, args: dict[str, bool]) -> Shader: pass

# ShaderManager
class ShaderManager(IShaderManager):
    emptyArgs: dict[str, bool] = {}
    _pakFile: PakFile
    _builder: ShaderBuilderBase

    def __init__(self, pakFile: PakFile, builder: ShaderBuilderBase):
        self._pakFile = pakFile
        self._builder = builder
    
    def createShader(self, path: object, args: dict[str, bool] = None) -> (Shader, object):
        return (self._builder.createShader(path, args or self.emptyArgs), None)

# ObjectBuilderBase
class ObjectBuilderBase:
    def ensurePrefab(self) -> None: pass
    def createNewObject(self, prefab: object) -> object: pass
    def createObject(self, source: object, materialManager: IMaterialManager) -> object: pass

# ObjectManager
class ObjectManager(IObjectManager):
    _pakFile: PakFile
    _materialManager: IMaterialManager
    _builder: ObjectBuilderBase
    _cachedObjects: dict[str, object] = {}
    _preloadTasks: dict[str, object] = {}

    def __init__(self, pakFile: PakFile, materialManager: IMaterialManager, builder: ObjectBuilderBase):
        self._pakFile = pakFile
        self._materialManager = materialManager
        self._builder = builder

    def createNewObject(self, path: object) -> (object, object):
        tag = None
        self._builder.ensurePrefab()
        # load & cache the prefab.
        if not path in self._cachedObjects: prefab = self._cachedObjects[path] = (self._loadObject(path), tag)
        else: prefab = self._cachedObjects[path]
        # instantiate the prefab.
        return (self._builder.createNewObject(prefab[0]), prefab[1])
 
    def preloadObject(self, path: object) -> None:
        if path in self._cachedPrefabs: return
        # start loading the object asynchronously if we haven't already started.
        if not path in self._preloadTasks: self._preloadTasks[path] = self._pakFile.loadFileObject(object, path)

    async def _loadObject(self, path: object) -> object:
        assert(not path in self._cachedPrefabs)
        self.preloadObject(path)
        source = await self._preloadTasks[path]
        self._preloadTasks.remove(path)
        return self._builder.buildObject(source, self._materialManager)

# MaterialBuilderBase
class MaterialBuilderBase:
    textureManager : ITextureManager
    normalGeneratorIntensity: float = 0.75
    defaultMaterial: Material

    def __init__(self, textureManager: ITextureManager): self.TextureManager = textureManager
    def createMaterial(self, path: object) -> Material: pass

# MaterialManager
class MaterialManager(IMaterialManager):
    _pakFile: PakFile
    _builder: MaterialBuilderBase
    _cachedMaterials: dict[object, (Material, object)] = {}
    _preloadTasks: dict[object, IMaterial] = {}

    textureManager: ITextureManager

    def __init__(self, pakFile: PakFile, textureManager: ITextureManager, builder: MaterialBuilderBase):
        self._pakFile = pakFile
        self._textureManager = textureManager
        self._builder = builder

    def createMaterial(self, path: object) -> (Material, object):
        if path in self._cachedMaterials: return self._cachedMaterials[path]
        # load & cache the material.
        source = path if isinstance(path, IMaterial) else self._loadMaterial(path)
        material = self._builder.createMaterial(source) if source else self._builder.defaultMaterial
        tag = None #source.data if source else None
        self._cachedMaterials[path] = (material, tag)
        return (material, tag)

    def preloadMaterial(self, path: object) -> None:
        if path in self._cachedMaterials: return
        # start loading the material file asynchronously if we haven't already started.
        if not path in self._preloadTasks: self._preloadTasks[path] = self._pakFile.loadFileObject(IMaterial, path)

    async def _loadMaterial(self, path: object) -> IMaterial:
        assert(not path in self._cachedMaterials)
        self.preloadMaterial(path)
        source = await self.preloadTasks[path]
        self.preloadTasks.remove(path)
        return source

# typedefs
# class Type(Enum): pass
# class OS(Enum): pass

# Platform
class Platform:
    class Type(Enum):
        Unknown = 0
        OpenGL = 1
        Unity = 2
        Vulken = 3
        Test = 4
        Other = 5

    class OS(Enum):
        Windows = 1
        OSX = 1
        Linux = 1
        Android = 1

    platformType: Type = None
    platformTag: str = None
    platformOS: OS = OS.Windows
    gfxFactory: callable = None
    sfxFactory: callable = None
    startups: list[object] = []
    inTestHost: bool = False
    logFunc: callable = None

    class Stats:
        maxTextureMaxAnisotropy: int

    # startup
    @staticmethod
    def startup() -> None:
        if Platform.inTestHost and len(Platform.startups) == 0: Platform.startups.append(TestPlatform.startup)
        for startup in Platform.startups:
            if startup(): return
        Platform.platformType = Platform.Type.Unknown
        Platform.gfxFactory = lambda source: None
        Platform.sfxFactory = lambda source: None
        Platform.logFunc = lambda a: print(a)

# TestGfx
class TestGfx:
    def __init__(self, source):
        self._source = source

# TestGfx
class TestSfx:
    def __init__(self, source):
        self._source = source

# TestPlatform
class TestPlatform:
    @staticmethod
    def startup() -> bool:
        Platform.platformType = Platform.Type.Test
        Platform.gfxFactory = lambda source: TestGfx(source)
        Platform.sfxFactory = lambda source: TestSfx(source)
        Platform.logFunc = lambda a: print(a)
        return True

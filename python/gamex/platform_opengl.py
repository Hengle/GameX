import os, io, numpy as np
from OpenGL.GL import *
from OpenGL.GL.EXT import texture_compression_s3tc
from openstk.gfx.gfx import IFixedMaterial, IParamMaterial
from openstk.gfx.gl import IOpenGLGfx, QuadIndexBuffer, GLMeshBufferCache
from openstk.gfx.gl_shader import ShaderDebugLoader
from openstk.gfx.gl_render import GLRenderMaterial
from openstk.gfx.gfx_texture import TextureGLFormat, TextureFlags
from openstk.poly import IDisposable
from gamex.platform_system import SystemSfx
from gamex.platform import ObjectBuilderBase, ObjectManager, MaterialBuilderBase, MaterialManager, ShaderBuilderBase, ShaderManager, TextureManager, TextureBuilderBase, Platform

# typedefs
class PakFile: pass
class Shader: pass
class ShaderLoader: pass
class IMaterialManager: pass
class ITexture: pass

# OpenGLObjectBuilder
class OpenGLObjectBuilder(ObjectBuilderBase):
    def ensurePrefab(self) -> None: pass
    def createNewObject(self, prefab: object) -> object: raise NotImplementedError()
    def createObject(self, path: object, materialManager: IMaterialManager) -> object: raise NotImplementedError()

# OpenGLShaderBuilder
class OpenGLShaderBuilder(ShaderBuilderBase):
    _loader: ShaderLoader = ShaderDebugLoader()
    def createShader(self, path: object, args: dict[str, bool]) -> Shader: return self._loader.createShader(path, args)

# OpenGLTextureBuilder
#  @see https://github.com/jcteng/python-opengl-tutorial/blob/master/utils/textureLoader.py
class OpenGLTextureBuilder(TextureBuilderBase):
    _defaultTexture: int = -1
    @property
    def defaultTexture(self) -> int:
        if self._defaultTexture > -1: return self._defaultTexture
        self._defaultTexture = self._createDefaultTexture()
        return self._defaultTexture

    def release(self) -> None:
        if self._defaultTexture > -1: glDeleteTexture(self._defaultTexture); self._defaultTexture = -1

    def _createDefaultTexture(self) -> int: return self.createSolidTexture(4, 4, np.array([
        0.9, 0.2, 0.8, 1.0,
        0.0, 0.9, 0.0, 1.0,
        0.9, 0.2, 0.8, 1.0,
        0.0, 0.9, 0.0, 1.0,

        0.0, 0.9, 0.0, 1.0,
        0.9, 0.2, 0.8, 1.0,
        0.0, 0.9, 0.0, 1.0,
        0.9, 0.2, 0.8, 1.0,

        0.9, 0.2, 0.8, 1.0,
        0.0, 0.9, 0.0, 1.0,
        0.9, 0.2, 0.8, 1.0,
        0.0, 0.9, 0.0, 1.0,

        0.0, 0.9, 0.0, 1.0,
        0.9, 0.2, 0.8, 1.0,
        0.0, 0.9, 0.0, 1.0,
        0.9, 0.2, 0.8, 1.0
        ], dtype = np.float32))

    def createTexture(self, reuse: int, source: ITexture, level2: range = None) -> int:
        id = reuse if reuse != None else glGenTextures(1)
        numMipMaps = max(1, source.mipMaps)
        level = range(level2.start if level2 else 0, numMipMaps - 1)
        
        # bind
        glBindTexture(GL_TEXTURE_2D, id)
        if level.start > 0: glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_BASE_LEVEL, level.start)
        glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_MAX_LEVEL, level.stop - level.start)
        bytes, fmt, spans = source.begin(Platform.Type.OpenGL)
        pixels = []

        # decode
        def compressedTexImage2D(source: ITexture, level: range, internalFormat: int) -> bool:
            nonlocal pixels
            width = source.width; height = source.height
            for l in level:
                span = spans[l]
                if span and span[0] < 0: return False
                pixels = bytes[span.start:span.stop] if span else bytes
                glCompressedTexImage2D(GL_TEXTURE_2D, l, internalFormat, width >> l, height >> l, 0, pixels)
            return True
        def texImage2D(source: ITexture, level: range, internalFormat: int, format: int, type: int) -> bool:
            nonlocal pixels, spans
            width = source.width; height = source.height
            for l in level:
                span = spans[l]
                if span and span[0] < 0: return False
                pixels = bytes[span.start:span.stop] if span else bytes
                glTexImage2D(GL_TEXTURE_2D, l, internalFormat, width >> l, height >> l, 0, format, type, pixels)
            return True
        match fmt:
            case glFormat if isinstance(fmt, TextureGLFormat):
                internalFormat = glFormat
                if not internalFormat or not compressedTexImage2D(source, level, internalFormat.value): return self.defaultTexture
            case glPixelFormat if isinstance(fmt, tuple):
                internalFormat, format, type = glPixelFormat, glPixelFormat, glPixelFormat
                if not internalFormat or not texImage2D(source, level, internalFormat.value, format.value, type.value): return self.defaultTexture
            case _: raise Exception(f'Unknown format: {fmt}')

        source.end()

        # texture
        if self.maxTextureMaxAnisotropy >= 4:
            glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_MAX_ANISOTROPY_EXT, self.maxTextureMaxAnisotropy)
            glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_LINEAR_MIPMAP_LINEAR)
            glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_LINEAR)
        else:
            glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST)
            glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST)
        glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_CLAMP if (source.flags & TextureFlags.SUGGEST_CLAMPS.value) != 0 else GL_REPEAT)
        glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_CLAMP if (source.flags & TextureFlags.SUGGEST_CLAMPT.value) != 0 else GL_REPEAT)
        glBindTexture(GL_TEXTURE_2D, 0) # unbind texture
        return id

    def createSolidTexture(self, width: int, height: int, pixels: np.array) -> int:
        id = glGenTextures(1)
        glBindTexture(GL_TEXTURE_2D, id)
        glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA32F, width, height, 0, GL_RGBA, GL_FLOAT, pixels)
        glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_MAX_LEVEL, 0)
        glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST)
        glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_MAG_FILTER, GL_NEAREST)
        glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_WRAP_S, GL_REPEAT)
        glTexParameter(GL_TEXTURE_2D, GL_TEXTURE_WRAP_T, GL_REPEAT)
        glBindTexture(GL_TEXTURE_2D, 0) # unbind texture
        return id

    def createNormalMap(self, source: int, strength: float) -> int: raise NotImplementedError()

    def deleteTexture(self, texture: int) -> None: glDeleteTexture(texture)

# OpenGLMaterialBuilder
class OpenGLMaterialBuilder(MaterialBuilderBase):
    _defaultMaterial: GLRenderMaterial
    @property
    def defaultMaterial(self) -> int:
        if self._defaultMaterial: return self._defaultMaterial
        self._defaultMaterial = self._createDefaultMaterial(-1)
        return self._defaultMaterial

    def __init__(self, textureManager: TextureManager):
        super().__init__(textureManager)

    def _createDefaultMaterial(type: int) -> GLRenderMaterial:
        m = GLRenderMaterial(None)
        m.textures['g_tColor'] = self.textureManager.defaultTexture
        m.material.shaderName = 'vrf.error'
        return m

    def createMaterial(self, key: object) -> GLRenderMaterial:
        match key:
            case s if isinstance(key, IMaterial):
                match s:
                    case m if isinstance(key, IFixedMaterial): return m
                    case p if isinstance(key, IMaterial):
                        for tex in p.textureParams: m.textures[tex.key], _ = self.textureManager.createTexture(f'{tex.Value}_c')
                        if 'F_SOLID_COLOR' in p.intParams and p.intParams['F_SOLID_COLOR'] == 1:
                            a = p.vectorParams['g_vColorTint']
                            m.textures['g_tColor'] = self.textureManager.buildSolidTexture(1, 1, a[0], a[1], a[2], a[3])
                        if not 'g_tColor' in m.textures: m.textures['g_tColor'] = self.textureManager.defaultTexture

                        # Since our shaders only use g_tColor, we have to find at least one texture to use here
                        if m.textures['g_tColor'] == self.textureManager.defaultTexture:
                            for name in ['g_tColor2', 'g_tColor1', 'g_tColorA', 'g_tColorB', 'g_tColorC']:
                                if name in m.textures:
                                    m.textures['g_tColor'] = m.textures[name]
                                    break

                        # Set default values for scale and positions
                        if not 'g_vTexCoordScale' in p.vectorParams: p.vectorParams['g_vTexCoordScale'] = np.ones(4)
                        if not 'g_vTexCoordOffset' in p.vectorParams: p.vectorParams['g_vTexCoordOffset'] = np.zeros(4)
                        if not 'g_vColorTint' in p.vectorParams: p.vectorParams['g_vColorTint'] = np.ones(4)
                        return m
                    case _: raise Exception(f'Unknown: {s}')
            case _: raise Exception(f'Unknown: {key}')

# OpenGLGfx
class OpenGLGfx(IOpenGLGfx):
    source: PakFile
    textureManager: TextureManager
    materialManager: MaterialManager
    objectManager: ObjectManager
    shaderManager: ShaderManager

    def __init__(self, source: PakFile):
        self.source = source
        self.textureManager = TextureManager(source, OpenGLTextureBuilder())
        self.materialManager = MaterialManager(source, self.textureManager, OpenGLMaterialBuilder(self.textureManager))
        self.objectManager = ObjectManager(source, self.materialManager, OpenGLObjectBuilder())
        self.shaderManager = ShaderManager(source, OpenGLShaderBuilder())
        self.meshBufferCache = GLMeshBufferCache()

    def createTexture(self, path: object, level: range = None) -> int: return self.textureManager.createTexture(path, level)[0]
    def preloadTexture(self, path: object) -> None: self.textureManager.preloadTexture(path)
    def createObject(self, path: object) -> (object, dict[str, object]): return self.objectManager.createObject(path)[0]
    def preloadObject(self, path: object) -> None: self.objectManager.preloadObject(path)
    def createShader(self, path: object, args: dict[str, bool] = None) -> Shader: return self.shaderManager.createShader(path, args)[0]
    def loadFileObject(self, type: type, path: object) -> object: return self.source.loadFileObject(type, path)

    # cache
    _quadIndices: QuadIndexBuffer
    @property
    def quadIndices(self) -> QuadIndexBuffer: return self._quadIndices if self._quadIndices else (_quadIndices := QuadIndexBuffer(65532))
    meshBufferCache: GLMeshBufferCache

# OpenGLPlatform
class OpenGLPlatform:
    def startup() -> bool:
        Platform.platformType = Platform.Type.OpenGL
        Platform.gfxFactory = lambda source: OpenGLGfx(source)
        Platform.sfxFactory = lambda source: SystemSfx(source)
        Platform.logFunc = lambda a: print(a)
        return True

# OpenGL:startup
Platform.startups.append(OpenGLPlatform.startup)
Platform.startup()
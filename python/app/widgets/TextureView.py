import sys, os
import numpy as np
from OpenGL.GL import *
from PyQt6.QtWidgets import QWidget
from PyQt6.QtCore import Qt
from PyQt6.QtOpenGLWidgets import QOpenGLWidget
from PyQt6.QtOpenGL import QOpenGLBuffer, QOpenGLShader, QOpenGLShaderProgram, QOpenGLTexture
from openstk.gfx_render import RenderPass
from openstk.gfx_texture import ITexture, ITextureSelect
from openstk.gl_view import OpenGLView
from openstk.gl_renders import TextureRenderer
from openstk.gfx_ui import MouseState, KeyboardState

FACTOR: int = 1

# typedefs
class GLCamera: pass
class IOpenGLGraphic: pass

# TextureView
class TextureView(OpenGLView):
    background: bool = False
    level: range = None
    renderers: list[TextureRenderer] = []
    obj: obj = None
    # ui
    id: int = 0

    def __init__(self, parent, tab):
        super().__init__()
        self.parent = parent
        self.graphic: IOpenGLGraphic = parent.graphic
        self.source: ITexture = tab.value
        
    def initializeGL(self):
        super().initializeGL()
        self.onProperty()

    def setViewportSize(self, x: int, y: int, width: int, height: int) -> None:
        if not self.obj: return
        if self.obj.width > 1024 or self.obj.height > 1024 or False: super().setViewportSize(x, y, width, height)
        else: super().setViewportSize(x, y, self.obj.width << FACTOR, self.obj.height << FACTOR)

    def onProperty(self):
        if not self.graphic or not self.source: return
        self.graphicGl = self.graphic
        self.obj = self.source if isinstance(self.source, ITexture) else None
        if not self.obj: return
        if isinstance(self.source, ITextureSelect): self.source.select(self.id)

        # self.camera.setLocation(np.array([200., 200., 200.]))
        # self.camera.lookAt(np.zeros(3))

        self.graphicGl.textureManager.deleteTexture(self.obj)
        texture, _ = self.graphicGl.textureManager.createTexture(self.obj, self.level)
        self.renderers.clear()
        self.renderers.append(TextureRenderer(self.graphicGl, texture, self.background))

    def render(self, camera: GLCamera, frameTime: float):
        for renderer in self.renderers: renderer.render(camera, RenderPass.Both)

    def handleInput(self, mouseState: MouseState, keyboardState: KeyboardState):
        pass
    #     # if key == "r":
    #     #     glColor3f(1.0, 0.0, 0.0)
    #     #     print "Presionaste",key
    #     # elif key == "g":
    #     #     glColor3f(0.0, 1.0, 0.0)
    #     #     print "Presionaste g"
    #     # elif key ==   "b":
    #     #     glColor3f(0.0, 0.0, 1.0)
    #     #     print "Presionaste b"


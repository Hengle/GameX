import sys, os
import numpy as np
from OpenGL.GL import *
from PyQt6.QtWidgets import QWidget
from PyQt6.QtCore import Qt
from PyQt6.QtOpenGLWidgets import QOpenGLWidget
from PyQt6.QtOpenGL import QOpenGLBuffer, QOpenGLShader, QOpenGLShaderProgram, QOpenGLTexture
from openstk.gfx_render import RenderPass
from openstk.gfx_texture import ITexture
from openstk.gl_view import OpenGLView
from openstk.gl_renders import TextureRenderer

FACTOR: int = 1

# typedefs
class IOpenGLGraphic: pass

# TextureView
class TextureView(OpenGLView):
    background: bool = False
    span: range = None
    renderers: list[TextureRenderer] = []
    obj: obj = None

    def __init__(self, parent, tab):
        super().__init__()
        self.parent = parent
        self.graphic: IOpenGLGraphic = parent.graphic
        self.source: ITexture = tab.value
        
    def initializeGL(self):
        super().initializeGL()
        self.onProperty()

    def handleResize(self):
        if not self.obj: return
        if self.obj.width > 1024 or self.obj.height > 1024 or False: super().handleResize(); return
        self.camera.setViewportSize(self.obj.width << FACTOR, self.obj.height << FACTOR)
        self.recalculatePositions()

    def onProperty(self):
        if not self.graphic or not self.source: return
        graphic = self.graphic
        self.obj = self.source if isinstance(self.source, ITexture) else None
        if not self.obj: return

        self.handleResize()
        self.camera.setLocation(np.array([200., 200., 200.]))
        self.camera.lookAt(np.zeros(3))

        graphic.textureManager.deleteTexture(self.obj)
        texture, _ = graphic.textureManager.loadTexture(self.obj, self.span)
        self.renderers.clear()
        self.renderers.append(TextureRenderer(graphic, texture, self.background))

    def paintGL(self):
        super().paintGL()
        # self.handleInput(Keyboard.GetState())
        for renderer in self.renderers: renderer.render(self.camera, RenderPass.Both)
        self.update()

    # def keyPressed(key):
    #     print(key)
    #     # if key == "r":
    #     #     glColor3f(1.0, 0.0, 0.0)
    #     #     print "Presionaste",key
    #     # elif key == "g":
    #     #     glColor3f(0.0, 1.0, 0.0)
    #     #     print "Presionaste g"
    #     # elif key ==   "b":
    #     #     glColor3f(0.0, 0.0, 1.0)
    #     #     print "Presionaste b"


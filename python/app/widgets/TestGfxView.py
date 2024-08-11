import sys, os
import numpy as np
from OpenGL.GL import *
from PyQt6.QtWidgets import QWidget
from PyQt6.QtCore import Qt
from PyQt6.QtOpenGLWidgets import QOpenGLWidget
from PyQt6.QtOpenGL import QOpenGLBuffer, QOpenGLShader, QOpenGLShaderProgram, QOpenGLTexture
from openstk.gfx.gfx_render import RenderPass
from openstk.gfx.gfx_texture import ITexture, ITextureSelect
from openstk.gfx.gl_view import OpenGLView
from openstk.gfx.gl_renders import TextureRenderer
from openstk.gfx.gfx_ui import MouseState, KeyboardState

# from OpenGL.GL import *
from OpenGL.GL.shaders import compileProgram, compileShader

# from PyQt6.QtGui import QSurfaceFormat
# from PyQt6.QtCore import Qt
# from PyQt6.QtOpenGLWidgets import QOpenGLWidget
# from PyQt6.QtOpenGL import QOpenGLVersionProfile


vertex_src = '''
#version 330 core

layout (location=0) in vec3 vertexPos;
layout (location=1) in vec3 vertexColor;

out vec3 fragmentColor;

void main() {
   gl_Position = vec4(vertexPos, 1.0);
   fragmentColor = vertexColor;
}
'''

fragment_src = '''
#version 330 core

in vec3 fragmentColor;

out vec4 color;

void main() {
   color = vec4(fragmentColor, 1.0);
}
'''

FACTOR: int = 1

# typedefs
class GLCamera: pass
class IOpenGLGfx: pass

# TestGfxView
class TestGfxView(OpenGLView):
    background: bool = False
    level: range = None
    renderers: list[TextureRenderer] = []
    obj: obj = None
    # ui
    id: int = 0

    def __init__(self, parent, tab):
        super().__init__()
        self.parent = parent
        self.gfx: IOpenGLGfx = parent.gfx
        self.source: ITexture = tab.value
        
    def initializeGL(self):
        # super().initializeGL()
        # self.onProperty()

        self.shader = compileProgram(compileShader(vertex_src, GL_VERTEX_SHADER), compileShader(fragment_src, GL_FRAGMENT_SHADER))
        glUseProgram(self.shader)

        # x, y, z, r, g, b
        self.vertices = np.array((
                -0.5, -0.5, 0.0, 1.0, 0.0, 0.0,
                 0.5, -0.5, 0.0, 0.0, 1.0, 0.0,
                 0.0,  0.5, 0.0, 0.0, 0.0, 1.0), dtype=np.float32)

        self.vbo = glGenBuffers(1)
        self.vao = glGenVertexArrays(1)
        glBindVertexArray(self.vao)
        glBindBuffer(GL_ARRAY_BUFFER, self.vao)

        glBufferData(GL_ARRAY_BUFFER, self.vertices.nbytes, self.vertices, GL_STATIC_DRAW)

        glEnableVertexAttribArray(0)
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 24, ctypes.c_void_p(0))

        glEnableVertexAttribArray(1)
        glVertexAttribPointer(1, 3, GL_FLOAT, GL_FALSE, 24, ctypes.c_void_p(12))

    def paintGL(self):
        glClear(GL_COLOR_BUFFER_BIT)
        glUseProgram(self.shader)
        glBindVertexArray(self.vao)
        glDrawArrays(GL_TRIANGLES, 0, 6)
        glUseProgram(0)

    # def setViewportSize(self, x: int, y: int, width: int, height: int) -> None:
    #     if not self.obj: return
    #     if self.obj.width > 1024 or self.obj.height > 1024 or False: super().setViewportSize(x, y, width, height)
    #     else: super().setViewportSize(x, y, self.obj.width << FACTOR, self.obj.height << FACTOR)

    def onProperty(self):
        if not self.gfx or not self.source: return
        self.gl = self.gfx
        # self.obj = self.source if isinstance(self.source, ITexture) else None
        # if not self.obj: return
        # if isinstance(self.source, ITextureSelect): self.source.select(self.id)

        # # self.camera.setLocation(np.array([200., 200., 200.]))
        # # self.camera.lookAt(np.zeros(3))

        # self.gl.textureManager.deleteTexture(self.obj)
        # texture, _ = self.gl.textureManager.createTexture(self.obj, self.level)
        # self.renderers.clear()
        # self.renderers.append(TextureRenderer(self.gl, texture, self.background))

    def render(self, camera: GLCamera, frameTime: float):
        pass
    #     # for renderer in self.renderers: renderer.render(camera, RenderPass.Both)

    def handleInput(self, mouseState: MouseState, keyboardState: KeyboardState):
        pass

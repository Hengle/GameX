import sys, os
import numpy as np
from OpenGL.GL import *
from OpenGL.GL.shaders import compileProgram, compileShader
from PyQt6.QtGui import QSurfaceFormat
from PyQt6.QtCore import Qt
from PyQt6.QtOpenGLWidgets import QOpenGLWidget
from PyQt6.QtOpenGL import QOpenGLVersionProfile

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

# ViewTestGfx
class ViewTestGfx(QOpenGLWidget):
    def __init__(self, parent, tab):
        super().__init__()

    def initializeGL(self):
        super().initializeGL()
        # self.fmt = QOpenGLVersionProfile()
        # self.fmt.setVersion(3, 3)
        # self.fmt.setProfile(QSurfaceFormat.OpenGLContextProfile.CoreProfile)
        # print(f'running {glGetString(GL_VERSION)}')

        self.shader = compileProgram(compileShader(vertex_src, GL_VERTEX_SHADER), compileShader(fragment_src, GL_FRAGMENT_SHADER))
        glUseProgram(self.shader)

        # create and bind vao
        vbo = glGenBuffers(1)
        glBindBuffer(GL_ARRAY_BUFFER, vbo)
        vao = glGenVertexArrays(1)
        glBindVertexArray(vao)
        vertices = np.array([
            # x, y, z, r, g, b
           -0.5, -0.5, 0.0, 1.0, 0.0, 0.0,
            0.5, -0.5, 0.0, 0.0, 1.0, 0.0,
            0.0,  0.5, 0.0, 0.0, 0.0, 1.0
            ], dtype = np.float32)
        glBufferData(GL_ARRAY_BUFFER, vertices.nbytes, vertices, GL_STATIC_DRAW)

        # attributes
        glEnableVertexAttribArray(0)
        glVertexAttribPointer(0, 3, GL_FLOAT, GL_FALSE, 24, ctypes.c_void_p(0))
        glEnableVertexAttribArray(1)
        glVertexAttribPointer(1, 3, GL_FLOAT, GL_FALSE, 24, ctypes.c_void_p(12))
        glBindVertexArray(0) # unbind vao
        self.vao = vao

    def paintGL(self):
        glClear(GL_COLOR_BUFFER_BIT)
        glUseProgram(self.shader)
        glBindVertexArray(self.vao)
        glDrawArrays(GL_TRIANGLES, 0, 6)
        glBindVertexArray(0) # unbind vao
        glUseProgram(0) # unbind program


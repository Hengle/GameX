import sys, os
import numpy as np
from typing import Any, TypeVar
from OpenGL.GL import *
from openstk.gfx.gfx_render import IRenderer, RenderPass
from openstk.gfx.gfx_texture import ITextureSelect
from openstk.gfx.gl_view import OpenGLView
from openstk.gfx.gfx_ui import MouseState, KeyboardState

# typedefs
class GLCamera: pass
class IOpenGLGfx: pass

TObj = TypeVar('TObj')

# ViewBase
class ViewBase(OpenGLView):
    FACTOR: int = 0
    toggleValue: bool = False
    gl: IOpenGLGfx = None
    obj: TObj = None
    level: range = None
    renderers: list[IRenderer] = []
    # ui
    id: int = 0

    def __init__(self, parent, tab):
        super().__init__()
        self.parent: Any = parent
        self.gfx: IOpenGLGfx = parent.gfx
        self.source: object = tab.value
        
    def initializeGL(self) -> None:
        super().initializeGL()
        self.onSourceChanged()

    # def setViewport(self, x: int, y: int, width: int, height: int) -> None:
    #     if not self.obj: return
    #     if self.obj.width > 1024 or self.obj.height > 1024 or False: super().setViewport(x, y, width, height)
    #     else: super().setViewport(x, y, self.obj.width << self.FACTOR, self.obj.height << self.FACTOR)

    def getObj(self, source: object) -> (TObj, list[IRenderer]):
        pass

    def onSourceChanged(self) -> None:
        if not self.gfx or not self.source: return
        self.gl: IOpenGLGfx = self.gfx
        (self.obj, self.renderers) = self.getObj(self.source)
        if not self.obj: return
        if isinstance(self.source, ITextureSelect): self.source.select(self.id)

    def render(self, camera: GLCamera, frameTime: float) -> None:
        if not self.renderers: return
        for renderer in self.renderers: renderer.render(camera, RenderPass.Both)

    def handleInput(self, mouseState: MouseState, keyboardState: KeyboardState) -> None:
        pass


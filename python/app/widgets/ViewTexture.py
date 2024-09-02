import sys, os
import numpy as np
from typing import TypeVar
from OpenGL.GL import *
from openstk.gfx.gfx_render import IRenderer
from openstk.gfx.gfx_texture import ITexture
from openstk.gfx.gl_renders import TextureRenderer
from .ViewBase import ViewBase

# TObj = TypeVar('TObj')

# TextureView
class ViewTexture(ViewBase):
    def __init__(self, parent, tab):
        super().__init__(parent, tab)

    def setViewportSize(self, x: int, y: int, width: int, height: int) -> None:
        if not self.obj: return
        if self.obj.width > 1024 or self.obj.height > 1024 or False: super().setViewportSize(x, y, width, height)
        else: super().setViewportSize(x, y, self.obj.width << FACTOR, self.obj.height << FACTOR)

    def getObj(self, source: object) -> (ITexture, list[IRenderer]):
        obj: ITexture = self.obj
        self.gl.textureManager.deleteTexture(obj)
        texture, _ = self.gl.textureManager.createTexture(obj, self.level)
        return (obj, [TextureRenderer(self.gl, texture, self.toggleValue)])

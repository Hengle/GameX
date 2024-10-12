import sys, os
import numpy as np
from OpenGL.GL import *
from openstk.gfx.gfx_render import IRenderer
from openstk.gfx.gfx_texture import ITextureFrames
from openstk.gfx.gl_renderer import TextureRenderer
from .ViewBase import ViewBase

# ViewVideoTexture
class ViewVideoTexture(ViewBase):
    frameDelay: int = 0

    def __init__(self, parent, tab):
        super().__init__(parent, tab, 1.0)

    # def setViewport(self, x: int, y: int, width: int, height: int) -> None:
    #     if not self.obj: return
    #     if self.obj.width > 1024 or self.obj.height > 1024 or False: super().setViewport(x, y, width, height)
    #     else: super().setViewport(x, y, self.obj.width << self.FACTOR, self.obj.height << self.FACTOR)

    def getObj(self, source: object) -> (ITextureFrames, list[IRenderer]):
        obj: ITextureFrames = source
        self.gl.textureManager.deleteTexture(obj)
        texture, _ = self.gl.textureManager.createTexture(obj, self.level)
        return (obj, [TextureRenderer(self.gl, texture, self.toggleValue)])

    def tick(self, **kwargs) -> None:
        super().tick(**kwargs)
        obj = self.obj
        if not self.gl or not obj or not obj.hasFrames(): return
        self.frameDelay += self.deltaTime
        if self.frameDelay <= obj.fps or not obj.decodeFrame(): return
        self.frameDelay = 0 # reset delay between frames
        self.gl.textureManager.reloadTexture(obj)
        self.render(self.camera, 0.0)

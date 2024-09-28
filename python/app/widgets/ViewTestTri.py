import sys, os
import numpy as np
from OpenGL.GL import *
from openstk.gfx.gfx_render import IRenderer
from openstk.gfx.gl_renderer import TestTriRenderer
from .ViewBase import ViewBase

# ViewTestTri
class ViewTestTri(ViewBase):
    def __init__(self, parent, tab):
        super().__init__(parent, tab)

    def getObj(self, source: object) -> (object, list[IRenderer]):
        obj: object = self.obj
        return (obj, [TestTriRenderer(self.gl)])

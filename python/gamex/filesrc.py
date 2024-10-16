import os

# FileSource
class FileSource:
    emptyObjectFactory = lambda a, b, c: None
    def __init__(self, id = None, path = None, offset = None, fileSize = None, packedSize = None, compressed = None, flags = None, hash = None, pak = None, parts = None, data = None, tag = None):
        self.id = id
        self.path = path
        self.offset = offset
        self.fileSize = fileSize
        self.packedSize = packedSize
        self.compressed = compressed
        self.flags = flags
        self.hash = hash
        self.pak = pak
        self.parts = parts
        self.data = data
        self.tag = tag
        # cache
        self.cachedObjectFactory = None
        self.cachedOption = None
    def __repr__(self): return f'{self.path}:{self.fileSize}'

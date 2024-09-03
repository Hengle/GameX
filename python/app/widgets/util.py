import os

def _throw(message: str) -> None:
    raise Exception(message)

def _pathExtension(path: str) -> str:
    return os.path.splitext(path)[1]

def _pathTempFile(ext: str) -> str:
    c = 0
    tmp_file = f'tmp/{c}.{ext}'
    if not os.path.exists('tmp'): os.mkdir('tmp')
    while os.path.exists(tmp_file):
        c += 1
        tmp_file = f'tmp/{c}.{ext}'
    return tmp_file

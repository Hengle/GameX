import sys, os, io
from PyQt6.QtWidgets import QWidget, QTextEdit, QHBoxLayout
from PyQt6.QtGui import QFont
from PyQt6.QtCore import Qt
from .util import _pathExtension, _pathTempFile

# ViewHex
class ViewHex(QWidget):
    def __init__(self, parent, tab):
        super().__init__()
        self.parent = parent
        self.initUI()
        self.rowlen = 0x10
        self.max_hexdump_byte_len = 8192
        # content
        value = tab.value
        if isinstance(value, io.BytesIO): self.viewFile(tab.name, value.read(self.max_hexdump_byte_len), sys.getsizeof(value))
        else: self.viewFile(tab.name, value, len(value))

    def closeEvent(self, e):
        self.text = None
        self.layout = None

    def showMedia(self, external_viewer = False):
        self.tmp_file = _pathTempFile(self.ext)
        with open(self.tmp_file, 'wb+') as f: f.write(self.content)
        webbrowser.open(os.path.join(os.getcwd(), self.tmp_file))
        self.close()

    def showText(self):
        self.text.setText(str(self.content, self.encoding))
        self.text.show()

    def showHexdump(self):
        t = ''
        hexstrlen = self.rowlen * 3
        charstrlen = self.rowlen
        numbytestoprint = min(self.max_hexdump_byte_len + 1, len(self.content))
        for x in range(0, numbytestoprint, self.rowlen):
            section = self.content[x:x+self.rowlen]
            hexstr = ' '.join([f'{y:02x}' for y in section])
            charstr = ''.join([chr(y) if 0x20 <= y <= 0x7E else '.' for y in section])
            t += f'{x:06x} {hexstr.ljust(hexstrlen)} {charstr.ljust(charstrlen)}\n'
        if numbytestoprint < self.file_size:
            t += f'... ({self.file_size - numbytestoprint} bytes truncated) ...'
        self.text.setText(t)
        self.text.show()

    def getFileInfo(self, path, type, content):
        ext = _pathExtension(path)[1:]
        encoding = 'utf-8'
        if type: return (ext, encoding, type)
        import filetype
        g = filetype.guess(content[:4096])
        if g is not None and g.mime.split('/')[0] in ['video', 'audio']: type = g.mime.split('/')[0]; ext = g.extension
        elif (g is None or g.mime.split('/')[0] not in ['application']):
            if content[0:4] == b'\xff\xfe\0\0': encoding = 'utf-32'; type = 'txt'
            elif content[0:2] == b'\xff\xfe': encoding = 'utf-16'; type = 'txt'
            elif all(0x20 <= x <= 0x7E or x in [0xd, 0xa, 0x9] for x in content): encoding = 'utf-8'; type = 'txt'
        return (ext, encoding, type)

    def viewFile(self, filename, content, file_size, file_type = None):
        self.text.setText('Loading your file... Please wait')
        self.content = content.read(self.max_hexdump_byte_len) if isinstance(content, io.BytesIO) else content
        self.file_size = file_size
        self.ext, self.encoding, self.type = self.getFileInfo(filename, file_type, self.content)
        if self.type == 'txt': self.showText() # show strings as normal text files
        elif self.type in ['audio', 'video', 'media']: self.showMedia() # play the audio/video externally
        else: self.showHexdump() # show binary data in hexview
        self.content = None # do not need anymore

    def initUI(self):
        self.text = QTextEdit(self)
        self.text.setLineWrapMode(QTextEdit.LineWrapMode.NoWrap)
        self.text.setText('Close this window if you see this text.')
        self.text.setFont(QFont('Courier New', 10))
        self.text.setReadOnly(True)
        self.text.hide()
        self.tmp_file = None
        self.layout = QHBoxLayout()
        self.layout.addWidget(self.text)
        self.setLayout(self.layout)


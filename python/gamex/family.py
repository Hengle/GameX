from __future__ import annotations
import os, json, glob, re, random
from typing import Any
from urllib.parse import urlparse
from importlib import resources
from openstk.poly import findType
from gamex import familyKeys
from gamex.pak import PakState, ManyPakFile, MultiPakFile
from gamex.file import FileManager, HostFileSystem, StandardFileSystem, VirtualFileSystem
from gamex.platform import Platform
from .util import _throw, _value, _list, _method, _related, _dictTrim

# tag::parseKey[]
# parse key
@staticmethod
def parseKey(value: str) -> object:
    if not value: return None
    elif value.startswith('b64:'): return base64.b64decode(value[4:].encode('ascii')) 
    elif value.startswith('hex:'): return bytes.fromhex(value[4:].replace('/x', ''))
    elif value.startswith('txt:'): return value[4:]
    else: raise Exception(f'Unknown value: {value}')
# end::parseKey[]

# tag::parseEngine[]
# parse engine
@staticmethod
def parseEngine(value: str) -> (str, str):
    if not value: return (None, None)
    p = value.split(':', 2)
    return (p[0], None if len(p) < 2 else p[1])
# end::parseEngine[]

# create Detector
@staticmethod
def createDetector(game: FamilyGame, id: str, elem: dict[str, object]) -> Detector:
    detectorType = _value(elem, 'detectorType')
    return findType(detectorType)(game, id, elem) if detectorType else Detector(game, id, elem)

# create FamilySample
@staticmethod
def createFamilySample(path: str, loader: callable) -> FamilySample:
    elem = loader(path)
    return FamilySample(elem)

# create Family
@staticmethod
def createFamily(any: str, loader: callable = None, loadSamples: bool = False) -> Family:
    elem = loader(any) if loader else any
    familyType = _value(elem, 'familyType')
    family = findType(familyType)(elem) if familyType else \
        Family(elem)
    if family.specSamples and loadSamples:
        for sample in family.specSamples:
            family.mergeSample(createFamilySample(sample, loader))
    if family.specs:
        for spec in family.specs:
            family.merge(createFamily(spec, loader, loadSamples))
    return family

# create FamilyEngine
@staticmethod
def createFamilyEngine(family: Family, id: str, elem: dict[str, object]) -> FamilyEngine:
    engineType = _value(elem, 'engineType')
    return findType(engineType)(family, id, elem) if engineType else FamilyEngine(family, id, elem)

# create FamilyGame
@staticmethod
def createFamilyGame(family: Family, id: str, elem: dict[str, object], dgame: FamilyGame, paths: dict[str, object]) -> FamilyGame:
    gameType = _value(elem, 'gameType', dgame.gameType)
    game = findType(gameType)(family, id, elem, dgame) if gameType else \
        FamilyGame(family, id, elem, dgame)
    game.gameType = gameType
    game.found = id in paths if paths else False
    return game

# create FamilyApp
@staticmethod
def createFamilyApp(family: Family, id: str, elem: dict[str, object]) -> FamilyApp:
    appType = _value(elem, 'appType')
    return findType(appType)(family, id, elem) if appType else FamilyApp(family, id, elem)

# create FileManager
@staticmethod
def createFileManager(elem: dict[str, object]) -> FileManager:
    return FileManager(elem)

# create FileSystem
@staticmethod
def createFileSystem(fileSystemType: str, path: FileManager.PathItem, subPath: str, virtuals: dict[str, object], host: str = None) -> IFileSystem:
    system = HostFileSystem(host) if host else \
        findType(fileSystemType)(path) if fileSystemType else \
        None
    if not system:
        firstPath = next(iter(path.paths), None) if path else None
        root = path.root if not subPath else os.path.join(path.root, subPath)
        match path.type:
            case None: system = StandardFileSystem(root if not firstPath else os.path.join(root, firstPath))
            case 'zip': system = ZipFileSystem(root, firstPath)
            case 'zip:iso': system = ZipIsoFileSystem(root, firstPath)
            case _: raise Exception(f'Unknown {path.type}')
    return system if not virtuals else VirtualFileSystem(system, virtuals)

# tag::Detector[]
class Detector:
    def __init__(self, game: FamilyGame, id: str, elem: dict[str, object]):
        self.cache = {}
        self.id = id
        self.game = game
        self.data = self.parse(game, elem)
    @property
    def name(self): return self.data['name'] or self.id
    def parse(self, game: FamilyGame, elem: dict[str, object]) -> dict[str, object]:
        def switch(k,v):
            match k:
                case 'type': return v
                case 'key': return _method(elem, 'key', parseKey)
                # case 'hashs': Hashs = _related(elem, 'hashs', k => k.GetProperty("hash").GetString(), v => parseHash(game, v)); return v
                case _: return v
        return { k:switch(k,v) for k,v in elem.items() }
    def parseHash(self, game: FamilyGame, elem: dict[str, object]) -> dict[str, object]:
        def switch(k,v):
            match k:
                case 'edition': return v
                case 'locale': return v
                case _: return v
        return { k:switch(k,v) for k,v in elem.items() }
    def __repr__(self): return f'detector#{self.game}'
# end::Detector[]

# tag::Resource[]
class Resource:
    def __init__(self, fileSystem: IFileSystem, game: FamilyGame, edition: Edition, searchPattern: str):
        self.fileSystem = fileSystem
        self.game = game
        self.edition = edition
        self.searchPattern = searchPattern
    def __repr__(self): return f'res:/{self.searchPattern}#{self.game}'
# end::Resource[]

# tag::Family[]
class Family:
    def __init__(self, elem: dict[str, object]):
        self.id = _value(elem, 'id')
        self.name = _value(elem, 'name')
        self.studio = _value(elem, 'studio')
        self.description = _value(elem, 'description')
        self.urls = _list(elem, 'url')
        self.specSamples = _list(elem, 'samples')
        self.specs = _list(elem, 'specs')
        # file manager
        self.fileManager = _method(elem, 'fileManager', createFileManager)
        paths = self.fileManager.paths if self.fileManager else None
        # related
        dgame = FamilyGame(self, None, None, None)
        def gameMethod(k, v):
            nonlocal dgame
            game = createFamilyGame(self, k, v, dgame, paths)
            if k.startswith('*'): dgame = game; return None
            return game
        self.samples = {}
        self.engines = _related(elem, 'engines', lambda k,v: createFamilyEngine(self, k, v))
        self.games = _dictTrim(_related(elem, 'games', gameMethod))
        self.apps = _related(elem, 'apps', lambda k,v: createFamilyApp(self, k, v))
    def __repr__(self): return f'''
{self.id}: {self.name}
engines: {[x for x in self.engines.values()]}
games: {[x for x in self.games.values()]}
fileManager: {self.fileManager if self.fileManager else None}'''

    # merge
    def merge(self, source: Family) -> None:
        if not source: return
        self.engines.update(source.engines)
        self.games.update(source.games)
        self.apps.update(source.apps)
        if self.fileManager: self.fileManager.merge(source.fileManager)
        else: self.fileManager = source.fileManager

    # merge Sample
    def mergeSample(self, source: FamilySample) -> None:
        if not source: return
        self.samples.update(source.samples)

    # get Game
    def getGame(self, id: str, throwOnError: bool = True) -> FamilyGame:
        ids = id.rsplit('.', 1)
        gid = ids[0]; eid = ids[1] if len(ids) > 1 else ''
        game = _value(self.games, gid) or (throwOnError and _throw(f'Unknown game: {id}'))
        edition = _value(game.editions, eid)
        return (game, edition)

    # tag::Family.parseResource[]
    # parse Resource
    def parseResource(self, uri: str, throwOnError: bool = True) -> Resource:
        if uri is None or not (uri := urlparse(uri)).fragment:
            return Resource(Game = FamilyGame(self, None, None, None))
        game, edition = self.getGame(uri.fragment)
        searchPattern = '' if uri.scheme == 'file' else uri.path[1:]
        virtuals = self.fileManager.virtuals[game.id] if game.id in self.fileManager.virtuals else None
        paths = self.fileManager.paths
        subPath = edition.path if edition else None
        fileSystemType = game.fileSystemType
        fileSystem = \
            (createFileSystem(fileSystemType, paths[game.id], subPath, virtuals) if game.id in paths and paths[game.id] else None) if uri.scheme == 'game' else \
            (createFileSystem(fileSystemType, FileManager.PathItem(uri.path, None), subPath, virtuals) if uri.path else None) if uri.scheme == 'file' else \
            (createFileSystem(fileSystemType, None, subPath, virtuals, uri) if uri.netloc else None) if uri.scheme.startswith('http') else None
        if not fileSystem:
            if throwOnError: raise Exception(f'Not located: {game.id}')
            else: return None
        return Resource(
            fileSystem = fileSystem,
            game = game,
            edition = edition,
            searchPattern = searchPattern
            )
    # end::Family.parseResource[]            

    # open PakFile
    def openPakFile(self, res: Resource | str, throwOnError: bool = True) -> PakFile:
        r = None
        match res:
            case s if isinstance(res, Resource): r = s
            case u if isinstance(res, str): r = self.parseResource(u)
            case _: raise Exception(f'Unknown: {res}')
        return (pak := r.game.createPakFile(r.fileSystem, r.edition, r.searchPattern, throwOnError)) and pak.open() if r.game else \
            _throw(f'Undefined Game')
# end::Family[]

# tag::FamilyApp[]
class FamilyApp:
    def __init__(self, family: Family, id: str, elem: dict[str, object]):
        self.family = family
        self.id = self.name = id
        self.data = self.parse(elem)
    def parse(self, elem: dict[str, object]) -> dict[str, object]:
        def switch(k,v):
            match k:
                case 'name': self.name = v; return v
                case 'explorerAppType': self.explorerType = v; return v
                case 'explorer2AppType': self.explorer2Type = v; return v
                case 'key': return _method(elem, 'key', parseKey)
                case _: return v
        return { k:switch(k,v) for k,v in elem.items() }
    def __repr__(self): return f'\n  {self.id}: {self.name}'
# end::FamilyApp[]

# tag::FamilyEngine[]
class FamilyEngine:
    def __init__(self, family: Family, id: str, elem: dict[str, object]):
        self.family = family
        self.id = self.name = id
        self.data = self.parse(elem)
    def parse(self, elem: dict[str, object]) -> dict[str, object]:
        def switch(k,v):
            match k:
                case 'name': self.name = v; return v
                case 'key': return _method(elem, 'key', parseKey)
                case _: return v
        return { k:switch(k,v) for k,v in elem.items() }
    def __repr__(self): return f'\n  {self.id}: {self.name}'
# end::FamilyEngine[]

# tag::FamilySample[]
class FamilySample:
    samples: dict[str, list[object]] = {}
    class File:
        def __init__(self, elem: dict[str, object]):
            self.data = self.parse(elem)
        def parse(self, elem: dict[str, object]) -> dict[str, object]:
            def switch(k,v):
                match k:
                    case 'path': self.path = v; return v
                    case 'size': return v
                    case _: return v
            return { k:switch(k,v) for k,v in elem.items() }
        def __repr__(self): return f'{self.path}'

    def __init__(self, elem: dict[str, object]):
        for k,v in elem.items():
            self.samples[k] = [FamilySample.File(x) for x in v['files']]
# end::FamilySample[]

# tag::FamilyGame[]
class FamilyGame:
    class Edition:
        def __init__(self, id: str, elem: dict[str, object]):
            self.id = self.name = id
            self.path = None
            self.ignores = None
            self.data = self.parse(elem)
        def parse(self, elem: dict[str, object]) -> dict[str, object]:
            def switch(k,v):
                match k:
                    case 'name': self.name = v; return v
                    case 'path': self.path = v; return v
                    case 'ignore': self.ignores = _list(elem, 'ignore'); return v
                    case 'key': return _method(elem, 'key', parseKey)
                    case _: return v
            return { k:switch(k,v) for k,v in elem.items() }
        def __repr__(self): return f'{self.id}: {self.name}'
        
    class DownloadableContent:
        def __init__(self, id: str, elem: dict[str, object]):
            self.id = self.name = id
            self.path = None
            self.data = self.parse(elem)
        def parse(self, elem: dict[str, object]) -> dict[str, object]:
            def switch(k,v):
                match k:
                    case 'name': self.name = v; return v
                    case 'path': self.path = v; return v
                    case 'key': return _method(elem, 'key', parseKey)
                    case _: return v
            return { k:switch(k,v) for k,v in elem.items() }
        def __repr__(self): return f'{self.id}: {self.name}'

    class Locale:
        def __init__(self, id: str, elem: dict[str, object]):
            self.id = self.name = id
            self.data = self.parse(elem)
        def parse(self, elem: dict[str, object]) -> dict[str, object]:
            def switch(k,v):
                match k:
                    case 'name': self.name = v; return v
                    case 'key': return _method(elem, 'key', parseKey)
                    case _: return v
            return { k:switch(k,v) for k,v in elem.items() }
        def __repr__(self): return f'{self.id}: {self.name}'

    def __init__(self, family: Family, id: str, elem: dict[str, object], dgame: FamilyGame):
        self.family = family
        self.id = id
        if not dgame:
            self.ignore = False; self.searchBy = 'Default'; self.paks = ['game:/']
            self.gameType = self.engine = self.resource = \
            self.paths = self.key = self.detector = self.fileSystemType = \
            self.pakFileType = self.pakExts = None
            return
        self.ignore = _value(elem, 'n/a', dgame.ignore)
        self.name = _value(elem, 'name')
        self.engine = _method(elem, 'engine', parseEngine, dgame.engine)
        self.resource = _value(elem, 'resource', dgame.resource)
        self.urls = _list(elem, 'url')
        self.date = _value(elem, 'date')
        #self.option = _list(elem, 'option', dgame.option)
        self.paks = _list(elem, 'pak', dgame.paks)
        self.paths = _list(elem, 'path', dgame.paths)
        self.key = _method(elem, 'key', parseKey, dgame.key)
        # self.status = _value(elem, 'status')
        self.tags = _value(elem, 'tags', '').split(' ')
        # interface
        self.fileSystemType = _value(elem, 'fileSystemType', dgame.fileSystemType)
        self.searchBy = _value(elem, 'searchBy', dgame.searchBy)
        self.pakFileType = _value(elem, 'pakFileType', dgame.pakFileType)
        self.pakExts = _list(elem, 'pakExt', dgame.pakExts) 
        # related
        self.editions = _related(elem, 'editions', lambda k,v: FamilyGame.Edition(k, v))
        self.dlcs = _related(elem, 'dlcs', lambda k,v: FamilyGame.DownloadableContent(k, v))
        self.locales = _related(elem, 'locales', lambda k,v: FamilyGame.Locale(k, v))
        # detector
        self.detector = _related(elem, 'detectors', lambda k,v: createDetector(self, k, v))
    def __repr__(self): return f'''
   {self.id}: {self.name} - {self.found}'''
#     def __repr__(self): return f'''
#   {self.id}: {self.name} - {self.status}
#   - editions: {self.editions if self.editions else None}
#   - dlcs: {self.dlcs if self.dlcs else None}
#   - locales: {self.locales if self.locales else None}'''

    # detect
    def detect(self, id: str, key: str, value: object, func: callable) -> Any:
        return self.detectors[id].get(key, value, func) if id in self.detectors else None

    # converts the Paks to Application Paks
    def toPaks(self, edition: str) -> list[str]:
        return [f'{x}#{self.id}{'.' + edition if edition else ''}' for x in self.paks] # if self.paks else []

    # gets a family sample
    def getSample(self, id: str) -> FamilySample.File:
        if not self.id in self.family.samples or not (samples := self.family.samples[self.id]): return None
        random.seed()
        idx = random.randrange(len(samples)) if id == '*' else int(id)
        return samples[idx] if len(samples) > idx else None

    # with platform
    @staticmethod
    def withPlatform(pakFile: PakFile) -> PakFile:
        if not pakFile: return None
        if Platform.gfxFactory: pakFile.gfx = Platform.gfxFactory(pakFile)
        if Platform.sfxFactory: pakFile.sfx = Platform.sfxFactory(pakFile)
        return pakFile

    # create SearchPatterns
    def createSearchPatterns(self, searchPattern: str) -> str:
        if searchPattern: return searchPattern
        elif self.searchBy == 'Default': return ''
        elif self.searchBy == 'Pak': return '' if not self.pakExts else \
            f'*{self.pakExts[0]}' if len(self.pakExts) == 1 else f'(*{":*".join(self.pakExts)})'
        elif self.searchBy == 'TopDir': return '*'
        elif self.searchBy == 'TwoDir': return '*/*'
        elif self.searchBy == 'DirDown': return '**/*'
        elif self.searchBy == 'AllDir': return '**/*'
        else: raise Exception(f'Unknown searchBy: {self.searchBy}')

    # create PakFile
    def createPakFile(self, fileSystem: IFileSystem, edition: Edition, searchPattern: str, throwOnError: bool) -> PakFile:
        if isinstance(fileSystem, HostFileSystem): raise Exception('HostFileSystem not supported')
        searchPattern = self.createSearchPatterns(searchPattern)
        pakFiles = []
        dlcKeys = [x[0] for x in self.dlcs.items() if x[1].path]
        slash = '\\'
        for key in [None] + dlcKeys:
            for p in self.findPaths(fileSystem, edition, self.dlcs[key] if key else None, searchPattern):
                if self.searchBy == 'Pak':
                    for path in p[1]:
                        if self.isPakFile(path):
                            pakFiles.append(self.createPakFileObj(fileSystem, edition, path))
                else:
                    pakFiles.append(self.createPakFileObj(fileSystem, edition,
                        (p[0], [x for x in p[1] if x.find(slash) >= 0]) if self.searchBy == 'DirDown' else p))
        return FamilyGame.withPlatform(pakFiles[0] if len(pakFiles) == 1 else self.createPakFileObj(fileSystem, edition, pakFiles))

    # create createPakFileObj
    def createPakFileObj(self, fileSystem: IFileSystem, edition: Edition, value: object, tag: object = None) -> PakFile:
        pakState = PakState(fileSystem, self, edition, value if isinstance(value, str) else None, tag)
        match value:
            case s if isinstance(value, str): return self.createPakFileType(pakState) if self.isPakFile(s) else _throw(f'{self.id} missing {s}')
            case p, l if isinstance(value, tuple):
                return self.createPakFileObj(fileSystem, edition, l[0], tag) if len(l) == 1 and self.isPakFile(l[0]) \
                    else ManyPakFile(
                        self.createPakFileType(pakState), pakState,
                        p if len(p) > 0 else 'Many', l,
                        pathSkip = len(p) + 1 if len(p) > 0 else 0)
            case s if isinstance(value, list): return s[0] if len(s) == 1 else MultiPakFile(pakState, 'Multi', s)
            case None: return None
            case _: raise Exception(f'Unknown: {value}')

    # create PakFileType
    def createPakFileType(self, state: PakState) -> PakFile:
        if not self.pakFileType: raise Exception(f'{self.id} missing PakFileType')
        return findType(self.pakFileType)(state)

    # find Paths
    def findPaths(self, fileSystem: IFileSystem, edition: Edition, dlc: DownloadableContent, searchPattern: str):
        ignores = _value(self.family.fileManager.ignores, self.id)
        for path in self.paths or ['']:
            searchPath = os.path.join(path, dlc.path) if dlc and dlc.path else path
            fileSearch = fileSystem.findPaths(searchPath, searchPattern)
            if ignores: fileSearch = [x for x in fileSearch if not os.path.basename(x) in ignores]
            yield (path, list(fileSearch))

    # is a PakFile
    def isPakFile(self, path: str) -> bool:
        return self.pakExts and any([x for x in self.pakExts if path.endswith(x)])
# end::FamilyGame[]

families = {}

@staticmethod
def init(loadSamples: bool = True):
    def commentRemover(text: str) -> str:
        def replacer(match): s = match.group(0); return ' ' if s.startswith('/') else s
        pattern = re.compile(r'//.*?$|/\*.*?\*/|\'(?:\\.|[^\\\'])*\'|"(?:\\.|[^\\"])*"', re.DOTALL | re.MULTILINE)
        return re.sub(pattern, replacer, text)
    def loadJson(path: str) -> dict[str, object]:
        body = resources.files().joinpath('Specs', path).read_text(encoding='utf-8')
        return json.loads(commentRemover(body).encode().decode('utf-8-sig'))
    for path in [f'{x}Family.json' for x in familyKeys]:
        family = createFamily(path, loadJson, loadSamples)
        families[family.id] = family
    return families

@staticmethod
def getFamily(id: str, throwOnError: bool = True) -> Family:
    family = _value(families, id)
    if not family and throwOnError: raise Exception(f'Unknown family: {id}')
    return family

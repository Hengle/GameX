import os
from io import BytesIO
from enum import Enum, IntFlag
from gamex.pak import PakBinaryT
from gamex.meta import FileSource
from gamex.compression import decompressLz4, decompressZlib2

# typedefs
class Reader: pass
class BinaryPakFile: pass
class Header: pass
class Record: pass
class FormId: pass

#region Enums

class FormType(IntFlag):
    APPA = 0x41505041,
    ARMA = 0x414D5241,
    AACT = 0x54434141,
    ASPC = 0x43505341,
    ACTI = 0x49544341,
    ARMO = 0x4F4D5241,
    AMMO = 0x4F4D4D41,
    ASTP = 0x50545341,
    ARTO = 0x4F545241,
    AMDL = 0x4C444D41,
    AECH = 0x48434541,
    ACRE = 0x45524341,
    AORU = 0x55524F41,
    ALCH = 0x48434C41,
    ACHR = 0x52484341,
    ANIO = 0x4F494E41,
    AVIF = 0x46495641,
    ADDN = 0x4E444441,
    BOOK = 0x4B4F4F42,
    BSGN = 0x4E475342,
    BODY = 0x59444F42,
    BNDS = 0x53444E42,
    BPTD = 0x44545042,
    CMPO = 0x4F504D43,
    CLAS = 0x53414C43,
    CSTY = 0x59545343,
    CONT = 0x544E4F43,
    CLMT = 0x544D4C43,
    CELL = 0x4C4C4543,
    CAMS = 0x534D4143,
    CPTH = 0x48545043,
    CREA = 0x41455243,
    CLFM = 0x4D464C43,
    COLL = 0x4C4C4F43,
    CLOT = 0x544F4C43,
    COBJ = 0x4A424F43,
    DMGT = 0x54474D44,
    DOOR = 0x524F4F44,
    DOBJ = 0x4A424F44,
    DFOB = 0x424F4644,
    DUAL = 0x4C415544,
    DLBR = 0x52424C44,
    DEBR = 0x52424544,
    DLVW = 0x57564C44,
    DIAL = 0x4C414944,
    EQUP = 0x50555145,
    EYES = 0x53455945,
    EFSH = 0x48534645,
    ECZN = 0x4E5A4345,
    EXPL = 0x4C505845,
    ENCH = 0x48434E45,
    FACT = 0x54434146,
    FURN = 0x4E525546,
    FLOR = 0x524F4C46,
    FSTP = 0x50545346,
    FSTS = 0x53545346,
    FLST = 0x54534C46,
    GRUP = 0x50555247,
    GMST = 0x54534D47,
    GLOB = 0x424F4C47,
    GRAS = 0x53415247,
    GDRY = 0x59524447,
    HAZD = 0x445A4148,
    HDPT = 0x54504448,
    HAIR = 0x52494148,
    INGR = 0x52474E49,
    IDLM = 0x4D4C4449,
    INFO = 0x4F464E49,
    IDLE = 0x454C4449,
    IPCT = 0x54435049,
    IPDS = 0x53445049,
    IMGS = 0x53474D49,
    IMAD = 0x44414D49,
    INNR = 0x524E4E49,
    KYWD = 0x4457594B,
    KEYM = 0x4D59454B,
    KSSM = 0x4D53534B,
    LIGH = 0x4847494C,
    LCTN = 0x4E54434C,
    LCRT = 0x5452434C,
    LTEX = 0x5845544C,
    LVLN = 0x4E4C564C,
    LVLI = 0x494C564C,
    LAND = 0x444E414C,
    LSCR = 0x5243534C,
    LVSP = 0x5053564C,
    LGTM = 0x4D54474C,
    LVLC = 0x434C564C,
    LEVC = 0x4356454C,
    LOCK = 0x4B434F4C,
    LENS = 0x534E454C,
    LSPR = 0x5250534C,
    LEVI = 0x4956454C,
    LAYR = 0x5259414C,
    MATT = 0x5454414D,
    MSTT = 0x5454534D,
    MGEF = 0x4645474D,
    MICN = 0x4E43494D,
    MISC = 0x4353494D,
    MESG = 0x4753454D,
    MUSC = 0x4353554D,
    MUST = 0x5453554D,
    MOVT = 0x54564F4D,
    MATO = 0x4F54414D,
    MSWP = 0x5057534D,
    NONE = 0x454E4F4E,
    NPC_ = 0x5F43504E,
    NOTE = 0x45544F4E,
    NAVI = 0x4956414E,
    NAVM = 0x4D56414E,
    NOCM = 0x4D434F4E,
    OTFT = 0x5446544F,
    OMOD = 0x444F4D4F,
    OVIS = 0x5349564F,
    PROJ = 0x4A4F5250,
    PMIS = 0x53494D50,
    PARW = 0x57524150,
    PGRE = 0x45524750,
    PBEA = 0x41454250,
    PFLA = 0x414C4650,
    PCON = 0x4E4F4350,
    PBAR = 0x52414250,
    PHZD = 0x445A4850,
    PACK = 0x4B434150,
    PERK = 0x4B524550,
    PKIN = 0x4E494B50,
    PROB = 0x424F5250,
    PGRD = 0x44524750,
    QUST = 0x54535551,
    RFCT = 0x54434652,
    REGN = 0x4E474552,
    RACE = 0x45434152,
    REFR = 0x52464552,
    RGDL = 0x4C444752,
    REVB = 0x42564552,
    ROAD = 0x44414F52,
    REPA = 0x41504552,
    RFGP = 0x50474652,
    RELA = 0x414C4552,
    SPGD = 0x44475053,
    SLGM = 0x4D474C53,
    STAT = 0x54415453,
    SCOL = 0x4C4F4353,
    SOUN = 0x4E554F53,
    SKIL = 0x4C494B53,
    SCPT = 0x54504353,
    SPEL = 0x4C455053,
    SCRL = 0x4C524353,
    SMBN = 0x4E424D53,
    SMQN = 0x4E514D53,
    SMEN = 0x4E454D53,
    SHOU = 0x554F4853,
    SBSP = 0x50534253,
    SNDR = 0x52444E53,
    SNCT = 0x54434E53,
    SOPM = 0x4D504F53,
    SCCO = 0x4F434353,
    SCSN = 0x4E534353,
    STAG = 0x47415453,
    SGST = 0x54534753,
    SSCR = 0x52435353,
    SCEN = 0x4E454353,
    SNDG = 0x47444E53,
    TOFT = 0x54464F54,
    TERM = 0x4D524554,
    TREE = 0x45455254,
    TLOD = 0x444F4C54,
    TES3 = 0x33534554,
    TES4 = 0x34534554,
    TES5 = 0x35534554,
    TES6 = 0x36534554,
    TRNS = 0x534E5254,
    TXST = 0x54535854,
    TACT = 0x54434154,
    VTYP = 0x50595456,
    WRLD = 0x444C5257,
    WEAP = 0x50414557,
    WTHR = 0x52485457,
    WATR = 0x52544157,
    WOOP = 0x504F4F57,
    ZOOM = 0x4D4F4F5A

class FieldType(Enum):
    ANAM = 0x4D414E41,
    AVFX = 0x58465641,
    ASND = 0x444E5341,
    AADT = 0x54444141,
    AODT = 0x54444F41,
    ALDT = 0x54444C41,
    AMBI = 0x49424D41,
    ATXT = 0x54585441,
    ATTR = 0x52545441,
    AIDT = 0x54444941,
    AI_W = 0x575F4941,
    AI_T = 0x545F4941,
    AI_F = 0x465F4941,
    AI_E = 0x455F4941,
    AI_A = 0x415F4941,
    BTXT = 0x54585442,
    BVFX = 0x58465642,
    BSND = 0x444E5342,
    BMDT = 0x54444D42,
    BKDT = 0x54444B42,
    BNAM = 0x4D414E42,
    BYDT = 0x54445942,
    CSTD = 0x44545343,
    CSAD = 0x44415343,
    CNAM = 0x4D414E43,
    CTDA = 0x41445443,
    CTDT = 0x54445443,
    CVFX = 0x58465643,
    CSND = 0x444E5343,
    CNDT = 0x54444E43,
    CLDT = 0x54444C43,
    CNTO = 0x4F544E43,
    DATA = 0x41544144,
    DNAM = 0x4D414E44,
    DESC = 0x43534544,
    DELE = 0x454C4544,
    DODT = 0x54444F44,
    ENDT = 0x54444E45,
    ESCE = 0x45435345,
    ENIT = 0x54494E45,
    EFID = 0x44494645,
    EFIT = 0x54494645,
    EDID = 0x44494445,
    ENAM = 0x4D414E45,
    FADT = 0x54444146,
    FGGS = 0x53474746,
    FGGA = 0x41474746,
    FGTS = 0x53544746,
    FRMR = 0x524D5246,
    FLAG = 0x47414C46,
    FLTV = 0x56544C46,
    FULL = 0x4C4C5546,
    FNAM = 0x4D414E46,
    GNAM = 0x4D414E47,
    HEDR = 0x52444548,
    HSND = 0x444E5348,
    HVFX = 0x58465648,
    HNAM = 0x4D414E48,
    ICON = 0x4E4F4349,
    ICO2 = 0x324F4349,
    INDX = 0x58444E49,
    INTV = 0x56544E49,
    INCC = 0x43434E49,
    INAM = 0x4D414E49,
    ITEX = 0x58455449,
    IRDT = 0x54445249,
    JNAM = 0x4D414E4A,
    KNAM = 0x4D414E4B,
    LVLD = 0x444C564C,
    LVLF = 0x464C564C,
    LVLO = 0x4F4C564C,
    LNAM = 0x4D414E4C,
    LKDT = 0x54444B4C,
    LHDT = 0x5444484C,
    MODL = 0x4C444F4D,
    MODB = 0x42444F4D,
    MODT = 0x54444F4D,
    MNAM = 0x4D414E4D,
    MAST = 0x5453414D,
    MEDT = 0x5444454D,
    MOD2 = 0x32444F4D,
    MO2B = 0x42324F4D,
    MO2T = 0x54324F4D,
    MOD3 = 0x33444F4D,
    MO3B = 0x42334F4D,
    MO3T = 0x54334F4D,
    MOD4 = 0x34444F4D,
    MO4B = 0x42344F4D,
    MO4T = 0x54344F4D,
    MCDT = 0x5444434D,
    NPCS = 0x5343504E,
    NAM1 = 0x314D414E,
    NAME = 0x454D414E,
    NAM2 = 0x324D414E,
    NAM0 = 0x304D414E,
    NAM9 = 0x394D414E,
    NNAM = 0x4D414E4E,
    NAM5 = 0x354D414E,
    NPCO = 0x4F43504E,
    NPDT = 0x5444504E,
    OFST = 0x5453464F,
    ONAM = 0x4D414E4F,
    PGRP = 0x50524750,
    PGRR = 0x52524750,
    PFIG = 0x47494650,
    PFPC = 0x43504650,
    PKDT = 0x54444B50,
    PLDT = 0x54444C50,
    PSDT = 0x54445350,
    PTDT = 0x54445450,
    PBDT = 0x54444250,
    PTEX = 0x58455450,
    PGRC = 0x43524750,
    PGAG = 0x47414750,
    PGRL = 0x4C524750,
    PGRI = 0x49524750,
    PNAM = 0x4D414E50,
    QSDT = 0x54445351,
    QSTA = 0x41545351,
    QSTI = 0x49545351,
    QSTR = 0x52545351,
    QSTN = 0x4E545351,
    QSTF = 0x46545351,
    QNAM = 0x4D414E51,
    RIDT = 0x54444952,
    RNAM = 0x4D414E52,
    RCLR = 0x524C4352,
    RPLI = 0x494C5052,
    RPLD = 0x444C5052,
    RDAT = 0x54414452,
    RDOT = 0x544F4452,
    RDMP = 0x504D4452,
    RDGS = 0x53474452,
    RDMD = 0x444D4452,
    RDSD = 0x44534452,
    RDWT = 0x54574452,
    RADT = 0x54444152,
    RGNN = 0x4E4E4752,
    SCIT = 0x54494353,
    SCRI = 0x49524353,
    SCHR = 0x52484353,
    SCDA = 0x41444353,
    SCTX = 0x58544353,
    SCRO = 0x4F524353,
    SOUL = 0x4C554F53,
    SLCP = 0x50434C53,
    SNAM = 0x4D414E53,
    SPLO = 0x4F4C5053,
    SCHD = 0x44484353,
    SCVR = 0x52564353,
    SCDT = 0x54444353,
    SLSD = 0x44534C53,
    SCRV = 0x56524353,
    SCPT = 0x54504353,
    STRV = 0x56525453,
    SKDT = 0x54444B53,
    SNDX = 0x58444E53,
    SNDD = 0x44444E53,
    SPIT = 0x54495053,
    SPDT = 0x54445053,
    TNAM = 0x4D414E54,
    TPIC = 0x43495054,
    TRDT = 0x54445254,
    TCLT = 0x544C4354,
    TCLF = 0x464C4354,
    TEXT = 0x54584554,
    UNAM = 0x4D414E55,
    VNAM = 0x4D414E56,
    VTXT = 0x54585456,
    VNML = 0x4C4D4E56,
    VHGT = 0x54474856,
    VCLR = 0x524C4356,
    VTEX = 0x58455456,
    WLST = 0x54534C57,
    WNAM = 0x4D414E57,
    WHGT = 0x54474857,
    WPDT = 0x54445057,
    WEAT = 0x54414557,
    XTEL = 0x4C455458,
    XLOC = 0x434F4C58,
    XTRG = 0x47525458,
    XSED = 0x44455358,
    XCHG = 0x47484358,
    XHLT = 0x544C4858,
    XLCM = 0x4D434C58,
    XRTM = 0x4D545258,
    XACT = 0x54434158,
    XCNT = 0x544E4358,
    XMRK = 0x4B524D58,
    XXXX = 0x58585858,
    XOWN = 0x4E574F58,
    XRNK = 0x4B4E5258,
    XGLB = 0x424C4758,
    XESP = 0x50534558,
    XSCL = 0x4C435358,
    XRGD = 0x44475258,
    XPCI = 0x49435058,
    XLOD = 0x444F4C58,
    XMRC = 0x43524D58,
    XHRS = 0x53524858,
    XSOL = 0x4C4F5358,
    XCLC = 0x434C4358,
    XCLL = 0x4C4C4358,
    XCLW = 0x574C4358,
    XCLR = 0x524C4358,
    XCMT = 0x544D4358,
    XCCM = 0x4D434358,
    XCWT = 0x54574358,
    XNAM = 0x4D414E58

#endregion

#region Base

# IHaveMODL
class IHaveMODL:
    # MODL: MODLGroup
    pass

# Header
class Header:
    # HeaderFlags
    class HeaderFlags(IntFlag):
        EsmFile = 0x00000001,               # ESM file. (TES4.HEDR record only.)
        Deleted = 0x00000020,               # Deleted
        R00 = 0x00000040,                   # Constant / (REFR) Hidden From Local Map (Needs Confirmation: Related to shields)
        R01 = 0x00000100,                   # Must Update Anims / (REFR) Inaccessible
        R02 = 0x00000200,                   # (REFR) Hidden from local map / (ACHR) Starts dead / (REFR) MotionBlurCastsShadows
        R03 = 0x00000400,                   # Quest item / Persistent reference / (LSCR) Displays in Main Menu
        InitiallyDisabled = 0x00000800,     # Initially disabled
        Ignored = 0x00001000,               # Ignored
        VisibleWhenDistant = 0x00008000,    # Visible when distant
        R04 = 0x00010000,                   # (ACTI) Random Animation Start
        R05 = 0x00020000,                   # (ACTI) Dangerous / Off limits (Interior cell) Dangerous Can't be set withough Ignore Object Interaction
        Compressed = 0x00040000,            # Data is compressed
        CantWait = 0x00080000,              # Can't wait
        # tes5
        R06 = 0x00100000,                   # (ACTI) Ignore Object Interaction Ignore Object Interaction Sets Dangerous Automatically
        IsMarker = 0x00800000,              # Is Marker
        R07 = 0x02000000,                   # (ACTI) Obstacle / (REFR) No AI Acquire
        NavMesh01 = 0x04000000,             # NavMesh Gen - Filter
        NavMesh02 = 0x08000000,             # NavMesh Gen - Bounding Box
        R08 = 0x10000000,                   # (FURN) Must Exit to Talk / (REFR) Reflected By Auto Water
        R09 = 0x20000000,                   # (FURN/IDLM) Child Can Use / (REFR) Don't Havok Settle
        R10 = 0x40000000,                   # NavMesh Gen - Ground / (REFR) NoRespawn
        R11 = 0x80000000                    # (REFR) MultiBound

    # HeaderGroupType
    class HeaderGroupType(Enum):
        Top = 0,                         # Label: Record type
        WorldChildren = 1,               # Label: Parent (WRLD)
        InteriorCellBlock = 2,           # Label: Block number
        InteriorCellSubBlock = 3,        # Label: Sub-block number
        ExteriorCellBlock = 4,           # Label: Grid Y, X (Note the reverse order)
        ExteriorCellSubBlock = 5,        # Label: Grid Y, X (Note the reverse order)
        CellChildren = 6,                # Label: Parent (CELL)
        TopicChildren = 7,               # Label: Parent (DIAL)
        CellPersistentChilden = 8,       # Label: Parent (CELL)
        CellTemporaryChildren = 9,       # Label: Parent (CELL)
        CellVisibleDistantChildren = 10  # Label: Parent (CELL)

    def __repr__(self) -> str: return f'{self.type}:{self.groupType}'
    parent: Header 
    type: FormType
    dataSize: int 
    flags: HeaderFlags 
    @property
    def compressed(self) -> bool: return (self.flags & HeaderFlags.Compressed) != 0
    formId: int
    position: int
    # group
    label: FormType
    groupType: HeaderGroupType

    def __init__(self): pass
    def __init__(self, r: Reader, format: FormType, parent: Header):
        self.parent = parent
        self.type = FormType(r.readUInt32())
        if type == FormType.GRUP:
            self.dataSize = (r.readUInt32() - (20 if format == FormType.TES4 else 24))
            self.label = FormType(r.readUInt32())
            self.groupType = HeaderGroupType(r.readInt32())
            r.readUInt32() # stamp | stamp + uknown
            if format != FormType.TES4: r.ReadUInt32() # version + uknown
            self.position = r.Tell()
            return
        self.dataSize = r.readUInt32()
        if format == FormType.TES3: r.readUInt32() # Unknown
        self.flags = Header.HeaderFlags(r.readUInt32())
        if format == FormType.TES3: self.position = r.tell(); return
        # tes4
        self.formId = r.ReadUInt32()
        r.readUInt32()
        if format == FormType.TES4: self.position = r.tell(); return
        # tes5
        r.readUInt32()
        self.position = r.tell()

    createMap: dict[FormType, (callable, callable)] = {
        FormType.TES3: (lambda: TES3Record(), lambda x: True),
        FormType.TES4: (lambda: TES4Record(), lambda x: True),
        # 0                                 
        FormType.LTEX: (lambda: LTEXRecord(), lambda x: x > 0),
        FormType.STAT: (lambda: STATRecord(), lambda x: x > 0),
        FormType.CELL: (lambda: CELLRecord(), lambda x: x > 0),
        FormType.LAND: (lambda: LANDRecord(), lambda x: x > 0),
        # 1                                 
        FormType.DOOR: (lambda: DOORRecord(), lambda x: x > 1),
        FormType.MISC: (lambda: MISCRecord(), lambda x: x > 1),
        FormType.WEAP: (lambda: WEAPRecord(), lambda x: x > 1),
        FormType.CONT: (lambda: CONTRecord(), lambda x: x > 1),
        FormType.LIGH: (lambda: LIGHRecord(), lambda x: x > 1),
        FormType.ARMO: (lambda: ARMORecord(), lambda x: x > 1),
        FormType.CLOT: (lambda: CLOTRecord(), lambda x: x > 1),
        FormType.REPA: (lambda: REPARecord(), lambda x: x > 1),
        FormType.ACTI: (lambda: ACTIRecord(), lambda x: x > 1),
        FormType.APPA: (lambda: APPARecord(), lambda x: x > 1),
        FormType.LOCK: (lambda: LOCKRecord(), lambda x: x > 1),
        FormType.PROB: (lambda: PROBRecord(), lambda x: x > 1),
        FormType.INGR: (lambda: INGRRecord(), lambda x: x > 1),
        FormType.BOOK: (lambda: BOOKRecord(), lambda x: x > 1),
        FormType.ALCH: (lambda: ALCHRecord(), lambda x: x > 1),
        FormType.CREA: (lambda: CREARecord(), lambda x: x > 1 and True),
        FormType.NPC_: (lambda: NPC_Record(), lambda x: x > 1 and True),
        # 2                                 
        FormType.GMST: (lambda: GMSTRecord(), lambda x: x > 2),
        FormType.GLOB: (lambda: GLOBRecord(), lambda x: x > 2),
        FormType.SOUN: (lambda: SOUNRecord(), lambda x: x > 2),
        FormType.REGN: (lambda: REGNRecord(), lambda x: x > 2),
        # 3                                 
        FormType.CLAS: (lambda: CLASRecord(), lambda x: x > 3),
        FormType.SPEL: (lambda: SPELRecord(), lambda x: x > 3),
        FormType.BODY: (lambda: BODYRecord(), lambda x: x > 3),
        FormType.PGRD: (lambda: PGRDRecord(), lambda x: x > 3),
        FormType.INFO: (lambda: INFORecord(), lambda x: x > 3),
        FormType.DIAL: (lambda: DIALRecord(), lambda x: x > 3),
        FormType.SNDG: (lambda: SNDGRecord(), lambda x: x > 3),
        FormType.ENCH: (lambda: ENCHRecord(), lambda x: x > 3),
        FormType.SCPT: (lambda: SCPTRecord(), lambda x: x > 3),
        FormType.SKIL: (lambda: SKILRecord(), lambda x: x > 3),
        FormType.RACE: (lambda: RACERecord(), lambda x: x > 3),
        FormType.MGEF: (lambda: MGEFRecord(), lambda x: x > 3),
        FormType.LEVI: (lambda: LEVIRecord(), lambda x: x > 3),
        FormType.LEVC: (lambda: LEVCRecord(), lambda x: x > 3),
        FormType.BSGN: (lambda: BSGNRecord(), lambda x: x > 3),
        FormType.FACT: (lambda: FACTRecord(), lambda x: x > 3),
        FormType.SSCR: (lambda: SSCRRecord(), lambda x: x > 3),
        # 4 - Oblivion                      
        FormType.WRLD: (lambda: WRLDRecord(), lambda x: x > 0),
        FormType.ACRE: (lambda: ACRERecord(), lambda x: x > 1),
        FormType.ACHR: (lambda: ACHRRecord(), lambda x: x > 1),
        FormType.REFR: (lambda: REFRRecord(), lambda x: x > 1),
        #                                   
        FormType.AMMO: (lambda: AMMORecord(), lambda x: x > 4),
        FormType.ANIO: (lambda: ANIORecord(), lambda x: x > 4),
        FormType.CLMT: (lambda: CLMTRecord(), lambda x: x > 4),
        FormType.CSTY: (lambda: CSTYRecord(), lambda x: x > 4),
        FormType.EFSH: (lambda: EFSHRecord(), lambda x: x > 4),
        FormType.EYES: (lambda: EYESRecord(), lambda x: x > 4),
        FormType.FLOR: (lambda: FLORRecord(), lambda x: x > 4),
        FormType.FURN: (lambda: FURNRecord(), lambda x: x > 4),
        FormType.GRAS: (lambda: GRASRecord(), lambda x: x > 4),
        FormType.HAIR: (lambda: HAIRRecord(), lambda x: x > 4),
        FormType.IDLE: (lambda: IDLERecord(), lambda x: x > 4),
        FormType.KEYM: (lambda: KEYMRecord(), lambda x: x > 4),
        FormType.LSCR: (lambda: LSCRRecord(), lambda x: x > 4),
        FormType.LVLC: (lambda: LVLCRecord(), lambda x: x > 4),
        FormType.LVLI: (lambda: LVLIRecord(), lambda x: x > 4),
        FormType.LVSP: (lambda: LVSPRecord(), lambda x: x > 4),
        FormType.PACK: (lambda: PACKRecord(), lambda x: x > 4),
        FormType.QUST: (lambda: QUSTRecord(), lambda x: x > 4),
        FormType.ROAD: (lambda: ROADRecord(), lambda x: x > 4),
        FormType.SBSP: (lambda: SBSPRecord(), lambda x: x > 4),
        FormType.SGST: (lambda: SGSTRecord(), lambda x: x > 4),
        FormType.SLGM: (lambda: SLGMRecord(), lambda x: x > 4),
        FormType.TREE: (lambda: TREERecord(), lambda x: x > 4),
        FormType.WATR: (lambda: WATRRecord(), lambda x: x > 4),
        FormType.WTHR: (lambda: WTHRRecord(), lambda x: x > 4),
        # 5 - Skyrim                        
        FormType.AACT: (lambda: AACTRecord(), lambda x: x > 5),
        FormType.ADDN: (lambda: ADDNRecord(), lambda x: x > 5),
        FormType.ARMA: (lambda: ARMARecord(), lambda x: x > 5),
        FormType.ARTO: (lambda: ARTORecord(), lambda x: x > 5),
        FormType.ASPC: (lambda: ASPCRecord(), lambda x: x > 5),
        FormType.ASTP: (lambda: ASTPRecord(), lambda x: x > 5),
        FormType.AVIF: (lambda: AVIFRecord(), lambda x: x > 5),
        FormType.DLBR: (lambda: DLBRRecord(), lambda x: x > 5),
        FormType.DLVW: (lambda: DLVWRecord(), lambda x: x > 5),
        FormType.SNDR: (lambda: SNDRRecord(), lambda x: x > 5)
    }

    def createRecord(self, position: int, recordLevel: int) -> Record:
        if not (recordType := self.createMap.get(self.type)): print(f'Unsupported ESM record type: {self.type}'); return None
        if not recordType[1](recordLevel): return None
        record = recordType[0]()
        record.header = self
        return record

# FieldHeader
class FieldHeader:
    def __repr__(self) -> str: return f'{self.type}'
    def __init__(self, r: Reader, format: FormType):
        self.type: FieldType = FieldType(r.readUInt32())
        self.dataSize: int = (r.readUInt32() if format == FormType.TES3 else r.readUInt16())

#endregion

#region Base : Standard Fields

class ColorRef3:
    def __repr__(self) -> str: return f'{self.red}:{self.green}:{self.blue}'
    struct = ('<3c', 3)
    def __init__(self, tuple): self.red, self.green, self.blue = tuple
class ColorRef4:
    def __repr__(self) -> str: return f'{self.red}:{self.green}:{self.blue}'
    struct = ('<4c', 4)
    def __init__(self, tuple): self.red, self.green, self.blue, self.null = tuple
    # def asColor32(self) -> GXColor32: return GXColor32(self.red, self.green, self.blue, 255)
class STRVField:
    def __repr__(self) -> str: return f'{self.value}'
    value: str
class FILEField:
    def __repr__(self) -> str: return f'{self.value}'
    value: str
class DATVField:
    def __repr__(self) -> str: return f'DATV'
    b: bool; i: int; f: float; s: str
class FLTVField: 
    def __repr__(self) -> str: return f'{self.value}'
    struct = ('<f', 4)
    def __init__(self, tuple): self.value = tuple
class BYTEField: 
    def __repr__(self) -> str: return f'{self.value}'
    struct = ('<c', 1)
    def __init__(self, tuple): self.value = tuple
class IN16Field: 
    def __repr__(self) -> str: return f'{self.value}'
    struct = ('<h', 2)
    def __init__(self, tuple): self.value = tuple
class UI16Field: 
    def __repr__(self) -> str: return f'{self.value}'
    struct = ('<H', 2)
    def __init__(self, tuple): self.value = tuple
class IN32Field: 
    def __repr__(self) -> str: return f'{self.value}'
    struct = ('<i', 4)
    def __init__(self, tuple): self.value = tuple
class UI32Field: 
    def __repr__(self) -> str: return f'{self.value}'
    struct = ('<I', 4)
    def __init__(self, tuple): self.value = tuple
class INTVField:
    def __repr__(self) -> str: return f'{self.value}'
    struct = ('<q', 8)
    def __init__(self, tuple): self.value = tuple
    def asUI16Field(self) -> UI16Field: return UI16Field(self.value)
class CREFField:
    def __repr__(self) -> str: return f'{self.color}'
    struct = ('<4c', 4)
    def __init__(self, tuple): self.color = ColorRef4(tuple)
class CNTOField:
    def __repr__(self) -> str: return f'{self.item}'
    itemCount: int # Number of the item
    item: FormId   # The ID of the item
    def __init__(self, r: Reader, dataSize: int, format: FormType):
        if format == FormType.TES3: self.itemCount = r.readUInt32(); self.item = FormId(r.readZString(32)); return
        self.item = FormId(r.readUInt32()); self.itemCount = r.readUInt32()
class BYTVField:
    def __repr__(self) -> str: return f'BYTS'
    value: bytes
class UNKNField: 
    def __repr__(self) -> str: return f'UNKN'
    value: bytes

class MODLGroup:
    def __repr__(self) -> str: return f'{self.value}'
    def __init__(self, r: Reader, dataSize: int): self.value: str = r.readYEncoding(dataSize)
    bound: float
    textures: bytes # Texture Files Hashes
    def MODBField(self, r: Reader, dataSize: int) -> object: return setattr(self, bound, r.readSingle())
    def MODTField(self, r: Reader, dataSize: int) -> object: return setattr(self, textures, r.readBytes(dataSize))

#endregion

#region Base : Record

class Record:
    def __repr__(self) -> str: return f'{self.__name__}:{self.EDID.value}'
    Empty: Record = Record()
    header: Header
    @property
    def id(self) -> int: return self.header.formId
    EDID: STRVField # Editor ID

    # Return an uninitialized subrecord to deserialize, or null to skip.
    def createField(self, r: Reader, format: FormType, type: FieldType, dataSize: int) -> object: return Empty

    def read(self, r: Reader, filePath: str, format: FormType) -> None:
        startTell = r.tell(); endTell = startTell + self.header.dataSize
        while r.tell() < endTell:
            fieldHeader = FieldHeader(r, format)
            if fieldHeader.type == FieldType.XXXX:
                if fieldHeader.dataSize != 4: raise Exception()
                fieldHeader.dataSize = r.readUInt32()
                continue
            elif fieldHeader.type == FieldType.OFST and Header.Type == FormType.WRLD: r.seek(endTell); continue
            tell = r.tell()
            if self.createField(r, format, fieldHeader.type, fieldHeader.dataSize) == Empty: print(f'Unsupported ESM record type: {self.header.type}:{fieldHeader.type}'); r.skip(fieldHeader.dataSize); continue
            # check full read
            if r.tell() != tell + fieldHeader.dataSize: raise Exception(f'Failed reading {self.header.type}:{fieldHeader.type} field data at offset {tell} in {filePath} of {r.tell() - tell - fieldHeader.dataSize}')
        # check full read
        if r.tell() != endTell: raise Exception(f'Failed reading {self.header.type} record data at offset {startTell} in {filePath}')

#endregion

#region Base : RecordGroup

#endregion

#region Base : Extensions

#endregion

#region Base : Reference Fields

#endregion




#region 3000 : TES3.Plugin info

class TES3Record(Record):
    class HEDRField:
        def __init__(r: Reader, dataSize: int):
            self.version: float = r.readSingle()
            self.fileType: int = r.readUInt32()
            self.companyName: str = r.readZString(32)
            self.fileDescription: str = r.readZString(256)
            self.numRecords: int = r.readUInt32()

        HEDR: any #HEDRField 
        MASTs: list[STRVField]
        DATAs: list[INTVField]

    def createField(r: Reader, format: FormType, type: FieldType, dataSize: int) -> object:
        match type:
            case FieldType.HEDR: return setattr(self, HEDR, HEDRField(r, dataSize))
            case FieldType.MAST: return None #(MASTs ??= []).AddX(r.ReadSTRV(dataSize))
            case FieldType.DATA: return None #(DATAs ??= []).AddX(r.ReadINTV(dataSize))
            case _: return Empty

#endregion

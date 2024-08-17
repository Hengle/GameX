using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace GameX.FileSystems.Casc
{
    #region Base

    public abstract class RootHandlerBase
    {
        protected readonly Jenkins96 Hasher = new();
        protected CascFolder Root;

        public virtual int Count { get; protected set; }
        public virtual int CountTotal { get; protected set; }
        public virtual int CountSelect { get; protected set; }
        public virtual int CountUnknown { get; protected set; }
        public virtual LocaleFlags Locale { get; protected set; }
        public bool OverrideArchive { get; protected set; }
        public bool PreferHighResTextures { get; protected set; }
        public abstract IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries();
        public abstract IEnumerable<RootEntry> GetAllEntries(ulong hash);
        public abstract IEnumerable<RootEntry> GetEntries(ulong hash);
        public abstract void LoadListFile(string path, BackgroundWorkerEx worker = null);
        public abstract void Clear();
        public abstract void Dump(EncodingHandler encodingHandler = null);
        protected abstract CascFolder CreateStorageTree();
        static readonly char[] PathDelimiters = ['/', '\\'];

        protected void CreateSubTree(CascFolder root, ulong filehash, string file)
        {
            var parts = file.Split(PathDelimiters);
            var folder = root;
            for (var i = 0; i < parts.Length; ++i)
            {
                var isFile = i == parts.Length - 1;
                var entryName = parts[i];
                if (isFile)
                {
                    var entry = folder.GetFile(entryName);
                    if (entry == null)
                    {
                        if (!CascFile.Files.ContainsKey(filehash)) CascFile.Files[filehash] = entry = new CascFile(filehash, file);
                        else entry = CascFile.Files[filehash];
                        folder.Files[entryName] = entry;
                    }
                }
                else
                {
                    var entry = folder.GetFolder(entryName);
                    if (entry == null) folder.Folders[entryName] = entry = new CascFolder(entryName);
                    folder = entry;
                }
            }
        }

        protected IEnumerable<RootEntry> GetEntriesForSelectedLocale(ulong hash)
        {
            var rootInfos = GetAllEntries(hash);
            if (!rootInfos.Any()) yield break;
            var rootInfosLocale = rootInfos.Where(re => (re.LocaleFlags & Locale) != 0);
            foreach (var entry in rootInfosLocale) yield return entry;
        }

        public void MergeInstall(InstallHandler install)
        {
            if (install == null) return;
            foreach (var entry in install.GetEntries())
                CreateSubTree(Root, Hasher.ComputeHash(entry.Name), entry.Name);
        }

        public CascFolder SetFlags(LocaleFlags locale, bool overrideArchive = false, bool preferHighResTextures = false, bool createTree = true)
        {
            using var _ = new PerfCounter(GetType().Name + "::SetFlags()");
            Locale = locale;
            OverrideArchive = overrideArchive;
            PreferHighResTextures = preferHighResTextures;
            if (createTree) Root = CreateStorageTree();
            return Root;
        }
    }

    #endregion

    #region D3RootHandler

    struct D3RootEntry
    {
        public MD5Hash cKey;
        public int Type;
        public int SNO;
        public int FileIndex;
        public string Name;

        public static D3RootEntry Read(int type, BinaryReader s)
        {
            var e = new D3RootEntry()
            {
                Type = type,
                cKey = s.Read<MD5Hash>()
            };
            if (type == 0 || type == 1) // has SNO id
            {
                e.SNO = s.ReadInt32();
                if (type == 1) e.FileIndex = s.ReadInt32(); // has file index
            }
            else e.Name = s.ReadCString(); // Named file
            return e;
        }
    }

    public class D3RootHandler : RootHandlerBase
    {
        readonly MultiDictionary<ulong, RootEntry> RootData = new();
        readonly Dictionary<string, List<D3RootEntry>> D3RootData = [];
        CoreTOCParser tocParser;
        PackagesParser pkgParser;

        public override int Count => RootData.Count;
        public override int CountTotal => RootData.Sum(re => re.Value.Count);

        public D3RootHandler(BinaryReader stream, BackgroundWorkerEx worker, CascHandler casc)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            byte b1 = stream.ReadByte();
            byte b2 = stream.ReadByte();
            byte b3 = stream.ReadByte();
            byte b4 = stream.ReadByte();

            int count = stream.ReadInt32();

            for (int j = 0; j < count; j++)
            {
                MD5Hash md5 = stream.Read<MD5Hash>();
                string name = stream.ReadCString();

                var entries = new List<D3RootEntry>();
                D3RootData[name] = entries;

                if (!casc.Encoding.GetEntry(md5, out EncodingEntry enc))
                    continue;

                using (BinaryReader s = new BinaryReader(casc.OpenFile(enc.Keys[0])))
                {
                    uint magic = s.ReadUInt32();

                    int nEntries0 = s.ReadInt32();

                    for (int i = 0; i < nEntries0; i++)
                    {
                        entries.Add(D3RootEntry.Read(0, s));
                    }

                    int nEntries1 = s.ReadInt32();

                    for (int i = 0; i < nEntries1; i++)
                    {
                        entries.Add(D3RootEntry.Read(1, s));
                    }

                    int nNamedEntries = s.ReadInt32();

                    for (int i = 0; i < nNamedEntries; i++)
                    {
                        entries.Add(D3RootEntry.Read(2, s));
                    }
                }

                worker?.ReportProgress((int)((j + 1) / (float)(count + 2) * 100));
            }

            // Parse CoreTOC.dat
            var coreTocEntry = D3RootData["Base"].Find(e => e.Name == "CoreTOC.dat");

            casc.Encoding.GetEntry(coreTocEntry.cKey, out EncodingEntry enc1);

            using (var file = casc.OpenFile(enc1.Keys[0]))
                tocParser = new CoreTOCParser(file);

            worker?.ReportProgress((int)((count + 1) / (float)(count + 2) * 100));

            // Parse Packages.dat
            var pkgEntry = D3RootData["Base"].Find(e => e.Name == "Data_D3\\PC\\Misc\\Packages.dat");

            casc.Encoding.GetEntry(pkgEntry.cKey, out EncodingEntry enc2);

            using (var file = casc.OpenFile(enc2.Keys[0]))
                pkgParser = new PackagesParser(file);

            worker?.ReportProgress(100);
        }

        public override void Clear()
        {
            RootData.Clear();
            D3RootData.Clear();
            tocParser = null;
            pkgParser = null;
            CascFile.Files.Clear();
        }

        public override void Dump(EncodingHandler encodingHandler = null)
        {

        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            foreach (var set in RootData)
                foreach (var entry in set.Value)
                    yield return new KeyValuePair<ulong, RootEntry>(set.Key, entry);
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            RootData.TryGetValue(hash, out List<RootEntry> result);

            if (result == null)
                yield break;

            foreach (var entry in result)
                yield return entry;
        }

        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            return GetEntriesForSelectedLocale(hash);
        }

        void AddFile(string pkg, D3RootEntry e)
        {
            string name;

            switch (e.Type)
            {
                case 0:
                    SNOInfoD3 sno1 = tocParser.GetSNO(e.SNO);
                    name = string.Format("{0}\\{1}{2}", sno1.GroupId, sno1.Name, sno1.Ext);
                    break;
                case 1:
                    SNOInfoD3 sno2 = tocParser.GetSNO(e.SNO);
                    name = string.Format("{0}\\{1}\\{2:D4}", sno2.GroupId, sno2.Name, e.FileIndex);

                    string ext = pkgParser.GetExtension(name);

                    if (ext != null)
                        name += ext;
                    else
                    {
                        CountUnknown++;
                        name += ".xxx";
                    }
                    break;
                case 2:
                    name = e.Name;
                    break;
                default:
                    name = "Unknown";
                    break;
            }

            RootEntry entry = new RootEntry
            {
                cKey = e.cKey
            };

            if (Enum.TryParse(pkg, out LocaleFlags locale))
                entry.LocaleFlags = locale;
            else
                entry.LocaleFlags = LocaleFlags.All;

            ulong fileHash = Hasher.ComputeHash(name);
            CascFile.Files[fileHash] = new CascFile(fileHash, name);

            RootData.Add(fileHash, entry);
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {
            worker?.ReportProgress(0, "Loading \"listfile\"...");

            Logger.WriteLine("D3RootHandler: loading file names...");

            int numFiles = D3RootData.Sum(p => p.Value.Count);

            int i = 0;

            foreach (var kv in D3RootData)
            {
                foreach (var e in kv.Value)
                {
                    AddFile(kv.Key, e);

                    worker?.ReportProgress((int)(++i / (float)numFiles * 100));
                }
            }

            Logger.WriteLine("D3RootHandler: loaded {0} file names", i);
        }

        protected override CascFolder CreateStorageTree()
        {
            var root = new CascFolder("root");

            CountSelect = 0;

            // Create new tree based on specified locale
            foreach (var rootEntry in RootData)
            {
                var rootInfosLocale = rootEntry.Value.Where(re => (re.LocaleFlags & Locale) != 0);

                if (!rootInfosLocale.Any())
                    continue;

                CreateSubTree(root, rootEntry.Key, CascFile.Files[rootEntry.Key].FullName);
                CountSelect++;
            }

            Logger.WriteLine("D3RootHandler: {0} file names missing extensions for locale {1}", CountUnknown, Locale);

            return root;
        }
    }

    public class SNOInfoD3
    {
        public SNOGroup GroupId;
        public string Name;
        public string Ext;
    }

    public enum SNOGroup
    {
        Code = -2,
        None = -1,
        Actor = 1,
        Adventure = 2,
        AiBehavior = 3,
        AiState = 4,
        AmbientSound = 5,
        Anim = 6,
        Animation2D = 7,
        AnimSet = 8,
        Appearance = 9,
        Hero = 10,
        Cloth = 11,
        Conversation = 12,
        ConversationList = 13,
        EffectGroup = 14,
        Encounter = 15,
        Explosion = 17,
        FlagSet = 18,
        Font = 19,
        GameBalance = 20,
        Globals = 21,
        LevelArea = 22,
        Light = 23,
        MarkerSet = 24,
        Monster = 25,
        Observer = 26,
        Particle = 27,
        Physics = 28,
        Power = 29,
        Quest = 31,
        Rope = 32,
        Scene = 33,
        SceneGroup = 34,
        Script = 35,
        ShaderMap = 36,
        Shaders = 37,
        Shakes = 38,
        SkillKit = 39,
        Sound = 40,
        SoundBank = 41,
        StringList = 42,
        Surface = 43,
        Textures = 44,
        Trail = 45,
        UI = 46,
        Weather = 47,
        Worlds = 48,
        Recipe = 49,
        Condition = 51,
        TreasureClass = 52,
        Account = 53,
        Conductor = 54,
        TimedEvent = 55,
        Act = 56,
        Material = 57,
        QuestRange = 58,
        Lore = 59,
        Reverb = 60,
        PhysMesh = 61,
        Music = 62,
        Tutorial = 63,
        BossEncounter = 64,
        ControlScheme = 65,
        Accolade = 66,
        AnimTree = 67,
        Vibration = 68,
        DungeonFinder = 69,
    }

    public class CoreTOCParser
    {
        const int NUM_SNO_GROUPS = 70;

        public unsafe struct TOCHeader
        {
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_SNO_GROUPS)]
            public fixed int entryCounts[NUM_SNO_GROUPS];
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_SNO_GROUPS)]
            public fixed int entryOffsets[NUM_SNO_GROUPS];
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = NUM_SNO_GROUPS)]
            public fixed int entryUnkCounts[NUM_SNO_GROUPS];
            public int unk;
        }

        readonly Dictionary<int, SNOInfoD3> snoDic = new Dictionary<int, SNOInfoD3>();

        readonly Dictionary<SNOGroup, string> extensions = new Dictionary<SNOGroup, string>()
        {
            { SNOGroup.Code, "" },
            { SNOGroup.None, "" },
            { SNOGroup.Actor, ".acr" },
            { SNOGroup.Adventure, ".adv" },
            { SNOGroup.AiBehavior, "" },
            { SNOGroup.AiState, "" },
            { SNOGroup.AmbientSound, ".ams" },
            { SNOGroup.Anim, ".ani" },
            { SNOGroup.Animation2D, ".an2" },
            { SNOGroup.AnimSet, ".ans" },
            { SNOGroup.Appearance, ".app" },
            { SNOGroup.Hero, "" },
            { SNOGroup.Cloth, ".clt" },
            { SNOGroup.Conversation, ".cnv" },
            { SNOGroup.ConversationList, "" },
            { SNOGroup.EffectGroup, ".efg" },
            { SNOGroup.Encounter, ".enc" },
            { SNOGroup.Explosion, ".xpl" },
            { SNOGroup.FlagSet, "" },
            { SNOGroup.Font, ".fnt" },
            { SNOGroup.GameBalance, ".gam" },
            { SNOGroup.Globals, ".glo" },
            { SNOGroup.LevelArea, ".lvl" },
            { SNOGroup.Light, ".lit" },
            { SNOGroup.MarkerSet, ".mrk" },
            { SNOGroup.Monster, ".mon" },
            { SNOGroup.Observer, ".obs" },
            { SNOGroup.Particle, ".prt" },
            { SNOGroup.Physics, ".phy" },
            { SNOGroup.Power, ".pow" },
            { SNOGroup.Quest, ".qst" },
            { SNOGroup.Rope, ".rop" },
            { SNOGroup.Scene, ".scn" },
            { SNOGroup.SceneGroup, ".scg" },
            { SNOGroup.Script, "" },
            { SNOGroup.ShaderMap, ".shm" },
            { SNOGroup.Shaders, ".shd" },
            { SNOGroup.Shakes, ".shk" },
            { SNOGroup.SkillKit, ".skl" },
            { SNOGroup.Sound, ".snd" },
            { SNOGroup.SoundBank, ".sbk" },
            { SNOGroup.StringList, ".stl" },
            { SNOGroup.Surface, ".srf" },
            { SNOGroup.Textures, ".tex" },
            { SNOGroup.Trail, ".trl" },
            { SNOGroup.UI, ".ui" },
            { SNOGroup.Weather, ".wth" },
            { SNOGroup.Worlds, ".wrl" },
            { SNOGroup.Recipe, ".rcp" },
            { SNOGroup.Condition, ".cnd" },
            { SNOGroup.TreasureClass, "" },
            { SNOGroup.Account, "" },
            { SNOGroup.Conductor, "" },
            { SNOGroup.TimedEvent, "" },
            { SNOGroup.Act, ".act" },
            { SNOGroup.Material, ".mat" },
            { SNOGroup.QuestRange, ".qsr" },
            { SNOGroup.Lore, ".lor" },
            { SNOGroup.Reverb, ".rev" },
            { SNOGroup.PhysMesh, ".phm" },
            { SNOGroup.Music, ".mus" },
            { SNOGroup.Tutorial, ".tut" },
            { SNOGroup.BossEncounter, ".bos" },
            { SNOGroup.ControlScheme, "" },
            { SNOGroup.Accolade, ".aco" },
            { SNOGroup.AnimTree, ".ant" },
            { SNOGroup.Vibration, "" },
            { SNOGroup.DungeonFinder, "" },
        };

        public unsafe CoreTOCParser(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                TOCHeader hdr = br.Read<TOCHeader>();

                for (int i = 0; i < NUM_SNO_GROUPS; i++)
                {
                    if (hdr.entryCounts[i] > 0)
                    {
                        br.BaseStream.Position = hdr.entryOffsets[i] + Marshal.SizeOf(hdr);

                        for (int j = 0; j < hdr.entryCounts[i]; j++)
                        {
                            SNOGroup snoGroup = (SNOGroup)br.ReadInt32();
                            int snoId = br.ReadInt32();
                            int pName = br.ReadInt32();

                            long oldPos = br.BaseStream.Position;
                            br.BaseStream.Position = hdr.entryOffsets[i] + Marshal.SizeOf(hdr) + 12 * hdr.entryCounts[i] + pName;
                            string name = br.ReadCString();
                            br.BaseStream.Position = oldPos;

                            snoDic.Add(snoId, new SNOInfoD3() { GroupId = snoGroup, Name = name, Ext = extensions[snoGroup] });
                        }
                    }
                }
            }
        }

        public SNOInfoD3 GetSNO(int id)
        {
            snoDic.TryGetValue(id, out SNOInfoD3 sno);
            return sno;
        }
    }

    public class PackagesParser
    {
        readonly Dictionary<string, string> nameToExtDic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public PackagesParser(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                int sign = br.ReadInt32();
                int namesCount = br.ReadInt32();

                for (int i = 0; i < namesCount; i++)
                {
                    string name = br.ReadCString();
                    nameToExtDic[name.Substring(0, name.Length - 4)] = Path.GetExtension(name);
                }
            }
        }

        public string GetExtension(string partialName)
        {
            nameToExtDic.TryGetValue(partialName, out string ext);
            return ext;
        }
    }

    #endregion

    #region D4RootHandler

    public class D4RootHandler : TVFSRootHandler
    {
        CoreTocParserD4 TocParser;
        CascHandler CascHandler;
        readonly Dictionary<int, int> SharedPayloads = [];
        readonly Dictionary<int, string> EncryptedNames = [];
        readonly Dictionary<int, (int group, ulong keyId)> EncryptedSnos = [];

        public D4RootHandler(BackgroundWorkerEx worker, CascHandler casc) : base(worker, casc)
        {
            CascHandler = casc;
            worker?.ReportProgress(0, "Loading \"root\"...");
            // parse CoreTOC.dat
            var coreTocEntry = GetVfsRootEntries(Hasher.ComputeHash("Base\\CoreTOC.dat")).FirstOrDefault();
            using (var file = casc.OpenFile(coreTocEntry.eKey)) TocParser = new CoreTocParserD4(file);
            // parse CoreTOCSharedPayloadsMapping.dat
            var vfsCoreTocSharedPayloads = GetVfsRootEntries(Hasher.ComputeHash("Base\\CoreTOCSharedPayloadsMapping.dat"));
            // check if it exist (older versions missing it)
            if (vfsCoreTocSharedPayloads != null)
            {
                var coreTocSharedPayloads = vfsCoreTocSharedPayloads.FirstOrDefault();
                using var r = new BinaryReader(casc.OpenFile(coreTocSharedPayloads.eKey));
                var unk1 = r.ReadInt32();
                var count = r.ReadInt32();
                //using StreamWriter sw = new StreamWriter("CoreTOCSharedPayloadsMapping.txt");
                for (var i = 0; i < count; i++)
                {
                    var snoID = r.ReadInt32();
                    var sharedSnoID = r.ReadInt32();
                    SharedPayloads.Add(snoID, sharedSnoID);
                    //var sno1 = tocParser.GetSNO(snoID);
                    //var sno2 = tocParser.GetSNO(sharedSnoID);
                    //sw.WriteLine($"{snoID} {sno1.GroupId} {sno1.Name} -> {sharedSnoID} {sno2.GroupId} {sno2.Name}");
                }
            }
            // Parse EncryptedSNOs.dat and collect encryption keys
            var vfsEncryptedSNOs = GetVfsRootEntries(Hasher.ComputeHash("Base\\EncryptedSNOs.dat"));
            if (vfsEncryptedSNOs != null)
            {
                var encryptedSNOsEntry = vfsEncryptedSNOs.FirstOrDefault();
                using var r = new BinaryReader(casc.OpenFile(encryptedSNOsEntry.eKey));
                var unkHash = r.ReadInt32();
                var count = r.ReadInt32();
                for (var i = 0; i < count; i++)
                {
                    var snoGroup = r.ReadInt32();
                    var snoID = r.ReadInt32();
                    var keyID = r.ReadUInt64();
                    EncryptedSnos.Add(snoID, (snoGroup, keyID));
                }
            }

            // Parse EncryptedNameDict-0xXXXXXXXXXXXXXXXX.dat files
#if NETSTANDARD2_0
            var encKeys = new HashSet<ulong>(); foreach (var encSno in EncryptedSnos) encKeys.Add(encSno.Value.keyId);
#else
            var encKeys = EncryptedSnos.Select(e => e.Value.keyId).ToHashSet();
#endif
            foreach (var encKey in encKeys)
            {
                if (!KeyService.HasKey(BinaryPrimitives.ReverseEndianness(encKey))) continue;
                var encDictPath = $"Base\\EncryptedNameDict-0x{encKey:X16}.dat";
                var encDictEntries = GetVfsRootEntries(Hasher.ComputeHash(encDictPath));
                if (encDictEntries == null) continue;
                var encDictEntry = encDictEntries.FirstOrDefault();
                try
                {
                    using var r = new BinaryReader(casc.OpenFile(encDictEntry.eKey));
                    var unkHash = r.ReadInt32();
                    var count = r.ReadInt32();
                    var snoIDs = new List<int>(count);
                    for (int i = 0; i < count; i++)
                    {
                        var snoGroup = r.ReadInt32();
                        var snoID = r.ReadInt32();
                        snoIDs.Add(snoID);
                    }
                    for (var i = 0; i < count; i++)
                    {
                        var name = r.ReadCString();
                        EncryptedNames[snoIDs[i]] = name;
                    }
                }
                catch (BLTEDecoderException) { } // Unknown key name
            }
            // TODO: handle base/CoreTOCReplacedSnosMapping.dat?
            worker?.ReportProgress(100);
        }

        public override void Clear()
        {
            TocParser = null;
            CascHandler = null;
            base.Clear();
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {
            worker?.ReportProgress(0, "Loading \"listfile\"...");
            Logger.WriteLine("D4RootHandler: loading file names...");
            Logger.WriteLine("D4RootHandler: loaded {0} file names", 0);
        }

        protected override CascFolder CreateStorageTree()
        {
            var root = base.CreateStorageTree();
            CountSelect = 0;
            var locales = Enum.GetNames(typeof(LocaleFlags));
            string[] folders = ["Base", "Speech", "Text"];
            string[] subfolders = ["child", "meta", "payload", "paylow", "paymed"];
            HashSet<string> payloadFolders = ["payload", "paylow", "paymed"];
            List<string> filesToRemove = [];

            void CreateNewFileEntry(CascFile file, string newName)
            {
                var newHash = Hasher.ComputeHash(newName);
                SetHashDuplicate(file.Hash, newHash);
                CreateSubTree(root, newHash, newName);
                CountSelect++;
            }

            void CreateSNOEntry(int snoid, CascFile file, string folder, string subfolder, int subid = -1)
            {
                var sno = TocParser.GetSNO(snoid);
                if (sno != null)
                {
                    if (EncryptedSnos.ContainsKey(snoid))
                        sno.Name = EncryptedNames.TryGetValue(snoid, out var encName) ? encName : $"_encrypted_{snoid}";    // Override with encrypted name
                    var newName = Path.Combine(folder, subfolder, sno.GroupId.ToString(), subid == -1 ? $"{sno.Name}{sno.Ext}" : $"{sno.Name}-{subid}{sno.Ext}");
                    CreateNewFileEntry(file, newName);
                }
                else Logger.WriteLine($"SNO {snoid} (file {file.FullName}) not found!");
            }

            void CleanupFolder(CascFolder folder)
            {
                foreach (var file in filesToRemove) folder.Files.Remove(file);
                filesToRemove.Clear();
            }

            foreach (var folder in folders) ProcessFolder(folder);
            foreach (var locale in locales) foreach (var folder in folders) ProcessFolder(locale + "_" + folder);

            void ProcessFolder(string folder)
            {
                if (root.Folders.TryGetValue(folder, out var folder1))
                    foreach (var subfolder in subfolders)
                        if (folder1.Folders.TryGetValue(subfolder, out var subfolder1))
                        {
                            foreach (var child in subfolder1.Files)
                            {
                                if (child.Key.Contains('-'))
                                {
                                    var tokens = child.Key.Split('-');
                                    if (tokens.Length != 2) continue;
                                    var snoid = int.Parse(tokens[0]);
                                    var subId = int.Parse(tokens[1]);
                                    CreateSNOEntry(snoid, child.Value, folder, subfolder, subId);
                                }
                                else
                                {
                                    var snoid = int.Parse(child.Key);
                                    CreateSNOEntry(snoid, child.Value, folder, subfolder);
                                }
                                filesToRemove.Add(child.Key);
                            }
                            CleanupFolder(subfolder1);
                        }
            }

            foreach (var sharedPayload in SharedPayloads)
            {
                //var sno1 = tocParser.GetSNO(sharedPayload.Key);
                var sno2 = TocParser.GetSNO(sharedPayload.Value);
                // shared payloads seems to be only used for textures which are in "Base" folder
                if (root.Folders.TryGetValue("Base", out var baseFolder))
                    foreach (var payloadFolder in payloadFolders)
                        if (baseFolder.Folders.TryGetValue(payloadFolder, out var subfolder1))
                            if (subfolder1.Folders.TryGetValue($"{sno2.GroupId}", out var subfolder2))
                                if (subfolder2.Files.TryGetValue($"{sno2.Name}{sno2.Ext}", out var file))
                                    CreateSNOEntry(sharedPayload.Key, file, "Base", subfolder1.Name);
            }

            // move "package" files
            foreach (var folder in folders)
            {
                foreach (var locale in locales)
                {
                    var fileKey = $"{locale}.{folder}";
                    if (root.Files.TryGetValue(fileKey, out var file))
                    {
                        var newName = $"Packages\\{fileKey}";
                        CreateNewFileEntry(file, newName);
                        filesToRemove.Add(fileKey);
                    }
                }
            }

            CreateNewFileEntry(root.Files["Base"], $"Packages\\enUS.Base");
            filesToRemove.Add("Base");

            CleanupFolder(root);
            Logger.WriteLine($"D4RootHandler: {CountUnknown} file names missing for locale {Locale}");
            return root;
        }

        public Stream OpenFile(string prefix, D4FolderType folderType, int snoId, int subId = -1)
        {
            var sno = TocParser.GetSNO(snoId);
            if (sno == null) return null;
            var fileName = Path.Combine(prefix, folderType.ToString(), sno.GroupId.ToString(), subId == -1 ? $"{sno.Name}{sno.Ext}" : $"{sno.Name}-{subId}{sno.Ext}");
            var fileHash = Hasher.ComputeHash(fileName);
            return CascHandler.OpenFile(fileHash);
        }
    }

    public enum D4FolderType
    {
        Child,
        Meta,
        Payload,
        PayLow,
        PayMed
    }

    public class SnoInfoD4
    {
        public SnoGroupD4 GroupId;
        public string Name;
        public string Ext;
    }

    public enum SnoGroupD4
    {
        Unknown = -3,
        Code = -2,
        None = -1,
        Actor = 1,
        NPCComponentSet = 2,
        AIBehavior = 3,
        AIState = 4,
        AmbientSound = 5,
        Anim = 6,
        Anim2D = 7,
        AnimSet = 8,
        Appearance = 9,
        Hero = 10,
        Cloth = 11,
        Conversation = 12,
        ConversationList = 13,
        EffectGroup = 14,
        Encounter = 15,
        Explosion = 17,
        FlagSet = 18,
        Font = 19,
        GameBalance = 20,
        Global = 21,
        LevelArea = 22,
        Light = 23,
        MarkerSet = 24,
        Observer = 26,
        Particle = 27,
        Physics = 28,
        Power = 29,
        Quest = 31,
        Rope = 32,
        Scene = 33,
        Script = 35,
        ShaderMap = 36,
        Shader = 37,
        Shake = 38,
        SkillKit = 39,
        Sound = 40,
        StringList = 42,
        Surface = 43,
        Texture = 44,
        Trail = 45,
        UI = 46,
        Weather = 47,
        World = 48,
        Recipe = 49,
        Condition = 51,
        TreasureClass = 52,
        Account = 53,
        Material = 57,
        Lore = 59,
        Reverb = 60,
        Music = 62,
        Tutorial = 63,
        AnimTree = 67,
        Vibration = 68,
        wWiseSoundBank = 71,
        Speaker = 72,
        Item = 73,
        PlayerClass = 74,
        FogVolume = 76,
        Biome = 77,
        Wall = 78,
        SoundTable = 79,
        Subzone = 80,
        MaterialValue = 81,
        MonsterFamily = 82,
        TileSet = 83,
        Population = 84,
        MaterialValueSet = 85,
        WorldState = 86,
        Schedule = 87,
        VectorField = 88,
        Storyboard = 90,
        Territory = 92,
        AudioContext = 93,
        VOProcess = 94,
        DemonScroll = 95,
        QuestChain = 96,
        LoudnessPreset = 97,
        ItemType = 98,
        Achievement = 99,
        Crafter = 100,
        HoudiniParticlesSim = 101,
        Movie = 102,
        TiledStyle = 103,
        Affix = 104,
        Reputation = 105,
        ParagonNode = 106,
        MonsterAffix = 107,
        ParagonBoard = 108,
        SetItemBonus = 109,
        StoreProduct = 110,
        ParagonGlyph = 111,
        ParagonGlyphAffix = 112,
        Challenge = 114,
        MarkingShape = 115,
        ItemRequirement = 116,
        Boost = 117,
        Emote = 118,
        Jewelry = 119,
        PlayerTitle = 120,
        Emblem = 121,
        Dye = 122,
        FogOfWar = 123,
        ParagonThreshold = 124,
        AIAwareness = 125,
        TrackedReward = 126,
        CollisionSettings = 127,
        Aspect = 128,
        ABTest = 129,
        Stagger = 130,
        EyeColor = 131,
        Makeup = 132,
        MarkingColor = 133,
        HairColor = 134,
        DungeonAffix = 135,
        Activity = 136,
        Season = 137,
        HairStyle = 138,
        FacialHair = 139,
        Face = 140,
        MercenaryClass = 141,
        PassivePowerContainer = 142,
        MountProfile = 143,
        AICoordinator = 144,
        CrafterTab = 145,
        TownPortalCosmetic = 146,
        AxeTest = 147,
        Wizard = 148,
        FootstepTable = 149,
        Modal = 150,
        CollectiblePower = 151,
        AppearanceSet = 152,
        Preset = 153,
        PreviewComposition = 154,
        SpawnPool = 155,
        Unknown_156 = 156, // .rdx
        BattlePassTier = 157,
        Zone = 158,
        Unknown_159 = 159, // .ggu
        Unknown_160 = 160, // .dtk
        Snippet = 161,
        CommunityModifier = 162,
        GenericNodeGraph = 163,
        UserDefinedData = 164,
        Unknown_165 = 165, // .fds
        Unknown_166 = 166, // .bvr
        Unknown_167 = 167, // .asv
        Unknown_168 = 168, // .dmg
        MAX_SNO_GROUPS = 169,
    }

    public class CoreTocParserD4
    {
        const int MAX_SNO_GROUPS = 169;

        public unsafe struct TocHeader
        {
            public int numSnoGroups;
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_SNO_GROUPS)]
            public fixed int entryCounts[MAX_SNO_GROUPS];
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_SNO_GROUPS)]
            public fixed int entryOffsets[MAX_SNO_GROUPS];
            //[MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_SNO_GROUPS)]
            public fixed int entryUnkCounts[MAX_SNO_GROUPS];
            public int unk;
        }

        readonly Dictionary<int, SnoInfoD4> snoDic = [];
        public IReadOnlyDictionary<int, SnoInfoD4> SnoData => snoDic;
        readonly Dictionary<SnoGroupD4, string> extensions = new()
        {
            [(SnoGroupD4)0] = "",
            [(SnoGroupD4)1] = ".acr",
            [(SnoGroupD4)2] = ".npc",
            [(SnoGroupD4)3] = ".aib",
            [(SnoGroupD4)4] = ".ais",
            [(SnoGroupD4)5] = ".ams",
            [(SnoGroupD4)6] = ".ani",
            [(SnoGroupD4)7] = ".an2",
            [(SnoGroupD4)8] = ".ans",
            [(SnoGroupD4)9] = ".app",
            [(SnoGroupD4)10] = ".hro",
            [(SnoGroupD4)11] = ".clt",
            [(SnoGroupD4)12] = ".cnv",
            [(SnoGroupD4)13] = ".cnl",
            [(SnoGroupD4)14] = ".efg",
            [(SnoGroupD4)15] = ".enc",
            [(SnoGroupD4)16] = "",
            [(SnoGroupD4)17] = ".xpl",
            [(SnoGroupD4)18] = ".flg",
            [(SnoGroupD4)19] = ".fnt",
            [(SnoGroupD4)20] = ".gam",
            [(SnoGroupD4)21] = ".glo",
            [(SnoGroupD4)22] = ".lvl",
            [(SnoGroupD4)23] = ".lit",
            [(SnoGroupD4)24] = ".mrk",
            [(SnoGroupD4)25] = "",
            [(SnoGroupD4)26] = ".obs",
            [(SnoGroupD4)27] = ".prt",
            [(SnoGroupD4)28] = ".phy",
            [(SnoGroupD4)29] = ".pow",
            [(SnoGroupD4)30] = "",
            [(SnoGroupD4)31] = ".qst",
            [(SnoGroupD4)32] = ".rop",
            [(SnoGroupD4)33] = ".scn",
            [(SnoGroupD4)34] = "",
            [(SnoGroupD4)35] = ".scr",
            [(SnoGroupD4)36] = ".shm",
            [(SnoGroupD4)37] = ".shd",
            [(SnoGroupD4)38] = ".shk",
            [(SnoGroupD4)39] = ".skl",
            [(SnoGroupD4)40] = ".snd",
            [(SnoGroupD4)41] = "",
            [(SnoGroupD4)42] = ".stl",
            [(SnoGroupD4)43] = ".srf",
            [(SnoGroupD4)44] = ".tex",
            [(SnoGroupD4)45] = ".trl",
            [(SnoGroupD4)46] = ".ui",
            [(SnoGroupD4)47] = ".wth",
            [(SnoGroupD4)48] = ".wrl",
            [(SnoGroupD4)49] = ".rcp",
            [(SnoGroupD4)50] = "",
            [(SnoGroupD4)51] = ".cnd",
            [(SnoGroupD4)52] = ".trs",
            [(SnoGroupD4)53] = ".acc",
            [(SnoGroupD4)54] = "",
            [(SnoGroupD4)55] = "",
            [(SnoGroupD4)56] = "",
            [(SnoGroupD4)57] = ".mat",
            [(SnoGroupD4)58] = "",
            [(SnoGroupD4)59] = ".lor",
            [(SnoGroupD4)60] = ".rev",
            [(SnoGroupD4)61] = "",
            [(SnoGroupD4)62] = ".mus",
            [(SnoGroupD4)63] = ".tut",
            [(SnoGroupD4)64] = "",
            [(SnoGroupD4)65] = "",
            [(SnoGroupD4)66] = "",
            [(SnoGroupD4)67] = ".ant",
            [(SnoGroupD4)68] = ".vib",
            [(SnoGroupD4)69] = "",
            [(SnoGroupD4)70] = "",
            [(SnoGroupD4)71] = ".wsb",
            [(SnoGroupD4)72] = ".spk",
            [(SnoGroupD4)73] = ".itm",
            [(SnoGroupD4)74] = ".pcl",
            [(SnoGroupD4)75] = "",
            [(SnoGroupD4)76] = ".fog",
            [(SnoGroupD4)77] = ".bio",
            [(SnoGroupD4)78] = ".wal",
            [(SnoGroupD4)79] = ".sdt",
            [(SnoGroupD4)80] = ".sbz",
            [(SnoGroupD4)81] = ".mtv",
            [(SnoGroupD4)82] = ".mfm",
            [(SnoGroupD4)83] = ".tst",
            [(SnoGroupD4)84] = ".pop",
            [(SnoGroupD4)85] = ".mvs",
            [(SnoGroupD4)86] = ".wst",
            [(SnoGroupD4)87] = ".sch",
            [(SnoGroupD4)88] = ".vfd",
            [(SnoGroupD4)89] = "",
            [(SnoGroupD4)90] = ".stb",
            [(SnoGroupD4)91] = "",
            [(SnoGroupD4)92] = ".ter",
            [(SnoGroupD4)93] = ".auc",
            [(SnoGroupD4)94] = ".vop",
            [(SnoGroupD4)95] = ".dss",
            [(SnoGroupD4)96] = ".qc",
            [(SnoGroupD4)97] = ".lou",
            [(SnoGroupD4)98] = ".itt",
            [(SnoGroupD4)99] = ".ach",
            [(SnoGroupD4)100] = ".crf",
            [(SnoGroupD4)101] = ".hps",
            [(SnoGroupD4)102] = ".vid",
            [(SnoGroupD4)103] = ".tsl",
            [(SnoGroupD4)104] = ".aff",
            [(SnoGroupD4)105] = ".rep",
            [(SnoGroupD4)106] = ".pgn",
            [(SnoGroupD4)107] = ".maf",
            [(SnoGroupD4)108] = ".pbd",
            [(SnoGroupD4)109] = ".set",
            [(SnoGroupD4)110] = ".prd",
            [(SnoGroupD4)111] = ".gph",
            [(SnoGroupD4)112] = ".gaf",
            [(SnoGroupD4)113] = "",
            [(SnoGroupD4)114] = ".cha",
            [(SnoGroupD4)115] = ".msh",
            [(SnoGroupD4)116] = ".irq",
            [(SnoGroupD4)117] = ".bst",
            [(SnoGroupD4)118] = ".emo",
            [(SnoGroupD4)119] = ".jwl",
            [(SnoGroupD4)120] = ".pt",
            [(SnoGroupD4)121] = ".emb",
            [(SnoGroupD4)122] = ".dye",
            [(SnoGroupD4)123] = ".fow",
            [(SnoGroupD4)124] = ".pth",
            [(SnoGroupD4)125] = ".aia",
            [(SnoGroupD4)126] = ".trd",
            [(SnoGroupD4)127] = ".col",
            [(SnoGroupD4)128] = ".asp",
            [(SnoGroupD4)129] = ".abt",
            [(SnoGroupD4)130] = ".stg",
            [(SnoGroupD4)131] = ".eye",
            [(SnoGroupD4)132] = ".mak",
            [(SnoGroupD4)133] = ".mcl",
            [(SnoGroupD4)134] = ".hcl",
            [(SnoGroupD4)135] = ".dax",
            [(SnoGroupD4)136] = ".act",
            [(SnoGroupD4)137] = ".sea",
            [(SnoGroupD4)138] = ".har",
            [(SnoGroupD4)139] = ".fhr",
            [(SnoGroupD4)140] = ".fac",
            [(SnoGroupD4)141] = ".mrc",
            [(SnoGroupD4)142] = ".ppc",
            [(SnoGroupD4)143] = ".mpp",
            [(SnoGroupD4)144] = ".aic",
            [(SnoGroupD4)145] = ".ctb",
            [(SnoGroupD4)146] = ".tpc",
            [(SnoGroupD4)147] = ".axe",
            [(SnoGroupD4)148] = ".wiz",
            [(SnoGroupD4)149] = ".fst",
            [(SnoGroupD4)150] = ".mdl",
            [(SnoGroupD4)151] = ".cpw",
            [(SnoGroupD4)152] = ".aps",
            [(SnoGroupD4)153] = ".pst",
            [(SnoGroupD4)154] = ".pvc",
            [(SnoGroupD4)155] = ".spn",
            [(SnoGroupD4)156] = ".rdx",
            [(SnoGroupD4)157] = ".bpt",
            [(SnoGroupD4)158] = ".zon",
            [(SnoGroupD4)159] = ".ggu",
            [(SnoGroupD4)160] = ".dtk",
            [(SnoGroupD4)161] = ".snp",
            [(SnoGroupD4)162] = ".cmo",
            [(SnoGroupD4)163] = ".gng",
            [(SnoGroupD4)164] = ".udd",
            [(SnoGroupD4)165] = ".fds",
            [(SnoGroupD4)166] = ".bvr",
            [(SnoGroupD4)167] = ".asv",
            [(SnoGroupD4)168] = ".dmg",
        };

        public unsafe CoreTocParserD4(Stream stream)
        {
            using var r = new BinaryReader(stream);
            var numSnoGroups = r.ReadInt32();
            //if (numSnoGroups != NUM_SNO_GROUPS) return;
            var entryCounts = new int[numSnoGroups];
            for (var i = 0; i < entryCounts.Length; i++) entryCounts[i] = r.ReadInt32();
            var entryOffsets = new int[numSnoGroups];
            for (var i = 0; i < entryOffsets.Length; i++) entryOffsets[i] = r.ReadInt32();
            var entryUnkCounts = new int[numSnoGroups];
            for (var i = 0; i < entryUnkCounts.Length; i++) entryUnkCounts[i] = r.ReadInt32();
            var unk1 = r.ReadInt32();
            var headerSize = 4 + numSnoGroups * (4 + 4 + 4) + 4;
            for (var i = 0; i < numSnoGroups; i++)
                if (entryCounts[i] > 0)
                {
                    r.BaseStream.Position = entryOffsets[i] + headerSize;
                    for (int j = 0; j < entryCounts[i]; j++)
                    {
                        var snoGroup = (SnoGroupD4)r.ReadInt32();
                        var snoId = r.ReadInt32();
                        var pName = r.ReadInt32();
                        var oldPos = r.BaseStream.Position;
                        r.BaseStream.Position = entryOffsets[i] + headerSize + 12 * entryCounts[i] + pName;
                        var name = r.ReadCString();
                        r.BaseStream.Position = oldPos;
                        snoDic.Add(snoId, new SnoInfoD4 { GroupId = snoGroup, Name = name, Ext = extensions.TryGetValue(snoGroup, out var ext) ? ext : $".{(int)snoGroup:D3}" });
                    }
                }
        }

        public SnoInfoD4 GetSNO(int id) { snoDic.TryGetValue(id, out var sno); return sno; }
    }

    #endregion

    #region DummyRootHandler

    public class DummyRootHandler : RootHandlerBase
    {
        public DummyRootHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            // root file is executable, skip

            worker?.ReportProgress(100);
        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            yield break;
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            yield break;
        }

        // Returns only entries that match current locale and content flags
        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            yield break;
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {

        }

        protected override CascFolder CreateStorageTree()
        {
            var root = new CascFolder("root");

            CountSelect = 0;

            // Cleanup fake names for unknown files
            CountUnknown = 0;

            Logger.WriteLine("HSRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }

        public override void Clear()
        {
            Root.Files.Clear();
            Root.Folders.Clear();
            CascFile.Files.Clear();
        }

        public override void Dump(EncodingHandler encodingHandler = null)
        {

        }
    }

    #endregion

    #region MNDXRootHandler

    public struct MNDXHeader
    {
        public int Signature;                            // 'MNDX'
        public int HeaderVersion;                        // Must be <= 2
        public int FormatVersion;
    }

    public struct MARInfo
    {
        public int MarIndex;
        public int MarDataSize;
        public int MarDataSizeHi;
        public int MarDataOffset;
        public int MarDataOffsetHi;
    }

    public struct TRIPLET
    {
        public int BaseValue;
        public int Value2;
        public int Value3;
    }

    public struct NAME_FRAG
    {
        public int ItemIndex;   // Back index to various tables
        public int NextIndex;   // The following item index
        public int FragOffs;    // Higher 24 bits are 0xFFFFFF00 --> A single matching character
                                // Otherwise --> Offset to the name fragment table
    }

    class CASC_ROOT_ENTRY_MNDX
    {
        public MD5Hash cKey;         // Encoding key for the file
        public int Flags;           // High 8 bits: Flags, low 24 bits: package index
        public int FileSize;        // Uncompressed file size, in bytes
        public CASC_ROOT_ENTRY_MNDX Next;
    }

    public class PATH_STOP
    {
        public int ItemIndex { get; set; }
        public int Field_4 { get; set; }
        public int Field_8 { get; set; }
        public int Field_C { get; set; }
        public int Field_10 { get; set; }

        public PATH_STOP()
        {
            ItemIndex = 0;
            Field_4 = 0;
            Field_8 = 0;
            Field_C = -1;
            Field_10 = -1;
        }
    }

    class MNDXRootHandler : RootHandlerBase
    {
        private const int CASC_MNDX_SIGNATURE = 0x58444E4D;          // 'MNDX'
        private const int CASC_MAX_MAR_FILES = 3;

        //[0] - package names
        //[1] - file names stripped off package names
        //[2] - complete file names
        private MARFileNameDB[] MarFiles = new MARFileNameDB[CASC_MAX_MAR_FILES];

        private Dictionary<int, CASC_ROOT_ENTRY_MNDX> mndxRootEntries = new Dictionary<int, CASC_ROOT_ENTRY_MNDX>();
        private Dictionary<int, CASC_ROOT_ENTRY_MNDX> mndxRootEntriesValid;

        private Dictionary<int, string> Packages = new Dictionary<int, string>();
        private Dictionary<int, LocaleFlags> PackagesLocale = new Dictionary<int, LocaleFlags>();

        private Dictionary<ulong, RootEntry> mndxData = new Dictionary<ulong, RootEntry>();

        public override int Count => MarFiles[2].NumFiles;
        public override int CountTotal => MarFiles[2].NumFiles;

        public MNDXRootHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            var header = stream.Read<MNDXHeader>();

            if (header.Signature != CASC_MNDX_SIGNATURE || header.FormatVersion > 2 || header.FormatVersion < 1)
                throw new Exception("invalid root file");

            if (header.HeaderVersion == 2)
            {
                var build1 = stream.ReadInt32(); // build number
                var build2 = stream.ReadInt32(); // build number
            }

            int MarInfoOffset = stream.ReadInt32();                            // Offset of the first MAR entry info
            int MarInfoCount = stream.ReadInt32();                             // Number of the MAR info entries
            int MarInfoSize = stream.ReadInt32();                              // Size of the MAR info entry
            int MndxEntriesOffset = stream.ReadInt32();
            int MndxEntriesTotal = stream.ReadInt32();                         // Total number of MNDX root entries
            int MndxEntriesValid = stream.ReadInt32();                         // Number of valid MNDX root entries
            int MndxEntrySize = stream.ReadInt32();                            // Size of one MNDX root entry

            if (MarInfoCount > CASC_MAX_MAR_FILES || MarInfoSize != Marshal.SizeOf<MARInfo>())
                throw new Exception("invalid root file (1)");

            for (int i = 0; i < MarInfoCount; i++)
            {
                stream.BaseStream.Position = MarInfoOffset + (MarInfoSize * i);

                MARInfo marInfo = stream.Read<MARInfo>();

                stream.BaseStream.Position = marInfo.MarDataOffset;

                MarFiles[i] = new MARFileNameDB(stream);

                if (stream.BaseStream.Position != marInfo.MarDataOffset + marInfo.MarDataSize)
                    throw new Exception("MAR parsing error!");
            }

            //if (MndxEntrySize != Marshal.SizeOf(typeof(CASC_ROOT_ENTRY_MNDX)))
            //    throw new Exception("invalid root file (2)");

            stream.BaseStream.Position = MndxEntriesOffset;

            CASC_ROOT_ENTRY_MNDX prevEntry = null;

            //Dictionary<int, int> p = new Dictionary<int, int>();

            for (int i = 0; i < MndxEntriesTotal; i++)
            {
                CASC_ROOT_ENTRY_MNDX entry = new CASC_ROOT_ENTRY_MNDX();

                if (prevEntry != null)
                    prevEntry.Next = entry;

                prevEntry = entry;
                entry.Flags = stream.ReadInt32();
                entry.cKey = stream.Read<MD5Hash>();
                entry.FileSize = stream.ReadInt32();
                mndxRootEntries.Add(i, entry);

                //if ((entry.Flags & 0x80000000) != 0)
                //{
                //    if (!p.ContainsKey(entry.Flags & 0x00FFFFFF))
                //        p[entry.Flags & 0x00FFFFFF] = 1;
                //    else
                //        p[entry.Flags & 0x00FFFFFF]++;
                //}

                worker?.ReportProgress((int)((i + 1) / (float)MndxEntriesTotal * 100));
            }

            //for (int i = 0; i < MndxEntriesTotal; ++i)
            //    Logger.WriteLine("{0:X8} - {1:X8} - {2}", i, mndxRootEntries[i].Flags, mndxRootEntries[i].MD5.ToHexString());

            mndxRootEntriesValid = new Dictionary<int, CASC_ROOT_ENTRY_MNDX>();// mndxRootEntries.Where(e => (e.Flags & 0x80000000) != 0).ToList();

            //var e1 = mndxRootEntries.Where(e => (e.Value.Flags & 0x80000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e2 = mndxRootEntries.Where(e => (e.Value.Flags & 0x40000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e3 = mndxRootEntries.Where(e => (e.Value.Flags & 0x20000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e4 = mndxRootEntries.Where(e => (e.Value.Flags & 0x10000000) != 0).ToDictionary(e => e.Key, e => e.Value);

            //var e5 = mndxRootEntries.Where(e => (e.Value.Flags & 0x8000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e6 = mndxRootEntries.Where(e => (e.Value.Flags & 0x4000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e7 = mndxRootEntries.Where(e => (e.Value.Flags & 0x2000000) != 0).ToDictionary(e => e.Key, e => e.Value);
            //var e8 = mndxRootEntries.Where(e => (e.Value.Flags & 0x1000000) != 0).ToDictionary(e => e.Key, e => e.Value);

            //var e9 = mndxRootEntries.Where(e => (e.Value.Flags & 0x4000000) == 0).ToDictionary(e => e.Key, e => e.Value);

            //int c = 0;
            //foreach(var e in e9)
            //    Console.WriteLine("{0:X8} - {1:X8} - {2:X8} - {3}", c++,e.Key, e.Value.Flags, e.Value.EncodingKey.ToHexString());

            int ValidEntryCount = 1; // edx
            int index = 0;

            mndxRootEntriesValid[index++] = mndxRootEntries[0];

            for (int i = 0; i < MndxEntriesTotal; i++)
            {
                if (ValidEntryCount >= MndxEntriesValid)
                    break;

                if ((mndxRootEntries[i].Flags & 0x80000000) != 0)
                {
                    mndxRootEntriesValid[index++] = mndxRootEntries[i + 1];

                    ValidEntryCount++;
                }
            }

            //for (int i = 0, j = 0; i < MndxEntriesTotal; i++, j++)
            //{
            //    if ((mndxRootEntries[i].Flags & 0x80000000) != 0)
            //    {
            //        mndxRootEntriesValid[j] = mndxRootEntries[i];
            //    }
            //}
        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            return mndxData;
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            if (mndxData.TryGetValue(hash, out RootEntry rootEntry))
                yield return rootEntry;
        }

        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            //RootEntry rootEntry;
            //mndxData.TryGetValue(hash, out rootEntry);

            //if (rootEntry != null)
            //    yield return rootEntry;
            //else
            //    yield break;
            //return GetAllEntries(hash);
            return GetEntriesForSelectedLocale(hash);
        }

        private int FindMNDXPackage(string fileName)
        {
            int nMaxLength = 0;
            int pMatching = -1;

            int fileNameLen = fileName.Length;

            foreach (var package in Packages)
            {
                string pkgName = package.Value;
                int pkgNameLen = pkgName.Length;

                if (pkgNameLen < fileNameLen && pkgNameLen > nMaxLength)
                {
                    // Compare the package name
                    if (string.CompareOrdinal(fileName, 0, pkgName, 0, pkgNameLen) == 0)
                    {
                        pMatching = package.Key;
                        nMaxLength = pkgNameLen;
                    }
                }
            }

            return pMatching;
        }

        private CASC_ROOT_ENTRY_MNDX FindMNDXInfo(string path, int dwPackage)
        {
            MNDXSearchResult result = new MNDXSearchResult()
            {
                SearchMask = path.Substring(Packages[dwPackage].Length + 1).ToLower()
            };
            MARFileNameDB marFile1 = MarFiles[1];

            if (marFile1.FindFileInDatabase(result))
            {
                var pRootEntry = mndxRootEntriesValid[result.FileNameIndex];

                while ((pRootEntry.Flags & 0x00FFFFFF) != dwPackage)
                {
                    // The highest bit serves as a terminator if set
                    if ((pRootEntry.Flags & 0x80000000) != 0)
                        throw new Exception("File not found!");

                    pRootEntry = pRootEntry.Next;
                }

                // Give the root entry pointer to the caller
                return pRootEntry;
            }

            throw new Exception("File not found!");
        }

        private CASC_ROOT_ENTRY_MNDX FindMNDXInfo2(string path, int dwPackage)
        {
            MNDXSearchResult result = new MNDXSearchResult()
            {
                SearchMask = path
            };
            MARFileNameDB marFile2 = MarFiles[2];

            if (marFile2.FindFileInDatabase(result))
            {
                var pRootEntry = mndxRootEntries[result.FileNameIndex];

                while ((pRootEntry.Flags & 0x00FFFFFF) != dwPackage)
                {
                    // The highest bit serves as a terminator if set
                    //if ((pRootEntry.Flags & 0x80000000) != 0)
                    //    throw new Exception("File not found!");

                    pRootEntry = pRootEntry.Next;
                }

                // Give the root entry pointer to the caller
                return pRootEntry;
            }

            throw new Exception("File not found!");
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {
            worker?.ReportProgress(0, "Loading \"listfile\"...");

            Logger.WriteLine("MNDXRootHandler: loading file names...");

            //MNDXSearchResult result = new MNDXSearchResult();

            //MARFileNameDB marFile0 = MarFiles[0];

            Regex regex1 = new Regex("\\w{4}(?=\\.(storm|sc2)data)", RegexOptions.Compiled);
            Regex regex2 = new Regex("\\w{4}(?=\\.(storm|sc2)assets)", RegexOptions.Compiled);

            foreach (var result in MarFiles[0].EnumerateFiles())
            {
                Packages.Add(result.FileNameIndex, result.FoundPath);

                Match match1 = regex1.Match(result.FoundPath);
                Match match2 = regex2.Match(result.FoundPath);

                if (match1.Success || match2.Success)
                {
                    var localeStr = match1.Success ? match1.Value : match2.Value;

                    if (!Enum.TryParse(localeStr, true, out LocaleFlags locale))
                        locale = LocaleFlags.All;

                    PackagesLocale.Add(result.FileNameIndex, locale);
                }
                else
                    PackagesLocale.Add(result.FileNameIndex, LocaleFlags.All);
            }

            //MNDXSearchResult result2 = new MNDXSearchResult();

            //MARFileNameDB marFile2 = MarFiles[2];

            //result.SetSearchPath("mods/heroes.stormmod/base.stormassets/Assets/Sounds/Ambient_3D/Amb_3D_Birds_FlyAway01.ogg");
            //bool res = MarFiles[0].FindFileInDatabase(result);
            //result.SetSearchPath("mods/heroes.stormmod/base.stormassets/Assets/Textures/tyrael_spec.dds");
            //bool res = MarFiles[1].FindFileInDatabase(result);

            //int pkg = FindMNDXPackage("mods/heroes.stormmod/eses.stormassets/localizeddata/sounds/vo/tyrael_ping_defendthing00.ogg");

            //var info1 = FindMNDXInfo("mods/heroes.stormmod/eses.stormassets/localizeddata/sounds/vo/tyrael_ping_defendthing00.ogg", pkg);

            //var info2 = FindMNDXInfo2("mods/heroes.stormmod/eses.stormassets/localizeddata/sounds/vo/tyrael_ping_defendthing00.ogg", pkg);

            //var info3 = FindMNDXInfo2("mods/heroes.stormmod/eses.stormassets/LocalizedData/Sounds/VO/Tyrael_Ping_DefendThing00.ogg", pkg);

            int i = 0;

            foreach (var result in MarFiles[2].EnumerateFiles())
            {
                string file = result.FoundPath;

                ulong fileHash = Hasher.ComputeHash(file);

                CascFile.Files[fileHash] = new CascFile(fileHash, file);

                RootEntry entry = new RootEntry();

                int package = FindMNDXPackage(file);
                entry.LocaleFlags = PackagesLocale[package];
                entry.ContentFlags = ContentFlags.None;
                entry.cKey = FindMNDXInfo(file, package).cKey;
                mndxData[fileHash] = entry;

                //Console.WriteLine("{0:X8} - {1:X8} - {2} - {3}", result2.FileNameIndex, root.Flags, root.EncodingKey.ToHexString(), file);

                worker?.ReportProgress((int)(++i / (float)MarFiles[2].NumFiles * 100));
            }

            //var sorted = data.OrderBy(e => e.Key);
            //foreach (var e in sorted)
            //    Console.WriteLine("{0:X8} - {1:X8} - {2}", e.Key, e.Value.Flags, e.Value.EncodingKey.ToHexString());

            Logger.WriteLine("MNDXRootHandler: loaded {0} file names", i);
        }

        protected override CascFolder CreateStorageTree()
        {
            var root = new CascFolder("root");

            CountSelect = 0;

            foreach (var entry in mndxData)
            {
                if ((entry.Value.LocaleFlags & Locale) == 0)
                    continue;

                CreateSubTree(root, entry.Key, CascFile.Files[entry.Key].FullName);
                CountSelect++;
            }

            return root;
        }

        public override void Clear()
        {
            mndxData.Clear();
            mndxRootEntries.Clear();
            mndxRootEntriesValid.Clear();
            Packages.Clear();
            PackagesLocale.Clear();
            Root.Files.Clear();
            Root.Folders.Clear();
            CascFile.Files.Clear();
        }

        public override void Dump(EncodingHandler encodingHandler = null)
        {

        }
    }

    class MARFileNameDB
    {
        private const int CASC_MAR_SIGNATURE = 0x0052414d;           // 'MAR\0'

        private TSparseArray Struct68_00;
        private TSparseArray FileNameIndexes;
        private TSparseArray Struct68_D0;
        private byte[] FrgmDist_LoBits;
        private TBitEntryArray FrgmDist_HiBits;
        private TNameIndexStruct IndexStruct_174;
        private MARFileNameDB NextDB;
        private NAME_FRAG[] NameFragTable;
        private int NameFragIndexMask;
        private int field_214;

        public int NumFiles => FileNameIndexes.ValidItemCount;

        public MARFileNameDB(BinaryReader reader, bool next = false)
        {
            if (!next && reader.ReadInt32() != CASC_MAR_SIGNATURE)
                throw new Exception("invalid MAR file");

            Struct68_00 = new TSparseArray(reader);
            FileNameIndexes = new TSparseArray(reader);
            Struct68_D0 = new TSparseArray(reader);
            FrgmDist_LoBits = reader.ReadArray<byte>();
            FrgmDist_HiBits = new TBitEntryArray(reader);
            IndexStruct_174 = new TNameIndexStruct(reader);

            if (Struct68_D0.ValidItemCount != 0 && IndexStruct_174.Count == 0)
            {
                NextDB = new MARFileNameDB(reader, true);
            }

            NameFragTable = reader.ReadArray<NAME_FRAG>();

            NameFragIndexMask = NameFragTable.Length - 1;

            field_214 = reader.ReadInt32();

            int dwBitMask = reader.ReadInt32();
        }

        bool CheckNextPathFragment(MNDXSearchResult pStruct1C)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;
            int CollisionIndex;
            int NameFragIndex;
            int SaveCharIndex;
            int HiBitsIndex;
            int FragOffs;

            // Calculate index of the next name fragment in the name fragment table
            NameFragIndex = ((pStruct40.ItemIndex << 0x05) ^ pStruct40.ItemIndex ^ pStruct1C.SearchMask[pStruct40.CharIndex]) & NameFragIndexMask;

            // Does the hash value match?
            if (NameFragTable[NameFragIndex].ItemIndex == pStruct40.ItemIndex)
            {
                // Check if there is single character match
                if (IsSingleCharMatch(NameFragTable, NameFragIndex))
                {
                    pStruct40.ItemIndex = NameFragTable[NameFragIndex].NextIndex;
                    pStruct40.CharIndex++;
                    return true;
                }

                // Check if there is a name fragment match
                if (NextDB != null)
                {
                    if (!NextDB.sub_1957B80(pStruct1C, NameFragTable[NameFragIndex].FragOffs))
                        return false;
                }
                else
                {
                    if (!IndexStruct_174.CheckNameFragment(pStruct1C, NameFragTable[NameFragIndex].FragOffs))
                        return false;
                }

                pStruct40.ItemIndex = NameFragTable[NameFragIndex].NextIndex;
                return true;
            }

            //
            // Conflict: Multiple hashes give the same table index
            //

            // HOTS: 1957A0E
            CollisionIndex = Struct68_00.sub_1959CB0(pStruct40.ItemIndex) + 1;
            if (!Struct68_00.Contains(CollisionIndex))
                return false;

            pStruct40.ItemIndex = (CollisionIndex - pStruct40.ItemIndex - 1);
            HiBitsIndex = -1;

            // HOTS: 1957A41:
            do
            {
                // HOTS: 1957A41
                // Check if the low 8 bits if the fragment offset contain a single character
                // or an offset to a name fragment 
                if (Struct68_D0.Contains(pStruct40.ItemIndex))
                {
                    if (HiBitsIndex == -1)
                    {
                        // HOTS: 1957A6C
                        HiBitsIndex = Struct68_D0.GetItemValue(pStruct40.ItemIndex);
                    }
                    else
                    {
                        // HOTS: 1957A7F
                        HiBitsIndex++;
                    }

                    // HOTS: 1957A83
                    SaveCharIndex = pStruct40.CharIndex;

                    // Get the name fragment offset as combined value from lower 8 bits and upper bits
                    FragOffs = GetNameFragmentOffsetEx(pStruct40.ItemIndex, HiBitsIndex);

                    // Compare the string with the fragment name database
                    if (NextDB != null)
                    {
                        // HOTS: 1957AEC
                        if (NextDB.sub_1957B80(pStruct1C, FragOffs))
                            return true;
                    }
                    else
                    {
                        // HOTS: 1957AF7
                        if (IndexStruct_174.CheckNameFragment(pStruct1C, FragOffs))
                            return true;
                    }

                    // HOTS: 1957B0E
                    // If there was partial match with the fragment, end the search
                    if (pStruct40.CharIndex != SaveCharIndex)
                        return false;
                }
                else
                {
                    // HOTS: 1957B1C
                    if (FrgmDist_LoBits[pStruct40.ItemIndex] == pStruct1C.SearchMask[pStruct40.CharIndex])
                    {
                        pStruct40.CharIndex++;
                        return true;
                    }
                }

                // HOTS: 1957B32
                pStruct40.ItemIndex++;
                CollisionIndex++;
            }
            while (Struct68_00.Contains(CollisionIndex));
            return false;
        }

        private bool sub_1957B80(MNDXSearchResult pStruct1C, int dwKey)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;
            NAME_FRAG pNameEntry;
            int FragOffs;
            int eax, edi;

            edi = dwKey;

            // HOTS: 1957B95
            for (; ; )
            {
                pNameEntry = NameFragTable[(edi & NameFragIndexMask)];
                if (edi == pNameEntry.NextIndex)
                {
                    // HOTS: 01957BB4
                    if ((pNameEntry.FragOffs & 0xFFFFFF00) != 0xFFFFFF00)
                    {
                        // HOTS: 1957BC7
                        if (NextDB != null)
                        {
                            // HOTS: 1957BD3
                            if (!NextDB.sub_1957B80(pStruct1C, pNameEntry.FragOffs))
                                return false;
                        }
                        else
                        {
                            // HOTS: 1957BE0
                            if (!IndexStruct_174.CheckNameFragment(pStruct1C, pNameEntry.FragOffs))
                                return false;
                        }
                    }
                    else
                    {
                        // HOTS: 1957BEE
                        if (pStruct1C.SearchMask[pStruct40.CharIndex] != (byte)pNameEntry.FragOffs)
                            return false;
                        pStruct40.CharIndex++;
                    }

                    // HOTS: 1957C05
                    edi = pNameEntry.ItemIndex;
                    if (edi == 0)
                        return true;

                    if (pStruct40.CharIndex >= pStruct1C.SearchMask.Length)
                        return false;
                }
                else
                {
                    // HOTS: 1957C30
                    if (Struct68_D0.Contains(edi))
                    {
                        // HOTS: 1957C4C
                        if (NextDB != null)
                        {
                            // HOTS: 1957C58
                            FragOffs = GetNameFragmentOffset(edi);
                            if (!NextDB.sub_1957B80(pStruct1C, FragOffs))
                                return false;
                        }
                        else
                        {
                            // HOTS: 1957350
                            FragOffs = GetNameFragmentOffset(edi);
                            if (!IndexStruct_174.CheckNameFragment(pStruct1C, FragOffs))
                                return false;
                        }
                    }
                    else
                    {
                        // HOTS: 1957C8E
                        if (FrgmDist_LoBits[edi] != pStruct1C.SearchMask[pStruct40.CharIndex])
                            return false;

                        pStruct40.CharIndex++;
                    }

                    // HOTS: 1957CB2
                    if (edi <= field_214)
                        return true;

                    if (pStruct40.CharIndex >= pStruct1C.SearchMask.Length)
                        return false;

                    eax = Struct68_00.sub_1959F50(edi);
                    edi = (eax - edi - 1);
                }
            }
        }

        private void sub_1958D70(MNDXSearchResult pStruct1C, int arg_4)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;
            NAME_FRAG pNameEntry;

            // HOTS: 1958D84
            for (; ; )
            {
                pNameEntry = NameFragTable[(arg_4 & NameFragIndexMask)];
                if (arg_4 == pNameEntry.NextIndex)
                {
                    // HOTS: 1958DA6
                    if ((pNameEntry.FragOffs & 0xFFFFFF00) != 0xFFFFFF00)
                    {
                        // HOTS: 1958DBA
                        if (NextDB != null)
                        {
                            NextDB.sub_1958D70(pStruct1C, pNameEntry.FragOffs);
                        }
                        else
                        {
                            IndexStruct_174.CopyNameFragment(pStruct1C, pNameEntry.FragOffs);
                        }
                    }
                    else
                    {
                        // HOTS: 1958DE7
                        // Insert the low 8 bits to the path being built
                        pStruct40.Add((byte)(pNameEntry.FragOffs & 0xFF));
                    }

                    // HOTS: 1958E71
                    arg_4 = pNameEntry.ItemIndex;
                    if (arg_4 == 0)
                        return;
                }
                else
                {
                    // HOTS: 1958E8E
                    if (Struct68_D0.Contains(arg_4))
                    {
                        int FragOffs;

                        // HOTS: 1958EAF
                        FragOffs = GetNameFragmentOffset(arg_4);
                        if (NextDB != null)
                        {
                            NextDB.sub_1958D70(pStruct1C, FragOffs);
                        }
                        else
                        {
                            IndexStruct_174.CopyNameFragment(pStruct1C, FragOffs);
                        }
                    }
                    else
                    {
                        // HOTS: 1958F50
                        // Insert one character to the path being built
                        pStruct40.Add(FrgmDist_LoBits[arg_4]);
                    }

                    // HOTS: 1958FDE
                    if (arg_4 <= field_214)
                        return;

                    arg_4 = -1 - arg_4 + Struct68_00.sub_1959F50(arg_4);
                }
            }
        }

        private bool sub_1959010(MNDXSearchResult pStruct1C, int arg_4)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;
            NAME_FRAG pNameEntry;

            // HOTS: 1959024
            for (; ; )
            {
                pNameEntry = NameFragTable[(arg_4 & NameFragIndexMask)];
                if (arg_4 == pNameEntry.NextIndex)
                {
                    // HOTS: 1959047
                    if ((pNameEntry.FragOffs & 0xFFFFFF00) != 0xFFFFFF00)
                    {
                        // HOTS: 195905A
                        if (NextDB != null)
                        {
                            if (!NextDB.sub_1959010(pStruct1C, pNameEntry.FragOffs))
                                return false;
                        }
                        else
                        {
                            if (!IndexStruct_174.CheckAndCopyNameFragment(pStruct1C, pNameEntry.FragOffs))
                                return false;
                        }
                    }
                    else
                    {
                        // HOTS: 1959092
                        if ((byte)(pNameEntry.FragOffs & 0xFF) != pStruct1C.SearchMask[pStruct40.CharIndex])
                            return false;

                        // Insert the low 8 bits to the path being built
                        pStruct40.Add((byte)(pNameEntry.FragOffs & 0xFF));
                        pStruct40.CharIndex++;
                    }

                    // HOTS: 195912E
                    arg_4 = pNameEntry.ItemIndex;
                    if (arg_4 == 0)
                        return true;
                }
                else
                {
                    // HOTS: 1959147
                    if (Struct68_D0.Contains(arg_4))
                    {
                        int FragOffs;

                        // HOTS: 195917C
                        FragOffs = GetNameFragmentOffset(arg_4);
                        if (NextDB != null)
                        {
                            if (!NextDB.sub_1959010(pStruct1C, FragOffs))
                                return false;
                        }
                        else
                        {
                            if (!IndexStruct_174.CheckAndCopyNameFragment(pStruct1C, FragOffs))
                                return false;
                        }
                    }
                    else
                    {
                        // HOTS: 195920E
                        if (FrgmDist_LoBits[arg_4] != pStruct1C.SearchMask[pStruct40.CharIndex])
                            return false;

                        // Insert one character to the path being built
                        pStruct40.Add(FrgmDist_LoBits[arg_4]);
                        pStruct40.CharIndex++;
                    }

                    // HOTS: 19592B6
                    if (arg_4 <= field_214)
                        return true;

                    arg_4 = -1 - arg_4 + Struct68_00.sub_1959F50(arg_4);
                }

                // HOTS: 19592D5
                if (pStruct40.CharIndex >= pStruct1C.SearchMask.Length)
                    break;
            }

            sub_1958D70(pStruct1C, arg_4);
            return true;
        }

        private bool EnumerateFiles(MNDXSearchResult pStruct1C)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;

            if (pStruct40.SearchPhase == CASCSearchPhase.Finished)
                return false;

            if (pStruct40.SearchPhase != CASCSearchPhase.Searching)
            {
                // HOTS: 1959489
                pStruct40.InitSearchBuffers();

                // If the caller passed a part of the search path, we need to find that one
                while (pStruct40.CharIndex < pStruct1C.SearchMask.Length)
                {
                    if (!sub_1958B00(pStruct1C))
                    {
                        pStruct40.Finish();
                        return false;
                    }
                }

                // HOTS: 19594b0
                PATH_STOP PathStop = new PATH_STOP()
                {
                    ItemIndex = pStruct40.ItemIndex,
                    Field_4 = 0,
                    Field_8 = pStruct40.NumBytesFound,
                    Field_C = -1,
                    Field_10 = -1
                };
                pStruct40.AddPathStop(PathStop);
                pStruct40.ItemCount = 1;

                if (FileNameIndexes.Contains(pStruct40.ItemIndex))
                {
                    pStruct1C.SetFindResult(pStruct40.Result, FileNameIndexes.GetItemValue(pStruct40.ItemIndex));
                    return true;
                }
            }

            // HOTS: 1959522
            for (; ; )
            {
                // HOTS: 1959530
                if (pStruct40.ItemCount == pStruct40.NumPathStops)
                {
                    PATH_STOP pLastStop;
                    int CollisionIndex;

                    pLastStop = pStruct40.GetPathStop(pStruct40.NumPathStops - 1);
                    CollisionIndex = Struct68_00.sub_1959CB0(pLastStop.ItemIndex) + 1;

                    // Insert a new structure
                    PATH_STOP PathStop = new PATH_STOP()
                    {
                        ItemIndex = CollisionIndex - pLastStop.ItemIndex - 1,
                        Field_4 = CollisionIndex,
                        Field_8 = 0,
                        Field_C = -1,
                        Field_10 = -1
                    };
                    pStruct40.AddPathStop(PathStop);
                }

                // HOTS: 19595BD
                PATH_STOP pPathStop = pStruct40.GetPathStop(pStruct40.ItemCount);

                // HOTS: 19595CC
                if (Struct68_00.Contains(pPathStop.Field_4++))
                {
                    // HOTS: 19595F2
                    pStruct40.ItemCount++;

                    if (Struct68_D0.Contains(pPathStop.ItemIndex))
                    {
                        // HOTS: 1959617
                        if (pPathStop.Field_C == -1)
                            pPathStop.Field_C = Struct68_D0.GetItemValue(pPathStop.ItemIndex);
                        else
                            pPathStop.Field_C++;

                        // HOTS: 1959630
                        int FragOffs = GetNameFragmentOffsetEx(pPathStop.ItemIndex, pPathStop.Field_C);
                        if (NextDB != null)
                        {
                            // HOTS: 1959649
                            NextDB.sub_1958D70(pStruct1C, FragOffs);
                        }
                        else
                        {
                            // HOTS: 1959654
                            IndexStruct_174.CopyNameFragment(pStruct1C, FragOffs);
                        }
                    }
                    else
                    {
                        // HOTS: 1959665
                        // Insert one character to the path being built
                        pStruct40.Add(FrgmDist_LoBits[pPathStop.ItemIndex]);
                    }

                    // HOTS: 19596AE
                    pPathStop.Field_8 = pStruct40.NumBytesFound;

                    // HOTS: 19596b6
                    if (FileNameIndexes.Contains(pPathStop.ItemIndex))
                    {
                        // HOTS: 19596D1
                        if (pPathStop.Field_10 == -1)
                        {
                            // HOTS: 19596D9
                            pPathStop.Field_10 = FileNameIndexes.GetItemValue(pPathStop.ItemIndex);
                        }
                        else
                        {
                            pPathStop.Field_10++;
                        }

                        // HOTS: 1959755
                        pStruct1C.SetFindResult(pStruct40.Result, pPathStop.Field_10);
                        return true;
                    }
                }
                else
                {
                    // HOTS: 19596E9
                    if (pStruct40.ItemCount == 1)
                    {
                        pStruct40.Finish();
                        return false;
                    }

                    // HOTS: 19596F5
                    pPathStop = pStruct40.GetPathStop(pStruct40.ItemCount - 1);
                    pPathStop.ItemIndex++;

                    pPathStop = pStruct40.GetPathStop(pStruct40.ItemCount - 2);
                    int edi = pPathStop.Field_8;

                    // HOTS: 1959749
                    pStruct40.RemoveRange(edi);
                    pStruct40.ItemCount--;
                }
            }
        }

        private bool sub_1958B00(MNDXSearchResult pStruct1C)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;
            byte[] pbPathName = Encoding.ASCII.GetBytes(pStruct1C.SearchMask);
            int CollisionIndex;
            int FragmentOffset;
            int SaveCharIndex;
            int ItemIndex;
            int FragOffs;
            int var_4;

            ItemIndex = pbPathName[pStruct40.CharIndex] ^ (pStruct40.ItemIndex << 0x05) ^ pStruct40.ItemIndex;
            ItemIndex = ItemIndex & NameFragIndexMask;
            if (pStruct40.ItemIndex == NameFragTable[ItemIndex].ItemIndex)
            {
                // HOTS: 1958B45
                FragmentOffset = NameFragTable[ItemIndex].FragOffs;
                if ((FragmentOffset & 0xFFFFFF00) == 0xFFFFFF00)
                {
                    // HOTS: 1958B88
                    pStruct40.Add((byte)FragmentOffset);
                    pStruct40.ItemIndex = NameFragTable[ItemIndex].NextIndex;
                    pStruct40.CharIndex++;
                    return true;
                }

                // HOTS: 1958B59
                if (NextDB != null)
                {
                    if (!NextDB.sub_1959010(pStruct1C, FragmentOffset))
                        return false;
                }
                else
                {
                    if (!IndexStruct_174.CheckAndCopyNameFragment(pStruct1C, FragmentOffset))
                        return false;
                }

                // HOTS: 1958BCA
                pStruct40.ItemIndex = NameFragTable[ItemIndex].NextIndex;
                return true;
            }

            // HOTS: 1958BE5
            CollisionIndex = Struct68_00.sub_1959CB0(pStruct40.ItemIndex) + 1;
            if (!Struct68_00.Contains(CollisionIndex))
                return false;

            pStruct40.ItemIndex = (CollisionIndex - pStruct40.ItemIndex - 1);
            var_4 = -1;

            // HOTS: 1958C20
            for (; ; )
            {
                if (Struct68_D0.Contains(pStruct40.ItemIndex))
                {
                    // HOTS: 1958C0E
                    if (var_4 == -1)
                    {
                        // HOTS: 1958C4B
                        var_4 = Struct68_D0.GetItemValue(pStruct40.ItemIndex);
                    }
                    else
                    {
                        var_4++;
                    }

                    // HOTS: 1958C62
                    SaveCharIndex = pStruct40.CharIndex;

                    FragOffs = GetNameFragmentOffsetEx(pStruct40.ItemIndex, var_4);
                    if (NextDB != null)
                    {
                        // HOTS: 1958CCB
                        if (NextDB.sub_1959010(pStruct1C, FragOffs))
                            return true;
                    }
                    else
                    {
                        // HOTS: 1958CD6
                        if (IndexStruct_174.CheckAndCopyNameFragment(pStruct1C, FragOffs))
                            return true;
                    }

                    // HOTS: 1958CED
                    if (SaveCharIndex != pStruct40.CharIndex)
                        return false;
                }
                else
                {
                    // HOTS: 1958CFB
                    if (FrgmDist_LoBits[pStruct40.ItemIndex] == pStruct1C.SearchMask[pStruct40.CharIndex])
                    {
                        // HOTS: 1958D11
                        pStruct40.Add(FrgmDist_LoBits[pStruct40.ItemIndex]);
                        pStruct40.CharIndex++;
                        return true;
                    }
                }

                // HOTS: 1958D11
                pStruct40.ItemIndex++;
                CollisionIndex++;

                if (!Struct68_00.Contains(CollisionIndex))
                    break;
            }

            return false;
        }

        public bool FindFileInDatabase(MNDXSearchResult pStruct1C)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;

            pStruct40.ItemIndex = 0;
            pStruct40.CharIndex = 0;
            pStruct40.Init();

            if (pStruct1C.SearchMask.Length > 0)
            {
                while (pStruct40.CharIndex < pStruct1C.SearchMask.Length)
                {
                    // HOTS: 01957F12
                    if (!CheckNextPathFragment(pStruct1C))
                        return false;
                }
            }

            // HOTS: 1957F26
            if (!FileNameIndexes.Contains(pStruct40.ItemIndex))
                return false;

            pStruct1C.SetFindResult(pStruct1C.SearchMask, FileNameIndexes.GetItemValue(pStruct40.ItemIndex));
            return true;
        }

        public IEnumerable<MNDXSearchResult> EnumerateFiles()
        {
            MNDXSearchResult pStruct1C = new MNDXSearchResult();

            while (EnumerateFiles(pStruct1C))
                yield return pStruct1C;
        }

        private int GetNameFragmentOffsetEx(int LoBitsIndex, int HiBitsIndex)
        {
            return (FrgmDist_HiBits[HiBitsIndex] << 0x08) | FrgmDist_LoBits[LoBitsIndex];
        }

        private int GetNameFragmentOffset(int LoBitsIndex)
        {
            return GetNameFragmentOffsetEx(LoBitsIndex, Struct68_D0.GetItemValue(LoBitsIndex));
        }

        private bool IsSingleCharMatch(NAME_FRAG[] Table, int ItemIndex)
        {
            return ((Table[ItemIndex].FragOffs & 0xFFFFFF00) == 0xFFFFFF00);
        }

        private int GetNumberOfSetBits(int Value32)
        {
            Value32 = ((Value32 >> 1) & 0x55555555) + (Value32 & 0x55555555);
            Value32 = ((Value32 >> 2) & 0x33333333) + (Value32 & 0x33333333);
            Value32 = ((Value32 >> 4) & 0x0F0F0F0F) + (Value32 & 0x0F0F0F0F);

            return (Value32 * 0x01010101);
        }
    }

    public class TBitEntryArray : List<int>
    {
        private int BitsPerEntry;
        private int EntryBitMask;
        private int TotalEntries;

        public new int this[int index]
        {
            get
            {
                int dwItemIndex = (index * BitsPerEntry) >> 0x05;
                int dwStartBit = (index * BitsPerEntry) & 0x1F;
                int dwEndBit = dwStartBit + BitsPerEntry;
                int dwResult;

                // If the end bit index is greater than 32,
                // we also need to load from the next 32-bit item
                if (dwEndBit > 0x20)
                {
                    dwResult = (base[dwItemIndex + 1] << (0x20 - dwStartBit)) | (int)((uint)base[dwItemIndex] >> dwStartBit);
                }
                else
                {
                    dwResult = base[dwItemIndex] >> dwStartBit;
                }

                // Now we also need to mask the result by the bit mask
                return dwResult & EntryBitMask;
            }
        }

        public TBitEntryArray(BinaryReader reader) : base(reader.ReadArray<int>())
        {
            BitsPerEntry = reader.ReadInt32();
            EntryBitMask = reader.ReadInt32();
            TotalEntries = (int)reader.ReadInt64();
        }
    }

    public class TSparseArray
    {
        private int[] ItemBits;                    // Bit array for each item (1 = item is present)
        private TRIPLET[] BaseValues;              // Array of base values for item indexes >= 0x200
        private int[] ArrayDwords_38;
        private int[] ArrayDwords_50;

        public int TotalItemCount { get; private set; } // Total number of items in the array
        public int ValidItemCount { get; private set; } // Number of present items in the array

        public TSparseArray(BinaryReader reader)
        {
            ItemBits = reader.ReadArray<int>();
            TotalItemCount = reader.ReadInt32();
            ValidItemCount = reader.ReadInt32();
            BaseValues = reader.ReadArray<TRIPLET>();
            ArrayDwords_38 = reader.ReadArray<int>();
            ArrayDwords_50 = reader.ReadArray<int>();
        }

        public bool Contains(int index)
        {
            return (ItemBits[index >> 0x05] & (1 << (index & 0x1F))) != 0;
        }

        public int GetItemBit(int index)
        {
            return ItemBits[index];
        }

        public TRIPLET GetBaseValue(int index)
        {
            return BaseValues[index];
        }

        public int GetArrayValue_38(int index)
        {
            return ArrayDwords_38[index];
        }

        public int GetArrayValue_50(int index)
        {
            return ArrayDwords_50[index];
        }

        public int GetItemValue(int index)
        {
            TRIPLET pTriplet;
            int DwordIndex;
            int BaseValue;
            int BitMask;

            // 
            // Divide the low-8-bits index to four parts:
            //
            // |-----------------------|---|------------|
            // |       A (23 bits)     | B |      C     |
            // |-----------------------|---|------------|
            //
            // A (23-bits): Index to the table (60 bits per entry)
            //
            //    Layout of the table entry:
            //   |--------------------------------|-------|--------|--------|---------|---------|---------|---------|-----|
            //   |  Base Value                    | val[0]| val[1] | val[2] | val[3]  | val[4]  | val[5]  | val[6]  |  -  |
            //   |  32 bits                       | 7 bits| 8 bits | 8 bits | 9 bits  | 9 bits  | 9 bits  | 9 bits  |5bits|
            //   |--------------------------------|-------|--------|--------|---------|---------|---------|---------|-----|
            //
            // B (3 bits) : Index of the variable-bit value in the array (val[#], see above)
            //
            // C (32 bits): Number of bits to be checked (up to 0x3F bits).
            //              Number of set bits is then added to the values obtained from A and B

            // Upper 23 bits contain index to the table
            pTriplet = BaseValues[index >> 0x09];
            BaseValue = pTriplet.BaseValue;

            // Next 3 bits contain the index to the VBR
            switch (((index >> 0x06) & 0x07) - 1)
            {
                case 0:     // Add the 1st value (7 bits)
                    BaseValue += (pTriplet.Value2 & 0x7F);
                    break;

                case 1:     // Add the 2nd value (8 bits)
                    BaseValue += (pTriplet.Value2 >> 0x07) & 0xFF;
                    break;

                case 2:     // Add the 3rd value (8 bits)
                    BaseValue += (pTriplet.Value2 >> 0x0F) & 0xFF;
                    break;

                case 3:     // Add the 4th value (9 bits)
                    BaseValue += (pTriplet.Value2 >> 0x17) & 0x1FF;
                    break;

                case 4:     // Add the 5th value (9 bits)
                    BaseValue += (pTriplet.Value3 & 0x1FF);
                    break;

                case 5:     // Add the 6th value (9 bits)
                    BaseValue += (pTriplet.Value3 >> 0x09) & 0x1FF;
                    break;

                case 6:     // Add the 7th value (9 bits)
                    BaseValue += (pTriplet.Value3 >> 0x12) & 0x1FF;
                    break;
            }

            //
            // Take the upper 27 bits as an index to DWORD array, take lower 5 bits
            // as number of bits to mask. Then calculate number of set bits in the value
            // masked value.
            //

            // Get the index into the array of DWORDs
            DwordIndex = (index >> 0x05);

            // Add number of set bits in the masked value up to 0x3F bits
            if ((index & 0x20) != 0)
                BaseValue += GetNumbrOfSetBits32(ItemBits[DwordIndex - 1]);

            BitMask = (1 << (index & 0x1F)) - 1;
            return BaseValue + GetNumbrOfSetBits32(ItemBits[DwordIndex] & BitMask);
        }

        private int GetNumberOfSetBits(int Value32)
        {
            Value32 = ((Value32 >> 1) & 0x55555555) + (Value32 & 0x55555555);
            Value32 = ((Value32 >> 2) & 0x33333333) + (Value32 & 0x33333333);
            Value32 = ((Value32 >> 4) & 0x0F0F0F0F) + (Value32 & 0x0F0F0F0F);

            return (Value32 * 0x01010101);
        }

        private int GetNumbrOfSetBits32(int x)
        {
            return (GetNumberOfSetBits(x) >> 0x18);
        }

        public int sub_1959CB0(int index)
        {
            TRIPLET pTriplet;
            int dwKeyShifted = (index >> 9);
            int eax, ebx, ecx, edx, esi, edi;

            // If lower 9 is zero
            edx = index;
            if ((edx & 0x1FF) == 0)
                return this.GetArrayValue_38(dwKeyShifted);

            eax = this.GetArrayValue_38(dwKeyShifted) >> 9;
            esi = (this.GetArrayValue_38(dwKeyShifted + 1) + 0x1FF) >> 9;
            index = esi;

            if ((eax + 0x0A) >= esi)
            {
                // HOTS: 1959CF7
                int i = eax + 1;
                pTriplet = this.GetBaseValue(i);
                i++;
                edi = (eax << 0x09);
                ebx = edi - pTriplet.BaseValue + 0x200;
                while (edx >= ebx)
                {
                    // HOTS: 1959D14
                    edi += 0x200;
                    pTriplet = this.GetBaseValue(i);

                    ebx = edi - pTriplet.BaseValue + 0x200;
                    eax++;
                    i++;
                }
            }
            else
            {
                // HOTS: 1959D2E
                while ((eax + 1) < esi)
                {
                    // HOTS: 1959D38
                    // ecx = Struct68_00.BaseValues.TripletArray;
                    esi = (esi + eax) >> 1;
                    ebx = (esi << 0x09) - this.GetBaseValue(esi).BaseValue;
                    if (edx < ebx)
                    {
                        // HOTS: 01959D4B
                        index = esi;
                    }
                    else
                    {
                        // HOTS: 1959D50
                        eax = esi;
                        esi = index;
                    }
                }
            }

            // HOTS: 1959D5F
            pTriplet = this.GetBaseValue(eax);
            edx += pTriplet.BaseValue - (eax << 0x09);
            edi = (eax << 4);

            eax = pTriplet.Value2;
            ecx = (eax >> 0x17);
            ebx = 0x100 - ecx;
            if (edx < ebx)
            {
                // HOTS: 1959D8C
                ecx = ((eax >> 0x07) & 0xFF);
                esi = 0x80 - ecx;
                if (edx < esi)
                {
                    // HOTS: 01959DA2
                    eax = eax & 0x7F;
                    ecx = 0x40 - eax;
                    if (edx >= ecx)
                    {
                        // HOTS: 01959DB7
                        edi += 2;
                        edx = edx + eax - 0x40;
                    }
                }
                else
                {
                    // HOTS: 1959DC0
                    eax = (eax >> 0x0F) & 0xFF;
                    esi = 0xC0 - eax;
                    if (edx < esi)
                    {
                        // HOTS: 1959DD3
                        edi += 4;
                        edx = edx + ecx - 0x80;
                    }
                    else
                    {
                        // HOTS: 1959DD3
                        edi += 6;
                        edx = edx + eax - 0xC0;
                    }
                }
            }
            else
            {
                // HOTS: 1959DE8
                esi = pTriplet.Value3;
                eax = ((esi >> 0x09) & 0x1FF);
                ebx = 0x180 - eax;
                if (edx < ebx)
                {
                    // HOTS: 01959E00
                    esi = esi & 0x1FF;
                    eax = (0x140 - esi);
                    if (edx < eax)
                    {
                        // HOTS: 1959E11
                        edi = edi + 8;
                        edx = edx + ecx - 0x100;
                    }
                    else
                    {
                        // HOTS: 1959E1D
                        edi = edi + 0x0A;
                        edx = edx + esi - 0x140;
                    }
                }
                else
                {
                    // HOTS: 1959E29
                    esi = (esi >> 0x12) & 0x1FF;
                    ecx = (0x1C0 - esi);
                    if (edx < ecx)
                    {
                        // HOTS: 1959E3D
                        edi = edi + 0x0C;
                        edx = edx + eax - 0x180;
                    }
                    else
                    {
                        // HOTS: 1959E49
                        edi = edi + 0x0E;
                        edx = edx + esi - 0x1C0;
                    }
                }
            }

            // HOTS: 1959E53:
            // Calculate the number of bits set in the value of "ecx"
            ecx = ~this.GetItemBit(edi);
            eax = GetNumberOfSetBits(ecx);
            esi = eax >> 0x18;

            if (edx >= esi)
            {
                // HOTS: 1959ea4
                ecx = ~this.GetItemBit(++edi);
                edx = edx - esi;
                eax = GetNumberOfSetBits(ecx);
            }

            // HOTS: 1959eea 
            // ESI gets the number of set bits in the lower 16 bits of ECX
            esi = (eax >> 0x08) & 0xFF;
            edi = edi << 0x05;
            if (edx < esi)
            {
                // HOTS: 1959EFC
                eax = eax & 0xFF;
                if (edx >= eax)
                {
                    // HOTS: 1959F05
                    ecx >>= 0x08;
                    edi += 0x08;
                    edx -= eax;
                }
            }
            else
            {
                // HOTS: 1959F0D
                eax = (eax >> 0x10) & 0xFF;
                if (edx < eax)
                {
                    // HOTS: 1959F19
                    ecx >>= 0x10;
                    edi += 0x10;
                    edx -= esi;
                }
                else
                {
                    // HOTS: 1959F23
                    ecx >>= 0x18;
                    edi += 0x18;
                    edx -= eax;
                }
            }

            // HOTS: 1959f2b
            edx = edx << 0x08;
            ecx = ecx & 0xFF;

            // BUGBUG: Possible buffer overflow here. Happens when dwItemIndex >= 0x9C.
            // The same happens in Heroes of the Storm (build 29049), so I am not sure
            // if this is a bug or a case that never happens
            Debug.Assert((ecx + edx) < table_1BA1818.Length);
            return table_1BA1818[ecx + edx] + edi;
        }

        public int sub_1959F50(int index)
        {
            TRIPLET pTriplet;
            int eax, ebx, ecx, edx, esi, edi;

            edx = index;
            eax = index >> 0x09;
            if ((index & 0x1FF) == 0)
                return this.GetArrayValue_50(eax);

            int item0 = this.GetArrayValue_50(eax);
            int item1 = this.GetArrayValue_50(eax + 1);
            eax = (item0 >> 0x09);
            edi = (item1 + 0x1FF) >> 0x09;

            if ((eax + 0x0A) > edi)
            {
                // HOTS: 01959F94
                int i = eax + 1;
                pTriplet = this.GetBaseValue(i);
                i++;
                while (edx >= pTriplet.BaseValue)
                {
                    // HOTS: 1959FA3
                    pTriplet = this.GetBaseValue(i);
                    eax++;
                    i++;
                }
            }
            else
            {
                // Binary search
                // HOTS: 1959FAD
                if (eax + 1 < edi)
                {
                    // HOTS: 1959FB4
                    esi = (edi + eax) >> 1;
                    if (edx < this.GetBaseValue(esi).BaseValue)
                    {
                        // HOTS: 1959FC4
                        edi = esi;
                    }
                    else
                    {
                        // HOTS: 1959FC8
                        eax = esi;
                    }
                }
            }

            // HOTS: 1959FD4
            pTriplet = this.GetBaseValue(eax);
            edx = edx - pTriplet.BaseValue;
            edi = eax << 0x04;
            eax = pTriplet.Value2;
            ebx = (eax >> 0x17);
            if (edx < ebx)
            {
                // HOTS: 1959FF1
                esi = (eax >> 0x07) & 0xFF;
                if (edx < esi)
                {
                    // HOTS: 0195A000
                    eax = eax & 0x7F;
                    if (edx >= eax)
                    {
                        // HOTS: 195A007
                        edi = edi + 2;
                        edx = edx - eax;
                    }
                }
                else
                {
                    // HOTS: 195A00E
                    eax = (eax >> 0x0F) & 0xFF;
                    if (edx < eax)
                    {
                        // HOTS: 195A01A
                        edi += 4;
                        edx = edx - esi;
                    }
                    else
                    {
                        // HOTS: 195A01F
                        edi += 6;
                        edx = edx - eax;
                    }
                }
            }
            else
            {
                // HOTS: 195A026
                esi = pTriplet.Value3;
                eax = (pTriplet.Value3 >> 0x09) & 0x1FF;
                if (edx < eax)
                {
                    // HOTS: 195A037
                    esi = esi & 0x1FF;
                    if (edx < esi)
                    {
                        // HOTS: 195A041
                        edi = edi + 8;
                        edx = edx - ebx;
                    }
                    else
                    {
                        // HOTS: 195A048
                        edi = edi + 0x0A;
                        edx = edx - esi;
                    }
                }
                else
                {
                    // HOTS: 195A04D
                    esi = (esi >> 0x12) & 0x1FF;
                    if (edx < esi)
                    {
                        // HOTS: 195A05A
                        edi = edi + 0x0C;
                        edx = edx - eax;
                    }
                    else
                    {
                        // HOTS: 195A061
                        edi = edi + 0x0E;
                        edx = edx - esi;
                    }
                }
            }

            // HOTS: 195A066
            esi = this.GetItemBit(edi);
            eax = GetNumberOfSetBits(esi);
            ecx = eax >> 0x18;

            if (edx >= ecx)
            {
                // HOTS: 195A0B2
                esi = this.GetItemBit(++edi);
                edx = edx - ecx;
                eax = GetNumberOfSetBits(esi);
            }

            // HOTS: 195A0F6
            ecx = (eax >> 0x08) & 0xFF;

            edi = (edi << 0x05);
            if (edx < ecx)
            {
                // HOTS: 195A111
                eax = eax & 0xFF;
                if (edx >= eax)
                {
                    // HOTS: 195A111
                    edi = edi + 0x08;
                    esi = esi >> 0x08;
                    edx = edx - eax;
                }
            }
            else
            {
                // HOTS: 195A119
                eax = (eax >> 0x10) & 0xFF;
                if (edx < eax)
                {
                    // HOTS: 195A125
                    esi = esi >> 0x10;
                    edi = edi + 0x10;
                    edx = edx - ecx;
                }
                else
                {
                    // HOTS: 195A12F
                    esi = esi >> 0x18;
                    edi = edi + 0x18;
                    edx = edx - eax;
                }
            }

            esi = esi & 0xFF;
            edx = edx << 0x08;

            // BUGBUG: Potential buffer overflow
            // Happens in Heroes of the Storm when arg_0 == 0x5B
            Debug.Assert((esi + edx) < table_1BA1818.Length);
            return table_1BA1818[esi + edx] + edi;
        }

        private static readonly byte[] table_1BA1818 =
        {
            0x07, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x05, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x06, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x05, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x07, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x05, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x06, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x05, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x04, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00, 0x03, 0x00, 0x01, 0x00, 0x02, 0x00, 0x01, 0x00,
            0x07, 0x07, 0x07, 0x01, 0x07, 0x02, 0x02, 0x01, 0x07, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x05, 0x05, 0x01, 0x05, 0x02, 0x02, 0x01, 0x05, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x05, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x06, 0x06, 0x01, 0x06, 0x02, 0x02, 0x01, 0x06, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x06, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x06, 0x05, 0x05, 0x01, 0x05, 0x02, 0x02, 0x01, 0x05, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x05, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x07, 0x07, 0x01, 0x07, 0x02, 0x02, 0x01, 0x07, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x05, 0x05, 0x01, 0x05, 0x02, 0x02, 0x01, 0x05, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x05, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x06, 0x06, 0x01, 0x06, 0x02, 0x02, 0x01, 0x06, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x06, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x06, 0x05, 0x05, 0x01, 0x05, 0x02, 0x02, 0x01, 0x05, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x05, 0x04, 0x04, 0x01, 0x04, 0x02, 0x02, 0x01, 0x04, 0x03, 0x03, 0x01, 0x03, 0x02, 0x02, 0x01,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x02, 0x07, 0x07, 0x07, 0x03, 0x07, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x04, 0x07, 0x04, 0x04, 0x02, 0x07, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x02, 0x07, 0x05, 0x05, 0x03, 0x05, 0x03, 0x03, 0x02,
            0x07, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x02, 0x05, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x02, 0x07, 0x06, 0x06, 0x03, 0x06, 0x03, 0x03, 0x02,
            0x07, 0x06, 0x06, 0x04, 0x06, 0x04, 0x04, 0x02, 0x06, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x02, 0x06, 0x05, 0x05, 0x03, 0x05, 0x03, 0x03, 0x02,
            0x06, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x02, 0x05, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x02, 0x07, 0x07, 0x07, 0x03, 0x07, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x04, 0x07, 0x04, 0x04, 0x02, 0x07, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x02, 0x07, 0x05, 0x05, 0x03, 0x05, 0x03, 0x03, 0x02,
            0x07, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x02, 0x05, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x02, 0x07, 0x06, 0x06, 0x03, 0x06, 0x03, 0x03, 0x02,
            0x07, 0x06, 0x06, 0x04, 0x06, 0x04, 0x04, 0x02, 0x06, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x02, 0x06, 0x05, 0x05, 0x03, 0x05, 0x03, 0x03, 0x02,
            0x06, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x02, 0x05, 0x04, 0x04, 0x03, 0x04, 0x03, 0x03, 0x02,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x04, 0x07, 0x07, 0x07, 0x04, 0x07, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05, 0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x03,
            0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x04, 0x07, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x03,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x04, 0x07, 0x06, 0x06, 0x04, 0x06, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05, 0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x03,
            0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x04, 0x06, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x04, 0x07, 0x07, 0x07, 0x04, 0x07, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05, 0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x03,
            0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x04, 0x07, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x03,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x04, 0x07, 0x06, 0x06, 0x04, 0x06, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05, 0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x03,
            0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x04, 0x06, 0x05, 0x05, 0x04, 0x05, 0x04, 0x04, 0x03,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05, 0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05, 0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05, 0x07, 0x07, 0x07, 0x05, 0x07, 0x05, 0x05, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05,
            0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05, 0x07, 0x06, 0x06, 0x05, 0x06, 0x05, 0x05, 0x04,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06, 0x07, 0x07, 0x07, 0x06, 0x07, 0x06, 0x06, 0x05,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x06,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
            0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07
        };
    }

    public class TNameIndexStruct
    {
        private byte[] NameFragments;
        private TSparseArray FragmentEnds;

        public int Count => NameFragments.Length;

        public TNameIndexStruct(BinaryReader reader)
        {
            NameFragments = reader.ReadArray<byte>();
            FragmentEnds = new TSparseArray(reader);
        }

        public bool CheckAndCopyNameFragment(MNDXSearchResult pStruct1C, int dwFragOffs)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;

            if (FragmentEnds.TotalItemCount == 0)
            {
                string szSearchMask = pStruct1C.SearchMask;

                int startPos = dwFragOffs - pStruct40.CharIndex;

                // Keep copying as long as we don't reach the end of the search mask
                while (pStruct40.CharIndex < pStruct1C.SearchMask.Length)
                {
                    // HOTS: 195A5A0
                    if (NameFragments[startPos + pStruct40.CharIndex] != szSearchMask[pStruct40.CharIndex])
                        return false;

                    // HOTS: 195A5B7
                    pStruct40.Add(NameFragments[startPos + pStruct40.CharIndex]);
                    pStruct40.CharIndex++;

                    if (NameFragments[startPos + pStruct40.CharIndex] == 0)
                        return true;
                }

                // HOTS: 195A660
                // Now we need to copy the rest of the fragment
                while (NameFragments[startPos + pStruct40.CharIndex] != 0)
                {
                    pStruct40.Add(NameFragments[startPos + pStruct40.CharIndex]);
                    startPos++;
                }
            }
            else
            {
                // Get the offset of the fragment to compare
                // HOTS: 195A6B7
                string szSearchMask = pStruct1C.SearchMask;

                // Keep copying as long as we don't reach the end of the search mask
                while (dwFragOffs < pStruct1C.SearchMask.Length)
                {
                    if (NameFragments[dwFragOffs] != szSearchMask[pStruct40.CharIndex])
                        return false;

                    pStruct40.Add(NameFragments[dwFragOffs]);
                    pStruct40.CharIndex++;

                    // Keep going as long as the given bit is not set
                    if (FragmentEnds.Contains(dwFragOffs++))
                        return true;
                }

                // Now we need to copy the rest of the fragment
                while (!FragmentEnds.Contains(dwFragOffs))
                {
                    // HOTS: 195A7A6
                    pStruct40.Add(NameFragments[dwFragOffs]);
                    dwFragOffs++;
                }
            }

            return true;
        }

        public bool CheckNameFragment(MNDXSearchResult pStruct1C, int dwFragOffs)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;

            if (FragmentEnds.TotalItemCount == 0)
            {
                // Get the offset of the fragment to compare. For convenience with pStruct40->CharIndex,
                // subtract the CharIndex from the fragment offset
                string szSearchMask = pStruct1C.SearchMask;

                int startPos = dwFragOffs - pStruct40.CharIndex;

                // Keep searching as long as the name matches with the fragment
                while (NameFragments[startPos + pStruct40.CharIndex] == szSearchMask[pStruct40.CharIndex])
                {
                    // Move to the next character
                    pStruct40.CharIndex++;

                    // Is it the end of the fragment or end of the path?
                    if (NameFragments[startPos + pStruct40.CharIndex] == 0)
                        return true;

                    if (pStruct40.CharIndex >= pStruct1C.SearchMask.Length)
                        return false;
                }

                return false;
            }
            else
            {
                // Get the offset of the fragment to compare.
                string szSearchMask = pStruct1C.SearchMask;

                // Keep searching as long as the name matches with the fragment
                while (NameFragments[dwFragOffs] == szSearchMask[pStruct40.CharIndex])
                {
                    // Move to the next character
                    pStruct40.CharIndex++;

                    // Is it the end of the fragment or end of the path?
                    if (FragmentEnds.Contains(dwFragOffs++))
                        return true;

                    if (dwFragOffs >= pStruct1C.SearchMask.Length)
                        return false;
                }

                return false;
            }
        }

        public void CopyNameFragment(MNDXSearchResult pStruct1C, int dwFragOffs)
        {
            SearchBuffer pStruct40 = pStruct1C.Buffer;

            if (FragmentEnds.TotalItemCount == 0)
            {
                while (NameFragments[dwFragOffs] != 0)
                {
                    pStruct40.Add(NameFragments[dwFragOffs++]);
                }
            }
            else
            {
                while (!FragmentEnds.Contains(dwFragOffs))
                {
                    pStruct40.Add(NameFragments[dwFragOffs++]);
                }
            }
        }
    }

    public enum CASCSearchPhase
    {
        Initializing = 0,
        Searching = 2,
        Finished = 4
    }

    public class SearchBuffer
    {
        private List<byte> SearchResult = new List<byte>();
        private List<PATH_STOP> PathStops = new List<PATH_STOP>();   // Array of path checkpoints

        public int ItemIndex { get; set; } = 0;// Current name fragment: Index to various tables
        public int CharIndex { get; set; } = 0;
        public int ItemCount { get; set; } = 0;
        public CASCSearchPhase SearchPhase { get; private set; } = CASCSearchPhase.Initializing; // 0 = initializing, 2 = searching, 4 = finished

        public string Result => Encoding.ASCII.GetString(SearchResult.ToArray());

        public int NumBytesFound => SearchResult.Count;

        public int NumPathStops => PathStops.Count;

        public void Add(byte value) => SearchResult.Add(value);

        public void RemoveRange(int index) => SearchResult.RemoveRange(index, SearchResult.Count - index);

        public void AddPathStop(PATH_STOP item) => PathStops.Add(item);

        public PATH_STOP GetPathStop(int index) => PathStops[index];

        public void Init() => SearchPhase = CASCSearchPhase.Initializing;

        public void Finish() => SearchPhase = CASCSearchPhase.Finished;

        public void InitSearchBuffers()
        {
            SearchResult.Clear();
            PathStops.Clear();

            ItemIndex = 0;
            CharIndex = 0;
            ItemCount = 0;
            SearchPhase = CASCSearchPhase.Searching;
        }
    }

    public class MNDXSearchResult
    {
        private string szSearchMask;
        public string SearchMask        // Search mask without wildcards
        {
            get { return szSearchMask; }
            set
            {
                Buffer.Init();

                szSearchMask = value ?? throw new ArgumentNullException(nameof(SearchMask));
            }
        }
        public string FoundPath { get; private set; }       // Found path name
        public int FileNameIndex { get; private set; }      // Index of the file name
        public SearchBuffer Buffer { get; private set; }

        public MNDXSearchResult()
        {
            Buffer = new SearchBuffer();

            SearchMask = string.Empty;
        }

        public void SetFindResult(string szFoundPath, int dwFileNameIndex)
        {
            FoundPath = szFoundPath;
            FileNameIndex = dwFileNameIndex;
        }
    }

    #endregion

    #region S1RootHandler

    public class S1RootHandler : RootHandlerBase
    {
        Dictionary<ulong, RootEntry> RootData = [];

        public override int Count => RootData.Count;

        public S1RootHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");
            using var r = new StreamReader(stream.BaseStream);
            string line;
            while ((line = r.ReadLine()) != null)
            {
                var tokens = line.Split('|');
                string file;
                var locale = LocaleFlags.All;
                if (tokens[0].IndexOf(':') != -1)
                {
                    var tokens2 = tokens[0].Split(':');
                    file = tokens2[0];
                    locale = (LocaleFlags)Enum.Parse(typeof(LocaleFlags), tokens2[1]);
                }
                else file = tokens[0];
                var fileHash = Hasher.ComputeHash(file);
                RootData[fileHash] = new RootEntry
                {
                    LocaleFlags = locale,
                    ContentFlags = ContentFlags.None,
                    cKey = tokens[1].FromHexString().ToMD5()
                };
                CascFile.Files[fileHash] = new CascFile(fileHash, file);
            }
            worker?.ReportProgress(100);
        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries() => RootData;

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            if (RootData.TryGetValue(hash, out var rootEntry)) yield return rootEntry;
        }

        // Returns only entries that match current locale and content flags
        public override IEnumerable<RootEntry> GetEntries(ulong hash) => GetEntriesForSelectedLocale(hash);

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null) { }

        protected override CascFolder CreateStorageTree()
        {
            var root = new CascFolder("root");
            CountSelect = 0;
            foreach (var entry in RootData)
            {
                if ((entry.Value.LocaleFlags & Locale) == 0) continue;
                CreateSubTree(root, entry.Key, CascFile.Files[entry.Key].FullName);
                CountSelect++;
            }
            // cleanup fake names for unknown files
            CountUnknown = 0;
            Logger.WriteLine("S1RootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);
            return root;
        }

        public override void Clear()
        {
            Root.Files.Clear();
            Root.Folders.Clear();
            CascFile.Files.Clear();
        }

        public override void Dump(EncodingHandler encodingHandler = null) { }
    }
    #endregion

    #region TVFSRootHandler

    ref struct TVFS_DIRECTORY_HEADER
    {
        public uint Magic;
        public byte FormatVersion;
        public byte HeaderSize;
        public byte EKeySize;
        public byte PatchKeySize;
        public int Flags;
        public int PathTableOffset;
        public int PathTableSize;
        public int VfsTableOffset;
        public int VfsTableSize;
        public int CftTableOffset;
        public int CftTableSize;
        public ushort MaxDepth;
        public int EstTableOffset;
        public int EstTableSize;
        public int CftOffsSize;
        public int EstOffsSize;
        public ReadOnlySpan<byte> PathTable;
        public ReadOnlySpan<byte> VfsTable;
        public ReadOnlySpan<byte> CftTable;
        //public ReadOnlySpan<byte> EstTable;
    }

    public ref struct PathBuffer
    {
        public Span<byte> Data;
        public int Position;

        public PathBuffer()
        {
            Data = new byte[512];
        }

        public void Append(byte value)
        {
            Data[Position++] = value;
        }

        public void Append(ReadOnlySpan<byte> values)
        {
            values.CopyTo(Data.Slice(Position));
            Position += values.Length;
        }

        public unsafe string GetString()
        {
#if NETSTANDARD2_0
            fixed (byte* ptr = Data)
                return Encoding.ASCII.GetString(ptr, Position);
#else
            return Encoding.ASCII.GetString(Data.Slice(0, Position));
#endif
        }
    }

    public struct VfsRootEntry
    {
        public MD5Hash eKey;
        public int ContentOffset; // not used
        public int ContentLength;
        public int CftOffset; // only used once and not need to be stored
    }

    public class TVFSRootHandler : RootHandlerBase
    {
        private Dictionary<ulong, RootEntry> tvfsData = new Dictionary<ulong, RootEntry>();
        private Dictionary<ulong, List<VfsRootEntry>> tvfsRootData = new Dictionary<ulong, List<VfsRootEntry>>();
        private List<(MD5Hash CKey, MD5Hash EKey)> VfsRootList;
        private HashSet<MD5Hash> VfsRootSet = new HashSet<MD5Hash>(MD5HashComparer9.Instance);
        protected readonly Dictionary<ulong, (string Orig, string New)> fileTree = new Dictionary<ulong, (string, string)>();

        private const uint TVFS_ROOT_MAGIC = 0x53465654;

        private const int TVFS_PTE_PATH_SEPARATOR_PRE = 0x0001;
        private const int TVFS_PTE_PATH_SEPARATOR_POST = 0x0002;
        private const int TVFS_PTE_NODE_VALUE = 0x0004;

        private const uint TVFS_FOLDER_NODE = 0x80000000;
        private const int TVFS_FOLDER_SIZE_MASK = 0x7FFFFFFF;

        ref struct PathTableEntry
        {
            public ReadOnlySpan<byte> Name;
            public int NodeFlags;
            public int NodeValue;
        }

        public TVFSRootHandler(BackgroundWorkerEx worker, CascHandler casc)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            var config = casc.Config;
            VfsRootList = config.VfsRootList;

            foreach (var vfsRoot in VfsRootList)
                VfsRootSet.Add(vfsRoot.EKey);

            var rootEKey = config.VfsRootEKey;

            using (var rootFile = casc.OpenFile(rootEKey))
            using (var reader = new BinaryReader(rootFile))
            {
                CaptureDirectoryHeader(out var dirHeader, reader);

                PathBuffer PathBuffer = new PathBuffer();

                ParseDirectoryData(casc, ref dirHeader, ref PathBuffer);
            }

            //foreach (var enc in casc.Encoding.Entries)
            //{
            //    Logger.WriteLine($"ENC: {enc.Key.ToHexString()} {enc.Value.Size}");
            //    foreach (var key in enc.Value.Keys)
            //        Logger.WriteLine($"    {key.ToHexString()}");
            //}

            worker?.ReportProgress(100);
        }

        private static bool PathBuffer_AppendNode(ref PathBuffer pathBuffer, in PathTableEntry pathEntry)
        {
            if ((pathEntry.NodeFlags & TVFS_PTE_PATH_SEPARATOR_PRE) != 0)
                pathBuffer.Append((byte)'/');

            pathBuffer.Append(pathEntry.Name);

            if ((pathEntry.NodeFlags & TVFS_PTE_PATH_SEPARATOR_POST) != 0)
                pathBuffer.Append((byte)'/');

            return true;
        }

        private bool CaptureDirectoryHeader(out TVFS_DIRECTORY_HEADER dirHeader, BinaryReader reader)
        {
            dirHeader = new TVFS_DIRECTORY_HEADER();

            dirHeader.Magic = reader.ReadUInt32();
            if (dirHeader.Magic != TVFS_ROOT_MAGIC)
                throw new InvalidDataException();

            dirHeader.FormatVersion = reader.ReadByte();
            if (dirHeader.FormatVersion != 1)
                throw new InvalidDataException();

            dirHeader.HeaderSize = reader.ReadByte();
            if (dirHeader.HeaderSize < 8)
                throw new InvalidDataException();

            dirHeader.EKeySize = reader.ReadByte();
            if (dirHeader.EKeySize != 9)
                throw new InvalidDataException();

            dirHeader.PatchKeySize = reader.ReadByte();
            if (dirHeader.PatchKeySize != 9)
                throw new InvalidDataException();

            dirHeader.Flags = reader.ReadInt32BE();
            dirHeader.PathTableOffset = reader.ReadInt32BE();
            dirHeader.PathTableSize = reader.ReadInt32BE();
            dirHeader.VfsTableOffset = reader.ReadInt32BE();
            dirHeader.VfsTableSize = reader.ReadInt32BE();
            dirHeader.CftTableOffset = reader.ReadInt32BE();
            dirHeader.CftTableSize = reader.ReadInt32BE();
            dirHeader.MaxDepth = reader.ReadUInt16BE();
            dirHeader.EstTableOffset = reader.ReadInt32BE();
            dirHeader.EstTableSize = reader.ReadInt32BE();

            static int GetOffsetFieldSize(int dwTableSize)
            {
                return dwTableSize switch
                {
                    > 0xffffff => 4,
                    > 0xffff => 3,
                    > 0xff => 2,
                    _ => 1
                };
            }

            dirHeader.CftOffsSize = GetOffsetFieldSize(dirHeader.CftTableSize);
            dirHeader.EstOffsSize = GetOffsetFieldSize(dirHeader.EstTableSize);

            reader.BaseStream.Position = dirHeader.PathTableOffset;
            dirHeader.PathTable = reader.ReadBytes(dirHeader.PathTableSize);

            reader.BaseStream.Position = dirHeader.VfsTableOffset;
            dirHeader.VfsTable = reader.ReadBytes(dirHeader.VfsTableSize);

            reader.BaseStream.Position = dirHeader.CftTableOffset;
            dirHeader.CftTable = reader.ReadBytes(dirHeader.CftTableSize);

            // reading this causes crash on some CoD games...
            //reader.BaseStream.Position = dirHeader.EstTableOffset;
            //dirHeader.EstTable = reader.ReadBytes(dirHeader.EstTableSize);

            return true;
        }

        private ReadOnlySpan<byte> CaptureVfsSpanCount(ref TVFS_DIRECTORY_HEADER dirHeader, int dwVfsOffset, ref byte SpanCount)
        {
            ReadOnlySpan<byte> VfsFileTable = dirHeader.VfsTable;
            ReadOnlySpan<byte> pbVfsFileEntry = VfsFileTable.Slice(dwVfsOffset);

            if (pbVfsFileEntry.Length == 0)
                return default;

            SpanCount = pbVfsFileEntry[0];
            pbVfsFileEntry = pbVfsFileEntry.Slice(1);

            return (1 <= SpanCount && SpanCount <= 224) ? pbVfsFileEntry : default;
        }

        private int CaptureVfsSpanEntry(ref TVFS_DIRECTORY_HEADER dirHeader, scoped ReadOnlySpan<byte> vfsSpanEntry, ref VfsRootEntry vfsRootEntry)
        {
            ReadOnlySpan<byte> cftFileTable = dirHeader.CftTable;
            int itemSize = sizeof(int) + sizeof(int) + dirHeader.CftOffsSize;

            int contentOffset = vfsSpanEntry.ReadInt32BE();
            int contentLength = vfsSpanEntry.Slice(4).ReadInt32BE();
            int cftOffset = vfsSpanEntry.Slice(4 + 4).ReadInt32(dirHeader.CftOffsSize);

            vfsRootEntry.ContentOffset = contentOffset;
            vfsRootEntry.ContentLength = contentLength;
            vfsRootEntry.CftOffset = cftOffset;

            ReadOnlySpan<byte> cftFileEntry = cftFileTable.Slice(cftOffset);
            ReadOnlySpan<byte> eKeySlice = cftFileEntry.Slice(0, dirHeader.EKeySize);
            Span<byte> eKey = stackalloc byte[16];
            eKeySlice.CopyTo(eKey);

            vfsRootEntry.eKey = Unsafe.As<byte, MD5Hash>(ref eKey[0]);

            return itemSize;
        }

        private ReadOnlySpan<byte> CapturePathEntry(ReadOnlySpan<byte> pathTable, out PathTableEntry pathEntry)
        {
            pathEntry = new PathTableEntry();

            if (pathTable.Length > 0 && pathTable[0] == 0)
            {
                pathEntry.NodeFlags |= TVFS_PTE_PATH_SEPARATOR_PRE;
                pathTable = pathTable.Slice(1);
            }

            if (pathTable.Length > 0 && pathTable[0] != 0xFF)
            {
                byte len = pathTable[0];
                pathTable = pathTable.Slice(1);

                pathEntry.Name = pathTable.Slice(0, len);
                pathTable = pathTable.Slice(len);
            }

            if (pathTable.Length > 0 && pathTable[0] == 0)
            {
                pathEntry.NodeFlags |= TVFS_PTE_PATH_SEPARATOR_POST;
                pathTable = pathTable.Slice(1);
            }

            if (pathTable.Length > 0)
            {
                if (pathTable[0] == 0xFF)
                {
                    if (1 + sizeof(int) > pathTable.Length)
                        return default;

                    pathEntry.NodeValue = pathTable.Slice(1).ReadInt32BE();
                    pathEntry.NodeFlags |= TVFS_PTE_NODE_VALUE;
                    pathTable = pathTable.Slice(1 + sizeof(int));
                }
                else
                {
                    pathEntry.NodeFlags |= TVFS_PTE_PATH_SEPARATOR_POST;
                    Debug.Assert(pathTable[0] != 0);
                }
            }

            return pathTable;
        }

        private bool IsVfsFileEKey(in MD5Hash eKey, out MD5Hash fullEKey)
        {
#if NET6_0_OR_GREATER
            return VfsRootSet.TryGetValue(eKey, out fullEKey);
#else
            if (VfsRootSet.Contains(eKey))
            {
                foreach (MD5Hash hash in VfsRootSet)
                {
                    if (hash.EqualsTo9(eKey))
                    {
                        fullEKey = hash;
                        return true;
                    }
                }
            }
            fullEKey = default;
            return false;
#endif
        }

        private bool IsVfsSubDirectory(CascHandler casc, out TVFS_DIRECTORY_HEADER subHeader, scoped in MD5Hash eKey)
        {
            if (IsVfsFileEKey(eKey, out var fullEKey))
            {
                using (var vfsRootFile = casc.OpenFile(fullEKey))
                using (var reader = new BinaryReader(vfsRootFile))
                {
                    CaptureDirectoryHeader(out subHeader, reader);
                }
                return true;
            }

            subHeader = default;
            return false;
        }

        private void ParsePathFileTable(CascHandler casc, ref TVFS_DIRECTORY_HEADER dirHeader, ref PathBuffer pathBuffer, ReadOnlySpan<byte> pathTable)
        {
            int savePos = pathBuffer.Position;

            while (pathTable.Length > 0)
            {
                pathTable = CapturePathEntry(pathTable, out var pathEntry);

                if (pathTable == default)
                    throw new InvalidDataException();

                PathBuffer_AppendNode(ref pathBuffer, pathEntry);

                if ((pathEntry.NodeFlags & TVFS_PTE_NODE_VALUE) != 0)
                {
                    if ((pathEntry.NodeValue & TVFS_FOLDER_NODE) != 0)
                    {
                        int dirLen = (pathEntry.NodeValue & TVFS_FOLDER_SIZE_MASK) - sizeof(int);

                        Debug.Assert((pathEntry.NodeValue & TVFS_FOLDER_SIZE_MASK) >= sizeof(int));

                        ParsePathFileTable(casc, ref dirHeader, ref pathBuffer, pathTable.Slice(0, dirLen));

                        pathTable = pathTable.Slice(dirLen);
                    }
                    else
                    {
                        byte dwSpanCount = 0;

                        ReadOnlySpan<byte> vfsSpanEntry = CaptureVfsSpanCount(ref dirHeader, pathEntry.NodeValue, ref dwSpanCount);
                        if (vfsSpanEntry == default)
                            throw new InvalidDataException();

                        if (dwSpanCount == 1)
                        {
                            VfsRootEntry vfsRootEntry = new VfsRootEntry();

                            int itemSize = CaptureVfsSpanEntry(ref dirHeader, vfsSpanEntry, ref vfsRootEntry);
                            vfsSpanEntry = vfsSpanEntry.Slice(itemSize);

                            if (vfsSpanEntry == default)
                                throw new InvalidDataException();

                            //Logger.WriteLine($"VFS: {vfsRootEntry.ContentOffset:X8} {vfsRootEntry.ContentLength:D9} {vfsRootEntry.CftOffset:X8} {vfsRootEntry.eKey.ToHexString()} 0");

                            if (IsVfsSubDirectory(casc, out var subHeader, vfsRootEntry.eKey))
                            {
                                pathBuffer.Append((byte)'/');

                                ParseDirectoryData(casc, ref subHeader, ref pathBuffer);
                            }
                            else
                            {
                                //string fileName = pathBuffer.ToString();
                                string fileName = pathBuffer.GetString();
                                string fileNameNew = MakeFileName(fileName);
                                ulong fileHash = Hasher.ComputeHash(fileNameNew);
                                fileTree.Add(fileHash, (fileName, fileNameNew));

                                tvfsRootData.Add(fileHash, new List<VfsRootEntry> { vfsRootEntry });

                                //if (casc.Encoding.GetCKeyFromEKey(vfsRootEntry.eKey, out MD5Hash cKey))
                                //{
                                //    tvfsData.Add(fileHash, new RootEntry { LocaleFlags = LocaleFlags.All, ContentFlags = ContentFlags.None, cKey = cKey });
                                //}
                                //else
                                //{
                                //    tvfsData.Add(fileHash, new RootEntry { LocaleFlags = LocaleFlags.All, ContentFlags = ContentFlags.None, cKey = default });
                                //}
                            }
                        }
                        else
                        {
                            //string fileName = pathBuffer.ToString();
                            string fileName = pathBuffer.GetString();
                            string fileNameNew = MakeFileName(fileName);
                            ulong fileHash = Hasher.ComputeHash(fileNameNew);
                            fileTree.Add(fileHash, (fileName, fileNameNew));

                            List<VfsRootEntry> vfsRootEntries = new List<VfsRootEntry>(dwSpanCount);

                            for (int dwSpanIndex = 0; dwSpanIndex < dwSpanCount; dwSpanIndex++)
                            {
                                VfsRootEntry vfsRootEntry = new VfsRootEntry();

                                int itemSize = CaptureVfsSpanEntry(ref dirHeader, vfsSpanEntry, ref vfsRootEntry);
                                vfsSpanEntry = vfsSpanEntry.Slice(itemSize);

                                if (vfsSpanEntry == default)
                                    throw new InvalidDataException();

                                //Logger.WriteLine($"VFS: {vfsRootEntry.ContentOffset:X8} {vfsRootEntry.ContentLength:D9} {vfsRootEntry.CftOffset:X8} {vfsRootEntry.eKey.ToHexString()} {dwSpanIndex}");

                                vfsRootEntries.Add(vfsRootEntry);

                                //if (casc.Encoding.GetCKeyFromEKey(vfsRootEntry.eKey, out MD5Hash cKey))
                                //{
                                //    throw new Exception("got CKey for EKey!");
                                //}
                            }

                            //tvfsData.Add(fileHash, new RootEntry { LocaleFlags = LocaleFlags.All, ContentFlags = ContentFlags.None, cKey = default });
                            tvfsRootData.Add(fileHash, vfsRootEntries);
                        }
                    }

                    //pathBuffer.Remove(savePos, pathBuffer.Length - savePos);
                    pathBuffer.Position = savePos;
                }
            }
        }

        private static string MakeFileName(string data)
        {
            return data;
            //string file = data;

            //if (data.IndexOf(':') != -1)
            //{
            //    StringBuilder sb = new StringBuilder(data);

            //    for (int i = 0; i < sb.Length; i++)
            //    {
            //        if (sb[i] == ':')
            //            sb[i] = '\\';
            //    }

            //    file = sb.ToString();

            //    //string[] tokens2 = data.Split(':');

            //    //if (tokens2.Length == 2 || tokens2.Length == 3 || tokens2.Length == 4)
            //    //    file = Path.Combine(tokens2);
            //    //else
            //    //    throw new InvalidDataException("tokens2.Length");
            //}
            //else
            //{
            //    file = data;
            //}
            //return file;
        }

        private void ParseDirectoryData(CascHandler casc, ref TVFS_DIRECTORY_HEADER dirHeader, ref PathBuffer pathBuffer)
        {
            ReadOnlySpan<byte> pathTable = dirHeader.PathTable;

            if (1 + sizeof(int) < pathTable.Length)
            {
                if (pathTable[0] == 0xFF)
                {
                    int dwNodeValue = pathTable.Slice(1).ReadInt32BE();

                    if ((dwNodeValue & TVFS_FOLDER_NODE) == 0)
                        throw new InvalidDataException();

                    int pathFileTableSize = dwNodeValue & TVFS_FOLDER_SIZE_MASK;

                    if (pathFileTableSize > pathTable.Length)
                        throw new InvalidDataException();

                    pathTable = pathTable.Slice(1 + sizeof(int));
                }
            }

            ParsePathFileTable(casc, ref dirHeader, ref pathBuffer, pathTable);
        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            return tvfsData;
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            if (tvfsData.TryGetValue(hash, out RootEntry rootEntry))
                yield return rootEntry;
        }

        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            return GetEntriesForSelectedLocale(hash);
        }

        public virtual List<VfsRootEntry> GetVfsRootEntries(ulong hash)
        {
            tvfsRootData.TryGetValue(hash, out var vfsRootEntry);
            return vfsRootEntry;
        }

        public void SetHashDuplicate(ulong oldHash, ulong newHash)
        {
            if (tvfsRootData.TryGetValue(oldHash, out var vfsRootEntry))
            {
                tvfsRootData[newHash] = vfsRootEntry;
                fileTree[newHash] = fileTree[oldHash];
            }
            if (tvfsData.TryGetValue(oldHash, out var rootEntry))
            {
                tvfsData[newHash] = rootEntry;
            }
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {

        }

        protected override CascFolder CreateStorageTree()
        {
            var root = new CascFolder("root");

            CountSelect = 0;

            foreach (var entry in tvfsRootData)
            {
                CreateSubTree(root, entry.Key, fileTree[entry.Key].New);
                CountSelect++;
            }

            return root;
        }

        public override void Clear()
        {
            tvfsData.Clear();
            tvfsRootData.Clear();
            Root.Files.Clear();
            Root.Folders.Clear();
            CascFile.Files.Clear();
        }

        public override void Dump(EncodingHandler encodingHandler = null)
        {
#if DEBUG
            Logger.WriteLine("TVFSRootHandler Dump:");

            Dictionary<ulong, int> keyCounts = new Dictionary<ulong, int>();
            keyCounts[0] = 0;

            foreach (var fd in tvfsRootData)
            {
                if (!fileTree.TryGetValue(fd.Key, out var name))
                {
                    Logger.WriteLine($"Can't get name for Hash: {fd.Key:X16}");
                    continue;
                }

                Logger.WriteLine($"Hash: {fd.Key:X16} Name: {name.Orig}");

                foreach (var entry in fd.Value)
                {
                    Logger.WriteLine($"\teKey: {entry.eKey.ToHexString()} ContentLength {entry.ContentLength} ContentOffset {entry.ContentOffset} CftOffset {entry.CftOffset}");

                    if (encodingHandler != null)
                    {
                        if (encodingHandler.GetCKeyFromEKey(entry.eKey, out MD5Hash cKey))
                        {
                            if (encodingHandler.GetEntry(cKey, out var encodingEntry))
                            {
                                foreach (var eKey in encodingEntry.Keys)
                                {
                                    var keys = encodingHandler.GetEncryptionKeys(eKey);
                                    if (keys != null)
                                    {
                                        Logger.WriteLine($"\teKey: {eKey.ToHexString()} cKey: {cKey.ToHexString()} TactKeys: {string.Join(",", keys.Select(k => $"{k:X16}"))} Size: {encodingEntry.Size}");

                                        foreach (var key in keys)
                                        {
                                            if (!keyCounts.ContainsKey(key))
                                                keyCounts[key] = 0;

                                            keyCounts[key]++;
                                        }
                                    }
                                    else
                                    {
                                        Logger.WriteLine($"\teKey: {eKey.ToHexString()} cKey: {cKey.ToHexString()} TactKeys: NA Size: {encodingEntry.Size}");
                                        keyCounts[0]++;
                                    }
                                }
                            }
                            else
                            {
                                Logger.WriteLine($"\tEncodingEntry: NA");
                            }
                        }
                        else
                        {
                            Logger.WriteLine($"\tcKey: NA");
                        }
                    }
                }
            }

            foreach (var kv in keyCounts)
            {
                Logger.WriteLine($"Key: {kv.Key:X16} Count: {kv.Value}");
            }
#endif
        }
    }

    static class SpanExtensions
    {
        public static int ReadInt32(this ReadOnlySpan<byte> source, int numBytes)
        {
            int Value = 0;

            if (numBytes > 0)
                Value = (Value << 0x08) | source[0];
            if (numBytes > 1)
                Value = (Value << 0x08) | source[1];
            if (numBytes > 2)
                Value = (Value << 0x08) | source[2];
            if (numBytes > 3)
                Value = (Value << 0x08) | source[3];

            return Value;
        }

        public static int ReadInt32BE(this ReadOnlySpan<byte> source)
        {
            return BinaryPrimitives.ReadInt32BigEndian(source);
        }
    }

    #endregion

    #region Wc3RootHandler

    public class Wc3RootHandler : RootHandlerBase
    {
        readonly Dictionary<ulong, RootEntry> RootData = [];

        public override int Count => RootData.Count;

        public Wc3RootHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");
            using var r = new StreamReader(stream.BaseStream);
            string line;
            while ((line = r.ReadLine()) != null)
            {
                var tokens = line.Split('|');
                if (tokens.Length != 3 && tokens.Length != 4) throw new InvalidDataException("tokens.Length != 3 && tokens.Length != 4");
                string file;
                if (tokens[0].IndexOf(':') != -1)
                {
                    var tokens2 = tokens[0].Split(':');
                    if (tokens2.Length == 2 || tokens2.Length == 3 || tokens2.Length == 4) file = Path.Combine(tokens2);
                    else throw new InvalidDataException("tokens2.Length");
                }
                else
                    file = tokens[0];
                if (!Enum.TryParse(tokens[2], out LocaleFlags locale))
                    locale = LocaleFlags.All;
                var fileHash = Hasher.ComputeHash(file);
                RootData[fileHash] = new RootEntry()
                {
                    LocaleFlags = locale,
                    ContentFlags = ContentFlags.None,
                    cKey = tokens[1].FromHexString().ToMD5()
                };
                CascFile.Files[fileHash] = new CascFile(fileHash, file);
            }
            worker?.ReportProgress(100);
        }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries() => RootData;

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            if (RootData.TryGetValue(hash, out var rootEntry)) yield return rootEntry;
        }

        // Returns only entries that match current locale and content flags
        public override IEnumerable<RootEntry> GetEntries(ulong hash) => GetEntriesForSelectedLocale(hash);

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null) { }

        protected override CascFolder CreateStorageTree()
        {
            var root = new CascFolder("root");
            CountSelect = 0;
            foreach (var entry in RootData)
            {
                if ((entry.Value.LocaleFlags & Locale) == 0) continue;
                CreateSubTree(root, entry.Key, CascFile.Files[entry.Key].FullName);
                CountSelect++;
            }
            // cleanup fake names for unknown files
            CountUnknown = 0;
            Logger.WriteLine("WC3RootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);
            return root;
        }

        public override void Clear()
        {
            Root.Files.Clear();
            Root.Folders.Clear();
            CascFile.Files.Clear();
        }

        public override void Dump(EncodingHandler encodingHandler = null) { }
    }

    #endregion

    #region WowRootHandler

    [Flags]
    public enum LocaleFlags : uint
    {
        All = 0xFFFFFFFF,
        None = 0,
        Unk1 = 0x1,
        enUS = 0x2,
        koKR = 0x4,
        Unk8 = 0x8,
        frFR = 0x10,
        deDE = 0x20,
        zhCN = 0x40,
        esES = 0x80,
        zhTW = 0x100,
        enGB = 0x200,
        enCN = 0x400,
        enTW = 0x800,
        esMX = 0x1000,
        ruRU = 0x2000,
        ptBR = 0x4000,
        itIT = 0x8000,
        ptPT = 0x10000,
        enSG = 0x01000000, // custom
        plPL = 0x02000000, // custom
        jaJP = 0x04000000, // custom
        trTR = 0x08000000, // custom
        arSA = 0x10000000, // custom
        All_WoW = enUS | koKR | frFR | deDE | zhCN | esES | zhTW | enGB | esMX | ruRU | ptBR | itIT | ptPT
    }

    [Flags]
    public enum ContentFlags : uint
    {
        None = 0,
        HighResTexture = 0x1, // seen on *.wlm files
        F00000002 = 0x2,
        F00000004 = 0x4, // install?
        Windows = 0x8, // added in 7.2.0.23436
        MacOS = 0x10, // added in 7.2.0.23436
        F00000020 = 0x20, // x86?
        F00000040 = 0x40, // x64?
        Alternate = 0x80, // many chinese models have this flag
        F00000100 = 0x100, // apparently client doesn't load files with this flag
        F00000800 = 0x800, // only seen on UpdatePlugin files
        F00008000 = 0x8000, // Windows ARM64?
        F00020000 = 0x20000, // new 9.0
        F00040000 = 0x40000, // new 9.0
        F00080000 = 0x80000, // new 9.0
        F00100000 = 0x100000, // new 9.0
        F00200000 = 0x200000, // new 9.0
        F00400000 = 0x400000, // new 9.0
        F00800000 = 0x800000, // new 9.0
        F02000000 = 0x2000000, // new 9.0
        F04000000 = 0x4000000, // new 9.0
        Encrypted = 0x8000000, // encrypted may be?
        NoNameHash = 0x10000000, // doesn't have name hash?
        F20000000 = 0x20000000, // added in 21737, used for many cinematics
        F40000000 = 0x40000000,
        NotCompressed = 0x80000000 // sounds have this flag
    }

    public readonly struct MD5Hash
    {
        public readonly ulong lowPart;
        public readonly ulong highPart;
    }

    public struct RootEntry
    {
        public MD5Hash cKey;
        public ContentFlags ContentFlags;
        public LocaleFlags LocaleFlags;
    }

    public static class FileDataHash
    {
        public static ulong ComputeHash(int fileDataId)
        {
            ulong baseOffset = 0xCBF29CE484222325UL;

            for (int i = 0; i < 4; i++)
            {
                baseOffset = 0x100000001B3L * ((((uint)fileDataId >> (8 * i)) & 0xFF) ^ baseOffset);
            }

            return baseOffset;
        }
    }

    public class ContentFlagsFilter
    {
        protected static bool Check(ContentFlags value, ContentFlags flag, bool include) => include ? (value & flag) != ContentFlags.None : (value & flag) == ContentFlags.None;

        public static IEnumerable<RootEntry> Filter(IEnumerable<RootEntry> entries, bool alternate, bool highResTexture)
        {
            IEnumerable<RootEntry> temp = entries;

            if (temp.Any(e => Check(e.ContentFlags, ContentFlags.Alternate, true)))
                temp = temp.Where(e => Check(e.ContentFlags, ContentFlags.Alternate, alternate));

            if (temp.Any(e => Check(e.ContentFlags, ContentFlags.HighResTexture, true)))
                temp = temp.Where(e => Check(e.ContentFlags, ContentFlags.HighResTexture, highResTexture));

            return temp;
        }
    }

    public class WowRootHandler : RootHandlerBase
    {
        private MultiDictionary<int, RootEntry> RootData = new MultiDictionary<int, RootEntry>();
        private Dictionary<int, ulong> FileDataStore = new Dictionary<int, ulong>();
        private Dictionary<ulong, int> FileDataStoreReverse = new Dictionary<ulong, int>();
        private HashSet<ulong> UnknownFiles = new HashSet<ulong>();

        public override int Count => RootData.Count;
        public override int CountTotal => RootData.Sum(re => re.Value.Count);
        public override int CountUnknown => UnknownFiles.Count;
        public IReadOnlyDictionary<int, List<RootEntry>> RootEntries => RootData;
        public IReadOnlyDictionary<int, ulong> FileDataToLookup => FileDataStore;

        public WowRootHandler(BinaryReader stream, BackgroundWorkerEx worker)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            int magic = stream.ReadInt32();

            int numFilesTotal = 0, numFilesWithNameHash = 0, numFilesRead = 0;

            const int TSFMMagic = 0x4D465354;

            int headerSize;
            bool isLegacy;

            if (magic == TSFMMagic)
            {
                isLegacy = false;

                if (stream.BaseStream.Length < 12)
                    throw new Exception("build manifest is truncated");

                int field04 = stream.ReadInt32();
                int field08 = stream.ReadInt32();

                int version = field08;
                headerSize = field04;

                if (version != 1)
                {
                    numFilesTotal = field04;
                    numFilesWithNameHash = field08;
                    headerSize = 12;
                }
                else
                {
                    numFilesTotal = stream.ReadInt32();
                    numFilesWithNameHash = stream.ReadInt32();
                }
            }
            else
            {
                isLegacy = true;
                headerSize = 0;
                numFilesTotal = (int)(stream.BaseStream.Length / 28);
                numFilesWithNameHash = (int)(stream.BaseStream.Length / 28);
            }

            if (stream.BaseStream.Length < headerSize)
                throw new Exception("build manifest is truncated");

            stream.BaseStream.Position = headerSize;

            int blockIndex = 0;

            while (stream.BaseStream.Position < stream.BaseStream.Length)
            {
                int count = stream.ReadInt32();

                numFilesRead += count;

                ContentFlags contentFlags = (ContentFlags)stream.ReadUInt32();
                LocaleFlags localeFlags = (LocaleFlags)stream.ReadUInt32();

                if (localeFlags == LocaleFlags.None)
                    throw new InvalidDataException("block.LocaleFlags == LocaleFlags.None");

                if (contentFlags != ContentFlags.None && (contentFlags & (ContentFlags.HighResTexture | ContentFlags.Windows | ContentFlags.MacOS | ContentFlags.Alternate | ContentFlags.F00020000 | ContentFlags.F00080000 | ContentFlags.F00100000 | ContentFlags.F00200000 | ContentFlags.F00400000 | ContentFlags.F02000000 | ContentFlags.NotCompressed | ContentFlags.NoNameHash | ContentFlags.F20000000)) == 0)
                    throw new InvalidDataException("block.ContentFlags != ContentFlags.None");

                RootEntry[] entries = new RootEntry[count];
                int[] filedataIds = new int[count];

                int fileDataIndex = 0;

                for (var i = 0; i < count; ++i)
                {
                    entries[i].LocaleFlags = localeFlags;
                    entries[i].ContentFlags = contentFlags;

                    filedataIds[i] = fileDataIndex + stream.ReadInt32();
                    fileDataIndex = filedataIds[i] + 1;
                }

                //Console.WriteLine($"Block {blockIndex}: {contentFlags} {localeFlags} count {count}");

                ulong[] nameHashes = null;

                if (!isLegacy)
                {
                    for (var i = 0; i < count; ++i)
                        entries[i].cKey = stream.Read<MD5Hash>();

                    if ((contentFlags & ContentFlags.NoNameHash) == 0)
                    {
                        nameHashes = new ulong[count];

                        for (var i = 0; i < count; ++i)
                            nameHashes[i] = stream.ReadUInt64();
                    }
                }
                else
                {
                    nameHashes = new ulong[count];

                    for (var i = 0; i < count; ++i)
                    {
                        entries[i].cKey = stream.Read<MD5Hash>();
                        nameHashes[i] = stream.ReadUInt64();
                    }
                }

                for (var i = 0; i < count; ++i)
                {
                    int fileDataId = filedataIds[i];

                    //Logger.WriteLine("filedataid {0}", fileDataId);

                    ulong hash;

                    if (nameHashes == null)
                    {
                        hash = FileDataHash.ComputeHash(fileDataId);
                    }
                    else
                    {
                        hash = nameHashes[i];
                    }

                    RootData.Add(fileDataId, entries[i]);

                    //Console.WriteLine($"File: {fileDataId:X8} {hash:X16} {entries[i].cKey.ToHexString()}");

                    if (FileDataStore.TryGetValue(fileDataId, out ulong hash2))
                    {
                        if (hash2 == hash)
                        {
                            // duplicate, skipping
                        }
                        else
                        {
                            Logger.WriteLine("ERROR: got multiple hashes for filedataid {0}", fileDataId);
                        }
                        continue;
                    }

                    FileDataStore.Add(fileDataId, hash);
                    FileDataStoreReverse.Add(hash, fileDataId);

                    if (nameHashes != null)
                    {
                        // generate our custom hash as well so we can still find file without calling GetHashByFileDataId in some weird cases
                        ulong fileDataHash = FileDataHash.ComputeHash(fileDataId);
                        FileDataStoreReverse.Add(fileDataHash, fileDataId);
                    }
                }

                worker?.ReportProgress((int)(stream.BaseStream.Position / (float)stream.BaseStream.Length * 100));

                blockIndex++;
            }
        }

        public IEnumerable<RootEntry> GetAllEntriesByFileDataId(int fileDataId) => GetAllEntries(GetHashByFileDataId(fileDataId));

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            foreach (var set in RootData)
                foreach (var entry in set.Value)
                    yield return new KeyValuePair<ulong, RootEntry>(FileDataStore[set.Key], entry);
        }

        public IEnumerable<(int FileDataId, RootEntry Entry)> GetAllEntriesWithFileDataId()
        {
            foreach (var set in RootData)
                foreach (var entry in set.Value)
                    yield return (set.Key, entry);
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            if (!FileDataStoreReverse.TryGetValue(hash, out int fileDataId))
                yield break;

            if (!RootData.TryGetValue(fileDataId, out List<RootEntry> result))
                yield break;

            foreach (var entry in result)
                yield return entry;
        }

        public IEnumerable<RootEntry> GetEntriesByFileDataId(int fileDataId) => GetEntries(GetHashByFileDataId(fileDataId));

        // Returns only entries that match current locale and override setting
        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            var rootInfos = GetAllEntries(hash);

            if (!rootInfos.Any())
                yield break;

            var rootInfosLocale = rootInfos.Where(re => (re.LocaleFlags & Locale) != LocaleFlags.None);

            if (rootInfosLocale.Count() > 1)
            {
                IEnumerable<RootEntry> rootInfosLocaleOverride = ContentFlagsFilter.Filter(rootInfosLocale, OverrideArchive, PreferHighResTextures);

                if (rootInfosLocaleOverride.Any())
                    rootInfosLocale = rootInfosLocaleOverride;
            }

            foreach (var entry in rootInfosLocale)
                yield return entry;
        }

        public bool FileExist(int fileDataId) => RootData.ContainsKey(fileDataId);

        public ulong GetHashByFileDataId(int fileDataId)
        {
            FileDataStore.TryGetValue(fileDataId, out ulong hash);
            return hash;
        }

        public int GetFileDataIdByHash(ulong hash)
        {
            FileDataStoreReverse.TryGetValue(hash, out int fid);
            return fid;
        }

        public int GetFileDataIdByName(string name) => GetFileDataIdByHash(Hasher.ComputeHash(name));

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {
            //CASCFile.Files.Clear();

            using (var _ = new PerfCounter("WowRootHandler::LoadListFile()"))
            {
                worker?.ReportProgress(0, "Loading \"listfile\"...");

                if (!File.Exists(path))
                {
                    Logger.WriteLine("WowRootHandler: list file missing!");
                    return;
                }

                bool isCsv = Path.GetExtension(path) == ".csv";

                Logger.WriteLine($"WowRootHandler: loading listfile {path}...");

                using (var fs2 = File.Open(path, FileMode.Open))
                using (var sr = new StreamReader(fs2))
                {
                    string line;

                    char[] splitChar = isCsv ? new char[] { ';' } : new char[] { ' ' };

                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] tokens = line.Split(splitChar, 2);

                        if (tokens.Length != 2)
                        {
                            Logger.WriteLine($"Invalid line in listfile: {line}");
                            continue;
                        }

                        if (!int.TryParse(tokens[0], out int fileDataId))
                        {
                            Logger.WriteLine($"Invalid line in listfile: {line}");
                            continue;
                        }

                        // skip invalid names
                        if (!RootData.ContainsKey(fileDataId))
                        {
                            Logger.WriteLine($"Invalid fileDataId in listfile: {line}");
                            continue;
                        }

                        string file = tokens[1];

                        ulong fileHash = FileDataStore[fileDataId];

                        if (!CascFile.Files.ContainsKey(fileHash))
                            CascFile.Files.Add(fileHash, new CascFile(fileHash, file));
                        else
                            Logger.WriteLine($"Duplicate fileDataId {fileDataId} detected: {line}");

                        worker?.ReportProgress((int)(sr.BaseStream.Position / (float)sr.BaseStream.Length * 100));
                    }
                }

                Logger.WriteLine($"WowRootHandler: loaded {CascFile.Files.Count} valid file names");
            }
        }

        protected override CascFolder CreateStorageTree()
        {
            var root = new CascFolder("root");

            // Reset counts
            CountSelect = 0;
            UnknownFiles.Clear();

            // Create new tree based on specified locale
            foreach (var rootEntry in RootData)
            {
                var rootInfosLocale = rootEntry.Value.Where(re => (re.LocaleFlags & Locale) != LocaleFlags.None);

                if (rootInfosLocale.Count() > 1)
                {
                    IEnumerable<RootEntry> rootInfosLocaleOverride = ContentFlagsFilter.Filter(rootInfosLocale, OverrideArchive, PreferHighResTextures);

                    if (rootInfosLocaleOverride.Any())
                        rootInfosLocale = rootInfosLocaleOverride;
                }

                if (!rootInfosLocale.Any())
                    continue;

                string filename;

                ulong hash = FileDataStore[rootEntry.Key];

                if (!CascFile.Files.TryGetValue(hash, out CascFile file))
                {
                    filename = "unknown\\" + "FILEDATA_" + rootEntry.Key;

                    UnknownFiles.Add(hash);
                }
                else
                {
                    filename = file.FullName;
                }

                CreateSubTree(root, hash, filename);
                CountSelect++;
            }

            Logger.WriteLine("WowRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }

        public bool IsUnknownFile(ulong hash) => UnknownFiles.Contains(hash);

        public override void Clear()
        {
            RootData.Clear();
            RootData = null;
            FileDataStore.Clear();
            FileDataStore = null;
            FileDataStoreReverse.Clear();
            FileDataStoreReverse = null;
            UnknownFiles.Clear();
            UnknownFiles = null;
            Root?.Files.Clear();
            Root?.Folders.Clear();
            Root = null;
            CascFile.Files.Clear();
        }

        public override void Dump(EncodingHandler encodingHandler = null)
        {
            Logger.WriteLine("WowRootHandler Dump:");

            foreach (var fd in RootData.OrderBy(r => r.Key))
            {
                string name;

                if (FileDataStore.TryGetValue(fd.Key, out ulong hash) && CascFile.Files.TryGetValue(hash, out CascFile file))
                    name = file.FullName;
                else
                    name = $"FILEDATA_{fd.Key}";

                Logger.WriteLine($"FileData: {fd.Key:D7} Hash: {hash:X16} Locales: {fd.Value.Aggregate(LocaleFlags.None, (a, b) => a | b.LocaleFlags)} Name: {name}");

                foreach (var entry in fd.Value)
                {
                    Logger.WriteLine($"\tcKey: {entry.cKey.ToHexString()} Locale: {entry.LocaleFlags} CF: {entry.ContentFlags}");

                    if (encodingHandler != null)
                    {
                        if (encodingHandler.GetEntry(entry.cKey, out var encodingEntry))
                        {
                            foreach (var eKey in encodingEntry.Keys)
                            {
                                var keys = encodingHandler.GetEncryptionKeys(eKey);
                                if (keys != null)
                                    Logger.WriteLine($"\teKey: {eKey.ToHexString()} TactKeys: {string.Join(",", keys.Select(k => $"{k:X16}"))} Size: {encodingEntry.Size}");
                                else
                                    Logger.WriteLine($"\teKey: {eKey.ToHexString()} TactKeys: NA Size: {encodingEntry.Size}");
                            }
                        }
                    }
                }
            }
        }
    }

    #endregion

    #region WowTVFSRootHandler

    public struct WowVfsRootEntry
    {
        public MD5Hash cKey;
        public ContentFlags ContentFlags;
        public LocaleFlags LocaleFlags;
        public MD5Hash eKey;
        public int ContentOffset; // not used
        public int ContentLength;
        public int CftOffset; // only used once and not need to be stored
    }

    public class ContentFlagsFilterVfs : ContentFlagsFilter
    {
        public static IEnumerable<WowVfsRootEntry> Filter(IEnumerable<WowVfsRootEntry> entries, bool alternate, bool highResTexture)
        {
            IEnumerable<WowVfsRootEntry> temp = entries;

            if (temp.Any(e => Check(e.ContentFlags, ContentFlags.Alternate, true)))
                temp = temp.Where(e => Check(e.ContentFlags, ContentFlags.Alternate, alternate));

            if (temp.Any(e => Check(e.ContentFlags, ContentFlags.HighResTexture, true)))
                temp = temp.Where(e => Check(e.ContentFlags, ContentFlags.HighResTexture, highResTexture));

            return temp;
        }
    }

    public sealed class WowTVFSRootHandler : TVFSRootHandler
    {
        private readonly MultiDictionary<int, WowVfsRootEntry> RootData = new MultiDictionary<int, WowVfsRootEntry>();
        private readonly Dictionary<int, ulong> FileDataStore = new Dictionary<int, ulong>();
        private readonly Dictionary<ulong, int> FileDataStoreReverse = new Dictionary<ulong, int>();
        private readonly HashSet<ulong> UnknownFiles = new HashSet<ulong>();
        public IReadOnlyDictionary<int, List<WowVfsRootEntry>> RootEntries => RootData;

        public override int Count => RootData.Count;
        public override int CountTotal => RootData.Sum(re => re.Value.Count);
        public override int CountUnknown => UnknownFiles.Count;

        public WowTVFSRootHandler(BackgroundWorkerEx worker, CascHandler casc) : base(worker, casc)
        {
            worker?.ReportProgress(0, "Loading \"root\"...");

            foreach (var tvfsEntry in fileTree)
            {
                if (tvfsEntry.Value.Orig.Length == 53)
                {
#if NET6_0_OR_GREATER
                    ReadOnlySpan<char> entryData = tvfsEntry.Value.Orig.AsSpan();
                    LocaleFlags locale = (LocaleFlags)int.Parse(entryData.Slice(0, 8), System.Globalization.NumberStyles.HexNumber);
                    ContentFlags content = (ContentFlags)int.Parse(entryData.Slice(8, 4), System.Globalization.NumberStyles.HexNumber);
                    int fileDataId = int.Parse(entryData.Slice(13, 8), System.Globalization.NumberStyles.HexNumber);
                    ReadOnlySpan<char> cKeySpan = entryData.Slice(21, 32);
                    MD5Hash cKey = Convert.FromHexString(cKeySpan).ToMD5();
#else
                    string entryData = tvfsEntry.Value.Orig;
                    LocaleFlags locale = (LocaleFlags)int.Parse(entryData.Substring(0, 8), System.Globalization.NumberStyles.HexNumber);
                    ContentFlags content = (ContentFlags)int.Parse(entryData.Substring(8, 4), System.Globalization.NumberStyles.HexNumber);
                    int fileDataId = int.Parse(entryData.Substring(13, 8), System.Globalization.NumberStyles.HexNumber);
                    byte[] cKeyBytes = entryData.Substring(21, 32).FromHexString();
                    MD5Hash cKey = cKeyBytes.ToMD5();
#endif

                    ulong hash = FileDataHash.ComputeHash(fileDataId);

#if DEBUG
                    Logger.WriteLine($"{tvfsEntry.Value.Orig} {tvfsEntry.Key:X16} {hash:X16} {locale} {content} {fileDataId} {cKey.ToHexString()}");
#endif
                    var vfsEntries = base.GetVfsRootEntries(tvfsEntry.Key);

                    if (vfsEntries.Count != 1)
                        throw new Exception("vfsEntries.Count != 1");

                    RootData.Add(fileDataId, new WowVfsRootEntry { cKey = cKey, LocaleFlags = locale, ContentFlags = content, eKey = vfsEntries[0].eKey, ContentLength = vfsEntries[0].ContentLength, ContentOffset = vfsEntries[0].ContentOffset, CftOffset = vfsEntries[0].CftOffset });

                    if (FileDataStore.TryGetValue(fileDataId, out ulong hash2))
                    {
                        if (hash2 == hash)
                        {
                            // duplicate, skipping
                        }
                        else
                        {
                            Logger.WriteLine($"ERROR: got multiple hashes for filedataid {fileDataId}: {hash:X16} {hash2:X16}");
                        }
                        continue;
                    }

                    FileDataStore.Add(fileDataId, hash);
                    FileDataStoreReverse.Add(hash, fileDataId);
                    //SetHashDuplicate(tvfsEntry.Key, hash);
                }
#if DEBUG
                else
                {
                    Logger.WriteLine($"{tvfsEntry.Value.Orig} {LocaleFlags.All} {ContentFlags.None} {0}");
                }
#endif
            }

            worker?.ReportProgress(100);
        }

        public IEnumerable<RootEntry> GetAllEntriesByFileDataId(int fileDataId) => GetAllEntries(GetHashByFileDataId(fileDataId));

        //public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        //{
        //    foreach (var set in RootData)
        //        foreach (var entry in set.Value)
        //            yield return new KeyValuePair<ulong, RootEntry>(FileDataStore[set.Key], entry);
        //}

        //public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        //{
        //    if (!FileDataStoreReverse.TryGetValue(hash, out int fileDataId))
        //        yield break;

        //    if (!RootData.TryGetValue(fileDataId, out List<RootEntry> result))
        //        yield break;

        //    foreach (var entry in result)
        //        yield return entry;
        //}

        public IEnumerable<RootEntry> GetEntriesByFileDataId(int fileDataId) => GetEntries(GetHashByFileDataId(fileDataId));

        // Returns only entries that match current locale and override setting
        public override IEnumerable<RootEntry> GetEntries(ulong hash)
        {
            var rootInfos = GetAllEntries(hash);

            if (!rootInfos.Any())
                yield break;

            var rootInfosLocale = rootInfos.Where(re => (re.LocaleFlags & Locale) != LocaleFlags.None);

            if (rootInfosLocale.Count() > 1)
            {
                IEnumerable<RootEntry> rootInfosLocaleOverride = ContentFlagsFilter.Filter(rootInfosLocale, OverrideArchive, PreferHighResTextures);

                if (rootInfosLocaleOverride.Any())
                    rootInfosLocale = rootInfosLocaleOverride;
            }

            foreach (var entry in rootInfosLocale)
                yield return entry;
        }

        public bool FileExist(int fileDataId) => RootData.ContainsKey(fileDataId);

        public ulong GetHashByFileDataId(int fileDataId)
        {
            FileDataStore.TryGetValue(fileDataId, out ulong hash);
            return hash;
        }

        public override List<VfsRootEntry> GetVfsRootEntries(ulong hash)
        {
            if (!FileDataStoreReverse.TryGetValue(hash, out int fileDataId))
                return null;

            if (!RootData.TryGetValue(fileDataId, out List<WowVfsRootEntry> result))
                return null;

            var rootInfos = result;

            if (!rootInfos.Any())
                return null;

            var rootInfosLocale = rootInfos.Where(re => (re.LocaleFlags & Locale) != LocaleFlags.None);

            if (rootInfosLocale.Count() > 1)
            {
                IEnumerable<WowVfsRootEntry> rootInfosLocaleOverride = ContentFlagsFilterVfs.Filter(rootInfosLocale, OverrideArchive, PreferHighResTextures);

                if (rootInfosLocaleOverride.Any())
                    rootInfosLocale = rootInfosLocaleOverride;
            }

            return rootInfosLocale.Select(e => new VfsRootEntry { eKey = e.eKey, ContentLength = e.ContentLength, ContentOffset = e.ContentOffset, CftOffset = e.CftOffset }).ToList();
        }

        public int GetFileDataIdByHash(ulong hash)
        {
            FileDataStoreReverse.TryGetValue(hash, out int fid);
            return fid;
        }

        public int GetFileDataIdByName(string name) => GetFileDataIdByHash(Hasher.ComputeHash(name));

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {
            //CASCFile.Files.Clear();

            using (var _ = new PerfCounter("WowRootHandler::LoadListFile()"))
            {
                worker?.ReportProgress(0, "Loading \"listfile\"...");

                if (!File.Exists(path))
                {
                    Logger.WriteLine("WowRootHandler: list file missing!");
                    return;
                }

                bool isCsv = Path.GetExtension(path) == ".csv";

                Logger.WriteLine($"WowRootHandler: loading listfile {path}...");

                using (var fs2 = File.Open(path, FileMode.Open))
                using (var sr = new StreamReader(fs2))
                {
                    string line;

                    char[] splitChar = isCsv ? new char[] { ';' } : new char[] { ' ' };

                    while ((line = sr.ReadLine()) != null)
                    {
                        string[] tokens = line.Split(splitChar, 2);

                        if (tokens.Length != 2)
                        {
                            Logger.WriteLine($"Invalid line in listfile: {line}");
                            continue;
                        }

                        if (!int.TryParse(tokens[0], out int fileDataId))
                        {
                            Logger.WriteLine($"Invalid line in listfile: {line}");
                            continue;
                        }

                        // skip invalid names
                        if (!RootData.ContainsKey(fileDataId))
                        {
                            Logger.WriteLine($"Invalid fileDataId in listfile: {line}");
                            continue;
                        }

                        string file = tokens[1];

                        ulong fileHash = FileDataStore[fileDataId];

                        if (!CascFile.Files.ContainsKey(fileHash))
                            CascFile.Files.Add(fileHash, new CascFile(fileHash, file));
                        else
                            Logger.WriteLine($"Duplicate fileDataId {fileDataId} detected: {line}");

                        worker?.ReportProgress((int)(sr.BaseStream.Position / (float)sr.BaseStream.Length * 100));
                    }
                }

                Logger.WriteLine($"WowRootHandler: loaded {CascFile.Files.Count} valid file names");
            }
        }

        protected override CascFolder CreateStorageTree()
        {
            var root = new CascFolder("root");

            // Reset counts
            CountSelect = 0;
            UnknownFiles.Clear();

            // Create new tree based on specified locale
            foreach (var rootEntry in RootData)
            {
                var rootInfosLocale = rootEntry.Value.Where(re => (re.LocaleFlags & Locale) != LocaleFlags.None);

                if (rootInfosLocale.Count() > 1)
                {
                    IEnumerable<WowVfsRootEntry> rootInfosLocaleOverride = ContentFlagsFilterVfs.Filter(rootInfosLocale, OverrideArchive, PreferHighResTextures);

                    if (rootInfosLocaleOverride.Any())
                        rootInfosLocale = rootInfosLocaleOverride;
                }

                if (!rootInfosLocale.Any())
                    continue;

                string filename;

                ulong hash = FileDataStore[rootEntry.Key];

                if (!CascFile.Files.TryGetValue(hash, out CascFile file))
                {
                    filename = "unknown\\" + "FILEDATA_" + rootEntry.Key;
                    UnknownFiles.Add(hash);
                }
                else
                {
                    filename = file.FullName;
                }

                CreateSubTree(root, hash, filename);
                CountSelect++;
            }

            Logger.WriteLine("WowRootHandler: {0} file names missing for locale {1}", CountUnknown, Locale);

            return root;
        }
    }

    #endregion
}

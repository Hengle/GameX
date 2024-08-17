using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace GameX.Directorys.Casc
{
    #region Base

    public abstract class RootHandlerBase
    {
        protected readonly Jenkins96 Hasher = new Jenkins96();
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
        static readonly char[] PathDelimiters = new char[] { '/', '\\' };

        protected void CreateSubTree(CascFolder root, ulong filehash, string file)
        {
            var parts = file.Split(PathDelimiters);
            CascFolder folder = root;
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
                    if (entry == null) folder.Folders[entryName] = entry = new CASCFolder(entryName);
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
            using (var _ = new PerfCounter(GetType().Name + "::SetFlags()"))
            {
                Locale = locale;
                OverrideArchive = overrideArchive;
                PreferHighResTextures = preferHighResTextures;
                if (createTree) Root = CreateStorageTree();
                return Root;
            }
        }
    }

    #endregion

    struct D3RootEntry
    {
        public MD5Hash cKey;
        public int Type;
        public int SNO;
        public int FileIndex;
        public string Name;

        public static D3RootEntry Read(int type, BinaryReader s)
        {
            var e = new D3RootEntry() { Type = type, cKey = s.Read<MD5Hash>() };
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
        readonly MultiDictionary<ulong, RootEntry> RootData = new MultiDictionary<ulong, RootEntry>();
        readonly Dictionary<string, List<D3RootEntry>> D3RootData = new Dictionary<string, List<D3RootEntry>>();
        CoreTOCParser tocParser;
        PackagesParser pkgParser;

        public override int Count => RootData.Count;
        public override int CountTotal => RootData.Sum(re => re.Value.Count);

        public D3RootHandler(BinaryReader stream, BackgroundWorkerEx worker, CascHandler casc)
        {
            Log("Loading \"root\"...");
            var b1 = stream.ReadByte();
            var b2 = stream.ReadByte();
            var b3 = stream.ReadByte();
            var b4 = stream.ReadByte();
            var count = stream.ReadInt32();
            for (var j = 0; j < count; j++)
            {
                var md5 = stream.Read<MD5Hash>();
                var name = stream.ReadCString();
                var entries = new List<D3RootEntry>();
                D3RootData[name] = entries;
                if (!casc.Encoding.GetEntry(md5, out var enc)) continue;
                using (var s = new BinaryReader(casc.OpenFile(enc.Keys[0])))
                {
                    var magic = s.ReadUInt32();
                    var nEntries0 = s.ReadInt32();
                    for (var i = 0; i < nEntries0; i++) entries.Add(D3RootEntry.Read(0, s));
                    int nEntries1 = s.ReadInt32();
                    for (var i = 0; i < nEntries1; i++) entries.Add(D3RootEntry.Read(1, s));
                    var nNamedEntries = s.ReadInt32();
                    for (var i = 0; i < nNamedEntries; i++) entries.Add(D3RootEntry.Read(2, s));
                }
                Log?.ReportProgress((int)((j + 1) / (float)(count + 2) * 100));
            }

            // parse CoreTOC.dat
            var coreTocEntry = D3RootData["Base"].Find(e => e.Name == "CoreTOC.dat");
            casc.Encoding.GetEntry(coreTocEntry.cKey, out var enc1);
            using (var file = casc.OpenFile(enc1.Keys[0])) tocParser = new CoreTOCParser(file);
            Log.ReportProgress((int)((count + 1) / (float)(count + 2) * 100));
            // parse Packages.dat
            var pkgEntry = D3RootData["Base"].Find(e => e.Name == "Data_D3\\PC\\Misc\\Packages.dat");
            casc.Encoding.GetEntry(pkgEntry.cKey, out EncodingEntry enc2);
            using (var file = casc.OpenFile(enc2.Keys[0])) pkgParser = new PackagesParser(file);
            Log.ReportProgress(100);
        }

        public override void Clear()
        {
            RootData.Clear();
            D3RootData.Clear();
            tocParser = null;
            pkgParser = null;
            CascFile.Files.Clear();
        }

        public override void Dump(EncodingHandler encodingHandler = null) { }

        public override IEnumerable<KeyValuePair<ulong, RootEntry>> GetAllEntries()
        {
            foreach (var set in RootData)
                foreach (var entry in set.Value)
                    yield return new KeyValuePair<ulong, RootEntry>(set.Key, entry);
        }

        public override IEnumerable<RootEntry> GetAllEntries(ulong hash)
        {
            RootData.TryGetValue(hash, out List<RootEntry> result);
            if (result == null) yield break;
            foreach (var entry in result) yield return entry;
        }

        public override IEnumerable<RootEntry> GetEntries(ulong hash) => GetEntriesForSelectedLocale(hash);

        void AddFile(string pkg, D3RootEntry e)
        {
            string name;
            switch (e.Type)
            {
                case 0:
                    var sno1 = tocParser.GetSNO(e.SNO);
                    name = string.Format("{0}\\{1}{2}", sno1.GroupId, sno1.Name, sno1.Ext);
                    break;
                case 1:
                    var sno2 = tocParser.GetSNO(e.SNO);
                    name = string.Format("{0}\\{1}\\{2:D4}", sno2.GroupId, sno2.Name, e.FileIndex);
                    var ext = pkgParser.GetExtension(name);
                    if (ext != null) name += ext;
                    else { CountUnknown++; name += ".xxx"; }
                    break;
                case 2: name = e.Name; break;
                default: name = "Unknown"; break;
            }
            var entry = new RootEntry { cKey = e.cKey, LocaleFlags = Enum.TryParse(pkg, out var locale) ? locale : LocaleFlags.All };
            var fileHash = Hasher.ComputeHash(name);
            CascFile.Files[fileHash] = new CascFile(fileHash, name);
            RootData.Add(fileHash, entry);
        }

        public override void LoadListFile(string path, BackgroundWorkerEx worker = null)
        {
            Log.Progress(0, "Loading \"listfile\"...");
            Log.WriteLine("D3RootHandler: loading file names...");
            var numFiles = D3RootData.Sum(p => p.Value.Count);
            var i = 0;
            foreach (var kv in D3RootData)
                foreach (var e in kv.Value)
                {
                    AddFile(kv.Key, e);
                    Log.ReportProgress((int)(++i / (float)numFiles * 100));
                }
            Log.WriteLine("D3RootHandler: loaded {0} file names", i);
        }

        protected override CASCFolder CreateStorageTree()
        {
            var root = new CASCFolder("root");
            CountSelect = 0;
            // create new tree based on specified locale
            foreach (var rootEntry in RootData)
            {
                var rootInfosLocale = rootEntry.Value.Where(re => (re.LocaleFlags & Locale) != 0);
                if (!rootInfosLocale.Any()) continue;
                CreateSubTree(root, rootEntry.Key, CASCFile.Files[rootEntry.Key].FullName);
                CountSelect++;
            }
            Log.WriteLine("D3RootHandler: {0} file names missing extensions for locale {1}", CountUnknown, Locale);
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
        public unsafe struct TocHeader
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

        public unsafe CoreTocParser(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                var hdr = br.Read<TOCHeader>();
                for (int i = 0; i < NUM_SNO_GROUPS; i++)
                    if (hdr.entryCounts[i] > 0)
                    {
                        br.BaseStream.Position = hdr.entryOffsets[i] + Marshal.SizeOf(hdr);
                        for (var j = 0; j < hdr.entryCounts[i]; j++)
                        {
                            var snoGroup = (SNOGroup)br.ReadInt32();
                            var snoId = br.ReadInt32();
                            var pName = br.ReadInt32();
                            var oldPos = br.BaseStream.Position;
                            br.BaseStream.Position = hdr.entryOffsets[i] + Marshal.SizeOf(hdr) + 12 * hdr.entryCounts[i] + pName;
                            var name = br.ReadCString();
                            br.BaseStream.Position = oldPos;
                            snoDic.Add(snoId, new SNOInfoD3 { GroupId = snoGroup, Name = name, Ext = extensions[snoGroup] });
                        }
                    }
            }
        }

        public SNOInfoD3 GetSNO(int id) { snoDic.TryGetValue(id, out var sno); return sno; }
    }

    public class PackagesParser
    {
        readonly Dictionary<string, string> nameToExtDic = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public PackagesParser(Stream stream)
        {
            using (var br = new BinaryReader(stream))
            {
                var sign = br.ReadInt32();
                var namesCount = br.ReadInt32();
                for (var i = 0; i < namesCount; i++)
                {
                    var name = br.ReadCString();
                    nameToExtDic[name.Substring(0, name.Length - 4)] = Path.GetExtension(name);
                }
            }
        }

        public string GetExtension(string partialName) { nameToExtDic.TryGetValue(partialName, out var ext); return ext; }
    }

    #endregion
}

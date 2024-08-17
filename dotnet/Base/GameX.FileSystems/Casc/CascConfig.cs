using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameX.FileSystems.Casc
{
    [Flags]
    public enum LoadFlags
    {
        All = -1,
        None = 0,
        Download = 1,
        Install = 2,
        FileIndex = 4
    }

    public class VerBarConfig
    {
        readonly List<Dictionary<string, string>> Data = [];
        public int Count => Data.Count;
        public Dictionary<string, string> this[int index] => Data[index];
        public static VerBarConfig ReadVerBarConfig(Stream stream) { using var r = new StreamReader(stream); return ReadVerBarConfig(r); }
        public static VerBarConfig ReadVerBarConfig(TextReader reader)
        {
            var result = new VerBarConfig();
            var lineNum = 0;
            string[] fields = null;
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue; // skip empty lines and comments
                var tokens = line.Split(['|']);
                if (lineNum == 0) // keys
                {
                    fields = new string[tokens.Length];
                    for (var i = 0; i < tokens.Length; ++i)
                        fields[i] = tokens[i].Split(['!'])[0].Replace(" ", "");
                }
                else // values
                {
                    result.Data.Add([]);
                    for (var i = 0; i < tokens.Length; ++i)
                        result.Data[lineNum - 1].Add(fields[i], tokens[i]);
                }
                lineNum++;
            }
            return result;
        }
    }

    public class KeyValueConfig
    {
        readonly Dictionary<string, List<string>> m_data = [];
        public List<string> this[string key]
        {
            get { m_data.TryGetValue(key, out var ret); return ret; }
        }
        public IReadOnlyDictionary<string, List<string>> Values => m_data;
        public static KeyValueConfig ReadKeyValueConfig(Stream stream) { using var r = new StreamReader(stream); return ReadKeyValueConfig(r); }
        public static KeyValueConfig ReadKeyValueConfig(TextReader reader)
        {
            var result = new KeyValueConfig();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue; // skip empty lines and comments
                var tokens = line.Split(['='], StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length != 2) throw new Exception("KeyValueConfig: tokens.Length != 2");
                var values = tokens[1].Trim().Split([' '], StringSplitOptions.RemoveEmptyEntries);
                var valuesList = values.ToList();
                result.m_data.Add(tokens[0].Trim(), valuesList);
            }
            return result;
        }
    }

    public class CascConfig
    {
        KeyValueConfig CdnConfig;
        List<KeyValueConfig> _Builds;
        VerBarConfig BuildInfo;
        VerBarConfig CdnsData;
        VerBarConfig VersionsData;

        public string Region;
        public CascGameType GameType;
        public static bool ValidateData = true;
        public static bool ThrowOnFileNotFound = true;
        public static bool ThrowOnMissingDecryptionKey = true;
        public static bool UseWowTVfs = false;
        public static bool UseOnlineFallbackForMissingFiles = true;
        public static LoadFlags LoadFlags = LoadFlags.FileIndex;

        CascConfig() { }

        public static CascConfig LoadOnlineStorageConfig(string product, string region, bool useCurrentBuild = false, ILoggerOptions loggerOptions = null)
        {
            if (product == null) throw new ArgumentNullException(nameof(product));
            if (region == null) throw new ArgumentNullException(nameof(region));
            Logger.Init(loggerOptions);
            var config = new CascConfig { OnlineMode = true, Region = region, Product = product };
            using (var ribbit = new RibbitClient("us"))
            using (var cdnsStream = ribbit.GetProductInfoStream(product, ProductInfoType.Cdns))
            //using (var cdnsStream = CDNIndexHandler.OpenFileDirect(string.Format("http://us.patch.battle.net:1119/{0}/cdns", product)))
            {
                config.CdnsData = VerBarConfig.ReadVerBarConfig(cdnsStream);
            }
            using (var ribbit = new RibbitClient("us"))
            using (var versionsStream = ribbit.GetProductInfoStream(product, ProductInfoType.Versions))
            //using (var versionsStream = CDNIndexHandler.OpenFileDirect(string.Format("http://us.patch.battle.net:1119/{0}/versions", product)))
            {
                config.VersionsData = VerBarConfig.ReadVerBarConfig(versionsStream);
            }
            CdnCache.Init(config);
            config.GameType = CascGame.DetectGameByUid(product);
            if (File.Exists("fakecdnconfig"))
            {
                using var stream = new FileStream("fakecdnconfig", FileMode.Open);
                config.CdnConfig = KeyValueConfig.ReadKeyValueConfig(stream);
            }
            else if (File.Exists("fakecdnconfighash"))
            {
                var cdnKey = File.ReadAllText("fakecdnconfighash");
                using var stream = CdnIndexHandler.OpenConfigFileDirect(config, cdnKey);
                config.CdnConfig = KeyValueConfig.ReadKeyValueConfig(stream);
            }
            else
            {
                var cdnKey = config.GetVersionsVariable("CDNConfig").ToLower();
                //var cdnKey = "da4896ce91922122bc0a2371ee114423";
                using var stream = CdnIndexHandler.OpenConfigFileDirect(config, cdnKey);
                config.CdnConfig = KeyValueConfig.ReadKeyValueConfig(stream);
            }
            config.ActiveBuild = 0;
            config._Builds = [];
            if (config.CdnConfig["builds"] != null)
            {
                for (var i = 0; i < config.CdnConfig["builds"].Count; i++)
                    try
                    {
                        using var stream = CdnIndexHandler.OpenConfigFileDirect(config, config.CdnConfig["builds"][i]);
                        var cfg = KeyValueConfig.ReadKeyValueConfig(stream);
                        config._Builds.Add(cfg);
                    }
                    catch { Console.WriteLine("Failed to load build {0}", config.CdnConfig["builds"][i]); }
                if (useCurrentBuild)
                {
                    var curBuildKey = config.GetVersionsVariable("BuildConfig");
                    var buildIndex = config.CdnConfig["builds"].IndexOf(curBuildKey);
                    if (buildIndex != -1) config.ActiveBuild = buildIndex;
                }
            }
            if (File.Exists("fakebuildconfig"))
            {
                using var stream = new FileStream("fakebuildconfig", FileMode.Open);
                var cfg = KeyValueConfig.ReadKeyValueConfig(stream);
                config._Builds.Add(cfg);
            }
            else if (File.Exists("fakebuildconfighash"))
            {
                var buildKey = File.ReadAllText("fakebuildconfighash");
                using var stream = CdnIndexHandler.OpenConfigFileDirect(config, buildKey);
                var cfg = KeyValueConfig.ReadKeyValueConfig(stream);
                config._Builds.Add(cfg);
            }
            else
            {
                var buildKey = config.GetVersionsVariable("BuildConfig").ToLower();
                //var buildKey = "3b0517b51edbe0b96f6ac5ea7eaaed38";
                using var stream = CdnIndexHandler.OpenConfigFileDirect(config, buildKey);
                var cfg = KeyValueConfig.ReadKeyValueConfig(stream);
                config._Builds.Add(cfg);
            }
            return config;
        }

        public static CascConfig LoadLocalStorageConfig(string basePath, string product, ILoggerOptions loggerOptions = null)
        {
            if (basePath == null) throw new ArgumentNullException(nameof(basePath));
            if (product == null) throw new ArgumentNullException(nameof(product));
            var buildInfoPath = Path.Combine(basePath, ".build.info");
            if (!File.Exists(buildInfoPath)) throw new Exception("Local mode not supported for this game!");
            Logger.Init(loggerOptions);
            var config = new CascConfig { OnlineMode = false, BasePath = basePath, Product = product };
            using (var buildInfoStream = new FileStream(buildInfoPath, FileMode.Open))
                config.BuildInfo = VerBarConfig.ReadVerBarConfig(buildInfoStream);
            CascGameType gameType;
            if (!HasConfigVariable(config.BuildInfo, "Product"))
            {
                var detectedGameType = CascGame.DetectLocalGame(basePath, product, config.GetBuildInfoVariable("BuildKey"));
                if (detectedGameType.HasValue) gameType = detectedGameType.Value;
                else throw new Exception($"No product {product} found at {basePath}");
            }
            else
            {
                var productUid = config.GetBuildInfoVariable("Product") ?? throw new Exception($"No product {product} found at {basePath}");
                gameType = CascGame.DetectGameByUid(product);
            }
            config.GameType = gameType;
            var dataFolder = CascGame.GetDataFolder(config.GameType);
            config.ActiveBuild = 0;
            config._Builds = [];
            if (File.Exists("fakebuildconfig"))
            {
                using var stream = new FileStream("fakebuildconfig", FileMode.Open);
                var cfg = KeyValueConfig.ReadKeyValueConfig(stream);
                config._Builds.Add(cfg);
            }
            else if (File.Exists("fakebuildconfighash"))
            {
                var buildKey = File.ReadAllText("fakebuildconfighash");
                var buildCfgPath = Path.Combine(basePath, dataFolder, "config", buildKey[..2], buildKey.Substring(2, 2), buildKey);
                using var stream = new FileStream(buildCfgPath, FileMode.Open);
                config._Builds.Add(KeyValueConfig.ReadKeyValueConfig(stream));
            }
            else
            {
                var buildKey = config.GetBuildInfoVariable("BuildKey");
                //var buildKey = "5a05c58e28d0b2c3245954b6f4e2ae66";
                var buildCfgPath = Path.Combine(basePath, dataFolder, "config", buildKey[..2], buildKey.Substring(2, 2), buildKey);
                using var stream = new FileStream(buildCfgPath, FileMode.Open);
                config._Builds.Add(KeyValueConfig.ReadKeyValueConfig(stream));
            }
            if (File.Exists("fakecdnconfig"))
            {
                using var stream = new FileStream("fakecdnconfig", FileMode.Open);
                config.CdnConfig = KeyValueConfig.ReadKeyValueConfig(stream);
            }
            else if (File.Exists("fakecdnconfighash"))
            {
                var cdnKey = File.ReadAllText("fakecdnconfighash");
                var cdnCfgPath = Path.Combine(basePath, dataFolder, "config", cdnKey[..2], cdnKey.Substring(2, 2), cdnKey);
                using var stream = new FileStream(cdnCfgPath, FileMode.Open);
                config.CdnConfig = KeyValueConfig.ReadKeyValueConfig(stream);
            }
            else
            {
                var cdnKey = config.GetBuildInfoVariable("CDNKey");
                //var cdnKey = "23d301e8633baaa063189ca9442b3088";
                var cdnCfgPath = Path.Combine(basePath, dataFolder, "config", cdnKey[..2], cdnKey.Substring(2, 2), cdnKey);
                using var stream = new FileStream(cdnCfgPath, FileMode.Open);
                config.CdnConfig = KeyValueConfig.ReadKeyValueConfig(stream);
            }
            CdnCache.Init(config);
            return config;
        }

        public string BasePath { get; private set; }

        public bool OnlineMode { get; private set; }

        public int ActiveBuild { get; set; }

        public string VersionName { get { return GetBuildInfoVariable("Version") ?? GetVersionsVariable("VersionsName"); } }

        public string Product { get; private set; }

        public MD5Hash RootCKey => _Builds[ActiveBuild]["root"][0].FromHexString().ToMD5();

        public MD5Hash InstallCKey => _Builds[ActiveBuild]["install"][0].FromHexString().ToMD5();

        public string InstallSize => _Builds[ActiveBuild]["install-size"][0];

        public MD5Hash DownloadCKey => _Builds[ActiveBuild]["download"][0].FromHexString().ToMD5();

        public string DownloadSize => _Builds[ActiveBuild]["download-size"][0];

        //public MD5Hash PartialPriorityMD5 => _Builds[ActiveBuild]["partial-priority"][0].ToByteArray().ToMD5();

        //public string PartialPrioritySize => _Builds[ActiveBuild]["partial-priority-size"][0];

        public MD5Hash EncodingCKey => _Builds[ActiveBuild]["encoding"][0].FromHexString().ToMD5();

        public MD5Hash EncodingEKey => _Builds[ActiveBuild]["encoding"][1].FromHexString().ToMD5();

        public string EncodingSize => _Builds[ActiveBuild]["encoding-size"][0];

        public MD5Hash PatchEKey => _Builds[ActiveBuild]["patch"][0].FromHexString().ToMD5();

        public string PatchSize => _Builds[ActiveBuild]["patch-size"][0];

        public string BuildUID => _Builds[ActiveBuild]["build-uid"][0];

        public string BuildProduct => _Builds[ActiveBuild]["build-product"][0];

        public string BuildName => _Builds[ActiveBuild]["build-name"][0];

        public bool IsVfsRoot => _Builds[ActiveBuild]["vfs-root"] != null;

        public MD5Hash VfsRootCKey => _Builds[ActiveBuild]["vfs-root"][0].FromHexString().ToMD5();

        public MD5Hash VfsRootEKey => _Builds[ActiveBuild]["vfs-root"][1].FromHexString().ToMD5();

        public List<(MD5Hash CKey, MD5Hash EKey)> VfsRootList => GetVfsRootList();

        List<(MD5Hash CKey, MD5Hash EKey)> GetVfsRootList()
        {
            if (!IsVfsRoot) return null;
            var list = new List<(MD5Hash CKey, MD5Hash EKey)>();
            var build = _Builds[ActiveBuild];
            var regex = new Regex("(^vfs-\\d+$)");
            foreach (var kvp in build.Values)
            {
                Match match = regex.Match(kvp.Key);
                if (match.Success) list.Add((kvp.Value[0].FromHexString().ToMD5(), kvp.Value[1].FromHexString().ToMD5()));
            }
            return list;
        }

        int cdnHostIndex;

        public string CDNHost
        {
            get
            {
                if (OnlineMode)
                {
                    var hosts = GetCdnsVariable("Hosts").Split(' ');

                    if (cdnHostIndex >= hosts.Length)
                        cdnHostIndex = 0;

                    return hosts[cdnHostIndex++];
                }
                else
                {
                    return GetBuildInfoVariable("CDNHosts").Split(' ')[0];
                }
            }
        }

        public string CDNPath => OnlineMode ? GetCdnsVariable("Path") : GetBuildInfoVariable("CDNPath");

        public string CDNUrl
        {
            get
            {
                if (OnlineMode)
                    return string.Format("http://{0}/{1}", GetCdnsVariable("Hosts").Split(' ')[0], GetCdnsVariable("Path"));
                else
                    return string.Format("http://{0}{1}", GetBuildInfoVariable("CDNHosts").Split(' ')[0], GetBuildInfoVariable("CDNPath"));
            }
        }

        public string GetBuildInfoVariable(string varName) => GetConfigVariable(BuildInfo, "Product", Product, varName);

        public string GetVersionsVariable(string varName) => GetConfigVariable(VersionsData, "Region", Region, varName);

        public string GetCdnsVariable(string varName) => GetConfigVariable(CdnsData, "Name", Region, varName);

        private static bool HasConfigVariable(VerBarConfig config, string varName) => config[0].ContainsKey(varName);

        private static string GetConfigVariable(VerBarConfig config, string filterParamName, string filterParamValue, string varName)
        {
            if (config == null)
                return null;

            if (config.Count == 1 || !HasConfigVariable(config, filterParamName))
            {
                if (config[0].TryGetValue(varName, out string varValue))
                    return varValue;
                return null;
            }

            for (int i = 0; i < config.Count; i++)
            {
                var cfg = config[i];
                if (cfg.TryGetValue(filterParamName, out string paramValue) && paramValue == filterParamValue && cfg.TryGetValue(varName, out string varValue))
                    return varValue;
            }
            return null;
        }

        public List<string> Archives => CdnConfig["archives"];

        public string ArchiveGroup => CdnConfig["archive-group"][0];

        public List<string> PatchArchives => CdnConfig["patch-archives"];

        public string PatchArchiveGroup => CdnConfig["patch-archive-group"][0];

        public string FileIndex => CdnConfig["file-index"][0];

        public List<KeyValueConfig> Builds => _Builds;
    }
}

using System;
using System.IO;

namespace GameX.FileSystems.Casc
{
    public enum CascGameType
    {
        HotS,
        WoW,
        D3,
        S2,
        Agent,
        Hearthstone,
        Overwatch,
        Bna,
        Client,
        S1,
        WC3,
        Destiny2,
        D2R,
        Wlby,
        Viper,
        Odin,
        Lazarus,
        Fore,
        Zeus,
        Rtro,
        Anbs,
        D4,
        DRTL,
        DRTL2,
        WC1,
        WC2,
        Gryphon
    }

    public class CascGame
    {
        public static CascGameType? DetectLocalGame(string path, string product, string buildKey)
        {
            string[] dirs = Directory.GetDirectories(path, "*", SearchOption.AllDirectories);

            foreach (var dir in dirs)
            {
                string buildCfgPath = Path.Combine(dir, buildKey);
                if (File.Exists(buildCfgPath))
                {
                    using (Stream stream = new FileStream(buildCfgPath, FileMode.Open))
                    {
                        KeyValueConfig cfg = KeyValueConfig.ReadKeyValueConfig(stream);
                        string buildUid = cfg["build-uid"][0];
                        if (buildUid != product)
                            return null;
                        return DetectGameByUid(cfg["build-uid"][0]);
                    }
                }
            }
            return null;
        }

        public static CascGameType DetectGameByUid(string uid)
        {
            return uid switch
            {
                _ when uid.StartsWith("hero") => CascGameType.HotS,
                _ when uid.StartsWith("hs") => CascGameType.Hearthstone,
                _ when uid.StartsWith("w3") => CascGameType.WC3,
                _ when uid.StartsWith("s1") => CascGameType.S1,
                _ when uid.StartsWith("s2") => CascGameType.S2,
                _ when uid.StartsWith("wow") => CascGameType.WoW,
                _ when uid.StartsWith("d3") => CascGameType.D3,
                _ when uid.StartsWith("agent") => CascGameType.Agent,
                _ when uid.StartsWith("pro") => CascGameType.Overwatch,
                _ when uid.StartsWith("bna") => CascGameType.Bna,
                _ when uid.StartsWith("clnt") => CascGameType.Client,
                _ when uid.StartsWith("dst2") => CascGameType.Destiny2,
                _ when uid.StartsWith("osi") => CascGameType.D2R,
                _ when uid.StartsWith("wlby") => CascGameType.Wlby,
                _ when uid.StartsWith("viper") => CascGameType.Viper,
                _ when uid.StartsWith("odin") => CascGameType.Odin,
                _ when uid.StartsWith("lazr") => CascGameType.Lazarus,
                _ when uid.StartsWith("fore") => CascGameType.Fore,
                _ when uid.StartsWith("zeus") => CascGameType.Zeus,
                _ when uid.StartsWith("rtro") => CascGameType.Rtro,
                _ when uid.StartsWith("anbs") => CascGameType.Anbs,
                _ when uid.StartsWith("fenris") => CascGameType.D4,
                _ when uid.StartsWith("drtl2") => CascGameType.DRTL2,
                _ when uid.StartsWith("drtl") => CascGameType.DRTL,
                _ when uid.StartsWith("war1") => CascGameType.WC1,
                _ when uid.StartsWith("w2bn") => CascGameType.WC2,
                _ when uid.StartsWith("gryphon") => CascGameType.Gryphon,
                _ => throw new Exception("Unable to detect game type by uid")
            };
        }

        public static string GetDataFolder(CascGameType gameType)
        {
            return gameType switch
            {
                CascGameType.HotS => "HeroesData",
                CascGameType.S2 => "SC2Data",
                CascGameType.Hearthstone => "Hearthstone_Data",
                CascGameType.WoW or CascGameType.D3 or CascGameType.D4 or CascGameType.WC3 or CascGameType.D2R => "Data",
                CascGameType.Odin => "Data",
                CascGameType.Overwatch => "data/casc",
                _ => throw new Exception("GetDataFolder called with unsupported gameType")
            };
        }

        public static bool SupportsLocaleSelection(CascGameType gameType)
        {
            return gameType is CascGameType.D3 or
                CascGameType.WoW or
                CascGameType.HotS or
                CascGameType.S2 or
                CascGameType.S1 or
                CascGameType.Overwatch;
        }
    }
}

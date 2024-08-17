using CommandLine;
using GameX.Formats.Collada;
using GameX.Formats.Unknown;
using GameX.Formats.Wavefront;
using System;
using System.IO;

namespace GameX.App.Cli
{
    // test
    // list -e "Tes"
    // import -e "Tes" -u "game:/Oblivion*.bsa#Oblivion" --path "D:\T_\Test2"
    // export -e "Tes" -u "game:/Oblivion*.bsa#Oblivion" --path "D:\T_\Test2"
    // xsport -e "Tes" -u "game:/Oblivion*.bsa#Oblivion" --path "D:\T_\Test2"
    partial class Program
    {
        #region ProgramState

        static class ProgramState
        {
            public static T Load<T>(Func<byte[], T> action, T defaultValue)
            {
                try
                {
                    if (File.Exists(@".\lastChunk.txt"))
                    {
                        using var s = File.Open(@".\lastChunk.txt", FileMode.Open);
                        var data = new byte[s.Length];
                        s.Read(data, 0, (int)s.Length);
                        return action(data);
                    }
                }
                catch { }
                return defaultValue;
            }

            public static void Store(Func<byte[]> action)
            {
                try
                {
                    var data = action();
                    using var s = new FileStream(@".\lastChunk.txt", FileMode.Create, FileAccess.Write);
                    s.Write(data, 0, data.Length);
                }
                catch { Clear(); }
            }

            public static void Clear()
            {
                try
                {
                    if (File.Exists(@".\lastChunk.txt")) File.Delete(@".\lastChunk.txt");
                }
                catch { }
            }
        }

        #endregion

        static readonly string[] test00 = ["test"];
                
        static readonly string[] args00 = ["list"];
        static readonly string[] args01 = ["list", "-f", "Red"];
        static readonly string[] args02 = ["list", "-f", "Tes", "-u", "game:/Oblivion*.bsa#Oblivion"];
        static readonly string[] args03 = ["list", "-f", "Tes", "-u", "file:///D:/T_/Oblivion/Oblivion*.bsa#Oblivion"];
                
        static readonly string[] dev00 = ["list", "-f", "Unity", "-u", "game:/resources.assets#Cities"];
                
        static readonly string[] dev0zb = ["list", "-f", "Unity", "-u", @"file:///C:/T_/Unity/Assets/myscene.unity#AmongUs"];
                
        static readonly string[] dev01a = ["list", "-f", "Unity", "-u", @"game:/StreamingAssets\aa\Steam\StandaloneWindows\6fcbc56bf87ce16ef93cf7950ec3d7c9_unitybuiltinshaders_e998f854a714e8c70679af5d74e29f20.bundle#AmongUs"];
        static readonly string[] dev01aa = ["list", "-f", "Unity", "-u", @"game:/StreamingAssets\aa\Steam\StandaloneWindows\initialmaps_assets_all.bundle#AmongUs"];
        static readonly string[] dev01b = ["list", "-f", "Unity", "-u", @"game:/resources.assets#AmongUs"]; // asset.v22
        static readonly string[] dev01c = ["list", "-f", "Unity", "-u", @"game:/sharedassets2.assets#AmongUs"]; // asset.v22
        static readonly string[] dev01d = ["list", "-f", "Unity", "-u", @"game:/globalgamemanagers.assets#AmongUs"]; // asset.v22
                
        static readonly string[] dev02a = ["list", "-f", "Unity", "-u", @"game:/resources.assets#Cities"]; // asset.v17
        static readonly string[] dev02b = ["list", "-f", "Unity", "-u", @"game:/resources.assets#Cities"]; // asset.v17
        static readonly string[] dev02c = ["list", "-f", "Unity", "-u", @"game:/globalgamemanagers.assets#Cities"]; // asset.v17
                
        static readonly string[] dev03b = ["list", "-f", "Unity", "-u", @"game:/resources.assets#Tabletop"]; // asset.v21
        static readonly string[] dev03c = ["list", "-f", "Unity", "-u", @"game:/globalgamemanagers.assets#Tabletop"]; // asset.v21
                
        static readonly string[] dev04a = ["list", "-f", "Lith", "-u", @"game:/FEAR_1.Arch00#FEAR"];
                
        static readonly string[] argsRsi1 = ["export", "-f", "Rsi", "-u", "game:/Data.p4k#StarCitizen", "--path", @"D:\T_\StarCitizen"];
                
        static readonly string[] argsTes1 = ["export", "-f", "Tes", "-u", "game:/Oblivion*.bsa#Oblivion", "--path", @"D:\T_\Oblivion"];
        static readonly string[] argsTes2 = ["import", "-f", "Tes", "-u", "game:/Oblivion*.bsa#Oblivion", "--path", @"D:\T_\Oblivion"];
         
        static readonly string[] argsRed1 = ["export", "-f", "Red", "-u", "game:/main.key#Witcher", "--path", @"D:\T_\Witcher"];
        static readonly string[] argsRed2 = ["export", "-f", "Red", "-u", "game:/krbr.dzip#Witcher2", "--path", @"D:\T_\Witcher2"];
                
        static readonly string[] argsArkane1 = ["list", "-f", "Arkane"];
        static readonly string[] argsArkane2 = ["export", "-f", "Arkane", "-u", "game:/*.pak#AF", "--path", @"C:\T_\AF"];
        static readonly string[] argsBlizzard1 = ["list", "-f", "Blizzard"];
        static readonly string[] argsBioware1 = ["list", "-f", "Bioware"];
        static readonly string[] argsValve1 = ["list", "-f", "Valve"];
        static readonly string[] argsValve2 = ["list", "-f", "Valve", "-u", "game:/*_dir.vpk#L4D"];
        static readonly string[] argsValve3 = ["export", "-f", "Valve", "-u", "game:/*_dir.vpk#L4D", "--path", @"~/T_/L4D"];
        static readonly string[] argsValveNull = ["export", "-f", "Valve", "-u", "game:/*_dir.vpk#L4D", "--path", "null"];
        static readonly string[] argsIW1 = ["list", "-f", "IW"];

        static void Register()
        {
            UnknownFileWriter.Factories["Collada"] = file => new ColladaFileWriter(file);
            UnknownFileWriter.Factories["Wavefront"] = file => new WavefrontFileWriter(file);
            UnknownFileWriter.Factories["default"] = UnknownFileWriter.Factories["Wavefront"];
        }

        static void Main(string[] args)
        {
            Register();
            Parser.Default.ParseArguments<TestOptions, ListOptions, GetOptions, ImportOptions>(argsValveNull)
            .MapResult(
                (TestOptions opts) => RunTestAsync(opts).GetAwaiter().GetResult(),
                (ListOptions opts) => RunListAsync(opts).GetAwaiter().GetResult(),
                (GetOptions opts) => RunGetAsync(opts).GetAwaiter().GetResult(),
                (ImportOptions opts) => RunImportAsync(opts).GetAwaiter().GetResult(),
                errs => 1);
        }

        internal static string GetPlatformPath(string path)
            => string.Compare(path, "null", true) == 0 ? null
            : path.StartsWith('~') ? path[1..].Insert(0, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            : path;
    }
}
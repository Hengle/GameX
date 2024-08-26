using CommandLine;
using GameX.Platforms;
using System.Windows;
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

// https://www.wpf-tutorial.com/data-binding/debugging/
namespace GameX.App.Explorer
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        static App() => Platform.Startups.Add(OpenGLPlatform.Startup);

        static readonly string[] args = [];
        //static readonly string[] args = ["open", "-f", "Arkane", "-u", "game:/readme.txt#AF"];
        //static readonly string[] args = ["open", "-f", "WB", "-u", "game:/client_portal.dat#AC", "-p", "01000001.obj"];
        //static readonly string[] args = ["open", "-f", "WB", "-u", "game:/client_portal.dat#AC", "-p", "02000001.set"];
        //static readonly string[] args = ["open", "-f", "WB", "-u", "game:/client_portal.dat#AC", "-p", "03000001.obj"];
        //static readonly string[] args = ["open", "-f", "WB", "-u", "game:/client_portal.dat#AC", "-p", "0400008E.pal"];
        //static readonly string[] args = ["open", "-f", "Red", "-u", "game:/basegame_2_mainmenu.archive#CP77"];;
        //static readonly string[] args = ["open", "-f", "Red", "-u", "game:/basegame_1_engine.archive#CP77"];
        //static readonly string[] args = ["open", "-f", "Red", "-u", "game:/lang_en_text.archive#CP77"];
        //static readonly string[] args = ["open", "-f", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "materials/models/npc_minions/siege1_color_psd_12a9c12b.vtex_c"];
        //static readonly string[] args = ["open", "-f", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "materials/models/npc_minions/siege1.vmat_c"];
        //static readonly string[] args = ["open", "-f", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "materials/startup_background_color_png_65ffcfa7.vtex_c"];
        //static readonly string[] args = ["open", "-f", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "materials/startup_background.vmat_c"];
        //static readonly string[] args = ["open", "-f", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "models/npc_minions/draft_siege_good_reference.vmesh_c"];
        //static readonly string[] args = ["open", "-f", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "models/npc_minions/draft_siege_good.vmdl_c"];
        //static readonly string[] args = ["open", "-f", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "particles/hw_fx/candy_carrying_overhead.vpcf_c"];

        void Application_Startup(object sender, StartupEventArgs e)
        {
            //GLViewerControl.ShowConsole = true;
            _ = new AppShell();
            Parser.Default.ParseArguments<DefaultOptions, TestOptions, OpenOptions>(args ?? e.Args)
            .MapResult(
                (DefaultOptions opts) => RunDefault(opts),
                (TestOptions opts) => RunTest(opts),
                (OpenOptions opts) => RunOpen(opts),
                RunError);
        }

        #region Options

        [Verb("default", true, HelpText = "Default action.")]
        class DefaultOptions { }

        [Verb("test", HelpText = "Test fixture.")]
        class TestOptions { }

        [Verb("open", HelpText = "Extract files contents to folder.")]
        class OpenOptions
        {
            [Option('f', "family", HelpText = "Family", Required = true)]
            public string Family { get; set; }

            [Option('u', "uri", HelpText = "Pak file to be opened", Required = true)]
            public Uri Uri { get; set; }

            [Option('p', "path", HelpText = "optional file to be opened")]
            public string Path { get; set; }
        }

        #endregion

        static int RunDefault(DefaultOptions opts) => AppShell.Current.Startup();

        static int RunTest(TestOptions opts) => AppShell.Current.Startup();

        static int RunOpen(OpenOptions opts) => AppShell.Current.StartupOpen(FamilyManager.GetFamily(opts.Family), [opts.Uri], opts.Path);

        static int RunError(IEnumerable<Error> errs) { MessageBox.Show("Errors: \n\n" + errs.First()); Current.Shutdown(1); return 1; }
    }
}

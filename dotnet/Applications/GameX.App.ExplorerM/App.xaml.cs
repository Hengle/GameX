using CommandLine;
using GameX.Platforms;

namespace GameX.App.Explorer
{
    public partial class App : Application
    {
        static App() => Platforms.Platform.Startups.Add(OpenGLPlatform.Startup);

        static readonly string[] args = [];
        //static readonly string[] args = ["open", "-e", "AC", "-u", "game:/client_portal.dat#AC", "-p", "01000001.obj"];
        //static readonly string[] args = ["open", "-e", "AC", "-u", "game:/client_portal.dat#AC", "-p", "02000001.set"];
        //static readonly string[] args = ["open", "-e", "AC", "-u", "game:/client_portal.dat#AC", "-p", "03000001.obj"];
        //static readonly string[] args = ["open", "-e", "AC", "-u", "game:/client_portal.dat#AC", "-p", "0400008E.pal"];
        //static readonly string[] args = ["open", "-e", "Red", "-u", "game:/basegame_2_mainmenu.archive#CP77"];
        //static readonly string[] args = ["open", "-e", "Red", "-u", "game:/basegame_1_engine.archive#CP77"];
        //static readonly string[] args = ["open", "-e", "Red", "-u", "game:/lang_en_text.archive#CP77"];
        //static readonly string[] args = ["open", "-e", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "materials/models/npc_minions/siege1_color_psd_12a9c12b.vtex_c"];
        //static readonly string[] args = ["open", "-e", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "materials/models/npc_minions/siege1.vmat_c"];
        //static readonly string[] args = ["open", "-e", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "materials/startup_background_color_png_65ffcfa7.vtex_c"];
        //static readonly string[] args = ["open", "-e", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "materials/startup_background.vmat_c"];
        //static readonly string[] args = ["open", "-e", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "models/npc_minions/draft_siege_good_reference.vmesh_c"];
        //static readonly string[] args = ["open", "-e", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "models/npc_minions/draft_siege_good.vmdl_c"];
        //static readonly string[] args = ["open", "-e", "Valve", "-u", "game:/dota/pak01_dir.vpk#Dota2", "-p", "particles/hw_fx/candy_carrying_overhead.vpcf_c"];

        public App()
        {
            InitializeComponent();
            MainPage = new AppShell();
        }

        protected override void OnStart()
        {
            if (!HasPermissions()) return;
            //GLViewerControl.ShowConsole = true;
            Parser.Default.ParseArguments<DefaultOptions, TestOptions, OpenOptions>(args)
            .MapResult(
                (DefaultOptions opts) => RunDefault(opts),
                (TestOptions opts) => RunTest(opts),
                (OpenOptions opts) => RunOpen(opts),
                RunError);
            base.OnStart();
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

        int RunDefault(DefaultOptions opts) => (Shell.Current as AppShell).Startup();

        int RunTest(TestOptions opts) => (Shell.Current as AppShell).Startup();

        int RunOpen(OpenOptions opts) => (Shell.Current as AppShell).StartupOpen(FamilyManager.GetFamily(opts.Family), [opts.Uri], opts.Path);

        int RunError(IEnumerable<Error> errs)
        {
            MainPage.DisplayAlert("Alert", $"Errors: \n\n {errs.First()}", "Cancel").Wait();
            //Current.Shutdown(1);
            return 1;
        }
    }
}
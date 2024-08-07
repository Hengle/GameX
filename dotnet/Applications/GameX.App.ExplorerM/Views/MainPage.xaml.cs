using static GameX.FamilyManager;

namespace GameX.App.Explorer.Views
{
    /// <summary>
    /// MainPageTab
    /// </summary>
    public class MainPageTab
    {
        public string Name { get; set; }
        public PakFile PakFile { get; set; }
        public string Text { get; set; }
    }

    public partial class MainPage : ContentPage
    {
        public static MainPage Instance;

        public MainPage()
        {
            InitializeComponent();
            Instance = this;
            BindingContext = this;
        }

        // https://dev.to/davidortinau/making-a-tabbar-or-segmentedcontrol-in-net-maui-54ha
        void MainTab_Changed(object sender, CheckedChangedEventArgs e) => MainTabContent.BindingContext = ((RadioButton)sender).BindingContext;

        public void Open(Family family, IEnumerable<Uri> pakUris, string path = null)
        {
            foreach (var pakFile in PakFiles) pakFile?.Dispose();
            PakFiles.Clear();
            if (family == null) return;
            foreach (var pakUri in pakUris)
            {
                Log.WriteLine($"Opening {pakUri}");
                var pak = family.OpenPakFile(pakUri);
                if (pak != null) PakFiles.Add(pak);
            }
            Log.WriteLine("Done");
            OnOpenedAsync(family, path).Wait();
        }

        public static readonly BindableProperty MainTabsProperty = BindableProperty.Create(nameof(MainTabs), typeof(IList<MainPageTab>), typeof(MainPage),
            propertyChanged: (d, e, n) =>
            {
                var mainTab = ((MainPage)d).MainTab;
                var firstTab = mainTab.Children.FirstOrDefault() as RadioButton;
                if (firstTab != null) firstTab.IsChecked = true;
            });
        public IList<MainPageTab> MainTabs
        {
            get => (IList<MainPageTab>)GetValue(MainTabsProperty);
            set => SetValue(MainTabsProperty, value);
        }

        public readonly IList<PakFile> PakFiles = new List<PakFile>();

        public Task OnOpenedAsync(Family family, string path = null)
        {
            var tabs = PakFiles.Select(pakFile => new MainPageTab
            {
                Name = pakFile.Name,
                PakFile = pakFile,
            }).ToList();
            //var firstPakFile = tabs.FirstOrDefault()?.PakFile ?? PakFile.Empty;
            //if (FamilyApps.Count > 0)
            //    tabs.Add(new MainPageTab
            //    {
            //        Name = "Apps",
            //        PakFile = firstPakFile,
            //        AppList = FamilyApps.Values.ToList(),
            //        Text = "Choose an application.",
            //    });
            if (!string.IsNullOrEmpty(family.Description))
                tabs.Add(new MainPageTab
                {
                    Name = "Information",
                    Text = family.Description,
                });
            MainTabs = tabs;
            return Task.CompletedTask;
        }

        //void App_Click(object sender, RoutedEventArgs e)
        //{
        //    var button = (Button)sender;
        //    var app = (FamilyApp)button.DataContext;
        //    app.OpenAsync(app.ExplorerType, Manager).Wait();
        //}

        internal void OnReady()
        {
            //if (!string.IsNullOrEmpty(Option.ForcePath) && Option.ForcePath.StartsWith("app:") && FamilyApps != null && FamilyApps.TryGetValue(Option.ForcePath[4..], out var app))
            //    App_Click(new Button { DataContext = app }, null);
            OpenPage_Click(null, null);
        }

        #region Menu

        void OpenPage_Click(object sender, EventArgs e)
        {
            var openPage = new OpenPage();
            openPage.OnReady();
            Navigation.PushModalAsync(openPage).Wait();
        }

        void OptionsPage_Click(object sender, EventArgs e)
        {
            var optionsPage = new OptionsPage();
            Navigation.PushModalAsync(optionsPage).Wait();
        }

        void WorldMap_Click(object sender, EventArgs e)
        {
            //if (DatManager.CellDat == null || DatManager.PortalDat == null) return;
            //EngineView.ViewMode = ViewMode.Map;
        }

        void AboutPage_Click(object sender, EventArgs e)
        {
            var aboutPage = new AboutPage();
            Navigation.PushModalAsync(aboutPage).Wait();
        }

        void Guide_Click(object sender, EventArgs e)
        {
            //Process.Start(@"docs\index.html");
        }

        #endregion
    }
}
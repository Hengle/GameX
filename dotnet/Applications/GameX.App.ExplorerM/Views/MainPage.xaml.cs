using GameX.Meta;

namespace GameX.App.Explorer.Views
{
    /// <summary>
    /// MainPageTab
    /// </summary>
    public class MainPageTab
    {
        public string Name { get; set; }
        public PakFile PakFile { get; set; }
        public IList<FamilyApp> AppList { get; set; }
        public string Text { get; set; }
    }

    public partial class MainPage : ContentPage
    {
        public readonly static MetaManager Manager = ResourceManager.Current;
        public static MainPage Current;

        public MainPage()
        {
            InitializeComponent();
            Current = this;
            BindingContext = this;
        }

        // https://dev.to/davidortinau/making-a-tabbar-or-segmentedcontrol-in-net-maui-54ha
        void MainTab_Changed(object sender, CheckedChangedEventArgs e) => MainTabContent.BindingContext = ((RadioButton)sender).BindingContext;

        public MainPage Open(Family family, IEnumerable<Uri> pakUris, string path = null)
        {
            foreach (var pakFile in PakFiles) pakFile?.Dispose();
            PakFiles.Clear();
            if (family == null) return this;
            FamilyApps = family.Apps;
            foreach (var pakUri in pakUris)
            {
                Log.WriteLine($"Opening {pakUri}");
                var pak = family.OpenPakFile(pakUri);
                if (pak != null) PakFiles.Add(pak);
            }
            Log.WriteLine("Done");
            OnOpenedAsync(family, path).Wait();
            return this;
        }

        public static readonly BindableProperty MainTabsProperty = BindableProperty.Create(nameof(MainTabs), typeof(IList<MainPageTab>), typeof(MainPage),
            propertyChanged: (d, e, n) =>
            {
                var mainTab = ((MainPage)d).MainTab;
                if (mainTab.Children.FirstOrDefault() is RadioButton firstTab) firstTab.IsChecked = true;
            });
        public IList<MainPageTab> MainTabs
        {
            get => (IList<MainPageTab>)GetValue(MainTabsProperty);
            set => SetValue(MainTabsProperty, value);
        }

        public readonly IList<PakFile> PakFiles = [];
        public Dictionary<string, FamilyApp> FamilyApps;

        public Task OnOpenedAsync(Family family, string path = null)
        {
            //MainTabControl.SelectedIndex = 0; // family.Apps != null ? 1 : 0
            var tabs = PakFiles.Select(pakFile => new MainPageTab
            {
                Name = pakFile.Name,
                PakFile = pakFile,
            }).ToList();
            var firstPakFile = tabs.FirstOrDefault()?.PakFile ?? PakFile.Empty;
            if (FamilyApps.Count > 0)
                tabs.Add(new MainPageTab
                {
                    Name = "Apps",
                    PakFile = firstPakFile,
                    AppList = FamilyApps.Values.ToList(),
                    Text = "Choose an application.",
                });
            if (!string.IsNullOrEmpty(family.Description))
                tabs.Add(new MainPageTab
                {
                    Name = "Information",
                    Text = family.Description,
                });
            MainTabs = tabs;
            return Task.CompletedTask;
        }

        internal void App_Click(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var app = (FamilyApp)button.BindingContext;
            app.OpenAsync(app.ExplorerType, Manager).Wait();
        }
    }
}
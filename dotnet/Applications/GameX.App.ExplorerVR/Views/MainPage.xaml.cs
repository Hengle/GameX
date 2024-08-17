using Microsoft.Maui.Controls;
using GameX.Meta;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

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
                    AppList = [.. FamilyApps.Values],
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

        #region Menu

        internal void Ready()
        {
            OpenPage_Click(null, null);
        }

        void OpenPage_Click(object sender, EventArgs e)
        {
            var openPage = new OpenPage();
            openPage.OnReady();
            //App.Instance.MainPage = openPage;
            Navigation.PushAsync(openPage).Wait();
        }

        void OptionsPage_Click(object sender, EventArgs e)
        {
            var optionsPage = new OptionsPage();
            //Navigation.PushAsync(optionsPage).Wait();
        }

        void WorldMap_Click(object sender, EventArgs e)
        {
            //if (DatManager.CellDat == null || DatManager.PortalDat == null) return;
            //EngineView.ViewMode = ViewMode.Map;
        }

        void AboutPage_Click(object sender, EventArgs e)
        {
            var aboutPage = new AboutPage();
            //Navigation.PushModalAsync(aboutPage).Wait();
        }

        void Guide_Click(object sender, EventArgs e)
        {
            //Process.Start(@"docs\index.html");
        }

        #endregion
    }
}
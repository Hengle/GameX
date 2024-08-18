using GameX.Meta;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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

    /// <summary>
    /// Interaction logic for MainPage.xaml
    /// </summary>
    public partial class MainPage : Window, INotifyPropertyChanged
    {
        public readonly static MetaManager Manager = ResourceManager.Current;
        public static MainPage Current;

        public MainPage()
        {
            InitializeComponent();
            Current = this;
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

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

        IList<MainPageTab> _mainTabs = [];
        public IList<MainPageTab> MainTabs
        {
            get => _mainTabs;
            set { _mainTabs = value; OnPropertyChanged(); }
        }

        public readonly IList<PakFile> PakFiles = [];
        public Dictionary<string, FamilyApp> FamilyApps;

        public Task OnOpenedAsync(Family family, string path = null)
        {
            //MainTabControl.SelectedIndex = family.Apps != null ? 1 : 0;
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
                    AppList = [..FamilyApps.Values],
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

        #region Menu

        internal void App_Click(object sender, EventArgs e)
        {
            var button = (Button)sender;
            var app = (FamilyApp)button.DataContext;
            app.OpenAsync(app.ExplorerType, Manager).Wait();
        }

        internal void OpenPage_Click(object sender, RoutedEventArgs e)
        {
            var openPage = new OpenPage();
            if (openPage.ShowDialog() == true) Current.Open((Family)openPage.Family.SelectedItem, openPage.PakUris);
        }

        void OptionsPage_Click(object sender, RoutedEventArgs e)
        {
            var optionsPage = new OptionsPage();
            optionsPage.ShowDialog();
        }

        void WorldMap_Click(object sender, RoutedEventArgs e)
        {
            //if (DatManager.CellDat == null || DatManager.PortalDat == null) return;
            //EngineView.ViewMode = ViewMode.Map;
        }

        void AboutPage_Click(object sender, RoutedEventArgs e)
        {
            var aboutPage = new AboutPage();
            aboutPage.ShowDialog();
        }

        void Guide_Click(object sender, RoutedEventArgs e)
        {
            //Process.Start(@"docs\index.html");
        }

        #endregion
    }
}

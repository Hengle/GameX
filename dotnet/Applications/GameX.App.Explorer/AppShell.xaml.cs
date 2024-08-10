using GameX.App.Explorer.Views;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static GameX.FamilyManager;

namespace GameX.App.Explorer
{
    /// <summary>
    /// Interaction logic for AppShell.xaml
    /// </summary>
    public partial class AppShell : UserControl
    {
        public static AppShell Current;

        public AppShell()
        {
            //InitializeComponent();
            Current = this;
            new MainPage();
        }

        internal int Startup()
        {
            var p = MainPage.Current;
            p.Show();
            if (!string.IsNullOrEmpty(Option.ForcePath) && Option.ForcePath.StartsWith("app:") && p.FamilyApps != null && p.FamilyApps.TryGetValue(Option.ForcePath[4..], out var app))
                p.App_Click(new Button { DataContext = app }, null);
            p.OpenPage_Click(null, null);
            return 0;
        }

        internal int StartupOpen(Family family, IEnumerable<Uri> pakUris, string path = null)
        {
            var p = MainPage.Current;
            p.Show();
            p.Open(family, pakUris, path);
            return 0;
        }

        void OpenPage_Click(object sender, RoutedEventArgs e)
        {
            //var openPage = new OpenPage();
            //if (openPage.ShowDialog() == true) Instance.Open((Family)openPage.Family.SelectedItem, openPage.PakUris);
        }

        void OptionsPage_Click(object sender, RoutedEventArgs e)
        {
            //var optionsPage = new OptionsPage();
            //optionsPage.ShowDialog();
        }

        void WorldMap_Click(object sender, RoutedEventArgs e)
        {
            //if (DatManager.CellDat == null || DatManager.PortalDat == null) return;
            //EngineView.ViewMode = ViewMode.Map;
        }

        void AboutPage_Click(object sender, RoutedEventArgs e)
        {
            //var aboutPage = new AboutPage();
            //aboutPage.ShowDialog();
        }

        void Guide_Click(object sender, RoutedEventArgs e)
        {
            //Process.Start(@"docs\index.html");
        }
    }
}

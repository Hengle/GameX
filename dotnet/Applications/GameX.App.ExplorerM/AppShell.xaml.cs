using GameX.App.Explorer.Views;
using static GameX.FamilyManager;

namespace GameX.App.Explorer
{
    public partial class AppShell : Shell
    {
        public AppShell() => InitializeComponent();

        internal int Startup()
        {
            var p = MainPage.Current;
            if (!string.IsNullOrEmpty(Option.ForcePath) && Option.ForcePath.StartsWith("app:") && p.FamilyApps != null && p.FamilyApps.TryGetValue(Option.ForcePath[4..], out var app))
                p.App_Click(new Button { BindingContext = app }, null);
            Current.GoToAsync("//open");
            return 0;
        }

        internal int StartupOpen(Family family, IEnumerable<Uri> pakUris, string path = null)
        {
            var p = MainPage.Current;
            p.Open(family, pakUris, path);
            return 0;
        }
    }
}
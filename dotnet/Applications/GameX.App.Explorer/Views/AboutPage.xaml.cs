using System.Windows;

namespace GameX.App.Explorer.Views
{
    /// <summary>
    /// Interaction logic for AboutPage.xaml
    /// </summary>
    public partial class AboutPage : Window
    {
        public AboutPage() => InitializeComponent();

        void OK_Click(object sender, RoutedEventArgs e) => Close();
    }
}

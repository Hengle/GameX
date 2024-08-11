using GameX.Metadata;
using Microsoft.Maui.Controls;
using OpenStack.Gfx;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace GameX.App.Explorer.Views
{
    public partial class FileContent : ContentView
    {
        public static FileContent Instance;

        public FileContent()
        {
            InitializeComponent();
            Instance = this;
            BindingContext = this;
        }

        //void ContentTab_Changed(object sender, CheckedChangedEventArgs e) => ContentTabContent.BindingContext = ((RadioButton)sender).BindingContext;

        IOpenGfx _gfx;
        public IOpenGfx Gfx
        {
            get => _gfx;
            set { _gfx = value; OnPropertyChanged(); }
        }

        IList<MetadataContent> _contentTabs;
        public IList<MetadataContent> ContentTabs
        {
            get => _contentTabs;
            set { _contentTabs = value; OnPropertyChanged(); }
        }

        public void OnFileInfo(PakFile pakFile, List<MetadataInfo> infos)
        {
            if (ContentTabs != null) foreach (var dispose in ContentTabs.Where(x => x.Dispose != null).Select(x => x.Dispose)) dispose.Dispose();
            Gfx = pakFile.Gfx;
            ContentTabs = infos?.Select(x => x.Tag as MetadataContent).Where(x => x != null).ToList();
            //ContentTab.CurrentItem = ContentTabs != null ? ContentTabs.FirstOrDefault() : null;
        }
    }
}

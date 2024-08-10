using GameX.Meta;
using static GameX.FamilyManager;

namespace GameX.App.Explorer.Views
{
    public partial class FileExplorer : ContentView
    {
        public readonly static MetaManager Resource = ResourceManager.Current;
        public static FileExplorer Current;

        public FileExplorer()
        {
            InitializeComponent();
            Current = this;
            BindingContext = this;
        }

        public static readonly BindableProperty PakFileProperty = BindableProperty.Create(nameof(PakFile), typeof(PakFile), typeof(FileExplorer),
            propertyChanged: (d, e, n) =>
            {
                if (d is not FileExplorer fileExplorer || n is not PakFile pakFile) return;
                fileExplorer.Filters = pakFile.GetMetadataFilters(Resource);
                fileExplorer.Nodes = fileExplorer.PakNodes = pakFile.GetMetaItems(Resource);
                fileExplorer.Ready(pakFile);
            });
        public PakFile PakFile
        {
            get => (PakFile)GetValue(PakFileProperty);
            set => SetValue(PakFileProperty, value);
        }

        List<MetaItem.Filter> _filters;
        public List<MetaItem.Filter> Filters
        {
            get => _filters;
            set { _filters = value; OnPropertyChanged(); }
        }

        //void OnFilterKeyUp(object sender, EventArgs e)
        //{
        //    if (string.IsNullOrEmpty(Filter.SelectedItem as string)) Nodes = PakNodes;
        //    else Nodes = PakNodes.Select(x => x.Search(y => y.Name.Contains(Filter.SelectedItem as string))).Where(x => x != null).ToList();
        //    //var view = (CollectionView)CollectionViewSource.GetDefaultView(Node.ItemsSource);
        //    //view.Filter = o =>
        //    //{
        //    //    if (string.IsNullOrEmpty(Filter.Text)) return true;
        //    //    else return (o as MetaItem).Name.Contains(Filter.Text);
        //    //};
        //    //view.Refresh();
        //}

        void OnFilterSelected(object s, EventArgs e)
        {
            var filter = (MetaItem.Filter)Filter.SelectedItem;
            if (filter == null) Nodes = PakNodes;
            else Nodes = PakNodes.Select(x => x.Search(y => y.Name.Contains(filter.Description))).Where(x => x != null).ToList();
        }

        List<MetaItem> PakNodes;

        List<MetaItem> _nodes;
        public List<MetaItem> Nodes
        {
            get => _nodes;
            set { _nodes = value; OnPropertyChanged(); }
        }

        List<MetaInfo> _infos;
        public List<MetaInfo> Infos
        {
            get => _infos;
            set { _infos = value; OnPropertyChanged(); }
        }

        MetaItem _selectedItem;
        public MetaItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (_selectedItem == value) return;
                _selectedItem = value;
                if (value == null) { OnInfo(); return; }
                try
                {
                    var pak = (value.Source as FileSource)?.Pak;
                    if (pak != null)
                    {
                        if (pak.Status == PakFile.PakStatus.Opened) return;
                        pak.Open(value.Items, Resource);
                        //OnFilterKeyUp(null, null); //value.Items.AddRange(pak.GetMetaItemsAsync(Resource).Result);
                    }
                    OnInfo(value.PakFile?.GetMetaInfos(Resource, value).Result);
                }
                catch (Exception ex)
                {
                    OnInfo([
                        new MetaInfo($"EXCEPTION: {ex.Message}"),
                        new MetaInfo(ex.StackTrace),
                    ]);
                }
            }
        }

        public void OnInfoUpdated()
        {
        }

        public void OnInfo(IEnumerable<MetaInfo> infos = null)
        {
            FileContent.Current.OnInfo(PakFile, infos?.Where(x => x.Name == null).ToList());
            Infos = infos?.Where(x => x.Name != null).ToList();
        }

        void OnNodeSelected(object s, EventArgs args)
        {
            var parameter = ((TappedEventArgs)args).Parameter;
            if (parameter is MetaItem item && item.PakFile != null) SelectedItem = item;
            //e.Handled = true;
        }

        FileExplorer Ready(PakFile pakFile)
        {
            if (string.IsNullOrEmpty(Option.ForcePath) || Option.ForcePath.StartsWith("app:")) return this;
            var sample = Option.ForcePath.StartsWith("sample:") ? pakFile.Game.GetSample(Option.ForcePath[7..]) : null;
            var forcePath = sample != null ? sample.Path : Option.ForcePath;
            if (forcePath == null) return this;
            SelectedItem = MetaItem.FindByPathForNodes(PakNodes, forcePath, Resource);
            return this;
        }
    }
}
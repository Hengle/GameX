using GameX.Meta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using static GameX.FamilyManager;

namespace GameX.App.Explorer.Views
{
    /// <summary>
    /// Interaction logic for FileType.xaml
    /// </summary>
    public partial class FileExplorer : UserControl, INotifyPropertyChanged
    {
        public static MetaManager Resource = new ResourceManager();
        public static FileExplorer Instance;

        public FileExplorer()
        {
            InitializeComponent();
            Instance = this;
            DataContext = this;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        void OnPropertyChanged([CallerMemberName] string propertyName = "") => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public static readonly DependencyProperty PakFileProperty = DependencyProperty.Register(nameof(PakFile), typeof(PakFile), typeof(FileExplorer),
            new PropertyMetadata((d, e) =>
            {
                if (d is not FileExplorer fileExplorer || e.NewValue is not PakFile pakFile) return;
                fileExplorer.Filters = pakFile.GetMetadataFilters(Resource);
                fileExplorer.Nodes = new ObservableCollection<MetaItem>(fileExplorer.PakNodes = pakFile.GetMetaItems(Resource));
                fileExplorer.OnReady(pakFile);
            }));
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

        void OnFilterKeyUp(object sender, KeyEventArgs e)
        {
            if (string.IsNullOrEmpty(Filter.Text)) Nodes = new ObservableCollection<MetaItem>(PakNodes);
            else Nodes = new ObservableCollection<MetaItem>(PakNodes.Select(x => x.Search(y => y.Name.Contains(Filter.Text))).Where(x => x != null));
            //var view = (CollectionView)CollectionViewSource.GetDefaultView(Node.ItemsSource);
            //view.Filter = o =>
            //{
            //    if (string.IsNullOrEmpty(Filter.Text)) return true;
            //    else return (o as MetaItem).Name.Contains(Filter.Text);
            //};
            //view.Refresh();
        }

        void OnFilterSelected(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count <= 0) return;
            var filter = e.AddedItems[0] as MetaItem.Filter;
            if (string.IsNullOrEmpty(Filter.Text)) Nodes = new ObservableCollection<MetaItem>(PakNodes);
            else Nodes = new ObservableCollection<MetaItem>(PakNodes.Select(x => x.Search(y => y.Name.Contains(filter.Description))).Where(x => x != null));
        }

        List<MetaItem> PakNodes;

        ObservableCollection<MetaItem> _nodes;
        public ObservableCollection<MetaItem> Nodes
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
                if (value == null) { OnInfo(null); return; }
                try
                {
                    var pak = (value.Source as FileSource)?.Pak;
                    if (pak != null)
                    {
                        if (pak.Status == PakFile.PakStatus.Opened) return;
                        pak.Open(value.Items, Resource);
                        OnFilterKeyUp(null, null);
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

        public void OnInfo(IEnumerable<MetaInfo> infos)
        {
            FileContent.Instance.OnInfo(PakFile, infos?.Where(x => x.Name == null).ToList());
            Infos = infos?.Where(x => x.Name != null).ToList();
        }

        void OnNodeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Items.Count > 0) (item.Items[0] as TreeViewItem).IsSelected = true;
            else if (e.NewValue is MetaItem itemNode && itemNode.PakFile != null && SelectedItem != itemNode) SelectedItem = itemNode;
            e.Handled = true;
        }

        void OnReady(PakFile pakFile)
        {
            if (string.IsNullOrEmpty(Option.ForcePath) || Option.ForcePath.StartsWith("app:")) return;
            var sample = Option.ForcePath.StartsWith("sample:") ? pakFile.Game.GetSample(Option.ForcePath[7..]) : null;
            var forcePath = sample != null ? sample.Path : Option.ForcePath;
            if (forcePath == null) return;
            SelectedItem = MetaItem.FindByPathForNodes(PakNodes, forcePath, Resource);
        }
    }
}

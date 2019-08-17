using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.WindowsAPICodePack.Dialogs;
using Somewhere;
using StringHelper;

namespace SomewhereDesktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Constructor
        public MainWindow()
        {
            InitializeComponent();

            // Create popup resource
            Popup = new PopupSelectionWindow();
            // Create background worker search queue
            SearchKeywords = new ConcurrentBag<string>();
            // Parse command line arguments
            Arguments = ParseCommandlineArguments();
            // Create Commands object
            if (Arguments.ContainsKey("dir"))
                OpenRepository(Arguments["dir"]);
            else
                OpenRepository(Directory.GetCurrentDirectory());
        }
        private void OpenRepository(string homeFolderpath)
        {
            if (Commands != null)
                Commands.Dispose();

            // Open a new one
            Commands = new Commands(homeFolderpath);
            // Initialize items
            RefreshAllItems();
            RefreshItems();
            RefreshNotes();
            RefreshTags();
            // Update info
            InfoText = $"Home Directory: {Commands.HomeDirectory}; {Items.Count} items.";
        }
        #endregion

        #region Private Properties
        /// <summary>
        /// Key-value based command line arguments
        /// </summary>
        private Dictionary<string, string> Arguments { get; set; }
        /// <summary>
        /// Commands object
        /// </summary>
        private Commands Commands { get; set; }
        /// <summary>
        /// An in-memory cache of all items
        /// </summary>
        private List<FileItemObjectModel> AllItems { get; set; }
        /// <summary>
        /// An in-memory cache of all tags
        /// </summary>
        private List<string> AllTags { get; set; }
        #endregion

        #region Private View Routines
        private void RefreshAllItems()
        {
            // Update items list
            AllItems = Commands.GetFileDetails().Select(f => new FileItemObjectModel(f)).ToList();
            // Update type filters
            TypeFilters = new ObservableCollection<string>(AllItems
                .Select(i => GetItemExtensionType(i.Name)).Distinct().OrderBy(f => f));
            TypeFilters.Insert(0, "");    // Empty filter cleans filtering
        }
        private string GetItemExtensionType(string name)
        {
            // Knowledge
            if (name == null)
                return "Knowledge";
            // Folder
            else if (name.EndsWith("/") || name.EndsWith("\\"))
                return "Folder";
            else
            {
                string extension = System.IO.Path.GetExtension(name);
                if (string.IsNullOrEmpty(extension))
                    return "None";  // Can be either virtual note or file without extension
                else
                    return extension;
            }
        }
        private void RefreshItems()
            => Items = new ObservableCollection<FileItemObjectModel>(AllItems);
        private void RefreshNotes()
            => Notes = new ObservableCollection<FileItemObjectModel>(AllItems
                .Where(i => i.Name == null || i.Content != null));
        /// <summary>
        /// Refresh search all tags cache, search tags and tag filters so it is constrainted by currently displayed items
        /// </summary>
        private void RefreshTags()
        {
            // Update all tags
            AllTags = Items.SelectMany(t => t.TagsList).Distinct().ToList();
            // Update search tags
            SearchTags = new ObservableCollection<string>(AllTags);
            // Update filters
            TagFilters = new ObservableCollection<string>();
        }
        private void FilterItems()
        {
            // Refresh
            RefreshItems();
            // Filter by search name keyword
            if (!string.IsNullOrEmpty(_SearchNameKeyword))
                Items = new ObservableCollection<FileItemObjectModel>(
                    Items.Where(i => i.Name?
                    .ToLower()
                    .Contains(_SearchNameKeyword.ToLower()) ?? false));
            // Filter by type
            if (!string.IsNullOrEmpty(_SelectedTypeFilter))
                Items = new ObservableCollection<FileItemObjectModel>(
                    Items.Where(i => GetItemExtensionType(i.Name) == _SelectedTypeFilter));
            // Filter by tags
            if(TagFilters.Count != 0)            
                Items = new ObservableCollection<FileItemObjectModel>(
                    Items.Where(i => i.TagsList.Intersect(TagFilters).Count() == TagFilters.Count));
        }
        #endregion

        #region Public View Properties
        private string _InfoText;
        public string InfoText { get => _InfoText; set => SetField(ref _InfoText, value); }
        #endregion

        #region Inventory View Properties
        private ObservableCollection<FileItemObjectModel> _Items;
        /// <summary>
        /// Collection of items
        /// </summary>
        public ObservableCollection<FileItemObjectModel> Items
        {
            get => _Items;
            set => SetField(ref _Items, value);
        }
        private string _SelectedTypeFilter;
        public string SelectedTypeFilter
        {
            get => _SelectedTypeFilter;
            set
            {
                SetField(ref _SelectedTypeFilter, value);
                FilterItems();
                RefreshTags();
            }
        }
        private ObservableCollection<string> _TypeFilters;
        public ObservableCollection<string> TypeFilters { get => _TypeFilters; set => SetField(ref _TypeFilters, value); }
        private ObservableCollection<string> _TagFilters;
        public ObservableCollection<string> TagFilters
        {
            get => _TagFilters;
            set => SetField(ref _TagFilters, value);
        }
        private ObservableCollection<string> _SearchTags;
        /// <summary>
        /// Collection of tags
        /// </summary>
        public ObservableCollection<string> SearchTags
        {
            get => _SearchTags;
            set => SetField(ref _SearchTags, value);
        }
        private string _SearchTagKeyword;
        public string SearchTagKeyword
        {
            get => _SearchTagKeyword;
            set
            {
                SetField(ref _SearchTagKeyword, value);
                if (!string.IsNullOrEmpty(_SearchTagKeyword))
                    SearchTags = new ObservableCollection<string>(AllTags.Except(TagFilters).Where(t => t.ToLower().Contains(_SearchTagKeyword)));
                else
                    SearchTags = new ObservableCollection<string>(AllTags.Except(TagFilters));
            }
        }
        private string _SearchNameKeyword;
        public string SearchNameKeyword
        {
            get => _SearchNameKeyword;
            set
            {
                SetField(ref _SearchNameKeyword, value);
                FilterItems();
            }
        }
        private FileItemObjectModel _ActiveItem;
        public FileItemObjectModel ActiveItem
        {
            get => _ActiveItem;
            set
            {
                _ActiveItem = value;
                NotifyPropertyChanged("ActiveItemName");
                NotifyPropertyChanged("ActiveItemTags");
                NotifyPropertyChanged("ActiveItemMeta");
                NotifyPropertyChanged("ActiveItemContent");
                NotifyPropertyChanged("IsItemFieldReadOnly");
            }
        }
        public string ActiveItemMeta
        {
            get => ActiveItem?.Meta;
            set { ActiveItem.Meta = value; NotifyPropertyChanged(); CommitActiveItemChange(); ActiveItem.BroadcastPropertyChange(); }
        }
        public string ActiveItemName
        {
            get => ActiveItem?.Name;
            set { ActiveItem.Name = value; NotifyPropertyChanged(); CommitActiveItemChange(); ActiveItem.BroadcastPropertyChange(); }
        }

        public string ActiveItemTags
        {
            get => ActiveItem?.Tags;
            set { ActiveItem.Tags = value; NotifyPropertyChanged(); CommitActiveItemChange(); ActiveItem.BroadcastPropertyChange(); }
        }
        //public string ActiveItemContent
        //{
        //    get => ActiveItem?.Content;
        //    set { ActiveItem.Content = value; NotifyPropertyChanged(); CommitActiveItemChange(); ActiveItem.BroadcastPropertyChange(); }
        //}
        public bool IsItemFieldReadOnly
            => true;   // Don't allow edit for items
        #endregion

        #region Notebook View Properties
        private ObservableCollection<FileItemObjectModel> _Notes;
        public ObservableCollection<FileItemObjectModel> Notes
        {
            get => _Notes;
            set => SetField(ref _Notes, value);
        }
        private FileItemObjectModel _ActiveNote;
        public FileItemObjectModel ActiveNote
        {
            get => _ActiveNote;
            set
            {
                _ActiveNote = value;
                NotifyPropertyChanged("ActiveNoteName");
                NotifyPropertyChanged("ActiveNoteTags");
                NotifyPropertyChanged("ActiveNoteContent");
                NotifyPropertyChanged("IsNoteFieldEditEnabled");
            }
        }
        public string ActiveNoteName
        {
            get => ActiveNote?.Name;
            set { ActiveNote.Name = value; NotifyPropertyChanged(); CommitActiveNoteChange(); ActiveNote.BroadcastPropertyChange(); }
        }

        public string ActiveNoteTags
        {
            get => ActiveNote?.Tags;
            set { ActiveNote.Tags = value; NotifyPropertyChanged(); CommitActiveNoteChange(); ActiveNote.BroadcastPropertyChange(); }
        }
        //public string ActiveNoteContent
        //{
        //    get => ActiveNote?.Content;
        //    set { ActiveNote.Content = value; NotifyPropertyChanged(); CommitActiveNoteChange(); ActiveNote.BroadcastPropertyChange(); }
        //}
        public bool IsNameFieldEditEnabled
            => ActiveNote != null;
        #endregion

        #region Logs View Properties
        private string _LogsText;
        public string LogsText { get => _LogsText; set => SetField(ref _LogsText, value); }
        #endregion

        #region Status View Properties
        private string _StatusText;
        public string StatusText { get => _StatusText; set => SetField(ref _StatusText, value); }
        #endregion

        #region Setting View Properties
        private string _ConfigurationsText;
        public string ConfigurationsText { get => _ConfigurationsText; set => SetField(ref _ConfigurationsText, value); }
        #endregion

        #region Commands
        private void SearchCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = InventoryPanel.Visibility == Visibility.Visible;
        private void SearchCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
        }
        private void CloseWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;

        private void CloseWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Safety
            if (ActiveItem != null)
                CommitActiveItemChange();
            this.Close();
        }
        private void MaximizeWIndowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;

        private void MaximizeWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Safety
            if (ActiveItem != null)
                CommitActiveItemChange();
            if (this.WindowState == WindowState.Normal)
                this.WindowState = WindowState.Maximized;
            else this.WindowState = WindowState.Normal;
        }
        private void HideWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void HideWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Safety
            if (ActiveItem != null)
                CommitActiveItemChange();
            this.WindowState = WindowState.Minimized;
        }
        private void CreateNoteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = NotebookPanel.Visibility == Visibility.Visible;
        private void CreateNoteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Add to database
            int id = Commands.AddFile(null, null);
            // Add to view collection
            var item = new FileItemObjectModel(Commands.GetFileDetail(id));
            // Add to items cache
            AllItems.Add(item);
            // Update note collection
            RefreshNotes();
            // Set active
            ActiveNote = item;
        }
        private void OpenHomeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void OpenHomeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = Commands.HomeDirectory;
            dialog.IsFolderPicker = true;
            if(dialog.ShowDialog() == CommonFileDialogResult.Ok)
                OpenRepository(dialog.FileName);
        }
        #endregion

        #region Window Events
        private void RemoveTagFilter_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Update tag display lists
            string tag = (sender as TextBlock).Text as string;
            TagFilters.Remove(tag);
            SearchTags.Add(tag);
            // Perform filtering
            FilterItems();
        }
        private void AddTagFilter_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Update tag display lists
            string tag = (sender as TextBlock).Text as string;
            TagFilters.Add(tag);
            SearchTags.Remove(tag);
            // Perform filtering
            FilterItems();
        }
        private void RepositoryAddAllFiles_Click(object sender, RoutedEventArgs e)
        {
            Commands.Add("*");
            ShowUpdateStatusPanel();
        }
        private void TabHeader_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Get header name
            Label label = sender as Label;
            // Get available styles
            Style activeTitle = FindResource("Title") as Style;
            Style inactiveTitle = FindResource("TitleDim") as Style;
            // Reset styles
            InventoryTabLabel.Style = NotebookTabLabel.Style = SettingsTabLabel.Style 
                = LogsTabLabel.Style = StatusTabLabel.Style = NTFSSearchTabLabel.Style
                = KnowledgeTabLabel.Style
                = inactiveTitle;
            InventoryPanel.Visibility = NotebookPanel.Visibility = SettingsPanel.Visibility 
                = LogsPanel.Visibility = StatusPanel.Visibility = NTFSSearchPanel.Visibility
                = KnowledgePanel.Visibility
                = Visibility.Collapsed;
            // Toggle active panels, update header styles
            label.Style = activeTitle;
            if (label == InventoryTabLabel)
            {
                InventoryPanel.Visibility = Visibility.Visible;
            }
            else if (label == NotebookTabLabel)
            {
                NotebookPanel.Visibility = Visibility.Visible;
            }
            else if (label == SettingsTabLabel)
            {
                StringBuilder builder = new StringBuilder();
                foreach (var config in Commands.GetAllConfigurations())
                    builder.AppendLine($"{$"{config.Key}:",-60}{config.Value}{(string.IsNullOrEmpty(config.Comment) ? "" : $" - {config.Comment}")}");
                ConfigurationsText = builder.ToString();
                SettingsPanel.Visibility = Visibility.Visible;
            }
            else if (label == NTFSSearchTabLabel)
            {
                NTFSSearchPanel.Visibility = Visibility.Visible;
            }
            else if(label == LogsTabLabel)
            {
                StringBuilder builder = new StringBuilder();
                foreach (var log in Commands.GetAllLogs())
                {
                    LogEvent logEvent = log.LogEvent;
                    builder.AppendLine($"{log.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),-25}{logEvent.Command, -20}{string.Join(" ", logEvent.Arguments)}");
                    builder.AppendLine($"{logEvent.Result}");
                }
                ConfigurationsText = builder.ToString();
                SettingsPanel.Visibility = Visibility.Visible;
            }
            else if(label == KnowledgeTabLabel)
            {
                KnowledgePanel.Visibility = Visibility.Visible;
            }
            else if(label == StatusTabLabel)
                ShowUpdateStatusPanel();
            e.Handled = true;
        }
        private void ShowUpdateStatusPanel()
        {
            StringBuilder builder = new StringBuilder();
            foreach (var line in Commands.Status())
                builder.AppendLine(line);
            StatusText = builder.ToString();
            StatusPanel.Visibility = Visibility.Visible;
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
            => this.DragMove();
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Dispose resources
            Commands.Dispose();
            Popup.Close();
            LastWorker?.Dispose();
        }
        #endregion

        #region Searching Routines and Auxliary
        private int WorkerCount { get; } = 0;
        private PopupSelectionWindow Popup { get; }
        private BackgroundWorker LastWorker { get; set; }
        private ConcurrentBag<string> SearchKeywords { get; }
        /// <summary>
        /// Queue and start a new search
        /// </summary>
        private void QueueNewSearch(string keyword)
        {
            // Cancel last
            if (LastWorker.IsBusy)
                LastWorker.CancelAsync();
            // Start new
            SearchKeywords.Add(keyword);
            LastWorker = new BackgroundWorker();
            LastWorker.WorkerReportsProgress = false;
            LastWorker.WorkerSupportsCancellation = true;
            LastWorker.DoWork += BackgroundProcessQueueSearches();  // Notice it will automatically start
            LastWorker.RunWorkerAsync();
        }
        /// <summary>
        /// Process the last remaining queued item, and clear all others
        /// </summary>
        private DoWorkEventHandler BackgroundProcessQueueSearches()
        {
            throw new NotImplementedException();
        }
        #endregion

        #region Sub-Routines
        /// <remark>
        /// Parse command line arguments in the format "-key value" in pairs;
        /// Keys are converted to lowercase, and without dash; Values are case-sensitive
        /// </remark>
        private Dictionary<string, string> ParseCommandlineArguments()
        {
            // Get all arguments
            string[] args = Environment.GetCommandLineArgs();
            // Loop each argument, skipping the first
            Dictionary<string, string> arguments = new Dictionary<string, string>();
            for (int index = 1; index < args.Length; index += 2)
                if (args[index].Length > 1 && args[index][0] == '-')
                    arguments.Add(args[index].ToLower().Substring(1), args[index + 1]);
            return arguments;
        }
        private string GetArgumentValue(string arg)
        {
            if (Arguments.TryGetValue(arg, out string value))
                return value;
            else return null;
        }
        private string this[string arg]
            => GetArgumentValue(arg);
        private void CommitActiveItemChange()
        {
            // Commit to database
        }
        private void CommitActiveNoteChange()
        {
            // Commit to database
        }
        #endregion

        #region Data Binding
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void NotifyPropertyChanged([CallerMemberName]string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetField<type>(ref type field, type value, [CallerMemberName]string propertyName = null)
        {
            if (EqualityComparer<type>.Default.Equals(field, value)) return false;
            field = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
}
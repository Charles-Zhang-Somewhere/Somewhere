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

            // Instantiate a Commands object
            Commands = new Commands(homeFolderpath);
            // Validate home existence, if not, give options to open another one or create a new one
            if (!Commands.IsHomePresent)
            {
                var options = new string[] { "Create Home Repository Here", "Create Home Repository at A Different Place", "Open A Different Home Folder", "Close and Exit" };
                var dialog = new DialogWindow(null/*This window may not have been initialized yet*/, "Home Action", $"Home repository doesn't exist at path `{homeFolderpath}`, what would you like to do?", options);
                dialog.ShowDialog();
                // Create home at current path
                if (dialog.Selection == options[0])
                    CreateAndOpenNewHome(homeFolderpath);
                // Show new home dialog
                else if (dialog.Selection == options[1])
                    NewHomeCommand_Executed(null, null);
                // Show open home dialog
                else if (dialog.Selection == options[2])
                    OpenHomeCommand_Executed(null, null);
                else
                    this.Close();
                return;
            }
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
        private void UpdateItemPreview()
        {
            // Clear previous
            PreviewText = PreviewImage = PreviewStatus = PreviewMarkdown = null;
            // Return early in case of items refresh
            if (ActiveItem == null)
                return;
            // Preview knowledge and note (default as markdown)
            if (ActiveItem.Content != null || ActiveItem.Name == null)
            {
                PreviewMarkdown = ActiveItem.Content;
            }
            // Preview folder
            else if (ActiveItem.Name.EndsWith("/") || ActiveItem.Name.EndsWith("\\"))
            {
                StringBuilder builder = new StringBuilder("Folder Contents: " + Environment.NewLine);
                foreach (string fileEntry in Directory.EnumerateFileSystemEntries(System.IO.Path.Combine(Commands.HomeDirectory, ActiveItem.Name)))
                    builder.AppendLine($"{fileEntry}");
                PreviewText = builder.ToString();
            }
            // Preview files
            else
            {
                // Preview image
                string extension = System.IO.Path.GetExtension(ActiveItem.Name).ToLower();
                if (extension == ".png" || extension == ".img" || extension == ".jpg")
                    PreviewImage = Commands.GetPhysicalPath(ActiveItem.Name);
                // Preview markdown
                else if (extension == ".md")
                    PreviewMarkdown = File.ReadAllText(Commands.GetPhysicalPath(ActiveItem.Name));
                else
                    PreviewStatus = "No Preview Available for Binary Data";
            }
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
        private string _PreviewText;
        public string PreviewText { get => _PreviewText; set => SetField(ref _PreviewText, value); }
        private string _PreviewStatus;
        private string _PreviewMarkdown;
        public string PreviewMarkdown { get => _PreviewMarkdown; set => SetField(ref _PreviewMarkdown, value); }
        public string PreviewStatus { get => _PreviewStatus; set => SetField(ref _PreviewStatus, value); }
        private string _PreviewImage;
        public string PreviewImage { get => _PreviewImage; set => SetField(ref _PreviewImage, value); }
        private FileItemObjectModel _ActiveItem;
        public FileItemObjectModel ActiveItem
        {
            get => _ActiveItem;
            set
            {
                _ActiveItem = value;
                NotifyPropertyChanged("ActiveItemID");
                NotifyPropertyChanged("ActiveItemName");
                NotifyPropertyChanged("ActiveItemTags");
                NotifyPropertyChanged("ActiveItemMeta");
                NotifyPropertyChanged("ActiveItemContent");
                UpdateItemPreview();
            }
        }
        public string ActiveItemMeta
        {
            get => ActiveItem?.Meta;
            set { ActiveItem.Meta = value; NotifyPropertyChanged(); CommitActiveItemChange(); ActiveItem.BroadcastPropertyChange(); }
        }
        public int ActiveItemID
        {
            get => ActiveItem?.ID ?? 0;
            set { ActiveItem.ID = value; NotifyPropertyChanged(); CommitActiveItemChange(); ActiveItem.BroadcastPropertyChange(); }
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
                NotifyPropertyChanged("ActiveNoteID");
                NotifyPropertyChanged("ActiveNoteName");
                NotifyPropertyChanged("ActiveNoteTags");
                NotifyPropertyChanged("ActiveNoteContent");
                NotifyPropertyChanged("IsNoteFieldEditEnabled");
            }
        }
        private string _SearchNotebookKeyword;
        public string SearchNotebookKeyword
        {
            get => _SearchNotebookKeyword;
            set
            {
                SetField(ref _SearchNotebookKeyword, value);
                if (string.IsNullOrEmpty(_SearchNotebookKeyword))
                    RefreshNotes();
                else
                    Notes = new ObservableCollection<FileItemObjectModel>(AllItems
                        .Where(i => (i.Name == null || i.Content != null)
                        && (i?.Name.Contains(_SearchNotebookKeyword) ?? false)));
            }
        }
        /// <summary>
        /// Read and notification only, cannot be changed value
        /// </summary>
        public int ActiveNoteID
        {
            get => ActiveNote?.ID ?? 0;
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
        public string ActiveNoteContent
        {
            get => ActiveNote?.Content;
            set { ActiveNote.Content = value; NotifyPropertyChanged(); CommitActiveNoteChange(); ActiveNote.BroadcastPropertyChange(); }
        }
        public bool IsNoteFieldEditEnabled
            => ActiveNote != null;
        #endregion

        #region Logs View Properties
        private string _LogsText;
        public string LogsText { get => _LogsText; set => SetField(ref _LogsText, value); }
        #endregion

        #region Status View Properties
        private string _StatusText;
        public string StatusText { get => _StatusText; set => SetField(ref _StatusText, value); }
        private string _ConsoleInput;
        public string ConsoleInput { get => _ConsoleInput; set => SetField(ref _ConsoleInput, value); }
        private string _ConsoleResult;
        public string ConsoleResult { get => _ConsoleResult; set => SetField(ref _ConsoleResult, value); }
        #endregion

        #region Setting View Properties
        private string _ConfigurationsText;
        public string ConfigurationsText { get => _ConfigurationsText; set => SetField(ref _ConfigurationsText, value); }
        #endregion

        #region Commands
        private void SearchCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void SearchCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Just switch to Inventory tab and focus on keyname search input box
            TabHeader_MouseDown(InventoryTabLabel, null);
            SearchNameKeywordTextBox.Focus();
        }
        private void CommandAdd_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void CommandAdd_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Go to inventory panel
            TabHeader_MouseDown(InventoryTabLabel, null);
            // Show add dialog
            var dialog = GetHomeDirectoryFileDialog("Select files and folders to add", true, true);
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                IEnumerable<string> result = null;
                foreach (var name in dialog.FileNames)
                    result = Commands.Add(name);
                // Update panel and info
                RefreshAllItems();
                RefreshItems();
                if (dialog.FileNames.Count() > 1)
                    InfoText = $"{dialog.FileNames.Count()} items added.";
                else
                    InfoText = result.First();
            }
        }
        private void CloseWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void CloseWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // In case things are not saved, commit change for safety and avoid data loss
            if (ActiveItem != null)
            {
                CommitActiveItemChange();
                CommitActiveNoteChange();
            }
            this.Close();
        }
        private void ShowShortcutsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void ShowShortcutsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => new DialogWindow(this, "Keyboard Shorcuts", SomewhereDesktop.Properties.Resources.ShortcutsDocument).Show();
        private void ShowMarkdownReferenceCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void ShowMarkdownReferenceCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => new DialogWindow(this, "Markdown Reference", SomewhereDesktop.Properties.Resources.MarkdownReference).Show();
        private void MaximizeWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void MaximizeWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Safety
            if (ActiveItem != null)
            {
                CommitActiveItemChange();
                CommitActiveNoteChange();
            }
                
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
            {
                CommitActiveItemChange();
                CommitActiveNoteChange();
            }
            this.WindowState = WindowState.Minimized;
        }
        private void RefreshCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = InventoryPanel.Visibility == Visibility.Visible;
        private void RefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RefreshAllItems();
            RefreshItems();
            InfoText = $"{AllItems.Count()} items discovered.";
        }
        private void CreateNoteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void CreateNoteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Add to database
            int id = Commands.AddFile(null, string.Empty);  // Give some default empty content so it's not considered a binary file
            // Add to view collection
            var item = new FileItemObjectModel(Commands.GetFileDetail(id));
            // Add to items cache
            AllItems.Add(item);
            // Update note collection
            RefreshNotes();
            // Set activeadd
            ActiveNote = item;
            // Show note panel and focus on editing textbox
            TabHeader_MouseDown(NotebookTabLabel, null);
            NotenameTextBox.Focus();
        }
        private void OpenHomeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void OpenHomeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = GetHomeDirectoryFileDialog("Select home directory", false, true);
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                OpenRepository(dialog.FileName);
        }
        private void NewHomeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void NewHomeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = GetHomeDirectoryFileDialog("Select home directory", false, true);
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                CreateAndOpenNewHome(dialog.FileName);
        }
        private void CreateAndOpenNewHome(string folderPath)
        {
            try
            {
                Commands.New(folderPath);
                OpenRepository(folderPath);
            }
            catch (InvalidOperationException ex)
            {
                new DialogWindow(this, "Failed to create Home", ex.Message).ShowDialog();
            }
        }
        private CommonOpenFileDialog GetHomeDirectoryFileDialog(string title, bool file, bool folder)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = Commands.HomeDirectory;
            dialog.Title = title;
            if (file && folder)
                dialog.Multiselect = true;
            else if (folder)
                dialog.IsFolderPicker = true;
            return dialog;
        }
        private void OpenHyperlink_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Console.WriteLine("Hello World");
        }
        private void SwitchInventoryCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void SwitchNotebookCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void SwitchKnowledgeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void SwitchLogsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void SwitchNTFSSearchCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void SwitchSettingsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void SwitchStatusCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void SwitchInventoryCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => TabHeader_MouseDown(InventoryTabLabel, null);
        private void SwitchNotebookCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => TabHeader_MouseDown(NotebookTabLabel, null);
        private void SwitchKnowledgeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => TabHeader_MouseDown(KnowledgeTabLabel, null);
        private void SwitchLogsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => TabHeader_MouseDown(LogsTabLabel, null);
        private void SwitchNTFSSearchCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => TabHeader_MouseDown(NTFSSearchTabLabel, null);
        private void SwitchSettingsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => TabHeader_MouseDown(SettingsTabLabel, null);
        private void SwitchStatusCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            TabHeader_MouseDown(StatusTabLabel, null);
            ConsoleInputTextBox.Focus();
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
                    builder.AppendLine($"{log.DateTime.ToString("yyyy-MM-dd HH:mm:ss"),-25}{logEvent.Command, -20}{(logEvent.Arguments != null ? string.Join(" ", logEvent.Arguments) : string.Empty)}");
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
            if(e != null)
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
        private void ConsoleCommandInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if(e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ConsoleInput))
            {
                string[] positions = ConsoleInput.BreakCommandLineArgumentPositions();
                string command = positions.GetCommandName();
                string[] arguments = positions.GetArguments();
                // Disabled unsupported commands (i.e. those commands that have console input)
                if (command == "files" || command == "find")
                    ConsoleResult = $"Command `{command}` is not supported here, please use a real console emulator instead.";
                else
                {
                    StringBuilder result = new StringBuilder();
                    foreach (var line in Commands.ExecuteCommand(command, arguments))
                        result.AppendLine(line);
                    ConsoleResult = result.ToString();
                }
                ConsoleInput = string.Empty;
                e.Handled = true;
            }
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
            // Commit to database for active item
            if(ActiveItem != null)
            {
                try
                {
                    // Commit Name and Content change
                    Commands.ChangeFileName(ActiveItem.ID, ActiveItem.Name);
                    // Commit Tag change
                    Commands.ChangeFileTags(ActiveItem.ID, ActiveItem.TagsList);
                    // Update log and info display
                    Commands.AddLog("Update Item", $"Item #{ActiveItem.ID} `{ActiveItem.Name}` is updated in SD (Somewhere Desktop).");
                    InfoText = $"Item `{ActiveItem.Name.Limit(150)}` saved.";
                }
                catch (Exception e)
                {
                    new DialogWindow(this, "Error during updating note", e.Message).ShowDialog();
                }
            }
        }
        private void CommitActiveNoteChange()
        {
            // Commit to database for active note item
            if (ActiveNote != null)
            {
                //try
                //{
                    // Commit Name and Content change
                    Commands.ChangeFile(ActiveNote.ID, ActiveNote.Name, ActiveNote.Content);
                    // Commit Tag change
                    Commands.ChangeFileTags(ActiveNote.ID, ActiveNote.TagsList);
                    // Update log and info display
                    Commands.AddLog("Update Item", $"Item #{ActiveNote.ID} `{ActiveNote.Name}` is updated in SD (Somewhere Desktop).");
                    InfoText = $"Item `{ActiveNote.Name.Limit(150)}` saved.";
                //}
                //catch (Exception e)
                //{
                //    new DialogWindow(this, "Error during updating note", e.Message).ShowDialog();
                //}
            }
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
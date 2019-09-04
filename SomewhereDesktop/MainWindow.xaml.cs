using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
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
using System.Windows.Threading;
using Microsoft.WindowsAPICodePack.Dialogs;
using Somewhere;
using StringHelper;
using Vlc.DotNet.Forms;

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
            // Parse single command line argument as home repository path
            if(Environment.GetCommandLineArgs().Length == 1)
            {
                string filePath = Environment.GetCommandLineArgs().Single();
                // Load directly from somewhere repository database
                if (System.IO.Path.GetFileName(filePath) == Commands.DBName)
                    Arguments["dir"] = System.IO.Path.GetDirectoryName(filePath);
                // Ignore it and initialize empty arguments dictionary
                else
                    Arguments = new Dictionary<string, string>();
            }
            // Parse as key-value pair command line arguments
            else
                Arguments = ParseCommandlineArguments();
            // Create Commands object
            if (Arguments.ContainsKey("dir"))
                OpenRepository(Arguments["dir"]);
            else
                OpenRepository(Directory.GetCurrentDirectory());
            if (Commands == null || !Commands.IsHomePresent)
                Close();

            // Initialize Video Preview Control
            InitializeVLCControl();
            // Check new versions in background
            BackgroundCheckNewVersion();
        }
        private void ShowRecentPathsDialog(string[] recent)
        {
            var options = recent.ToList();
            options.Add("Clear History");
            var dialog = new DialogWindow(this?.Visibility == Visibility.Visible ? this : null, 
                "Recent homes", "Double click on a path to open Home repository.", options);
            dialog.ShowDialog();
            if (dialog.Selection == null)
                this.Close();
            else if (dialog.Selection == "Clear History")
            {
                CleanRecentHomePaths();
                OpenHomeCommand_Executed(null, null);
            }
            else
                OpenRepository(dialog.Selection);
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
                var options = new List<string> {
                    "1. Create Home Repository Here",
                    "2. Create Home Repository at A Different Place",
                    "3. Open a Different Home Folder",
                    "4. Close and Exit"};
                // Get recent paths if available
                var recentPaths = GetRecentHomePaths();
                if (recentPaths.Length != 0)
                    options.Add("5. Open Recent Home Paths");
                // Show dialog
                var dialog = new DialogWindow(null/*This window may not have been initialized yet*/,
                    "Home Action",
                    $"Home repository doesn't exist at path `{homeFolderpath}`, what would you like to do?",
                    options);
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
                else if (dialog.Selection == options[3]
                    || dialog.Selection == null)
                    this.Close();
                else if (options.Count == 5 && dialog.Selection == options[4])
                {
                    if (recentPaths.Length == 0)
                        // Just show open home again
                        OpenHomeCommand_Executed(null, null);
                    else
                        // Show recent paths
                        ShowRecentPathsDialog(recentPaths);
                }
                else
                    throw new ArgumentException($"Unexpected selection `{dialog.Selection}`.");
                return;
            }
            // Record path to recent
            else
                SaveToRecentHomePaths(Commands.HomeDirectory);
            // Initialize items
            RefreshAllItems();
            RefreshItems();
            RefreshNotes();
            RefreshTags();
            // Initialize UI Theme
            InitializeUI();
            // Update info
            InfoText = $"Home Directory: {Commands.HomeDirectory}; {Items.Count} items.";
        }
        private void InitializeUI()
        {
            if (Commands.IsFileInDatabase("_HomeBackgroundImage.png"))
                BackgroundImage = Commands.GetPhysicalPath("_HomeBackgroundImage.png");
            else if (Commands.IsFileInDatabase("_HomeBackgroundImage.jpg"))
                BackgroundImage = Commands.GetPhysicalPath("_HomeBackgroundImage.jpg");
            else
                BackgroundImage = null;
        }
        private void InitializeVLCControl()
        {
            VLCControl = new VlcControl();
            this.PreviewWindowsFormsHost.Child = VLCControl;
            var currentAssembly = Assembly.GetEntryAssembly();
            var currentDirectory = new FileInfo(currentAssembly.Location).DirectoryName;
            // Default installation path of VideoLAN.LibVLC.Windows
            var libDirectory = new DirectoryInfo(System.IO.Path.Combine(currentDirectory, "libvlc", IntPtr.Size == 4 ? "win-x86" : "win-x64"));
            VLCControl.BeginInit();
            VLCControl.MouseDown += VLCControl_MouseDown;
            VLCControl.Opening += VLCControl_Opening;
            VLCControl.VlcLibDirectory = libDirectory;
            VLCControl.EndInit();
        }
        private VlcControl VLCControl { get; set; }
        private void BackgroundCheckNewVersion()
        {
            BackgroundWorker worker = null;

            void ShowLatestVersionNotificationDialog(string newVersion, string currentVersion, string downloadAddress)
            {
                var dialog = new DialogWindow(null, "New version available!", 
                    $"Your copy of Somewhere (**{currentVersion}**) is outdated! " +
                    $"A new version (**{newVersion}**) is available at address [{downloadAddress}]({downloadAddress})");
                dialog.Show();
            }
            // Extract MAJOR, MINOR, PATCH version string from version code in this form: 
            // `VMAJOR-BETA/ALPHA+WHATEVER.MINOR-BETA/ALPHA+WHATEVER.PATCH-BETA/ALPHA+WHATEVER` 
            // without space
            string GetVersionNumber(string versionCode, string partName)
            {
                if (versionCode.ToLower().First() != 'v')
                    throw new ArgumentException($"Invalid version code format: `{versionCode}`; Expect starting with 'v'.");
                if (versionCode.Contains(' '))
                    throw new ArgumentException($"Invalid version code format: `{versionCode}`; Should not contain space.");
                // Remove leading v
                versionCode = versionCode.Substring(1);
                switch (partName.ToLower())
                {
                    case "major":
                        return versionCode.Split('.')[0];
                    case "minor":
                        return versionCode.Split('.')[1];
                    case "patch":
                        return versionCode.Split('.')[2];
                    default:
                        throw new ArgumentException($"Invalid part name: `{partName}`; Expect one of MAJOR, MINOR and PATCH (case insensitive).");
                }
            }
            async void CheckNewVersion(object sender, DoWorkEventArgs e)
            {
                string latestReleaseUrl = await GetRedirection("https://github.com/szinubuntu/Somewhere/releases/latest");
                // Null check for exception
                if (latestReleaseUrl == null) return;
                // E.g. Format like https://github.com/szinubuntu/Somewhere/releases/tag/Va.b.c-alpha_or_beta
                // Just return the "Va.b.c" part
                var newVersion = latestReleaseUrl.Substring(latestReleaseUrl.LastIndexOf('/') + 1);
                var currentVersion = Commands.ReleaseVersion;
                // If we do not have a latest version, show notification dialog
                if (newVersion != currentVersion
                    && (Convert.ToInt32(GetVersionNumber(newVersion, "major")) > (Convert.ToInt32(GetVersionNumber(currentVersion, "major"))))
                    && (Convert.ToInt32(GetVersionNumber(newVersion, "minor")) > (Convert.ToInt32(GetVersionNumber(currentVersion, "minor")))))
                    await Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() => {
                            ShowLatestVersionNotificationDialog(newVersion, currentVersion, latestReleaseUrl);
                        }));
                else
                    await Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() => {
                            InfoText = "Current version is up-to-date.";
                        }));
                // Dispose worker
                worker.Dispose();
            }
            worker = new BackgroundWorker();
            worker.DoWork += CheckNewVersion;
            worker.RunWorkerAsync();
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
            RefreshTypeFilters();
        }
        private void RefreshTypeFilters()
        {
            // Get available types from current items
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
        /// <summary>
        /// Refresh Inventory Tab items listview per current AllItems
        /// </summary>
        private void RefreshItems(bool reversedOrder = false)
        {
            if (reversedOrder)
            {
                // By name
                if (SelectedItemSorting == ItemsSortingOptions[0])
                    Items = new ObservableCollection<FileItemObjectModel>(AllItems.OrderByDescending(i => i.DisplayName));
                // By entry date
                else if (SelectedItemSorting == ItemsSortingOptions[1])
                    Items = new ObservableCollection<FileItemObjectModel>(AllItems.OrderByDescending(i => i.EntryDate));
                // Default
                else
                    Items = new ObservableCollection<FileItemObjectModel>(AllItems);
            }
            // Default order
            else
            {
                // By name
                if (SelectedItemSorting == ItemsSortingOptions[0])
                    Items = new ObservableCollection<FileItemObjectModel>(AllItems.OrderBy(i => i.DisplayName));
                // By entry date
                else if (SelectedItemSorting == ItemsSortingOptions[1])
                    Items = new ObservableCollection<FileItemObjectModel>(AllItems.OrderBy(i => i.EntryDate));
                // Default
                else
                    Items = new ObservableCollection<FileItemObjectModel>(AllItems);
            }
        }
        private void RefreshNotes()
            => Notes = new ObservableCollection<FileItemObjectModel>(AllItems
                .Where(i => i.Name == null || i.Content != null));
        /// <summary>
        /// Refresh search all tags cache, search tags and tag filters so it is constrainted by currently displayed items
        /// </summary>
        /// <param name="searchTagsOnlyExcludeTagFilters">Update only SearchTags icons with current available list items, and keep TagFilters active</param>
        private void RefreshTags(bool searchTagsOnlyExcludeTagFilters = false)
        {
            // Update all tags
            AllTags = Items.SelectMany(t => t.TagsList).Distinct().OrderBy(t => t).ToList();
            // Update search tags
            SearchTags = searchTagsOnlyExcludeTagFilters
                ? new ObservableCollection<string>(AllTags.Except(TagFilters))
                : new ObservableCollection<string>(AllTags);
            // Update filters
            if (!searchTagsOnlyExcludeTagFilters)
                TagFilters = new ObservableCollection<string>();
        }
        /// <summary>
        /// Filter items display by various constraints; This will also update tags accordingly when filtering is done.
        /// Notice this function can be called AFTER an existing RefreshTags() call for reseting tag constraing, 
        /// and it can also be called BEFORE another RefreshTags() to constraint tags.
        /// </summary>
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
            if (TagFilters.Count != 0)
                Items = new ObservableCollection<FileItemObjectModel>(
                    Items.Where(i => i.TagsList.Intersect(TagFilters).Count() == TagFilters.Count));
        }
        void ClearItemPreview()
        {
            PreviewText = PreviewImage = PreviewStatus = PreviewMarkdown = null;
            VLCControl.Stop();
            PreviewTextBox.Visibility = PreviewImageSource.Visibility = PreviewTextBlock.Visibility
                = PreviewMarkdownViewer.Visibility = PreviewWindowsFormsHost.Visibility
                = PreviewBrowser.Visibility
                = Visibility.Collapsed;
        }
        private void UpdateItemPreview()
        {   
            // Clear previous
            ClearItemPreview();
            // Return early in case of items refresh
            if (ActiveItem == null)
                return;
            // Preview knowledge and note (default as markdown)
            if (ActiveItem.Content != null || ActiveItem.Name == null)
            {
                // Preview web link
                if (ActiveItem.Content != null /* Make sure it is a note */
                    && ActiveItem.Content.IndexOfAny(new char[] { '\r', '\n' }) == -1   /* Make sure it contains only a single line */
                    && IsStringWebUrl(ActiveItem.Content) /* Make sure it's a url */)
                {
                    PreviewBrowser.Visibility = Visibility.Visible;
                    PreviewBrowser.Address = ActiveItem.Content;
                }
                // Preview markdown
                else
                {
                    PreviewMarkdownViewer.Visibility = Visibility.Visible;
                    PreviewMarkdown = ActiveItem.Content;
                }
            }
            // Preview folder
            else if (ActiveItem.Name.EndsWith("/") || ActiveItem.Name.EndsWith("\\"))
            {
                StringBuilder builder = new StringBuilder("Folder Contents: " + Environment.NewLine);
                foreach (string fileEntry in Directory.EnumerateFileSystemEntries(System.IO.Path.Combine(Commands.HomeDirectory, ActiveItem.Name)))
                    builder.AppendLine($"{fileEntry}");
                PreviewTextBox.Visibility = Visibility.Visible;
                PreviewText = builder.ToString();
            }
            // Preview files
            else
            {
                // Preview image
                string extension = System.IO.Path.GetExtension(ActiveItem.Name).ToLower();
                if (ImageFileExtensions.Contains(extension))
                {
                    PreviewImageSource.Visibility = Visibility.Visible;
                    PreviewImage = Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name);
                }
                // Preview markdown
                else if (extension == ".md")
                {
                    PreviewMarkdownViewer.Visibility = Visibility.Visible;
                    PreviewMarkdown = File.ReadAllText(Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name));
                }
                // Preview text
                else if (extension == ".txt")
                {
                    PreviewTextBox.Visibility = Visibility.Visible;
                    PreviewText = File.ReadAllText(Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name));
                }
                // Preview videos and audios
                else if (VideoFileExtensions.Contains(extension) || AudioFileExtensions.Contains(extension))
                {
                    PreviewWindowsFormsHost.Visibility = Visibility.Visible;
                    Play(Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name));
                }
                // Preview webpages
                else if(extension == ".html")
                {
                    PreviewBrowser.Visibility = Visibility.Visible;
                    PreviewBrowser.Address = Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name);
                }
                else
                {
                    PreviewTextBlock.Visibility = Visibility.Visible;
                    PreviewStatus = "No Preview Available for Binary Data";
                }
            }
        }
        private readonly static string[] ImageFileExtensions = new string[] { ".png", ".img", ".jpg", ".bmp" };
        private readonly static string[] AudioFileExtensions = new string[] { ".ogg", ".mp3", ".wav",".ogm", ".m4a" };
        private readonly static string[] VideoFileExtensions = new string[] { ".avi", ".flv", ".mp4", ".mpeg", ".wmv", ".mpg" };
        #endregion

        #region Public View Properties
        private string _InfoText;
        public string InfoText { get => _InfoText; set => SetField(ref _InfoText, value); }
        private string _BackgroundImage;
        public string BackgroundImage { get => _BackgroundImage; set => SetField(ref _BackgroundImage, value); }
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
                RefreshTags(true);
            }
        }
        private ObservableCollection<string> _TypeFilters;
        public ObservableCollection<string> TypeFilters { get => _TypeFilters; set => SetField(ref _TypeFilters, value); }
        private string[] _ItemsSortingOptions = new string[] { "Sort by Name", "Sort by Entry Date" };
        public string[] ItemsSortingOptions { get => _ItemsSortingOptions; set => SetField(ref _ItemsSortingOptions, value); }
        private string _SelectedItemSorting = "Sort by Name";
        public string SelectedItemSorting { get => _SelectedItemSorting; set { SetField(ref _SelectedItemSorting, value); RefreshItems(); } }
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
                RefreshTags(true);
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
        public string _PreviewInput;
        public string PreviewInput { get => _PreviewInput; set => SetField(ref _PreviewInput, value); }
        private FileItemObjectModel _ActiveItem;
        public FileItemObjectModel ActiveItem
        {
            get => _ActiveItem;
            set
            {
                SetField(ref _ActiveItem, value);
                NotifyPropertyChanged("ActiveItemID");
                NotifyPropertyChanged("ActiveItemEntryDate");
                NotifyPropertyChanged("ActiveItemName");
                NotifyPropertyChanged("ActiveItemTags");
                NotifyPropertyChanged("ActiveItemRemarkMeta");
                UpdateItemPreview();
            }
        }
        public string ActiveItemRemarkMeta
        {
            get => (ActiveItem != null && ActiveItem.Meta != null) ? Commands.ExtractRemarkMeta(ActiveItem.Meta).Remark : null;
            set
            {
                if (ActiveItem == null) return;
                string newMeta = Commands.ReplaceOrInitializeMetaAttribute(ActiveItem.Meta, "Remark", value);
                if(ActiveItem.Meta != newMeta)
                {
                    ActiveItem.Meta = newMeta;  NotifyPropertyChanged(); CommitActiveItemChange(); ActiveItem.BroadcastPropertyChange();
                }
            }
        }
        public string ActiveItemEntryDate
        {
            get => ActiveItem?.EntryDate.ToString("yyyy-MM-dd HH:mm:ss");
        }
        public int ActiveItemID
        {
            get => ActiveItem?.ID ?? 0;
        }
        public string ActiveItemName
        {
            get => ActiveItem?.Name;
            set
            {
                if (ActiveItem == null) return;
                if (ActiveItem?.Name != value)
                {
                    // Update old value and save new value
                    string oldName = ActiveItem.Name;
                    ActiveItem.Name = value;
                    // In addition, if item is file, update file
                    bool deleted = false;
                    if (ActiveItem.Content == null && oldName != null && !oldName.Last().IsSeparator())
                    {
                        // Emptying the name will delete item
                        if (string.IsNullOrWhiteSpace(ActiveItem.Name))
                        {
                            Commands.RM(oldName);
                            deleted = true;
                        }
                        // Rename item properly
                        else
                            Commands.MV(oldName, ActiveItem.Name);
                    }
                    else
                        CommitActiveItemChange();
                    // Data binding update
                    NotifyPropertyChanged();
                    ActiveItem.BroadcastPropertyChange();
                    RefreshTypeFilters();
                    // In addition, update active note if it's also selected
                    if (ActiveItem == ActiveNote)
                        NotifyPropertyChanged("ActiveNoteName");
                    // Delete from view
                    if (deleted)
                        ActiveItem = null;
                }
            }
        }
        public string ActiveItemTags
        {
            get => ActiveItem?.Tags;
            set
            {
                if (ActiveItem == null) return;
                if (ActiveItem?.Tags != value)
                {
                    ActiveItem.Tags = value; NotifyPropertyChanged(); CommitActiveItemChange(); ActiveItem.BroadcastPropertyChange(); RefreshTags(); FilterItems(); if (ActiveItem == ActiveNote) NotifyPropertyChanged("ActiveNoteTags");
                }
            }
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
                SetField(ref _ActiveNote, value);
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
                        && (i?.Name?.ToLower().Contains(_SearchNotebookKeyword.ToLower()) ?? false)));
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
            set
            {
                if (ActiveNote == null) return;
                if (ActiveNote?.Name != value)
                {
                    ActiveNote.Name = value; NotifyPropertyChanged(); CommitActiveNoteChange(); ActiveNote.BroadcastPropertyChange(); RefreshTypeFilters(); if (ActiveNote == ActiveItem) NotifyPropertyChanged("ActiveItemName");
                }
            }
        }
        public string ActiveNoteTags
        {
            get => ActiveNote?.Tags;
            set
            {
                if (ActiveNote == null) return;
                if (ActiveNote.Tags != value)
                {
                    ActiveNote.Tags = value; NotifyPropertyChanged(); CommitActiveNoteChange(); ActiveNote.BroadcastPropertyChange(); RefreshTags(); FilterItems(); if (ActiveNote == ActiveItem) NotifyPropertyChanged("ActiveItemTags");
                }
            }
        }
        public string ActiveNoteContent
        {
            get => ActiveNote?.Content;
            set
            {
                if (ActiveNote == null) return;
                if (ActiveNote?.Content != value)
                {
                    ActiveNote.Content = value; NotifyPropertyChanged(); CommitActiveNoteChange(); ActiveNote.BroadcastPropertyChange(); if (ActiveNote == ActiveItem) UpdateItemPreview();
                }
            }
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
            // Go to inventory tab
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
                {
                    string text = result.First();
                    // Show highlight dialog
                    new DialogWindow(this, "Add file", text).ShowDialog();
                    // Update info text
                    InfoText = text;
                    // Select added item
                    if (InventoryPanel.Visibility == Visibility.Visible)
                        ActiveItem = Items.OrderBy(i => i.EntryDate).LastOrDefault() ?? ActiveItem;
                }
            }
        }
        private void CommandImport_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void CommandImport_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Go to inventory tab
            TabHeader_MouseDown(InventoryTabLabel, null);
            // Show add dialog
            var dialog = GetHomeDirectoryFileDialog("Select file or folder to import", true, true);
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                try
                {
                    int oldCount = AllItems.Count();
                    List<string> result = new List<string>();
                    foreach (string path in dialog.FileNames)
                    {
                        result.Add($"Import result for `{path}`: \n\n");
                        result.AddRange(Commands.Im(path).Select(r => $"* {r}\n")); // Notice this line can 
                        // throw exceptions so we need to catch it
                    }
                    // Update panel
                    RefreshAllItems();
                    RefreshItems();
                    // Update info
                    InfoText = $"{dialog.FileNames.Count()} targets imported; {AllItems.Count() - oldCount} items are added.";
                    // Show report in dialog
                    StringBuilder text = new StringBuilder();
                    result.ForEach(r => text.AppendLine(r));
                    new DialogWindow(this, "Import result", text.ToString()).ShowDialog();
                }
                catch (Exception error)
                {
                    new DialogWindow(this, "Error when importing", error.Message).ShowDialog();
                    InfoText = "An error occured during importing.";
                }
            }
        }
        private void CloseWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void CloseWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => this.Close();
        private void ShowShortcutsCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void ShowShortcutsCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => new DialogWindow(this, "Keyboard Shorcuts", SomewhereDesktop.Properties.Resources.ShortcutsDocument).Show();
        private void ShowMarkdownReferenceCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void ShowMarkdownReferenceCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => new DialogWindow(this, "Markdown Reference", SomewhereDesktop.Properties.Resources.MarkdownReference).Show();
        private void OpenCommandPromptCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void OpenCommandPromptCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => Process.Start(new ProcessStartInfo()
            {
                FileName = "cmd",
                WorkingDirectory = Commands.HomeDirectory,
                UseShellExecute = true
            });
        private void MaximizeWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void MaximizeWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Safety
            TryCommitActiveItemChange();
            TryCommitActiveNoteChange();

            if (this.WindowState == WindowState.Normal)
                this.WindowState = WindowState.Maximized;
            else this.WindowState = WindowState.Normal;
        }
        private void HideWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void HideWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Safety
            TryCommitActiveItemChange();
            TryCommitActiveNoteChange();
            this.WindowState = WindowState.Minimized;
        }
        private void RefreshCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = InventoryPanel.Visibility == Visibility.Visible;
        private void RefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RefreshAllItems();
            RefreshItems();
            RefreshTags();
            InfoText = $"{AllItems.Count()} items discovered.";
        }
        private void SaveCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = ActiveItem != null || ActiveNote != null;
        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            TryCommitActiveItemChange();
            TryCommitActiveNoteChange();
            InfoText = "Item saved.";
        }
        private void GotoActiveItemEditContentCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = (ActiveItem != null && (ActiveItem.Name == null || ActiveItem.Content != null));
        private void GotoActiveItemEditContentCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            TabHeader_MouseDown(NotebookTabLabel, null);
            ActiveNote = ActiveItem;
            NoteContentTextBox.Focus();
            NoteContentTextBox.SelectAll();
        }
        private void CreateNoteCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void CreateNoteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Save old note by forcing update
            NoteNameTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            NoteContentTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            // Add to database
            int id = Commands.AddFile(null, string.Empty);  // Give some default empty content so it's not considered a binary file
            // Add to view collection
            var item = new FileItemObjectModel(Commands.GetFileDetail(id));
            // Add to items cache
            AllItems.Add(item);
            RefreshItems();
            // Update note collection
            RefreshNotes();
            // Set activeadd
            ActiveNote = item;
            // Show note panel and focus on editing textbox
            TabHeader_MouseDown(NotebookTabLabel, null);
            NoteNameTextBox.Focus();
            NoteNameTextBox.SelectAll();
        }
        private void OpenHomeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        /// <summary>
        /// Shows "Select home directory" dialog
        /// </summary>
        private void OpenHomeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var dialog = GetHomeDirectoryFileDialog("Select home directory", false, true);
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
                OpenRepository(dialog.FileName);
        }
        private void RecentHomesCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void RecentHomesCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => ShowRecentPathsDialog(GetRecentHomePaths());
        private void NewHomeCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void NewHomeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var message = "Select home directory";
            while (true)
            {
                var dialog = GetHomeDirectoryFileDialog(message, false, true);
                var result = dialog.ShowDialog();
                // Proceed only if that folder doesn't already contains a home
                Commands temp;
                if (result == CommonFileDialogResult.Ok
                    && !(temp = new Commands(dialog.FileName, false)).IsHomePresent)    // Make sure there is no home at that directory
                {
                    temp.Dispose(); // Notice temp will only be initialized if `result == CommonFileDialogResult.Ok` is passed
                    CreateAndOpenNewHome(dialog.FileName);
                    return;
                }
                else if (result == CommonFileDialogResult.Cancel
                    || result == CommonFileDialogResult.None)
                    return;
                else
                {
                    message = "Home already exists at selected folder, select a different home directory";
                    continue;
                }
            }
        }
        private void CreateAndOpenNewHome(string folderPath)
        {
            try
            {
                Commands.GenerateDBFileAt(folderPath);
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
        private bool SortReverOrder = false;
        private void AdvancedOperationsPanelToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if(BasicOperationsPanel.Visibility == Visibility.Visible)
            {
                BasicOperationsPanel.Visibility = Visibility.Collapsed;
                AdvancedOperationsPanel.Visibility = Visibility.Visible;
                AdvancedOperationsPanelToggleButton.Content = "Basic";
            }
            else
            {
                BasicOperationsPanel.Visibility = Visibility.Visible;
                AdvancedOperationsPanel.Visibility = Visibility.Collapsed;
                AdvancedOperationsPanelToggleButton.Content = "Advanced";
            }
        }
        private void ItemSortingReverseButton_Click(object sender, RoutedEventArgs e)
            => RefreshItems(SortReverOrder = !SortReverOrder);
        private void RemoveTagFilter_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Update tag display lists
            string tag = (sender as TextBlock).Text as string;
            TagFilters.Remove(tag);
            SearchTags.Add(tag);
            // Perform filtering
            FilterItems();
            RefreshTags(true);
        }
        private void AddTagFilter_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Update tag display lists
            string tag = (sender as TextBlock).Text as string;
            TagFilters.Add(tag);
            SearchTags.Remove(tag);
            // Perform filtering
            FilterItems();
            RefreshTags(true);
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
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ConsoleInput))
            {
                var oldReadValue = Commands.ReadFromConsoleEnabled;
                var oldWriteValue = Commands.WriteToConsoleEnabled;
                Commands.ReadFromConsoleEnabled = false;
                Commands.WriteToConsoleEnabled = false;
                // Break a chord into notes, each key represent a seperate command
                string[] chord = ConsoleInput.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);
                StringBuilder result = new StringBuilder();
                foreach (string chordNote in chord)
                {
                    string[] positions = chordNote.BreakCommandLineArgumentPositions();
                    string command = positions.GetCommandName().ToLower();
                    string[] arguments = positions.GetArguments();
                    if (chord.Length > 1)
                        result.AppendLine($"{command}:");
                    // Disabled unsupported commands (i.e. those commands that have console input)
                    string[] disabledCommands = new string[] { "purge", "x" };
                    if (disabledCommands.Contains(command))
                    {
                        ConsoleResult = $"Command `{command}` is not supported here, please use a real console emulator instead.";
                        break;
                    }
                    else
                    {
                        foreach (var line in Commands.ExecuteCommand(command, arguments))
                            result.AppendLine(line);
                    }
                }
                ConsoleResult = result.ToString();
                ConsoleInput = string.Empty;
                Commands.ReadFromConsoleEnabled = oldReadValue;
                Commands.WriteToConsoleEnabled = oldWriteValue;
                e.Handled = true;
            }
        }
        /// <summary>
        /// Simple action command handling interface for previewed content
        /// </summary>
        private void PreviewActionInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(PreviewInput))
            {
                // Handle commands (in lower case)
                var positions = PreviewInput.BreakCommandLineArgumentPositions();
                var commandName = positions.GetCommandName();
                string[] arguments = positions.GetArguments();
                ProcessPreviewCommand(commandName, arguments);
                PreviewInput = string.Empty;
                e.Handled = true;
            }
        }
        /// <summary>
        /// Mouse down event for VLC player
        /// </summary>
        private void VLCControl_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
            => Pause();
        /// <summary>
        /// Opening event for VLC player
        /// </summary>
        private void VLCControl_Opening(object sender, Vlc.DotNet.Core.VlcMediaPlayerOpeningEventArgs e)
        {
            if (ShouldSkipAutoplay)
                VLCControl.SetPause(true);  // Notice VLCControl.Pause() won't work, probably because at this time the video is in a state between playing and not playing
        }
        /// <summary>
        /// Provides shortcut for note content textbox
        /// </summary>
        private void NoteContentTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && Keyboard.IsKeyDown(Key.LeftShift))
            {
                e.Handled = true;
                NoteTagsTextBox.Focus();
            }
        }
        /// <summary>
        /// Provides handling for Markdown syntax related shortcuts
        /// </summary>
        private void NoteContentTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if(textBox.SelectionLength != 0)
            {
                if(e.Text == "*")
                {
                    textBox.SelectedText = $"*{textBox.SelectedText}*";
                    e.Handled = true;
                }
                else if (e.Text == "_")
                {
                    textBox.SelectedText = $"_{textBox.SelectedText}_";
                    e.Handled = true;
                }
                else if (e.Text == "`")
                {
                    textBox.SelectedText = $"`{textBox.SelectedText}`";
                    e.Handled = true;
                }
                else if (e.Text == "[")
                {
                    textBox.SelectedText = $"[{textBox.SelectedText}]";
                    e.Handled = true;
                }
                else if (e.Text == "{")
                {
                    textBox.SelectedText = $"{{{textBox.SelectedText}}}";
                    e.Handled = true;
                }
                else if (e.Text == "(")
                {
                    textBox.SelectedText = $"({textBox.SelectedText})";
                    e.Handled = true;
                }
                else if (e.Text == "\"")
                {
                    textBox.SelectedText = $"\"{textBox.SelectedText}\"";
                    e.Handled = true;
                }
                else if (e.Text == "'")
                {
                    textBox.SelectedText = $"'{textBox.SelectedText}'";
                    e.Handled = true;
                }
            }
        }
        private void ItemsList_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            if ( // Make sure we have some selection
                ActiveItem != null 
                // Make sure the selection is not an "Knowledge" item
                && ActiveItem.Name != null)
                System.Diagnostics.Process.Start(Commands.GetPhysicalPath(ActiveItem.Name));
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
            => this.DragMove();
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // In case things are not saved, commit change for safety and avoid data loss
            TryCommitActiveItemChange();
            TryCommitActiveNoteChange();

            // Dispose resources
            VLCControl?.Stop(); VLCControl?.Dispose(); VLCControl = null;
            Commands?.Dispose(); Commands = null;
            Popup?.Close(); Popup = null;
            LastWorker?.Dispose(); LastWorker = null;
        }
        #endregion

        #region Searching Routines and Auxliary
        private int WorkerCount { get; } = 0;
        private PopupSelectionWindow Popup { get; set; }
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
        private void TryCommitActiveItemChange()
        {
            if(ActiveItem != null)
            {
                ItemNameTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                ItemTagsTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                ItemRemarkTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            }
        }
        private void CommitActiveItemChange()
        {
            if (Commands == null) return;
            // Commit to database for active item
            if (ActiveItem != null)
            {
                try
                {
                    // Commit Name and Content change
                    Commands.ChangeFileName(ActiveItem.ID, ActiveItem.Name);
                    // Commit Tag change
                    Commands.ChangeFileTags(ActiveItem.ID, ActiveItem.TagsList);
                    // Update log and info display
                    Commands.AddLog("Update Item", $"Item #{ActiveItem.ID} `{ActiveItem.Name}` is updated in SD (Somewhere Desktop).");
                    InfoText = $"Item `{ActiveItem.DisplayName.Limit(150)}` (#{ActiveItem.ID}) saved.";
                    // Update meta
                    Commands.SetMeta(ActiveItem.ID, ActiveItem.Meta);
                }
                catch (Exception e)
                {
                    new DialogWindow(this, "Error during updating item", e.Message).ShowDialog();
                }
            }
        }
        private void TryCommitActiveNoteChange()
        {
            if (ActiveNote != null)
            {
                NoteNameTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                NoteTagsTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
                NoteContentTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            }
        }
        private void CommitActiveNoteChange()
        {
            if (Commands == null) return;
            // Commit to database for active note item
            if (ActiveNote != null)
            {
                try
                {
                    // Commit Name and Content change
                    Commands.ChangeFile(ActiveNote.ID, ActiveNote.Name, ActiveNote.Content);
                    // Commit Tag change
                    Commands.ChangeFileTags(ActiveNote.ID, ActiveNote.TagsList);
                    // Update log and info display
                    Commands.AddLog("Update Item", $"Item #{ActiveNote.ID} `{ActiveNote.Name}` is updated in SD (Somewhere Desktop).");
                    InfoText = $"Item `{ActiveNote.Name?.Limit(150) ?? "(Knowledge)"}` saved.";
                }
                catch (Exception e)
                {
                    new DialogWindow(this, "Error during updating note", e.Message).ShowDialog();
                }
            }
        }
        /// <summary>
        /// Path to a text file containing recent opened home paths
        /// </summary>
        private static readonly string RecentFile = 
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Recent.txt");
        /// <summary>
        /// Get an array of recently opened home paths
        /// </summary>
        private string[] GetRecentHomePaths()
        {
            if (File.Exists(RecentFile))
                // Each line represents one location
                return File.ReadAllText(RecentFile).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            else return new string[] { };
        }
        /// <summary>
        /// Save to recently opened home paths
        /// </summary>
        private void SaveToRecentHomePaths(string newPath)
        {
            // First read existing
            List<string> existingPaths = GetRecentHomePaths().ToList();
            // Then add to it (most recent inserted as first)
            existingPaths.Insert(0, newPath);
            // Then take distinct
            IEnumerable<string> distinctPaths = existingPaths.Distinct();
            // Write
            using (StreamWriter writer = new StreamWriter(RecentFile))
            {
                foreach (var line in distinctPaths)
                    writer.WriteLine(line);
                writer.Flush();
            }
        }
        /// <summary>
        /// Clean recent home paths records
        /// </summary>
        private void CleanRecentHomePaths()
        {
            if (File.Exists(RecentFile))
                File.Delete(RecentFile);
        }
        /// <summary>
        /// Get redirected result of an HTTP GET request
        /// </summary>
        async Task<string> GetRedirection(string originalUrl)
        {
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(originalUrl);
                request.AllowAutoRedirect = true;

                using (HttpWebResponse response = (HttpWebResponse)await request.GetResponseAsync())
                {
                    return response.ResponseUri.ToString();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
        /// <summary>
        /// Check whether a given string is valid url
        /// </summary>
        bool IsStringWebUrl(string text)
        {
            bool result = Uri.TryCreate(text, UriKind.Absolute, out Uri uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
            return result /* may return null for Urls that doesn't start with http or https */
                || Uri.IsWellFormedUriString(text, UriKind.RelativeOrAbsolute);
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

        #region Preview Actions
        private Dictionary<MethodInfo, CommandAttribute> _CommandMethods = null;
        private Dictionary<MethodInfo, CommandArgumentAttribute[]> _CommandArguments = null;
        private Dictionary<string, MethodInfo> _CommandNames = null;
        /// <summary>
        /// Returns a list of all Command methods
        /// </summary>
        public Dictionary<MethodInfo, CommandAttribute> CommandAttributes
            => _CommandMethods == null
            ? (_CommandMethods = typeof(MainWindow).GetMethods(BindingFlags.Instance | BindingFlags.Public).ToDictionary(m => m,
                m => m.GetCustomAttributes(typeof(CommandAttribute), false).SingleOrDefault() as CommandAttribute)
                .Where(d => d.Value != null).ToDictionary(d => d.Key, d => d.Value))    // Initialize and return member
            : _CommandMethods; // Return already initialized member
        public Dictionary<MethodInfo, CommandArgumentAttribute[]> CommandArguments
            => _CommandArguments == null
            ? (_CommandArguments = CommandAttributes.ToDictionary(m => m.Key,
                m => m.Key.GetCustomAttributes(typeof(CommandArgumentAttribute), false)
                .Select(a => a as CommandArgumentAttribute).ToArray())
                .ToDictionary(d => d.Key, d => d.Value))    // Initialize and return member
            : _CommandArguments; // Return already initialized member
        /// <summary>
        /// Returns a list of all commands by name
        /// </summary>
        public Dictionary<string, MethodInfo> CommandNames
            => _CommandNames == null
            ? (_CommandNames = CommandAttributes.ToDictionary(m => m.Key.Name.ToLower(), m => m.Key)) // Initialize and return member
            : _CommandNames;
        /// <summary>
        /// Sessional settings;
        /// All saved as lower cases
        /// </summary>
        public HashSet<string> QuickSettings { get; set; } = new HashSet<string>();
        /// <summary>
        /// Whether item preview should skip autoplay
        /// </summary>
        public bool ShouldSkipAutoplay
            => QuickSettings.Contains("nvp");
        private void ProcessPreviewCommand(string commandName, string[] arguments)
        {
            if (CommandNames.ContainsKey(commandName))
            {
                var method = CommandNames[commandName];
                var attribute = CommandAttributes[method];
                try
                {
                    // Execute the command
                    IEnumerable<string> result = method.Invoke(this, new[] { arguments }) as IEnumerable<string>;
                    StringBuilder builder = new StringBuilder();
                    if (result != null)
                        foreach (string line in result)
                            builder.Append(line);
                    InfoText = builder.ToString();
                }
                catch (Exception e) { InfoText = $"{e.InnerException.Message}"; }
            }
            else InfoText = $"Specified command `{commandName}` doesn't exist. Try again.";
        }
        [Command("Output all available commands into a text file.")]
        [CommandArgument("filename", "name of the file to dump details", optional: true)]
        public IEnumerable<string> Doc(params string[] args)
        {
            string documentation = "PreviewCommands.txt";
            if (args.Length != 0)
                documentation = args[0];
            using (FileStream file = new FileStream(Commands.GetPathInHomeHolder(documentation), FileMode.Create))
            using (StreamWriter writer = new StreamWriter(file))
            {
                writer.WriteLine(Help().Single());
                foreach (string commandName in CommandNames.Keys.OrderBy(k => k))
                {
                    writer.WriteLine(); // Add empty line
                    foreach (var line in GetCommandHelp(commandName))
                        writer.WriteLine(line);
                }
            }
            return new string[] { $"Document generated at {Commands.GetPathInHomeHolder(documentation)}" };
        }
        [Command("Goto a website.")]
        [CommandArgument("adress", "url of the website, can also be physical local file path")]
        public IEnumerable<string> G(params string[] args)
        {
            if (args.Length == 0 || !IsStringWebUrl(args[0]))
                return new string[] { "A valid address must be passed." };
            else if (PreviewBrowser.Visibility == Visibility.Visible)
            {
                PreviewBrowser.Address = args[0];
                return new string[] { $"Opening address {args[0]}." };
            }
            else
                return new string[] { "A preview browser must be visible; Use `switch` to open preview browser." };
        }
        [Command("Pause or continue currenly playing video.")]
        public IEnumerable<string> Pause(params string[] args)
        {
            if (VLCControl != null)
            {
                if (VLCControl.IsPlaying)
                    VLCControl.Pause();
                else
                    VLCControl.Play();
            }
            return null;
        }
        [Command("Play a new or continue play/pause a video.")]
        [CommandArgument("url", "an url to play", optional: true)]
        public IEnumerable<string> Play(params string[] args)
        {
            if (VLCControl != null)
            {
                if (VLCControl.IsPlaying)
                    VLCControl.Pause();
                else
                {
                    // Play new media
                    if (args.Length == 1)
                    {
                        VLCControl.Play(new Uri(args[0]));
                        // Disable VLC input capture and handle it ourselves
                        VLCControl.Video.IsMouseInputEnabled = false;
                        VLCControl.Video.IsKeyInputEnabled = false;
                    }
                    // Continue play old media
                    else
                        VLCControl.Play();
                }
                return null;
            }
            else
                return new string[] { "No preview source available. Open some video first." };
        }
        [Command("Set or Show available quick settings and current settings.")]
        [CommandArgument("setting", "the setting to set", optional: true)]
        public IEnumerable<string> Set(params string[] args)
        {
            if (args.Length == 0)
                return new string[] { $"Available settings: " +
                    $"nvp - No Video Preview; " +
                    $"Current settings: {string.Join(", ", QuickSettings.OrderBy(s => s))}" };
            else if (args.Length == 1)
            {
                string setting = args[0].ToLower();
                QuickSettings.Add(setting);
                return new string[] { $"`{setting}` is set." };
            }
            else
                return new string[] { $"Invalid number of arguments: {args.Length} is given, one or none is expected." };
        }
        [Command("Unset or Show available quick settings and current settings.")]
        [CommandArgument("setting", "the setting to unset", optional: true)]
        public IEnumerable<string> Unset(params string[] args)
        {
            if (args.Length == 0)
                return new string[] { $"Available settings: " +
                    $"nvp - No Video Preview; " +
                    $"Current settings: {string.Join(", ", QuickSettings.OrderBy(s => s))}" };
            else if (args.Length == 1)
            {
                string setting = args[0].ToLower();
                QuickSettings.Remove(setting);
                return new string[] { $"`{setting}` is unset." };
            }
            else
                return new string[] { $"Invalid number of arguments: {args.Length} is given, one or none is expected." };
        }
        [Command("Switch to a different preview type.")]
        [CommandArgument("type", "type of preview control to switch, must be either `video` or `web`")]
        public IEnumerable<string> SP(params string[] args)
        {
            string[] validOptions = new string[] {"web", "video", "w", "v" };
            if(args.Length == 0 || !validOptions.Contains(args[0].ToLower()))
                return new string[] { $"A valid type option must be passed: either {string.Join(" or ", validOptions)} is accepted." };
            else
            {
                // Clear preview
                ClearItemPreview();
                string type = args[0].ToLower();
                switch (type)
                {
                    case "w":
                    case "web":
                        PreviewBrowser.Visibility = Visibility.Visible;
                        break;
                    case "v":
                    case "video":
                        PreviewWindowsFormsHost.Visibility = Visibility.Visible;
                        break;
                    default:
                        throw new ArgumentException($"Unexpected preview type `{type}`");
                }
                return new string[] { $"Preview is changed to `{type}` type." };
            }
        }
        [Command("Show available preview actions.")]
        [CommandArgument("command", "name of the command to show details", optional: true)]
        public IEnumerable<string> Help(params string[] args)
        {
            // Show general help, i.e. commands list
            if (args.Length == 0)
            {
                string availableCommands = string.Join(", ", CommandNames.Keys.OrderBy(k => k));
                // Return single line
                return new string[] { $"Available commands: {availableCommands}." };
            }
            // Show help of specific command in a single line
            else if (args.Length == 1)
            {
                StringBuilder commandHelp = new StringBuilder();
                string commandName = args[0];
                MethodInfo command = CommandNames[commandName];
                CommandAttribute commandAttribute = CommandAttributes[command];
                commandHelp.Append($"{commandName} ({commandAttribute.Description}). ");
                CommandArgumentAttribute[] arguments = CommandArguments[command];
                if (arguments.Length != 0) commandHelp.Append($"Available options: ");
                for (int i = 0; i < arguments.Length; i++)
                {
                    var argument = arguments[i];
                    commandHelp.Append($"{argument.Name}{(argument.Optional ? "(Optional)" : "")} - {argument.Explanation}" +
                        $"{(i == arguments.Length - 1 ? "." : "; ")}");
                }                    
                return new string[] { commandHelp.ToString() };
            }
            else
                return new string[] { $"Invalid number or arguments: {args.Length} is given; one or none is expected." };
        }
        /// <summary>
        /// Get a formatted help info for a given command
        /// </summary>
        private IEnumerable<string> GetCommandHelp(string commandName)
        {
            List<string> commandHelp = new List<string>();
            MethodInfo command = CommandNames[commandName];
            CommandAttribute commandAttribute = CommandAttributes[command];
            commandHelp.Add($"{commandName} - {commandAttribute.Description}");
            if (commandAttribute.Documentation != null)
                commandHelp.Add($"\t{commandAttribute.Documentation}");
            var arguments = CommandArguments[command];
            if (arguments.Length != 0) commandHelp.Add($"\tOptions:");
            foreach (var argument in arguments)
                commandHelp.Add($"\t\t{argument.Name}{(argument.Optional ? "(Optional)" : "")} - {argument.Explanation}");
            return commandHelp;
        }
        #endregion
    }
}
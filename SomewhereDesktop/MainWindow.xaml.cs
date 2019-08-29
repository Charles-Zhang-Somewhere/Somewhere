using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            // Parse command line arguments
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
        }
        private void OpenRepository(string homeFolderpath)
        {
            void ShowRecentPathsDialog(string[] recent)
            {
                var options = recent.ToList();
                options.Add("Clear History");
                var dialog = new DialogWindow(null, "Recent homes", 
                    "Double click on a path to open Home repository.", options);
                dialog.ShowDialog();
                if (dialog.Selection == null)
                    this.Close();
                else if(dialog.Selection == "Clear History")
                {
                    CleanRecentHomePaths();
                    OpenHomeCommand_Executed(null, null);
                }
                else
                    OpenRepository(dialog.Selection);
            }

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
            VLCControl.VlcLibDirectory = libDirectory;
            VLCControl.EndInit();
        }
        private VlcControl VLCControl { get; set; }
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
        private void UpdateItemPreview()
        {
            // Clear previous
            PreviewText = PreviewImage = PreviewStatus = PreviewMarkdown = null;
            VLCControl.Stop();
            PreviewTextBox.Visibility = PreviewImageSource.Visibility = PreviewTextBlock.Visibility
                = PreviewMarkdownViewer.Visibility = PreviewWindowsFormsHost.Visibility
                = Visibility.Collapsed;
            // Return early in case of items refresh
            if (ActiveItem == null)
                return;
            // Preview knowledge and note (default as markdown)
            if (ActiveItem.Content != null || ActiveItem.Name == null)
            {
                PreviewMarkdownViewer.Visibility = Visibility.Visible;
                PreviewMarkdown = ActiveItem.Content;
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
                    PreviewImage = Commands.GetPhysicalPath(ActiveItem.Name);
                }
                // Preview markdown
                else if (extension == ".md")
                {
                    PreviewMarkdownViewer.Visibility = Visibility.Visible;
                    PreviewMarkdown = File.ReadAllText(Commands.GetPhysicalPath(ActiveItem.Name));
                }
                // Preview text
                else if (extension == ".txt")
                {
                    PreviewTextBox.Visibility = Visibility.Visible;
                    PreviewText = File.ReadAllText(Commands.GetPhysicalPath(ActiveItem.Name));
                }
                // Preview videos and audios
                else if (VideoFileExtensions.Contains(extension) || AudioFileExtensions.Contains(extension))
                {
                    PreviewWindowsFormsHost.Visibility = Visibility.Visible;
                    VLCControl.Play(new Uri(Commands.GetPhysicalPath(ActiveItem.Name)));
                }
                else
                {
                    PreviewTextBlock.Visibility = Visibility.Visible;
                    PreviewStatus = "No Preview Available for Binary Data";
                }
            }
        }
        private readonly static string[] ImageFileExtensions = new string[] { ".png", ".img", ".jpg", ".bmp" };
        private readonly static string[] AudioFileExtensions = new string[] { ".ogg", ".mp3", ".wav",".ogm" };
        private readonly static string[] VideoFileExtensions = new string[] { ".avi", ".flv", ".mp4", ".mpeg", ".wmv" };
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
            set { ActiveItem.Meta = Commands.ReplaceOrInitializeMetaAttribute(ActiveItem.Meta, "Remark", value); NotifyPropertyChanged(); TryCommitActiveItemChange(); ActiveItem.BroadcastPropertyChange(); }
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
            set { ActiveItem.Name = value; NotifyPropertyChanged(); TryCommitActiveItemChange(); ActiveItem.BroadcastPropertyChange(); RefreshTypeFilters(); if(ActiveItem == ActiveNote) NotifyPropertyChanged("ActiveNoteName"); }
        }
        public string ActiveItemTags
        {
            get => ActiveItem?.Tags;
            set { ActiveItem.Tags = value; NotifyPropertyChanged(); TryCommitActiveItemChange(); ActiveItem.BroadcastPropertyChange(); RefreshTags(); FilterItems(); if(ActiveItem == ActiveNote) NotifyPropertyChanged("ActiveNoteTags"); }
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
            set { ActiveNote.Name = value; NotifyPropertyChanged(); TryCommitActiveNoteChange(); ActiveNote.BroadcastPropertyChange(); RefreshTypeFilters(); if(ActiveNote == ActiveItem) NotifyPropertyChanged("ActiveItemName"); }
        }
        public string ActiveNoteTags
        {
            get => ActiveNote?.Tags;
            set { ActiveNote.Tags = value; NotifyPropertyChanged(); TryCommitActiveNoteChange(); ActiveNote.BroadcastPropertyChange(); RefreshTags(); FilterItems(); if(ActiveNote == ActiveItem) NotifyPropertyChanged("ActiveItemTags"); }
        }
        public string ActiveNoteContent
        {
            get => ActiveNote?.Content;
            set { ActiveNote.Content = value; NotifyPropertyChanged(); TryCommitActiveNoteChange(); ActiveNote.BroadcastPropertyChange(); if (ActiveNote == ActiveItem) UpdateItemPreview(); }
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
                    InfoText = result.First();
            }
        }
        private void CloseWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void CloseWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // In case things are not saved, commit change for safety and avoid data loss
            TryCommitActiveItemChange();
            TryCommitActiveNoteChange();
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
            // Save old note by changing focus
            NotenameTextBox.Focus();
            NoteContentTextBox.Focus();
            NotenameTextBox.Focus();
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
            NotenameTextBox.Focus();
            NotenameTextBox.SelectAll();
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
            if(e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ConsoleInput))
            {
                // Break a chord into notes, each key represent a seperate command
                string[] chord = ConsoleInput.Split(new string[] { "\\n" }, StringSplitOptions.RemoveEmptyEntries);
                StringBuilder result = new StringBuilder();
                foreach (string chordNote in chord)
                {
                    string[] positions = chordNote.BreakCommandLineArgumentPositions();
                    string command = positions.GetCommandName();
                    string[] arguments = positions.GetArguments();
                    if (chord.Length > 1)
                        result.AppendLine($"{command}:");
                    // Disabled unsupported commands (i.e. those commands that have console input)
                    if (command == "files" || command == "find")
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
                e.Handled = true;
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
            if (ActiveItem != null)
                System.Diagnostics.Process.Start(Commands.GetPhysicalPath(ActiveItem.Name));
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
            => this.DragMove();
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            // Safety
            TryCommitActiveItemChange();
            TryCommitActiveNoteChange();

            // Dispose resources
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
            if (Commands == null) return;
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
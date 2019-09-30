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
using InteropCommon;
using Vlc.DotNet.Forms;
using System.Text.RegularExpressions;

namespace SomewhereDesktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Constructor
        private readonly CefSharp.Wpf.CefSettings CefSettings = new CefSharp.Wpf.CefSettings()
        {
            RemoteDebuggingPort = 8088
        };
        public MainWindow()
        {
            // Initialize Cef
            CefSharp.Cef.Initialize(CefSettings);

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
            {
                Close();
                return;
            }

            // Initialize Video Preview Control
            InitializeVLCControl();
            // Initialize CefSharp Browser Control
            PreviewBrowser.MenuHandler = new CustomMenuHandler();
            uint ColorToUInt(Color color)
                => (uint)((color.A << 24) | (color.R << 16) | (color.G << 8) | (color.B << 0));
            PreviewBrowser.BrowserSettings.BackgroundColor = ColorToUInt(Color.FromArgb(255, 255, 255, 255));
            // Check new versions in background
            BackgroundCheckNewVersion();

            // Set identifiable window title
            Title = "Somewhere - " + System.IO.Path.GetFileName(Commands.HomeDirectory.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        }
        private void ShowRecentPathsDialog(string[] recent)
        {
            var options = recent.ToList();
            options.Add("Clear History");
            var dialog = new DialogWindow(this?.Visibility == Visibility.Visible ? this : null, 
                "Recent homes", "Double click on a path to open Home repository.", options);
            dialog.ShowDialog();
            if (dialog.Selection == null)
            {
                // Otherwise if we have not already opened a home, close the application (e.g. during startup)
                if (Commands == null || !Commands.IsHomePresent)
                    this.Close();
                // Just return if no action is selected
                else 
                    return;
            }
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
                {
                    // Put in an try-catch to handle situation like pre-mature program exit e.g. during home selection dialog
                    try
                    {
                        await Dispatcher.BeginInvoke(DispatcherPriority.Background,
                        new Action(() => {
                            InfoText = "Current version is up-to-date.";
                        }));
                    }
                    catch (Exception){ }
                }
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
                .Select(i => GetItemExtensionType(i)).Distinct().OrderBy(f => f));
            TypeFilters.Insert(0, "");    // Empty filter cleans filtering
        }
        private string GetItemExtensionType(FileItemObjectModel item)
        {
            // Knowledge
            if (item.Name == null)
                return "Knowledge";
            // Folder
            else if (item.Name.EndsWith("/") || item.Name.EndsWith("\\"))
                return "Folder";
            // Note exclude knowledge
            else if (item.Content != null && item.Name != null)
                return "Note";
            // Ordinary files
            else // item.Content == null
            {
                string extension = System.IO.Path.GetExtension(Commands.GetPhysicalNameForFilesThatCanBeInsideFolder(item.Name));
                if (string.IsNullOrEmpty(extension))
                    return "None";  // File without extension
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
                    Items.Where(i => GetItemExtensionType(i) == _SelectedTypeFilter));
            // Filter by tags
            if (TagFilters.Count != 0)
                Items = new ObservableCollection<FileItemObjectModel>(
                    Items.Where(i => i.TagsList.Intersect(TagFilters).Count() == TagFilters.Count));
        }
        private void ClearItemPreview()
        {
            PreviewText = PreviewImage = PreviewStatus = PreviewMarkdown = null;
            VLCControl.Stop();
            PreviewTextBox.Visibility = PreviewImageSource.Visibility = PreviewTextBlock.Visibility
                = PreviewMarkdownViewer.Visibility = PreviewWindowsFormsHost.Visibility
                = PreviewBrowser.Visibility
                = Visibility.Collapsed;
        }
        private void DeleteTemporaryFiles()
        {
            foreach (var path in TempFiles)
                File.Delete(path);
            TempFiles.Clear();
        }
        /// <summary>
        /// A collection of temporary files generated for preview
        /// </summary>
        private List<string> TempFiles = new List<string>();
        /// <summary>
        /// Create and return full path for a temp file (file not created yet);
        /// May or may not contain an extension, caller should not depend on that
        /// </summary>
        private string GetTempFileName()
            // Use a Home local file path instead of System.IO.GetTempFileName() for referencing local files
            => System.IO.Path.Combine(Commands.HomeDirectory, Guid.NewGuid().ToString());
        private string ExtractValueAfterSymbol(string inputText, string lineSymbol, string template)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var line in SplitToLines(inputText))
            {
                if (line.StartsWith(lineSymbol))
                    builder.AppendLine(string.Format(template, line.Substring(lineSymbol.Length)));
            }
            return builder.ToString();
        }
        private const string CSSSymbol = "@css ";
        private const string LibSymbol = "@lib ";
        /// <summary>
        /// Extract and form a string containing CSS links
        /// </summary>
        private string ExtractCSSLinks(string script)
            => ExtractValueAfterSymbol(script, CSSSymbol, "<link rel=\"stylesheet\" href=\"{0}\" >");
        /// <summary>
        /// Extract and form a string containing Javascript libraries
        /// </summary>
        private string ExtractLibraries(string script)
            => ExtractValueAfterSymbol(script, LibSymbol, "<script src=\"{0}\"></script>");
        /// <summary>
        /// Extract lines that doesn't start with symbol
        /// </summary>
        private string ExtractNormalScript(string script)
        {
            StringBuilder builder = new StringBuilder();
            foreach (var line in SplitToLines(script))
            {
                if (line.StartsWith(CSSSymbol))
                    continue;
                else if (line.StartsWith(LibSymbol))
                    continue;
                else
                    builder.AppendLine(line);
            }
            return builder.ToString();
        }
        private void UpdateItemPreview()
        {
            // Clear previous
            ClearItemPreview();
            DeleteTemporaryFiles();
            // Return early in case of items refresh
            if (ActiveItem == null)
                return;
            // Preview knowledge and note (default as markdown)
            if (ActiveItem.Content != null || ActiveItem.Name == null)
            {
                // Preview web link
                if (ActiveItem.Content != null /* Make sure it is a note */
                    && !string.IsNullOrWhiteSpace(ActiveItem.Content) /* Make sure it's not empty - if it's empty then it will pass test as url and cause showing browser */
                    && ActiveItem.Content.IndexOfAny(new char[] { '\r', '\n' }) == -1   /* Make sure it contains only a single line */
                    && IsStringWebUrl(ActiveItem.Content) /* Make sure it's a url */)
                {
                    PreviewBrowser.Visibility = Visibility.Visible;
                    PreviewAddress = ActiveItem.Content;
                }
                // Preview as HTML
                else if (ActiveItem.Content.StartsWith("<!DOCTYPE html>"))
                {
                    PreviewBrowser.Visibility = Visibility.Visible;
                    string temp = GetTempFileName() + ".html";
                    File.WriteAllText(temp, ActiveItem.Content);
                    PreviewAddress = temp;
                    TempFiles.Add(temp);
                }
                // Preview as HTML using SVG
                else if (ActiveItem.Content.StartsWith("<!-- svg -->"))
                {
                    string GenerateHtmlTemplateForSVG(string svg)
                        => "<!DOCTYPE html>\n" +
                        "<html>\n" +
                        "<head>\n" +
                        "   <title>Sketch - svg</title>\n" +
                        "   <meta charset=\"utf-8\">\n" +
                        "</head>\n" +
                        "<body>\n" +
                        "   <h3>Sketch with svg</h3>\n" +
                        svg.Replace("@xmlns", "xmlns=\"http://www.w3.org/2000/svg\"") +
                        "</body>\n" +
                        "</html>";
                    PreviewBrowser.Visibility = Visibility.Visible;
                    string temp = GetTempFileName() + ".html";
                    File.WriteAllText(temp, GenerateHtmlTemplateForSVG(ActiveItem.Content));
                    PreviewAddress = temp;
                    TempFiles.Add(temp);
                }
                // Preview as HTML using Three.js
                else if(ActiveItem.Content.StartsWith("// Three.js"))
                {
                    string GenerateHtmlTemplateForThreeJS(string script)
                        => "<!DOCTYPE html>\n" +
                        "<html>\n" +
                        "<head>\n" +
                        "   <meta charset=\"utf-8\">\n" +
                        "   <title>Sketch - Three.js</title>\n" +
                        "   <style>\n" +
                        "       body { margin: 0; background: black; }\n" +   // For Three.js, use default black background
                        "       canvas { width: 100%; height: 100% }\n" +
                        "   </style>\n" +
                        ExtractCSSLinks(script) +
                        "</head>\n" +
                        "<body>\n" +
                        "   <h3 style=\"color: white;\">Sketch with Three.js</h3>\n" +
                        "   <script src=\"https://threejs.org/build/three.js\"></script>\n" +
                        ExtractLibraries(script) + 
                        // If the script contains explicit script tag, i.e. it may contain other explicit items, then we just include it
                        (script.Contains("</script>") ? ExtractNormalScript(script)
                        // Otherwise it's pure JS and we create a script tag for it
                        : ("<script>" + ExtractNormalScript(script) + "</script>")) +
                        "</body>\n" +
                        "</html>";
                    PreviewBrowser.Visibility = Visibility.Visible;
                    string temp = GetTempFileName() + ".html";
                    File.WriteAllText(temp, GenerateHtmlTemplateForThreeJS(ActiveItem.Content));
                    PreviewAddress = temp;
                    TempFiles.Add(temp);
                }
                // Preview as HTML using PlotlyJS
                else if (ActiveItem.Content.StartsWith("<!-- Plotly.js -->"))
                {
                    string GenerateHtmlTemplateForPlotlyJS(string script)
                        => "<!DOCTYPE html>\n" +
                        "<html>\n" +
                        "<head>\n" +
                        "   <title>Sketch - Plotly.js</title>\n" +
                        ExtractCSSLinks(script) + 
                        "</head>\n" +
                        "<body>\n" +
                        "   <h3>Sketch with Plotly.js</h3>\n" +
                        "   <script src=\"https://cdn.plot.ly/plotly-latest.min.js\"></script>\n" +
                        ExtractLibraries(script) + 
                        ExtractNormalScript(script) +
                        "</body>\n" +
                        "</html>";
                    PreviewBrowser.Visibility = Visibility.Visible;
                    string temp = GetTempFileName() + ".html";
                    File.WriteAllText(temp, GenerateHtmlTemplateForPlotlyJS(ActiveItem.Content));
                    PreviewAddress = temp;
                    TempFiles.Add(temp);
                }
                // Preview as HTML using Boostrap and JQuery
                else if (ActiveItem.Content.StartsWith("<!-- Bootstrap -->")
                    || ActiveItem.Content.StartsWith("<!-- JQuery.js -->"))
                {
                    string GenerateHtmlTemplateForBootstrap(string script)
                        => "<!DOCTYPE html>\n" +
                        "<html>\n" +
                        "<head>\n" +
                        "<title>Sketch - Boostrap (JQuery)</title>\n" +
                        "   <meta charset=\"utf-8\">\n" +
                        "   <meta name=\"viewport\" content=\"width=device-width, initial-scale=1, shrink-to-fit=no\">\n" +
                        "   <link rel=\"stylesheet\" href=\"https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/css/bootstrap.min.css\" integrity=\"sha384-ggOyR0iXCbMQv3Xipma34MD+dH/1fQ784/j6cY/iJTQUOhcWr7x9JvoRxT2MZw1T\" crossorigin=\"anonymous\">\n" +
                        ExtractCSSLinks(script) + 
                        "</head>\n" +
                        "<body>\n" +
                        "   <h3>Sketch with Boostrap and JQuery.js</h3>" +
                        "   <script src=\"https://code.jquery.com/jquery-3.3.1.slim.min.js\" integrity=\"sha384-q8i/X+965DzO0rT7abK41JStQIAqVgRVzpbzo5smXKp4YfRvH+8abtTE1Pi6jizo\" crossorigin=\"anonymous\"></script>" +
                        "   <script src=\"https://cdnjs.cloudflare.com/ajax/libs/popper.js/1.14.7/umd/popper.min.js\" integrity=\"sha384-UO2eT0CpHqdSJQ6hJty5KVphtPhzWj9WO1clHTMGa3JDZwrnQq4sF86dIHNDz0W1\" crossorigin=\"anonymous\"></script>" +
                        "   <script src=\"https://stackpath.bootstrapcdn.com/bootstrap/4.3.1/js/bootstrap.min.js\" integrity=\"sha384-JjSmVgyd0p3pXB1rRibZUAYoIIy6OrQ6VrjIEaFf/nJGzIxFDsf4x0xIM+B07jRM\" crossorigin=\"anonymous\"></script>" +
                        ExtractLibraries(script) + 
                        ExtractNormalScript(script) +
                        "</body>\n" +
                        "</html>";
                    PreviewBrowser.Visibility = Visibility.Visible;
                    string temp = GetTempFileName() + ".html";
                    File.WriteAllText(temp, GenerateHtmlTemplateForBootstrap(ActiveItem.Content));
                    PreviewAddress = temp;
                    TempFiles.Add(temp);
                }
                // Preview as HTML using Processing JS
                else if(ActiveItem.Content.StartsWith("// Processing.js"))
                {
                    string GenerateHtmlTemplateForProcessingJS(string script)
                        => "<!DOCTYPE html>\n" +
                        "<html>\n" +
                        "<head>\n" +
                        "   <title>Sketch - Processing.js</title>\n" +
                        ExtractCSSLinks(script) + 
                        "</head>\n" +
                        "<body>\n" +
                        "   <h3>Sketch with Processing.js</h3>\n" +
                        "   <script src=\"https://cdnjs.cloudflare.com/ajax/libs/processing.js/1.6.6/processing.min.js\"></script>\n" +
                        ExtractLibraries(script) + 
                        "   <script type=\"application/processing\" data-processing-target=\"pjs\">\n" +
                        ExtractNormalScript(script) + 
                        "</script>\n" +
                        "<canvas id=\"pjs\"> </canvas>\n" +
                        "</body>\n" +
                        "</html>";
                    PreviewBrowser.Visibility = Visibility.Visible;
                    string temp = GetTempFileName() + ".html";
                    File.WriteAllText(temp, GenerateHtmlTemplateForProcessingJS(ActiveItem.Content));
                    PreviewAddress = temp;
                    TempFiles.Add(temp);
                }
                // Preview as HTML using P5 JS
                else if (ActiveItem.Content.StartsWith("// P5.js"))
                {
                    string GenerateHtmlTemplateForP5JS(string script)
                        => "<!DOCTYPE html>\n" +
                        "<html>\n" +
                        "<head>\n" +
                        "   <title>Sketch - P5.js</title>\n" +
                        ExtractCSSLinks(script) +
                        "</head>\n" +
                        "<body>\n" +
                        "   <h3>Sketch with Processing.js</h3>\n" +
                        "   <script src=\"https://cdnjs.cloudflare.com/ajax/libs/p5.js/0.9.0/p5.min.js\"></script>\n" +
                        "   <script src=\"https://cdnjs.cloudflare.com/ajax/libs/p5.js/0.9.0/addons/p5.dom.min.js\"></script>\n" +
                        "   <script src=\"https://cdnjs.cloudflare.com/ajax/libs/p5.js/0.9.0/addons/p5.sound.min.js\"></script>\n" +
                        ExtractLibraries(script) + 
                        "   <script>\n" +
                        ExtractNormalScript(script) +
                        "   </script>\n" +
                        "</body>\n" +
                        "</html>";
                    PreviewBrowser.Visibility = Visibility.Visible;
                    string temp = GetTempFileName() + ".html";
                    File.WriteAllText(temp, GenerateHtmlTemplateForP5JS(ActiveItem.Content));
                    PreviewAddress = temp;
                    TempFiles.Add(temp);
                }
                // Preview as HTML Javascript Notebook
                else if(ActiveItem.Content.Contains("```jsx"))
                {
                    PreviewBrowser.Visibility = Visibility.Visible;
                    string temp = GetTempFileName() + ".html";
                    File.WriteAllText(temp, GenerateJavascriptNotebook(ActiveItem.Content));
                    PreviewAddress = temp;
                    TempFiles.Add(temp);
                }
                // Preview markdown
                else
                {
                    PreviewMarkdownViewer.Visibility = Visibility.Visible;
                    // Source code
                    var lang = CheckCodeLanguage(null, ActiveItem.Content);
                    if (lang == LanguageType.CityScript)
                        PreviewMarkdown = $"({lang.ToString()} notebook - use `cr` to run)\n{ActiveItem.Content}";
                    else if (lang != LanguageType.Unidentified)
                        PreviewMarkdown = $"{lang.ToString()} Code - use `cr` to run:\n```\n{ActiveItem.Content}\n```";
                    // Normal markdown
                    else
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
                string extension = System.IO.Path.GetExtension(Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name)).ToLower();
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
                else if (extension == ".txt" || extension == ".conf")
                {
                    PreviewTextBox.Visibility = Visibility.Visible;
                    PreviewText = File.ReadAllText(Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name));
                }
                // Preview source code
                else if(CompilableSourceCodeExtensions.Contains(extension))
                {
                    PreviewMarkdownViewer.Visibility = Visibility.Visible;
                    string text = File.ReadAllText(Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name));
                    // Source code
                    var lang = CheckCodeLanguage(extension, text);
                    if(lang == LanguageType.CityScript)
                        PreviewMarkdown = $"({lang.ToString()} notebook - use `cr` to run)\n{text}";
                    else if (lang != LanguageType.Unidentified)
                        PreviewMarkdown = $"{lang.ToString()} Code - use `cr` to run:\n```\n{text}\n```";
                    else
                        PreviewMarkdown = $"```{text}\n```";
                }
                // Preview videos and audios
                else if (VideoFileExtensions.Contains(extension) || AudioFileExtensions.Contains(extension))
                {
                    PreviewWindowsFormsHost.Visibility = Visibility.Visible;
                    CurrentVideoUrl = Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name);
                    Play(CurrentVideoUrl);
                }
                // Preview webpages
                else if(extension == ".html"
                    // Preview pdf
                    || extension == ".pdf"
                    // Preview gif
                    || extension == ".webp"
                    || extension == ".gif")
                {
                    PreviewBrowser.Visibility = Visibility.Visible;
                    PreviewAddress = Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name);
                }
                else
                {
                    PreviewTextBlock.Visibility = Visibility.Visible;
                    PreviewStatus = "No Preview Available for Binary Data";
                }
            }
        }
        private readonly static string[] ImageFileExtensions = new string[] { ".png", ".img", ".jpg", ".jpeg", ".bmp" };
        private readonly static string[] AudioFileExtensions = new string[] { ".ogg", ".mp3", ".wav",".ogm", ".m4a" };
        private readonly static string[] VideoFileExtensions = new string[] { ".avi", ".flv", ".mp4", ".mpeg", ".wmv", ".mpg" };
        /// <summary>
        /// Extensions that can be compiled/interpreted/executed
        /// </summary>
        private readonly static string[] CompilableSourceCodeExtensions = new string[] { ".bat", ".c", ".cpp", ".cs", ".python", ".pbrt" };
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
        private string _PreviewAddress;
        private Stack<string> BrowseHistory = new Stack<string>();
        private bool StepingBack = false;
        public string PreviewAddress
        {
            get => _PreviewAddress;
            set
            {
                // Save current address as a note
                if (!Keyboard.IsKeyDown(Key.Tab) // Avoid the case when we are merely update notes and go to tags field of note
                    && !(Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))// Avoid the case when we are merely use PreviewNote command to preview note
                    && (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                    CreateNewNote(PreviewBrowser.Title, value);
                else
                {
                    // If we are stepping back, then don't save this address changing value
                    if (!StepingBack)
                        BrowseHistory.Push(value);
                    // Set value for the browser control
                    StepingBack = false;
                    SetField(ref _PreviewAddress, value);
                }
            }
        }
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
        private void CommandDumpAll_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void CommandDumpAll_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Go to inventory tab
            TabHeader_MouseDown(InventoryTabLabel, null);
            // Dump all and output
            try
            {
                InfoText = Commands.Dump("all").Single();
            }
            catch (Exception error)
            {
                new DialogWindow(this, "Error when dumping", error.Message).ShowDialog();
                InfoText = "An error occured during importing.";
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
            RefreshNotes();
            InfoText = $"{AllItems.Count()} items discovered.";
        }
        private void CompileAndRunCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
        {
            string extension = (ActiveItem?.Name != null 
                ? System.IO.Path.GetExtension(Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name)).ToLower()
                : null) ?? null;
            Visibility visibility = (ActiveItem != null && CheckCodeLanguage(extension, ActiveItem.Content) != LanguageType.Unidentified)
                ? Visibility.Visible
                : Visibility.Collapsed;
            CompileAndRunButton.Visibility = visibility;
            e.CanExecute = visibility == Visibility.Visible;
        }
        private void CompileAndRunCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => InfoText = CR().Single();
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
        private void CreateNewNote(string title, string content)
        {
            // Save old note by forcing update
            NoteNameTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            NoteContentTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
            // Add to database
            int id = Commands.AddFile(title, content);
            // Add to view collection
            var item = new FileItemObjectModel(Commands.GetFileDetail(id));
            // Add to items cache
            AllItems.Add(item);
            RefreshItems();
            // Update note collection
            RefreshNotes();
            // Set activead
            ActiveNote = item;
            // Show note panel and focus on editing textbox
            TabHeader_MouseDown(NotebookTabLabel, null);
            NoteNameTextBox.Focus();
            NoteNameTextBox.SelectAll();
        }
        private void CreateNoteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => CreateNewNote(null, string.Empty);  // Give some default empty content (i.e. not null) so it's not considered a binary file
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
        private void PreviewNote_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = ActiveNote != null && NotebookPanel.Visibility == Visibility.Visible;
        private void PreviewNote_CanExecute(object sender, ExecutedRoutedEventArgs e)
        {
            ActiveItem = ActiveNote;
            SaveCommand_Executed(null, null);   // Force saving and updating
            TabHeader_MouseDown(InventoryTabLabel, null);
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
        /// <summary>
        /// A list of command history that can dynamically update itself including order
        /// </summary>
        private List<string> CommandHistory = new List<string>();
        /// <summary>
        /// Index for current command history navigation
        /// </summary>
        private int CurrentCommandHistoryIndex = 0;
        private void ConsoleCommandInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            void AddCommandToHistory(string input)
            {
                // Add
                CommandHistory.Add(input);
                // Distinct
                CommandHistory = CommandHistory.Distinct().ToList();
                // Update index
                CurrentCommandHistoryIndex = CommandHistory.Count - 1;
            }
            string NavigateBackHistory()
            {
                // Get current
                string current = CommandHistory[CurrentCommandHistoryIndex];
                // Decrement
                CurrentCommandHistoryIndex--;
                // Roll back
                if (CurrentCommandHistoryIndex < 0)
                    CurrentCommandHistoryIndex = CommandHistory.Count - 1;
                // Return selection
                return current;
            }

            // Execute command
            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(ConsoleInput))
            {
                // Add to history
                AddCommandToHistory(ConsoleInput);
                // Evaluate
                var oldReadValue = Commands.ReadFromConsoleEnabled;
                var oldWriteValue = Commands.WriteToConsoleEnabled;
                Commands.ReadFromConsoleEnabled = false;
                Commands.WriteToConsoleEnabled = false;
                // Break a chord into notes, each note represent a seperate command
                var chord = ConsoleInput.Split(new string[] { "\\n" /* Like, literally, `\n` */ }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim());
                StringBuilder result = new StringBuilder();
                foreach (string chordNote in chord)
                {
                    string[] positions = chordNote.BreakCommandLineArgumentPositions();
                    string commandName = positions.GetCommandName().ToLower();
                    string[] arguments = positions.GetArguments();
                    if (chord.Count() > 1)
                        result.AppendLine($"{commandName}:");
                    // Disabled unsupported commands (i.e. those commands that have console input)
                    string[] disabledCommands = new string[] { "purge", "x" };
                    if (disabledCommands.Contains(commandName))
                    {
                        ConsoleResult = $"Command `{commandName}` is not supported here, please use a real console emulator instead.";
                        break;
                    }
                    else
                    {
                        foreach (var line in Commands.ExecuteCommand(commandName, arguments))
                            result.AppendLine(line);
                    }
                    // Automatically refresh items
                    if (new string[] { "add", "create", "generate", "im", "mv", "mt", "mvt", "rm", "rmt", "tag", "untag", "update" }
                        .Contains(commandName))
                        RefreshCommand_Executed(null, null);
                    // Automatically refresh status
                    else if (new string[] { "add", "doc", "dump", "export", "im", "mv", "purge", "rm" }
                        .Contains(commandName))
                        ShowUpdateStatusPanel();
                }
                ConsoleResult = result.ToString();
                ConsoleInput = string.Empty;
                Commands.ReadFromConsoleEnabled = oldReadValue;
                Commands.WriteToConsoleEnabled = oldWriteValue;
                e.Handled = true;
            }
            // Roll back history (without deleting)
            else if(e.Key == Key.Up)
            {
                // Update
                ConsoleInput = NavigateBackHistory();
                // Select
                ConsoleInputTextBox.SelectAll();
                e.Handled = true;
            }
        }
        /// <summary>
        /// Provide support for jumping back
        /// </summary>
        private void PreviewBrowser_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Disabled because it currently has issue with when user is doing typing
            return;
            if(e.Key == Key.Back && BrowseHistory.Count() != 0)
            {
                BrowseBackHistory();
                e.Handled = true;
            }
        }
        private string BrowseBackHistory()
        {
            BrowseHistory.Pop();    // Pop current

            // Go back one step
            StepingBack = true;
            PreviewAddress = BrowseHistory.Pop();
            return PreviewAddress;
        }
        /// <summary>
        /// Simple action command handling interface for previewed content
        /// </summary>
        private void PreviewActionInputTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            void GetCurrentPreviewInformation()
            {
                // Shows title and address
                if (PreviewBrowser.Visibility == Visibility.Visible)
                    PreviewInput = $"Title: {PreviewBrowser.Title}; Address: {PreviewBrowser.Address}";
                else if(PreviewWindowsFormsHost.Visibility == Visibility.Visible)
                    PreviewInput = $"Path: {CurrentVideoUrl}";
            }

            if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(PreviewInput))
            {
                // Shows current browsing or video address
                if (PreviewInput.ToLower() == "current")
                    GetCurrentPreviewInformation();
                // Process and normal command
                else
                {
                    // Break a chord into notes, each note represent a seperate command
                    var chord = PreviewInput.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(c => c.Trim());
                    // Handle commands (in lower case)
                    foreach (string chordNote in chord)
                    {
                        string[] positions = chordNote.BreakCommandLineArgumentPositions();
                        string commandName = positions.GetCommandName().ToLower();
                        string[] arguments = positions.GetArguments();
                        ProcessPreviewCommand(commandName, arguments);
                    }
                    PreviewInput = string.Empty;
                }
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
            if(e.Key == Key.Tab)
            {
                TextBox textBox = sender as TextBox;
                if(!string.IsNullOrEmpty(textBox.SelectedText))
                {
                    // Remove indentation
                    if (Keyboard.IsKeyDown(Key.LeftShift))
                        textBox.SelectedText = string.Join(Environment.NewLine, SplitToLines(textBox.SelectedText).Select(r => Regex.Replace(r, "^\\t", "")));
                    // Add indentation to whole section of text
                    else
                        textBox.SelectedText = string.Join(Environment.NewLine, SplitToLines(textBox.SelectedText).Select(r => r.Insert(0, "\t")));
                    e.Handled = true;
                }
                // Focus on tags field
                else if(Keyboard.IsKeyDown(Key.LeftShift))
                {
                    e.Handled = true;
                    NoteTagsTextBox.Focus();
                }
            }
            else if (e.Key == Key.Enter)
            {
                TextBox textBox = sender as TextBox;
                int caret = textBox.CaretIndex;
                string lastLineWhiteSpaces = Regex.Match(SplitToLines(textBox.Text.Substring(0, caret)).LastOrDefault() ?? string.Empty, 
                    "^[ \\t]*").Value;
                string insertion = Environment.NewLine + lastLineWhiteSpaces;
                textBox.Text = textBox.Text.Insert(caret, insertion);
                textBox.CaretIndex = caret + insertion.Length;
                e.Handled = true;
            }
        }
        /// <summary>
        /// Provides handling for Markdown syntax related shortcuts
        /// </summary>
        private void NoteContentTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            if (textBox.SelectionLength != 0)
            {
                if (e.Text == "*")
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
                // Make sure the selection is not an "Note" item
                && ActiveItem.Content == null && ActiveItem.Name != null)
                System.Diagnostics.Process.Start(Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name));
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if(e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
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

            // Delete temp file
            DeleteTemporaryFiles();
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
        private enum LanguageType
        {
            Unidentified,
            CPP,
            CSharp,
            Python,  // Python 3
            Pbrt,
            CityScript
        }
        /// <summary>
        /// Check language type of a given piece of code;
        /// Extension (lower cased) if not null will take prescendence than heruistics from code
        /// </summary>
        private LanguageType CheckCodeLanguage(string extension, string code)
        {
            if(extension != null)
            {
                switch (extension)
                {
                    case ".c":
                    case ".cpp":
                        return LanguageType.CPP;
                    case ".cs":
                        return LanguageType.CSharp;
                    case ".python":
                        return LanguageType.Python;
                    case ".pbrt":
                        return LanguageType.Pbrt;
                    case ".city":
                        return LanguageType.CityScript;
                    default:
                        break;
                }
            }
            if(code == null)
                return LanguageType.Unidentified;

            if (code.Contains("#include"))
                return LanguageType.CPP;
            else if (code.Contains("using System;"))
                return LanguageType.CSharp;
            // Use heuristics to guess host language is Python
            else if (code.StartsWith("def ") || code.Contains("__main__")
                // Single line statements
                || code.EndsWith(")") || code.EndsWith("]")
                // Single line expressions
                || (!code.Contains("\n") && !code.Contains("=") && code.IndexOfAny(new char[] { '+', '-', '*', '/', '^' }) != -1))
                return LanguageType.Python;
            // Prbt
            else if (code.Contains("WorldBegin") && code.Contains("WorldEnd"))
                return LanguageType.Pbrt;
            else if (code.Contains("```cityscript"))
                return LanguageType.CityScript;
            else return LanguageType.Unidentified;
        }
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
        private string[] SplitToLines(string input)
                => input.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
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
                    Commands.ChangeFileName(ActiveItem.ID, string.IsNullOrWhiteSpace(ActiveItem.Name) ? null : ActiveItem.Name);
                    // Commit Tag change
                    Commands.ChangeFileTags(ActiveItem.ID, ActiveItem.TagsList);
                    // Update log and info display
                    Commands.AddLog("Update Item", $"Item #{ActiveItem.ID} {(string.IsNullOrWhiteSpace(ActiveItem.Name) ? "(Knowledge)" : $"`{ActiveItem.Name}`")} is updated in SD (Somewhere Desktop).");
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
                return SplitToLines(File.ReadAllText(RecentFile));
            else return new string[] { };
        }
        /// <summary>
        /// Read content of an embedded resource
        /// </summary>
        private string GetEmbeddedResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            string GetResourceName(string input)
                => assembly.GetManifestResourceNames()
                .Single(str => str.EndsWith(name));

            var resourceName = GetResourceName(name);

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }
        /// <summary>
        /// Generate an HTML page for given Javascript Notebook
        /// </summary>
        private string GenerateJavascriptNotebook(string text)
        {
            string template = GetEmbeddedResource("notebook-template.html");
            string library = GetEmbeddedResource("notebook-library.js");
            string parser = GetEmbeddedResource("notebook-parser.js");
            string escapedString = ExtractNormalScript(text)
                // Replace all jsx code section with actual javascript code section for syntax highlighting
                .Replace("```jsx", "```javascript")
                // Replace javascript string literals
                .Replace("\\", "\\\\")    // Escape backslash
                .Replace("\"", "\\\"")  // Escape double quote
                .Replace("'", "\\'")    // Escape single quote
                .Replace("\t", "\\t")       // Escape tab
                // Replace new line character
                .Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");
            string html = template
                .Replace("@css", ExtractCSSLinks(text))
                .Replace("@lib", ExtractLibraries(text))
                .Replace("@core", library)
                .Replace("@parser", parser)
                .Replace("@data", $"'{escapedString}'");
            return html;
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
            else InfoText = $"Specified command `{commandName}` doesn't exist. Try again; Use `help` to see available commands.";
        }
        [Command("Navigates back browser history.")]
        public IEnumerable<string> B(params string[] args)
        {
            if (PreviewBrowser.Visibility == Visibility.Visible)
                return new string[] { BrowseBackHistory() };
            else
                return new string[] { "No preview browser is available." };
        }
        [Command("Compile and run.")]
        public IEnumerable<string> CR(params string[] args)
        {
            string previewCode = ActiveItem.Content     // From virtual note
                ?? File.ReadAllText(Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name));    // From physical file 
            // (notice in this case a redundant temp copy will be created so little additional code logic 
            // is needed for current implementation)

            // Compile CPP snippets, return compiled target path
            string CompileCPP()
            {
                string temp = GetTempFileName();
                string sourcePath = temp + ".cpp";
                string batPath = temp + ".bat";
                string objectPath = temp + ".obj";
                string targetPath = temp + ".exe";
                // Dump source
                System.IO.File.WriteAllText(sourcePath, previewCode);
                try
                {
                    // Get the location of VS (2019) compiler dev command line
                    string appendix = @"Microsoft Visual Studio\2019\Community\Common7\Tools\VsDevCmd.bat";
                    string devCmdPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), appendix);
                    if (!System.IO.File.Exists(devCmdPath))
                        throw new InvalidOperationException($"No VS2019 CPP compiler folder found on this system at `{devCmdPath}`.");
                    // Dump bat: notice for CPP we need to setup proper environment (including PATH and include dir etc.) so we need to 
                    // run the dev cmd first, which forces us to create a bat for multiple commands
                    // And for some reason, the bat cannot contain multiple lines without causing issue (probably due to encoding),
                    // so we are running everything as one line
                    // Also if characters contain Unicode it will not run properly so we are writing out as ANSI (Encoding.Default)
                    using (StreamWriter sw = new StreamWriter(File.Open(batPath, FileMode.OpenOrCreate), Encoding.Default))
                    {
                        sw.Write($"@ECHO OFF && \"{devCmdPath}\" && cl \"{sourcePath}\" -o \"{targetPath}\"");
                    }
                    using (Process myProcess = new Process())
                    {
                        myProcess.StartInfo.UseShellExecute = false;
                        myProcess.StartInfo.FileName = batPath;
                        myProcess.StartInfo.CreateNoWindow = true;
                        myProcess.StartInfo.RedirectStandardError = true;
                        myProcess.StartInfo.RedirectStandardOutput = true;
                        myProcess.Start();
                        string message = myProcess.StandardError.ReadToEnd() + myProcess.StandardOutput.ReadToEnd();  // Notice the order might be messed up
                        myProcess.WaitForExit();
                        // Due to how CL works, the error may be just normal output (e.g. warnings), instead of critical errors, so just display it
                        if (!string.IsNullOrEmpty(message))
                            new DialogWindow(this, "Compile Message", message.Replace("\n", "\n\n")).ShowDialog();
                    }
                    // Add temp file list
                    TempFiles.Add(sourcePath);
                    TempFiles.Add(objectPath);
                    TempFiles.Add(batPath);
                    TempFiles.Add(targetPath);
                    return targetPath;
                }
                catch (Exception) { throw; }
            }
            // Compile CSharp snippets
            string CompileCSharp()
            {
                string temp = GetTempFileName();
                string sourcePath = temp + ".cs";
                string targetPath = temp + ".exe";
                // Dump source
                System.IO.File.WriteAllText(sourcePath, previewCode);
                try
                {
                    // Get the location of .Net framework compiler
                    string frameworkFolder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Microsoft.NET\Framework\");
                    string frameworkVersionFolder = Directory
                            .EnumerateDirectories(frameworkFolder)
                            .OrderByDescending(version => Convert.ToInt32(Regex.Match(System.IO.Path.GetFileName(version), "^v(\\d)\\.").Groups[1].Value))
                            .FirstOrDefault();
                    if (!System.IO.Directory.Exists(frameworkFolder) || frameworkVersionFolder == null)
                        throw new InvalidOperationException($"No .Net Framework compiler folder found on this system at `{frameworkFolder}`.");
                    using (Process myProcess = new Process())
                    {
                        myProcess.StartInfo.UseShellExecute = false;
                        myProcess.StartInfo.FileName = System.IO.Path.Combine(frameworkVersionFolder, "csc.exe");
                        myProcess.StartInfo.CreateNoWindow = true;
                        myProcess.StartInfo.Arguments = $"\"/out:{targetPath}\" \"{sourcePath}\"";
                        myProcess.Start();
                        myProcess.WaitForExit();
                    }
                    // Add temp file list
                    TempFiles.Add(sourcePath);
                    TempFiles.Add(targetPath);
                    return targetPath;
                }
                catch (Exception) { throw; }
            }
            // Run Python snippets
            double RunPython()
            {
                string temp = GetTempFileName();
                string sourcePath = temp + ".python";
                // Dump source
                System.IO.File.WriteAllText(sourcePath, previewCode);
                try
                {
                    // Add temp file list
                    TempFiles.Add(sourcePath);
                    // Run interpreter
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    using (Process myProcess = new Process())
                    {
                        myProcess.StartInfo.UseShellExecute = true;
                        myProcess.StartInfo.FileName = "python";
                        myProcess.StartInfo.CreateNoWindow = false;
                        myProcess.StartInfo.Arguments = $"\"{sourcePath}\"";
                        myProcess.Start();
                        myProcess.WaitForExit();
                    }
                    sw.Stop();
                    return sw.ElapsedMilliseconds / 1000;
                }
                catch (Exception) { throw; }
            }
            double RunCityScript()
            {
                string ExtractCityScriptSource(string markdown)
                {
                    StringBuilder source = new StringBuilder();
                    bool insideCodeSection = false;
                    foreach (var line in SplitToLines(markdown))
                    {
                        if (insideCodeSection)
                        {
                            if (line == "```")
                                insideCodeSection = false;
                            else
                                source.AppendLine(line);
                        }
                        else if (line == "```cityscript")
                            insideCodeSection = true;
                        else
                            continue;
                    }
                    return source.ToString();
                }
                string Preprocess(string source)
                {
                    StringBuilder preprocessed = new StringBuilder();
                    foreach (var line in SplitToLines(source))
                    {
                        // Convert numbers to `place` action
                        if (int.TryParse(line[0].ToString(), out _))
                        {
                            string formatted = line.Replace(" ", ", ");
                            string pattern = ", ";
                            int third = line.IndexOf(pattern, line.IndexOf(pattern, line.IndexOf(pattern) + 1) + 1);
                            string vector = formatted.Substring(0, third);
                            string arguments = string.Join("\", \"", formatted.Substring(third + 1).Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries));
                            preprocessed.AppendLine($"place({vector}, \"{arguments}\")");
                        }
                        else
                            preprocessed.AppendLine(line);
                    }
                    return preprocessed.ToString();
                }
                Stopwatch sw = new Stopwatch();
                sw.Start();
                // Run proprocessing on source code part inside markdown
                string script = Preprocess(ExtractCityScriptSource(previewCode));
                // Run processor
                string path = new CityScript(script).Output(GetTempFileName(), CityScript.OutputType.Default);
                // Preview result
                Process.Start(path);
                sw.Stop();
                return sw.ElapsedMilliseconds / 1000;
            }
            double RenderPbrt()
            {
                string temp = GetTempFileName();
                string sourcePath = temp + ".pbrt";
                string targetPath = temp + ".png";
                // Dump source
                System.IO.File.WriteAllText(sourcePath, previewCode);
                try
                {
                    // Add temp file list
                    TempFiles.Add(sourcePath);
                    TempFiles.Add(targetPath);
                    // Run renderer
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    using (Process myProcess = new Process())
                    {
                        myProcess.StartInfo.UseShellExecute = true;
                        myProcess.StartInfo.FileName = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pbrt/pbrt.exe");
                        myProcess.StartInfo.CreateNoWindow = false;
                        myProcess.StartInfo.Arguments = $"--outfile \"{targetPath}\" \"{sourcePath}\"";
                        myProcess.StartInfo.WorkingDirectory = Commands.HomeDirectory;
                        myProcess.Start();
                        myProcess.WaitForExit();
                    }
                    sw.Stop();
                    Process.Start(targetPath);
                    return sw.ElapsedMilliseconds / 1000;
                }
                catch (Exception) { throw; }
            }
            // Run a program and measure it's performance in seconds
            double RunProgram(string exePath)
            {
                try
                {
                    if (!File.Exists(exePath))
                        throw new ArgumentException("Executable doesn't exist (probably due to compilation error).");
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    using (Process myProcess = new Process())
                    {
                        myProcess.StartInfo.UseShellExecute = false;
                        myProcess.StartInfo.FileName = exePath;
                        myProcess.StartInfo.CreateNoWindow = false;
                        myProcess.Start();
                        myProcess.WaitForExit();
                    }
                    sw.Stop();
                    return sw.ElapsedMilliseconds / 1000;
                }
                catch (Exception){ throw; }
            }

            if (string.IsNullOrEmpty(previewCode))
                return new string[] { "No code available for preview." };
            else
            {
                var type = CheckCodeLanguage(ActiveItem.Name != null ? System.IO.Path.GetExtension(Commands.GetPhysicalPathForFilesThatCanBeInsideFolder(ActiveItem.Name)).ToLower() : null
                    , previewCode);
                switch (type)
                {
                    case LanguageType.CPP:
                        DeleteTemporaryFiles(); // In case we are previewing multiple times on the same item (in which case UpdateItemPreview() is not called), delete preview temp files
                        try { return new string[] { $"C++ program finished in {RunProgram(CompileCPP())} seconds." }; }
                        catch (Exception e) { return new string[] { e.Message }; }
                    case LanguageType.CSharp:
                        DeleteTemporaryFiles(); // In case we are previewing multiple times on the same item (in which case UpdateItemPreview() is not called), delete preview temp files
                        try { return new string[] { $"C# program finished in {RunProgram(CompileCSharp())} seconds." }; }
                        catch (Exception e) { return new string[] { e.Message }; }
                    case LanguageType.Python:
                        DeleteTemporaryFiles(); // In case we are previewing multiple times on the same item (in which case UpdateItemPreview() is not called), delete preview temp files
                        try { return new string[] { $"Python script run finished in {RunPython()} seconds." }; }
                        catch (Exception e) { return new string[] { e.Message }; }
                    case LanguageType.Pbrt:
                        DeleteTemporaryFiles(); // In case we are previewing multiple times on the same item (in which case UpdateItemPreview() is not called), delete preview temp files
                        try { return new string[] { $"Rendering pbrt scene finished in {RenderPbrt()} seconds." }; }
                        catch (Exception e) { return new string[] { e.Message }; }
                    case LanguageType.CityScript:
                        DeleteTemporaryFiles();
                        try { return new string[] { $"Rendering CityScript scene finished in {RunCityScript()} seconds." }; }
                        catch (Exception e) { return new string[] { e.Message }; }
                    case LanguageType.Unidentified:
                    default:
                        return new string[] { "Language type unknown." };
                }
            }
        }
        [Command("Open webpage remote debug dev tool address.")]
        public IEnumerable<string> D(params string[] args)
        {
            if (PreviewBrowser.Visibility == Visibility.Visible)
            {
                string address = $"http://localhost:{CefSettings.RemoteDebuggingPort}";
                Process.Start(address);
                return new string[] { $"Opening address {address}..." };
            }
            else
                return new string[] { "A preview browser must be visible; Use `s web` to open preview browser." };
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
                PreviewAddress = args[0];
                return new string[] { $"Opening address {args[0]}." };
            }
            else
                return new string[] { "A preview browser must be visible; Use `s web` to open preview browser." };
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
        private string CurrentVideoUrl = null;
        [Command("Play a new or continue play/pause a video.")]
        [CommandArgument("url", "an url to play", optional: true)]
        public IEnumerable<string> Play(params string[] args)
        {
            if (VLCControl != null)
            {
                if (VLCControl.IsPlaying)
                {
                    VLCControl.Pause();
                    return new string[] { "Video is paused." };
                }
                else
                {
                    // Play new media
                    if (args.Length == 1)
                    {
                        CurrentVideoUrl = args[0];
                        VLCControl.Play(new Uri(CurrentVideoUrl));
                        // Disable VLC input capture and handle it ourselves
                        VLCControl.Video.IsMouseInputEnabled = false;
                        VLCControl.Video.IsKeyInputEnabled = false;
                        return new string[] { $"Playing {args[0]}." };
                    }
                    // Continue play old media
                    else
                    {
                        VLCControl.Play();
                        return new string[] { "Continue play." };
                    }
                }
            }
            else
                return new string[] { "No preview source available. Open some video first or use `s video` to open video preview." };
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
        [Command("Perform Google search.",
            "Entered arguments are simply joined together with space replace with + sign and " +
            "then appended to `www.google.com/search?q=`.")]
        [CommandArgument("keywords", "keywords to search; (special) no need to have quotes")]
        public IEnumerable<string> Google(params string[] args)
        {
            if (args.Length == 0)
                return new string[] { "Enter keywords to search." };
            else
            {
                string searchPhrase = string.Join(" ", args);
                return SW($"www.google.com/search?q={searchPhrase.Replace(' ', '+')}");
            }
        }
        [Command("Switch to browser and optionally open some website.")]
        [CommandArgument("address", "website to open", optional: true)]
        public IEnumerable<string> SW(params string[] args)
        {
            var r = S("web");
            if (args.Length == 1)
                r = G(args[0]);
            return r;
        }
        [Command("Switch to video and optionally play some video source.")]
        [CommandArgument("path", "video to open", optional: true)]
        public IEnumerable<string> SV(params string[] args)
        {
            var r = S("video");
            if (args.Length == 1)
            {
                CurrentVideoUrl = args[0];
                r = Play(CurrentVideoUrl);
            }
            return r;
        }
        [Command("Switch to a different preview type.")]
        [CommandArgument("type", "type of preview control to switch, must be either `video` or `web`")]
        public IEnumerable<string> S(params string[] args)
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
                return new string[] { $"Available commands: {availableCommands}. You can chain commands together using `|` symbol; Use `current` to get current preview information." };
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
using Csv;
using InteropCommon;
using MoonSharp.Interpreter;
using Newtonsoft.Json;
using SQLiteExtension;
using StringHelper;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Text;
using YamlDotNet.Serialization;

namespace Somewhere
{
    public class Commands : IDisposable
    {
        #region Constructor and Disposing
        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public Commands(string initialWorkingDirectory, bool initializeFSWatcher = false)
        {
            // Initialize home directory
            string fullPath = Path.GetFullPath(initialWorkingDirectory);
            HomeDirectory = fullPath.Last() == Path.DirectorySeparatorChar
                ? fullPath
                : fullPath + Path.DirectorySeparatorChar;
            // Create and register file system watcher
            if (initializeFSWatcher)
                _FSWatcher = CreateWatcher(HomeDirectory);
        }
        private FileSystemWatcher CreateWatcher(string fullFolderPath)
        {
            FileSystemWatcher watcher = new FileSystemWatcher()
            {
                // Set to home dir
                Path = fullFolderPath,
                // Watch for changes in LastAccess and LastWrite times, and
                // the renaming of files or directories.
                NotifyFilter = NotifyFilters.LastAccess
                                 | NotifyFilters.LastWrite
                                 | NotifyFilters.FileName
                                 | NotifyFilters.DirectoryName
                                 | NotifyFilters.Size,
                // Watch everything
                Filter = "" // Empty represents everything
            };

            // Add event handlers
            watcher.Changed += OnFSChanged;
            watcher.Created += OnFSCreated;
            watcher.Deleted += OnFSDeleted;
            watcher.Renamed += OnFSRenamed;

            // Begin watching and return
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        public void Dispose()
        {
            // Dispose DB connection
            _Connection?.Close();
            _Connection?.Dispose();
            // Dispose FS watcher
            _FSWatcher?.Dispose();
        }
        #endregion

        #region Console Behavior Configurations
        /// <summary>
        /// Whether Commands class and its command actions should write to console;
        /// If not, then functions here should not write to console;
        /// This may or may not be respected by all commands that have already been implemented.
        /// </summary>
        /// <remarks>
        /// Used for interoperation between other library clients e.g. desktop application
        /// in which case we may want to disable reading/writing from console
        /// </remarks>
        public bool WriteToConsoleEnabled { get; set; } = true;
        /// <summary>
        /// Whether Commands class and its command actions should read from console;
        /// If not, then functions here should not read from console;
        /// This may or may not be respected by all commands that have already been implemented.
        /// </summary>
        /// <remarks>
        /// Used for interoperation between other library clients e.g. desktop application
        /// in which case we may want to disable reading/writing from console
        /// </remarks>
        public bool ReadFromConsoleEnabled { get; set; } = true;
        #endregion

        #region Public Properties
        /// <summary>
        /// Get current working directory; Expects to be a full path, 
        /// because we are not calling "SetCurrentDirectory()" - to avoid messing up the host env
        /// </summary>
        /// <remarks>
        /// Changing HD is not allowed; 
        /// To swtich a home directory, just create a new Commands instance with new path
        /// </remarks>
        public string HomeDirectory { get; }
        /// <summary>
        /// Returns a list of all Command methods
        /// </summary>
        public Dictionary<MethodInfo, CommandAttribute> CommandAttributes
            => _CommandMethods == null
            ? (_CommandMethods = typeof(Commands).GetMethods(BindingFlags.Instance | BindingFlags.Public).ToDictionary(m => m,
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
        /// Return the list of all tags;
        /// Returns empty array instead of null if there is no tag
        /// </summary>
        public string[] AllTags
            => Connection.ExecuteQuery("select Name from Tag").List<string>()?.ToArray() ?? new string[] { };
        /// <summary>
        /// Check whether current working directory is a "home" folder, i.e. whether Somewhere DB file is present
        /// </summary>
        public bool IsHomePresent
            => FileExistsAtHomeFolder(DBName);
        /// <summary>
        /// Count of managed items
        /// </summary>
        public int ItemCount
            => Connection.ExecuteQuery("select count(*) from File").Single<int>();
        /// <summary>
        /// Count of managed files
        /// </summary>
        public int FileCount
            => Connection.ExecuteQuery(@"select count(*) from File 
                where Name is not null and Content is null
                and Name not like '%/' and Name not like '%\'").Single<int>();
        /// <summary>
        /// Count of managed files
        /// </summary>
        public int FolderCount
            => Connection.ExecuteQuery(@"select count(*) from File 
                where Name like '%/' or Name like '%\' and Content is null").Single<int>();
        /// <summary>
        /// Get count of log entry
        /// </summary>
        public int LogCount
            => Connection.ExecuteQuery("select count(*) from Journal where Type='Log'").Single<int>();
        /// <summary>
        /// Get count of tags
        /// </summary>
        public int TagCount
            => Connection.ExecuteQuery("select count(*) from Tag").Single<int>();
        /// <summary>
        /// Get count of virtual notes
        /// </summary>
        public int NoteCount
            => Connection.ExecuteQuery("select count(*) from File where Content is not null").Single<int>();
        /// <summary>
        /// Get count of Knowledge (virtual notes with title null)
        /// </summary>
        public int KnowledgeCount
            => Connection.ExecuteQuery("select count(*) from File where Name is null").Single<int>();
        /// <summary>
        /// Check whether a file exists and it exists at home directory
        /// </summary>
        /// <param name="filename">Actual (Physical) filename</param>
        public bool FileExistsAtHomeFolder(string filename)
            => // Check absolute path
            (filename.Contains(HomeDirectory) && File.Exists(filename))
            // Check relative path
            || (!Path.IsPathRooted(filename) && File.Exists(Path.Combine(HomeDirectory, filename)));
        /// <summary>
        /// Check whether a directory exists and it exists at home directory
        /// </summary>
        public bool DirectoryExistsAtHomeFolder(string directoryName)
            => // Check absolute path
            (directoryName.Contains(HomeDirectory) && Directory.Exists(directoryName))
            // Check relative path
            || (!Path.IsPathRooted(directoryName) && Directory.Exists(Path.Combine(HomeDirectory, directoryName)));
        public string GetPathInHomeHolder(string homeRelativePath)
            => Path.Combine(HomeDirectory, homeRelativePath);
        /// <param name="filename">Actual (Physical) filename</param>
        public void DeleteFileFromHomeFolder(string filename)
            => File.Delete(Path.Combine(HomeDirectory, filename));
        /// <summary>
        /// Renames a file, assuming input paths are physical and valid;
        /// Automatically creates folders for target location if not already exist
        /// </summary>
        /// <param name="itemname">Actual (Physical) filename</param>
        public void MoveFileInHomeFolder(string itemname, string newItemname)
        {
            void CreateDirectoriesForFileIfNotAlreadyExist(string targetFolder)
                => Directory.CreateDirectory(targetFolder); // Yes, CreateDirectory() handles it properly
            if (itemname == newItemname)
                return;
            CreateDirectoriesForFileIfNotAlreadyExist(Path.Combine(HomeDirectory, newItemname).GetRealDirectoryPath());
            File.Move(Path.Combine(HomeDirectory, itemname),
                Path.Combine(HomeDirectory, newItemname));
        }
        /// <summary>
        /// Get physical name with additional information for further processsing
        /// </summary>
        public string GetPhysicalName(string itemName, out string escapedNameWithoutExtension, out string extension, out int availableNameLength, int pathLengthLimit = 256)
        {
            // Get properly formatted file name first
            string characterEscapedName = itemName.EscapeFilename();
            // Shorten file name
            escapedNameWithoutExtension = Path.GetFileNameWithoutExtension(characterEscapedName);
            extension = Path.GetExtension(characterEscapedName);
            availableNameLength = 260 /* Windows MAX PATH */
                - HomeDirectory.Length /* Folder name/path */
                - 1 /* Trailing folder slash */
                - 1 /* Reserved for End-of-Line character*/
                - extension.Length /* Reserved for extension */;
            if (availableNameLength <= 0)
                throw new InvalidOperationException("Not enough available length for file name. Please move your Home folder to a shorter path.");
            string shortenedName = escapedNameWithoutExtension.Limit(availableNameLength, $"..", extension /* Notic extension already contains a dot*/);  // Shorten and preserve extension
            // Return expected shortened physical name
            return shortenedName;
        }
        /// <summary>
        /// Get physical name for the file, escaping all windows and linux special path characters, clamping to allowed system max filepath length
        /// preserve extension, allow unicode characters, automatically (transparently) handle file name collisions (as long as item name is unique,
        /// as is required).
        /// Note due to those effects if home directory is renamed, file is renamed, or home directory is moved to a different location,
        /// existing filename can become invalid when callling this function again; Caller should handle this gracefully.
        /// </summary>
        /// <remarks>
        /// Notice item names can never have collision, even between folders and files - once they are managed by Somewhere they are treated the same;
        /// The purpose of this function is to mostly deal with FS file path length and naming convention limit;
        /// Invalid characters are systematically replaced with '_', and during name shortening if name collisions occur then
        /// filenames are appended with their database item ID in the format "Shortened name#DDDDDDDD.ext", thus during a (shortened) name collision, it will require 
        /// at most an additional 9 characters (in addition to shortened text and file extention) for file name;
        /// If this caused the file's path exceed system length limit, an exception will be thrown.
        /// </remarks>
        public string GetPhysicalName(string itemName)
            => GetPhysicalName(itemName, out _, out _, out _);
        /// <summary>
        /// Get physical name for a path that potentially exist in a folder
        /// </summary>
        public string GetPhysicalNameForFilesThatCanBeInsideFolder(string itemPath)
            => Path.Combine(itemPath.GetRealDirectoryPath(), GetPhysicalName(itemPath.GetRealFilename(), out _, out _, out _));
        /// <summary>
        /// Get new physical name for a path, rather than just an itemname, this is a more robust version of GetPhysicalName()
        /// </summary>
        private string GetNewPhysicalNameForFilesThatCanBeInsideFolder(string itempath, int id, string oldPhysicalName)
        {
            string realPath = itempath.GetRealDirectoryPath();
            return Path.Combine(realPath, GetNewPhysicalName(itempath.GetRealFilename(), id, oldPhysicalName, realPath));
        }
        /// <summary>
        /// Get physical name for a new file
        /// </summary>
        public string GetNewPhysicalName(string newItemname, int itemID, string oldPhysicalName, string parentFolder = null, int pathLengthLimit = 256)
        {
            string physicalName = GetPhysicalName(newItemname, out string escapedNameWithoutExtension, out string extension, out int availableNameLength, pathLengthLimit);
            // Get file ID string
            string idString = $"#{itemID}";
            // Validate existence
            if (physicalName != oldPhysicalName && (parentFolder != null 
                ? File.Exists(Path.Combine(parentFolder.IsPathRooted() ? parentFolder : Path.Combine(HomeDirectory, parentFolder), physicalName))
                : FileExistsAtHomeFolder(physicalName)))
            {
                // Get a supposedly unique name
                string uniqueName = escapedNameWithoutExtension.Limit(availableNameLength, "..", $"{idString}{extension}" /* Notic extension already contains a dot*/);
                // Check name existence for collision and provide name correction
                return uniqueName;  // Notice we didn't handle the case when unique name already exist
            }
            return physicalName;
        }
        /// <summary>
        /// Get physical path for the file, escaping all windows and linux special path characters, clamping to system allowed file path length,
        /// preserve extension, allow unicode characters, automatically (transparently) handle file name collisions (as long as item name is unique,
        /// as is required).
        /// </summary>
        public string GetPhysicalPath(string itemname)
            => Path.Combine(HomeDirectory, GetPhysicalName(itemname));
        /// <summary>
        /// Get physical path for a name/path that potentially exist in a folder
        /// </summary>
        public string GetPhysicalPathForFilesThatCanBeInsideFolder(string relativePath)
            => Path.Combine(HomeDirectory, GetPhysicalNameForFilesThatCanBeInsideFolder(relativePath));
        /// <summary>
        /// Get physical path for a new file
        /// </summary>
        public string GetNewPhysicalPath(string itemname, int itemID, string oldPhysicalName)
            => Path.Combine(HomeDirectory, GetNewPhysicalName(itemname, itemID, oldPhysicalName));
        #endregion

        #region Configuration Shortcuts
        public bool ShouldRecordCommits
            => GetConfiguration<bool>("RegisterCommits");
        #endregion

        #region Constants
        public const string ReleaseVersion = "V0.1.0";
        public const string DBName = "Home.somewhere";
        #endregion

        #region Commands (Public Interface as Library)
        [Command("Add an item to home.",
            "Notice for folders inside Home this command will add the folder itself - to import and flatten the folder, use `im` command.", 
            category: "File")]
        [CommandArgument("itemname", "name of item; use * to add all items in current directory (will not add subdirectories or items in subdirectory); " +
            "if given path is outside Home directory - for files they will be copied, for folders they will be cut and paste inside Home")]
        [CommandArgument("tags", "tags for the item", optional: true)]
        public IEnumerable<string> Add(params string[] args)
        {
            ValidateArgs(args, true);
            // Add single item
            if (args[0] != "*")
            {
                string itemname = null;
                // Handle foreign (absolute) path (i.e. outside home)
                string path = args[0];
                if (Path.IsPathRooted(path) && !path.Contains(HomeDirectory))
                {
                    // Path existence check
                    bool existAsFile = File.Exists(path), existAsDirectory = Directory.Exists(path);
                    if (!existAsFile && !existAsDirectory)
                        throw new ArgumentException($"Specified item `{path}` doesn't exist on disk.");
                    // For foreign file, make a copy
                    else if (existAsFile)
                    {
                        string name = Path.GetFileName(path);
                        if (File.Exists(GetPathInHomeHolder(name)))
                            throw new ArgumentException($"Specified path `{path}` contains itemname `{name}` that already exist in home directory.");
                        else
                        {
                            File.Copy(path, GetPathInHomeHolder(name));
                            itemname = name;
                        }
                    }
                    // For foreign directories, cut it
                    else
                    {
                        string name = Path.GetFileName(path);   // This function actually returns folder name because Path.GetDirectoryName() returns parent folder
                        Directory.Move(path, GetPathInHomeHolder(name));
                        itemname = name + Path.DirectorySeparatorChar;
                    }
                }
                // Handle absoluate and relative path
                else
                {
                    itemname = GetRelative(args[0]);    // Convert absoluate path to relative
                    // Handle non-existing path
                    bool existAsFile = FileExistsAtHomeFolder(GetPhysicalNameForFilesThatCanBeInsideFolder(itemname)), 
                        existAsFolder = DirectoryExistsAtHomeFolder(itemname);
                    if (!existAsFile && !existAsFolder)
                        throw new ArgumentException($"Specified item `{path}` doesn't exist in Home folder ({HomeDirectory}); Notice current working directory is `{Directory.GetCurrentDirectory()}`. " +
                            $"Use an absolute path instead to avoid potential confusion.");
                    else if (existAsFolder)
                        itemname += Path.DirectorySeparatorChar;
                }
                // Handle actually adding file and tags
                if (IsFileInDatabase(itemname))
                    return new string[] { $"Item `{itemname}` already added in database." };
                else
                {
                    AddFile(itemname);
                    TryRecordCommit(JournalEvent.CommitOperation.AddFile, itemname, null);
                    if (args.Length == 2)
                    {
                        var tags = AddTagsToFile(itemname, args[1].SplitTags());
                        TryRecordCommit(JournalEvent.CommitOperation.ChangeItemTags, itemname, tags.JoinTags());
                    }
                    return new string[] { $"Item `{itemname}` added to database with a total of {ItemCount} {(ItemCount > 1 ? "items" : "item")}." };
                }
            }
            // Add all files (don't add directories and skip subdirectories by default)
            else
            {
                string[] newFiles = Directory.EnumerateFiles(HomeDirectory)
                    .Select(filepath => Path.GetFileName(filepath)) // Returned strings contain complete folder path, strip it out
                    .Where(filename => !IsFileInDatabase(filename) && filename != DBName)
                    .ToArray();    // Exclude DB file itself
                string[] tags = null;
                if (args.Length == 2)
                    tags = args[1].SplitTags().ToArray();
                List<string> result = new List<string>();
                result.Add($"Add {newFiles.Length} files");
                result.Add("------------");
                foreach (var filename in newFiles)
                {
                    AddFile(filename);
                    TryRecordCommit(JournalEvent.CommitOperation.AddFile, filename, null);
                    if (args.Length == 2)
                    {
                        var resultTags = AddTagsToFile(filename, tags);
                        TryRecordCommit(JournalEvent.CommitOperation.ChangeItemTags, filename, resultTags.JoinTags());
                    }
                    result.Add($"[Added] `{filename}`");
                }
                result.Add($"Total: {FileCount} {(FileCount > 1 ? "items" : "item")} in database.");
                return result;
            }
        }
        [Command("Get or set configurations.", category: "Misc.")]
        [CommandArgument("key", "name of the configuration; if not given then return all keys currently exist", optional: true)]
        [CommandArgument("value", "value of the configuration; if given then update key; if not given then return the value for the specified key", optional: true)]
        public IEnumerable<string> Cf(params string[] args)
        {
            ValidateArgs(args);
            // Return all configuration keys
            List<string> rows = new List<string>();
            if (args.Length == 0)
            {
                rows.Add("Configurations");
                rows.Add(new string('-', rows[0].Length));
                var configurations = GetAllConfigurations();
                foreach (var item in configurations)
                    rows.Add($"{item.Key} ({item.Type}){(string.IsNullOrEmpty(item.Comment) ? string.Empty : $" - {item.Comment}")}");
                rows.Add($"Total: {configurations.Count} {(configurations.Count > 1 ? "configurations" : "configuration")}.");
            }
            // Return value for one configuration
            else if (args.Length == 1)
            {
                string key = args[0];
                rows.Add($"{key}:");
                rows.Add(GetConfiguration(key));
            }
            // Set value for configuration
            else if (args.Length == 2)
            {
                string key = args[0];
                string value = args[1];
                SetConfiguration(key, value);
                rows.Add($"{key} is set to `{value}`.");
            }
            return rows;
        }
        [Command("Create a virtual file (virtual text note).",
            "Virtual text notes may or may not have a name. " +
            "If it doesn't have a name (i.e. empty), it's also called a \"knowledge\" item, as is used by Somewhere Knowledge subsystem.",
            category: "File")]
        [CommandArgument("notename", "name for the virtual file, must be unique among all managed files")]
        [CommandArgument("content", "initial content for the virtual file")]
        [CommandArgument("tags", "comma delimited list of tags in double quotes; any character except commas and double quotes are allowed; tags are required", optional: false)]
        public IEnumerable<string> Create(params string[] args)
        {
            ValidateArgs(args);
            string name = args[0];
            if (string.IsNullOrEmpty(name))
                name = null;    // Nullify empty name
            if (name != null && FileExistsAtHomeFolder(name))
                throw new ArgumentException($"Specified notename `{name}` already exist (as a physical file) on disk.");
            if (name != null && IsFileInDatabase(name))
                throw new InvalidOperationException($"Specified notename `{name}` is already used by another virtual file in database.");
            // Get content and save the file
            string content = args[1];
            int id = AddFile(name, content);
            TryRecordCommit(JournalEvent.CommitOperation.CreateNote, name, null);
            TryRecordCommit(JournalEvent.CommitOperation.ChangeItemContent, name, content);
            // Get tags
            string[] tags = args[2].SplitTags().ToArray();   // Save as lower case
            string[] allTags = AddTagsToFile(id, tags);
            TryRecordCommit(JournalEvent.CommitOperation.ChangeItemTags, name, allTags.JoinTags());
            return new string[] { $"{(name == null ? $"Knowledge #{id}" : $"Note `{name}`")} has been created with {allTags.Length} {(allTags.Length > 1 ? "tags" : "tag")}: `{allTags.JoinTags()}`." };
        }
        [Command("Dump historical versions of repository.", category: "Mgmt.")]
        [CommandArgument("outputPath", "path of output; contains Format of output, available extensions: .csv (lists with content), " +
            ".log (commit journal), .html (report), .sqlite (database)")]
        [CommandArgument("targetItemname", "name of an item to track history of changes; supported by `csv` format", optional: true)]
        public IEnumerable<string> Dump(params string[] args)
        {
            ValidateArgs(args);
            string outputPath = args[0];
            if (!Path.IsPathRooted(outputPath)) outputPath = GetPathInHomeHolder(outputPath);
            string format = Path.GetExtension(outputPath).TrimStart('.');
            string targetName = args.Length == 2 ? args[1] : null;
            format = format.ToUpper();
            // Get history and pass through it
            var repoState = new VirtualRepository();
            if(targetName != null)
                repoState.PassThrough(GetAllCommits(), targetName);
            else
                repoState.PassThrough(GetAllCommits());
            try
            {
                VirtualRepository.DumpFormat dumpFormat = (VirtualRepository.DumpFormat)Enum.Parse(typeof(VirtualRepository.DumpFormat), format);
                try
                {
                    repoState.Dump(dumpFormat, outputPath);
                    return new string[] { $"Repository state is dumped into {outputPath}" };
                }
                catch (Exception e) { return new string[] { $"[Error] Failed to dump: {e.Message}" }; }
            }
            catch (Exception) { return new string[] { $"Invalid output format `{format}`." }; }
        }
        [Command("Evaluate a Lua expression.",
            "Like `run` command, but automatically prepends \"return \" and thus cannot be used to run script files.", category: "Advanced")]
        [CommandArgument("expression", "a simple lua expression")]
        public IEnumerable<string> Eval(params string[] args)
            => Run($"return {args[0]}");
        [Command("Export files, folders, notes and knowledge. Placeholder, not implemented yet, coming soon.", category: "Mgmt.")]
        public IEnumerable<string> Export(params string[] args)
        {
            throw new NotImplementedException();
        }
        /// <remarks>Due to it's particular function of pagination, this command function behaves slightly different from usual ones;
        /// Instead of returning lines of output for the caller to output, it manages output and keyboard input itself</remarks>
        [Command("Show a list of all files.",
            "Use command line arguments for more advanced display setup.", category: "Display")]
        [CommandArgument("pageitemcount", "number of items to show each time", true)]
        [CommandArgument("datefilter", "a formatted string filtering items with a given entry date; valid formats: specific date string, recent (10 days)", true)]
        public IEnumerable<string> Files(params string[] args)
        {
            ValidateArgs(args);
            int pageItemCount = args.Length >= 1 ? Convert.ToInt32(args[1]) : 20;
            string dateFilter = args.Length >= 2 ? args[2] : null;  // TODO: This feature is not implemented yet
            // Get tags along with file count
            List<QueryRows.FileDetail> fileDetails = GetFileDetails();
            return InteractiveFileRows(fileDetails, pageItemCount);
        }
        [Command("Find with (or without) action.",
            "Find with filename, tags and extra information, and optionally perform an action with find results.",
            category: "Display")]
        [CommandArgument("searchtype (either `name` or `tag`)", "indicates search type; more will be added")]
        [CommandArgument("searchstring", "for `name`, use part of file name to search; for `tag`, use comma delimited list of tags to search")]
        [CommandArgument("action (either `show` or `open`)", "optional action to perform on search results; default `show`; more will be added", optional: true)]
        public IEnumerable<string> Find(params string[] args)
        {
            ValidateArgs(args);
            List<QueryRows.FileDetail> fileRows = null;
            // Switch search type
            switch (args[0].ToLower())
            {
                case "name":
                    fileRows = FilterByNameDetailed(args[1]);
                    break;
                case "tag":
                    fileRows = FilterByTagsDetailed(args[1].SplitTags());
                    break;
                default:
                    throw new ArgumentException($"Unrecognized search type: `{args[0].ToLower()}`");
            }
            // Perform actions
            string action = "show";
            if (args.Length == 3)
                action = args[2].ToLower();
            switch (action)
            {
                case "show":
                    // Interactive display
                    return InteractiveFileRows(fileRows);
                default:
                    throw new ArgumentException($"Unrecognized action: `{action}`");
            }
        }
        [Command("Generate documentation of Somewhere program.", logged: false, category: "Misc.")]
        [CommandArgument("path", "path for the generated file; relative to home folder")]
        public IEnumerable<string> Doc(params string[] args)
        {
            string documentation = "SomewhereDoc.txt";
            if (args.Length != 0)
                documentation = args[0];
            using (FileStream file = new FileStream(GetPathInHomeHolder(documentation), FileMode.Create))
            using (StreamWriter writer = new StreamWriter(file))
            {
                foreach (string line in Help())
                    writer.WriteLine(line);
                foreach (string commandName in CommandNames.Keys.OrderBy(k => k))
                {
                    writer.WriteLine(); // Add empty line
                    foreach (string line in Help(new string[] { commandName }))
                        writer.WriteLine(line);
                }
            }
            return new string[] { $"Document generated at {GetPathInHomeHolder(documentation)}" };
        }
        [Command("Show available commands and general usage help. Use `help commandname` to see more.", logged: false, category: "Misc.")]
        [CommandArgument("commandname", "name of command", optional: true)]
        public IEnumerable<string> Help(params string[] args)
        {
            // Show list of commands
            if (args.Length == 0)
            {
                var list = CommandAttributes
                .OrderBy(cm => cm.Key.Name) // Sort alphabetically
                .GroupBy(cm => cm.Value.Category)   // Group by category
                // .OrderBy(cmg => cmg.Key)    // Don't sort groups alphabetically so overall commands list follow some alphabetical order
                .SelectMany(cmg => cmg.Select((cm, index) => 
                    index == 0 
                    // Add group label to first item
                    ? $"{cmg.Key, -10}{cm.Key.Name.ToLower()} - {(cm.Value as CommandAttribute).Description}"
                    : $"{string.Empty, -10}{cm.Key.Name.ToLower()} - {(cm.Value as CommandAttribute).Description}"))
                .ToList();
                list.Insert(0, "Available Commands: ");
                return list;
            }
            // Show help of specific command
            else
            {
                string command = args[0];
                return GetCommandHelp(command);
            }
        }
        [Command("Import items, files, folders and notes.",
            "Formats: 1) CSV: title,text,created,tags, where tags is space seperated and for items containing spaces using [[]] to enclose them; " +
            "2) Json: An array of {title,text,created,tags}, rules for tags same for csv.",
            category: "Mgmt.")]
        [CommandArgument("sourcepath", "path for the import source; " +
            "either a file or a folder; suffix indicates source, common ones include: folder, csv, txt, tw (TiddlyWiki Json) and md; " +
            "can be internal or external, can be already managed if it's internal; " +
            "can be * to mean anything (that is both managed and not managed) inside current Home")]
        [CommandArgument("howparameter", "specifies how to import; this is format specific; " +
            "for text sources (csv, txt and md), this can be `line` or `header`", optional: true)]
        [CommandArgument("optionparameter", "defines options for import; source format specific; " +
            "empty for the most source formats; for `md` sources, this can be `header` or `title`", optional: true)]
        [CommandArgument("target", "target for the import; can be either `file`, `archive`, `note`, or `knowledge`; " +
            "must be supported by the format", optional: true)]
        public IEnumerable<string> Im(params string[] args)
        {
            string GetAbsPath(string path)
            {
                // Convert relative path to absolute path
                // Notice for a relative path we can't just assume it's relative to "Current Working Directory"
                if (!path.IsPathRooted())
                {
                    if (DirectoryExistsAtHomeFolder(path))
                        return GetPathInHomeHolder(path);
                    return Path.Combine(Directory.GetCurrentDirectory(), path);
                }
                else return path;
            }

            ValidateArgs(args);
            string sourcePath = GetAbsPath(args[0]);
            // Import Tiddly Wiki format csv as notes
            List<string> result = new List<string>();
            string extension = Path.GetExtension(sourcePath).ToLower();
            // Import Tiddly Wiki format csv and json
            if (extension == ".csv" || extension == ".json")
            {
                IEnumerable<Tiddler> tiddlers = null;
                // Collect tiddler definitions
                if (extension == ".csv")
                {
                    // Disable CSV importing fow now to avoid data corruption
                    throw new ArgumentException("Csv importing is disabled at this moment.");
                    string content = File.ReadAllText(sourcePath);
                    var list = new List<Tiddler>();
                    // TODO: Currently "really simple csv" library is not reading correctly when last attribute is empty like this "",
                    // which will cause it to read wrong attribute column counts
                    foreach (var line in CsvReader.ReadFromText(content, new CsvOptions() { AllowNewLineInEnclosedFieldValues = true }))
                    {
                        list.Add(new Tiddler()
                        {
                            title = line["title"],
                            text = line["text"],
                            tags = line["tags"],
                            created = line["created"]
                        });
                    }
                    tiddlers = list;
                }
                else
                {
                    string content = File.ReadAllText(sourcePath);
                    tiddlers = JsonConvert.DeserializeObject<Tiddler[]>(content);
                }
                // Perform actual add action
                foreach (var tiddler in tiddlers)
                {
                    // Handling file with same name already exist, or already imported before
                    if(IsFileInDatabase(tiddler.title))
                        result.Add($"[Warning] `{tiddler.title}` already exist in database.");
                    else
                    {
                        try
                        {
                            // Add file to DB
                            int id = AddFile(tiddler.title, tiddler.text);
                            // Add tags
                            var tags = tiddler.Tags;
                            AddTagsToFile(id, tags);
                            // Modify entry date
                            ChangeEntryDate(id, tiddler.CreatedDate);
                            // Return result
                            result.Add($"`{tiddler.title}` added with {(tags.Length > 1 ? "tags" : "tag")}: {tags.JoinTags()}");
                        }
                        // Additional exception handling
                        catch (Exception e)
                        {
                            result.Add($"{e.Message.Replace('\r', ' ').Replace('\n', ' ')} - Error when importing `{tiddler.title}`");
                        }
                    }
                }
            }
            // Import a folder
            else if (Directory.Exists(sourcePath))
            {
                // For internal folders, flatten it by cutting; Don't delete folder afterwards - some files might have name collision and thus not moved
                // Notice for files that are already managed they are moved properly instead
                if (DirectoryExistsAtHomeFolder(sourcePath))
                    EnumerateAllFilesInDirectory(sourcePath, file =>
                    {
                        // Move Old
                        if (IsFileInDatabase(GetRelative(file)))
                            MV(GetRelative(file), Path.GetFileName(file));
                        // Add New
                        else
                        {
                            string cutFile = Path.Combine(HomeDirectory, Path.GetFileName(file));
                            // Cut
                            File.Move(file, cutFile);
                            // Add
                            try
                            {
                                result.AddRange(Add(cutFile, file.SplitDirectoryAsTags().JoinTags()));
                            }
                            catch (Exception e){ result.Add(e.Message); }
                            
                        }
                    });
                // For external folders, import each file individually by copying and store at home in a flattened fasshion with tags as directory names
                else
                    EnumerateAllFilesInDirectory(sourcePath, file => 
                    {
                        try
                        {
                            result.AddRange(Add(file, file.SplitDirectoryAsTags().JoinTags()));
                        }
                        catch (Exception e) { result.Add(e.Message); }
                    });
            }
            else
                result.Add($"Format for `{sourcePath}` is not supported yet or it doesn't exist.");
            // Return result outputs
            return result;
        }
        [Command("Rename file.",
            "If the file doesn't exist on disk or in database then will issue a warning instead of doing anything.", 
            category: "File")]
        [CommandArgument("filename", "name of file")]
        [CommandArgument("newfilename", "new name of file")]
        public IEnumerable<string> MV(params string[] args)
        {
            ValidateArgs(args);
            string itemname = args[0];
            string newFilename = args[1];
            if (itemname == newFilename)
                return new string[] { $"Filename `{itemname}` is the same as new filename: `{newFilename}`." };
            string oldPhysicalName = GetPhysicalNameForFilesThatCanBeInsideFolder(itemname);
            int? id = GetFileID(itemname);
            if (id == null)
                throw new InvalidOperationException($"Specified item `{itemname}` is not managed in database.");
            if (FileExistsAtHomeFolder(GetNewPhysicalNameForFilesThatCanBeInsideFolder(newFilename, id.Value, oldPhysicalName)) 
                || IsFileInDatabase(newFilename))
                throw new ArgumentException($"Itemname `{newFilename}` is already used.");
            // Move in filesystem (if it's a physical file rather than a virtual file)
            string[] result = null;
            if (FileExistsAtHomeFolder(GetPhysicalNameForFilesThatCanBeInsideFolder(itemname)))
            {
                MoveFileInHomeFolder(oldPhysicalName, GetNewPhysicalNameForFilesThatCanBeInsideFolder(newFilename, id.Value, oldPhysicalName));
                result = new string[] { $"File (Physical) `{itemname}` has been renamed to `{newFilename}`." };
            }
            else
                result = new string[] { $"Virtual file `{itemname}` has been renamed to `{newFilename}`." };
            // Update in DB
            RenameFile(itemname, newFilename);
            TryRecordCommit(JournalEvent.CommitOperation.ChangeItemName, itemname, newFilename);
            return result;
        }
        [Command("Read or set meta attribtues.", category: "Advanced")]
        [CommandArgument("itemname", "name of the item to read or set meta attribute")]
        [CommandArgument("metaname", "name of the meta parameter; " +
            "if not given then return all meta currently exist", optional: true)]
        [CommandArgument("value", "value of the meta parameter; " +
            "if given then update meta; " +
            "if not given then return the value for the specified meta attribute", optional: true)]
        public IEnumerable<string> Mt(params string[] args)
        {
            ValidateArgs(args);
            List<string> rows = new List<string>();
            // Validate item existence
            string itemname = args[0];
            int? id = GetFileID(itemname);
            if (id == null)
            {
                if (FileExistsAtHomeFolder(GetPhysicalNameForFilesThatCanBeInsideFolder(itemname)))
                    throw new ArgumentException($"Specified item `{itemname}` does not exist, i.e. it is not managed.");
                else
                    throw new ArgumentException($"Specified item `{itemname}` is not managed and it doesn't exist in Home folder.");
            }
            // Show all meta
            if (args.Length == 1)
            {
                rows.Add(itemname);
                rows.Add(new string('-', Math.Min(itemname.Length, Console.WindowWidth)));
                var metas = GetMetas(id.Value);
                foreach (KeyValuePair<string, string> item in metas)
                    rows.Add($"{item.Key.Limit(20),-20}: {item.Value.Limit(60),-60}");
                rows.Add($"Total: {metas.Count}");
            }
            // Show specific meta value
            else if (args.Length == 2)
            {
                string meta = args[1];
                string value = GetMeta(id.Value, meta);
                if (meta == null)
                    throw new ArgumentException($"Specified meta `{meta}` doesn't exist on item `{itemname}`.");
                rows.Add($"{meta}: ");
                rows.Add(value);
            }
            // Set meta value
            else if (args.Length == 3)
            {
                string meta = args[1];
                string value = args[2];
                SetMeta(id.Value, meta, value);
                rows.Add($"Meta attribute `{meta}` for item `{itemname}` is set to `{value}`.");
            }
            // Return results
            return rows;
        }
        [Command("Move Tags, renames specified tag.",
            "If source tag doesn't exist in database then will issue a warning instead of doing anything. " +
            "If the target tag name already exist, then this action will merge the two tags. " +
            "If more than one replacement tags are provided, the source tag will be split into multiple new ones, this operation is also called \"explosion\".",
            category: "Tagging")]
        [CommandArgument("sourcetag", "old name for the tag")]
        [CommandArgument("targettags", "new name(s) for the tag; if more than one is specified, the tag will be replaced with all of them")]
        public IEnumerable<string> MVT(params string[] args)
        {
            ValidateArgs(args);
            string sourceTag = args[0];
            string[] targetTags = args[1].SplitTags().ToArray();
            List<string> results = new List<string>();
            if (!IsTagInDatabase(sourceTag))
                throw new InvalidOperationException($"Specified tag `{sourceTag}` does not exist in database.");
            // Commit (already in a transaction)
            TryRecordCommit(JournalEvent.CommitOperation.RenameTag, sourceTag, args[1]);
            // Perform actions in a transaction for efficiency
            SQLiteTransaction transaction = Connection.BeginTransaction();  // Create and dispose manually to avoid changing too many lines in git
            // Get files with old tag
            List<int> sourceFileIDs = FilterByTags(new string[] { sourceTag }, transaction).Select(f => f.ID).ToList();
            List<TagRow> tagIDs = GetTagRows(targetTags.Union(new string[] { sourceTag })); // Including all mentioned tags that exist
            int sourceTagID = tagIDs.Find(t => t.Name == sourceTag).ID;
            // Routine functions
            void AppendExistingTag(string targetTag)
            {
                // Assume exists
                int targetTagID = tagIDs.Find(t => t.Name == targetTag).ID;
                // Add reference to new tag
                AddFileTags(sourceFileIDs, targetTagID, transaction);
            }
            void MergeIntoTag(string targetTag)
            {
                // Delete reference to old tag
                DeleteFileTags(sourceFileIDs, sourceTagID, transaction);
                // Delete source tag
                DeleteTag(sourceTagID, transaction);
                // Add new tag
                AppendExistingTag(targetTag);
            }
            void AppendNewTag(string targetTag)
            {
                // Assume exists
                int targetTagID = AddTag(targetTag, transaction);
                // Add reference to new tag
                AddFileTags(sourceFileIDs, targetTagID, transaction);
            }
            // Rename source tag and add new multiple new tags
            for (int i = 0; i < targetTags.Length; i++)
            {
                var targetTag = targetTags[i];
                // Handle same
                if(sourceTag == targetTag)
                {
                    results.Add($"Tag `{targetTag}` is the same as source tag, skipped.");
                    continue;
                }
                // For first new tag, old one still exists, so just rename or merge it
                if (i == 0)
                {
                    if (!IsTagInDatabase(targetTag, transaction)) // If target doesn't exist yet just rename source
                    {
                        RenameTag(sourceTag, targetTag, transaction);
                        results.Add($"Tag `{sourceTag}` is renamed to `{targetTag}`.");
                    }
                    // Merge tags
                    else
                    {
                        MergeIntoTag(targetTag);
                        results.Add($"Tag `{sourceTag}` is merged into `{targetTag}`");
                    }
                }
                // For other new tags, old one no longer exist
                else
                {
                    if (!IsTagInDatabase(targetTag, transaction)) // If target doesn't exist yet we need to create it
                    {
                        AppendNewTag(targetTag);
                        results.Add($"New tag `{targetTag}` is added.");
                    }
                    // Merge tags by add directly
                    else
                    {
                        AppendExistingTag(targetTag);
                        results.Add($"Tag `{targetTag}` is appended.");
                    }
                }
            }
            transaction.Commit();
            transaction.Dispose();
            
            return results;
        }
        [Command("Create a new Somewhere home at current home directory.")]
        public IEnumerable<string> New(params string[] args)
        {
            try
            {
                // Check not existing
                if (IsHomePresent)
                    throw new InvalidOperationException($"A Somewhere database already exist in {HomeDirectory} directory");
                string path = GenerateDBFileAt(HomeDirectory);
                return new string[] { $"Database generated at ${path}" };
            }
            catch (InvalidOperationException) { throw; }
        }
        [Command("Permanantly delete all the files that are marked as \"_deleted\"", category: "Mgmt.")]
        [CommandArgument("-f", "force purging and purge without warning", optional: true)]
        public IEnumerable<string> Purge(params string[] args)
        {
            // A routine to search in a given folder and its subfolders for deleted files
            void SearchDirectoryForDeleted(string dir, List<string> resultList)
            {
                EnumerateAllFilesInDirectory(dir, file =>
                {
                    if (Path.GetFileName(file).EndsWith("_deleted"))
                        resultList.Add(file);
                });
            }

            ValidateArgs(args);
            // Get all files in all directories that are marked as deleted
            List<string> deletedFiles = new List<string>();
            try { SearchDirectoryForDeleted(HomeDirectory, deletedFiles); }
            catch (Exception e) { return new string[] { e.Message }; }
            // Exit if nothing to purge
            if (deletedFiles.Count == 0)
                return new string[] { "Nothing to purge!" };

            List<string> result = new List<string>();
            // Show warning
            if ( // If no argument is given
                args.Length == 0 
                // If argument given is invalid
                || args[0] != "-f")
            {
                // Warn about invalid argument
                if (args.Length == 1 && args[0] != "-f")
                    Console.WriteLine($"Argument {args[0]} is invalid; To force deleting files without warning, use `-f`.");
                // Print files interactively and ask for confirmation
                Console.WriteLine($"Following {(deletedFiles.Count > 1 ? $"files (Count: {deletedFiles.Count})" : "file")} will be deleted permanantly: ");
                deletedFiles.ForEach(f => Console.WriteLine($"\t{f}"));
                Console.Write($"Are you very very sure? (Y/N) - answer is case sensitive: ");
                if (Console.ReadLine() != "Y")
                    return new string[] { "Operation is cancelled." };
            }
            // Log the operation but don't ask for warning
            else
            {
                result.Add($"Following {(deletedFiles.Count > 1 ? "files" : "file")} will be deleted permanantly: ");
                deletedFiles.ForEach(f => result.Add($"\t{f}"));
            }
            // Delete files completely
            deletedFiles.ForEach(f => File.Delete(f));
            // Generate reports
            result.Add($"{deletedFiles.Count} {(deletedFiles.Count > 1 ? "files are" : "file is")} permanantly deleted.");
            return result;
        }
        [Command("Read content of an item.", category: "Display")]
        [CommandArgument("itemname", "name of item; can be either managed or not managed")]
        [CommandArgument("linecount", "the number of lines to display; if not specified, read all lines", optional: true)]
        public IEnumerable<string> Read(params string[] args)
        {
            ValidateArgs(args);
            string itemname = args[0];
            int lineCount = args.Length == 2 ? Convert.ToInt32(args[1]) : 0;

            IEnumerable<string> ReadPhysicalFile(string path)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var lines = File.ReadAllLines(path);
                        if (lineCount != 0)
                            return lines.Take(lineCount);
                        return lines;
                    }
                    else
                        return new string[] { $"Specified file {path} doesn't exist." };
                }
                catch (Exception e) { return new string[] { $"Unable to read file: {e.Message}" }; }
            }

            // Validate file existence
            int? id = GetFileID(itemname);
            // Non-managed file, try to read from disk
            if (id == null)
            {
                List<string> result = new List<string>();
                result.Add($"(`{itemname}` is not managed)");
                result.AddRange(ReadPhysicalFile(GetPhysicalPathForFilesThatCanBeInsideFolder(itemname)));
                return result;
            }
            // Managed file
            var file = GetFileDetail(id.Value);
            if (file.Content != null)
            {
                var lines = file.Content.Split(new char[] { '\r', '\n' });
                if (lineCount != 0)
                    return lines.Take(lineCount);
                return lines;
            }
            else
            {
                string path = GetPhysicalName(itemname);
                if (!FileExistsAtHomeFolder(path))
                    return new string[] { $"Item name `{itemname}` with path `{path}` doesn't exist at home folder." };
                else
                    return ReadPhysicalFile(GetPathInHomeHolder(path));
            }
        }
        [Command("Remove a file from Home directory, deletes the file both physically and from database.",
            "If the file doesn't exist on disk or in database then will issue a warning instead of doing anything.", category: "File")]
        [CommandArgument("filename", "name of file")]
        [CommandArgument("-f", "force physical deletion instead of mark as \"_deleted\"", optional: true)]
        public IEnumerable<string> RM(params string[] args)
        {
            ValidateArgs(args);
            string itemname = args[0];
            // Validate file existence (in database)
            int? id = GetFileID(itemname);
            if (id == null)
                throw new InvalidOperationException($"Specified item `{itemname}` is not managed in database.");
            // Delete file on disk if it's not note
            string[] result = null;
            if (GetFileDetail(id.Value).Content == null)
            {
                // Check file existence
                if (!FileExistsAtHomeFolder(GetPhysicalNameForFilesThatCanBeInsideFolder(itemname)))
                    throw new ArgumentException($"Specified item `{itemname}` doesn't exist on disk.");
                else
                {
                    // Delete from filesystem
                    if (args.Length == 2 && args[1] == "-f")
                    {
                        DeleteFileFromHomeFolder(GetPhysicalNameForFilesThatCanBeInsideFolder(itemname));
                        result = new string[] { $"File `{itemname}` is forever gone (deleted)." };
                    }
                    else
                    {
                        string oldPhysicalName = GetPhysicalNameForFilesThatCanBeInsideFolder(itemname);
                        MoveFileInHomeFolder(oldPhysicalName, 
                            GetNewPhysicalNameForFilesThatCanBeInsideFolder(itemname + "_deleted", id.Value, oldPhysicalName));
                        result = new string[] { $"File `{itemname}` is marked as \"_deleted\"." };
                    }
                }
            }
            else
                result = new string[] { $"Note `{itemname}` has been deleted." };
            // Delete from DB
            RemoveFile(itemname);
            TryRecordCommit(JournalEvent.CommitOperation.DeleteFile, itemname, null);
            return result;
        }
        [Command("Removes a tag.", 
            "This command deletes the tag from the database, there is no going back.",
            category: "Taggging")]
        [CommandArgument("tags", "comma delimited list of tags in double quotes")]
        public IEnumerable<string> RMT(params string[] args)
        {
            ValidateArgs(args);
            string[] tags = args[0].SplitTags().ToArray();   // Get as lower case
            // Specially handle single tag specifically with existence validation
            if(tags.Length == 1)
            {
                string tag = tags[0];
                var tagID = GetTagID(tag);
                if (tagID == null)
                    throw new ArgumentException($"Specified tag `{tag}` doesn't exist in database.");
                // Delete file tags
                DeleteFileTag(tagID.Value);
                // Delete tag itself
                DeleteTag(tag);
                TryRecordCommit(JournalEvent.CommitOperation.DeleteTag, tag, null);
                return new string[] { $"Tag `{tag}` has been deleted from database." };
            }
            // For more than one tag we don't enforce strict existence validation
            else
            {
                List<TagRow> realTags = GetTagRows(tags);
                // Delete from FileTag table
                DeleteFileTags(realTags.Select(t => t.ID), false);
                // Delete from Tag table
                DeleteTags(realTags.Select(t => t.ID));
                // Commit deletion
                foreach (var tag in realTags)
                    TryRecordCommit(JournalEvent.CommitOperation.DeleteTag, tag.Name, null);
                // Return
                return new string[] { $"Tags `{realTags.Select(t => t .Name).JoinTags()}` " +
                    $"{(realTags.Count > 1 ? "have" : "has")} been deleted." };
            }            
        }
        [Command("Evaluate a Lua script.", category: "Advanced")]
        [CommandArgument("script", "either plain script string or name of the script file (can be either managed or not managed)")]
        public IEnumerable<string> Run(params string[] args)
        {
            string BuildFromLines(IEnumerable<string> lines)
            {
                StringBuilder builder = new StringBuilder();
                foreach (var line in lines)
                    builder.AppendLine(line);
                return builder.ToString();
            }

            ValidateArgs(args);
            string script = args[0];
            // Evaluate script file
            int suffixLocation = script.ToLower().LastIndexOf(".lua");
            if (suffixLocation != -1 && suffixLocation == script.Length - 4)
            {
                // File can be managed, so use Read to get lines
                var lines = Read();
                // Build and evaluate script
                DynValue res = Script.RunString(BuildFromLines(lines));
                return new string[] { res.ToString() };
            }
            // Evaluate script itself
            else
            {
                DynValue res = Script.RunString(script);
                return new string[] { res.ToString() };
            }
        }
        [Command("Displays the state of the Home directory and the staging area.",
            "Shows which files have been staged, which haven't, and which files aren't being tracked by Somewhere. " +
            "Notice only the files in current directory are checked, we don't go through children folders. " +
            "We also don't check folders. The (reasons) last two points are made clear in design document.")]
        public IEnumerable<string> Status(params string[] args)
        {
            var files = Directory.GetFiles(HomeDirectory).OrderBy(f => f)
                .Select(filepath => Path.GetFileName(filepath))
                .Where(filename => filename.IndexOf("_deleted") != filename.Length - "_deleted".Length)
                .Except(new string[] { DBName });  // Exlude db file itself
            var directories = Directory.GetDirectories(HomeDirectory);
            var managed = Connection.ExecuteQuery("select Name from File where Name is not null").List<string>()
                ?.ToDictionary(f => f, f => 1 /* Dummy */) ?? new Dictionary<string, int>();
            List<string> result = new List<string>();
            result.Add($"Home: {HomeDirectory}");
            result.Add($"{files.Count()} {(files.Count() > 1 ? "files" : "file")} on disk; " +
                $"{directories.Count()} {(directories.Count() > 1 ? "directories" : "directory")} on disk.");
            result.Add($"------------");
            int newFilesCount = 0;
            foreach (string file in files)
            {
                if (!managed.ContainsKey(file))
                {
                    result.Add($"[New] {file}");
                    newFilesCount++;
                }
            }
            result.Add($"{newFilesCount} new files. " + $"{managed.Count} {(managed.Count > 1 ? "items" : "item")} in database.");
            return result;
        }
        [Command("Tag a specified file.", 
            "Tags are case-insensitive and will be stored in lower case; Though allowed, it's recommended tags don't contain spaces. " +
            "Use underscore \"_\" to connect words. Spaces immediately before and after comma delimiters are trimmed. Commas are not allowed in tags, otherwise any character is allowed. " +
            "If specified file doesn't exist on disk or in database then will issue a warning instead of doing anything.",
            category: "Tagging")]
        [CommandArgument("filename", "name of file")]
        [CommandArgument("tags", "comma delimited list of tags in double quotes; any character except commas and double quotes are allowed; double quotes will be replaced by underscore if entered.")]
        public IEnumerable<string> Tag(params string[] args)
        {
            ValidateArgs(args);
            string filename = args[0];
            if (!IsFileInDatabase(filename))
                throw new InvalidOperationException($"Specified file `{filename}` is not managed in database.");
            // Add tags
            string[] tags = args[1].SplitTags().ToArray();
            string[] allTags = AddTagsToFile(filename, tags);
            TryRecordCommit(JournalEvent.CommitOperation.ChangeItemTags, filename, allTags.JoinTags());
            return new string[] { $"File `{filename}` has been updated with a total of {allTags.Length} {(allTags.Length > 1 ? "tags": "tag")}: `{allTags.JoinTags()}`." };
        }
        [Command("Show all tags currently exist.",
            "The displayed result will be a plain alphanumerically ordered list of tag names, " +
            "along with ID and tag usage count.", category: "Display")]
        public IEnumerable<string> Tags(params string[] args)
        {
            ValidateArgs(args);
            List<string> result = new List<string>();
            result.Add($"{"ID", -8}{"Name", -64}{"Usage Count", 8}");
            result.Add(new string('-', result.First().Length));
            // Get tags along with file count
            List<QueryRows.TagCount> tagCount = GetTagCount();
            foreach (QueryRows.TagCount item in tagCount.OrderBy(t => t.Name))
                /* Since we are not using comma delimited list, give ID part some decoration so it's easier to extract using regular expression if one needs. */
                result.Add($"{$"({item.ID})", -8}{item.Name, -64}{$"x{item.Count}", 8}");   // single line formatted list
            result.Add($"Total: {TagCount}");
            return result;
        }
        [Command("Run desktop version of Somewhere.", category: "Misc.")]
        public IEnumerable<string> UI(params string[] args)
        {
            using (System.Diagnostics.Process pProcess = new System.Diagnostics.Process())
            {
                pProcess.StartInfo.FileName = @"SomewhereDesktop";  // Relative to assembly location, not working dir
                pProcess.StartInfo.Arguments = $"\"{HomeDirectory}\""; //argument
                // pProcess.StartInfo.UseShellExecute = false;
                // pProcess.StartInfo.RedirectStandardOutput = true;
                // pProcess.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                // pProcess.StartInfo.CreateNoWindow = true; // not diplay a windows
                pProcess.Start();
                // string output = pProcess.StandardOutput.ReadToEnd(); //The output result
                // pProcess.WaitForExit();
            }
            return null;
        }
        [Command("Untag a file.", category: "Tagging")]
        [CommandArgument("filename", "name of file")]
        [CommandArgument("tags", "comma delimited list of tags in double quotes; any character except commas and double quotes are allowed; " +
            "if the file doesn't have a specified tag then the tag is not effected")]
        public IEnumerable<string> Untag(params string[] args)
        {
            ValidateArgs(args);
            string filename = args[0];
            if (!IsFileInDatabase(filename))
                throw new InvalidOperationException($"Specified file `{filename}` is not managed in database.");
            // Remove tags
            string[] tags = args[1].SplitTags().ToArray();   // Get as lower case
            string[] allTags = RemoveTags(filename, tags);
            return new string[] { $"File `{filename}` has been updated with a total of {allTags.Length} {(allTags.Length > 1 ? "tags": "tag")}: `{allTags.JoinTags()}`." };
        }
        [Command("Update or replace tags for a file completely.", category: "Tagging")]
        [CommandArgument("filename", "name of file")]
        [CommandArgument("tags", "comma delimited list of tags in double quotes; any character except commas and double quotes are allowed")]
        public IEnumerable<string> Update(params string[] args)
        {
            ValidateArgs(args);
            string filename = args[0];
            int? fileID = GetFileID(filename);
            if (fileID == null)
                throw new InvalidOperationException($"Specified file `{filename}` is not managed in database.");
            // Add new tags
            string[] tags = args[1].SplitTags().Distinct().ToArray();   // Get as lower case
            // Replace
            ChangeFileTags(fileID.Value, tags);
            return new string[] { $"Item `{filename}` has been updated with {tags.Length} {(tags.Length > 1 ? "tags" : "tag")}: `{tags.JoinTags()}`." };
        }
        [Command("Enter Somewhere power mode.", 
            "Power mode operates on whole screen area and provides many keyboard specific behaviors.", category: "Advanced")]
        public IEnumerable<string> X(params string[] args)
        {
            // Instantiate and enter power mode
            if (new PowerMode().Enter(this, args))
                InternalBreakSignal = true;
            // Return empty
            return new string[] { };
        }
        /// <summary>
        /// Set by a command to indicate it wishes SW to exit completely;
        /// Handled and respected by caller of commands, not enforced.
        /// </summary>
        public bool InternalBreakSignal { get; set; }
        #endregion

        #region Medium Level Functions (Operational Logics Involved)
        /// <summary>
        /// Add to given file a set of tags which may or may not already be present (new tags are added, already existing tags are ignored)
        /// Return an array of all current tags
        /// </summary>
        public string[] AddTagsToFile(string filename, IEnumerable<string> tags)
        {
            string[] existingTags = GetFileTags(filename);
            IEnumerable<string> newTags = tags.Except(existingTags);
            int fileID = GetFileID(filename).Value;
            AddFileTags(fileID, newTags.Select(tag => TryAddTag(tag)));
            return GetFileTags(filename);
        }
        /// <summary>
        /// Add to given file a set of tags which may or may not already be present (new tags are added, already existing tags are ignored)
        /// Return an array of all current tags
        /// </summary>
        public string[] AddTagsToFile(int id, IEnumerable<string> tags)
        {
            string[] existingTags = GetFileTags(id);
            IEnumerable<string> newTags = tags.Except(existingTags);
            AddFileTags(id, newTags.Select(tag => TryAddTag(tag)));
            return GetFileTags(id);
        }
        /// <summary>
        /// Add to all files given set of tags (add new tags) in large batch
        /// </summary>
        public void AddTagsToFiles(IEnumerable<string> filenames, string[] tags)
        {
            // Make sure all specified tags exist in database and we have their IDs at hand
            IEnumerable<string> newTags = tags.Except(
                /*Get among those input tags which ones already exist in database, the remaining will be new olds*/
                GetTagRows(tags).Select(tag => tag.Name));
            AddTags(tags);
            int[] allTagIDs = GetTagRows(tags).Select(t => t.ID).ToArray();
            Dictionary<string, int> rawFileIDs = GetAllFiles().ToDictionary(f => f.Name, f => f.ID);
            // For each file, prepare its new tags (IDs)
            Dictionary<string, IGrouping<string, FileTagMapping>> fileTagMappings = GetAllFileTagMappings().GroupBy(m => m.FileName).ToDictionary(m => m.Key, m => m);
            Dictionary<int, IEnumerable<int>> fileExistingTagIDs = filenames.ToDictionary(filename => rawFileIDs[filename],
                filename => fileTagMappings.ContainsKey(filename) ? fileTagMappings[filename].Select(n => n.TagID) : new int[] { });
            Dictionary<int, IEnumerable<int>> fileNewTagIDs = fileExistingTagIDs.ToDictionary(f => f.Key, f => allTagIDs.Except(f.Value));
            IEnumerable<object> parameterSets = fileNewTagIDs.SelectMany(ftids => ftids.Value.Select(ft => new { fileId = ftids.Key, tagID = ft }));
            Connection.ExecuteSQLNonQuery("insert into FileTag(FileID, TagID) values(@fileId, @tagId)", parameterSets);
        }
        /// <summary>
        /// Add to files a given set of tags in large batch, each one with distinct set of new tags
        /// </summary>
        public void AddTagsToFiles(Dictionary<string, string[]> fileanemAndTags)
        {
            // Get all tags
            string[] allTags = fileanemAndTags.Values.SelectMany(tag => tag)./*Slight optimization - avoid repetitions*/Distinct().ToArray();
            // Make sure all specified tags exist in database and we have their IDs at hand
            IEnumerable<string> newTags = allTags.Except(
                /*Get among those input tags which ones already exist in database, the remaining will be new olds*/
                GetTagRows(allTags).Select(tag => tag.Name));
            AddTags(newTags);
            Dictionary<string, int> allTagIDs = GetTagRows(allTags).ToDictionary(t => t.Name, t => t.ID);
            Dictionary<string, int> rawFileIDs = GetAllFiles().ToDictionary(f => f.Name, f => f.ID);
            // For each file, prepare its new tags (IDs)
            Dictionary<string, IGrouping<string, FileTagMapping>> fileTagMappings = GetAllFileTagMappings().GroupBy(m => m.FileName).ToDictionary(m => m.Key, m => m);
            Dictionary<int, IEnumerable<int>> fileNewTagIDs = fileanemAndTags.ToDictionary(p => rawFileIDs[p.Key],
                p => /*All specified tags for the file*/p.Value.Select(t => allTagIDs[t])./*Minus all existing tags for the file*/Except(fileTagMappings.ContainsKey(p.Key) ? fileTagMappings[p.Key].Select(n => n.TagID) : new int[] { }));
            // Prepare query parameters
            IEnumerable<object> parameterSets = fileNewTagIDs.SelectMany(ftids => ftids.Value.Select(ft => new { fileId = ftids.Key, tagID = ft }));
            Connection.ExecuteSQLNonQuery("insert into FileTag(FileID, TagID) values(@fileId, @tagId)", parameterSets);
        }
        /// <summary>
        /// Delete all tags that doesn't have any files tagged by them
        /// </summary>
        private void CleanDanglingTags()
        {
            IEnumerable<int> danglingTags = GetDanglingTags();
            if(danglingTags != null)
                DeleteTags(danglingTags);
        }
        /// <summary>
        /// Remove from file given set of tags if present, return updated tags for file
        /// </summary>
        public string[] RemoveTags(string filename, IEnumerable<string> tags)
        {
            var tagIDs = GetTagRows(tags).Select(t => t.ID);    // Notice this returns only those tags that exist in DB
            DeleteFileTags(GetFileID(filename) ?? 0, tagIDs);   // Notice this if effective only for tags that are applicable to the file 
            // (i.e. even if tagIDs contain tags that don't apply to file, those tags do not present in FileTag table so no effect
            return GetFileTags(filename);
        }
        /// <summary>
        /// Remove all tags for a given file, and cleans dangling tags after the operation
        /// </summary>
        public void RemoveAllTags(int fileID)
        {
            // Delete FileTag table records
            DeleteFileTagsByFileID(fileID);
            // Clean dangling tags
            CleanDanglingTags();
        }
        /// <summary>
        /// Remove all tags for a given file, and cleans dangling tags after the operation
        /// </summary>
        public void RemoveAllTags(string filename)
        {
            int? id = GetFileID(filename);
            if (id == null) throw new ArgumentException($"Specified file `{filename}` is not managed.");
            RemoveAllTags(id.Value);
        }
        /// <summary>
        /// Change tags by removing all old ones and replace with new ones by file ID
        /// </summary>
        public void ChangeFileTags(int fileID, IEnumerable<string> newTags)
        {
            // Remove old tags
            RemoveAllTags(fileID);
            // Add new tags
            AddTagsToFile(fileID, newTags);
        }
        /// <summary>
        /// Try add tag and return tag ID, if tag already exist then return existing ID
        /// </summary>
        public int TryAddTag(string tag)
        {
            int? id = GetTagID(tag);
            if (id == null)
                return AddTag(tag);
            return id.Value;
        }
        /// <summary>
        /// Set value for a specific meta attribute for an item
        /// </summary>
        public void SetItemMeta(string itemname, string name, string value)
        {
            int? id = GetFileID(itemname);
            if (id == null)
                throw new ArgumentException($"Item `{itemname}` doesn't exist.");
            else
                SetMeta(id.Value, name, value);
        }
        /// <summary>
        /// Get value for a specific meta attribute for an item
        /// </summary>
        public string GetItemMeta(string itemname, string name)
        {
            int? id = GetFileID(itemname);
            if (id == null)
                throw new ArgumentException($"Item `{itemname}` doesn't exist.");
            else
                return GetMeta(id.Value, name);
        }
        /// <summary>
        /// Get all files that contain specified tags;
        /// Optionally in a transaction
        /// </summary>
        public List<FileRow> FilterByTags(IEnumerable<string> tags, SQLiteTransaction transaction = null)
            => Connection.ExecuteQueryDictionary(transaction, $"select File.* from Tag, File, FileTag " +
                $"where Tag.Name in ({string.Join(", ", tags.Select((t, i) => $"@tag{i}"))}) " +
                $"and FileTag.FileID = File.ID and FileTag.TagID = Tag.ID",
                tags.Select((t, i) => new KeyValuePair<string, string>($"tag{i}", t)).ToDictionary(p => p.Key, p => p.Value as object)).Unwrap<FileRow>();
        /// <summary>
        /// Get all file that contain specified tags with details 
        /// </summary>
        public List<QueryRows.FileDetail> FilterByTagsDetailed(IEnumerable<string> tags, SQLiteTransaction transaction = null)
            => Connection.ExecuteQueryDictionary(  transaction,
                // Notice unlike GetFileDetails, here we don't use left join for File and FileTagDetails
@"select FileTagDetails.*,
	max(Revision.RevisionTime) as RevisionTime, max(Revision.RevisionID) as RevisionCount
from 
	(select File.ID, File.EntryDate, File.Name, group_concat(FileTags.Name, ', ') as Tags, File.Meta
	from File, 
		(select FileTag.FileID, Tag.ID as TagID, Tag.Name
		from Tag, FileTag
		where FileTag.TagID = Tag.ID
        and Tag.Name " + $"in ({string.Join(", ", tags.Select((t, i) => $"@name{i}"))})" + @"
        ) as FileTags
	on FileTags.FileID = File.ID
	group by File.ID) as FileTagDetails 
	left join Revision
on Revision.FileID = FileTagDetails.ID
group by FileTagDetails.ID",
    tags.Select((t, i) => new KeyValuePair<string, string>($"name{i}", t)).ToDictionary(p => p.Key, p => p.Value as object))
            .Unwrap<QueryRows.FileDetail>();
        /// <summary>
        /// Get all file that contain part of name with details 
        /// </summary>
        public List<QueryRows.FileDetail> FilterByNameDetailed(string name)
            => Connection.ExecuteQuery(   // Notice unlike GetFileDetails, here we don't use left join for File and FileTagDetails
@"select FileTagDetails.*,
	max(Revision.RevisionTime) as RevisionTime, max(Revision.RevisionID) as RevisionCount
from 
	(select File.ID, File.EntryDate, File.Name, group_concat(FileTags.Name, ', ') as Tags, File.Meta
	from File left join
		(select FileTag.FileID, Tag.ID as TagID, Tag.Name
		from Tag, FileTag
		where FileTag.TagID = Tag.ID) as FileTags
	on FileTags.FileID = File.ID
    where File.Name like '%' || @name || '%'
	group by File.ID) as FileTagDetails 
	left join Revision
on Revision.FileID = FileTagDetails.ID
group by FileTagDetails.ID", new { name }).Unwrap<QueryRows.FileDetail>();
        #endregion

        #region Low-Level Public (Database) CRUD Interface; Database Query Wrappers; Notice data handling is generic (as long as DB allows) and assumed input parameters make sense (e.g. file actually exists on disk, tag names are lower cases)
        /// <summary>
        /// Add a file entry to database
        /// </summary>
        public int AddFile(string filename, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLInsert(transaction, "insert into File (Name, EntryDate) values (@name, @date)", 
                new { name = filename, date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") });
        /// <summary>
        /// Add a file entry to database with initial content
        /// </summary>
        public int AddFile(string filename, string content, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLInsert(transaction, "insert into File (Name, Content, EntryDate) values (@name, @content, @date)", 
                new { name = filename, content, date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") });
        /// <summary>
        /// Add files in batch
        /// </summary>
        public void AddFiles(IEnumerable<string> filenames)
            => Connection.ExecuteSQLNonQuery("insert into File (Name, EntryDate) values (@name, @date)", 
                filenames.Select(fn => new { name = fn, date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") }));
        /// <summary>
        /// Add files in batch with initial contents (for virtual notes)
        /// </summary>
        public void AddFiles(IEnumerable<Tuple<string, string>> filenamesAndContents)
            => Connection.ExecuteSQLNonQuery("insert into File (Name, Content, EntryDate) values (@name, @content, @date)", 
                filenamesAndContents.Select( fn=> new { name = fn.Item1, content = fn.Item2, date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") }));
        /// <summary>
        /// Remove a file entry from database
        /// </summary>
        public void RemoveFile(string filename)
            => Connection.ExecuteSQLNonQuery("delete from File where Name=@name", new { name = filename });
        /// <summary>
        /// Rename a file entry in database
        /// </summary>
        public void RenameFile(string filename, string newFilename)
            => ChangeFileName(filename, newFilename);
        /// <summary>
        /// Rename a tag in database
        /// </summary>
        public void RenameTag(string tagname, string newTagname, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "update Tag set Name=@newTagname where Name=@tagname", new { tagname, newTagname});
        /// <summary>
        /// Change the name of a file by file ID
        /// </summary>
        public void ChangeFileName(int fileID, string newFilename)
            => Connection.ExecuteSQLNonQuery("update File set Name=@newFilename where ID=@fileID", new { fileID, newFilename });
        /// <summary>
        /// Change the name of a file
        /// </summary>
        public void ChangeFileName(string filename, string newFilename)
            => Connection.ExecuteSQLNonQuery("update File set Name=@newFilename where Name=@filename", new { filename, newFilename });
        /// <summary>
        /// Change the text content of a file by file ID
        /// </summary>
        public void ChangeFileContent(int fileID, string content)
            => Connection.ExecuteSQLNonQuery("update File set Content=@content where ID=@fileID", new { fileID, content });
        /// <summary>
        /// Change the text content of a file
        /// </summary>
        public void ChangeFileContent(string filename, string content)
            => Connection.ExecuteSQLNonQuery("update File set Content=@content where Name=@filename", new { filename, content });
        /// <summary>
        /// Change both the name and content of a file by file id
        /// </summary>
        public void ChangeFile(int fileID, string filename, string content)
            => Connection.ExecuteSQLNonQuery(@"update File 
                set Name=@filename, Content=@content 
                where ID=@fileID", new { fileID, filename, content });
        /// <summary>
        /// Change entry date for a given item
        /// </summary>
        private void ChangeEntryDate(int id, DateTime entryDate)
            => Connection.ExecuteSQLNonQuery("update File set EntryDate=@entryDate where ID=@id", new { id, entryDate });
        /// <summary>
        /// Add a log entry to database in yaml
        /// </summary>
        public void AddLog(LogEvent content)
            => Connection.ExecuteSQLNonQuery("insert into Journal(DateTime, Event) values(@dateTime, @text)",
                new { dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), text = new Serializer().Serialize(content) });
        /// <summary>
        /// Add simple text log to database
        /// </summary>
        public void AddLog(string actionID, string content)
            => AddLog(new LogEvent() { Command = actionID, Result = content });
        /// <summary>
        /// Add a journal entry to database in yaml
        /// </summary>
        public void AddJournal(JournalEvent content, JournalType type)
            => Connection.ExecuteSQLNonQuery("insert into Journal(DateTime, Event, Type) values(@dateTime, @text, @type)",
                new { dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), text = new Serializer().Serialize(content), type = type.ToString() });
        /// <summary>
        /// Get ID of file (item) in database, this can sometimes be useful, though practical application shouldn't depend on it
        /// and should treat it as transient
        /// </summary>
        public int? GetFileID(string filename)
            => Connection.ExecuteQuery("select ID from File where Name=@filename", new { filename}).Single<int?>();
        /// <summary>
        /// Get IDs of files in database
        /// </summary>
        public List<FileRow> GetFileRows(IEnumerable<string> filenames, SQLiteTransaction transaction = null)
            => Connection.ExecuteQueryDictionary(transaction, $"select * from File where Name in ({string.Join(", ", filenames.Select((t, i) => $"@name{i}"))})",
                filenames.Select((t, i) => new KeyValuePair<string, string>($"name{i}", t)).ToDictionary(p => p.Key, p => p.Value as object)).Unwrap<FileRow>();
        /// <summary>
        /// Get ID of tag in database, this can sometimes be useful, though practical application shouldn't depend on it
        /// and should treat it as transient
        /// </summary>
        public int? GetTagID(string tag)
            => Connection.ExecuteQuery("select ID from Tag where Name=@tag", new { tag }).Single<int?>();
        /// <summary>
        /// Get IDs of tags that don't have any FileTag row
        /// </summary>
        private IEnumerable<int> GetDanglingTags()
            => Connection.ExecuteQuery(@"select Tag.ID
                from Tag left join FileTag
                on Tag.ID = FileTag.TagID
                where FileTag.FileID is null").List<int>();
        /// <summary>
        /// Get IDs of tags in database; Notice input tags may or may not be present
        /// </summary>
        public List<TagRow> GetTagRows(IEnumerable<string> tags, SQLiteTransaction transaction = null)
            => Connection.ExecuteQueryDictionary(transaction, $"select * from Tag where Name in ({string.Join(", ", tags.Select((t, i) => $"@tag{i}"))})", 
                tags.Select((t, i) => new KeyValuePair<string, string>($"tag{i}", t)).ToDictionary(p => p.Key, p => p.Value as object)).Unwrap<TagRow>();
        /// <summary>
        /// Get a list of tags along with their count of usage
        /// </summary>
        public List<QueryRows.TagCount> GetTagCount()
            => Connection.ExecuteQuery(@"select Tag.ID, Tag.Name, Count
                from 
	                (select TagID, count(*) as Count
	                from FileTag group by TagID), Tag
                where TagID = Tag.ID").Unwrap<QueryRows.TagCount>();
        /// <summary>
        /// Add tag with given name to database
        /// </summary>
        /// <remarks>
        /// Unlike TryAddTag, there is no checking, we just add
        /// </remarks>
        public int AddTag(string tag, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLInsert(transaction, "insert into Tag(Name) values(@tag)", new { tag });
        /// <summary>
        /// Delete from Tag table
        /// </summary>
        public void DeleteTag(int tagID, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "delete from Tag where ID = @id", new { id = tagID});
        /// <summary>
        /// Delete from Tag table
        /// </summary>
        public void DeleteTag(string tagName, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "delete from Tag where Name = @name", new { name = tagName});
        /// <summary>
        /// Delete all tags by name from Tag table
        /// </summary>
        public void DeleteTags(IEnumerable<string> tagNames, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "delete from Tag where Name = @name", tagNames.Select(name => new { name }) );
        /// <summary>
        /// Delete all tags by id from Tag table
        /// </summary>
        public void DeleteTags(IEnumerable<int> tagIDs, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "delete from Tag where ID = @id", tagIDs.Select(id => new { id }));
        /// <summary>
        /// Add tags with given names to database
        /// </summary>
        /// <remarks>
        /// Unlike TryAddTag, there is no checking, we just add
        /// </remarks>
        public void AddTags(IEnumerable<string> tags)
            => Connection.ExecuteSQLNonQuery("insert into Tag(Name) values(@tag)", tags.Select(tag => new { tag }));
        /// <summary>
        /// Get detail of a file
        /// </summary>
        public QueryRows.FileDetail GetFileDetail(int id)
            => Connection.ExecuteQuery(
@"select FileTagDetails.*,
	max(Revision.RevisionTime) as RevisionTime, max(Revision.RevisionID) as RevisionCount
from 
	(select File.ID, File.EntryDate, File.Name, File.Content, group_concat(FileTags.Name, ', ') as Tags, File.Meta
	from File left join
		(select FileTag.FileID, Tag.ID as TagID, Tag.Name
		from Tag, FileTag
		where FileTag.TagID = Tag.ID
        and FileTag.FileID = @id) as FileTags
	on FileTags.FileID = File.ID
    where File.ID = @id
	group by File.ID) as FileTagDetails 
	left join Revision
on Revision.FileID = FileTagDetails.ID
group by FileTagDetails.ID", new { id }).Unwrap<QueryRows.FileDetail>().SingleOrDefault();
        /// <summary>
        /// Get details of each file
        /// </summary>
        public List<QueryRows.FileDetail> GetFileDetails()
            => Connection.ExecuteQuery(
@"select FileTagDetails.*,
	max(Revision.RevisionTime) as RevisionTime, max(Revision.RevisionID) as RevisionCount
from 
	(select File.ID, File.EntryDate, File.Name, File.Content, group_concat(FileTags.Name, ', ') as Tags, File.Meta
	from File left join
		(select FileTag.FileID, Tag.ID as TagID, Tag.Name
		from Tag, FileTag
		where FileTag.TagID = Tag.ID) as FileTags
	on FileTags.FileID = File.ID
	group by File.ID) as FileTagDetails 
	left join Revision
on Revision.FileID = FileTagDetails.ID
group by FileTagDetails.ID").Unwrap<QueryRows.FileDetail>();
        /// <summary>
        /// Get details of notes, including content
        /// </summary>
        public List<QueryRows.FileDetail> GetNoteDetails()
            => Connection.ExecuteQuery(
@"select FileTagDetails.*,
	max(Revision.RevisionTime) as RevisionTime, max(Revision.RevisionID) as RevisionCount
from 
	(select File.ID, File.EntryDate, File.Name, File.Content, group_concat(FileTags.Name, ', ') as Tags, File.Meta
	from File left join
		(select FileTag.FileID, Tag.ID as TagID, Tag.Name
		from Tag, FileTag
		where FileTag.TagID = Tag.ID) as FileTags
	on FileTags.FileID = File.ID
    where File.Name is null or File.Content is not null
	group by File.ID) as FileTagDetails 
	left join Revision
on Revision.FileID = FileTagDetails.ID
group by FileTagDetails.ID").Unwrap<QueryRows.FileDetail>();
        /// <summary>
        /// Add rows to FileTag table
        /// </summary>
        public void AddFileTags(int fileID, IEnumerable<int> tagIDs, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "insert into FileTag(FileID, TagID) values(@fileID, @tagID)", tagIDs.Select(tagID => new { fileID, tagID }));
        /// <summary>
        /// Add rows to FileTag table
        /// </summary>
        public void AddFileTags(IEnumerable<int> fileIDs, int tagID, SQLiteTransaction transaction = null)
            // Already using a transaction
            => Connection.ExecuteSQLNonQuery(transaction, "insert into FileTag(FileID, TagID) values(@fileID, @tagID)", fileIDs.Select(fileID => new { fileID, tagID }));
        /// <summary>
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTag(int tagID, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "delete from FileTag where TagID=@tagID", new { tagID });
        /// <summary>
        /// <summary>
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTags(int fileID, IEnumerable<int> tagIDs, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "delete from FileTag where FileID=@fileID and tagID=@tagID", tagIDs.Select(tagID => new { fileID, tagID }));
        /// <summary>
        /// Delete file tags record completely for a file or for a tag
        /// </summary>
        public void DeleteFileTags(int id, bool isFileID, SQLiteTransaction transaction = null)
        {
            if (isFileID)
                Connection.ExecuteSQLNonQuery(transaction, "delete from FileTag where FileID=@id", new { id });
            else
                Connection.ExecuteSQLNonQuery(transaction, "delete from FileTag where TagID=@id", new { id });
        }
        /// <summary>
        /// Delete file tags record completely for a file
        /// </summary>
        public void DeleteFileTagsByFileID(int fileID, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "delete from FileTag where FileID=@id", new { id = fileID });
        /// <summary>
        /// Delete file tags record completely for a tag
        /// </summary>
        public void DeleteFileTagsByTagID(int tagID, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "delete from FileTag where FileID=@id", new { id = tagID });
        /// <summary>
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTags(IEnumerable<int> IDs, bool isFileID, SQLiteTransaction transaction = null)
        {
            if (isFileID)
                Connection.ExecuteSQLNonQuery(transaction, "delete from FileTag where FileID=@id", IDs.Select(id => new { id }) );
            else
                Connection.ExecuteSQLNonQuery(transaction, "delete from FileTag where TagID=@id", IDs.Select(id => new { id }));
        }
        /// <summary>
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTags(IEnumerable<int> fileIDs, int tagID, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "delete from FileTag where FileID=@fileID and tagID=@tagID", fileIDs.Select(fileID => new { fileID, tagID }));
        /// <summary>
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTags(IEnumerable<int> fileIDs, IEnumerable<int> tagIDs, SQLiteTransaction transaction = null)
            => Connection.ExecuteSQLNonQuery(transaction, "delete from FileTag where FileID=@fileID and tagID=@tagID", fileIDs.Zip(tagIDs, (fileID, tagID) => new { fileID, tagID }));
        /// <summary>
        /// Get tags for the file, if no tags are added, return empty array
        /// </summary>
        public string[] GetFileTags(string filename, SQLiteTransaction transaction = null)
            => Connection.ExecuteQuery(transaction, @"select Tag.Name from Tag, File, FileTag
                where FileTag.FileID = File.ID
                and FileTag.TagID = Tag.ID
                and File.Name = @filename", new { filename }).List<string>()?.ToArray() ?? new string[] { };
        /// <summary>
        /// Get tags for the file, if no tags are added, return empty array
        /// </summary>
        public string[] GetFileTags(int id, SQLiteTransaction transaction = null)
            => Connection.ExecuteQuery(transaction, @"select Tag.Name from Tag, File, FileTag
                where FileTag.FileID = File.ID
                and FileTag.TagID = Tag.ID
                and File.ID = @id", new { id }).List<string>()?.ToArray() ?? new string[] { };
        /// <summary>
        /// Get content for the file
        /// </summary>
        public string GetFileContent(string filename, SQLiteTransaction transaction = null)
            => Connection.ExecuteQuery(transaction, @"select Content from File where Name = filename", new { filename }).Single<string>();
        /// <summary>
        /// Get content for the file
        /// </summary>
        public string GetFileContent(int id, SQLiteTransaction transaction = null)
            => Connection.ExecuteQuery(transaction, @"select Content from File where ID = id", new { id }).Single<string>();
        /// <summary>
        /// Get raw list of all file tags
        /// </summary>
        public List<FileTagRow> GetAllFileTags(SQLiteTransaction transaction = null)
            => Connection.ExecuteQuery(transaction, @"select * from FileTag").Unwrap<FileTagRow>();
        /// <summary>
        /// Get raw list of all tags
        /// </summary>
        public List<TagRow> GetAllTags(SQLiteTransaction transaction = null)
            => Connection.ExecuteQuery(transaction, @"select * from Tag").Unwrap<TagRow>();
        /// <summary>
        /// Get raw list of all files
        /// </summary>
        public List<FileRow> GetAllFiles(SQLiteTransaction transaction = null)
            => Connection.ExecuteQuery(transaction, @"select * from File where Name is not null and Content is null
                and Name not like '%/' and Name not like '%\'").Unwrap<FileRow>();
        /// <summary>
        /// Get raw list of all notes
        /// </summary>
        public List<FileRow> GetAllNotes(SQLiteTransaction transaction = null)
            => Connection.ExecuteQuery(transaction, @"select * from File where Content is not null").Unwrap<FileRow>();
        /// <summary>
        /// Get raw list of all folders
        /// </summary>
        public List<FileRow> GetAllFolders()
            => Connection.ExecuteQuery(@"select * from File where Name is not null and Content is null
                and (Name like '%/' or Name like '%\')").Unwrap<FileRow>();
        /// <summary>
        /// Get raw list of all knowledge
        /// </summary>
        public List<FileRow> GetAllKnolwedgGetAllKnowledge()
            => Connection.ExecuteQuery(@"select * from File where Name is null and Content is not null").Unwrap<FileRow>();
        /// <summary>
        /// Get raw list of all items
        /// </summary>
        public List<FileRow> GetAllItems()
            => Connection.ExecuteQuery(@"select * from File").Unwrap<FileRow>();
        /// <summary>
        /// Get raw list of all non-knowledge item names
        /// </summary>
        public List<string> GetAllItemNames()
            => Connection.ExecuteQuery(@"select Name from File where Name is not null").List<string>();
        /// <summary>
        /// Get raw list of all configurations
        /// </summary>
        public List<ConfigurationRow> GetAllConfigurations()
            => Connection.ExecuteQuery(@"select * from Configuration").Unwrap<ConfigurationRow>();
        /// <summary>
        /// Try to get a configuration, if it exists, otherwise return null
        /// </summary>
        public string GetConfiguration(string key)
            => Connection.ExecuteQuery(@"select Value from Configuration where Key=@key", new { key }).Single<string>();
        /// <summary>
        /// Try to get a configuration of specified type
        /// </summary>
        public type GetConfiguration<type>(string key)
            => Connection.ExecuteQuery(@"select Value from Configuration where Key=@key", new { key }).Single<type>();
        /// <summary>
        /// Set new or overwrite a configuration
        /// </summary>
        public void SetConfiguration(string key, string value)
            => Connection.ExecuteSQLNonQuery(
                @"INSERT OR REPLACE INTO Configuration (Key, Value, Type, Comment) 
                    VALUES (@key, 
                        @value,
                        COALESCE((SELECT Type FROM Configuration WHERE Key = @key), 'string'),
                        COALESCE((SELECT Comment FROM Configuration WHERE Key = @key), 'A custom configuration.')
                        )", new { key, value });
        /// <summary>
        /// Get meta for a given item
        /// </summary>
        public string GetMeta(int id, string name)
        {
            string meta = Connection.ExecuteQuery(@"select Meta from File where ID=@id", new { id }).Single<string>();
            if (meta == null)
                return null;
            else
                return new YamlQuery(meta).Get<string>(name, false);
        }
        /// <summary>
        /// Set value for a specific meta attribute
        /// </summary>
        public void SetMeta(int id, string name, string value)
        {
            string meta = Connection.ExecuteQuery(@"select Meta from File where ID=@id", new { id }).Single<string>();
            string newMeta = ReplaceOrInitializeMetaAttribute(meta, name, value);
            SetMeta(id, newMeta);
        }
        /// <summary>
        /// Replace part of a meta text, i.e. one of its attribute with a new one;
        /// Returns the newly updated meta text
        /// </summary>
        public string ReplaceOrInitializeMetaAttribute(string meta, string name, string value)
        {
            if (meta == null)
                // Set new meta text with one attribute
                return new Serializer().Serialize(new Dictionary<string, string> { { name, value } });
            else
            {
                // Replace old meta text's corresponding attribute and set new meta text
                var dict = new YamlQuery(meta).ToDictionary();
                dict[name] = value;
                return new Serializer().Serialize(dict);
            }
        }
        public void SetMeta(int id, string meta)
            => Connection.ExecuteSQLNonQuery(@"update File set Meta=@meta where ID=@id", new { id, meta });
        /// <summary>
        /// Get all metas for a given item in KeyValuePair
        /// </summary>
        public Dictionary<string, string> GetMetas(int id)
        {
            string meta = Connection.ExecuteQuery(@"select Meta from File where ID=@id", new { id }).Single<string>();
            if (meta == null)
                return null;
            else
                return new YamlQuery(meta).ToDictionary();
        }
        /// <summary>
        /// Get remark meta for item
        /// </summary>
        public RemarkMeta GetRemarkMeta(int id)
            => ExtractRemarkMeta(Connection.ExecuteQuery("select Meta from File where ID=@id").Single<string>());
        /// <summary>
        /// Extract RemarkMeta from meta text
        /// </summary>
        public RemarkMeta ExtractRemarkMeta(string meta)
            => !string.IsNullOrWhiteSpace(meta)
            ? new DeserializerBuilder()
                        .IgnoreUnmatchedProperties().Build()
                        .Deserialize<RemarkMeta>(meta)
            : null;
        /// <summary>
        /// Get raw list of all logs
        /// </summary>
        public List<LogRow> GetAllLogs()
            => Connection.ExecuteQuery(@"select * from Journal where Type='Log'").Unwrap<LogRow>();
        /// <summary>
        /// Get raw list of all commits
        /// </summary>
        public List<JournalRow> GetAllCommits()
            => Connection.ExecuteQuery(@"select rowid, * from Journal where Type='Commit'").Unwrap<JournalRow>();
        /// <summary>
        /// Get raw list of all journal
        /// </summary>
        public List<JournalRow> GetAllJournal()
            => Connection.ExecuteQuery(@"select rowid, * from Journal").Unwrap<JournalRow>();
        /// <summary>
        /// Get joined table between File, Tag and FileTag database tables
        /// </summary>
        public List<FileTagMapping> GetAllFileTagMappings()
            => Connection.ExecuteQuery(@"select Tag.ID as TagID, Tag.Name as TagName, File.ID as FileID, File.Name as FileName
                from Tag, File, FileTag where FileTag.FileID = File.ID and FileTag.TagID = Tag.ID").Unwrap<FileTagMapping>();
        /// <summary>
        /// Given a filename check whether the file is reocrded in the database
        /// </summary>
        public bool IsFileInDatabase(string filename)
            => Connection.ExecuteQuery("select count(*) from File where Name = @name", new { name = filename }).Single<int>() == 1;
        /// <summary>
        /// Given a tag check whether it's recorded in the database, optionally in a transaction
        /// </summary>
        public bool IsTagInDatabase(string tag, SQLiteTransaction transaction = null)
            => Connection.ExecuteQuery(transaction, "select count(*) from Tag where Name = @name", new { name = tag }).Single<int>() == 1;
        /// <summary>
        /// Generate database for Home
        /// </summary>
        /// <returns>Fullpath of generated file</returns>
        public static string GenerateDBFileAt(string folderPath)
        {
            // Generate file
            using (SQLiteConnection connection = new SQLiteConnection($"DataSource={Path.Combine(folderPath, DBName)};Verions=3;"))
            {
                connection.Open();
                List<string> commands = new List<string>
                {
                    // Create tables
                    @"CREATE TABLE ""Tag"" (
	                    ""ID""	INTEGER PRIMARY KEY AUTOINCREMENT,
                        ""Name""	TEXT NOT NULL UNIQUE
                    )",
                    @"CREATE TABLE ""File"" (
	                    ""ID""	INTEGER PRIMARY KEY AUTOINCREMENT,
	                    ""Name""	TEXT UNIQUE,
	                    ""Content""	TEXT,
	                    ""Meta""	TEXT,
                        ""EntryDate""	TEXT
                    )",
                    @"CREATE TABLE ""FileTag"" (
	                    ""FileID""	INTEGER,
	                    ""TagID""	INTEGER,
	                    PRIMARY KEY(""FileID"",""TagID""),
	                    FOREIGN KEY(""FileID"") REFERENCES ""File""(""ID""),
	                    FOREIGN KEY(""TagID"") REFERENCES ""Tag""(""ID"")
                    )",
                    @"CREATE TABLE ""Journal"" (
	                    ""DateTime""	TEXT,
	                    ""Event""	TEXT,
                        ""Type""    TEXT DEFAULT 'Log'
                    )",
                    @"CREATE TABLE ""Configuration"" (
	                    ""Key""	TEXT,
	                    ""Value""	TEXT,
                        ""Type""	TEXT,
                        ""Comment""	TEXT,
	                    PRIMARY KEY(""Key"")
                    )",
                    @"CREATE TABLE ""Revision"" (
	                    ""FileID""	INTEGER,
	                    ""RevisionID""	INTEGER,
	                    ""RevisionTime""	TEXT,
	                    ""Binary""	BLOB,
	                    FOREIGN KEY(""FileID"") REFERENCES ""File""(""ID""),
	                    PRIMARY KEY(""FileID"",""RevisionID"")
                    )",
                    // Assign initial db configuration/status values
                    @"INSERT INTO Configuration (Key, Value, Type, Comment) 
                        values ('InitialVersion', '" + ReleaseVersion + "', 'string', 'String code of software version that created this repository.')",
                    @"INSERT INTO Configuration (Key, Value, Type, Comment) 
                        values ('ThemeColors', '', 'string', 'Theme colors in YAML format.')",
                    @"INSERT INTO Configuration (Key, Value, Type, Comment) 
                        values ('RegisterCommits', 'true', 'boolean', 'Indicates whether Somewhere should record commit operations into Journal; Commit records are used to reconstruct and dump the state of repository. Possible values are `true` and `false`.')"
                };
                connection.ExecuteSQLNonQuery(null, commands);
                connection.Close();
            }
            return Path.Combine(folderPath, DBName);
        }
        #endregion

        #region Primary Properties
        private Dictionary<MethodInfo, CommandAttribute> _CommandMethods = null;
        private Dictionary<MethodInfo, CommandArgumentAttribute[]> _CommandArguments = null;
        private Dictionary<string, MethodInfo> _CommandNames = null;
        private SQLiteConnection _Connection = null;
        private FileSystemWatcher _FSWatcher = null;
        #endregion

        #region Private Subroutines
        private SQLiteConnection Connection
        {
            get
            {
                if (_Connection == null && IsHomePresent)
                {
                    // Open connection
                    _Connection = new SQLiteConnection($"DataSource={GetPathInHomeHolder(DBName)};Version=3;");
                    _Connection.Open();
                    // Automatic update
                    using (SQLiteTransaction transaction = _Connection.BeginTransaction())
                    {
                        // Automatic update release version
                        string releaseVersion = _Connection.ExecuteQuery(transaction, 
                            @"select Value from Configuration where Key='ReleaseVersion'")
                            .Single<string>();
                        if (releaseVersion == null || releaseVersion != ReleaseVersion)
                        {
                            // Add ReleaseVersion configuration property
                            _Connection.ExecuteSQLNonQuery(transaction,
                               @"INSERT OR REPLACE INTO Configuration (Key, Value, Type, Comment) 
                                VALUES (@key, @value,
                                    COALESCE((SELECT Type FROM Configuration WHERE Key = @key), 'string'),
                                    COALESCE((SELECT Comment FROM Configuration WHERE Key = @key), 'Somewhere executable release version.')
                                    )", new { key = "ReleaseVersion", value = ReleaseVersion });
                            // Add RegisterCommits configuration property
                            _Connection.ExecuteSQLNonQuery(transaction,
                                @"INSERT OR REPLACE INTO Configuration (Key, Value, Type, Comment) 
                                VALUES (@key, 
                                    COALESCE((SELECT Value FROM Configuration WHERE Key = @key), 'true'),
                                    COALESCE((SELECT Type FROM Configuration WHERE Key = @key), 'boolean'),
                                    COALESCE((SELECT Comment FROM Configuration WHERE Key = @key), @comment)
                                    )", new { key = "RegisterCommits", comment = "Indicates whether Somewhere should record commit operations into Journal; Commit records are used to reconstruct and dump the state of repository. Possible values are `true` and `false`." });
                        }
                        // Automatic update old Log table to new Journal table
                        if (_Connection.ExecuteQuery(transaction, @"SELECT count(name) FROM sqlite_master WHERE type='table' AND name='Log'")
                            .Single<int>() != 0)
                        {
                            _Connection.ExecuteSQLNonQuery(transaction, @"ALTER TABLE Log RENAME TO Journal");
                            _Connection.ExecuteSQLNonQuery(transaction, @"ALTER TABLE Journal ADD COLUMN Type TEXT DEFAULT 'Log'"); // Will automatic set default values so no need for another @"Update Journal set Type='Log'"
                        }
                        transaction.Commit();
                    }
                    return _Connection;
                }
                else if (_Connection != null && IsHomePresent)
                    return _Connection;
                else throw new InvalidOperationException($"Cannot connect to database, not in a Home directory. Use `new` command to initialize a Home repository at current folder.");
            }
        }
        /// <summary>
        /// Enumerate all files in a directory including subfolders in a sorted manner and perform given action:
        ///     - Subfolders first
        ///     - Alphabetically
        /// Assume given starting directory exists
        /// </summary>
        private void EnumerateAllFilesInDirectory(string startingDirectory, Action<string> action)
        {
            // Enumerate sub directories
            foreach (string d in Directory.GetDirectories(startingDirectory).OrderBy(d => d))
                EnumerateAllFilesInDirectory(d, action);
            // Enumerate files and perform action
            foreach (string f in Directory.GetFiles(startingDirectory).OrderBy(f => f))
                action(f);
        }
        /// <summary>
        /// Validate argument (count) for a given command; 
        /// Throw exception when not valid
        /// </summary>
        /// <param name="isHomeOperation">Indicates whether this command requires presence in home folder and will validate
        /// working directory is a valid home folder</param>
        private void ValidateArgs(string[] args, bool isHomeOperation = false, [CallerMemberName]string commandName = null)
        {
            if(isHomeOperation && !IsHomePresent)
                throw new InvalidOperationException($"Current directory {HomeDirectory} is not a Somewhere home folder.");

            commandName = commandName.ToLower();
            var command = CommandNames[commandName];
            var arguments = CommandArguments[command];
            int maxLength = arguments.Length;
            int minLength = arguments.Where(a => a.Optional == false).Count();
            if (args.Length > maxLength || args.Length < minLength)
                throw new InvalidOperationException($"Command `{commandName}` requires " +
                    $"{(maxLength != minLength ? $"{arguments.Length}": $"{minLength}-{maxLength}")} arguments, " +
                    $"{args.Length} is given. Use `help {commandName}`.");
        }
        /// <summary>
        /// Given a path, get relative to Home directory if it contains that portion of path;
        /// If it's actually a relative path then just return it otherwise if it's rooted return null;
        /// </summary>
        private string GetRelative(string path)
        {
            if (path.Contains(HomeDirectory))
                return path.Substring(HomeDirectory.Length);
            else if (Path.IsPathRooted(path))
                return null;
            return path;
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
            if(commandAttribute.Documentation != null)
                commandHelp.Add($"\t{commandAttribute.Documentation}");
            var arguments = CommandArguments[command];
            if (arguments.Length != 0) commandHelp.Add($"\tOptions:");
            foreach (var argument in arguments)
                commandHelp.Add($"\t\t{argument.Name}{(argument.Optional ? "(Optional)" : "")} - {argument.Explanation}");
            return commandHelp;
        }
        /// <summary>
        /// Show interactive scrollable file rows
        /// </summary>
        private IEnumerable<string> InteractiveFileRows(IEnumerable<QueryRows.FileDetail> fileRows, int pageItemCount = 20)
        {
            List<string> results = new List<string>();
            void OutputDependingOnWwitch(string line)
            {
                if (WriteToConsoleEnabled)
                    Console.WriteLine(line);
                else
                    results.Add(line);
            }
            
            string headerLine = $"{"ID",-8}{"Add Date",-12}{"Name (Alphabetical order)",-40}{"Rev. Time",-18}{"Rev. Cnt",8}"; // Tags and Remarks are shown in seperate line
            OutputDependingOnWwitch(headerLine);
            OutputDependingOnWwitch(new string('-', headerLine.Length));
            int itemCount = 0, pageCount = 1;
            foreach (QueryRows.FileDetail item in fileRows.OrderBy(t => t.Name))
            {
                OutputDependingOnWwitch($"{$"({item.ID})",-8}{item.EntryDate.ToString("yyyy-MM-dd"),-12}{item.Name.Limit(40),-40}" +
                    $"{item.RevisionTime?.ToString("yyyy-MM-dd HH:mm") ?? "",-18}{(item.RevisionCount != 0 ? $"x{item.RevisionCount}" : ""),8}");
                if (!string.IsNullOrEmpty(item.Tags))
                    OutputDependingOnWwitch($"{"Tags: ",20}{item.Tags,-60}");
                if (!string.IsNullOrEmpty(item.Meta))
                {
                    RemarkMeta meta = ExtractRemarkMeta(item.Meta);
                    if (!string.IsNullOrEmpty(meta.Remark))
                        OutputDependingOnWwitch($"{"Remark: ",20}{meta.Remark,-60}");
                }
                itemCount++;
                // Pagination
                if (itemCount == pageItemCount)
                {
                    if(ReadFromConsoleEnabled)
                    {
                        Console.WriteLine($"Page {pageCount}/{(fileRows.Count() + pageItemCount - 1) / pageItemCount} (Press q to quit)");
                        var keyInfo = Console.ReadKey();  // Wait for continue
                        if (keyInfo.Key == ConsoleKey.Q)
                        {
                            Console.WriteLine();
                            break;
                        }
                    }
                    itemCount = 0;
                    pageCount++;
                }
            }
            // Show total only if we are at last page page
            if (pageCount == (fileRows.Count() + pageItemCount - 1) / pageItemCount)    // (Get ceiling)
                OutputDependingOnWwitch($"Total: {fileRows.Count()}");
            return results;
        }
        /// <summary>
        /// Try to make a commit journal entry if commiting is enabled
        /// </summary>
        private void TryRecordCommit(JournalEvent.CommitOperation operation, string itemname, string value)
        {
            // Check whether we have enabled committing and record new journal entry in one single line instead of calling ShouldRecordCommits and AddJournal()
            using(SQLiteTransaction transaction = Connection.BeginTransaction())
            {
                bool shouldRecordCommit = Connection.ExecuteQuery(transaction,
                    @"select Value from Configuration where Key=@key", new { key = "RegisterCommits" })
                    .Single<bool>();
                if(shouldRecordCommit)
                {
                    Connection.ExecuteSQLNonQuery(transaction, "insert into Journal(DateTime, Event, Type) values(@dateTime, @text, @type)",
                        new {
                            dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                            text = new SerializerBuilder().EmitDefaults().Build().Serialize(new JournalEvent()
                                {
                                    Operation = operation,
                                    Target = itemname,
                                    UpdateValue = value,
                                    ValueFormat = JournalEvent.UpdateValueFormat.Full
                                }),
                            type = JournalType.Commit.ToString()
                        });
                    transaction.Commit();
                }
            }
        }
        #endregion

        #region FS Events
        private void OnFSRenamed(object sender, RenamedEventArgs e)
        {
            // Specify what is done when a file is renamed.
            if(WriteToConsoleEnabled)
            {
                if (Console.CursorLeft != 0)
                    Console.WriteLine();    // Simple trick to avoid looking to ugly and interrupt current user input
                Console.WriteLine($"File: {e.OldFullPath} renamed to {e.FullPath}");
            }
            // Check if the file used to be managed by DB, update it
            string oldRelative = GetRelative(e.OldFullPath);
            string newRelative = GetRelative(e.FullPath);   // Notice this path has two propertise: 1. It cannot already exist on disk; 2. It cannot already managed by DB
            // Append folder suffix
            if(Directory.Exists(e.FullPath))
            {
                oldRelative += Path.DirectorySeparatorChar;
                newRelative += Path.DirectorySeparatorChar;
            }
            if (oldRelative != null)
            {
                int? id = GetFileID(oldRelative);
                if(id != null)
                {
                    // Update in DB
                    ChangeFileName(id.Value, newRelative);
                    if (Console.CursorLeft != 0)
                        Console.WriteLine();    // Simple trick to avoid looking to ugly and interrupt current user input
                    Console.WriteLine($"Item `{oldRelative}` has been renamed to `{newRelative}`.");
                }
            }
        }
        private void OnFSDeleted(object sender, FileSystemEventArgs e)
        {
            return; // Currently not used
            // Specify what is done when a file is changed, created, or deleted.
            Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");
        }
        private void OnFSCreated(object sender, FileSystemEventArgs e)
        {
            return; // Currently not used
            // Specify what is done when a file is changed, created, or deleted.
            Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");
        }
        private void OnFSChanged(object sender, FileSystemEventArgs e)
        {
            // Notice per doc (https://docs.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?redirectedfrom=MSDN&view=netframework-4.8#Mtps_DropDownFilterText): "Moving a file is a complex operation that consists of multiple simple operations, therefore raising multiple events. Likewise, some applications (for example, antivirus software) might cause additional file system events that are detected by FileSystemWatcher."
            return; // Currently not used
            // Specify what is done when a file is changed, created, or deleted.
            Console.WriteLine($"File: {e.FullPath} {e.ChangeType}");
        }
        #endregion
    }
}

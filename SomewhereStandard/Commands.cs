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
    public class Commands: IDisposable
    {
        #region Constructor and Disposing
        [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        public Commands(string initialWorkingDirectory, bool initializeFSWatcher = false)
        {
            // Initialize home directory
            HomeDirectory = Path.GetFullPath(initialWorkingDirectory) + Path.DirectorySeparatorChar;
            // Create and register file system watcher
            if(initializeFSWatcher)
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
            => Connection.ExecuteQuery("select count(*) from Log").Single<int>();
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
        public bool FileExistsAtHomeFolder(string filename)
            => File.Exists(Path.Combine(HomeDirectory, GetPhysicalName(filename)));
        public bool DirectoryExistsAtHomeFolder(string directoryName)
            => Directory.Exists(Path.Combine(HomeDirectory, directoryName));
        public string GetPathInHomeHolder(string homeRelativePath)
            => Path.Combine(HomeDirectory, homeRelativePath);
        public void DeleteFileFromHomeFolder(string filename)
            => File.Delete(Path.Combine(HomeDirectory, GetPhysicalName(filename)));
        /// <summary>
        /// Renames a file, also handles name escape and validation
        /// </summary>
        public void MoveFileInHomeFolder(int itemID, string itemname, string newItemname)
        {
            if (itemname == newItemname)
                return;
            File.Move(Path.Combine(HomeDirectory, GetPhysicalName(itemname)),
                Path.Combine(HomeDirectory, GetNewPhysicalName(newItemname, itemID)));
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
        /// Get physical name for a new file
        /// </summary>
        public string GetNewPhysicalName(string newItemname, int itemID, int pathLengthLimit = 256)
        {
            string physicalName = GetPhysicalName(newItemname, out string escapedNameWithoutExtension, out string extension, out int availableNameLength, pathLengthLimit);
            // Get file ID string
            string idString = $"#{itemID}";
            // Validate existence
            if (FileExistsAtHomeFolder(physicalName))
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
        /// Get physical path for a new file
        /// </summary>
        public string GetNewPhysicalPath(string itemname, int itemID)
            => Path.Combine(HomeDirectory, GetNewPhysicalName(itemname, itemID));
        #endregion

        #region Constants
        public const string DBName = "Home.somewhere";
        #endregion

        #region Commands (Public Interface as Library)
        [Command("Add an item to home.")]
        [CommandArgument("itemname", "name of item; use * to add all items in current directory; " +
            "if given path is outside Home directory - for files they will be copied, for folders they will be cut and paste inside Home")]
        [CommandArgument("tags", "tags for the item", optional: true)]
        public IEnumerable<string> Add(params string[] args)
        {
            ValidateArgs(args, true);
            // Add single item
            if(args[0] != "*")
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
                        File.Copy(path, GetPathInHomeHolder(name));
                        itemname = name;
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
                    bool existAsFile = FileExistsAtHomeFolder(itemname), existAsFolder = DirectoryExistsAtHomeFolder(itemname);
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
                    if (args.Length == 2) AddTagsToFile(itemname, args[1].SplitTags());
                    return new string[] { $"Item `{itemname}` added to database with a total of {FileCount} {(FileCount > 1 ? "files": "file")}." };
                }
            }
            // Add all files (don't add directories by default)
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
                    if (args.Length == 2) AddTagsToFile(filename, tags);
                    result.Add($"[Added] `{filename}`");
                }
                result.Add($"Total: {FileCount} {(FileCount > 1 ? "files": "file")}.");
                return result;
            }
        }
        [Command("Get or set configurations.")]
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
            else if(args.Length == 1)
            {
                string key = args[0];
                rows.Add($"{key}:");
                rows.Add(GetConfiguration(key));
            }
            // Set value for configuration
            else if(args.Length == 2)
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
            "If it doesn't have a name (i.e. empty), it's also called a \"knowledge\" item, as is used by Somewhere Knowledge subsystem.")]
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
            // Get tags
            string[] tags = args[2].SplitTags().ToArray();   // Save as lower case
            string[] allTags = AddTagsToFile(id, tags);
            return new string[] { $"{(name == null ? $"Knowledge #{id}" : "Note `{ name }`")} has been created with {allTags.Length} {(allTags.Length > 1 ? "tags": "tag")}: `{allTags.JoinTags()}`." };
        }
        [Command("Export files, folders, notes and knowledge. Placeholder, not implemented yet, coming soon.")]
        public IEnumerable<string> Export(params string[] args)
        {
            throw new NotImplementedException();
        }
        /// <remarks>Due to it's particular function of pagination, this command function behaves slightly different from usual ones;
        /// Instead of returning lines of output for the caller to output, it manages output and keyboard input itself</remarks>
        [Command("Show a list of all files.",
            "Use command line arguments for more advanced display setup.")]
        [CommandArgument("pageitemcount", "number of items to show each time", true)]
        [CommandArgument("datefilter", "a formatted string filtering items with a given entry date; valid formats: specific date string, recent (10 days)", true)]
        public IEnumerable<string> Files(params string[] args)
        {
            ValidateArgs(args);
            int pageItemCount = args.Length >= 1 ? Convert.ToInt32(args[1]) : 20;
            string dateFilter = args.Length >= 2 ? args[2] : null;  // TODO: This feature is not implemented yet
            // Get tags along with file count
            List<QueryRows.FileDetail> fileDetails = GetFileDetails();
            InteractiveFileRows(fileDetails, pageItemCount);
            return new string[] { }; // Return nothing instead
        }
        [Command("Find with (or without) action.",
            "Find with filename, tags and extra information, and optionally perform an action with find results.")]
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
            List<string> result = new List<string>();
            if (args.Length == 3)
                action = args[2].ToLower();
            switch (action)
            {
                case "show":
                    // Interactive display
                    InteractiveFileRows(fileRows);
                    // Return empty results
                    return result;
                default:
                    throw new ArgumentException($"Unrecognized action: `{action}`");
            }
        }
        [Command("Generate documentation of Somewhere program.", logged: false)]
        [CommandArgument("path", "path for the generated file.")]
        public IEnumerable<string> Doc(params string[] args)
        {
            string documentation = "SomewhereDoc.txt";
            if (args != null && args?.Length != 0)
                documentation = args[0];
            using (FileStream file = new FileStream(Path.Combine(HomeDirectory, documentation), FileMode.Create))
            using (StreamWriter writer = new StreamWriter(file))
            {
                foreach (string line in Help(null))
                    writer.WriteLine(line);
                foreach (string commandName in CommandNames.Keys.OrderBy(k => k))
                {
                    writer.WriteLine(); // Add empty line
                    foreach (string line in Help(new string[] { commandName }))
                        writer.WriteLine(line);
                }
            }
            return new string[] { $"Document generated at {((args != null && args.Length == 0) ? Path.Combine(HomeDirectory, documentation) : documentation)}" };
        }
        [Command("Show available commands and general usage help. Use `help commandname` to see more.", logged: false)]
        [CommandArgument("commandname", "name of command", optional: true)]
        public IEnumerable<string> Help(params string[] args)
        {
            // Show list of commands
            if (args == null || args.Length == 0)
            {
                var list = CommandAttributes
                .OrderBy(cm => cm.Key.Name) // Sort alphabetically
                .Select(cm => $"\t{cm.Key.Name.ToLower()} - {(cm.Value as CommandAttribute).Description}").ToList();
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
        [Command("Import items, files, folders and notes.")]
        [CommandArgument("by", "literals")]
        [CommandArgument("byparameter", "format specific parameters; for text sources (csv, txt and md), this can be `line` or `header`")]
        [CommandArgument("under", "literals")]
        [CommandArgument("underparameter", "format specific target parameters; empty for the most source formats; for `md` sources, this can be `header` or `title`")]
        [CommandArgument("from", "literals")]
        [CommandArgument("sourcepath", "path for the import source; either a file or a folder; suffix indicates source, common ones include: folder, csv, txt, tw (TiddlyWiki Json) and md")]
        [CommandArgument("into", "literals")]
        [CommandArgument("target", "target for the import; can be either `file`, `archive`, `note`, or `knowledge`; must be supported by the format")]
        public IEnumerable<string> Im(params string[] args)
        {
            throw new NotImplementedException();
        }
        [Command("Rename file.",
            "If the file doesn't exist on disk or in database then will issue a warning instead of doing anything.")]
        [CommandArgument("filename", "name of file")]
        [CommandArgument("newfilename", "new name of file")]
        public IEnumerable<string> MV(params string[] args)
        {
            ValidateArgs(args);
            string itemname = args[0];
            string newFilename = args[1];
            int? id = GetFileID(itemname);
            if (id == null)
                throw new InvalidOperationException($"Specified item `{itemname}` is not managed in database.");
            if (FileExistsAtHomeFolder(newFilename) || IsFileInDatabase(newFilename))
                throw new ArgumentException($"Itemname `{newFilename}` is already used.");
            // Move in filesystem (if it's a physical file rather than a virtual file)
            string[] result = null;
            if (FileExistsAtHomeFolder(itemname))
            {
                MoveFileInHomeFolder(id.Value, itemname, newFilename);
                result = new string[] { $"File (Physical) `{itemname}` has been renamed to `{newFilename}`." };
            }
            else
                result = new string[] { $"Virtual file `{itemname}` has been renamed to `{newFilename}`." };
            // Update in DB
            RenameFile(itemname, newFilename);
            return result;
        }
        [Command("Move Tags, renames specified tag.",
            "If source tag doesn't exist in database then will issue a warning instead of doing anything. " +
            "If the target tag name already exist, then this action will merge the two tags.")]
        [CommandArgument("sourcetag", "old name for the tag")]
        [CommandArgument("targettag", "new name for the tag")]
        public IEnumerable<string> MVT(params string[] args)
        {
            ValidateArgs(args);
            string sourceTag = args[0];
            string targetTag = args[1];
            if (!IsTagInDatabase(sourceTag))
                throw new InvalidOperationException($"Specified tag `{sourceTag}` does not exist in database.");
            // Update in DB
            if(!IsTagInDatabase(targetTag)) // If target doesn't exist yet just rename source
            {
                RenameTag(sourceTag, targetTag);
                return new string[] { $"Tag `{sourceTag}` is renamed to `{targetTag}`." };
            }
            else
            {
                // Get files with old tag
                List<FileRow> sourceFiles = FilterByTags(new string[] { sourceTag});
                List<TagRow> tagIDs = GetTagRows(new string[] { sourceTag, targetTag });
                int sourceTagID = tagIDs.Where(t => t.Name == sourceTag).Single().ID, 
                    targetTagID = tagIDs.Where(t => t.Name == targetTag).Single().ID;
                // Delete reference to old tag
                DeleteFileTags(sourceFiles.Select(f => f.ID), sourceTagID);
                // Add reference to new tag
                AddFileTags(sourceFiles.Select(f => f.ID), targetTagID);
                // Delete source tag
                DeleteTag(sourceTagID);
                return new string[] { $"Tag `{sourceTag}` is merged into `{targetTag}`" };
            }
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
        [Command("Remove a file from Home directory, deletes the file both physically and from database.",
            "If the file doesn't exist on disk or in database then will issue a warning instead of doing anything.")]
        [CommandArgument("filename", "name of file")]
        [CommandArgument("-f", "force physical deletion instead of mark as \"_deleted\"", optional: true)]
        public IEnumerable<string> RM(params string[] args)
        {
            ValidateArgs(args);
            string itemname = args[0];
            // Validate file existence (in database)
            int? id = GetFileID(itemname);
            if (id == null)
                throw new ArgumentException($"Specified item `{itemname}` doesn't exist on disk.");
            if (!IsFileInDatabase(itemname))
                throw new InvalidOperationException($"Specified item `{itemname}` is not managed in database.");
            // Delete from filesystem
            string[] result = null;
            if (args.Length == 2 && args[1] == "-f")
            {
                DeleteFileFromHomeFolder(itemname);
                result = new string[] { $"File `{itemname}` is forever gone (deleted)." };
            }
            else
            {
                MoveFileInHomeFolder(id.Value, itemname, itemname + "_deleted");
                result = new string[] { $"File `{itemname}` is marked as \"_deleted\"." };
            }
            // Delete from DB
            RemoveFile(itemname);
            return result;
        }
        [Command("Removes a tag.", 
            "This command deletes the tag from the database, there is no going back.")]
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
                return new string[] { $"Tag `{tag}` has been deleted from database." };
            }
            // For more than one tag we don't enforce strict existence validation
            else
            {
                List<TagRow> realTags = GetTagRows(tags);
                // Delete from FileTag table
                DeleteFileTags(realTags.Select(t => t.ID), false);
                // Delete from Tag table
                DeleteTags(realTags.Select(t=>t.ID));
                return new string[] { $"Tags `{realTags.Select(t => t .Name).JoinTags()}` " +
                    $"{(realTags.Count > 1 ? "have" : "has")} been deleted." };
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
            "If specified file doesn't exist on disk or in database then will issue a warning instead of doing anything.")]
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
            return new string[] { $"File `{filename}` has been updated with a total of {allTags.Length} {(allTags.Length > 1 ? "tags": "tag")}: `{allTags.JoinTags()}`." };
        }
        [Command("Show all tags currently exist.",
            "The displayed result will be a plain alphanumerically ordered list of tag names, " +
            "along with ID and tag usage count.")]
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
        [Command("Run desktop version of Somewhere.")]
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
        [Command("Untag a file.")]
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
        [Command("Update or replace tags for a file completely.")]
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
        /// Get all files that contain specified tags
        /// </summary>
        public List<FileRow> FilterByTags(IEnumerable<string> tags)
            => Connection.ExecuteQueryDictionary($"select File.* from Tag, File, FileTag " +
                $"where Tag.Name in ({string.Join(", ", tags.Select((t, i) => $"@tag{i}"))}) " +
                $"and FileTag.FileID = File.ID and FileTag.TagID = Tag.ID",
                tags.Select((t, i) => new KeyValuePair<string, string>($"tag{i}", t)).ToDictionary(p => p.Key, p => p.Value as object)).Unwrap<FileRow>();
        /// <summary>
        /// Get all file that contain specified tags with details 
        /// </summary>
        public List<QueryRows.FileDetail> FilterByTagsDetailed(IEnumerable<string> tags)
            => Connection.ExecuteQueryDictionary(   // Notice unlike GetFileDetails, here we don't use left join for File and FileTagDetails
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
	from File,
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
        public int AddFile(string filename)
            => Connection.ExecuteSQLInsert("insert into File (Name, EntryDate) values (@name, @date)", 
                new { name = filename, date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") });
        /// <summary>
        /// Add a file entry to database with initial content
        /// </summary>
        public int AddFile(string filename, string content)
            => Connection.ExecuteSQLInsert("insert into File (Name, Content, EntryDate) values (@name, @content, @date)", 
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
        public void RenameTag(string tagname, string newTagname)
            => Connection.ExecuteQuery("update Tag set Name=@newTagname where Name=@tagname", new { tagname, newTagname});
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
        /// Add a log entry to database in yaml
        /// </summary>
        public void AddLog(LogEvent content)
            => Connection.ExecuteSQLNonQuery("insert into Log(DateTime, Event) values(@dateTime, @text)",
                new { dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), text = new Serializer().Serialize(content) });
        /// <summary>
        /// Add simple text log to database
        /// </summary>
        public void AddLog(string actionID, string content)
            => AddLog(new LogEvent() { Command = actionID, Result = content });
        /// <summary>
        /// Get ID of file (item) in database, this can sometimes be useful, though practical application shouldn't depend on it
        /// and should treat it as transient
        /// </summary>
        public int? GetFileID(string filename)
            => Connection.ExecuteQuery("select ID from File where Name=@filename", new { filename}).Single<int?>();
        /// <summary>
        /// Get IDs of files in database
        /// </summary>
        public List<FileRow> GetFileRows(IEnumerable<string> filenames)
            => Connection.ExecuteQueryDictionary($"select * from File where Name in ({string.Join(", ", filenames.Select((t, i) => $"@name{i}"))})",
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
        public List<TagRow> GetTagRows(IEnumerable<string> tags)
            => Connection.ExecuteQueryDictionary($"select * from Tag where Name in ({string.Join(", ", tags.Select((t, i) => $"@tag{i}"))})", 
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
        public int AddTag(string tag)
            => Connection.ExecuteSQLInsert("insert into Tag(Name) values(@tag)", new { tag });
        /// <summary>
        /// Delete from Tag table
        /// </summary>
        public void DeleteTag(int tagID)
            => Connection.ExecuteSQLNonQuery("delete from Tag where ID = @id", new { id = tagID});
        /// <summary>
        /// Delete from Tag table
        /// </summary>
        public void DeleteTag(string tagName)
            => Connection.ExecuteSQLNonQuery("delete from Tag where Name = @name", new { name = tagName});
        /// <summary>
        /// Delete all tags by name from Tag table
        /// </summary>
        public void DeleteTags(IEnumerable<string> tagNames)
            => Connection.ExecuteSQLNonQuery("delete from Tag where Name = @name", tagNames.Select(name => new { name }) );
        /// <summary>
        /// Delete all tags by id from Tag table
        /// </summary>
        public void DeleteTags(IEnumerable<int> tagIDs)
            => Connection.ExecuteSQLNonQuery("delete from Tag where ID = @id", tagIDs.Select(id => new { id }));
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
        public void AddFileTags(int fileID, IEnumerable<int> tagIDs)
            => Connection.ExecuteSQLNonQuery("insert into FileTag(FileID, TagID) values(@fileId, @tagId)", tagIDs.Select(tagID => new { fileID, tagID }));
        /// <summary>
        /// Add rows to FileTag table
        /// </summary>
        public void AddFileTags(IEnumerable<int> fileIDs, int tagID)
            => Connection.ExecuteSQLNonQuery("insert into FileTag(FileID, TagID) values(@fileId, @tagId)", fileIDs.Select(fileID => new { fileID, tagID }));
        /// <summary>
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTag(int tagID)
            => Connection.ExecuteSQLNonQuery("delete from FileTag where TagID=@tagID", new { tagID });
        /// <summary>
        /// <summary>
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTags(int fileID, IEnumerable<int> tagIDs)
            => Connection.ExecuteSQLNonQuery("delete from FileTag where FileID=@fileID and tagID=@tagID", tagIDs.Select(tagID => new { fileID, tagID }));
        /// <summary>
        /// Delete file tags record completely for a file or for a tag
        /// </summary>
        public void DeleteFileTags(int id, bool isFileID)
        {
            if (isFileID)
                Connection.ExecuteSQLNonQuery("delete from FileTag where FileID=@id", new { id });
            else
                Connection.ExecuteSQLNonQuery("delete from FileTag where TagID=@id", new { id });
        }
        /// <summary>
        /// Delete file tags record completely for a file
        /// </summary>
        public void DeleteFileTagsByFileID(int fileID)
            => Connection.ExecuteSQLNonQuery("delete from FileTag where FileID=@id", new { id = fileID });
        /// <summary>
        /// Delete file tags record completely for a tag
        /// </summary>
        public void DeleteFileTagsByTagID(int tagID)
            => Connection.ExecuteSQLNonQuery("delete from FileTag where FileID=@id", new { id = tagID });
        /// <summary>
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTags(IEnumerable<int> IDs, bool isFileID)
        {
            if (isFileID)
                Connection.ExecuteSQLNonQuery("delete from FileTag where FileID=@id", IDs.Select(id => new { id }) );
            else
                Connection.ExecuteSQLNonQuery("delete from FileTag where TagID=@id", IDs.Select(id => new { id }));
        }
        /// <summary>
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTags(IEnumerable<int> fileIDs, int tagID)
            => Connection.ExecuteSQLNonQuery("delete from FileTag where FileID=@fileID and tagID=@tagID", fileIDs.Select(fileID => new { fileID, tagID }));
        /// <summary>
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTags(IEnumerable<int> fileIDs, IEnumerable<int> tagIDs)
            => Connection.ExecuteSQLNonQuery("delete from FileTag where FileID=@fileID and tagID=@tagID", fileIDs.Zip(tagIDs, (fileID, tagID) => new { fileID, tagID }));
        /// <summary>
        /// Get tags for the file, if no tags are added, return empty array
        /// </summary>
        public string[] GetFileTags(string filename)
            => Connection.ExecuteQuery(@"select Tag.Name from Tag, File, FileTag
                where FileTag.FileID = File.ID
                and FileTag.TagID = Tag.ID
                and File.Name = @filename", new { filename }).List<string>()?.ToArray() ?? new string[] { };
        /// <summary>
        /// Get tags for the file, if no tags are added, return empty array
        /// </summary>
        public string[] GetFileTags(int id)
            => Connection.ExecuteQuery(@"select Tag.Name from Tag, File, FileTag
                where FileTag.FileID = File.ID
                and FileTag.TagID = Tag.ID
                and File.ID = @id", new { id }).List<string>()?.ToArray() ?? new string[] { };
        /// <summary>
        /// Get content for the file
        /// </summary>
        public string GetFileContent(string filename)
            => Connection.ExecuteQuery(@"select Content from File where Name = filename", new { filename }).Single<string>();
        /// <summary>
        /// Get content for the file
        /// </summary>
        public string GetFileContent(int id)
            => Connection.ExecuteQuery(@"select Content from File where ID = id", new { id }).Single<string>();
        /// <summary>
        /// Get raw list of all file tags
        /// </summary>
        public List<FileTagRow> GetAllFileTags()
            => Connection.ExecuteQuery(@"select * from FileTag").Unwrap<FileTagRow>();
        /// <summary>
        /// Get raw list of all tags
        /// </summary>
        public List<TagRow> GetAllTags()
            => Connection.ExecuteQuery(@"select * from Tag").Unwrap<TagRow>();
        /// <summary>
        /// Get raw list of all files
        /// </summary>
        public List<FileRow> GetAllFiles()
            => Connection.ExecuteQuery(@"select * from File where Name is not null and Content is null
                and Name not like '%/' and Name not like '%\'").Unwrap<FileRow>();
        /// <summary>
        /// Get raw list of all notes
        /// </summary>
        public List<FileRow> GetAllNotes()
            => Connection.ExecuteQuery(@"select * from File where Content is not null").Unwrap<FileRow>();
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
                        COALESCE((SELECT Comment FROM Configuration WHERE Key = @key), 'A custom configuration')
                        )", new { key, value });
        /// <summary>
        /// Get raw list of all logs
        /// </summary>
        public List<LogRow> GetAllLogs()
            => Connection.ExecuteQuery(@"select * from Log").Unwrap<LogRow>();
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
        /// Given a tag check whether it's recorded in the database
        /// </summary>
        public bool IsTagInDatabase(string tag)
            => Connection.ExecuteQuery("select count(*) from Tag where Name = @name", new { name = tag }).Single<int>() == 1;
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
                    @"CREATE TABLE ""Log"" (
	                    ""DateTime""	TEXT,
	                    ""Event""	TEXT
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
                        values ('Version', 'V0.0.5', 'string', 'String code of software version.')",
                    @"INSERT INTO Configuration (Key, Value, Type, Comment) 
                        values ('ThemeColors', '', 'string', 'Theme colors in YAML format.')"
                };
                connection.ExecuteSQLNonQuery(commands);
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
                    _Connection = new SQLiteConnection($"DataSource={Path.Combine(HomeDirectory, DBName)};Version=3;");
                    _Connection.Open();
                    return _Connection;
                }
                else if (_Connection != null && IsHomePresent)
                    return _Connection;
                else throw new InvalidOperationException($"Cannot connect to database, not in a Home directory.");
            }
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
            var command = CommandNames[commandName];
            var commandAttribute = CommandAttributes[command];
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
        private void InteractiveFileRows(IEnumerable<QueryRows.FileDetail> fileRows, int pageItemCount = 20)
        {
            string headerLine = $"{"ID",-8}{"Add Date",-12}{"Name",-40}{"Rev. Time",-18}{"Rev. Cnt",8}"; // Tags and Remarks are shown in seperate line
            Console.WriteLine(headerLine);
            Console.WriteLine(new string('-', headerLine.Length));
            int itemCount = 0, pageCount = 1;
            foreach (QueryRows.FileDetail item in fileRows.OrderBy(t => t.Name))
            {
                Console.WriteLine($"{$"({item.ID})",-8}{item.EntryDate.ToString("yyyy-MM-dd"),-12}{item.Name.Limit(40),-40}" +
                    $"{item.RevisionTime?.ToString("yyyy-MM-dd HH:mm") ?? "",-18}{(item.RevisionCount != 0 ? $"x{item.RevisionCount}" : ""),8}");
                if (!string.IsNullOrEmpty(item.Tags))
                    Console.WriteLine($"{"Tags: ",20}{item.Tags,-60}");
                if (!string.IsNullOrEmpty(item.Meta))
                {
                    FileMeta meta = new Deserializer().Deserialize<FileMeta>(item.Meta);
                    if (!string.IsNullOrEmpty(meta.Remark))
                        Console.WriteLine($"{"Remark: ",20}{meta.Remark,-60}");
                }
                itemCount++;
                // Pagination
                if (itemCount == pageItemCount)
                {
                    Console.WriteLine($"Page {pageCount}/{(fileRows.Count() + pageItemCount - 1) / pageItemCount} (Press q to quit)");
                    var keyInfo = Console.ReadKey();  // Wait for continue
                    if (keyInfo.Key == ConsoleKey.Q)
                    {
                        Console.WriteLine();
                        break;
                    }
                    itemCount = 0;
                    pageCount++;
                }
            }
            // Show total only if we are at last page page
            if (pageCount == (fileRows.Count() + pageItemCount - 1) / pageItemCount)    // (Get ceiling)
                Console.WriteLine($"Total: {fileRows.Count()}");
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

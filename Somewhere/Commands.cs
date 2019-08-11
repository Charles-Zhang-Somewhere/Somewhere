using SQLiteExtension;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using YamlDotNet.Serialization;

namespace Somewhere
{
    public class Commands
    {
        #region Constructor
        public Commands(string workingDirectory)
            => HomeDirectory = workingDirectory;
        #endregion

        #region Public Properties
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
        /// Check whether current working directory is a "home" folder, i.e. whether Somewhere DB file is present
        /// </summary>
        public bool IsHomePresent
            => FileExistsAtHomeFolder(DBName);
        /// <summary>
        /// Count of managed files
        /// </summary>
        public int FileCount
            => Connection.ExecuteQuery("select count(*) from File").Single<int>();
        /// <summary>
        /// Get count of log entry
        /// </summary>
        public int LogCount
            => Connection.ExecuteQuery("select count(*) from Log").Single<int>();
        #endregion

        #region Constants
        public const string DBName = "Home.somewhere";
        #endregion

        #region Commands (Public Interface as Library)
        [Command("Add a file to home.")]
        [CommandArgument("filename", "name of file; use * to add all in current directory")]
        [CommandArgument("tags", "tags for the file", optional: true)]
        public IEnumerable<string> Add(params string[] args)
        {
            ValidateArgs(args, true);
            // Add single file
            if(args[0] != "*")
            {
                string filename = args[0];
                if (!FileExistsAtHomeFolder(filename) && !DirectoryExistsAtHomeFolder(filename))
                    throw new ArgumentException($"Specified file (directory) {filename} doesn't exist on disk.");
                if (IsFileInDatabase(filename))
                    return new string[] { $"File `{filename}` already added in database." };
                else
                {
                    AddFile(filename);
                    if (args.Length == 2) UpdateTags(filename, args[1].Split(',').Select(a => a.Trim().ToLower()));
                    return new string[] { $"File `{filename}` added to database with a total of {FileCount} {(FileCount > 1 ? "files": "file")}." };
                }
            }
            // Add all files
            else
            {
                string[] newFiles = Directory.EnumerateFiles(HomeDirectory)
                    .Select(filepath => Path.GetFileName(filepath)) // Returned strings contain folder path
                    .Where(filename => !IsFileInDatabase(filename) && filename != DBName)
                    .ToArray();    // Exclude DB file itself
                string[] tags = args[1].Split(',').Select(a => a.Trim().ToLower()).ToArray();
                List<string> result = new List<string>();
                result.Add($"Add {newFiles.Length} files");
                result.Add("------------");
                foreach (var filename in newFiles)
                {
                    AddFile(filename);
                    if (args.Length == 2) UpdateTags(filename, tags);
                    result.Add($"[Added] `{filename}`");
                }
                result.Add($"Total: {FileCount} {(FileCount > 1 ? "files": "file")}.");
                return result;
            }
        }
        [Command("Create a virtual file (virtual text note).")]
        [CommandArgument("filename", "name for the virtual file, must be unique among all managed files")]
        [CommandArgument("content", "initial content for the virtual file")]
        [CommandArgument("tags", "comma delimited list of tags in double quotes; any character except commas and double quotes are allowed.")]
        public IEnumerable<string> Create(params string[] args)
        {
            ValidateArgs(args);
            string filename = args[0];
            if (FileExistsAtHomeFolder(filename))
                throw new ArgumentException($"Specified filename `{filename}` already exist on disk.");
            if (IsFileInDatabase(filename))
                throw new InvalidOperationException($"Specified filename `{filename}` is already used by another virtual file in database.");
            // Get content and save the file
            string content = args[1];
            AddFile(filename, content);
            // Get tags
            string[] tags = args[2].Split(',').Select(a => a.Trim().ToLower()).ToArray();   // Save as lower case
            string[] allTags = UpdateTags(filename, tags);
            return new string[] { $"File `{filename}` has been created with {allTags.Length} {(allTags.Length > 1 ? "tags": "tag")}: `{string.Join(", ", allTags)}`." };
        }
        [Command("Generate documentation of Somewhere program.")]
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
                writer.WriteLine(); // Add empty line
                foreach (string commandName in CommandNames.Keys.OrderBy(k => k))
                    foreach (string line in Help(new string[] { commandName }))
                        writer.WriteLine(line);
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
        [Command("Rename file.",
            "If the file doesn't exist on disk or in database then will issue a warning instead of doing anything.")]
        [CommandArgument("filename", "name of file")]
        [CommandArgument("newfilename", "new name of file")]
        public IEnumerable<string> MV(params string[] args)
        {
            ValidateArgs(args);
            string filename = args[0];
            string newFilename = args[1];
            if (!IsFileInDatabase(filename))
                throw new InvalidOperationException($"Specified file `{filename}` is not managed in database.");
            if (FileExistsAtHomeFolder(newFilename) || IsFileInDatabase(newFilename))
                throw new ArgumentException($"Filename `{filename}` is already used.");
            // Update in DB
            RenameFile(filename, newFilename);
            // Move in filesystem (if it's a physical file rather than a virtual file)
            if (FileExistsAtHomeFolder(filename))
            {
                MoveFileInHomeFolder(filename, newFilename);
                return new string[] { $"File (Physical) `{filename}` has been renamed to `{newFilename}`." };
            }
            else
                return new string[] { $"Virtual file `{filename}` has been renamed to `{newFilename}`." };
        }
        [Command("Create a new Somewhere home at current home directory.")]
        public IEnumerable<string> New(params string[] args)
        {
            try
            {
                string path = GenerateDBFile();
                return new string[] { $"Database generated at ${path}" };
            }
            catch (Exception) { throw; }
        }
        [Command("Remove a file from Home directory, deletes the file both physically and from database.",
            "If the file doesn't exist on disk or in database then will issue a warning instead of doing anything.")]
        [CommandArgument("filename", "name of file")]
        [CommandArgument("-f", "force physical deletion instead of mark as \"_deleted\"", optional: true)]
        public IEnumerable<string> RM(params string[] args)
        {
            ValidateArgs(args);
            string filename = args[0];
            if (!FileExistsAtHomeFolder(filename))
                throw new ArgumentException($"Specified file `{filename}` doesn't exist on disk.");
            if (!IsFileInDatabase(filename))
                throw new InvalidOperationException($"Specified file `{filename}` is not managed in database.");
            // Delete from DB
            RemoveFile(filename);
            // Delete from filesystem
            if (args.Length == 2 && args[1] == "-f")
            {
                DeleteFileFromHomeFolder(filename);
                return new string[] { $"File `{filename}` is forever gone (deleted)." };
            }
            else
            {
                MoveFileInHomeFolder(filename, filename + "_deleted");
                return new string[] { $"File `{filename}` is marked as \"_deleted\"." };
            }
        }
        [Command("Displays the state of the Home directory and the staging area.",
            "Shows which files have been staged, which haven't, and which files aren't being tracked by Somewhere. " +
            "Notice only the files in current directory are checked, we don't go through children folders. " +
            "We also don't check folders. The (reasons) last two points are made clear in design document.")]
        public IEnumerable<string> Status(params string[] args)
        {
            var files = Directory.GetFiles(HomeDirectory).OrderBy(f => f).Except(new string[] { DBName });  // Exlude db file itself
            var managed = Connection.ExecuteQuery("select Name from File").List<string>()
                ?.ToDictionary(f => f, f => 1 /* Dummy */) ?? new Dictionary<string, int>();
            List<string> result = new List<string>();
            result.Add($"{files.Count()} {(files.Count() > 1 ? "files" : "file")} on disk; " +
                $"{managed.Count} {(managed.Count > 1 ? "files" : "file")} in database.");
            result.Add($"------------");
            int newCount = 0;
            foreach (string file in files)
            {
                if (!managed.ContainsKey(file))
                {
                    result.Add($"[New] {file}");
                    newCount++;
                }
            }
            result.Add($"{newCount} new.");
            return result;
        }
        [Command("Tag a specified file.", 
            "Tags are case-insensitive and will be stored in lower case; Though allowed, it's recommended tags don't contain spaces. " +
            "Use underscore \"_\" to connect words. Spaces immediately before and after comma delimiters are trimmed. Commas are not allowed in tags, otherwise any character is allowed." +
            "If specified file doesn't exist on disk or in database then will issue a warning instead of doing anything.")]
        [CommandArgument("filename", "name of file")]
        [CommandArgument("tags", "comma delimited list of tags in double quotes; any character except commas and double quotes are allowed.")]
        public IEnumerable<string> Tag(params string[] args)
        {
            ValidateArgs(args);
            string filename = args[0];
            if (!IsFileInDatabase(filename))
                throw new InvalidOperationException($"Specified file `{filename}` is not managed in database.");
            // Update tags
            string[] tags = args[1].Split(',').Select(a => a.Trim().ToLower()).ToArray();   // Save as lower case
            string[] allTags = UpdateTags(filename, tags);
            return new string[] { $"File `{filename}` has been updated with a total of {allTags.Length} {(allTags.Length > 1 ? "tags": "tag")}: `{string.Join(", ", allTags)}`." };
        }
        [Command("Run desktop version of Somewhere.")]
        public IEnumerable<string> UI(params string[] args)
        {
            using (System.Diagnostics.Process pProcess = new System.Diagnostics.Process())
            {
                pProcess.StartInfo.FileName = @"SomewhereDesktop";  // Relative to assembly location, not working dir
                pProcess.StartInfo.Arguments = ""; //argument
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
            // Update tags
            string[] tags = args[1].Split(',').Select(a => a.Trim().ToLower()).ToArray();   // Get as lower case
            string[] allTags = RemoveTags(filename, tags);
            return new string[] { $"File `{filename}` has been updated with a total of {allTags.Length} {(allTags.Length > 1 ? "tags": "tag")}: `{string.Join(", ", allTags)}`." };
        }
        #endregion

        #region Low-Level Public (Database) Interface; Notice data handling is generic (as long as DB allows) and assumed input parameters make sense (e.g. file actually exists on disk, tag names are lower cases)
        /// <summary>
        /// Add a file entry to database
        /// </summary>
        public void AddFile(string filename)
            => Connection.ExecuteSQLNonQuery("insert into File (Name, EntryDate) values (@name, @date)", new { name = filename, date = DateTime.Now.ToString("yyyy-MM-dd") });
        /// <summary>
        /// Add a file entry to database with initial content
        /// </summary>
        public void AddFile(string filename, string content)
            => Connection.ExecuteSQLNonQuery("insert into File (Name, Content, EntryDate) values (@name, @content, @date)", new { name = filename, content, date = DateTime.Now.ToString("yyyy-MM-dd") });
        /// <summary>
        /// Remove a file entry from database
        /// </summary>
        public void RemoveFile(string filename)
            => Connection.ExecuteSQLNonQuery("delete from File where Name=@name", new { name = filename });
        /// <summary>
        /// Rename a file entry in database
        /// </summary>
        public void RenameFile(string filename, string newFilename)
            => Connection.ExecuteQuery("update File set Name=@newFilename where Name=@filename", new { filename, newFilename });
        /// <summary>
        /// Update the text content of a file
        /// </summary>
        public void UpdateFileContent(string filename, string content)
            => Connection.ExecuteSQLNonQuery("update File set Content=@content where Name=@filename", new { filename, content });
        /// <summary>
        /// Add a log entry to database in yaml
        /// </summary>
        public void AddLog(object content)
            => Connection.ExecuteSQLNonQuery("insert into Log(DateTime, Event) values(@dateTime, @text)",
                new { dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), text = new Serializer().Serialize(content) });
        /// <summary>
        /// Update file with given set of tags which may or may not already be present
        /// Return an array of all current tags
        /// </summary>
        public string[] UpdateTags(string filename, IEnumerable<string> tags)
        {
            string[] existingTags = GetTags(filename);
            IEnumerable<string> newTags = tags.Except(existingTags);
            int fileID = GetFileID(filename);
            IEnumerable<object> parameterSets = newTags.Select(tag => new { fileID, tagID = TryAddTag(tag) });
            Connection.ExecuteSQLNonQuery("insert into FileTag(FileID, TagID) values(@fileId, @tagId)", parameterSets);
            return GetTags(filename);
        }
        /// <summary>
        /// Update file by removing given set of tags if present
        /// </summary>
        private string[] RemoveTags(string filename, IEnumerable<string> tags)
        {
            string[] existingTags = GetTags(filename);
            IEnumerable<string> matchingTags = tags.Intersect(existingTags);    // Get only those tags that are applicable to the file
            int fileID = GetFileID(filename);
            var tagIDs = matchingTags.Select(tag => GetTagID(tag));
            Connection.ExecuteSQLNonQuery("delete from FileTag where FileID=@fileID and tagID=@tagID", tagIDs.Select(tagID => new { fileID, tagID }));
            return GetTags(filename);
        }
        /// <summary>
        /// Get ID of file in database, this can sometimes be useful, though practical application shouldn't depend on it
        /// and should treat it as transient
        /// </summary>
        public int GetFileID(string filename)
            => Connection.ExecuteQuery("select ID from File where Name=@filename", new { filename}).Single<int>();
        /// <summary>
        /// Get ID of tag in database, this can sometimes be useful, though practical application shouldn't depend on it
        /// and should treat it as transient
        /// </summary>
        public int GetTagID(string tag)
            => Connection.ExecuteQuery("select ID from Tag where Name=@tag", new { tag }).Single<int>(true);
        /// <summary>
        /// Try add tag and return tag ID, if tag already exist then return existing ID
        /// </summary>
        public int TryAddTag(string tag)
        {
            int id = GetTagID(tag);
            if (id == 0)
            {
                Connection.ExecuteSQLNonQuery("insert into Tag(Name) values(@tag)", new { tag });
                id = GetTagID(tag);
            }
            return id;
        }
        /// <summary>
        /// Get tags for the file, if no tags are added, return empty array
        /// </summary>
        public string[] GetTags(string filename)
            => Connection.ExecuteQuery(@"select Tag.Name from Tag, File, FileTag
                where FileTag.FileID = File.ID
                and FileTag.TagID = Tag.ID
                and File.Name = @filename", new { filename }).List<string>()?.ToArray() ?? new string[] { };
        #endregion

        #region Primary Properties
        private Dictionary<MethodInfo, CommandAttribute> _CommandMethods = null;
        private Dictionary<MethodInfo, CommandArgumentAttribute[]> _CommandArguments = null;
        private Dictionary<string, MethodInfo> _CommandNames = null;
        private SQLiteConnection _Connection = null;
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
        /// Generate database for Home
        /// </summary>
        /// <returns>Fullpath of generated file</returns>
        private string GenerateDBFile()
        {
            // Check not existing
            if (IsHomePresent) throw new InvalidOperationException($"A Somewhere database already exist in {HomeDirectory} directory");
            // Generate file
            using (SQLiteConnection connection = new SQLiteConnection($"DataSource={Path.Combine(HomeDirectory, DBName)};Verions=3;"))
            {
                connection.Open();
                List<string> commands = new List<string>
                {
                    // Create tables
                    @"CREATE TABLE ""Tag"" (
	                    ""ID""	INTEGER PRIMARY KEY AUTOINCREMENT,
	                    ""Name""	TEXT UNIQUE
                    )",
                    @"CREATE TABLE ""File"" (
	                    ""ID""	INTEGER PRIMARY KEY AUTOINCREMENT,
	                    ""Name""	TEXT UNIQUE,
	                    ""Content""	BLOB,
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
	                    PRIMARY KEY(""Key"")
                    )",
                    @"CREATE TABLE ""Revision"" (
	                    ""FileID""	INTEGER,
	                    ""DateTime""	TEXT,
	                    ""Content""	BLOB,
	                    FOREIGN KEY(""FileID"") REFERENCES ""File""(""ID"")
                    )",
                    // Assign initial db meta values
                    @"INSERT INTO Configuration (Key, Value) 
                        values ('Version', 'V1.0.0')"
                };
                connection.ExecuteSQLNonQuery(commands);
            }
            return Path.Combine(HomeDirectory, DBName);
        }
        /// <summary>
        /// Given a filename check whether the file is reocrded in the database
        /// </summary>
        private bool IsFileInDatabase(string filename)
            => Connection.ExecuteQuery("select count(*) from File where Name = @name", new { name = filename })
            .Single<int>() == 1;
        private bool FileExistsAtHomeFolder(string filename)
            => File.Exists(Path.Combine(HomeDirectory, filename));
        private bool DirectoryExistsAtHomeFolder(string directoryName)
            => Directory.Exists(Path.Combine(HomeDirectory, directoryName));
        private void DeleteFileFromHomeFolder(string filename)
            => File.Delete(Path.Combine(HomeDirectory, filename));
        private void MoveFileInHomeFolder(string filename, string newFilename)
            => File.Move(Path.Combine(HomeDirectory, filename), Path.Combine(HomeDirectory, newFilename));
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
                throw new InvalidOperationException($"Command {commandName} requires " +
                    $"{(maxLength != minLength ? $"{arguments.Length}": $"{minLength}-{maxLength}")} arguments, " +
                    $"{args.Length} is given. Use `help {commandName}`.");
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
        #endregion
    }
}

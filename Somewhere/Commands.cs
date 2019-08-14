using SQLiteExtension;
using StringHelper;
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
        /// Count of managed files (including virtual files)
        /// </summary>
        public int FileCount
            => Connection.ExecuteQuery("select count(*) from File").Single<int>();
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
        public bool FileExistsAtHomeFolder(string filename)
            => File.Exists(Path.Combine(HomeDirectory, filename));
        public bool DirectoryExistsAtHomeFolder(string directoryName)
            => Directory.Exists(Path.Combine(HomeDirectory, directoryName));
        public void DeleteFileFromHomeFolder(string filename)
            => File.Delete(Path.Combine(HomeDirectory, filename));
        public void MoveFileInHomeFolder(string filename, string newFilename)
            => File.Move(Path.Combine(HomeDirectory, filename), Path.Combine(HomeDirectory, newFilename));
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
                    if (args.Length == 2) AddTagsToFile(filename, args[1].SplitTags());
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
                string[] tags = args[1].SplitTags().ToArray();
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
            string[] tags = args[2].SplitTags().ToArray();   // Save as lower case
            string[] allTags = AddTagsToFile(filename, tags);
            return new string[] { $"File `{filename}` has been created with {allTags.Length} {(allTags.Length > 1 ? "tags": "tag")}: `{string.Join(", ", allTags)}`." };
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
            string headerLine = $"{"ID",-8}{"Add Date",-12}{"Name",-40}{"Rev. Time",-12}{"Rev. Cnt",8}"; // Tags and Remarks are shown in seperate line
            Console.WriteLine(headerLine);
            Console.WriteLine(new string('-', headerLine.Length));
            // Get tags along with file count
            List<QueryRows.FileDetail> fileDetails = GetFileDetails();
            int itemCount = 0, pageCount = 1;
            foreach (QueryRows.FileDetail item in fileDetails.OrderBy(t => t.Name))
            {
                Console.WriteLine($"{$"({item.ID})",-8}{item.EntryDate.ToString("yyyy-MM-dd"),-12}{item.Name.Limit(40),-40}" +
                    $"{item.RevisionTime?.ToString("yyyy-MM-dd") ?? "",-12}{(item.RevisionCount != 0 ? $"x{item.RevisionCount}" : ""),8}");
                if (!string.IsNullOrEmpty(item.Tags))
                    Console.WriteLine($"{"Tags: ",20}{item.Tags,-60}");
                if(!string.IsNullOrEmpty(item.Meta))
                {
                    FileMeta meta = new Deserializer().Deserialize<FileMeta>(item.Meta);
                    if(!string.IsNullOrEmpty(meta.Remark))
                        Console.WriteLine($"{"Remark: ",20}{meta.Remark,-60}");
                }
                itemCount++;
                // Pagination
                if (itemCount == pageItemCount)
                {
                    Console.WriteLine($"Page {pageCount}/{(fileDetails.Count + pageItemCount - 1)/pageItemCount} (Press q to quit)");
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
            if(pageCount == (fileDetails.Count + pageItemCount - 1) / pageItemCount)
                Console.WriteLine($"Total: {fileDetails.Count}");
            return new string[] { }; // Return nothing instead
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
                List<FileRow> sourceFiles = Filter(new string[] { sourceTag});
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
                return new string[] { $"Tags `{string.Join(", ", realTags.Select(t => t .Name))}` " +
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
                .Except(new string[] { DBName });  // Exlude db file itself
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
            return new string[] { $"File `{filename}` has been updated with a total of {allTags.Length} {(allTags.Length > 1 ? "tags": "tag")}: `{string.Join(", ", allTags)}`." };
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
            // Remove tags
            string[] tags = args[1].SplitTags().ToArray();   // Get as lower case
            string[] allTags = RemoveTags(filename, tags);
            return new string[] { $"File `{filename}` has been updated with a total of {allTags.Length} {(allTags.Length > 1 ? "tags": "tag")}: `{string.Join(", ", allTags)}`." };
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
        public List<FileRow> Filter(IEnumerable<string> tags)
            => Connection.ExecuteQueryDictionary($"select File.* from Tag, File, FileTag " +
                $"where Tag.Name in ({string.Join(", ", tags.Select((t, i) => $"@tag{i}"))}) " +
                $"and FileTag.FileID = File.ID and FileTag.TagID = Tag.ID",
                tags.Select((t, i) => new KeyValuePair<string, string>($"tag{i}", t)).ToDictionary(p => p.Key, p => p.Value as object)).Unwrap<FileRow>();
        #endregion

        #region Low-Level Public (Database) CRUD Interface; Database Query Wrappers; Notice data handling is generic (as long as DB allows) and assumed input parameters make sense (e.g. file actually exists on disk, tag names are lower cases)
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
        /// Add files in batch
        /// </summary>
        public void AddFiles(IEnumerable<string> filenames)
            => Connection.ExecuteSQLNonQuery("insert into File (Name, EntryDate) values (@name, @date)", filenames.Select(fn => new { name = fn, date = DateTime.Now.ToString("yyyy-MM-dd") }));
        /// <summary>
        /// Add files in batch with initial contents (for virtual notes)
        /// </summary>
        public void AddFiles(Dictionary<string, string> filenamesAndContents)
            => Connection.ExecuteSQLNonQuery("insert into File (Name, Content, EntryDate) values (@name, @content, @date)", filenamesAndContents.Select( fn=> new { name = fn.Key, content = fn.Value, date = DateTime.Now.ToString("yyyy-MM-dd") }));
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
        /// Rename a tag in database
        /// </summary>
        public void RenameTag(string tagname, string newTagname)
            => Connection.ExecuteQuery("update Tag set Name=@newTagname where Name=@tagname", new { tagname, newTagname});
        /// <summary>
        /// Change the text content of a file
        /// </summary>
        public void ChangeFileContent(string filename, string content)
            => Connection.ExecuteSQLNonQuery("update File set Content=@content where Name=@filename", new { filename, content });
        /// <summary>
        /// Add a log entry to database in yaml
        /// </summary>
        public void AddLog(object content)
            => Connection.ExecuteSQLNonQuery("insert into Log(DateTime, Event) values(@dateTime, @text)",
                new { dateTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), text = new Serializer().Serialize(content) });
        /// <summary>
        /// Get ID of file in database, this can sometimes be useful, though practical application shouldn't depend on it
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
        /// Delete from Tag table
        /// </summary>
        public void DeleteTags(IEnumerable<string> tagNames)
            => Connection.ExecuteSQLNonQuery("delete from Tag where Name = @name", tagNames.Select(name => new { name }) );
        /// <summary>
        /// Delete from Tag table
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
        /// Get details of each file
        /// </summary>
        public List<QueryRows.FileDetail> GetFileDetails()
            => Connection.ExecuteQuery(
@"select FileTagDetails.*,
	max(Revision.RevisionTime) as RevisionTime, max(Revision.RevisionID) as RevisionCount
from 
	(select File.ID, File.EntryDate, File.Name, group_concat(FileTags.Name, ', ') as Tags, File.Meta
	from File, 
		(select FileTag.FileID, Tag.ID as TagID, Tag.Name
		from Tag, FileTag
		where FileTag.TagID = Tag.ID) as FileTags
	on FileTags.FileID = File.ID
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
        /// Delete file tags record
        /// </summary>
        public void DeleteFileTags(int id, bool isFileID)
        {
            if (isFileID)
                Connection.ExecuteSQLNonQuery("delete from FileTag where FileID=@id", new { id });
            else
                Connection.ExecuteSQLNonQuery("delete from FileTag where TagID=@id", new { id });
        }
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
        /// Get raw list of all files (exclude content meta)
        /// </summary>
        public List<FileRow> GetAllFiles()
            => Connection.ExecuteQuery(@"select * from File").Unwrap<FileRow>();
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
                        ""Name""	TEXT NOT NULL UNIQUE
                    )",
                    @"CREATE TABLE ""File"" (
	                    ""ID""	INTEGER PRIMARY KEY AUTOINCREMENT,
	                    ""Name""	TEXT NOT NULL UNIQUE,
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
	                    ""RevisionID""	INTEGER,
	                    ""RevisionTime""	TEXT,
	                    ""Content""	BLOB,
	                    FOREIGN KEY(""FileID"") REFERENCES ""File""(""ID""),
	                    PRIMARY KEY(""FileID"",""RevisionID"")
                    )",
                    // Assign initial db configuration/status values
                    @"INSERT INTO Configuration (Key, Value) 
                        values ('Version', 'V1.0.0')",
                    @"INSERT INTO Configuration (Key, Value) 
                        values ('Change Log', 'V1.0.0: Basic implementations.')"
                };
                connection.ExecuteSQLNonQuery(commands);
            }
            return Path.Combine(HomeDirectory, DBName);
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

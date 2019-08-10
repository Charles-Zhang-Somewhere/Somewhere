using SQLiteExtension;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace Somewhere
{
    public static class Commands
    {
        #region Public Properties
        /// <summary>
        /// Returns a list of all Command methods
        /// </summary>
        public static Dictionary<MethodInfo, CommandAttribute> CommandMethods
            => _CommandMethods == null 
            ? (_CommandMethods = typeof(Commands).GetMethods(BindingFlags.Static | BindingFlags.Public).ToDictionary(m => m,
                m => m.GetCustomAttributes(typeof(CommandAttribute), false).SingleOrDefault() as CommandAttribute)
                .Where(d => d.Value != null).ToDictionary(d => d.Key, d => d.Value))    // Initialize and return static member
            : _CommandMethods; // Return already initialized member
        public static Dictionary<MethodInfo, CommandArgumentAttribute[]> CommandArguments
            => _CommandArguments == null
            ? (_CommandArguments = CommandMethods.ToDictionary(m => m.Key,
                m => m.Key.GetCustomAttributes(typeof(CommandArgumentAttribute), false)
                .Select(a => a as CommandArgumentAttribute).ToArray())
                .ToDictionary(d => d.Key, d => d.Value))    // Initialize and return static member
            : _CommandArguments; // Return already initialized member
        /// <summary>
        /// Returns a list of all commands by name
        /// </summary>
        public static Dictionary<string, MethodInfo> CommandNames
            => _CommandNames == null
            ? (_CommandNames = CommandMethods.ToDictionary(m => m.Key.Name.ToLower(), m => m.Key)) // Initialize and return static member
            : _CommandNames;
        /// <summary>
        /// Check whether current working directory is a "home" folder, i.e. whether Somewhere DB file is present
        /// </summary>
        public static bool IsHomePresent
            => File.Exists(DBName);
        /// <summary>
        /// Count of managed files
        /// </summary>
        public static int FileCount
            => Connection.ExecuteQuery("select count(*) from File").Single<int>();
        #endregion

        #region Constants
        public const string DBName = "Home.somewhere";
        #endregion

        #region Commands (Public Interface as Library)
        [Command("Create a new Somewhere home at current directory.")]
        public static IEnumerable<string> New(string[] args = null)
        {
            try {
                string path = GenerateDBFile();
                return new string[] { $"Database generated at ${path}" };
            }
            catch (Exception) { throw; }
        }
        [Command("Add a file to home.")]
        [CommandArgument("filename", "name of file")]
        public static IEnumerable<string> Add(string[] args)
        {
            ValidateArgs(args, true);
            string fileName = args[0];
            if (!File.Exists(fileName) && !Directory.Exists(fileName))
                throw new ArgumentException($"Specified file (directory) {fileName} doesn't exist on disk.");
            if (IsFileInHome(fileName))
                return new string[] { $"File `{fileName}` already added in database." };
            else
            {
                AddFile(fileName);
                return new string[] { $"File `{fileName}` added to database with a total of {FileCount} files." };
            }
        }
        [Command("Displays the state of the Home directory and the staging area.",
            "Shows which files have been staged, which haven't, and which files aren't being tracked by Somewhere. " +
            "Notice only the files in current directory are checked, we don't go through children folders. " +
            "We also don't check folders. The (reasons) last two points are made clear in design document.")]
        public static IEnumerable<string> Status(string[] args = null)
        {
            var files = Directory.GetFiles(Directory.GetCurrentDirectory()).OrderBy(f => f);
            var managed = Connection.ExecuteQuery("select Name from File").List<string>()
                ?.ToDictionary(f => f, f => 1 /* Dummy */) ?? new Dictionary<string, int>();
            List<string> result = new List<string>();
            result.Add($"{files.Count()} files on disk; {managed.Count} files in database.");
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
        [Command("Show available commands and general usage help. Use `help commandname` to see more.")]
        [CommandArgument("commandname", "name of command", optional: true)]
        public static IEnumerable<string> Help(string[] args = null)
        {
            // Show list of commands
            if(args == null || args.Length == 0)
            {
                var list = CommandMethods
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
        [Command("Generate documentation of Somewhere program.")]
        [CommandArgument("path", "path for the generated file.")]
        public static IEnumerable<string> Doc(string[] args = null)
        {
            string documentation = "SomewhereDoc.txt";
            if (args != null && args?.Length == 0)
                documentation = args[0];
            using (FileStream file = new FileStream(documentation, FileMode.CreateNew))
            using (StreamWriter writer = new StreamWriter(file))
            {
                foreach (string line in Help(null))
                    writer.WriteLine(line);
                writer.WriteLine(); // Add empty line
                foreach (string commandName in CommandNames.Keys.OrderBy(k => k))
                    foreach (string line in Help(new string[] { commandName }))
                        writer.WriteLine(line);
            }
            return new string[] { $"Document generated at {((args != null && args.Length == 0) ? Path.Combine(Directory.GetCurrentDirectory(), documentation) : documentation)}" };
        }
        [Command("Run desktop version of Somewhere.")]
        public static IEnumerable<string> UI(string[] args = null)
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
        #endregion

        #region Low-Level Public Interface
        public static void AddFile(string fileName)
            => Connection.ExecuteSQLNonQuery("insert into File (Name) values (@name)", new { name = fileName });
        #endregion

        #region Primary Properties
        private static Dictionary<MethodInfo, CommandAttribute> _CommandMethods = null;
        private static Dictionary<MethodInfo, CommandArgumentAttribute[]> _CommandArguments = null;
        private static Dictionary<string, MethodInfo> _CommandNames = null;
        private static SQLiteConnection _Connection = null;
        #endregion

        #region Private Subroutines
        private static SQLiteConnection Connection
        {
            get
            {
                if (_Connection == null && IsHomePresent)
                {
                    _Connection = new SQLiteConnection($"DataSource={DBName};Version=3;");
                    _Connection.Open();
                    return _Connection;
                }
                else if (_Connection != null && IsHomePresent)
                    return _Connection;
                else throw new InvalidOperationException($"Cannot initialize database, not in a Home directory.");
            }
        }
        /// <summary>
        /// Generate database for Home
        /// </summary>
        /// <returns>Fullpath of generated file</returns>
        private static string GenerateDBFile()
        {
            // Check not existing
            if (IsHomePresent) throw new InvalidOperationException($"A Somewhere database already exist in {Directory.GetCurrentDirectory()} directory");
            // Generate file
            using (SQLiteConnection connection = new SQLiteConnection($"DataSource={DBName};Verions=3;"))
            {
                connection.Open();
                List<string> commands = new List<string>
                {
                    // Create tables
                    @"CREATE TABLE ""Tag"" (
	                    ""ID""	INTEGER PRIMARY KEY AUTOINCREMENT,
	                    ""Name""	TEXT
                    )",
                    @"CREATE TABLE ""File"" (
	                    ""ID""	INTEGER PRIMARY KEY AUTOINCREMENT,
	                    ""Name""	TEXT,
	                    ""Content""	BLOB,
	                    ""Meta""	TEXT
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
	                    ""Action""	TEXT
                    )",
                    @"CREATE TABLE ""Configuration"" (
	                    ""Key""	TEXT,
	                    ""Value""	TEXT,
	                    PRIMARY KEY(""Key"")
                    )",
                    // Assign initial db meta values
                    @"INSERT INTO Configuration (Key, Value) 
                        values ('Version', 'V1.0.0')",
                    @"CREATE TABLE ""Revision"" (
	                    ""FileID""	INTEGER,
	                    ""DateTime""	TEXT,
	                    ""Content""	BLOB,
	                    FOREIGN KEY(""FileID"") REFERENCES ""File""(""ID"")
                    )"
                };
                connection.ExecuteSQLNonQuery(commands);
            }
            return Path.Combine(Directory.GetCurrentDirectory(), DBName);
        }
        /// <summary>
        /// Given a filename check whether the file is reocrded in the database
        /// </summary>
        private static bool IsFileInHome(string fileName)
            => Connection.ExecuteQuery("select count(*) from File where Name = @name", new { name = fileName })
            .Single<int>() == 1;
        /// <summary>
        /// Validate argument (count) for a given command; 
        /// Throw exception when not valid
        /// </summary>
        /// <param name="isHomeOperation">Indicates whether this command requires presence in home folder and will validate
        /// working directory is a valid home folder</param>
        private static void ValidateArgs(string[] args, bool isHomeOperation = false, [CallerMemberName]string commandName = null)
        {
            if(isHomeOperation && !IsHomePresent)
                throw new InvalidOperationException($"Current directory {Directory.GetCurrentDirectory()} is not a Somewhere home folder.");

            commandName = commandName.ToLower();
            var command = CommandNames[commandName];
            var arguments = CommandArguments[command];
            if (args.Length != arguments.Length)
                throw new InvalidOperationException($"Command {commandName} requires {arguments.Length} arguments. Use `help {commandName}`.");
        }
        /// <summary>
        /// Get a formatted help info for a given command
        /// </summary>
        private static IEnumerable<string> GetCommandHelp(string commandName)
        {
            List<string> commandHelp = new List<string>();
            var command = CommandNames[commandName];
            var commandAttribute = CommandMethods[command];
            commandHelp.Add($"{commandName} - {commandAttribute.Description}");
            if(commandAttribute.Documentation != null)
                commandHelp.Add($"\t{commandAttribute.Documentation}");
            foreach (var argument in CommandArguments[command])
                commandHelp.Add($"\t{argument.Name}{(argument.Optional ? "(Optional)" : "")} - {argument.Explanation}");
            return commandHelp;
        }
        #endregion
    }
}

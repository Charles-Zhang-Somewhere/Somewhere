using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Data.SQLite;
using SQLiteExtension;

namespace Somewhere
{
    class Program
    {
        #region Program Entry
        static void Main(string[] args)
        {
            // Print working directory for identification
            if(args.Length == 0) Console.WriteLine($"Welcome to somewhere!");   // Show greeting only when it's not annoying, i.e. people not sending any specific command
            Console.WriteLine($"Current working Directory: {Directory.GetCurrentDirectory()}");

            // Show help if no argument is given
            if (args.Length == 0)
            {
                foreach (string line in Help(args))
                    Console.WriteLine(line); 
                return;  // Exit
            }

            // Gather all commands
            var commandMethodAttributes = CommandMethodAttributes;
            var commandNames = commandMethodAttributes.ToDictionary(m => m.Key.Name.ToLower(), m => m);
            // Handle commands (in lower case)
            var argsLower = args[0].ToLower();
            if (commandNames.ContainsKey(argsLower))
            {
                var methodAttribute = commandNames[argsLower];
                var method = methodAttribute.Key;
                var attribute = methodAttribute.Value;
                IEnumerable<string> result = method.Invoke(null, args) as IEnumerable<string>;
                foreach (string line in result)
                    Console.WriteLine(line);
            }
            else
                Console.WriteLine($"Specified command {argsLower} doesn't exist. Try again.");
        }
        #endregion

        #region Properties
        /// <summary>
        /// Returns a list of all Command methods
        /// </summary>
        private static Dictionary<MethodInfo, object> CommandMethodAttributes
            => typeof(Program).GetMethods(BindingFlags.Static | BindingFlags.NonPublic).ToDictionary(m => m,
                m => m.GetCustomAttributes(typeof(CommandAttribute), false).SingleOrDefault())
                .Where(d => d.Value != null).ToDictionary(d => d.Key, d => d.Value);
        /// <summary>
        /// Check whether current working directory is a "home" folder, i.e. whether Somewhere DB file is present
        /// </summary>
        private static bool IsHomePresent
            => File.Exists(SomewhereDBName);
        #endregion

        #region Constants
        private const string SomewhereDBName = "Home.somewhere";
        #endregion

        #region Commands
        /// <summary>
        /// Add a file to home
        /// </summary>
        private static IEnumerable<string> Add()
        {

        }
        [Command("Show available commands and general usage help.")]
        private static IEnumerable<string> Help(string[] args)
        {
            var list = CommandMethodAttributes
                .OrderBy(cm => cm.Key.Name) // Sort alphabetically
                .Select(cm => $"\t{cm.Key.Name.ToLower()} - {(cm.Value as CommandAttribute).Description}").ToList();
            list.Insert(0, "Available Commands: ");
            return list;
        }
        [Command("Run desktop version of Somewhere.")]
        private static void UI()
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
        }
        #endregion

        #region Subroutines
        /// <summary>
        /// Generate database for Home
        /// </summary>
        private static void GenerateDBFile()
        {
            // Check not existing
            if (IsHomePresent) throw new InvalidOperationException($"A {SomewhereDBName} already exist in {Directory.GetCurrentDirectory()} directory");
            // Generate file
            using(SQLiteConnection connection = new SQLiteConnection($"DataSource={SomewhereDBName};Verions=3;"))
            {
                connection.Open();
                List<string> commands = new List<string>
                {
                    @"CREATE TABLE ""Tag"" (
	                    ""ID""	INTEGER PRIMARY KEY AUTOINCREMENT,
	                    ""Name""	TEXT
                    )",
                    @"CREATE TABLE ""File"" (
	                    ""ID""	INTEGER PRIMARY KEY AUTOINCREMENT,
	                    ""Name""	TEXT,
	                    ""Path""	TEXT,
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
                    )"
                };
                connection.ExecuteSQLNonQuery(commands);
            }
        }
        #endregion
    }
}

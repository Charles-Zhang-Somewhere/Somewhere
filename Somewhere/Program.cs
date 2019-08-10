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
            if (args.Length == 0)
            {
                Console.WriteLine($"Welcome to Somewhere, a tag-based personal file management system!");   // Show greeting only when it's not annoying, i.e. people not sending any specific command
                Console.WriteLine($"Current working Directory: {Directory.GetCurrentDirectory()} {(Commands.IsHomePresent ? "" : "(Not a Home folder)")}");
            }

            // Show help if no argument is given
            if (args.Length == 0)
            {
                foreach (string line in Commands.Help(args))
                    Console.WriteLine(line); 
                return;  // Exit
            }

            // Handle commands (in lower case)
            var commandName = args[0].ToLower();
            if (Commands.CommandNames.ContainsKey(commandName))
            {
                var method = Commands.CommandNames[commandName];
                var arguments = args.ToList().GetRange(1, args.Length - 1).ToArray();
                try
                {
                    IEnumerable<string> result = method.Invoke(null, new [] {arguments}) as IEnumerable<string>;
                    if (result != null)
                        foreach (string line in result)
                            Console.WriteLine(line);
                }
                catch (Exception e){ Console.WriteLine($"{e.InnerException.Message}"); }
            }
            else
                Console.WriteLine($"Specified command {commandName} doesn't exist. Try again.");
        }
        #endregion
    }
}

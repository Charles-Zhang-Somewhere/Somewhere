using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Data.SQLite;
using SQLiteExtension;
using System.Text;
using StringHelper;

namespace Somewhere
{
    public class Program
    {
        #region Program Entry
        public static void Main(string[] args)
        {
            Commands Commands = new Commands(Directory.GetCurrentDirectory());
            // Print working directory for identification
            if (args.Length == 0)
            {
                Console.WriteLine($"Welcome to Somewhere, a tag-based personal file management system!");   // Show greeting only when it's not annoying, i.e. people not sending any specific command
                Console.WriteLine($"Current working Directory: {Directory.GetCurrentDirectory()} {(Commands.IsHomePresent ? "" : "(Not a Home folder)")}");
            }

            // Show help if no argument is given then enter interactive session
            if (args.Length == 0)
            {
                foreach (string line in Commands.Help(args))
                    Console.WriteLine(line);

                // Interactive session loop
                Console.WriteLine("You are now in the interactive session, \n" +
                    "  enter a command to continue; Enter `exit` to quit: ");
                while (true)
                {
                    Console.Write("> ");
                    string line = Console.ReadLine();
                    if (line.ToLower() == "exit") break;    // Exit signal
                    else if (line.ToLower() == "pwd") Console.WriteLine(Commands.HomeDirectory);    // View home directory
                    else
                    {
                        // Handle commands (in lower case)
                        var positions = line.BreakCommandLineArgumentPositions();
                        var commandName = positions.GetCommandName();
                        string[] arguments = positions.GetArguments();
                        Commands.ProcessCommand(commandName, arguments);
                    }
                }
            }
            else
            {
                // Handle commands directly (in lower case)
                var commandName = args.GetCommandName();
                var arguments = args.GetArguments();
                Commands.ProcessCommand(commandName, arguments);
            }
        }
        #endregion        
    }
}

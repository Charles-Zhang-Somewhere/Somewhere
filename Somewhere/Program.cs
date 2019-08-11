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
    class Program
    {
        #region Program Entry
        static void Main(string[] args)
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
                    else
                    {
                        // Handle commands (in lower case)
                        var commandName = line.Substring(0, line.IndexOf(' ') == -1 ? line.Length : line.IndexOf(' '));
                        string[] arguments = line.Substring(line.IndexOf(' ') == -1 ? line.Length - 1 : line.IndexOf(' ')).BreakCommandLineArguments();
                        HandleCommand(Commands, commandName, arguments);
                    }
                }
            }
            else
            {
                // Handle commands directly (in lower case)
                var commandName = args[0].ToLower();
                var arguments = args.ToList().GetRange(1, args.Length - 1).ToArray();
                HandleCommand(Commands, commandName, arguments);
            }
        }
        #endregion

        #region Subroutine
        static void HandleCommand(Commands Commands, string commandName, string[] arguments)
        {
            if (Commands.CommandNames.ContainsKey(commandName))
            {
                var method = Commands.CommandNames[commandName];
                var attribute = Commands.CommandAttributes[method];
                try
                {
                    // Execute the command
                    IEnumerable<string> result = method.Invoke(Commands, new[] { arguments }) as IEnumerable<string>;
                    StringBuilder builder = new StringBuilder();
                    if (result != null)
                    {
                        foreach (string line in result)
                        {
                            Console.WriteLine(line);
                            builder.AppendLine(line);
                        }
                    }

                    // Log the command
                    if (attribute.Logged)
                        Commands.AddLog(new { Command = commandName, Arguments = arguments, result = builder.ToString() });
                }
                catch (Exception e) { Console.WriteLine($"{e.InnerException.Message}"); }
            }
            else
                Console.WriteLine($"Specified command {commandName} doesn't exist. Try again.");
        }
        #endregion
    }
}

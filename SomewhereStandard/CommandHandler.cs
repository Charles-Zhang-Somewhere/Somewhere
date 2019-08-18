using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace Somewhere
{
    /// <summary>
    /// A extension class provding shared command handling routines
    /// </summary>
    public static class CommandHandler
    {
        #region CLI Routine
        /// <summary>
        /// Process CLI execution of a command
        /// </summary>
        public static void ProcessCommand(this Commands Commands, string commandName, string[] arguments)
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
                        Commands.AddLog(new LogEvent { Command = commandName, Arguments = arguments, Result = builder.ToString() });
                }
                catch (Exception e) { Console.WriteLine($"{e.InnerException.Message}"); }
            }
            else
                Console.WriteLine($"Specified command {commandName} doesn't exist. Try again.");
        }
        /// <summary>
        /// Execute command and return result strings and don't output to console, providing silent exception handling
        /// </summary>
        public static List<string> ExecuteCommand(this Commands Commands, string commandName, string[] arguments)
        {
            if (Commands.CommandNames.ContainsKey(commandName))
            {
                var method = Commands.CommandNames[commandName];
                var attribute = Commands.CommandAttributes[method];
                try
                {
                    // Execute the command
                    List<string> result = (method.Invoke(Commands, new[] { arguments }) as IEnumerable<string>).ToList();
                    StringBuilder builder = new StringBuilder();
                    if (result != null)
                    {
                        foreach (string line in result)
                            builder.AppendLine(line);
                    }

                    // Log the command
                    if (attribute.Logged)
                        Commands.AddLog(new LogEvent { Command = commandName, Arguments = arguments, Result = builder.ToString() });

                    return result;
                }
                catch (Exception e) { return new List<string>() { $"{e.InnerException.Message}" }; }
            }
            else
                return new List<string> { $"Specified command {commandName} doesn't exist. Try again." };
        }
        #endregion
    }
}

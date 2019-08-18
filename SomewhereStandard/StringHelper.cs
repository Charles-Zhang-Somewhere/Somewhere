using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StringHelper
{
    public static class StringExtension
    {
        /// <summary>
        /// Given a command line style string, break it into seperate strings like usual Windows convention;
        /// I.e. each substring is seperated by space, and spaces are escaped by double quotes, and double qoutes are escapped by 
        /// double double quotes
        /// </summary>
        /// <remarks>
        /// Not necessarily super efficient, but it works for such one-shot circumstances
        /// </remarks>
        public static string[] BreakCommandLineArgumentPositions(this string v)
        {
            List<string> arguments = new List<string>();
            bool isInsideQuote = false;
            string sofar = string.Empty;
            // Below is a state machine approach, which is a bit hard to read in code but actually very intuitive 
            // in terms of logic: basically we start with various key conditions, then gradually add more detailed
            // branched within the conditions to achieve final results.
            // No comments are given because I find it better to keep things clean this way, and it's hard to make 
            // comments anyway. This kind of logics are better illustrated using a state machine diagram.
            // And the way to read it is to go through actual test cases, for that please see StringTest unit tests.
            for (int i = 0; i < v.Length; i++)
            {
                char c = v[i];
                switch (c)
                {
                    case ' ':
                        if (isInsideQuote)
                            sofar += c;
                        else if (sofar != string.Empty)
                        {
                            arguments.Add(sofar);
                            sofar = string.Empty;
                        }
                        break;
                    case '"':
                        if (isInsideQuote)
                        {
                            if(i + 1 < v.Length && v[i+1] == '"')
                            {
                                sofar += c;
                                i++;
                            }
                            else
                            {
                                arguments.Add(sofar);
                                sofar = string.Empty;
                                isInsideQuote = false;
                            }
                        }
                        else
                            isInsideQuote = true;
                        break;
                    default:
                        sofar += c;
                        break;
                }
            }
            // Handle ending
            if (sofar != string.Empty)
                arguments.Add(sofar);
            return arguments.ToArray();
        }

        /// <summary>
        /// Get lower cased command name from an array of parsed command arguments
        /// </summary>
        public static string GetCommandName(this string[] commandLineArgumentsArray, bool lowerCase = true)
            => lowerCase
            ? commandLineArgumentsArray[0].ToLower()
            : commandLineArgumentsArray[0];

        /// <summary>
        /// Get command line arguments since the second element of the array
        /// </summary>
        public static string[] GetArguments(this string[] commandLineArgumentsArray, bool lowerCase = true)
            => commandLineArgumentsArray.ToList().GetRange(1, commandLineArgumentsArray.Length - 1).ToArray();

        /// <summary>
        /// Strictly escape invalid filename characters into underscore for both Linux and Windows systems;
        /// This only escapes names, the name might still be invalid to use due to existence of file with same name etc.
        /// </summary>
        public static string EscapeFilename(this string v)
        {
            // Basic escaping
            string characterEscaped = 
                // Linux
                v.Replace('/', '_')
                // Windows
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace(':', '_')
                .Replace('"', '_')
                .Replace('\\', '_')
                .Replace('|', '_')
                .Replace('?', '_')
                .Replace('*', '_')
                // We additionally support new lines in file name
                .Replace('\r', '_')
                .Replace('\n', '_')
                // Windows names cannot end with space or dot
                .TrimEnd()
                .TrimEnd('.');
            // Invalid name replacing (i.e. with or without extension)
            string pureName = Path.GetFileNameWithoutExtension(characterEscaped);
            string extension = Path.GetExtension(characterEscaped);
            if (new string[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }
                .Contains(pureName.ToUpper()))  // Windows is not case sensitive
                return $"{pureName}_{extension}";   // Do return as properly cased name for readability
            else
                return characterEscaped;
        }

        /// <summary>
        /// Limit the length of a string
        /// </summary>
        /// <param name="targetLength">Include end filler length</param>
        public static string Limit(this string v, int targetLength, string endFiller = "...", string requiredEnding = null)
        {
            if (v == null) return null;
            int actualLength = targetLength - endFiller.Length - (requiredEnding != null ? requiredEnding.Length : 0);
            return (v.Length > actualLength) ? v.Substring(0, actualLength) + endFiller + requiredEnding : v + requiredEnding;
        }

        /// <summary>
        /// Split a comma delimited string of tags into array, lower cased and removed empty
        /// </summary>
        public static IEnumerable<string> SplitTags(this string v)
            => v?.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim().ToLower().Replace('\"', '_')) // Save as lower case; Replace double quote (it can still be entered because command line allows it) with underscore
            .Where(t => !string.IsNullOrEmpty(t))  // Skip empty or white space entries
            ?? new string[] { };    // Return empty for null string
    }
}

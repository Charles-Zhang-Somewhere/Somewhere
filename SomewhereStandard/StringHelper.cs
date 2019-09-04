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
        /// Given a list of tags, properly join then together in comma delimited form and quotes when appropriate
        /// </summary>
        public static string JoinTags(this IEnumerable<string> tags)
            => string.Join(", ", tags.Select(t => 
                // Technically speaking tags won't contain commas during creation, but it's allowed in DB
                t.Contains(',') ? $"\"{/*The same applies to quotes */ (t.Contains('"') ? t.Replace("\"", "\"\"") : t)}\"" : t));

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
        /// Split a comma delimited string of tags into array, lower cased and removed empty;
        /// Notice there is no escape for commas
        /// </summary>
        public static IEnumerable<string> SplitTags(this string v)
            => v?.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(a =>
                // Save as lower case
                a.Trim().ToLower()
                // Replace double quote (it can still be entered because command line allows it) with underscore
                .Replace('"', '_'))
            .Where(t => !string.IsNullOrEmpty(t))  // Skip empty or white space entries
            ?? new string[] { };    // Return empty for null string
        /// <summary>
        /// Split a path as tags, lower cased
        /// </summary>
        public static IEnumerable<string> SplitDirectoryAsTags(this string path)
            => path?.GetRealDirectoryPath().Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(d => 
                // Save as lower case
                d.Trim().ToLower()
                // Replace double quote (not allowed in Windows, allowed in Linux) with underscore
                .Replace('"', '_'))
            .Where(t => !string.IsNullOrEmpty(t))  // Skip empty or white space entries
            ?? new string[] { };    // Return empty for null string
    }

    /// <summary>
    /// A class dedicated for file path related operations, all details defined in corresponding functions
    /// </summary>
    public static class PathExtension
    {
        private readonly static char[] InvalidFilenameCharacters = "/<>:\"\\|?*\r\n".ToCharArray();
        private readonly static char[] InvalidFilenameCharactersLast = "/<>:\"\\|?*\r\n .".ToCharArray();
        /// <summary>
        /// Try to append a separator to the folder path if it's not already appened;
        /// Assume input is a folder path
        /// </summary>
        public static string AppendSeparator(this string folderPath, char preferred = '/')
        {
            if (string.IsNullOrWhiteSpace(folderPath)) return folderPath;
            char last = folderPath.Last();
            if (!last.IsSeparator() && last != ':')
                return folderPath + (folderPath.First().IsSeparator() ? folderPath.First() : preferred);
            else return folderPath;
        }
        /// <summary>
        /// Given a path, check its real path part and see whether file name contains invalid FS characters;
        /// Folder names are always valid (otherwise it's counted as filename)
        /// </summary>
        public static bool ContainsInvalidCharacter(this string path)
        {
            string real = path.GetRealFilename();
            int lastIndex = real.Length - 1;
            for (int i = 0; i < real.Length; i++)
            {
                char c = real[i];
                if (c.IsInvalidFilenameCharacter(i == lastIndex))
                    return true;
            }
            return false;
        }
        /// <summary>
        /// Get root of path as defined in IsPathRooted;
        /// This function allows mixing the two separators.
        /// Since root is a folder, this function always appends an ending folder separator 
        /// (except for Windows relative root at drive name)
        /// </summary>
        /// <returns>
        /// Return null if path is not rooted
        /// </returns>
        public static string GetPathRoot(this string path)
        {
            int NextSeparator(string subpath, int startIndex)
            {
                int seperator1 = subpath.IndexOf('/', startIndex);
                int seperator2 = subpath.IndexOf('\\', startIndex);
                return seperator1 == -1
                    ? seperator2
                    : (seperator2 == -1 ? seperator1 : Math.Min(seperator1, seperator2));
            }
            string GetSubfolder(string subpath, int startIndex)
            {
                int nextSeperator = NextSeparator(subpath, startIndex);
                if (nextSeperator == -1)
                    return path;
                else
                    return path.Substring(0, nextSeperator);
            }
            // Validate it's rooted
            if (path.IsPathRooted() == false)
                return null;
            // Extract root
            string root = string.Empty;
            // Linux root
            if (path[0] == '/')
                root = GetSubfolder(path, 1);
            // Windows root
            else if (path.Length > 1 && path[1] == ':')
            {
                if (path.Length == 2)
                    root = path;
                else if (path[2] == '\\' || path[2] == '/')
                    root = path.Substring(0, 3);
                else
                    root = path.Substring(0, 2);
            }
            // UNC root
            else if (path.Length > 1 && path[0] == '\\' && path[1] == '\\')
                root = GetSubfolder(path, 2);
            // Check protocol
            else
            {
                string protocol = path.GetPathProtocol();
                if (protocol != null)
                    root = protocol;
            }
            return root.AppendSeparator();
        }
        /// <summary>
        /// Get the protocol part of a path if it contains protocol, otherwise return null
        /// </summary>
        public static string GetPathProtocol(this string path)
        {
            if (!path.IsPathContainProtocol())
                return null;
            else
            {
                int colon = path.IndexOf(':');
                // Get protocol itself
                string protocol = path.Substring(0, colon + 1);
                // Get remaining separators
                foreach (var c in path.Substring(colon + 1))
                {
                    if (c.IsSeparator())
                        protocol += c;
                    else break;
                }
                return protocol;
            }
        }
        /// <summary>
        /// Check whether a given character is inavlid filename character
        /// </summary>
        public static bool IsInvalidFilenameCharacter(this char c, bool characterIsLast)
            => characterIsLast 
            ? InvalidFilenameCharactersLast.Contains(c)
            : InvalidFilenameCharacters.Contains(c);
        /// <summary>
        /// Check whether a given path contains protocols
        /// </summary>
        public static bool IsPathContainProtocol(this string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            if (path[0] != ':' && path[0] != '.' && !path[0].IsSeparator()
                // Make sure it's not a windows root
                && path.Length > 2 && path[1] != ':')
            {
                int seperator1 = path.IndexOf('/');
                int seperator2 = path.IndexOf('\\');
                int colon = path.IndexOf(':');
                // No protocol colon
                if (colon == -1)
                    return false;
                // Assert colon occurs before any of the seperators
                else
                {
                    if (seperator1 != -1 && colon < seperator1)
                        return true;
                    else if (seperator2 != -1 && colon < seperator2)
                        return true;
                    else
                        return false;
                }
            }
            else
                return false;
        }
        /// <summary>
        /// Checks whether the path ends with a folder separator
        /// </summary>
        public static bool IsPathFolder(this string path)
            => path.Last().IsSeparator();
        /// <summary>
        /// Get the path to directory without protocol and filename;
        /// This function works for both seperators;
        /// This function will ensure returned value contains a seperator
        /// </summary>
        public static string GetRealDirectoryPath(this string path, char? preferredSeperator = null)
        {
            string real = path.GetRealPath().Replace(path.GetRealFilename(), "").AppendSeparator(preferredSeperator ?? '/');
            if (preferredSeperator.HasValue)
                return real.Replace('\\', preferredSeperator.Value).Replace('/', preferredSeperator.Value);
            else return real;
        }
        /// <summary>
        /// A path is considered rooted if: 
        ///     - On Linux it starts with '/';
        ///     - On Windows it starts with a drive letter and a colon (and may not have drive root separator e.g. 
        ///     it can be `c:Documents` i.e. relative to current working directory and not absolute but it's still 
        ///     considered rooted like `c:\user\my\Documents`)
        ///     - On both platforms if it starts with `\\` (i.e. UNC path);
        ///     - On both platforms it starts with a protocol in the form of `protocolName://` or `protocolName:\\`
        /// This function allows mixing the two separators.
        /// </summary>
        public static bool IsPathRooted(this string path)
        {
            // Null check
            if (string.IsNullOrWhiteSpace(path)) return false;
            // Relative check
            else if (path[0] == '.')
                return false;
            // Linux root
            else if (path[0] == '/')
                return true;
            // Windows root
            else if (path.Length > 1 && path[1] == ':')
                return true;
            // UNC root
            else if (path.Length > 1 && path[0] == '\\' && path[1] == '\\')
                return true;
            // Check protocol
            else if (path.IsPathContainProtocol())
                return true;
            else
                return false;
        }
        /// <summary>
        /// Test whether a character is folder seperator
        /// </summary>
        public static bool IsSeparator(this char c)
            => c == '/' || c == '\\';
        /// <summary>
        /// Get path without protocol
        /// </summary>
        public static string GetRealPath(this string path)
        {
            string root = path.GetPathProtocol();
            if (root != null)
                return path.Substring(root.Length);
            else
                return path;
        }
        /// <summary>
        /// Get path without protocol then get root of that path
        /// </summary>
        public static string GetRealPathRoot(this string path)
            => path.GetRealPath().GetPathRoot();
        /// <summary>
        /// Given arbitary string representing a path, assuming all folder names are valid,
        /// get the part of string that represents an arbitary filename which main contain
        /// invalid FS characters; If no such a filename is found, empty string is returned
        /// </summary>
        public static string GetRealFilename(this string path)
        {
            string realPath = path.GetRealPath();
            string realRoot = realPath.GetPathRoot();
            string remaining = realPath.Substring(realRoot?.Length ?? 0);

            // Enumerate
            int lastIndex = remaining.Length - 1;
            bool folderState = true;
            int lastFolderEndingIndex = -1;
            string lastFoldername = string.Empty;
            string realFilename = string.Empty;
            for (int i = 0; i < remaining.Length; i++)
            {
                char c = remaining[i];
                // We are still inside valid folder name paths
                if (folderState)
                {
                    if (c.IsSeparator())
                    {
                        lastFolderEndingIndex = i;
                        lastFoldername = string.Empty;
                    }
                    else if (c.IsInvalidFilenameCharacter(i == lastIndex))
                    {
                        folderState = false;
                        i = lastFolderEndingIndex;
                    }
                    else
                        lastFoldername += c;
                }
                // We are gathering filename
                else
                    realFilename += c;
            }
            // Check for the case when no invalid file names are encountered and the last captured
            // foldername is actually filename
            if (!path.Last().IsSeparator() && folderState)
                return lastFoldername;
            // In the end if it's pure folder path (i.e. path ending with folder separator and 
            // doesn't contain a file name), we will return empty
            else
                return realFilename;
        }
    }
}

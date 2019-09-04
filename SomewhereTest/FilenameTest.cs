using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Somewhere;

namespace SomewhereTest
{
    /// <summary>
    /// A suite of tests dedicated to handle filename cases
    /// </summary>
    public class FilenameTest
    {
        /// <summary>
        /// When changing the name of a managed file to include folder path, it should create proper subfolder hierarchies for the file
        /// </summary>
        [Fact]
        public void MoveShouldCreateSubfolder()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("test.txt");
            Commands.Add("test.txt");
            Commands.MV("test.txt", "folder1/folder2\\test*.txt");   // Name containing invalid characters
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

    }
}

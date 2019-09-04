using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Somewhere;
using System.IO;

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
            Assert.NotNull(Commands.GetFileID("folder1/folder2\\test*.txt"));
            Assert.True(Directory.Exists(Helper.GetFolderPath("folder1")));
            Assert.True(Directory.Exists(Path.Combine(Helper.GetFolderPath("folder1"), "folder2")));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

    }
}

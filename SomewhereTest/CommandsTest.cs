using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Somewhere;
using System.IO;
using System.Linq;

namespace SomewhereTest
{
    /// <summary>
    /// Tests for additional non-destructive (i.e. less dangerous) commands
    /// </summary>
    public class CommandsTest
    {
        [Fact]
        public void CfCommandShouldSetNewKeyValue()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            Commands.Cf("My.Property", "2019");
            Assert.Equal("2019", Commands.GetConfiguration("My.Property"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void DumpShouldDumpNotes()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            Commands.Doc("Test.txt");
            Commands.Add("Test.txt", "doc");
            Commands.Create("Note", "My note", "note");
            Commands.Dump();
            string dumpPath = Helper.GetFolderPath("Dump");
            // Validate dump direcctory
            Assert.True(Directory.Exists(dumpPath));
            // Validate notes
            Assert.Single(Directory.EnumerateFiles(dumpPath));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void DumpShouldDumpAll()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            Commands.Doc("Test.txt");
            Commands.Add("Test.txt", "doc");
            Commands.Create("Note", "My note", "note");
            Commands.Dump("all");
            string dumpPath = Helper.GetFolderPath("Dump");
            // Validate dump direcctory
            Assert.True(Directory.Exists(dumpPath));
            // Validate files
            Assert.Equal(2, Directory.EnumerateFiles(dumpPath).Count());
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
    }
}

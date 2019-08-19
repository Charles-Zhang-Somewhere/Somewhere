using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;
using Somewhere;

namespace SomewhereTest
{
    /// <summary>
    /// Running this tests using test explorer will cause tests aborted - so test then through debugging
    /// </summary>
    public class ManualTests
    {
        [Fact]
        public void FileSystemWatcherShouldAutomaticallyUpdateChangedFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            string oldFilename = "Old file.txt";
            string newFilename = "New file.txt";
            Commands.Doc(oldFilename);
            Commands.Add(oldFilename, "documentation");
            Assert.True(Commands.IsFileInDatabase(oldFilename));
            Assert.False(Commands.IsFileInDatabase(newFilename));
            File.Move(Helper.GetFilePath(oldFilename), Helper.GetFilePath(newFilename));
            // Evil but better than nothing: 
            Thread.Sleep(3000); // Give FS event some time to get handled
            Assert.False(Commands.IsFileInDatabase(oldFilename));
            Assert.True(Commands.IsFileInDatabase(newFilename));
            // throw new Exception("This unit test require check test results manually!");
        }
        [Fact]
        public void FileSystemWatcherShouldAutomaticallyUpdateChangedFolder()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            string oldFoldername = "Old Folder";
            string newFoldername = "New Folder";
            Helper.CreateEmptyFolder(oldFoldername);
            Commands.Add(oldFoldername, "important stuff");
            Assert.True(Commands.IsFileInDatabase(oldFoldername + Path.DirectorySeparatorChar));
            Assert.False(Commands.IsFileInDatabase(newFoldername + Path.DirectorySeparatorChar));
            Directory.Move(Helper.GetFilePath(oldFoldername), Helper.GetFilePath(newFoldername));
            // Evil but better than nothing: 
            Thread.Sleep(3000); // Give FS event some time to get handled
            Assert.False(Commands.IsFileInDatabase(oldFoldername + Path.DirectorySeparatorChar));
            Assert.True(Commands.IsFileInDatabase(newFoldername + Path.DirectorySeparatorChar));
            // throw new Exception("This unit test require check test results manually!");
        }
    }
}

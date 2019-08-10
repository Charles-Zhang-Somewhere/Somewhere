using Somewhere;
using System;
using System.IO;
using Xunit;

namespace SomewhereTest
{
    public class UnitTest1
    {
        [Fact]
        public void TestLocationContainsExecutable()
        {
            Assert.True(File.Exists("Somewhere.exe"));
        }

        [Fact]
        public void NewCommandGeneratesADBFile()
        {
            // Make sure we start clean
            CleanTestFolderRemoveDBFile();
            // Create and assert
            Commands.New();
            Assert.True(File.Exists(Commands.DBName));
        }

        [Fact]
        public void AddFileShouldUpdateFileCount()
        {
            CleanTestFolderRemoveDBFile();
            Commands.New(); // Create a new db
            Assert.Equal(0, Commands.FileCount);
            Commands.Add(new string[] { "Somewhere.exe" });
            Assert.Equal(1, Commands.FileCount);
        }

        [Fact]
        public void CleanTestFolderRemoveDBFile()
        {
            // Make sure we are in the test folder
            TestLocationContainsExecutable();
            // Remove DB file
            File.Delete(Commands.DBName);
        }
    }
}

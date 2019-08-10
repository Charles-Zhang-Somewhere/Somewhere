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
            CleanTestFolderRemoveDBFileDocFile();
            // Create and assert
            Commands.New();
            Assert.True(File.Exists(Commands.DBName));
        }

        [Fact]
        public void AddFileShouldUpdateFileCount()
        {
            CleanTestFolderRemoveDBFileDocFile();
            Commands.New(); // Create a new db
            Assert.Equal(0, Commands.FileCount);
            Commands.Add(new string[] { "Somewhere.exe" });
            Assert.Equal(1, Commands.FileCount);
        }

        [Fact]
        public void CleanTestFolderRemoveDBFileDocFile()
        {
            // Make sure we are in the test folder
            TestLocationContainsExecutable();
            // Remove DB file
            File.Delete(Commands.DBName);
            // Remove Doc file
            File.Delete("SomewhereDoc.txt");
        }

        [Fact]
        public void ShouldGenerateDoc()
        {
            CleanTestFolderRemoveDBFileDocFile();
            Commands.Doc();
            File.Exists("SomewhereDoc.txt");
        }

        [Fact]
        public void ShouldRunStatus()
        {
            CleanTestFolderRemoveDBFileDocFile();
            Commands.New(); // Create a new db
            foreach (var item in Commands.Status())
            {
                Console.WriteLine(item);
            }
        }

        [Fact]
        public void ShouldNotRunStatus()
        {
            CleanTestFolderRemoveDBFileDocFile();
            Commands.Status();
        }
    }
}

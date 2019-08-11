using Somewhere;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace SomewhereTest
{
    public class UnitTest1
    {
        [Fact]
        public void BaseTestLocationContainsExecutable()
        {
            Assert.True(Path.GetFileName(Directory.GetCurrentDirectory()) == "BinaryOutput" && File.Exists("Somewhere.exe"));
        }

        [Fact]
        public void NewCommandGeneratesADBFile()
        {
            // Make sure we start clean
            CleanOrCreateTestFolderRemoveAllFiles();
            // Create and assert
            Commands Commands = CreateNewCommands();
            Commands.New();
            Assert.True(TestFileExists(Commands.DBName));
        }

        [Fact]
        public void AddFileShouldUpdateFileCount()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New(); // Create a new db
            Assert.Equal(0, Commands.FileCount);
            Commands.Doc();
            Commands.Add("SomewhereDoc.txt");
            Assert.Equal(1, Commands.FileCount);
        }

        [Fact]
        public void ShouldGenerateDoc()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.Doc();
            Assert.True(TestFileExists("SomewhereDoc.txt"));
        }

        [Fact]
        public void ShouldRunStatus()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New(); // Create a new db
            foreach (var item in Commands.Status())
                Console.WriteLine(item);
        }

        [Fact]
        public void ShouldNotRunStatus()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Assert.Throws<InvalidOperationException>(() => { Commands.Status(); });
        }

        [Fact]
        public void ShouldRaiseExceptionDeleteNonExistingFile()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Assert.Throws<ArgumentException>(() => { Commands.RM("Some non existing file.extension"); });
        }

        [Fact]
        public void ShouldRaiseExceptionDeleteNonTrackedFile()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Assert.Throws<ArgumentException>(() => { Commands.RM("Somewhere.exe"); });
        }

        [Fact]
        public void ShouldDeleteFileByMarkAsDeletedInsteadOfActuallyDelete()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Commands.Add("SomewhereDoc.txt");
            Commands.RM("SomewhereDoc.txt");
            Assert.True(TestFileExists("SomewhereDoc.txt_deleted"));
        }

        [Fact]
        public void ShouldForceDeleteFile()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Commands.Add("SomewhereDoc.txt");
            Assert.True(TestFileExists("SomewhereDoc.txt"));
            Commands.RM("SomewhereDoc.txt", "-f");
            Assert.True(!TestFileExists("SomewhereDoc.txt"));
        }

        [Fact]
        public void RenameShouldUpdatePhysicalFile()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Commands.Add("SomewhereDoc.txt");
            Assert.True(TestFileExists("SomewhereDoc.txt"));
            Commands.MV("SomewhereDoc.txt", "SomewhereDocNew.txt");
            Assert.True(!TestFileExists("SomewhereDoc.txt"));
            Assert.True(TestFileExists("SomewhereDocNew.txt"));
            Assert.Equal(1, Commands.FileCount);
        }

        [Fact]
        public void RenameShouldNotWorkIfFileIsNotManaged()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Assert.True(TestFileExists("SomewhereDoc.txt"));
            Assert.Throws<InvalidOperationException>(()=> { Commands.MV("SomewhereDoc.txt", "SomewhereDocNew.txt"); });
        }

        #region Subroutines
        private void CleanOrCreateTestFolderRemoveAllFiles([CallerMemberName] string testFolderName = null)
        {
            // Make sure we (the runtime) are in the test folder
            BaseTestLocationContainsExecutable();

            if (Directory.Exists(testFolderName))
                Directory.Delete(testFolderName, true);
            Directory.CreateDirectory(testFolderName);
        }
        /// <summary>
        /// Check whether a given test file exist in test folder
        /// </summary>
        private bool TestFileExists(string fileName, [CallerMemberName] string testFolderName = null)
            => File.Exists(Path.Combine(testFolderName, fileName));
        private Commands CreateNewCommands([CallerMemberName] string testFolderName = null)
            => new Commands(testFolderName);
        #endregion
    }
}

using Somewhere;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace SomewhereTest
{
    public class UnitTest1
    {
        #region Test Cases
        [Fact]
        public void AddFileShouldAllowAddingTags()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc("File1.txt"); // Create file for test
            Commands.Add("File1.txt", "Tag1, Tag2");    // Notice we are passing in upper case
            Assert.Empty(new string[] { "tag1", "tag2" }.Except(Commands.GetFileTags("File1.txt")));    // Notice we are comparing lower case
        }

        [Fact]
        public void AddTagToFileGetsTag()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Commands.Add("SomewhereDoc.txt");
            Commands.Tag("SomewhereDoc.txt", "MyDoc");
            Assert.Equal(1, Commands.GetTagID("mydoc")); // Tentative, we shouldn't depend on this index, but this is expected
            Assert.Equal(1, Commands.TryAddTag("mydoc")); // Tentative, we shouldn't depend on this index, but this is expected
            Assert.Equal(2, Commands.TryAddTag("mydoc2")); // Tentative, we shouldn't depend on this index, but this is expected
            Commands.Tag("SomewhereDoc.txt", "MyDoc2");
            Assert.Empty(new string[] { "mydoc", "mydoc2" }.Except(Commands.GetFileTags("SomewhereDoc.txt")));
        }

        [Fact]
        public void AddAllShouldReallyAddAllAndWithTags()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc("File1.txt"); // Create file for test
            Commands.Doc("File2.txt"); // Create file for test
            Commands.Add("*", "A Tag, Another Tag");
            Assert.Equal(2, Commands.FileCount);
            Assert.Empty(new string[] { "a tag", "another tag" }.Except(Commands.GetFileTags("File2.txt")));
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
        public void AllTagShouldReturnNumberOfTags()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New(); // Create a new db
            Assert.Empty(Commands.AllTags);
            Assert.Throws<ArgumentException>(()=> { Commands.Add("SomewhereDoc.txt"); });
            Commands.Doc(); // Create test file
            Assert.Empty(Commands.AllTags);
            Commands.Add("SomewhereDoc.txt", "MyTag");
            Assert.Single(Commands.AllTags);
        }

        [Fact]
        public void BaseTestLocationContainsExecutable()
        {
            Assert.True(Path.GetFileName(Directory.GetCurrentDirectory()) == "BinaryOutput" && File.Exists("Somewhere.exe"));
        }

        [Fact]
        public void CreateVirtualFileShouldNotAffectDiskFile()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Create("My Note", "Initial Content", "my tag");            
            Assert.True(!TestFileExists("My Note"));
        }

        [Fact]
        public void LogShouldIncreseCount()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc();
            Commands.Help();    // Help doesn't count as log entry
            Commands.Add("SomewhereDoc.txt");
            // Above commands don't automatically log
            Commands.AddLog("This is a log."); // Log doesn't happen on the command level, it should be called by caller of commands
            Assert.Equal(1, Commands.LogCount);
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
        public void ShouldBeAbleToAddTagToVirtualFile()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Create("My Note", "Initial Content", "my tag");
            Commands.Tag("My Note", "Some tag");
            Assert.Empty(new string[] { "my tag", "some tag" }.Except(Commands.GetFileTags("My Note")));
        }

        [Fact]
        public void PerformanceTest()
        {
            // Configurations
            int testSetSelector = 1;
            List<Tuple<int, int, int, int>> testSets = new List<Tuple<int, int, int, int>>()
            {
                // File count, tag count, note count, file tag count
                new Tuple<int, int, int, int>(50000, 1000, 3000, 10),   // Takes around 4 min including deleting files
                new Tuple<int, int, int, int>(5000, 100, 300, 10)   // Currently using
            };
            int fileCount = testSets[testSetSelector].Item1;
            int tagCount = testSets[testSetSelector].Item2;
            int noteCount = testSets[testSetSelector].Item3;
            int fileTagCount = testSets[testSetSelector].Item4;
            Random rand = new Random();

            CleanOrCreateTestFolderRemoveAllFiles();
            // Generate tags and filenames
            List<string> tags = Enumerable.Range(1, tagCount).Select(i => Guid.NewGuid().ToString()).ToList();
            List<string> names = Enumerable.Range(1, fileCount).Select(i => Guid.NewGuid().ToString()).ToList();
            // Generate database
            Commands Commands = CreateNewCommands();
            Commands.New();
            // Add virtual notes
            string[] tempTag = new string[] { "virtual notes" };
            Commands.AddFiles(Enumerable.Range(1, noteCount).ToDictionary(i => $"My Note{i}", i => "Initial Content..."));
            Commands.AddTagsToFiles(Enumerable.Range(1, noteCount).ToDictionary(i => $"My Note{i}", i => tempTag));
            // Add physicla files
            names.ForEach(f => File.Create(GetFilePath(f)).Dispose());
            Commands.AddFiles(names);
            // Assign tags
            Commands.AddTagsToFiles(names.ToDictionary( n => n, n => Enumerable.Range(1, rand.Next(fileTagCount)).Select(i => rand.Next(tagCount)).Select(i => tags[i]).ToArray()));

            // Assertions
            Assert.Equal(fileCount + noteCount, Commands.FileCount);
            Assert.True(Commands.TagCount >= fileTagCount);
            Assert.Equal(noteCount, Commands.NoteCount);
        }

        [Fact]
        public void ShouldBeAbleToRenameVirtualFile()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Create("My Note", "Initial Content", "my tag");
            Commands.Tag("My Note", "Some tag");
            Commands.MV("My Note", "Yo Neki");
            Assert.Empty(new string[] { }.Except(Commands.GetFileTags("My Note")));  // Non existing
            Assert.Empty(new string[] { "my tag", "some tag" }.Except(Commands.GetFileTags("Yo Neki")));
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
        public void RenameFileShouldUpdatePhysicalFile()
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
        public void RenameFileShouldNotWorkIfFileIsNotManaged()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Assert.True(TestFileExists("SomewhereDoc.txt"));
            Assert.Throws<InvalidOperationException>(() => { Commands.MV("SomewhereDoc.txt", "SomewhereDocNew.txt"); });
        }

        [Fact]
        public void RenameTagShouldNotWorkIfTagDoesNotExist()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Assert.Equal(0, Commands.TagCount);
            Assert.Throws<InvalidOperationException>(() => { Commands.MVT("my tag", "my new tag"); });
        }

        [Fact]
        public void RemoveTagShouldActuallyRemoveTheTag()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc("file1.txt"); // Create test file
            Commands.Add("file1.txt", "tag1");
            Commands.Doc("file2.txt"); // Create test file
            Commands.Add("file2.txt", "tag2");
            Assert.Equal(2, Commands.TagCount);
            Commands.RMT("tag1");
            Assert.Equal(1, Commands.TagCount);
            Assert.Empty(new string[] { }.Except(Commands.GetFileTags("file1.txt")));
            Assert.Empty(new string[] { }.Except(Commands.GetFileTags("file2.txt")));
        }

        [Fact]
        public void RemoveTagShouldBeAbleToRemoveMoreThanOneTag()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc("file1.txt"); // Create test file
            Commands.Add("file1.txt", "tag1");
            Commands.Doc("file2.txt"); // Create test file
            Commands.Add("file2.txt", "tag2");
            Assert.Equal(2, Commands.TagCount);
            Commands.RMT("tag1, tag2");
            Assert.Equal(0, Commands.TagCount);
            Assert.Empty(new string[] { }.Except(Commands.GetFileTags("file1.txt")));
            Assert.Empty(new string[] { }.Except(Commands.GetFileTags("file2.txt")));
        }

        [Fact]
        public void RenameTagShouldMergeTags()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc("file1.txt"); // Create test file
            Commands.Add("file1.txt", "tag1");
            Commands.Doc("file2.txt"); // Create test file
            Commands.Add("file2.txt", "tag2");
            Assert.Equal(2, Commands.TagCount);
            Commands.MVT("tag1", "tag2");
            Assert.Equal(1, Commands.TagCount);
        }

        [Fact]
        public void RenameTagShouldRenameTag()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc("file1.txt"); // Create test file
            Commands.Add("file1.txt", "tag1");
            Commands.MVT("tag1", "tag2");
            Assert.Empty(new string[] { "tag2" }.Except(Commands.GetFileTags("file1.txt")));
            Assert.Equal(1, Commands.TagCount);
        }

        [Fact]
        public void UntagAFileRemovesTagsOnIt()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc("File1.txt"); // Create file for test
            Commands.Add("*", "A Tag, Another Tag, One More Tag");
            Assert.Equal(1, Commands.FileCount);
            Assert.Equal(3, Commands.GetFileTags("File1.txt").Length);
            Commands.Untag("File1.txt", "A Tag, One More Tag, Nonexisting Tag");
            Assert.Empty(new string[] { "another tag" }.Except(Commands.GetFileTags("File1.txt")));
        }

        [Fact]
        public void UpdateFileTagsShouldSilentlyHandleExistingTags()
        {
            CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = CreateNewCommands();
            Commands.New();
            Commands.Doc("File1.txt"); // Create file for test
            Commands.Doc("File2.txt"); // Create file for test
            Commands.Add("File1.txt");
            Commands.Add("File2.txt");
            Commands.Tag("File1.txt", "Tag1");
            Commands.Tag("File2.txt", "Tag1, Tag2");
            Assert.Empty(new string[] { "tag1", "tag2" }.Except(Commands.GetFileTags("File2.txt")));
        }
        #endregion

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
        private string GetFilePath(string fileName, [CallerMemberName] string testFolderName = null)
            => Path.Combine(testFolderName, fileName);
        private Commands CreateNewCommands([CallerMemberName] string testFolderName = null)
            => new Commands(testFolderName);
        #endregion
    }
}

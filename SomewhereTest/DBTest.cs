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
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("File1.txt"); // Create file for test
            Commands.Add("File1.txt", "Tag1, Tag2");    // Notice we are passing in upper case
            Assert.Empty(new string[] { "tag1", "tag2" }.Except(Commands.GetFileTags("File1.txt")));    // Notice we are comparing lower case
        }

        [Fact]
        public void AddTagToFileGetsTag()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
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
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
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
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            Assert.Equal(0, Commands.FileCount);
            Commands.Doc();
            Commands.Add("SomewhereDoc.txt");
            Assert.Equal(1, Commands.FileCount);
        }

        [Fact]
        public void AllTagShouldReturnNumberOfTags()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            Assert.Empty(Commands.AllTags);
            Assert.Throws<ArgumentException>(()=> { Commands.Add("SomewhereDoc.txt"); });
            Commands.Doc(); // Create test file
            Assert.Empty(Commands.AllTags);
            Commands.Add("SomewhereDoc.txt", "MyTag");
            Assert.Single(Commands.AllTags);
        }
        [Fact]
        public void AllowCreateKnowledgeItem()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            Commands.Create("", "My knowledge", "Some subject");
            Assert.Equal(1, Commands.KnowledgeCount);
        }
        [Fact]
        public void CreateVirtualFileShouldNotAffectDiskFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Create("My Note", "Initial Content", "my tag");            
            Assert.True(!Helper.TestFileExists("My Note"));
        }
        [Fact]
        public void GetPhysicalNameShouldWorkForVirtualNotes()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Create("My Note", "Initial Content", "my tag");
            Assert.True(!Helper.TestFileExists("My Note"));
            // Should not throw exception
            Commands.GetPhysicalName("My Note");
        }
        [Fact]
        public void GetPhysicalNameShouldNotRaiseExceptionsForUnmanagedFiles()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("File.txt");
            Assert.True(Helper.TestFileExists("File.txt"));
            // This is pure string function so it should just run fine
            Commands.GetPhysicalName("File.txt");
        }
        [Fact]
        public void GetPhysicalNameShouldProperlyEscapeSpecialCharacters()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("File.txt");
            Assert.Equal("Hello _World_.txt", Commands.GetPhysicalName("Hello \"World\".txt"));
        }
        [Fact]
        public void GetPhysicalNameShouldProperlyHandleLongNames()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("File.txt");
            Commands.Add("File.txt");
            string longItemName = @"This is /my file/ in Somewhere 
so I should be able to do <anything> I want with it \\including ""!!!!????**********""
This is /my file/ in Somewhere 
so I should be able to do <anything> I want with it \\including ""!!!!????**********""
This is /my file/ in Somewhere 
so I should be able to do <anything> I want with it \\including ""!!!!????**********""
This is /my file/ in Somewhere 
so I should be able to do <anything> I want with it \\including ""!!!!????**********""
This is /my file/ in Somewhere 
so I should be able to do <anything> I want with it \\including ""!!!!????**********""
This is /my file/ in Somewhere 
so I should be able to do <anything> I want with it \\including ""!!!!????**********""
This is /my file/ in Somewhere 
so I should be able to do <anything> I want with it \\including ""!!!!????**********""
This is /my file/ in Somewhere 
so I should be able to do <anything> I want with it \\including ""!!!!????**********""
This is /my file/ in Somewhere 
so I should be able to do <anything> I want with it \\including ""!!!!????**********""
This is /my file/ in Somewhere 
so I should be able to do <anything> I want with it \\including ""!!!!????**********"".txt";
            Commands.MoveFileInHomeFolder(Commands.GetFileID("File.txt").Value, "File.txt", longItemName);
            string escapedName = // A simple very specific logic for expected properly formatted name string
                longItemName.Substring(0, 260 - 1 /* Trailing null */ - Directory.GetCurrentDirectory().Length)
                .Replace('/', '_')
                .Replace('<', '_')
                .Replace('>', '_')
                .Replace('\\', '_')
                .Replace('"', '_')
                .Replace('?', '_')
                .Replace('*', '_')
                .Replace('\r', '_')
                .Replace('\n', '_');
            string escaped = Commands.GetPhysicalName(longItemName);
            Assert.True(escapedName.IndexOf(escaped.Substring(0, escaped.Length - "...txt".Length)) == 0);
            Assert.True(Helper.TestFileExists(escaped));
        }

        [Fact]
        public void GetNewPhysicalNameShouldProperlyHandleNameCollisions()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("File.txt");
            Commands.Doc("File_.txt");
            Assert.Equal("File_#1.txt", Commands.GetNewPhysicalName("File*.txt", 1));
        }

        [Fact]
        public void LogShouldIncreseCount()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc();
            Commands.Help();    // Help doesn't count as log entry
            Commands.Add("SomewhereDoc.txt");
            // Above commands don't automatically log
            Commands.AddLog("This is a log."); // Log doesn't happen on the command level, it should be called by caller of commands
            Assert.Equal(1, Commands.LogCount);
        }
        [Fact]
        public void MoveFileInHomeFolderShouldHandleInvalidCharacterEscape()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("test.txt");
            Commands.Add("test.txt");
            Commands.MoveFileInHomeFolder(1 /* Expected ID for new DB */, "test.txt", "test*.txt");
            Commands.MoveFileInHomeFolder(1, "test*.txt", "test*//WOW.");
            Assert.True(Helper.TestFileExists("test___WOW"));  // Notice this is not user level detail but is documented and standardized
        }
        [Fact]
        public void MoveFileInHomeFolderShouldNotWorkForUnmanagedFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("test.txt");
            Assert.Throws<InvalidOperationException>(() => Commands.MV("test.txt", "test*.txt"));
        }
        [Fact]
        public void MoveFileInHomeFolderShouldHandleNameCollisionGracefully()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("test.txt");
            Commands.Add("test.txt");
            Commands.Doc("test_.txt"); // Represent a manually added, non-managed file which can collide with managed file later
            Commands.MoveFileInHomeFolder(Commands.GetFileID("test.txt").Value, "test.txt", "test*.txt");
            Assert.True(Helper.TestFileExists("test_#1.txt"));  // Notice this is not user level detail but is documented and standardized
        }
        [Fact]
        public void NewCommandGeneratesADBFile()
        {
            // Make sure we start clean
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            // Create and assert
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Assert.True(Helper.TestFileExists(Commands.DBName));
        }

        [Fact]
        public void ShouldBeAbleToAddTagToVirtualFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
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

            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            // Generate tags and filenames
            List<string> tags = Enumerable.Range(1, tagCount).Select(i => Guid.NewGuid().ToString()).ToList();
            List<string> names = Enumerable.Range(1, fileCount).Select(i => Guid.NewGuid().ToString()).ToList();
            // Generate database
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            // Add virtual notes
            string[] tempTag = new string[] { "virtual notes" };
            Commands.AddFiles(Enumerable.Range(1, noteCount).ToDictionary(i => $"My Note{i}", i => "Initial Content..."));
            Commands.AddTagsToFiles(Enumerable.Range(1, noteCount).ToDictionary(i => $"My Note{i}", i => tempTag));
            // Add physicla files
            names.ForEach(f => File.Create(Helper.GetFilePath(f)).Dispose());
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
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
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
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.Doc();
            Assert.True(Helper.TestFileExists("SomewhereDoc.txt"));
        }

        [Fact]
        public void ShouldRunStatus()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            foreach (var item in Commands.Status())
                Console.WriteLine(item);
        }

        [Fact]
        public void ShouldNotRunStatus()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Assert.Throws<InvalidOperationException>(() => { Commands.Status(); });
        }

        [Fact]
        public void ShouldRaiseExceptionDeleteNonExistingFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Assert.Throws<ArgumentException>(() => { Commands.RM("Some non existing file.extension"); });
        }

        [Fact]
        public void ShouldRaiseExceptionDeleteNonTrackedFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Assert.Throws<ArgumentException>(() => { Commands.RM("Somewhere.exe"); });
        }

        [Fact]
        public void ShouldDeleteFileByMarkAsDeletedInsteadOfActuallyDelete()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Commands.Add("SomewhereDoc.txt");
            Commands.RM("SomewhereDoc.txt");
            Assert.True(Helper.TestFileExists("SomewhereDoc.txt_deleted"));
        }

        [Fact]
        public void ShouldForceDeleteFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Commands.Add("SomewhereDoc.txt");
            Assert.True(Helper.TestFileExists("SomewhereDoc.txt"));
            Commands.RM("SomewhereDoc.txt", "-f");
            Assert.True(!Helper.TestFileExists("SomewhereDoc.txt"));
        }

        [Fact]
        public void RenameFileShouldUpdatePhysicalFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Commands.Add("SomewhereDoc.txt");
            Assert.True(Helper.TestFileExists("SomewhereDoc.txt"));
            Commands.MV("SomewhereDoc.txt", "SomewhereDocNew.txt");
            Assert.True(!Helper.TestFileExists("SomewhereDoc.txt"));
            Assert.True(Helper.TestFileExists("SomewhereDocNew.txt"));
            Assert.Equal(1, Commands.FileCount);
        }

        [Fact]
        public void RenameFileShouldNotWorkIfFileIsNotManaged()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Assert.True(Helper.TestFileExists("SomewhereDoc.txt"));
            Assert.Throws<InvalidOperationException>(() => { Commands.MV("SomewhereDoc.txt", "SomewhereDocNew.txt"); });
        }

        [Fact]
        public void RenameTagShouldNotWorkIfTagDoesNotExist()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc(); // Create a doc file for test
            Assert.Equal(0, Commands.TagCount);
            Assert.Throws<InvalidOperationException>(() => { Commands.MVT("my tag", "my new tag"); });
        }

        [Fact]
        public void RemoveTagShouldActuallyRemoveTheTag()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
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
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
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
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
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
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
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
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
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
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
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
    }
}

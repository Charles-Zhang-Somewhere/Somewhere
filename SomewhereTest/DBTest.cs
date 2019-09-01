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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        }
        [Fact]
        public void AddFileShouldCopyForeignFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            if (!File.Exists("AddFileShouldCutForeignFileTempFile.txt"))
            {
                Commands.Doc("File.txt"); // Create file for test
                File.Move(Helper.GetFilePath("File.txt"), "AddFileShouldCutForeignFileTempFile.txt");
            }
            string fullPath = Path.GetFullPath("AddFileShouldCutForeignFileTempFile.txt");
            Assert.True(File.Exists(fullPath));
            Commands.Add(fullPath, "Tag1, Tag2");    // Add using full path for foreign file
            Assert.True(File.Exists(fullPath));
            // Assert added name is relative
            Assert.Equal("AddFileShouldCutForeignFileTempFile.txt", Commands.GetAllFiles().First().Name);
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void AddFileShouldThrowExceptionForNonExistingForeignFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            string nonExistingFilePath = Path.GetFullPath("NonExistingBecauseISaidSo");
            Assert.Throws<ArgumentException>(()=> Commands.Add(nonExistingFilePath, "Tag1, Tag2"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void AddFileShouldCutForeignFolder()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            // Create a folder outside test folder inside the output binary folder
            string folderPath = Path.GetFullPath("TempFolder");
            if(!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            // Add a test file to that folder
            string filePath = Path.Combine(folderPath, "TestFile.txt");
            File.Create(filePath).Dispose();
            Assert.True(Directory.Exists(folderPath));
            Assert.True(File.Exists(filePath));
            // Add to home
            Commands.Add(folderPath);
            // Assert old folder and file no longer exist
            Assert.True(!Directory.Exists(folderPath));
            Assert.True(!File.Exists(filePath));
            // Assert new folder and file created
            Assert.True(Directory.Exists(Helper.GetFilePath("TempFolder") /* The returned path can be used as a folder */));
            Assert.True(File.Exists(Helper.GetFilePath(Path.Combine("TempFolder", "TestFile.txt"))));
            // Assert added name is a folder
            Assert.Equal("TempFolder" + Path.DirectorySeparatorChar, Commands.GetAllFolders().First().Name);
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

        [Fact]
        public void AddFileShoudSaveRelativePath()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("File.txt"); // Create file for test
            Commands.Add(Path.GetFullPath(Helper.GetFilePath("File.txt")), "A Tag, Another Tag");
            Assert.Equal(1, Commands.FileCount);
            Assert.Equal("File.txt", Commands.GetAllFiles().Single().Name);
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

        [Fact]
        public void AllTagShouldReturnNumberOfTags()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            Assert.Empty(Commands.AllTags);
            // Use long filename to avoid file already exist in parent folder which is a different exception
            Assert.Throws<ArgumentException>(()=> { Commands.Add("SomewhereDoc-AllTagShouldReturnNumberOfTags.txt"); });   // File doesn't exit in home
            Commands.Doc("SomewhereDoc-AllTagShouldReturnNumberOfTags.txt"); // Create test file
            Assert.Empty(Commands.AllTags);
            Commands.Add("SomewhereDoc-AllTagShouldReturnNumberOfTags.txt", "MyTag");
            Assert.Single(Commands.AllTags);
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void AllowCreateKnowledgeItem()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            Commands.Create("", "My knowledge", "Some subject");
            Assert.Equal(1, Commands.KnowledgeCount);
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void GetFileDetailsReturnNoteItemContent()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            Commands.Create("My Note", "My Content", "My Tab");
            Assert.Equal(1, Commands.NoteCount);
            Assert.Equal("My Content", Commands.GetFileDetail(1).Content);
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void ConfigurationTestGetValue()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Assert.NotNull(Commands.GetConfiguration("InitialVersion"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void ConfigurationTestGetNonExistingValue()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Assert.Null(Commands.GetConfiguration("VersionNOTEXISTING"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void ConfigurationTestUpdateValue()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.SetConfiguration("Version", "15");
            Assert.Equal("15", Commands.GetConfiguration("Version"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void ConfigurationTestCreateValue()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.SetConfiguration("VersionNew", "15");
            Assert.Equal("15", Commands.GetConfiguration("VersionNew"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void CreateVirtualFileShouldNotAffectDiskFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Create("My Note", "Initial Content", "my tag");            
            Assert.True(!Helper.TestFileExists("My Note"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void GetPhysicalNameShouldProperlyEscapeSpecialCharacters()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("File.txt");
            Assert.Equal("Hello _World_.txt", Commands.GetPhysicalName("Hello \"World\".txt"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            Commands.AddLog("Test", "This is a log."); // Log doesn't happen on the command level, it should be called by caller of commands
            Assert.Equal(1, Commands.LogCount);
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void MoveFileInHomeFolderShouldNotWorkForUnmanagedFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("test.txt");
            Assert.Throws<InvalidOperationException>(() => Commands.MV("test.txt", "test*.txt"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void SetMetaShoudWork()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("File.txt");
            Commands.Add("File.txt", "documentation");
            Commands.SetItemMeta("File.txt", "Remark", "My file is coolest.");
            Assert.Equal("My file is coolest.", Commands.GetItemMeta("File.txt", "Remark"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void SetMetaCommandShoudWork()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("File.txt");
            Commands.Add("File.txt", "documentation");
            Commands.Mt("File.txt", "Remark", "My file is coolest.");
            Assert.Equal("My file is coolest.", Commands.GetItemMeta("File.txt", "Remark"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

        [Fact]
        public void ShouldGenerateDoc()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.Doc();
            Assert.True(Helper.TestFileExists("SomewhereDoc.txt"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

        [Fact]
        public void ShouldRunStatus()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            foreach (var item in Commands.Status())
                Console.WriteLine(item);
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

        [Fact]
        public void ShouldNotRunStatus()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Assert.Throws<InvalidOperationException>(() => { Commands.Status(); });
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

        [Fact]
        public void ShouldRaiseExceptionDeleteNonExistingFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Assert.Throws<ArgumentException>(() => { Commands.RM("Some non existing file.extension"); });
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

        [Fact]
        public void ShouldRaiseExceptionDeleteNonTrackedFile()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Assert.Throws<ArgumentException>(() => { Commands.RM("Somewhere.exe"); });
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void PurgeShouldPurge()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            // Create repository and create test files
            Commands.New();
            Commands.Doc("Test1.txt");
            Commands.Doc("Test2.txt");
            Commands.Add("Test1.txt");
            Commands.Add("Test2.txt");
            // Validate added files
            Assert.True(Helper.TestFileExists("Test1.txt"));
            Assert.True(Helper.TestFileExists("Test2.txt"));
            Assert.Equal(2, Commands.FileCount);
            // Test simple deleting
            Commands.RM("Test1.txt", "-f"); // Force delete
            Commands.RM("Test2.txt");
            Assert.False(Helper.TestFileExists("Test1.txt"));
            Assert.False(Helper.TestFileExists("Test1.txt_deleted"));
            Assert.False(Helper.TestFileExists("Test2.txt"));
            Assert.True(Helper.TestFileExists("Test2.txt_deleted"));
            Assert.Equal(0, Commands.FileCount);
            // Test purging
            Commands.Purge("-f");
            Assert.False(Helper.TestFileExists("Test2.txt_deleted"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
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
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

        [Fact]
        public void UpdateTagsShouldRemoveOldOnesAndAddNewOnes()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Doc("File.txt"); // Create file for test
            Commands.Add("File.txt");
            Commands.Tag("File.txt", "Tag1");
            Commands.Tag("File.txt", "Tag1, Tag2");
            Assert.Equal(2, Commands.TagCount);
            Commands.Update("File.txt", "Tag3, Tag4");
            Assert.Equal(2, Commands.TagCount);
            Assert.Empty(new string[] { "tag3", "tag4" }.Except(Commands.GetFileTags("File.txt")));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        #endregion
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Somewhere;
using System.IO;
using System.Linq;
using StringHelper;

namespace SomewhereTest
{
    public class ImportExportTest
    {
        [Fact]
        public void ImportExternalFolderShouldMakeCopy()
        {
            void InitializeExternalTestFiles(string dir, Commands commands)
            {
                if (!Directory.Exists(dir)) // Unit test working directory
                {
                    Directory.CreateDirectory(dir);
                    Directory.CreateDirectory(Path.Combine(dir, "subFolder"));
                    commands.Doc("File.txt"); // Create file for test
                    commands.Doc("File2.txt"); // Create file for test
                    commands.Doc("File3.txt"); // Create file for test
                    
                    // Move from Home to external path
                    File.Move(Helper.GetFilePath("File.txt"), Path.Combine(dir, "File.txt"));
                    File.Move(Helper.GetFilePath("File2.txt"), Path.Combine(dir, "File2.txt"));
                    File.Move(Helper.GetFilePath("File3.txt"), Path.Combine(dir, "subFolder", "File3.txt"));
                }
            }
            void ValidateTestFileExistence(string dir, bool flattened = false)
            {
                string file1 = Path.Combine(dir, "File.txt");
                string file2 = Path.Combine(dir, "File2.txt");
                string file3 = flattened 
                    ? Path.Combine(dir, "File3.txt")
                    : Path.Combine(dir, "subFolder", "File3.txt");
                Assert.True(File.Exists(Path.GetFullPath(file1)));
                Assert.True(File.Exists(Path.GetFullPath(file2)));
                Assert.True(File.Exists(Path.GetFullPath(file3)));
            }

            // Create commands object
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            // Initialize external folder
            string externalDir = "ImportExternalFolderShouldMakeCopyExternal";  // Inside working directory
            InitializeExternalTestFiles(externalDir, Commands);
            // Validate file existences independently inside external folder
            ValidateTestFileExistence(externalDir);
            // Perform import operation
            Commands.Im(Path.GetFullPath(externalDir)); // Add using full path for foreign directory
            // Validate external files still exist
            ValidateTestFileExistence(externalDir);
            // Validate files are copied to interal folder, flattned
            ValidateTestFileExistence(Commands.HomeDirectory, true);
            // Assert added name is relative
            Assert.Empty(new string[] { "File.txt", "File2.txt", "File3.txt" }.Except(
                Commands.GetAllFiles().Select(f => f.Name)));
            // Assert tags
            var expectedTags = Path.GetFullPath(Path.Combine(externalDir, "subFolder")).SplitDirectoryAsTags();
            var realTags = Commands.GetAllTags().Select(t => t.Name);
            Assert.Empty(expectedTags.Except(realTags));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
            Helper.CleanTestFolderRemoveAllFiles(externalDir);
        }
        [Fact]
        public void ImportExternalFolderShouldHandleConflictNameByDontChangeFiles()
        {
            void InitializeExternalTestFiles(string dir, Commands commands)
            {
                if (!Directory.Exists(dir)) // Unit test working directory
                {
                    Directory.CreateDirectory(dir);
                    Directory.CreateDirectory(Path.Combine(dir, "subFolder"));
                    commands.Doc("File.txt"); // Create file for test
                    commands.Doc("File2.txt"); // Create file for test
                    commands.Doc("File3.txt"); // Create file for test

                    // Move from Home to external path
                    File.Move(Helper.GetFilePath("File.txt"), Path.Combine(dir, "File.txt"));
                    File.Move(Helper.GetFilePath("File2.txt"), Path.Combine(dir, "File2.txt"));
                    File.Move(Helper.GetFilePath("File3.txt"), Path.Combine(dir, "subFolder", "File3.txt"));
                }
            }
            void ValidateTestFileExistence(string dir, bool flattened = false)
            {
                string file1 = Path.Combine(dir, "File.txt");
                string file2 = Path.Combine(dir, "File2.txt");
                string file3 = flattened
                    ? Path.Combine(dir, "File3.txt")
                    : Path.Combine(dir, "subFolder", "File3.txt");
                Assert.True(File.Exists(Path.GetFullPath(file1)));
                Assert.True(File.Exists(Path.GetFullPath(file2)));
                Assert.True(File.Exists(Path.GetFullPath(file3)));
            }

            // Create commands object
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            // Initialize external folder
            string externalDir = "ImportExternalFolderShouldMakeCopyExternal";  // Inside working directory
            InitializeExternalTestFiles(externalDir, Commands);
            // Validate file existences independently inside external folder
            ValidateTestFileExistence(externalDir);
            // Add a potentially duplicate file name
            Commands.Doc("File.txt");
            // Perform import operation
            Commands.Im(Path.GetFullPath(externalDir)); // Add using full path for foreign directory
            // Validate external files still exist
            ValidateTestFileExistence(externalDir);
            // Validate files are copied to interal folder, flattned
            ValidateTestFileExistence(Commands.HomeDirectory, true);
            // Assert added name is relative and File.txt is not added
            Assert.Empty(new string[] { "File2.txt", "File3.txt" }.Except(
                Commands.GetAllFiles().Select(f => f.Name)));
            // Assert tags
            var expectedTags = Path.GetFullPath(Path.Combine(externalDir, "subFolder")).SplitDirectoryAsTags();
            var realTags = Commands.GetAllTags().Select(t => t.Name);
            Assert.Empty(expectedTags.Except(realTags));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
            Helper.CleanTestFolderRemoveAllFiles(externalDir);
        }
        [Fact]
        public void ImportInternalFolderShouldCut()
        {
            void InitializeInternalTestFiles(Commands commands)
            {
                string fullPath = Helper.GetFolderPath("subFolder");
                if (!Directory.Exists(fullPath)) // A subdir inside test folder
                {
                    Directory.CreateDirectory(fullPath);
                    Directory.CreateDirectory(Path.Combine(fullPath, "subFolder2")); // Another subFolder
                    commands.Doc("File.txt"); // Create file for test
                    commands.Doc("File2.txt"); // Create file for test
                    commands.Doc("File3.txt"); // Create file for test

                    // Move from Home to subDir path
                    File.Move(Helper.GetFilePath("File.txt"), Path.Combine(fullPath, "File.txt"));
                    File.Move(Helper.GetFilePath("File2.txt"), Path.Combine(fullPath, "File2.txt"));
                    File.Move(Helper.GetFilePath("File3.txt"), Path.Combine(fullPath, "subFolder2", "File3.txt"));
                }
            }
            void ValidateTestFileExistence(string path, bool assertExist = true, bool flattened = false)
            {
                string fullPath = Path.GetFullPath(path);
                string file1 = Path.Combine(fullPath, "File.txt");
                string file2 = Path.Combine(fullPath, "File2.txt");
                string file3 = flattened
                    ? Path.Combine(fullPath, "File3.txt")
                    : Path.Combine(fullPath, "subFolder2", "File3.txt");
                Assert.True(assertExist ? File.Exists(Path.GetFullPath(file1)) : !File.Exists(Path.GetFullPath(file1)));
                Assert.True(assertExist ? File.Exists(Path.GetFullPath(file2)) : !File.Exists(Path.GetFullPath(file2)));
                Assert.True(assertExist ? File.Exists(Path.GetFullPath(file3)) : !File.Exists(Path.GetFullPath(file3)));
            }

            // Create commands object
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            // Initialize internal folder
            InitializeInternalTestFiles(Commands);
            // Validate file existences independently inside external folder
            ValidateTestFileExistence(Helper.GetFolderPath("subFolder"));
            // Perform import operation
            Commands.Im(Path.GetFullPath(Helper.GetFolderPath("subFolder"))); // Test adding using full path for internal directory
            // Validate internal files no longer exist
            ValidateTestFileExistence(Helper.GetFolderPath("subFolder"), false);
            // Validate files are copied to interal folder, flattned
            ValidateTestFileExistence(Commands.HomeDirectory, true, true);
            // Assert added name is relative
            Assert.Empty(new string[] { "File.txt", "File2.txt", "File3.txt" }.Except(
                Commands.GetAllFiles().Select(f => f.Name)));
            // Assert tags
            var expectedTags = Path.GetFullPath(Helper.GetFolderPath(Path.Combine("subFolder", "subFolder2"))).SplitDirectoryAsTags();
            var actualTags = Commands.GetAllTags().Select(t => t.Name);
            Assert.Empty(expectedTags.Except(actualTags));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        [Fact]
        public void ImportInternalFolderShouldCutHandleAlreadyMangedFilesProperly()
        {
            void InitializeInternalTestFiles(Commands commands)
            {
                string fullPath = Helper.GetFolderPath("subFolder");
                if (!Directory.Exists(fullPath)) // A subdir inside test folder
                {
                    Directory.CreateDirectory(fullPath);
                    Directory.CreateDirectory(Path.Combine(fullPath, "subFolder2")); // Another subFolder
                    commands.Doc("File.txt"); // Create file for test
                    commands.Doc("File2.txt"); // Create file for test
                    commands.Doc("File3.txt"); // Create file for test

                    // Move from Home to subDir path
                    File.Move(Helper.GetFilePath("File.txt"), Path.Combine(fullPath, "File.txt"));
                    File.Move(Helper.GetFilePath("File2.txt"), Path.Combine(fullPath, "File2.txt"));
                    File.Move(Helper.GetFilePath("File3.txt"), Path.Combine(fullPath, "subFolder2", "File3.txt"));
                }
            }
            void ValidateTestFileExistence(string path, bool assertExist = true, bool flattened = false)
            {
                string fullPath = Path.GetFullPath(path);
                string file1 = Path.Combine(fullPath, "File.txt");
                string file2 = Path.Combine(fullPath, "File2.txt");
                string file3 = flattened
                    ? Path.Combine(fullPath, "File3.txt")
                    : Path.Combine(fullPath, "subFolder2", "File3.txt");
                Assert.True(assertExist ? File.Exists(Path.GetFullPath(file1)) : !File.Exists(Path.GetFullPath(file1)));
                Assert.True(assertExist ? File.Exists(Path.GetFullPath(file2)) : !File.Exists(Path.GetFullPath(file2)));
                Assert.True(assertExist ? File.Exists(Path.GetFullPath(file3)) : !File.Exists(Path.GetFullPath(file3)));
            }

            // Create commands object
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            // Initialize internal folder
            InitializeInternalTestFiles(Commands);
            // Validate file existences independently inside external folder
            ValidateTestFileExistence(Helper.GetFolderPath("subFolder"));
            // Add a file inside folder explicitly
            Commands.Add(Path.Combine("subFolder", "File.txt"));
            Assert.Single(Commands.GetAllFiles());
            // Perform import operation
            Commands.Im(Path.GetFullPath(Helper.GetFolderPath("subFolder"))); // Test adding using full path for internal directory
            // Validate internal files no longer exist
            ValidateTestFileExistence(Helper.GetFolderPath("subFolder"), false);
            // Validate files are copied to interal folder, flattned
            ValidateTestFileExistence(Commands.HomeDirectory, true, true);
            // Assert added name is relative
            Assert.Empty(new string[] { "File.txt", "File2.txt", "File3.txt" }.Except(
                Commands.GetAllFiles().Select(f => f.Name)));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
    }
}

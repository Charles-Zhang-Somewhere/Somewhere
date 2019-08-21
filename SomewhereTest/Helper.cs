using Somewhere;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace SomewhereTest
{
    /// <summary>
    /// Helper Routines
    /// </summary>
    internal static class Helper
    {
        #region Subroutines
        /// <summary>
        /// Make sure we are inside BinaryOutput folder
        /// </summary>
        public static void BaseTestLocationIsInBinaryOutputFolder()
        {
            Assert.True(Path.GetFileName(Directory.GetCurrentDirectory()) == "BinaryOutput");
        }
        /// <summary>
        /// Clean update unit test folder
        /// </summary>
        public static void CleanOrCreateTestFolderRemoveAllFiles([CallerMemberName] string unitTestFolderName = null)
        {
            // Make sure we (the runtime) are in the test folder
            BaseTestLocationIsInBinaryOutputFolder();

            if (Directory.Exists(unitTestFolderName))
                Directory.Delete(unitTestFolderName, true);
            Directory.CreateDirectory(unitTestFolderName);
        }
        /// <summary>
        /// Check whether a given test file exist in test folder
        /// </summary>
        public static bool TestFileExists(string fileName, [CallerMemberName] string unitTestFolderName = null)
            => File.Exists(Path.Combine(unitTestFolderName, fileName));
        /// <summary>
        /// Get actual path for a file relative to the unit test
        /// </summary>
        public static string GetFilePath(string fileName, [CallerMemberName] string unitTestFolderName = null)
            => Path.Combine(unitTestFolderName, fileName);
        public static string GetFolderPath(string folderName, [CallerMemberName] string unitTestFolderName = null)
            => GetFilePath(folderName, unitTestFolderName);
        /// <summary>
        /// Get full path to unit test folder
        /// </summary>
        public static string GetFolderPath([CallerMemberName] string unitTestFolderName = null)
            => Path.GetFullPath(unitTestFolderName);
        public static void CreateEmptyFile(string fileName, [CallerMemberName] string unitTestFolderName = null)
            => File.Create(Path.Combine(unitTestFolderName, fileName)).Dispose();
        public static void CreateEmptyFolder(string folderName, [CallerMemberName] string unitTestFolderName = null)
            => Directory.CreateDirectory(Path.Combine(unitTestFolderName, folderName));
        /// <summary>
        /// Initialize a new home folder for the unit test
        /// </summary>
        public static Commands CreateNewCommands([CallerMemberName] string unitTestFolderName = null)
            => new Commands(unitTestFolderName);
        #endregion
    }
}

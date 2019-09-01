using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Somewhere;
using System.Diagnostics;
using System.IO;

namespace SomewhereTest
{
    /// <summary>
    /// Tests for advanced efficient workflows
    /// </summary>
    public class InputOutputTest
    {
        /// <summary>
        /// This test asserts that, for non-interactive commands, one should be able to chain them together
        /// by exploiting mechanism of interactive mode
        /// </summary>
        /// <remarks>
        /// Notice this test requires a published version of SW/Somewhere executable in BinaryOutput folder
        /// </remarks>
        [Fact]
        public void CommandChainingShouldWork()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            // Mimic user interactive experience
            var info = new ProcessStartInfo()
            {
                FileName = "somewhere", // Notice this unit test require PATH is set so this executable can be found
                WorkingDirectory = Helper.GetFolderPath(),
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process process = Process.Start(info))
            {
                StreamWriter myStreamWriter = process.StandardInput;
                myStreamWriter.WriteLine("new");    // Create home
                myStreamWriter.WriteLine("doc Doc.txt");    // Generate test file
                myStreamWriter.WriteLine("add Doc.txt Documentation");  // Add file to repository
                myStreamWriter.WriteLine("files");  // Output some random stuff 
                // that we are not going to see anyway
                myStreamWriter.WriteLine("cf My.Test 1516");  // Set some configuration

                // End the input stream to the sort command.
                // When the stream closes, the sort command writes the sorted text lines to the console.
                myStreamWriter.Close();

                // Read the output (or the error) - in case of exception this can be used for inspection
                string output = process.StandardOutput.ReadToEnd();
                string err = process.StandardError.ReadToEnd();
                // Wait for the sort process to write the sorted text lines.
                process.WaitForExit();
                
            }
            // Verify using a new commands object
            Commands Commands = Helper.CreateNewCommands();
            Assert.Equal(1, Commands.FileCount);
            Assert.Equal(1, Commands.TagCount);
            Assert.Equal("1516", Commands.GetConfiguration("My.Test"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
    }
}

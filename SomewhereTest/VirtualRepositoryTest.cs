using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Somewhere;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SomewhereTest
{
    /// <summary>
    /// Tests for virtual repository simulations
    /// </summary>
    public class VirtualRepositoryTest
    {
        [Fact]
        public void JournalShouldTrackNameChanges()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Create("note", "content", "tags"); // Create 
            Commands.MV("note", "note1");   // Rename once
            Commands.MVT("tags", "tags2");
            Commands.MV("note1", "note2");  // Rename again

            // Dump ending state
            Commands.Dump("dump.csv");
            // Parse output csv
            string csv = File.ReadAllText(Helper.GetFilePath("dump.csv"));
            // We should have only one file after all those process
            Assert.Single(Csv.CsvReader.ReadFromText(csv));

            // Dump single file changes
            Commands.Dump("dump.csv", "csv", "note1"); // Use an intermediate version of name
            // Parse output csv
            csv = File.ReadAllText(Helper.GetFilePath("dump.csv"));
            // We should have 6 versions (initial create counts as 3)
            Assert.Equal(6, Csv.CsvReader.ReadFromText(csv).Count());
            // We should have 3 distinct names
            Assert.Equal(3, Csv.CsvReader.ReadFromText(csv)
                // Extract file names from the name column, stripping out the `(Step: N)` part
                .Select(row => Regex.Replace(row["Name"], "\\(Step: .*?\\)(.*)", "$1"))
                // Get distinct name count
                .Distinct().Count());

            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }

        [Fact]
        public void JournalShouldTrackNameChangesForDifferentEntitiesThroughoutHistory()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New();
            Commands.Create("note", "content", "tags"); // Create 
            Commands.MV("note", "note1");   // Rename once
            Commands.MVT("tags", "tags2");
            Commands.MV("note1", "note2");  // Rename again
            Commands.Create("note", "relive", "tags"); // Create the note named "note" again which is distinct from note2, also recreated tags

            // Dump ending state
            Commands.Dump("dump.csv");
            // Parse output csv
            string csv = File.ReadAllText(Helper.GetFilePath("dump.csv"));
            // We should have 2 files after all those process
            Assert.Equal(2, Csv.CsvReader.ReadFromText(csv).Count());

            // Dump single file changes
            Commands.Dump("dump.csv", "csv", "note"); // Use an initial name version of name
            // Parse output csv
            csv = File.ReadAllText(Helper.GetFilePath("dump.csv"));
            // We should have 9 versions (initial create counts as 3)
            Assert.Equal(9, Csv.CsvReader.ReadFromText(csv).Count());
            // We should have 3 distinct names
            Assert.Equal(3, Csv.CsvReader.ReadFromText(csv)
                // Extract file names from the name column, stripping out the `(Step: N)` part
                .Select(row => Regex.Replace(row["Name"], "\\(Step: .*?\\)(.*)", "$1"))
                // Get distinct name count
                .Distinct().Count());
            // We should have 2 distinct tags
            Assert.Equal(2, Csv.CsvReader.ReadFromText(csv)
                // Extract file names from the name column, stripping out the `(Step: N)` part
                .Select(row => row["Tags"])
                .Where(tag => !string.IsNullOrEmpty(tag))
                // Get distinct name count
                .Distinct().Count());

            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
    }
}

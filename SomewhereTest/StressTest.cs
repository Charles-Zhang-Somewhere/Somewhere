using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using InteropCommon;
using Somewhere;
using Xunit;

namespace SomewhereTest
{
    /// <summary>
    /// Performance related tests for ensuring software usability
    /// </summary>
    public class StressTest
    {
        [Fact]
        public void PerformanceTest()
        {
            // Configurations
            int testSetSelector = 2;
            List<Tuple<int, int, int, int, int>> testSets = new List<Tuple<int, int, int, int, int>>()
            {
                // File count, tag count, note count, knowledge count, file tag count
                new Tuple<int, int, int, int, int>(50000, 1000, 3000, 100000, 10),   // Takes around 4 min including deleting files
                new Tuple<int, int, int, int, int>(10000, 500, 1000, 50000, 10),   // Medium load
                new Tuple<int, int, int, int, int>(5000, 100, 300, 10000, 10)   // Lower load
            };
            int fileCount = testSets[testSetSelector].Item1;
            int tagCount = testSets[testSetSelector].Item2;
            int noteCount = testSets[testSetSelector].Item3;
            int knowledgeCount = testSets[testSetSelector].Item4;
            int fileTagCount = testSets[testSetSelector].Item5;
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
            Commands.AddFiles(Enumerable.Range(1, noteCount).ToTuple(i => $"My Note{i}", i => "Initial Content..."));
            // TODO: Tags for bulk tag note items; Commands.AddTagsToNotes(Enumerable.Range(1, noteCount).ToDictionary(i => $"My Note{i}", i => tempTag));
            // Add physicla files
            names.ForEach(f => File.Create(Helper.GetFilePath(f)).Dispose());
            Commands.AddFiles(names);
            // Add knowledge items
            Commands.AddFiles(Enumerable.Range(1, knowledgeCount).ToTuple(i => (string)null, i => "Initial Content..."));
            // TODO: Tags for bulk import knowledge items; Commands.AddTagsToNotes(Enumerable.Range(1, noteCount).ToDictionary(i => $"My Note{i}", i => tempTag));
            // Assign tags
            Commands.AddTagsToFiles(names.ToDictionary(n => n, n => Enumerable.Range(1, rand.Next(fileTagCount)).Select(i => rand.Next(tagCount)).Select(i => tags[i]).ToArray()));

            // Assertions
            Assert.Equal(fileCount, Commands.FileCount);
            Assert.Equal(noteCount + knowledgeCount, Commands.NoteCount);
            Assert.Equal(fileCount + noteCount + knowledgeCount, Commands.ItemCount);
            Assert.True(Commands.TagCount >= fileTagCount);
        }
    }
}

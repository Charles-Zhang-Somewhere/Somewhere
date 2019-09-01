using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Somewhere;

namespace SomewhereTest
{
    /// <summary>
    /// This suite of tests focus on validation of all file/folder/note/knowledge types of items.
    /// </summary>
    public class ItemTypeTest
    {
        #region Folder Type
        [Fact]
        public void PhysicalFolderItemShouldCountAsItemAndNotCountAsFileAndCountAsDirectory()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            // Create a folder
            Helper.CreateEmptyFolder("My folder");
            Commands.Add("My Folder");
            Assert.Equal(1, Commands.ItemCount);
            Assert.Equal(0, Commands.FileCount);
            Assert.Equal(1, Commands.FolderCount);
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        #endregion

        #region Folder Type
        [Fact]
        public void KnowledgeItemShouldCountAsNote()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            // Create a knowledge (note)
            Commands.Create(null, "Paraceratherium is an extinct genus of hornless rhinoceros, and one of the largest terrestrial mammals that has ever existed.",
                "animal_note");
            Assert.Equal(1, Commands.ItemCount);
            Assert.Equal(0, Commands.FileCount);
            Assert.Equal(1, Commands.NoteCount);
            Assert.Equal(1, Commands.KnowledgeCount);
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
        #endregion
    }
}

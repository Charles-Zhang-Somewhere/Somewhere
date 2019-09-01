using System;
using System.Collections.Generic;
using System.Text;
using Xunit;
using Somewhere;

namespace SomewhereTest
{
    /// <summary>
    /// Tests for additional non-destructive (i.e. less dangerous) commands
    /// </summary>
    public class CommandsTest
    {
        [Fact]
        public void CfCommandShouldSetNewKeyValue()
        {
            Helper.CleanOrCreateTestFolderRemoveAllFiles();
            Commands Commands = Helper.CreateNewCommands();
            Commands.New(); // Create a new db
            Commands.Cf("My.Property", "2019");
            Assert.Equal("2019", Commands.GetConfiguration("My.Property"));
            // Clean up
            Commands.Dispose();
            Helper.CleanTestFolderRemoveAllFiles();
        }
    }
}

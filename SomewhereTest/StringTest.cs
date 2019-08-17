using StringHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;

namespace SomewhereTest
{
    public class StringTest
    {
        [Fact]
        public void ParseEmptyStrings()
        {
            string[] arguments = "".BreakCommandLineArguments();
            Assert.Empty(new string[] { }.Except(arguments));

            arguments = " ".BreakCommandLineArguments();
            Assert.Empty(new string[] { }.Except(arguments));
        }

        [Fact]
        public void ParseSimpleSpaceSeperatedArguments()
        {
            string[] arguments = "a1 a2 a3".BreakCommandLineArguments();
            Assert.Empty(new string[] { "a1", "a2", "a3" }.Except(arguments));
        }

        [Fact]
        public void ParseSimpleSpaceSeperatedArgumentsWithQuotedComponents()
        {
            string[] arguments = "a1 a2 \"a long\" a3".BreakCommandLineArguments();
            Assert.Empty(new string[] { "a1", "a2", "a long", "a3" }.Except(arguments));
        }

        [Fact]
        public void ParseSimpleSpaceSeperatedArgumentsWithQuotedComponentsAndEscapedQuotes()
        {
            string[] arguments = "a1 a2 \"a long\" a3 \"he said: \"\"Don't worry, it's going to be a good day tomorrow.\"\"\"".BreakCommandLineArguments();
            Assert.Empty(new string[] { "a1", "a2", "a long", "a3", "he said: \"Don't worry, it's going to be a good day tomorrow.\"" }.Except(arguments));
        }

        [Fact]
        public void ParseCombined()
        {
            string[] arguments = "a1 \"a2\" \"\"\"Something's fishy.\"\" He said.\" a3 \"Hello World\"".BreakCommandLineArguments();
            Assert.Empty(new string[] { "a1", "a2", "\"Something's fishy.\" He said.", "a3", "Hello World" }.Except(arguments));
        }

        [Fact]
        public void SystemPathGetExtensionWorksOnMultiline()
        {
            string multiLineName = "hello\nworld.txt";
            Assert.Equal(".txt", System.IO.Path.GetExtension(multiLineName));
        }

        [Fact]
        public void SplitTags()
        {
            Assert.Empty(new string[] { "t1", "t2", "hello world", "how's your day", "_oh' my god_" }
                .Except("t1, t2, hello world, how's your day, \"oh' my god\"".SplitTags()));
        }

        [Fact]
        public void ShouldEscapeInvalidFilenames()
        {
            Assert.Equal("My_File", "My\\File".EscapeFilename());
            Assert.Equal("_______.text", "<>?|\\/*.text".EscapeFilename());
            Assert.Equal("aux_.text", "aux.text".EscapeFilename());   // windows is not case sensitive
        }
    }
}

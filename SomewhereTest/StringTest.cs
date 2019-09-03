using Somewhere;
using StringHelper;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using StringHelper;

namespace SomewhereTest
{
    public class StringTest
    {
        [Fact]
        public void ParseEmptyStrings()
        {
            string[] arguments = "".BreakCommandLineArgumentPositions();
            Assert.Empty(new string[] { }.Except(arguments));

            arguments = " ".BreakCommandLineArgumentPositions();
            Assert.Empty(new string[] { }.Except(arguments));
        }

        [Fact]
        public void ParseSimpleSpaceSeperatedArguments()
        {
            string[] arguments = "a1 a2 a3".BreakCommandLineArgumentPositions();
            Assert.Empty(new string[] { "a1", "a2", "a3" }.Except(arguments));
        }

        [Fact]
        public void ParseSimpleSpaceSeperatedArgumentsWithQuotedComponents()
        {
            string[] arguments = "a1 a2 \"a long\" a3".BreakCommandLineArgumentPositions();
            Assert.Empty(new string[] { "a1", "a2", "a long", "a3" }.Except(arguments));
        }

        [Fact]
        public void ParseSimpleSpaceSeperatedArgumentsWithQuotedComponentsAndEscapedQuotes()
        {
            string[] arguments = "a1 a2 \"a long\" a3 \"he said: \"\"Don't worry, it's going to be a good day tomorrow.\"\"\"".BreakCommandLineArgumentPositions();
            Assert.Empty(new string[] { "a1", "a2", "a long", "a3", "he said: \"Don't worry, it's going to be a good day tomorrow.\"" }.Except(arguments));
        }

        [Fact]
        public void ParseCombined()
        {
            string[] arguments = "a1 \"a2\" \"\"\"Something's fishy.\"\" He said.\" a3 \"Hello World\"".BreakCommandLineArgumentPositions();
            Assert.Empty(new string[] { "a1", "a2", "\"Something's fishy.\" He said.", "a3", "Hello World" }.Except(arguments));
        }
        [Fact]
        public void PathRootedShouldWorkEvenForPathMixingFolderSeperators()
        {
            string mixedPath1 = @"file://C:\Folder\Folder2/Folder3";
            Assert.False(Path.IsPathRooted(mixedPath1));

            // Definition of "Rooted" path per how Path.IsPathRooted() works see 
            // https://docs.microsoft.com/en-us/dotnet/api/system.io.path.ispathrooted?view=netframework-4.8
            string mixedPath2 = @"C:\Folder\Folder2/Folder3\Folder*5\File";
            Assert.True(Path.IsPathRooted(mixedPath2));
            Assert.Equal(@"C:\", Path.GetPathRoot(mixedPath2));
            Assert.Equal(@"File", Path.GetFileName(mixedPath2));    // Path.GetFileName() properly handles mixing seperators
            Assert.Equal(@"C:\Folder\Folder2\Folder3\Folder*5", Path.GetDirectoryName(mixedPath2)); // Path.GetDirectoryName() converts separator and doesn't have ending separator
        }
        [Fact]
        public void RealPathRootShouldWorkEvenForPathMixingFolderSeperators()
        {
            string mixedPath1 = @"file://C:\Folder\Fo*lder2/Folder3";
            Assert.True(mixedPath1.IsPathRooted());
            Assert.Equal(@"file://", mixedPath1.GetPathRoot());
            Assert.Equal(@"C:\", mixedPath1.GetRealPathRoot());
            Assert.Equal(@"Fo*lder2/Folder3", mixedPath1.GetRealFilename());
            Assert.Equal(@"C:/Folder/", mixedPath1.GetRealDirectoryPath('/'));
            Assert.Equal(@"C:\Folder\", mixedPath1.GetRealDirectoryPath('\\'));

            // Definition of "Rooted" path per how String.IsPathRooted() works see StringHelper
            string mixedPath2 = @"C:\Folder\Folder2/Folder3";
            Assert.True(mixedPath2.IsPathRooted());
            Assert.Equal(@"C:\", mixedPath2.GetRealPathRoot());
            Assert.Equal(@"Folder3", mixedPath2.GetRealFilename());
            Assert.Equal(@"C:\Folder\Folder2/", mixedPath2.GetRealDirectoryPath(null)); //  Default append separator is Linux style
        }
        [Fact]
        public void RealPathRootShouldHandleProtocolGenericlly()
        {
            string mixedPath1 = @"file:\\C:\Folder\Folder2/Folder3";
            Assert.True(mixedPath1.IsPathRooted());
            Assert.Equal(@"file:\\", mixedPath1.GetPathProtocol());
            Assert.Equal(@"C:\", mixedPath1.GetRealPathRoot());
            Assert.Equal(@"Folder3", mixedPath1.GetRealFilename());
            Assert.Equal(@"C:/Folder/Folder2/", mixedPath1.GetRealDirectoryPath('/'));
            Assert.Equal(@"C:\Folder\Folder2\", mixedPath1.GetRealDirectoryPath('\\'));

            string mixedPath2 = @"file://Folder\Folder2/Folder3";
            Assert.True(mixedPath2.IsPathRooted());
            Assert.Equal(@"file://", mixedPath2.GetPathProtocol());
            Assert.Equal(@"file://", mixedPath2.GetPathRoot());
            Assert.Null(mixedPath2.GetRealPathRoot());
            Assert.Equal(@"Folder\Folder2/Folder3", mixedPath2.GetRealPath());
            Assert.Equal(@"Folder3", mixedPath2.GetRealFilename());
            Assert.Equal(@"Folder/Folder2/", mixedPath2.GetRealDirectoryPath('/'));
            Assert.Equal(@"Folder\Folder2\", mixedPath2.GetRealDirectoryPath('\\'));
        }
        [Fact]
        public void ShoudTellDifferenceBetweenCommandNameAndArguemts()
        {
            string[] arguments = "Add note.txt note2.txt".BreakCommandLineArgumentPositions();
            Assert.Equal("add", arguments.GetCommandName());
            Assert.Empty(new string[] { "note.txt", "note2.txt" }.Except(arguments.GetArguments()));
        }
        [Fact]
        public void StringHelperPathRootShouldReturnRootProperly()
        {
            Assert.Null("".GetPathRoot());
            Assert.Null(@"./file".GetPathRoot());
            Assert.Equal(@"/bin/", "/bin".GetPathRoot());
            Assert.Equal(@"C:/", "C:/Windows".GetPathRoot());
            Assert.Equal(@"\\myPc\", @"\\myPc\mydir\myfile".GetPathRoot());

            // Protocol
            Assert.Equal(@"http://", @"http://totalimagine.com".GetPathRoot());
            Assert.Equal(@"file://\\", @"file://\\Folder".GetPathRoot());
            Assert.Equal(@"file:\\", @"file:\\file".GetPathRoot());
            Assert.Null(@"file:something".GetPathRoot());
        }
        [Fact]
        public void StringHelperIsPathRootedShouldHandleAllCases()
        {
            Assert.False("".IsPathRooted());
            Assert.False("./file".IsPathRooted());
            Assert.True("/bin".IsPathRooted());
            Assert.True("C:/Windows".IsPathRooted());
            Assert.True(@"\\myPc\mydir\myfile".IsPathRooted());

            // Protocol
            Assert.True(@"http://totalimagine.com".IsPathRooted());
            Assert.True(@"file://\\Folder".IsPathRooted());
            Assert.True(@"file://file".IsPathRooted());
            Assert.False(@"file:something".IsPathRooted());
        }
        [Fact]
        public void StringHelperGerRealFilenameShouldConsiderInvalidCharacters()
        {
            Assert.Equal(@"This is a ?Fancy? filename/invalid filename", @"C:\Hello World/This is my !Folder!\This is a ?Fancy? filename/invalid filename".GetRealFilename());  // Notice exclamation mark is allowed in windows filename
            Assert.Equal(@"", @"C:\Hello World/This is my Folder\".GetRealFilename());
            Assert.Equal(@"This is my *file*\", @"C:\Hello World/This is my *file*\".GetRealFilename());    // Edge case, this is considered a real filename
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

        [Fact]
        public void TiddlerTagsShouldSplitCorrectly()
        {
            var import = new Tiddler()
            {
                tags = "[[Super Hot Girl]] 影视"
            };
            Assert.Empty(new string[] { "Super Hot Girl", "影视" }.Except(import.Tags));
        }
    }
}

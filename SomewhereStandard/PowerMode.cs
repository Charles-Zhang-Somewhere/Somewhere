using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StringHelper;
using System.IO;

namespace Somewhere
{
    /// <summary>
    /// Power mode is an intrusive mode injecting behaviors onto normal commands,
    /// in this sense power mode plays well with normal commands by exploiting 
    /// conventions used by normal commands to provide extra behaviors e.g. auto completion,
    /// in which case power mode augments existing commands without them needing to change 
    /// implementation explicityly, as such power model is command-aware and command-specific.
    /// </summary>
    internal class PowerMode
    {
        #region Entrance
        internal void Enter(Commands commands, string[] args)
        {
            // Save parameters
            Commands = commands;
            Arguments = args;
            // Clear console and show welcome message
            ClearConsole(Top);
            PrintWelcome();
            // Enter command loop
            CommandLoop();
        }
        #endregion

        #region Properties
        private Commands Commands { get; set; }
        private string[] Arguments { get; set; }
        private StringBuilder Buffer = new StringBuilder();
        #endregion

        #region Sub Routines
        void PrintWelcome()
        {
            // Go to top left corner
            SetPosition(0, 0);
            // Print a new line to also set cursor location for user input at next line
            PrintLine("Welcome to Somewhere - Power Mode");
        }
        void ProcessInput()
        {
            Tuple<string, string>[] extraCommands = new Tuple<string, string>[]
            {
                new Tuple<string, string>("clr", "clear screen")
            };
            void HandleExtraCommand(string command, string[] arguments)
            {
                switch (command)
                {
                    case "clr":
                        ClearConsole(null);
                        break;
                }
            }
            void ProcessCommand(string line)
            {
                // Get command parameters
                string[] positions = line.BreakCommandLineArgumentPositions();
                string command = positions.GetCommandName().ToLower();
                string[] arguments = positions.GetArguments();
                // Handle extra commands
                if (extraCommands.Select(c => c.Item1).Contains(command))
                    HandleExtraCommand(command, arguments);
                // Handle normal commands
                else
                {
                    var results = Commands.ExecuteCommand(command, arguments);
                    foreach (var result in results)
                        PrintLine(result);
                    // Print extra commands
                    if (command == "help")
                    {
                        PrintLine("Power Mode Commands: ");
                        foreach (var item in extraCommands)
                            PrintLine($"\tUse `{item.Item1}` to {item.Item2}.");
                        PrintLine("Press `ESC` to exit power mode.");
                    }
                }                
            }
            int PreviousHintCount = 0;
            string PreviousHintCommand = string.Empty;  // Optimization, avoid hinting again
            string[] PreviousArguments = null;
            void HintTabCompletion()
            {
                // Print single line of hint
                void PrintHint(string text)
                {
                    int currentLeft = Left;
                    SetPositionOffset(1, 0);
                    ClearLines(PreviousHintCount);
                    Left = 0;
                    PrintLine(text);    // Append new line in case window resizes
                    SetPositionOffset(-2, 0);
                    Left = currentLeft;
                    PreviousHintCount = 1;
                }
                void PrintHintLines(IEnumerable<string> lines)
                {
                    int currentLeft = Left;
                    int currentTop = Top;
                    SetPositionOffset(1, 0);
                    ClearLines(PreviousHintCount);
                    Left = 0;
                    foreach (var line in lines)
                    {
                        // Avoid overflow
                        if (Top < WindowHeight)
                            PrintLine(line);
                        else
                            break;
                    }   
                    SetPosition(currentTop, currentLeft);
                    PreviousHintCount = lines.Count();
                }
                // Get current typed commands
                string currentLine = Buffer.ToString();
                if(!string.IsNullOrEmpty(currentLine))
                {
                    // Get command parameters
                    string[] positions = currentLine.BreakCommandLineArgumentPositions();
                    string command = positions.GetCommandName().ToLower();
                    string[] arguments = positions.GetArguments();
                    // Show completion for very specific commands
                    switch (command)
                    {
                        // Hint add
                        case "add":
                            if (PreviousHintCommand != currentLine.Trim()
                                || ((arguments.Length != 0 || PreviousArguments.Length != 0) && !PreviousArguments.SequenceEqual(arguments)))
                            {
                                PrintHintLines(Directory
                                    .GetFiles(Commands.HomeDirectory)
                                    // Get name only
                                    .Select(f => Path.GetFileName(f))
                                    // Search filter
                                    .Where(f => arguments.Length > 0 ? f.ToLower().StartsWith(arguments[0].ToLower()) : true)
                                    // Return results
                                    .Select((f, i) => $"{i + 1}. {f}"));
                            }
                            break;
                        default:
                            if (PreviousHintCount != 0)
                                ClearLines(Top + 1, PreviousHintCount);
                            else
                                PrintHint("No suggestions available.");
                            break;
                    }
                    PreviousHintCommand = command;
                    PreviousArguments = arguments;
                }
            }
            void AppendAndPrint(char c)
            {
                Buffer.Append(c);
                Print(c);
            }
            bool breakProcessing = false;
            while (!breakProcessing)
            {
                var key = ReadKey();
                switch (key.Key)
                {
                    // Function Keys
                    case ConsoleKey.F1:
                        break;
                    case ConsoleKey.F10:
                        break;
                    case ConsoleKey.F11:
                        break;
                    case ConsoleKey.F12:
                        break;
                    case ConsoleKey.F13:
                        break;
                    case ConsoleKey.F14:
                        break;
                    case ConsoleKey.F15:
                        break;
                    case ConsoleKey.F16:
                        break;
                    case ConsoleKey.F17:
                        break;
                    case ConsoleKey.F18:
                        break;
                    case ConsoleKey.F19:
                        break;
                    case ConsoleKey.F2:
                        break;
                    case ConsoleKey.F20:
                        break;
                    case ConsoleKey.F21:
                        break;
                    case ConsoleKey.F22:
                        break;
                    case ConsoleKey.F23:
                        break;
                    case ConsoleKey.F24:
                        break;
                    case ConsoleKey.F3:
                        break;
                    case ConsoleKey.F4:
                        break;
                    case ConsoleKey.F5:
                        break;
                    case ConsoleKey.F6:
                        break;
                    case ConsoleKey.F7:
                        break;
                    case ConsoleKey.F8:
                        break;
                    case ConsoleKey.F9:
                        break;
                    // Action Keys
                    case ConsoleKey.Applications:
                        break;
                    case ConsoleKey.Attention:
                        break;
                    case ConsoleKey.Backspace:
                        if (Buffer.Length > 0 && Left > 0)
                        {
                            Buffer.Remove(Buffer.Length - 1, 1);
                            ClearCharacter();
                        }
                        HintTabCompletion();
                        break;
                    case ConsoleKey.BrowserBack:
                        break;
                    case ConsoleKey.BrowserFavorites:
                        break;
                    case ConsoleKey.BrowserForward:
                        break;
                    case ConsoleKey.BrowserHome:
                        break;
                    case ConsoleKey.BrowserRefresh:
                        break;
                    case ConsoleKey.BrowserSearch:
                        break;
                    case ConsoleKey.BrowserStop:
                        break;
                    case ConsoleKey.Clear:
                        break;
                    case ConsoleKey.CrSel:
                        break;
                    case ConsoleKey.Help:
                        break;
                    case ConsoleKey.LaunchApp1:
                        break;
                    case ConsoleKey.LaunchApp2:
                        break;
                    case ConsoleKey.LaunchMail:
                        break;
                    case ConsoleKey.LaunchMediaSelect:
                        break;
                    case ConsoleKey.LeftWindows:
                        break;
                    case ConsoleKey.MediaNext:
                        break;
                    case ConsoleKey.MediaPlay:
                        break;
                    case ConsoleKey.MediaPrevious:
                        break;
                    case ConsoleKey.MediaStop:
                        break;
                    case ConsoleKey.Pa1:
                        break;
                    case ConsoleKey.Packet:
                        break;
                    case ConsoleKey.Pause:
                        break;
                    case ConsoleKey.Play:
                        break;
                    case ConsoleKey.Print:
                        break;
                    case ConsoleKey.PrintScreen:
                        break;
                    case ConsoleKey.Process:
                        break;
                    case ConsoleKey.RightWindows:
                        break;
                    case ConsoleKey.Select:
                        break;
                    case ConsoleKey.Separator:
                        break;
                    case ConsoleKey.Sleep:
                        break;
                    case ConsoleKey.VolumeDown:
                        break;
                    case ConsoleKey.VolumeMute:
                        break;
                    case ConsoleKey.VolumeUp:
                        break;
                    case ConsoleKey.Zoom:
                        break;
                    case ConsoleKey.EraseEndOfFile:
                        break;
                    case ConsoleKey.Execute:
                        break;
                    case ConsoleKey.ExSel:
                        break;
                    // Control Keys
                    case ConsoleKey.Enter:
                        breakProcessing = true;
                        if (PreviousHintCount != 0)
                            ClearLines(Top + 1, PreviousHintCount);
                        break;
                    case ConsoleKey.Escape:
                        ShouldExit = true;
                        if(PreviousHintCount != 0)
                            ClearLines(Top + 1,PreviousHintCount);
                        Top += 1;
                        Left = 0;
                        PrintLine("Exit power mode.");
                        return;
                    case ConsoleKey.Delete:
                        break;
                    case ConsoleKey.Insert:
                        break;
                    case ConsoleKey.Tab:
                        break;
                    // Navigation Keys
                    case ConsoleKey.End:
                        break;
                    case ConsoleKey.DownArrow:
                        break;
                    case ConsoleKey.LeftArrow:
                        break;
                    case ConsoleKey.RightArrow:
                        break;
                    case ConsoleKey.UpArrow:
                        break;
                    case ConsoleKey.Home:
                        break;
                    case ConsoleKey.PageUp:
                        break;
                    case ConsoleKey.PageDown:
                        break;
                    // Character Keys
                    case ConsoleKey.Decimal:
                        break;
                    case ConsoleKey.Add:
                        break;
                    case ConsoleKey.Divide:
                        break;
                    case ConsoleKey.Multiply:
                        break;
                    case ConsoleKey.Subtract:
                        break;
                    case ConsoleKey.A:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.B:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.C:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.E:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.F:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.G:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.H:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.I:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.J:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.K:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.L:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;                   
                    case ConsoleKey.M:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.N:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.O:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.P:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Q:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.R:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.S:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.T:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.U:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.V:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.W:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.X:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Y:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Z:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Spacebar:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    // Number keys
                    case ConsoleKey.NumPad0:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.NumPad1:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.NumPad2:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.NumPad3:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.NumPad4:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.NumPad5:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.NumPad6:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.NumPad7:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.NumPad8:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.NumPad9:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D0:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D1:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D2:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D3:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D4:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D5:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D6:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D7:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D8:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.D9:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    // OEM Characters
                    case ConsoleKey.Oem1:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Oem102:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Oem2:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Oem3:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Oem4:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Oem5:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Oem6:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Oem7:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.Oem8:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.OemComma:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.OemMinus:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.OemPeriod:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    case ConsoleKey.OemPlus:
                        AppendAndPrint(key.KeyChar); HintTabCompletion();
                        break;
                    // Unidentified
                    case ConsoleKey.OemClear:
                        break;
                    case ConsoleKey.NoName:
                        break;
                    default:
                        break;
                }
                // Unicode character?
                if ((byte)key.Key == 0 && (int)key.KeyChar != 0)
                {
                    AppendAndPrint(key.KeyChar);
                    HintTabCompletion();
                }
            }
            PrintLine();
            ProcessCommand(Buffer.ToString());
            Buffer.Clear();
        }
        private bool ShouldExit = false;
        void CommandLoop()
        {
            while (!ShouldExit)
            {
                Print("> ");
                ProcessInput();
            }
        }
        #endregion

        #region Helper Methods
        /// <summary>
        /// Removes one character from current line;
        /// Assume left boundary safe;
        /// Updates cursor position
        /// </summary>
        void ClearCharacter()
        {
            Left = Left - 1;
            Print(' ');
            Left = Left - 1;
        }
        /// <summary>
        /// Clears current line;
        /// Updates cursor position
        /// </summary>
        void ClearLine()
        {
            Left = 0;
            Print(new string(' ', WindowWidth));
            Left = 0;
        }
        /// <summary>
        /// Clears line at specified row and goes back to current position;
        /// Updates cursor position
        /// </summary>
        void ClearLine(int row)
        {
            int currentLeft = Left;
            int currentTop = Top;
            Top = row;
            ClearLine();
            Left = currentLeft;
            Top = currentTop;
        }
        /// <summary>
        /// Clears specified number of lines starting from current line;
        /// Updates cursor position
        /// </summary>
        void ClearLines(int count)
        {
            if (count == 0) return;
            int currentTop = Top;
            for (int i = 0; i < count; i++)
            {
                Top = currentTop + i;
                ClearLine();
            }
            Top = currentTop;
        }
        /// <summary>
        /// Clears specified number of lines starting from given line and goes back to current position;
        /// Updates cursor position
        /// </summary>
        void ClearLines(int row, int count)
        {
            if (count == 0) return;
            int currentLeft = Left;
            int currentTop = Top;
            Top = row;
            ClearLines(count);
            Left = currentLeft;
            Top = currentTop;
        }
        void ClearConsole(int? lowerHeight)
        {
            for (int i = 0; i < (lowerHeight ?? WindowHeight); i++)
            {
                Top = i;
                ClearLine();
            }
            SetPosition(0, 0);
        }
        void SetPosition(int top, int left)
        {
            Top = top;
            Left = left;
        }
        private void SetPositionOffset(int rowOff, int colOff)
        {
            Top = Top + rowOff;
            Left = Left + colOff;
        }
        int Left
        {
            get => Console.CursorLeft;
            set => Console.CursorLeft = value;
        }
        int Top
        {
            get => Console.CursorTop;
            set => Console.CursorTop = value;
        }
        int WindowWidth => Console.WindowWidth;
        int WindowHeight => Console.WindowHeight;
        int BufferWidth => Console.BufferWidth;
        int BufferHeight => Console.BufferHeight;
        void Print(char character)
            => Console.Write(character);
        void Print(string text)
            => Console.Write(text);
        void PrintLine(string text)
            => Console.WriteLine(text);
        void PrintLine()
            => Console.WriteLine();
        ConsoleKeyInfo ReadKey()
            => Console.ReadKey(true);
        #endregion
    }
}

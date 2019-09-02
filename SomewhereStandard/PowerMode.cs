using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using StringHelper;

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
            ClearConsole();
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
            Print("Welcome to Somewhere - Power Mode");
            // Set cursor location for user input
            SetPosition(1, 0);
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
                        ClearConsole();
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
                    }
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
                        break;
                    case ConsoleKey.Escape:
                        ShouldExit = true;
                        PrintLine();
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
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.B:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.C:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.E:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.F:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.G:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.H:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.I:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.J:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.K:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.L:
                        AppendAndPrint(key.KeyChar);
                        break;                   
                    case ConsoleKey.M:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.N:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.O:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.P:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.Q:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.R:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.S:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.T:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.U:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.V:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.W:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.X:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.Y:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.Z:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.Spacebar:
                        AppendAndPrint(key.KeyChar);
                        break;
                    // Number keys
                    case ConsoleKey.NumPad0:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.NumPad1:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.NumPad2:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.NumPad3:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.NumPad4:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.NumPad5:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.NumPad6:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.NumPad7:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.NumPad8:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.NumPad9:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D0:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D1:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D2:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D3:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D4:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D5:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D6:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D7:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D8:
                        AppendAndPrint(key.KeyChar);
                        break;
                    case ConsoleKey.D9:
                        AppendAndPrint(key.KeyChar);
                        break;
                    // OEM
                    case ConsoleKey.Oem1:
                        break;
                    case ConsoleKey.Oem102:
                        break;
                    case ConsoleKey.Oem2:
                        break;
                    case ConsoleKey.Oem3:
                        break;
                    case ConsoleKey.Oem4:
                        break;
                    case ConsoleKey.Oem5:
                        break;
                    case ConsoleKey.Oem6:
                        break;
                    case ConsoleKey.Oem7:
                        break;
                    case ConsoleKey.Oem8:
                        break;
                    case ConsoleKey.OemClear:
                        break;
                    case ConsoleKey.OemComma:
                        break;
                    case ConsoleKey.OemMinus:
                        break;
                    case ConsoleKey.OemPeriod:
                        break;
                    case ConsoleKey.OemPlus:
                        break;
                    // Unidentified
                    case ConsoleKey.NoName:
                        break;
                    default:
                        break;
                }
                // Unicode character?
                if ((byte)key.Key == 0 && (int)key.KeyChar != 0)
                    AppendAndPrint(key.KeyChar);
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
        void ClearLine()
        {
            Console.CursorLeft = 0;
            Console.Write(new string(' ', Console.WindowWidth));
        }
        void ClearConsole()
        {
            for (int i = 0; i < Console.WindowHeight; i++)
            {
                Console.CursorTop = i;
                ClearLine();
            }
            SetPosition(0, 0);
        }
        void SetPosition(int top, int left)
        {
            Console.CursorTop = top;
            Console.CursorLeft = left;
        }
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

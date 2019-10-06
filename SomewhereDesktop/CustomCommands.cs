using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace SomewhereDesktop
{
    public static class CustomCommands
    {
        #region CLI Commands Mapping
        public static readonly RoutedUICommand Add = new RoutedUICommand("Add a new item to Home", "Add", typeof(CustomCommands));
        public static readonly RoutedUICommand New = new RoutedUICommand("Create a new Home", "New", typeof(CustomCommands));
        public static readonly RoutedUICommand Create = new RoutedUICommand("Create a note (F2)", "Create", typeof(CustomCommands));
        public static readonly RoutedUICommand Open = new RoutedUICommand("Open a different Home Repository (F1)", "Open", typeof(CustomCommands));
        public static readonly RoutedUICommand Find = new RoutedUICommand("Find items (F3)", "Find", typeof(CustomCommands));
        public static readonly RoutedUICommand Import = new RoutedUICommand("Import items", "Import", typeof(CustomCommands));
        public static readonly RoutedUICommand DumpAll = new RoutedUICommand("Dump all notes and files", "DumpAll", typeof(CustomCommands));
        #endregion

        #region UI Commands
        public static readonly RoutedUICommand Record = new RoutedUICommand("Record some sound", "Record", typeof(CustomCommands));
        public static readonly RoutedUICommand Recent = new RoutedUICommand("Show recent home paths", "Recent", typeof(CustomCommands));
        public static readonly RoutedUICommand Maximize = new RoutedUICommand("Maximize window (F11)", "Maximize", typeof(CustomCommands));
        public static readonly RoutedUICommand Hide = new RoutedUICommand("Hide window (Ctrl+` or ESC)", "Hide", typeof(CustomCommands));
        public static readonly RoutedUICommand ShowShortcuts = new RoutedUICommand("Show shortcuts dialog (F12)", "ShowShortcuts", typeof(CustomCommands));
        public static readonly RoutedUICommand ShowMarkdownReference = new RoutedUICommand("Show Markdown reference dialog (Alt+F12)", "ShowMarkdownReference", typeof(CustomCommands));
        public static readonly RoutedUICommand OpenCommandPrompt = new RoutedUICommand("Open a new command prompt window (F9)", "OpenCommandPrompt", typeof(CustomCommands));
        public static readonly RoutedUICommand Refresh = new RoutedUICommand("Refresh Inventory tab items (F5)", "Refresh", typeof(CustomCommands));
        public static readonly RoutedUICommand Save = new RoutedUICommand("Save current editing item/note (Ctrl+S); Used to explicitly save changes; Usually changes are saved automatically but only when text box lose focus", "Save", typeof(CustomCommands));
        public static readonly RoutedUICommand CompileAndRun = new RoutedUICommand("Compile current code file and run it (F10)", "CompileAndRun", typeof(CustomCommands));
        public static readonly RoutedUICommand GotoActiveItemEditContent = new RoutedUICommand("Edit content for note item", "GotoActiveItemEditContent", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchInventory = new RoutedUICommand("Switch to Inventory tab (Ctrl+1)", "SwitchInventory", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchNotebook = new RoutedUICommand("Switch to Notebook tab (Ctrl+2)", "SwitchNotebook", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchKnowledge = new RoutedUICommand("Switch to Knowledge tab (Ctrl+4)", "SwitchKnowledge", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchLogs = new RoutedUICommand("Switch to Logs tab (Ctrl+6)", "SwitchLogs", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchStatus = new RoutedUICommand("Switch to Status tab (Ctrl+3)", "SwitchStatus", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchNTFSSearch = new RoutedUICommand("Switch to NTFSSearch tab (Ctrl+5)", "SwitchNTFSSearch", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchSettings = new RoutedUICommand("Switch to Settings tab (Ctrl+7)", "SwitchSettings", typeof(CustomCommands));
        public static readonly RoutedUICommand PreviewNote = new RoutedUICommand("Switch to Inventory tab and preview current editing note (Ctrl+Shift+1)", "PreviewNote", typeof(CustomCommands));
        #endregion
    }
}

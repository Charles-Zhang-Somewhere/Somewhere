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
        public static readonly RoutedUICommand Create = new RoutedUICommand("Create a note", "Create", typeof(CustomCommands));
        public static readonly RoutedUICommand Open = new RoutedUICommand("Open Home", "Open", typeof(CustomCommands));
        public static readonly RoutedUICommand Find = new RoutedUICommand("Find items", "Find", typeof(CustomCommands));
        #endregion

        #region UI Commands
        public static readonly RoutedUICommand Maximize = new RoutedUICommand("Maximize window", "Maximize", typeof(CustomCommands));
        public static readonly RoutedUICommand Hide = new RoutedUICommand("Hide window", "Hide", typeof(CustomCommands));
        public static readonly RoutedUICommand ShowShortcuts = new RoutedUICommand("Show shortcuts dialog", "ShowShortcuts", typeof(CustomCommands));
        public static readonly RoutedUICommand ShowMarkdownReference = new RoutedUICommand("Show Markdown reference dialog", "ShowMarkdownReference", typeof(CustomCommands));
        public static readonly RoutedUICommand OpenCommandPrompt = new RoutedUICommand("Open a new command prompt window", "OpenCommandPrompt", typeof(CustomCommands));
        public static readonly RoutedUICommand Refresh = new RoutedUICommand("Refresh Inventory tab", "Refresh", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchInventory = new RoutedUICommand("Switch to Inventory tab", "SwitchInventory", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchNotebook = new RoutedUICommand("Switch to Notebook tab", "SwitchNotebook", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchKnowledge = new RoutedUICommand("Switch to Knowledge tab", "SwitchKnowledge", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchLogs = new RoutedUICommand("Switch to Logs tab", "SwitchLogs", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchStatus = new RoutedUICommand("Switch to Status tab", "SwitchStatus", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchNTFSSearch = new RoutedUICommand("Switch to NTFSSearch tab", "SwitchNTFSSearch", typeof(CustomCommands));
        public static readonly RoutedUICommand SwitchSettings = new RoutedUICommand("Switch to Settings tab", "SwitchSettings", typeof(CustomCommands));
        #endregion
    }
}

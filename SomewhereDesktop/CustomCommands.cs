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
        public static readonly RoutedUICommand Add = new RoutedUICommand("Add a new item to Home", "Add", typeof(CustomCommands));
    }
}

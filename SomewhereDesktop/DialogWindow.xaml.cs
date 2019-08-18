using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SomewhereDesktop
{
    /// <summary>
    /// Interaction logic for DialogWindow.xaml
    /// </summary>
    public partial class DialogWindow : Window, INotifyPropertyChanged
    {
        #region Constructor
        /// <param name="content">Markdown-enabled</param>
        public DialogWindow(Window owner, string title = "Dialog", string content = "")
        {
            Owner = owner;
            InitializeComponent();
            Title = title;
            Markdown = content;
        }
        #endregion

        #region View Properties
        private string _Markdown;
        /// <summary>
        /// Currently selected option
        /// </summary>
        public string Markdown
        {
            get => _Markdown;
            set => SetField(ref _Markdown, value);
        }
        #endregion

        #region Window Events
        private void CloseWindowCommand_CanExecute(object sender, CanExecuteRoutedEventArgs e)
            => e.CanExecute = true;
        private void CloseWindowCommand_Executed(object sender, ExecutedRoutedEventArgs e)
            => this.Close();
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
            => this.DragMove();
        #endregion

        #region Data Binding
        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void NotifyPropertyChanged([CallerMemberName]string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        protected bool SetField<type>(ref type field, type value, [CallerMemberName]string propertyName = null)
        {
            if (EqualityComparer<type>.Default.Equals(field, value)) return false;
            field = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
}

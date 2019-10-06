using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        public DialogWindow(Window owner, string title = "Dialog", string content = "", IEnumerable<string> options = null, string OKButtonText = null)
        {
            Owner = owner;
            InitializeComponent();
            Title = title;
            Markdown = content;
            if (options != null)
            {
                Options = new ObservableCollection<string>(options);
            }
            else
            {
                OptionsPanel.Visibility = Visibility.Collapsed;
                Options = null;
            }
            if (OKButtonText != null)
                OKButton.Content = OKButtonText;
        }
        #endregion

        #region View Properties
        /// <summary>
        /// Available options
        /// </summary>
        private ObservableCollection<string> _Options;
        public ObservableCollection<string> Options
        {
            get => _Options;
            set => SetField(ref _Options, value);
        }
        /// <summary>
        /// Current selected item
        /// </summary>
        private string _Selection;
        public string Selection
        {
            get => _Selection;
            set => SetField(ref _Selection, value);
        }
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
        {
            // Cancel selection if any
            Selection = null;
            this.Close();
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
        private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
            => this.Close();
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

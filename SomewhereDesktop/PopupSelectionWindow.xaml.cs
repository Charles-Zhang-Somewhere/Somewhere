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
    /// Interaction logic for PopupSelectionWindow.xaml;
    /// Can be used as a permanent resource or dialog
    /// </summary>
    public partial class PopupSelectionWindow : Window, INotifyPropertyChanged
    {
        #region Constructor
        public PopupSelectionWindow()
        {
            InitializeComponent();
        }
        #endregion

        #region Popup Interface
        /// <param name="location">
        /// Absolute location and size in desktop space
        /// </param>
        /// <remarks>
        /// Non blocking
        /// </remarks>
        public void Show(Rect location, IEnumerable<string> options)
        {
            this.Top = location.Top;
            this.Left = location.Left;
            this.Width = location.Width;
            this.Height = location.Height;
            this.Visibility = Visibility.Visible;
            Options = new ObservableCollection<string>(options);
        }
        #endregion

        #region Window and View Properties
        private ObservableCollection<string> _Options;
        /// <summary>
        /// List of selectible options
        /// </summary>
        public ObservableCollection<string> Options
        {
            get => _Options;
            set => SetField(ref _Options, value);
        }
        private string _SelectedOption;
        /// <summary>
        /// Currently selected option
        /// </summary>
        public string SelectedOption
        {
            get => _SelectedOption;
            set => SetField(ref _SelectedOption, value);
        }
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace SomewhereDesktop
{
    /// <summary>
    /// A converter that convers notename to tip texts for Notebook tab
    /// </summary>
    public class NotenameToTipConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => string.IsNullOrEmpty(value as string)
            ? "Empty notename represent a \"Knowledge\""
            : string.Empty;
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new InvalidOperationException("Single way specialized converter; No converting back.");
    }
}

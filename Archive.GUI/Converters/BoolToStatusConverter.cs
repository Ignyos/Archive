using System.Globalization;
using System.Windows.Data;

namespace Archive.GUI;

/// <summary>
/// Converts a boolean IsRunning value to a status text.
/// </summary>
public class BoolToStatusConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isRunning)
        {
            return isRunning ? "Running" : "Idle";
        }
        return "Idle";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace JuiceMidiMaker.Converters;

public sealed class BoolToColorConverter : IValueConverter
{
    public System.Windows.Media.Brush ActiveBrush { get; set; } = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0x55, 0x00));
    public System.Windows.Media.Brush InactiveBrush { get; set; } = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x33, 0x33));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? ActiveBrush : InactiveBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

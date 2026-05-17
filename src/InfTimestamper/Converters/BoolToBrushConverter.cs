using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace InfTimestamper.Converters;

public sealed class BoolToBrushConverter : IValueConverter
{
    public Brush TrueBrush { get; set; } = Brushes.White;
    public Brush FalseBrush { get; set; } = new SolidColorBrush(Color.FromRgb(255, 220, 220));

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? TrueBrush : FalseBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

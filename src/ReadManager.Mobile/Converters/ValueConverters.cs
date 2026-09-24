using System.Globalization;

namespace ReadManager.Mobile.Converters;

// true nếu giá trị khác null/rỗng — dùng để hiện/ẩn theo ErrorMessage (string) hoặc một object (vd Story đã tải xong chưa)
public class StringToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s ? !string.IsNullOrWhiteSpace(s) : value is not null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

// Đảo ngược bool — dùng để disable nút khi IsBusy = true
public class InvertedBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is bool b && b);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is bool b && b);
}

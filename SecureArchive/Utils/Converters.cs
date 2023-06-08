using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml;

namespace SecureArchive.Utils;

public class BoolVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, string language) {
        return ((bool)value) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) {
        throw new NotImplementedException();
    }
}
public class NegBoolVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, string language) {
        return (!(bool)value) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) {
        throw new NotImplementedException();
    }
}

public class NegBoolConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, string language) {
        return !(bool)value; 
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) {
        return value as bool? != true;
    }
}


public class EnumBooleanConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, string language) {
        string? parameterString = parameter as string;
        if (parameterString == null) {
            return DependencyProperty.UnsetValue;
        }

        if (Enum.IsDefined(value.GetType(), value) == false) {
            return DependencyProperty.UnsetValue;
        }

        object paramValue = Enum.Parse(value.GetType(), parameterString);

        if (paramValue.Equals(value)) {
            return true;
        }
        else {
            return false;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) {
        if (!(bool)value) {
            // true の場合以外は値が不定
            return DependencyProperty.UnsetValue;
        }
        string? parameterString = parameter as string;
        if (parameterString == null) {
            return DependencyProperty.UnsetValue;
        }

        return Enum.Parse(targetType, parameterString);
    }
}

public class NegEnumBooleanConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, string language) {
        string? parameterString = parameter as string;
        if (parameterString == null) {
            return DependencyProperty.UnsetValue;
        }

        if (Enum.IsDefined(value.GetType(), value) == false) {
            return DependencyProperty.UnsetValue;
        }

        object paramValue = Enum.Parse(value.GetType(), parameterString);

        if (paramValue.Equals(value)) {
            return false;
        }
        else {
            return true;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) {
        if ((bool)value) {
            // falseの場合以外は値が不定
            return DependencyProperty.UnsetValue;
        }
        string? parameterString = parameter as string;
        if (parameterString == null) {
            return DependencyProperty.UnsetValue;
        }

        return Enum.Parse(targetType, parameterString);
    }
}

public class EnumVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, string language) {
        string? parameterString = parameter as string;
        if (parameterString == null) {
            return DependencyProperty.UnsetValue;
        }

        if (Enum.IsDefined(value.GetType(), value) == false) {
            return DependencyProperty.UnsetValue;
        }

        object paramValue = Enum.Parse(value.GetType(), parameterString);

        if (paramValue.Equals(value)) {
            return Visibility.Visible;
        }
        else {
            return Visibility.Collapsed;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) {
        return DependencyProperty.UnsetValue;
    }
}

public class NegEnumVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, string language) {
        string? parameterString = parameter as string;
        if (parameterString == null) {
            return DependencyProperty.UnsetValue;
        }

        if (Enum.IsDefined(value.GetType(), value) == false) {
            return DependencyProperty.UnsetValue;
        }

        object paramValue = Enum.Parse(value.GetType(), parameterString);

        if (paramValue.Equals(value)) {
            return Visibility.Collapsed;
        }
        else {
            return Visibility.Visible;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) {
        return DependencyProperty.UnsetValue;
    }
}

public class DateStringConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, string language) {
        if (value is DateTime dateTime) {
            if (!DateTime.MinValue.Equals(value)) {
                return dateTime.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
            }
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) {
        throw new NotSupportedException();
    }
}

public class DecimalStringConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, string language) {
        return $"{value:#,0}";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) {
        throw new NotSupportedException();
    }
}

public class EmptyStringToVisibilityConverter : IValueConverter {
    public object Convert(object value, Type targetType, object parameter, string language) {
        if (string.IsNullOrEmpty(value as string)) {
            return Visibility.Visible;
        }
        else {
            return Visibility.Collapsed;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) {
        throw new NotSupportedException();
    }
}

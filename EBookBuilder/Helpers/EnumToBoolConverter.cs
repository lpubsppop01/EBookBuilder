using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace lpubsppop01.EBookBuilder
{
    internal class EnumToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var valueString = value?.ToString();
            if (valueString == null) return false;
            var parameterString = parameter as string;
            if (parameterString == null) return false;
            return valueString.ToLower() == parameterString.ToLower();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var valueBool = value as bool?;
            if (valueBool == null || !valueBool.Value) return null;
            var parameterString = parameter as string;
            if (parameterString == null) return null;
            return Enum.Parse(targetType, parameterString);
        }
    }
}

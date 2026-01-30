using System;
using System.ComponentModel;
using System.Globalization;
using Hyleus.Soundboard.Framework.Enums;

namespace Hyleus.Soundboard.Framework.TypeConverters;
public class LocaleTypeConverter : TypeConverter {

    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
        if (value is string s)
            return s.ToLocale();
        return base.ConvertFrom(context, culture, value);
    }

    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType) {
        if (destinationType == typeof(string) && value is Locale loc)
            return loc.ToFilename();
        return base.ConvertTo(context, culture, value, destinationType);
    }
}
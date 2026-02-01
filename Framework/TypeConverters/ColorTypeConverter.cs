using System;
using System.ComponentModel;
using System.Globalization;
using Microsoft.Xna.Framework;

namespace Hyleus.Soundboard.Framework.TypeConverters;
public class ColorTypeConverter : TypeConverter {
    public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) =>
        destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value) {
        if (value is string s) {
            s = s.Trim();

            // support hex #RRGGBB or #RRGGBBAA
            if (s.StartsWith('#')) {
                s = s[1..];
                if (s.Length == 6) {
                    byte r = byte.Parse(s[0..2], NumberStyles.HexNumber);
                    byte g = byte.Parse(s[2..4], NumberStyles.HexNumber);
                    byte b = byte.Parse(s[4..6], NumberStyles.HexNumber);
                    return new Color(r, g, b);
                } else if (s.Length == 8) {
                    byte r = byte.Parse(s[0..2], NumberStyles.HexNumber);
                    byte g = byte.Parse(s[2..4], NumberStyles.HexNumber);
                    byte b = byte.Parse(s[4..6], NumberStyles.HexNumber);
                    byte a = byte.Parse(s[6..8], NumberStyles.HexNumber);
                    return new Color(r, g, b, a);
                }
                throw new FormatException($"Invalid hex color: {value}");
            }

            // support CSV: R,G,B[,A]
            string[] parts = s.Split(',');
            if (parts.Length == 3 || parts.Length == 4) {
                byte r = byte.Parse(parts[0]);
                byte g = byte.Parse(parts[1]);
                byte b = byte.Parse(parts[2]);
                byte a = parts.Length == 4 ? byte.Parse(parts[3]) : (byte)255;
                return new Color(r, g, b, a);
            }

            throw new FormatException($"Cannot convert '{s}' to Color");
        }
        return base.ConvertFrom(context, culture, value);
    }

    public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType) {
        // output as #RRGGBB[AA]
        if (destinationType == typeof(string) && value is Color c)
            return c.A < 255 ? $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}" : $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        return base.ConvertTo(context, culture, value, destinationType);
    }
}

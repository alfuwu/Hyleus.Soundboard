using System.ComponentModel;
using Hyleus.Soundboard.Framework.TypeConverters;

namespace Hyleus.Soundboard.Framework.Enums;
[TypeConverter(typeof(LocaleTypeConverter))]
public enum Locale {
    // actual locales
    English,
    British,
    Spanish,
    French,
    German,
    Italian,
    Japanese,
    Chinese,
    Korean,
    Miulyn,

    // silly locales
    UwU,
    Pirate,
    OldEnglish,
    Gibber
}
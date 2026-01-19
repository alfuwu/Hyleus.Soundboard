using System;
using System.Collections.Generic;
using Hyleus.Pipeline;
using Hyleus.Soundboard.Framework.Enums;

namespace Hyleus.Soundboard;
public static class Language {
    private static readonly Dictionary<Locale, Localization> localizations = [];

    public static void LoadContent() {
        foreach (Locale loc in Enum.GetValues<Locale>())
            localizations[loc] = Main.Instance.Content.Load<Localization>($"Localization/{loc.ToFilename()}");
    }

    public static T Get<T>(string str, string def = null, Locale? loc = null) where T : AbstractLocalizable {
        if (!localizations[loc ?? Preferences.CurrentLocale].Entries.TryGetValue(str, out AbstractLocalizable value))
            if (def != null)
                return def as T;
            else if ((loc ?? Preferences.CurrentLocale) != Locale.English)
                if (localizations[Locale.English].Entries.TryGetValue(str, out value))
                    return value as T;
                else
                    throw new ArgumentException($"Localization key {str} does not exist");
            else
                return null;
        else
            return value as T;
    }

    public static AbstractLocalizable Get(string str, string def = null, Locale? loc = null) => Get<AbstractLocalizable>(str, def, loc);

    public static string ToFilename(this Locale loc) => loc switch {
        Locale.English => "en-US",
        Locale.British => "en-GB",
        Locale.Spanish => "es-ES",
        Locale.French => "fr-FR",
        Locale.German => "de-DE",
        Locale.Italian => "it-IT",
        Locale.Japanese => "ja-JP",
        Locale.Chinese => "zh-CN",
        Locale.Korean => "ko-KR",
        Locale.Miulyn => "mi-HY",

        Locale.UwU => "en-UwU",
        Locale.Pirate => "en-PI",
        Locale.OldEnglish => "ang-GB",

        _ => "gibber"
    };

    public static Locale ToLocale(this string localeStr) => localeStr.ToLower().Trim().Replace(' ', '-') switch {
        "en-us" => Locale.English,
        "en-gb" => Locale.British,
        "es-es" => Locale.Spanish,
        "fr-fr" => Locale.French,
        "de-de" => Locale.German,
        "it-it" => Locale.Italian,
        "ja-jp" => Locale.Japanese,
        "zh-cn" => Locale.Chinese,
        "ko-kr" => Locale.Korean,
        "mi-hy" => Locale.Miulyn,

        "en-uwu" => Locale.UwU,
        "en-pi" => Locale.Pirate,
        "ang-gb" => Locale.OldEnglish,

        _ => Locale.Gibber
    };
}

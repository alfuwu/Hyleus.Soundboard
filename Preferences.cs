using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Hyleus.Soundboard.Framework.Enums;
using Microsoft.Xna.Framework;

namespace Hyleus.Soundboard;

public static class Preferences {

    // event shenanigans
    /// <summary>
    /// Event invoked when a preference is updated.
    /// <para>Return true to accept the change, false to reject it.</para>
    /// </summary>
    /// <param name="propertyName"></param>
    /// <param name="old"></param>
    /// <param name="updated"></param>
    /// <returns></returns>
    public delegate bool PreferenceUpdateEvent(string propertyName, object old, object updated);
    public static event PreferenceUpdateEvent OnPreferenceUpdate;

    // internal fields
    private static bool _initialized = false;
    private static bool _safeToSave = true;

    // inner fields
    private static Locale _locale = Locale.English;
    private static bool _useCustomMouse = true;
    private static Color _mouseColor = Color.Black;
    private static Color _mouseBorderColor = Color.White;

    // properties
    [Description("""
        The language used in-game.
        Supported locales:
         - en-US (American English)
         - en-GB (British English)
         - es-ES (Spanish)
         - fr-FR (French)
         - de-DE (German)
         - it-IT (Italian)
         - ja-JP (Japanese)
         - zh-CN (Chinese)
         - ko-KR (Korean)
         - mi-HY (Miulyn)
        """)]
    public static Locale CurrentLocale { get => _locale; set => Update(nameof(CurrentLocale), ref _locale, value); }
    [Description("Determines whether the game should use a custom mouse cursor or not.")]
    public static bool UseCustomMouse { get => _useCustomMouse; set => Update(nameof(UseCustomMouse), ref _useCustomMouse, value); }
    [Description("The color of the mouse cursor's insides.\nDoesn't apply if UseCustomMouse is false.")]
    public static Color MouseColor { get => _mouseColor; set => Update(nameof(MouseColor), ref _mouseColor, value); }
    [Description("The color of the mouse cursor's border.\nDoesn't apply if UseCustomMouse is false.")]
    public static Color MouseBorderColor { get => _mouseBorderColor; set => Update(nameof(MouseBorderColor), ref _mouseBorderColor, value); }

    /// <summary>
    /// Updates a preference field and invokes the <see cref="OnPreferenceUpdate"/> event.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyName"></param>
    /// <param name="field"></param>
    /// <param name="value"></param>
    private static void Update<T>(string propertyName, ref T field, T value) {
        if (!field.Equals(value) && OnPreferenceUpdate?.Invoke(propertyName, field, value) != false) {
            field = value;
            if (_safeToSave)
                _ = SavePreferencesAsync();
        }
    }

    private static void LoadDefaults() {
        CurrentLocale = Locale.English;
        MouseColor = Color.Black;
        MouseBorderColor = Color.White;
    }

    #region IO
    private static void SetSafeToSave(bool value) {
        _safeToSave = value;
        if (value)
            _ = SavePreferencesAsync();
    }

    public static void LoadTypeConverters() {
        if (_initialized)
            return;

    }

    public static void LoadPreferences() {
        if (_initialized)
            return;
        LoadTypeConverters();
        _initialized = true;

        string path = Path.Combine(Main.DataFolder, "Soundboard.cfg");
        if (!File.Exists(path)) {
            Debug.WriteLine("Preferences file not found, loading defaults.");
            LoadDefaults();
            return;
        }

        SetSafeToSave(false);
        using FileStream fs = new(path, FileMode.Open, FileAccess.Read);
        using StreamReader reader = new(fs);
        Read(reader);
        reader.Close();
        fs.Close();

        SetSafeToSave(true);
    }
    public static void SavePreferences() {
        Directory.CreateDirectory(Main.DataFolder);
        using FileStream fs = new(Path.Combine(Main.DataFolder, "Preferences.cfg"), FileMode.Create, FileAccess.Write);
        using StreamWriter writer = new(fs);
        Write(writer);
    }
    public static async Task SavePreferencesAsync() {
        Directory.CreateDirectory(Main.DataFolder);
        string path = Path.Combine(Main.DataFolder, "Preferences.cfg");

        const int maxRetries = 10;
        const int delayMs = 100;

        for (int i = 0; i < maxRetries; i++) {
            try {
                await using FileStream fs = new(path, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, true);
                await using StreamWriter writer = new(fs);
                Write(writer);
                return; // success, exit the method
            } catch (IOException) when (i <= maxRetries) {
                Debug.WriteLine($"File access error when saving preferences. Retrying in {delayMs}ms...");
                await Task.Delay(delayMs); // wait before retrying
            }
        }
    }

    public static void Write(TextWriter writer) {
        PropertyInfo[] properties = typeof(Preferences).GetProperties();
        for (int i = 0; i < properties.Length; i++) {
            PropertyInfo inf = properties[i];
            if (inf.GetCustomAttribute<DescriptionAttribute>() != null)
                writer.WriteLine("# " + inf.GetCustomAttribute<DescriptionAttribute>().Description.Replace("\n", "\n# "));
            writer.Write(inf.Name + " = ");

            object value = inf.GetValue(null);
            TypeConverter converter = TypeDescriptor.GetConverter(inf.PropertyType);
            writer.WriteLine(converter != null ? converter.ConvertToString(value) : value?.ToString() ?? "null");
            if (i < properties.Length - 1)
                writer.WriteLine();
        }
    }
    public static void Read(StreamReader reader) {
        int lineNum = 0;
        string line;
        while ((line = reader.ReadLine()) != null) {
            lineNum++;
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            string[] parts = line.Split('=', 2);
            if (parts.Length != 2)
                throw new IOException($"malformed preferences file at line {lineNum}:\n{line}");

            string propName = parts[0].Trim();
            string propValue = parts[1].Trim();
            PropertyInfo inf = typeof(Preferences).GetProperty(propName);
            if (inf == null || !inf.CanWrite)
                continue;

            Type targetType = inf.PropertyType;
            object value = null;

            try {
                if (propValue != "null") {
                    // use TypeDescriptor to convert from string to the correct type
                    TypeConverter converter = TypeDescriptor.GetConverter(targetType);
                    if (converter != null && converter.CanConvertFrom(typeof(string)))
                        value = converter.ConvertFromString(propValue);
                    else if (targetType.IsEnum)
                        value = Enum.Parse(targetType, propValue);
                    else
                        throw new NotSupportedException($"Cannot convert '{propValue}' to {targetType}");
                }
            } catch (Exception ex) {
                throw new Exception($"Error converting preference '{propName}' value '{propValue}' to type {targetType} at line {lineNum}: {ex.Message}", ex);
            }

            // assign value via reflection
            inf.SetValue(null, value);
        }
    }
    #endregion
}
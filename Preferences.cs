using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Hyleus.Soundboard.Framework;
using Hyleus.Soundboard.Framework.Enums;
using Hyleus.Soundboard.Framework.Structs;
using Hyleus.Soundboard.Framework.TypeConverters;
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
    public delegate bool PreferenceUpdateEvent(string propertyName, object old, Ref<object> updated);
    public static event PreferenceUpdateEvent OnPreferenceUpdate;

    // internal fields
    private static bool _initialized = false;
    private static bool _safeToSave = true;

    // inner fields
    private static int _audioFrequency = 48000;
    private static Locale _locale = Locale.English;
    private static Color _bgColor = new(50, 46, 47);
    private static Color _fxColor1 = new(54, 48, 49);
    private static Color _fxColor2 = new(40, 39, 39);
    private static Color _fxColor3 = new(58, 50, 51);
    private static int _padding = 12;
    private static int _margin = 20;
    private static int _maxButtonSize = 128;
    private static int _minButtonSize = 64;
    private static bool _playSoundsToHeadphones = true;
    private static bool _playVoiceChangerToHeadphones = true;
    private static bool _playMicToHeadphones = false;
    private static float _volumeMin = 0.0f;
    private static float _volumeMax = 10.0f;
    private static float _speedMin = float.Epsilon;
    private static float _speedMax = 3.0f;
    private static bool _closeCtxMenuOnScroll = false;
    private static bool _preventScrollWhenCtxMenuIsOpen = true;

    // properties
    [AllowedValues(8000, 16000, 24000, 44100, 48000, 96000)]
    [Description("The frequency that sounds and microphone input will be played/recorded at, in hertz. Requires a restart.\nGenerally, 44.1kHz and 48kHz will be the best options, as they are the most common frequencies that audio is recorded at, and thus do not need resampling.\nIt is recommended to only change this value if the majority of your sounds are recorded at a different frequency (e.g. 44.1kHz).\nWARNING: Frequencies not supported by your audio devices (e.g. 27kHz) will output pure silence. Make sure that you're using a frequency that's supported by your audio device (normally, this will be 16kHz, 24kHz, 44.1kHz, and 48kHz).")]
    public static int AudioFrequency { get => _audioFrequency; set => Update(nameof(AudioFrequency), ref _audioFrequency, value); }
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
    [Description("The main background color.")]
    public static Color BackgroundColor { get => _bgColor; set => Update(nameof(BackgroundColor), ref _bgColor, value); }
    [Description("The three effects colors, used to spice up the background a bit.")]
    public static Color EffectsColor1 { get => _fxColor1; set => Update(nameof(EffectsColor1), ref _fxColor1, value); }
    public static Color EffectsColor2 { get => _fxColor2; set => Update(nameof(EffectsColor2), ref _fxColor2, value); }
    public static Color EffectsColor3 { get => _fxColor3; set => Update(nameof(EffectsColor3), ref _fxColor3, value); }
    [Description("The padding between soundboard buttons and the window border.")]
    public static int Padding { get => _padding; set => Update(nameof(Padding), ref _padding, value); }
    [Description("The margin between soundboard buttons.")]
    public static int Margin { get => _margin; set => Update(nameof(Margin), ref _margin, value); }
    [Description("The minimum size of a soundboard button (in pixels).")]
    public static int MinButtonSize { get => _minButtonSize; set => Update(nameof(MinButtonSize), ref _minButtonSize, value); }
    [Description("The maximum size of a soundboard button (in pixels).")]
    public static int MaxButtonSize { get => _maxButtonSize; set => Update(nameof(MaxButtonSize), ref _maxButtonSize, value); }
    [Description("Plays sounds to both the virtual cable and your main audio device, allowing you to hear them too.")]
    public static bool PlaySoundsToSystem { get => _playSoundsToHeadphones; set => Update(nameof(PlaySoundsToSystem), ref _playSoundsToHeadphones, value); }
    [Description("Plays microphone input affected by a voice changer to your main audio device.")]
    public static bool PlayVoiceChangerToSystem { get => _playVoiceChangerToHeadphones; set => Update(nameof(PlayVoiceChangerToSystem), ref _playVoiceChangerToHeadphones, value); }
    [Description("Plays all microphone input to your main audio device.")]
    public static bool PlayMicToSystem { get => _playMicToHeadphones; set => Update(nameof(PlayMicToSystem), ref _playMicToHeadphones, value); }
    [Description("The minimum value you can set audio volume to using the context menu.")]
    public static float VolumeMin { get => _volumeMin; set => Update(nameof(VolumeMin), ref _volumeMin, value); }
    [Description("The maximum value you can set audio volume to using the context menu.")]
    public static float VolumeMax { get => _volumeMax; set => Update(nameof(VolumeMax), ref _volumeMax, value); }
    [Description("The minimum value you can set audio playback speed to using the context menu.")]
    public static float SpeedMin { get => _speedMin; set => Update(nameof(SpeedMin), ref _speedMin, value); }
    [Description("The maximum value you can set audio playback speed to using the context menu.\nWARNING: Values above 3.0 may cause crashes. Proceed with caution.")]
    public static float SpeedMax { get => _speedMax; set => Update(nameof(SpeedMax), ref _speedMax, value); }
    [Description("Determines whether or not the context menu will automatically close when you use the scroll wheel.")]
    public static bool CloseContextMenuOnScroll { get => _closeCtxMenuOnScroll; set => Update(nameof(CloseContextMenuOnScroll), ref _closeCtxMenuOnScroll, value); }
    [Description("Prevents using the scroll wheel while the context menu is open.")]
    public static bool PreventScrollWhenContextMenuIsOpen { get => _preventScrollWhenCtxMenuIsOpen; set => Update(nameof(PreventScrollWhenContextMenuIsOpen), ref _preventScrollWhenCtxMenuIsOpen, value); }

    /// <summary>
    /// Updates a preference field and invokes the <see cref="OnPreferenceUpdate"/> event.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="propertyName"></param>
    /// <param name="field"></param>
    /// <param name="value"></param>
    private static void Update<T>(string propertyName, ref T field, T value) {
        Ref<T> r = value;
        if (!field.Equals(value) && OnPreferenceUpdate?.Invoke(propertyName, field, r) != false) {
            field = r;
            if (_safeToSave)
                _ = SavePreferencesAsync();
        }
    }

    private static void LoadDefaults() {
        AudioFrequency = 48000;
        CurrentLocale = Locale.English;
        BackgroundColor = new(50, 46, 47);
        EffectsColor1 = new(54, 48, 49);
        EffectsColor2 = new(40, 39, 39);
        EffectsColor3 = new(58, 50, 51);
        Padding = 12;
        Margin = 20;
        MaxButtonSize = 128;
        MinButtonSize = 64;
        PlaySoundsToSystem = true;
        PlayVoiceChangerToSystem = true;
        PlayMicToSystem = false;
        VolumeMin = 0.0f;
        VolumeMax = 10.0f;
        SpeedMin = float.Epsilon;
        SpeedMax = 3.0f;
        CloseContextMenuOnScroll = false;
        PreventScrollWhenContextMenuIsOpen = true;
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
        TypeDescriptor.AddAttributes(typeof(Color), new TypeConverterAttribute(typeof(ColorTypeConverter)));
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
        using FileStream fs = new(Path.Combine(Main.DataFolder, "Soundboard.cfg"), FileMode.Create, FileAccess.Write);
        using StreamWriter writer = new(fs);
        Write(writer);
    }
    public static async Task SavePreferencesAsync() {
        Directory.CreateDirectory(Main.DataFolder);
        string path = Path.Combine(Main.DataFolder, "Soundboard.cfg");

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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Hyleus.Soundboard.Audio;
using Hyleus.Soundboard.Framework;
using Hyleus.Soundboard.Framework.Extensions;
using Hyleus.Soundboard.Framework.Structs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using static SDL2.SDL;

namespace Hyleus.Soundboard;
class Program {
#if OS_WINDOWS
    [STAThread]
#endif
    static void Main(string[] args) {
        using Main main = new();
        main.Run();
    }
}

public class Main : Game {
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private AudioEngine _audio;
    private KeyboardState _previousKeyboard;
    private readonly List<SoundboardItem> _sounds = [];
    private readonly List<SoundboardButton> _buttons = [];
    private MouseState _previousMouse;
    private MouseState _mouse;
    private Texture2D _pixel;
    private Texture2D _defaultIcon;
    private Effect _menu;
    private Effect _rounded;
    private Color _btn;
    private Color _btnHover;
    private Color _btnPressed;
    private string _preferredInput = null;
    private string _preferredOutput = null;

    public static Vector2I WindowSize {
        get => new(Instance._graphics.PreferredBackBufferWidth, Instance._graphics.PreferredBackBufferHeight);
        set { Instance._graphics.PreferredBackBufferWidth = value.X; Instance._graphics.PreferredBackBufferHeight = value.Y; Instance._graphics.ApplyChanges(); }
    }

    public static int ScreenWidth { get; set; } = 1280;
    public static int ScreenHeight { get; set; } = 720;

    private int _currentColumns;

    public static Main Instance { get; private set; }

    public static string DataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hyleus", "Data");
    public static string SoundboardData { get; } = Path.Combine(DataFolder, "Soundboard.dat");

    // continuous update stuff
    private SDL_EventFilter _filter = null;
    private bool _started = false;
    private bool _isMaximized = false;
    [DllImport("user32.dll", ExactSpelling = true)]
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
    private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, TimerProc lpTimerFunc);
    private delegate void TimerProc(IntPtr hWnd, uint uMsg, IntPtr nIDEvent, uint dwTime);

    private IntPtr _handle;
    private TimerProc _timerProc = null;

    private int _manualTickCount = 0;
    private bool _manualTick;

    public Main() {
        if (Instance != null)
            throw new InvalidOperationException("Only a single Main instance may be created. To create a new Main, set Main.Instance to null. Note that this will most likely have unintended consequences.");

        Thread.CurrentThread.Name = "Main";
        Console.Title = "Hyleus";

        Instance = this;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        WindowSize = new Vector2I(ScreenWidth, ScreenHeight);

        Preferences.OnPreferenceUpdate += (propertyName, old, updated) => {
            if (propertyName == nameof(Preferences.BackgroundColor))
                CalculateButtonColors(Preferences.BackgroundColor);
            
            Preferences.SavePreferences();
            return true;
        };

        Window.FileDrop += (_, args) => {
            foreach (var file in args.Files)
                ImportSound(file);
        };
        Window.AllowUserResizing = true;
    }

    protected override void Initialize() {
        try {
            _audio = new AudioEngine(48000);
        } catch (Exception e) {
            if (e.Message.Contains("VB-Audio Cable")) {
                ShowInstallPrompt();
                Exit();
            }
            return;
        }

        _filter = new SDL_EventFilter(HandleSDLEvent);
        SDL_AddEventWatch(_filter, IntPtr.Zero);

        _timerProc = BackupTick;
#if OS_WINDOWS
        _handle = SetTimer(IntPtr.Zero, IntPtr.Zero, 1, _timerProc);
#endif

        base.Initialize();
    }

    protected override void LoadContent() {
        _spriteBatch = new SpriteBatch(GraphicsDevice);

        Preferences.LoadPreferences();
        CalculateButtonColors(Preferences.BackgroundColor);
        Log.Info("User preferences loaded successfully.");

        if (File.Exists(SoundboardData)) {
            using var fs = File.OpenRead(SoundboardData);
            using var br = new BinaryReader(fs);
            _preferredInput = br.ReadStringOrNull();
            _preferredOutput = br.ReadStringOrNull();
            while (fs.Position < fs.Length) {
                try {
                    _sounds.Add(SoundboardItem.FromBinary(br));
                } catch {
                    Log.Error("Failed to load a sound");
                }
            }
            Log.Info("Successfully loaded soundboad data");
        }

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _defaultIcon = Content.Load<Texture2D>("DefaultIcon");

        _menu = Content.Load<Effect>("Shaders/Menu");
        _menu.Parameters["Color1"].SetValue(Preferences.EffectsColor1.ToVector3());
        _menu.Parameters["Color2"].SetValue(Preferences.EffectsColor2.ToVector3());
        _menu.Parameters["Color3"].SetValue(Preferences.EffectsColor3.ToVector3());
        _rounded = Content.Load<Effect>("Shaders/RoundedCorners");
        _rounded.Parameters["CornerRadius"].SetValue(16f);

        OnResized();
    }

    protected override void Update(GameTime gameTime) {
        _started = true;
        if (!_manualTick)
            _manualTickCount = 0;

        int oldScreenHeight = ScreenHeight;
        int oldScreenWidth = ScreenWidth;
        ScreenHeight = Window.ClientBounds.Height;
        ScreenWidth = Window.ClientBounds.Width;

        if (oldScreenHeight != ScreenHeight || oldScreenWidth != ScreenWidth)
            OnResized();

        _mouse = Mouse.GetState();

        if (_mouse.LeftButton == ButtonState.Pressed &&
            _previousMouse.LeftButton == ButtonState.Released) {

            var mousePoint = _mouse.Position;

            foreach (var button in _buttons) {
                if (button.Bounds.Contains(mousePoint)) {
                    if (button.Item.SoundLocation != null && IsActive)
                        _audio.PlaySound(button.Item.SoundLocation, button.Item.Volume);
                    break;
                }
            }
        }

        _previousMouse = _mouse;

        _menu.Parameters["Time"].SetValue((float)gameTime.TotalGameTime.TotalSeconds);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) {
        GraphicsDevice.Clear(Preferences.BackgroundColor);

        _spriteBatch.Begin(SpriteSortMode.Immediate, effect: _menu);
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(0, 0, ScreenWidth, ScreenHeight),
            Color.White
        );
        _spriteBatch.End();

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, effect: _rounded);

        foreach (var button in _buttons) {
            if (button.Icon == null)
                _spriteBatch.Draw(
                    _pixel,
                    button.Bounds,
                    button.Bounds.Contains(_mouse.Position) ? _mouse.LeftButton == ButtonState.Pressed ?
                        _btnPressed : _btnHover : _btn
                );

            Texture2D icon = button.Icon ?? _defaultIcon;
            _spriteBatch.Draw(
                icon,
                button.Bounds,
                button.Bounds.Contains(_mouse.Position) ? _mouse.LeftButton == ButtonState.Pressed ?
                    Color.Gray : Color.DarkGray : Color.White
            );
        }

        _spriteBatch.End();
    }

    private void OnResized() {
        _menu.Parameters["AspectRatio"].SetValue((float)ScreenWidth / ScreenHeight);
        BuildSoundboardGrid();
    }

    private void CalculateButtonColors(Color bg) {
        bool white = bg.R + bg.G + bg.B > 128 * 3;
        Color light = white ? Color.Black : Color.White;
        Color dark = white ? Color.White : Color.Black;
        _btn = Color.Lerp(bg, light, 0.1f);
        _btnHover = Color.Lerp(bg, light, 0.05f);
        _btnPressed = Color.Lerp(bg, dark, 0.05f);
    }

    public void AddSound(SoundboardItem item) {
        _sounds.Add(item);
        SaveSounds();
    }
    public void RemoveSoundAt(int i) {
        _sounds.RemoveAt(i);
        SaveSounds();
    }
    public IReadOnlyList<SoundboardItem> Sounds => _sounds.AsReadOnly();
    public void SaveSounds() {
        lock (_sounds) {
            using var fs = File.OpenWrite(SoundboardData);
            using var bw = new BinaryWriter(fs);
            bw.WriteStringOrNull(_preferredInput);
            bw.WriteStringOrNull(_preferredOutput);
            foreach (var i in _sounds)
                i.WriteBinary(bw);
        }
    }

    private void ImportSound(string path) {
        var item = new SoundboardItem {
            Name = Path.GetFileNameWithoutExtension(path),
            SoundLocation = path,
            Volume = 1.0f
        };

        AddSound(item);
        BuildSoundboardGrid();
    }

    public static void ShowInstallPrompt() {
        Task.Run(async () => {
            var result = await MessageBox.Show(
                "VB-Audio Virtual Cable was not detected.\n\n" +
                "This application requires VB-Audio Virtual Cable to route audio correctly.\n\n" +
                "Would you like to open the download page now?",
                "VB-Audio Cable Not Found",
                ["Yes", "No"]
            );
            if (result == 0) {
                Process.Start(new ProcessStartInfo {
                    FileName = "https://vb-audio.com/Cable/",
                    UseShellExecute = true
                });
            }
        }).Start();
    }

    public void BuildSoundboardGrid() {
        _buttons.Clear();

        int availableWidth = ScreenWidth - Preferences.Margin * 2;

        int maxColumns = int.Min(
            int.Max(1,
                (availableWidth + Preferences.Padding) / (Preferences.MinButtonSize + Preferences.Padding)
            ),
            _sounds.Count
        );

        int buttonSize = (availableWidth - Preferences.Padding * (maxColumns - 1)) / maxColumns;
        buttonSize = int.Clamp(buttonSize, Preferences.MinButtonSize, Preferences.MaxButtonSize);
        _rounded.Parameters["TextureSize"].SetValue(new Vector2(buttonSize, buttonSize));

        int columns = int.Max(1,
            (availableWidth + Preferences.Padding) / (buttonSize + Preferences.Padding)
        );

        _currentColumns = columns;

        for (int i = 0; i < _sounds.Count; i++) {
            int col = i % columns;
            int row = i / columns;

            var bounds = new Rectangle(
                Preferences.Margin + col * (buttonSize + Preferences.Padding),
                Preferences.Margin + row * (buttonSize + Preferences.Padding),
                buttonSize,
                buttonSize
            );

            var sound = _sounds[i];
            var oldButton = _buttons.Find(b => b.Item.UUID == sound.UUID);

            Texture2D icon = oldButton?.Icon;
            if (icon == null && !string.IsNullOrEmpty(sound.IconLocation) && File.Exists(sound.IconLocation)) {
                using var fs = File.OpenRead(sound.IconLocation);
                icon = Texture2D.FromStream(GraphicsDevice, fs);
            }

            _buttons.Add(new SoundboardButton {
                Item = sound,
                Bounds = bounds,
                Icon = icon
            });
        }
    }

    private unsafe int HandleSDLEvent(IntPtr userdata, IntPtr ptr) {
        SDL_Event* e = (SDL_Event*)ptr;

        switch (e->type) {
            case SDL_EventType.SDL_WINDOWEVENT:
                switch (e->window.windowEvent) {
                    case SDL_WindowEventID.SDL_WINDOWEVENT_RESIZED:
                        int width = e->window.data1;
                        int height = e->window.data2;
                        var p = Window.Position;
                        _graphics.PreferredBackBufferWidth = width;
                        _graphics.PreferredBackBufferHeight = height;
                        _graphics.ApplyChanges();
                        Window.Position = p;
                        OnResized();
                        break;
                    case SDL_WindowEventID.SDL_WINDOWEVENT_MOVED:
                        break;
                    case SDL_WindowEventID.SDL_WINDOWEVENT_MAXIMIZED:
                        _isMaximized = true;
                        break;
                    case SDL_WindowEventID.SDL_WINDOWEVENT_RESTORED:
                        _isMaximized = false;
                        break;
                }
                break;
        }

        return 0;
    }

    private void BackupTick(IntPtr hWnd, uint uMsg, IntPtr nIDEvent, uint dwTime) {
        if (_started) {
            if (_manualTickCount > 2) {
                _manualTick = true;
                Tick();
                _manualTick = false;
            }
            _manualTickCount++;
        }
    }
}
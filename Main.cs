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
    private SpriteFont _font;
    private Texture2D _pixel;
    private Texture2D _defaultIcon;
    private Effect _menu;
    private Effect _rounded;
    private Color _btn;
    private Color _btnHover;
    private Color _btnPressed;
    private Color _ctxMenu;
    private Color _ctxHover;
    private Color _ctxPressed;
    private string _preferredInput = null;
    private string _preferredOutput = null;
    private string _preferredRegularOutput = null;
    private bool _hasOwnCable = false;
    private Point _ctxPos;
    private SoundboardItem _ctxItem = null;
    private Rectangle? _ctxRect = null;
    private bool _draggingVolumeSlider = false;

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
#if OS_WINDOWS
    [DllImport("user32.dll", ExactSpelling = true)]
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
    private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, TimerProc lpTimerFunc);
#endif
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
            _audio = new AudioEngine(_hasOwnCable, 48000);
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
            _preferredRegularOutput = br.ReadStringOrNull();
            _hasOwnCable = br.ReadBoolean();
            while (fs.Position < fs.Length) {
                try {
                    _sounds.Add(SoundboardItem.FromBinary(br));
                } catch {
                    Log.Error("Failed to load a sound");
                }
            }
            Log.Info("Successfully loaded soundboad data");
        }

        _font = Content.Load<SpriteFont>("Font");

        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData([Color.White]);
        _defaultIcon = Content.Load<Texture2D>("DefaultIcon");

        //var iChannel2 = Content.Load<Texture2D>("iChannel2");
        //_graphics.GraphicsDevice.Textures[2] = iChannel2;

        _menu = Content.Load<Effect>("Shaders/Menu");
        _menu.Parameters["Resolution"]?.SetValue(new Vector2(ScreenWidth, ScreenHeight));
        _menu.Parameters["Color1"]?.SetValue(Preferences.EffectsColor1.ToVector3());
        _menu.Parameters["Color2"]?.SetValue(Preferences.EffectsColor2.ToVector3());
        _menu.Parameters["Color3"]?.SetValue(Preferences.EffectsColor3.ToVector3());
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

        bool left = LeftClick() &&
            _previousMouse.LeftButton == ButtonState.Released;
        if ((left ||
            _mouse.RightButton == ButtonState.Pressed &&
            _previousMouse.RightButton == ButtonState.Released) &&
            IsActive
        ) {
            _ctxItem = null;
            _ctxRect = null;

            foreach (var button in _buttons) {
                if (MouseHover(button.Bounds)) {
                    if (left) {
                        if (button.Item.SoundLocation != null)
                            _audio.PlaySound(button.Item);
                    } else {
                        _ctxPos = _mouse.Position;
                        _ctxItem = button.Item;
                    }
                    break;
                }
            }
        }
        if (_ctxItem != null && _ctxRect.HasValue && IsActive) {
            Rectangle sliderRect = new(
                _ctxRect.Value.X,
                _ctxRect.Value.Y + (_ctxRect.Value.Height - 48),
                _ctxRect.Value.Width,
                48
            );

            if (_mouse.LeftButton == ButtonState.Pressed && sliderRect.Contains(_mouse.Position))
                _draggingVolumeSlider = true;
            else if (_mouse.LeftButton == ButtonState.Released) {
                _draggingVolumeSlider = false;
                SaveSounds();
            }

            if (_draggingVolumeSlider) {
                int barX = sliderRect.X + 16;
                int barWidth = sliderRect.Width - 32;

                float normalized =
                    (_mouse.X - barX) / (float)barWidth;

                normalized = MathHelper.Clamp(normalized, 0f, 1f);

                float volume =
                    Preferences.VolumeMin + normalized * (Preferences.VolumeMax - Preferences.VolumeMin);

                _ctxItem.Volume = volume;
            }
        }

        _previousMouse = _mouse;

        _menu.Parameters["Time"]?.SetValue((float)gameTime.TotalGameTime.TotalSeconds);
        _menu.Parameters["Mouse"]?.SetValue(new Vector4(_mouse.X, _mouse.Y, _mouse.LeftButton == ButtonState.Pressed && IsActive ? 1 : 0, 0));

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
                    MouseHover(button.Bounds) ? LeftClick() ?
                        _btnPressed : _btnHover : _btn
                );

            Texture2D icon = button.Icon ?? _defaultIcon;
            _spriteBatch.Draw(
                icon,
                button.Bounds,
                MouseHover(button.Bounds) ? LeftClick() ?
                    Color.Gray : Color.DarkGray : Color.White
            );
        }
        _spriteBatch.End();

        if (_ctxItem != null)
            DrawContextMenu(_ctxPos);
    }

    private void DrawContextMenu(Point position) {
        const int menuWidth = 220;
        const int menuItemHeight = 36;
        const int horizontalPadding = 18;

        string[] menuItems = ["Play", "Edit", "Delete"];
        int sliderHeight = 48;

        int totalHeight =
            menuItemHeight * menuItems.Length +
            sliderHeight;

        _ctxRect = new Rectangle(
            position.X,
            position.Y,
            menuWidth,
            totalHeight
        );

        // menu background
        var texVec = _rounded.Parameters["TextureSize"].GetValueVector2();
        _rounded.Parameters["TextureSize"].SetValue(new Vector2(_ctxRect.Value.Width, _ctxRect.Value.Height));
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, effect: _rounded);
        _spriteBatch.Draw(_pixel, _ctxRect.Value, _ctxMenu);
        _spriteBatch.End();
        _rounded.Parameters["TextureSize"].SetValue(texVec);

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);

        // menu items
        for (int i = 0; i < menuItems.Length; i++) {
            Rectangle itemRect = new(position.X, position.Y + i * menuItemHeight, menuWidth, menuItemHeight);
            Color hover = itemRect.Contains(_mouse.Position) ? _mouse.LeftButton == ButtonState.Pressed ?
                _ctxPressed : _ctxHover : Color.Transparent;
            _spriteBatch.Draw(_pixel, itemRect, hover);

            _spriteBatch.Draw(
                _pixel,
                new Rectangle(itemRect.X + 8, itemRect.Y + 14, 6, 6),
                Color.White * 0.6f
            );

            // text
            _spriteBatch.DrawString(
                _font,
                menuItems[i],
                new Vector2(itemRect.X + horizontalPadding, itemRect.Y + 8),
                Color.White,
                0,
                Vector2.Zero,
                1.0f,
                SpriteEffects.None,
                0
            );
        }

        var divider = new Rectangle(
            position.X + 12,
            position.Y + menuItemHeight * menuItems.Length - 1,
            menuWidth - 24,
            2
        );

        _spriteBatch.Draw(_pixel, divider, Color.White * 0.08f);

        var sliderRect = new Rectangle(
            position.X,
            position.Y + menuItemHeight * menuItems.Length,
            menuWidth,
            sliderHeight
        );

        // hover background
        bool sliderHover = sliderRect.Contains(_mouse.Position);
        _spriteBatch.Draw(
            _pixel,
            sliderRect,
            sliderHover ? _ctxHover : Color.Transparent
        );

        // label
        _spriteBatch.DrawString(
            _font,
            $"Volume: {_ctxItem.Volume:0.00}",
            new Vector2(sliderRect.X + horizontalPadding, sliderRect.Y + 6),
            Color.White
        );

        // slider bar
        int barMargin = 16;
        int barHeight = 6;
        var barRect = new Rectangle(
            sliderRect.X + barMargin,
            sliderRect.Y + sliderHeight - 16,
            sliderRect.Width - barMargin * 2,
            barHeight
        );

        // background bar
        _spriteBatch.Draw(_pixel, barRect, Color.White * 0.25f);

        // filled portion
        float t = (_ctxItem.Volume - Preferences.VolumeMin) / (Preferences.VolumeMax - Preferences.VolumeMin);
        t = MathHelper.Clamp(t, 0f, 1f);

        var fillRect = new Rectangle(
            barRect.X,
            barRect.Y,
            (int)(barRect.Width * t),
            barRect.Height
        );

        _spriteBatch.Draw(_pixel, fillRect, Color.White);

        _spriteBatch.End();
    }

    private bool LeftClick() => _mouse.LeftButton == ButtonState.Pressed && (!_ctxRect.HasValue || !_ctxRect.Value.Contains(_mouse.Position));
    private bool MouseHover(Rectangle rect) => rect.Contains(_mouse.Position) && (!_ctxRect.HasValue || !_ctxRect.Value.Contains(_mouse.Position));

    private void OnResized() {
        _menu.Parameters["AspectRatio"]?.SetValue((float)ScreenWidth / ScreenHeight);
        BuildSoundboardGrid();
    }

    private void CalculateButtonColors(Color bg) {
        bool white = bg.R + bg.G + bg.B > 384; // try 400 maybe?
        Color light = white ? Color.Black : Color.White;
        Color dark = white ? Color.White : Color.Black;
        _btn = Color.Lerp(bg, light, 0.1f);
        _btnHover = Color.Lerp(bg, light, 0.05f);
        _btnPressed = Color.Lerp(bg, dark, 0.05f);
        _ctxMenu = Color.Lerp(bg, dark, 0.5f);
        _ctxMenu.A = 230;
        _ctxHover = light * 0.1f;
        _ctxPressed = dark * 0.2f;
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
            bw.WriteStringOrNull(_preferredRegularOutput);
            bw.Write(_hasOwnCable);
            foreach (var i in _sounds)
                i.WriteBinary(bw);
        }
    }

    private void ImportSound(string path) {
        foreach (var i in _sounds) {
            if (i.SoundLocation == path) {
                Log.Warn($"Attempted to add a duplicate sound with path of '{path}'");
                Task.Run(async () => {
                    await MessageBox.Show(
                        "Duplicate Sound",
                        "You cannot have two sounds with the same path",
                        ["Close"]
                    );
                }).Start();
                return;
            }
        }

        var item = new SoundboardItem {
            Name = Path.GetFileNameWithoutExtension(path),
            SoundLocation = path,
            Volume = 1.0f
        };

        AddSound(item);
        BuildSoundboardGrid();
    }

    public void ShowInstallPrompt() {
        Task.Run(async () => {
            var result = await MessageBox.Show(
                "VB-Audio Cable Not Found",
                "VB-Audio Virtual Cable was not detected.\n\n" +
                "This application highly recommends using VB-Audio Virtual Cable to route audio correctly.\n\n" +
                "Would you like to open the download page now?",
                ["Yes", "No", "No, I have my own virtual cable"]
            );
            if (result == 0) {
                Process.Start(new ProcessStartInfo {
                    FileName = "https://vb-audio.com/Cable/",
                    UseShellExecute = true
                });
            } else if (result == 2) {
                _hasOwnCable = true;
                SaveSounds();
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
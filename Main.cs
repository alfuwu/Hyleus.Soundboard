using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    private const ushort FILE_VERSION = 0;
    private const int menuWidth = 220;
    private const int menuItemHeight = 36;
    private const int horizontalPadding = 18;
    private const int sliderHeight = 48;
    private readonly (string name, Action onClick)[] _menuItems;

    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private AudioEngine _audio;
    private KeyboardState _previousKeyboard;
    private readonly List<Category> _categories = [];
    private readonly List<SoundboardItem> _sounds = [];
    private SoundboardButton[] _buttons = [];
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
    private readonly List<ContextMenuSlider> _ctxSliders = [];
    private ContextMenuSlider _activeSlider = null;
    private bool _wasActive;

    public static Vector2I WindowSize {
        get => new(Instance._graphics.PreferredBackBufferWidth, Instance._graphics.PreferredBackBufferHeight);
        set { Instance._graphics.PreferredBackBufferWidth = value.X; Instance._graphics.PreferredBackBufferHeight = value.Y; Instance._graphics.ApplyChanges(); }
    }

    public static int ScreenWidth { get; set; } = 1280;
    public static int ScreenHeight { get; set; } = 720;

    private int _categoryListSize = 96;
    private int CategoryListSize {
        get => _categoryListSize;
        set {
            _categoryListSize = value;
            BuildSoundboardGrid();
        }
    }
    private int _buttonSize;
    private int _currentColumns;
    private int Rows => _currentColumns <= 0 ? 0 : _sounds.Count / _currentColumns;

    public static Main Instance { get; private set; }

    public static string DataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hyleus", "Data");
    public static string SoundboardData { get; } = Path.Combine(DataFolder, "Soundboard.dat");

    // continuous update stuff
    private SDL_EventFilter _filter = null;
    private bool _started = false;
#if OS_WINDOWS
    [DllImport("user32.dll", ExactSpelling = true)]
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
    private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, TimerProc lpTimerFunc);
    private IntPtr _handle;
    private delegate void TimerProc(IntPtr hWnd, uint uMsg, IntPtr nIDEvent, uint dwTime);
    private TimerProc _timerProc = null;
#endif

    private int _manualTickCount = 0;
    private bool _manualTick;
    private int _scrollPosition = 0;

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

        _menuItems = [
            ("Play", () => _audio.PlaySound(_ctxItem)),
            ("Edit", () => {}),
            ("Delete", () => {
                RemoveSound(_ctxItem);
                BuildSoundboardGrid();
            })
        ];
    }

    protected override void Initialize() {
        try {
            _audio = new AudioEngine(_hasOwnCable, Preferences.AudioFrequency);
        } catch (Exception e) {
            if (e.Message.Contains("VB-Audio Cable")) {
                ShowInstallPrompt();
                Exit();
            }
            return;
        }

        SDL_SetWindowMinimumSize(Window.Handle, 480, 270);

        _filter = new SDL_EventFilter(HandleSDLEvent);
        SDL_AddEventWatch(_filter, IntPtr.Zero);

#if OS_WINDOWS
        _timerProc = BackupTick;
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
            int fileVer = br.ReadUInt16();
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

    SettingsWindow settings = null;

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

        bool skipPressed = !_wasActive && IsActive;
        bool left = LeftClick() &&
            (_previousMouse.LeftButton == ButtonState.Released || skipPressed);
        if ((left ||
            _mouse.RightButton == ButtonState.Pressed &&
            (_previousMouse.RightButton == ButtonState.Released || skipPressed)) &&
            IsActive
        ) {
            ResetCtxMenu();

            foreach (var button in _buttons) {
                if (MouseHover(button.Bounds)) {
                    if (left) {
                        if (button.Item.SoundLocation != null)
                            _audio.PlaySound(button.Item);
                    } else {
                        _ctxPos = _mouse.Position;
                        _ctxItem = button.Item;
                        BuildContextSliders(_ctxItem);
                    }
                    break;
                }
            }

            var bounds = new Rectangle(8, ScreenHeight - 8 - (CategoryListSize - 16), CategoryListSize - 16, CategoryListSize - 16);
            if (MouseHover(bounds)) {
                Log.Info(Window);
                settings = new(Window, GraphicsDevice, Window.ClientBounds.Width, Window.ClientBounds.Height);
                settings.OnDraw += (window, sb) => {
                    //GraphicsDevice.Clear(Color.Black);
                    /*sb.Begin();
                    sb.Draw(
                        _pixel,
                        new Rectangle(0, 0, window.ClientBounds.Width, window.ClientBounds.Height),
                        Color.White
                    );
                    sb.End();*/
                };
                
                //SettingsWindow.CreateGraphicsDevice(settings.Handle, 400, 400);
            }
        }
        if (_ctxItem != null && _ctxRect.HasValue && IsActive) {
            left = _mouse.LeftButton == ButtonState.Pressed &&
                    (_previousMouse.LeftButton == ButtonState.Released || skipPressed);
            for (int i = 0; i < _menuItems.Length; i++) {
                Rectangle rect = GetMenuItemRect(i);

                if (left && rect.Contains(_mouse.Position)) {
                    _menuItems[i].onClick.Invoke();
                    ResetCtxMenu();
                    break;
                }
            }

            foreach (var slider in _ctxSliders) {
                Rectangle sliderRect = GetSliderRect(slider);

                if (left && sliderRect.Contains(_mouse.Position)) {
                    _activeSlider = slider;
                    slider.IsDragging = true;
                }
            }

            if (_activeSlider != null) {
                Rectangle sliderRect = GetSliderRect(_activeSlider);

                int barX = sliderRect.X + 16;
                float barWidth = sliderRect.Width - 32;

                float t = (_mouse.X - barX) / barWidth;
                _activeSlider.SetFromNormalized(t);
            }
        }

        if (_mouse.LeftButton == ButtonState.Released && _activeSlider != null) {
            _activeSlider.IsDragging = false;
            _activeSlider = null;
            SaveSounds();
        }

        if (_mouse.ScrollWheelValue != _previousMouse.ScrollWheelValue && (!Preferences.PreventScrollWhenContextMenuIsOpen || _ctxItem == null))
            CalculateScroll();

        _previousMouse = _mouse;
        _wasActive = IsActive;

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

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(0, 0, CategoryListSize, ScreenHeight),
            new Color(20, 19, 19, 176)
        );
        _spriteBatch.End();

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, effect: _rounded);
        foreach (var button in _buttons) {
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
        var bounds = new Rectangle(8, ScreenHeight - 8 - (CategoryListSize - 16), CategoryListSize - 16, CategoryListSize - 16);
        _spriteBatch.Draw(
            _pixel,
            bounds,
            MouseHover(bounds) ? LeftClick() ?
                _btnPressed : _btnHover : _btn
        );
        _spriteBatch.End();

        if (_ctxItem != null)
            DrawContextMenu(_ctxPos);
        
        if (settings != null && settings.CanDraw()) {
            settings.Draw();
            settings.FinishDraw();
        }
    }

    private void DrawContextMenu(Point position) {
        int totalHeight =
            menuItemHeight * _menuItems.Length +
            sliderHeight * _ctxSliders.Count;

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
        for (int i = 0; i < _menuItems.Length; i++) {
            Rectangle itemRect = GetMenuItemRect(i);
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
                _menuItems[i].name,
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
            position.Y + menuItemHeight * _menuItems.Length - 1,
            menuWidth - 24,
            2
        );

        _spriteBatch.Draw(_pixel, divider, Color.White * 0.08f);

        _ctxSliders.RemoveAll(slider => slider == null);
        foreach (var slider in _ctxSliders) {
            Rectangle rect = GetSliderRect(slider);

            // hover background
            bool hover = rect.Contains(_mouse.Position);
            _spriteBatch.Draw(_pixel, rect, hover ? _ctxHover : Color.Transparent);

            // label
            _spriteBatch.DrawString(
                _font,
                $"{slider.Label}: {slider.GetValue() * 100:n0}%",
                new Vector2(rect.X + 18, rect.Y + 6),
                Color.White
            );

            // slider bar
            int barMargin = 16;
            int barHeight = 6;
            var barRect = new Rectangle(
                rect.X + barMargin,
                rect.Y + rect.Height - 16,
                rect.Width - barMargin * 2,
                barHeight
            );

            // background bar
            _spriteBatch.Draw(_pixel, barRect, Color.White * 0.25f);

            // filled portion
            var fillRect = new Rectangle(
                barRect.X,
                barRect.Y,
                (int)(barRect.Width * slider.NormalizedValue),
                barRect.Height
            );

            _spriteBatch.Draw(_pixel, fillRect, Color.White);
        }
        _spriteBatch.End();
    }

    private bool LeftClick() => _mouse.LeftButton == ButtonState.Pressed && (!_ctxRect.HasValue || !_ctxRect.Value.Contains(_mouse.Position));
    private bool MouseHover(Rectangle rect) => rect.Contains(_mouse.Position) && (!_ctxRect.HasValue || !_ctxRect.Value.Contains(_mouse.Position));

    private Rectangle GetMenuItemRect(int i) => new(_ctxRect.Value.X, _ctxRect.Value.Y + i * menuItemHeight, menuWidth, menuItemHeight);
    private Rectangle GetSliderRect(ContextMenuSlider slider) {
        int index = _ctxSliders.IndexOf(slider);

        int y =
            _ctxRect.Value.Y +
            menuItemHeight * _menuItems.Length +
            index * sliderHeight;

        return new Rectangle(
            _ctxRect.Value.X,
            y,
            _ctxRect.Value.Width,
            sliderHeight
        );
    }

    private void ResetCtxMenu() {
        _ctxItem = null;
        _ctxRect = null;
        _ctxSliders.Clear();
    }

    private void OnResized() {
        _menu.Parameters["AspectRatio"]?.SetValue((float)ScreenWidth / ScreenHeight);
        CalculateScroll();
        BuildSoundboardGrid();
    }

    private void CalculateScroll() {
        int offset = (int)((_previousMouse.ScrollWheelValue - _mouse.ScrollWheelValue) / 10f);
        int newScroll = int.Clamp(_scrollPosition + offset, 0, _buttons.Length > 0 ? Rows * _buttonSize : 0);
        int delta = newScroll - _scrollPosition;
        _scrollPosition = newScroll;
        foreach (var btn in _buttons)
            btn.Bounds.Y += delta;
        if (Preferences.CloseContextMenuOnScroll)
            ResetCtxMenu();
        else
            _ctxPos.Y += delta;
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

    public void AddSound(SoundboardItem sound) {
        _sounds.Add(sound);
        SaveSounds();
        Log.Info($"Added sound '{sound.Name}'");
    }
    public void RemoveSound(SoundboardItem sound) {
        if (_sounds.Remove(sound)) {
            SaveSounds();
            Log.Info($"Successfully deleted sound '{sound.Name}'");
        } else {
            Log.Info($"Failed to delete sound '{sound.Name}'");
        }
    }
    public void RemoveSoundAt(int i) {
        _sounds.RemoveAt(i);
        SaveSounds();
    }
    public IReadOnlyList<SoundboardItem> Sounds => _sounds.AsReadOnly();
    public void SaveSounds() {
        lock (_sounds) {
            using var fs = File.Create(SoundboardData);
            using var bw = new BinaryWriter(fs);
            bw.Write(FILE_VERSION);
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
                        $"You cannot have two sounds with the same path:\n{path}",
                        ["Close"]
                    );
                }).Start();
                return;
            }
        }

        var item = new SoundboardItem {
            Name = Path.GetFileNameWithoutExtension(path),
            SoundLocation = path
        };

        AddSound(item);
        BuildSoundboardGrid();
    }

    public void ShowInstallPrompt() {
        Task.Run(async () => {
            var result = await MessageBox.Show(
                "VB-Audio Virtual Cable Not Found",
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

    private void BuildContextSliders(SoundboardItem item) {
        _ctxSliders.Clear();

        _ctxSliders.Add(new ContextMenuSlider {
            Label = "Volume",
            Min = Preferences.VolumeMin,
            Max = Preferences.VolumeMax,
            GetValue = () => item.Volume,
            SetValue = v => item.Volume = v
        });

        _ctxSliders.Add(new ContextMenuSlider {
            Label = "Speed",
            Min = Preferences.SpeedMin,
            Max = Preferences.SpeedMax,
            GetValue = () => item.Speed,
            SetValue = v => item.Speed = v
        });
    }

    public void BuildSoundboardGrid() {
        int availableWidth = ScreenWidth - Preferences.Margin * 2 - CategoryListSize;

        int maxColumns = int.Min(
            int.Max(1,
                (availableWidth + Preferences.Padding) / (Preferences.MinButtonSize + Preferences.Padding)
            ),
            int.Max(1, _sounds.Count)
        );

        _buttonSize = int.Clamp(
            (availableWidth - Preferences.Padding * (maxColumns - 1)) / maxColumns,
            Preferences.MinButtonSize, Preferences.MaxButtonSize
        );
        _rounded.Parameters["TextureSize"].SetValue(new Vector2(_buttonSize, _buttonSize));

        int columns = int.Max(1,
            (availableWidth + Preferences.Padding) / (_buttonSize + Preferences.Padding)
        );

        _currentColumns = columns;

        var buttons = new SoundboardButton[_sounds.Count];

        for (int i = 0; i < _sounds.Count; i++) {
            int col = i % columns;
            int row = i / columns;

            var bounds = new Rectangle(
                Preferences.Margin + col * (_buttonSize + Preferences.Padding) + CategoryListSize,
                Preferences.Margin + row * (_buttonSize + Preferences.Padding) + _scrollPosition,
                _buttonSize,
                _buttonSize
            );

            var sound = _sounds[i];
            var oldButton = _buttons.Find(b => b.Item.UUID == sound.UUID);

            Texture2D icon = oldButton?.Icon;
            if (icon == null && !string.IsNullOrEmpty(sound.IconLocation) && File.Exists(sound.IconLocation)) {
                using var fs = File.OpenRead(sound.IconLocation);
                icon = Texture2D.FromStream(GraphicsDevice, fs);
            }

            buttons[i] = new SoundboardButton {
                Item = sound,
                Bounds = bounds,
                Icon = icon
            };
        }

        _buttons = buttons;
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
                }
                break;
        }

        return 0;
    }

#if OS_WINDOWS
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
#endif
}
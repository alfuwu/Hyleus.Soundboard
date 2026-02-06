using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Hyleus.Soundboard.Audio;
using Hyleus.Soundboard.Framework;
using Hyleus.Soundboard.Framework.Enums;
using Hyleus.Soundboard.Framework.Extensions;
using Hyleus.Soundboard.Framework.Interpolation;
using Hyleus.Soundboard.Framework.Structs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NativeFileDialogSharp;
using static SDL2.SDL;

// TODO:
// - text input boxes
// - render soundboard categories
// - soundboard category creation modal
// - dragging sounds into soundboard categories
// - change sound/category names using text input boxes
// - display the names of sounds under them
// - voice changer tab
//
// - POLISH: make context menus prettier at higher resolutions
// - POLISH: audio visualization tab
// - POLISH: allow resizing the category sidebar size
// - POLISH: clean up settings modal & make it prettier

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
    // constants
    private const ushort FILE_VERSION = 0;
    private const int menuWidth = 220;
    private const int menuItemHeight = 36;
    private const int horizontalPadding = 18;
    private const int sliderHeight = 48;
    private const int SettingsRowHeight = 56;
    private const int SettingsPadding = 24;
    private const int SettingsLabelWidth = 260;
    private static readonly Color Semitransparent = new(128, 128, 128, 128);
    private static readonly RasterizerState Scissors = new() {
        ScissorTestEnable = true
    };

    // core
    private readonly GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private AudioEngine _audio;

    // main soundboard stuff
    private readonly List<Category> _categories = [];
    private Button<Category>[] _catButtons = [];
    private readonly List<SoundboardItem> _sounds = [];
    private Button<SoundboardItem>[] _buttons = [];
    private Guid? _currentCat = null;

    // mouse states
    private MouseState _previousMouse;
    private MouseState _mouse;

    // textures
    private SpriteFont _font;
    private Texture2D _pixel;
    private Texture2D _defaultIcon;

    // effects
    private Effect _blur;
    private Effect _menu;
    private Effect _rounded;

    // colors
    private Color _btn;
    private Color _btnHover;
    private Color _btnPressed;
    private Color _btnTransparent;
    private Color _ctxMenu;
    private Color _ctxHover;
    private Color _ctxPressed;
    private Color _blurColor = Color.White;
    private Color _settingsColor = Color.Transparent;

    // audio device stuff
    private string _preferredInput = null;
    private string _preferredOutput = null;
    private string _preferredRegularOutput = null;
    private bool _hasOwnCable = false;

    // context menu
    private readonly (string name, Action onClick)[] _menuItems;
    private readonly (string name, Action onClick)[] _mainItems;
    private readonly (string name, Action onClick)[] _catMenuItems;
    private readonly (string name, Action onClick)[] _catListItems;
    private (string name, Action onClick)[] _currentMenuItems;
    private Point _ctxPos;
    private Category _ctxCat = null;
    private SoundboardItem _ctxItem = null;
    private Rectangle? _ctxRect = null;
    private bool _generalCtx = false; // hacky
    private readonly List<ContextMenuSlider> _ctxSliders = [];
    private ContextMenuSlider? _activeSlider = null;
    private TextBox _text = new();

    // settings stuff
    private readonly List<SettingsItem> _settingsItems = [];
    private bool _settingsOpen = false;
    private RenderTarget2D blurTarget;
    private Rectangle _settingsButton;
    private Rectangle _settings;

    // dragging
    private int _draggingIdx = -1;
    private Point? _dragStart = null;
    private Point _dragOffset;
    private const float DragThreshold = 10; // in pixels

    // misc
    private bool _wasActive;
    private Rectangle _voiceChangerButton;

    public static Vector2I WindowSize {
        get => new(Instance._graphics.PreferredBackBufferWidth, Instance._graphics.PreferredBackBufferHeight);
        set { Instance._graphics.PreferredBackBufferWidth = value.X; Instance._graphics.PreferredBackBufferHeight = value.Y; Instance._graphics.ApplyChanges(); }
    }

    public static int ScreenWidth { get; set; } = 1280;
    public static int ScreenHeight { get; set; } = 720;

    private int _categoryListSize = 104;
    private int CategoryListSize {
        get => _categoryListSize;
        set {
            _categoryListSize = value;
            BuildSoundboardCategories();
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
#if OS_WINDOWS
    private bool _started = false;
    [DllImport("user32.dll", ExactSpelling = true)]
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable SYSLIB1054 // Use 'LibraryImportAttribute' instead of 'DllImportAttribute' to generate P/Invoke marshalling code at compile time
    private static extern IntPtr SetTimer(IntPtr hWnd, IntPtr nIDEvent, uint uElapse, TimerProc lpTimerFunc);
    private IntPtr _handle;
    private delegate void TimerProc(IntPtr hWnd, uint uMsg, IntPtr nIDEvent, uint dwTime);
    private TimerProc _timerProc = null;
    private int _manualTickCount = 0;
    private bool _manualTick;
#endif

    // scrolling
    private int _scrollPosition = 0;
    private int _catScroll = 0;
    private int _settingsScroll = 0;

    public Main() {
        if (Instance != null)
            throw new InvalidOperationException("Only a single Main instance may be created. To create a new Main, set Main.Instance to null. Note that this will most likely have unintended consequences.");

        Thread.CurrentThread.Name = "Main";
        Console.Title = "Hyleus";

        Instance = this;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;

        IsFixedTimeStep = false;
        _graphics.SynchronizeWithVerticalRetrace = false;
        WindowSize = new Vector2I(ScreenWidth, ScreenHeight);
        blurTarget = new(GraphicsDevice, ScreenWidth, ScreenHeight);

        Window.FileDrop += (_, args) => {
            foreach (var file in args.Files)
                ImportSound(file);
        };
        Window.AllowUserResizing = true;

        _menuItems = [
            ("Play", () => _audio.PlaySound(_ctxItem)),
            ("Change Name", () => {}),
            ("Set Icon", () => {
                var i = _ctxItem; // save ctx item because the context menu will disappear before the file dialog is finished
                new Thread(() => {
                    var result = Dialog.FileOpen("png,jpg,bmp,gif,tif,dds", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
                    if (result.IsOk) {
                        i.IconLocation = result.Path;
                        SaveSounds();
                        BuildSoundboardGrid();
                    }
                }).Start();
            }),
            ("Set Sound", () => {
                var i = _ctxItem;
                new Thread(() => {
                    var result = Dialog.FileOpen("mp3,wav,flac,ogg,oga,webm,aac,m4a,mp4,mkv,aiff", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
                    if (result.IsOk) {
                        i.SoundLocation = result.Path;
                        SaveSounds();
                        BuildSoundboardGrid();
                    }
                }).Start();
            }),
            ("Bind to Key", () => {}),
            ("Delete", () => {
                RemoveSound(_ctxItem);
                BuildSoundboardGrid();
            })
        ];
        _mainItems = [
            ("Add Sound", () => {
                new Thread(() => {
                    var result = Dialog.FileOpen("mp3,wav,flac,ogg,oga,webm,aac,m4a,mp4,mkv,aiff", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
                    if (result.IsOk)
                        ImportSound(result.Path);
                }).Start();
            }),
        ];
        _catMenuItems = [
            ("Open", () => {
                _currentCat = _ctxCat.UUID;
                BuildSoundboardGrid();
            }),
            ("Change Name", () => {}),
            ("Set Icon", () => {
                var i = _ctxCat;
                new Thread(() => {
                    var result = Dialog.FileOpen("png,jpg,bmp,gif,tif,dds", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
                    if (result.IsOk) {
                        i.IconLocation = result.Path;
                        SaveSounds();
                        BuildSoundboardGrid();
                    }
                }).Start();
            }),
            ("Delete", () => {
                _categories.Remove(_ctxCat);
                SaveSounds();
            })
        ];
        _catListItems = [
            ("Create Category", () => {

            }),
        ];
    }

    protected override void Initialize() {
        Preferences.LoadPreferences();
        Preferences.SavePreferences();
        CalculateButtonColors(Preferences.BackgroundColor);

        Preferences.OnPreferenceUpdate += (propertyName, old, updated) => {
            switch (propertyName) {
                case nameof(Preferences.BackgroundColor):
                    CalculateButtonColors(Preferences.BackgroundColor);
                    break;
                case nameof(Preferences.MinButtonSize):
                    if ((float)updated > Preferences.MaxButtonSize)
                        return false;
                    goto case nameof(Preferences.MaxButtonSize);
                case nameof(Preferences.MaxButtonSize):
                case nameof(Preferences.Padding):
                case nameof(Preferences.Margin):
                    BuildSoundboardGrid();
                    break;
                case nameof(Preferences.EffectsColor1):
                    _menu.Parameters["Color1"]?.SetValue(((Color)updated).ToVector3());
                    break;
                case nameof(Preferences.EffectsColor2):
                    _menu.Parameters["Color2"]?.SetValue(((Color)updated).ToVector3());
                    break;
                case nameof(Preferences.EffectsColor3):
                    _menu.Parameters["Color3"]?.SetValue(((Color)updated).ToVector3());
                    break;
                case nameof(Preferences.PlaySoundsToSystem):
                    _audio.PlaySoundsToSystem((Ref<bool>)(object)updated);
                    break;
                case nameof(Preferences.PlayVoiceChangerToSystem):
                    _audio.PlayVoiceChangerToSystem((Ref<bool>)(object)updated);
                    break;
                case nameof(Preferences.PlayMicToSystem):
                    _audio.PlayMicToSystem((Ref<bool>)(object)updated);
                    break;
                case nameof(Preferences.VolumeMin):
                    var f = (Ref<float>)(object)updated;
                    f.Value = float.Min(float.Max(0, f.Value), Preferences.VolumeMax);
                    break;
                case nameof(Preferences.SpeedMin):
                    f = (Ref<float>)(object)updated;
                    f.Value = float.Min(float.Max(float.Epsilon, f.Value), Preferences.SpeedMax);
                    break;
                default:
                    break;
            }

            return true;
        };

        Log.Info("User preferences loaded successfully.");

        try {
            _audio = new AudioEngine(_hasOwnCable, Preferences.AudioFrequency);
        } catch (Exception e) {
            if (e.Message.Contains("VB-Audio Cable"))
                ShowInstallPrompt();
            return;
        }

        SDL_SetWindowMinimumSize(Window.Handle, 480, 270);
        _ = SDL_Init(SDL_INIT_VIDEO);

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

        if (File.Exists(SoundboardData)) {
            using var fs = File.OpenRead(SoundboardData);
            using var br = new BinaryReader(fs);
            ushort fileVer = br.ReadUInt16();
            _preferredInput = br.ReadStringOrNull();
            _preferredOutput = br.ReadStringOrNull();
            _preferredRegularOutput = br.ReadStringOrNull();
            _hasOwnCable = br.ReadBoolean();
            Log.Info("Read soundboard metadata\n - Preferred Input:", _preferredInput ?? "null", "\n - Preferred Output:", _preferredOutput ?? "null", "\n - Preferred Regular Output:", _preferredRegularOutput ?? "null");
            byte catSchemaVer = br.ReadByte();
            byte itemSchemaVer = br.ReadByte();
            Log.Info("CAT Schema Ver:", catSchemaVer);
            Log.Info("ITM Schema Ver:", itemSchemaVer);
            while (br.PeekChar() > 0) { // we can do this because category binary data starts with the length of their Name, and their name must have at least 1 character, thus making it impossible for the value to be zero
                try {
                    _categories.Add(Category.FromBinary(br, catSchemaVer));
                } catch (Exception e) {
                    Log.Error("Failed to load a category:", e.Message);
                }
            }
            Log.Info("Successfully loaded soundboad categories");
            br.ReadByte(); // eat the delimiter
            while (fs.Position < fs.Length) {
                try {
                    _sounds.Add(SoundboardItem.FromBinary(br, itemSchemaVer));
                } catch (Exception e) {
                    Log.Error("Failed to load a sound:", e.Message);
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

        _blur = Content.Load<Effect>("Shaders/Blur");
        _menu = Content.Load<Effect>("Shaders/Menu");
        _menu.Parameters["Color1"]?.SetValue(Preferences.EffectsColor1.ToVector3());
        _menu.Parameters["Color2"]?.SetValue(Preferences.EffectsColor2.ToVector3());
        _menu.Parameters["Color3"]?.SetValue(Preferences.EffectsColor3.ToVector3());
        _rounded = Content.Load<Effect>("Shaders/RoundedCorners");
        _rounded.Parameters["CornerRadius"].SetValue(16f);

        OnResized();
    }

    protected override void Update(GameTime gameTime) {
#if OS_WINDOWS
        _started = true;
        if (!_manualTick)
            _manualTickCount = 0;
#endif

        int oldScreenHeight = ScreenHeight;
        int oldScreenWidth = ScreenWidth;
        ScreenHeight = Window.ClientBounds.Height;
        ScreenWidth = Window.ClientBounds.Width;

        if (oldScreenHeight != ScreenHeight || oldScreenWidth != ScreenWidth)
            OnResized();

        _previousMouse = _mouse;
        _mouse = Mouse.GetState();

        Interpolation.Update(gameTime);
        TextBox.ProcessInput(Keyboard.GetState(), gameTime.ElapsedGameTime.TotalSeconds);

        DoClickyClacky();

        _wasActive = IsActive;

        _menu.Parameters["Time"]?.SetValue((float)gameTime.TotalGameTime.TotalSeconds);
        _menu.Parameters["Mouse"]?.SetValue(new Vector4(_mouse.X, _mouse.Y, _mouse.LeftButton == ButtonState.Pressed && IsActive ? 1 : 0, 0));

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) {
        bool s = _settingsOpen;
        if (s)
            GraphicsDevice.SetRenderTarget(blurTarget);
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
            new Rectangle(8, 0, CategoryListSize, ScreenHeight),
            new Color(20, 19, 19, 176)
        );
        _spriteBatch.End();

        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, effect: _rounded);
        _spriteBatch.Draw(
            _pixel,
            _settingsButton,
            MouseHover(_settingsButton) ? LeftClick() ?
                _btnPressed : _btnHover : _btn
        );
        _spriteBatch.Draw(
            _pixel,
            _voiceChangerButton,
            MouseHover(_voiceChangerButton) ? LeftClick() ?
                _btnPressed : _btnHover : _btn
        );
        _spriteBatch.End();

        GraphicsDevice.ScissorRectangle = new Rectangle(0, 0, CategoryListSize, ScreenHeight - CategoryListSize * 2);
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, rasterizerState: Scissors, effect: _rounded);
        for (int i = 0; i < _catButtons.Length; i++) {
            var btn = _catButtons[i];
            DrawButton(btn, i);
            if (btn.Item.UUID == _currentCat)
                _spriteBatch.Draw(_pixel, new Rectangle(6, btn.Bounds.Y + 8, 5, btn.Bounds.Height - 16), Color.White);
        }

        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, effect: _rounded);
        for (int i = 0; i < _buttons.Length; i++) {
            if (i == _draggingIdx)
                continue;
            int samplePos = GetSamplePos(i);
            DrawButton(_buttons[i], i, samplePos != i ? _buttons[samplePos].Bounds.Location : null);
        }
        if (_draggingIdx >= 0)
            DrawButton(_buttons[_draggingIdx], _draggingIdx, _mouse.Position - _dragOffset);
        _spriteBatch.End();

        if (_ctxItem != null || _ctxCat != null || _generalCtx)
            DrawContextMenu(_ctxPos);

        if (s)
            DrawSettings();

        _spriteBatch.Begin();
        DrawTextBox(_text);
        _spriteBatch.End();
        _spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, rasterizerState: Scissors);
        DrawTextBoxText(_text, gameTime.TotalGameTime.TotalSeconds);
        _spriteBatch.End();
    }

    private int GetHoverIndex() {
        if (_mouse.Position.X <= CategoryListSize)
            return -1;
        int lastSP = -1;
        for (int i = 0; i < _buttons.Length; i++) {
            if (i == _draggingIdx)
                continue;
            int samplePos = GetSamplePos(i);
            if (lastSP < i && samplePos > i || lastSP < i - 1 && samplePos == i)
                return i;
            lastSP = samplePos;
        }
        return _buttons.Length;
    }

    private int GetSamplePos(int i) {
        if (_draggingIdx >= 0 && _mouse.Position.X > CategoryListSize) {
            int row = i / _currentColumns;
            int y = row * _buttonSize + _scrollPosition;
            int sizeY = Preferences.Margin + _buttonSize + Preferences.Padding;
            bool below = _mouse.Position.Y > y + sizeY;
            if (_draggingIdx < i)
                i--;
            if ((_mouse.Position.X < _buttons[i].Bounds.Center.X && !below) || _mouse.Position.Y < y)
                i++;
        }
        return i;
    }

    private void DrawButton<T>(Button<T> button, int idx, Point? pos = null) {
        var dragged = idx == _draggingIdx;
        var rect = pos.HasValue ?
            button.Bounds.WithPosition(pos.Value) :
            button.Bounds;
        _spriteBatch.Draw(
            _pixel,
            rect,
            dragged ? _btnTransparent : MouseHover(rect) ? LeftClick() ?
                _btnPressed : _btnHover : _btn
        );

        Texture2D icon = button.Icon ?? _defaultIcon;
        _spriteBatch.Draw(
            icon,
            rect,
            dragged ? Semitransparent : MouseHover(rect) ? LeftClick() ?
                Color.Gray : Color.DarkGray : Color.White
        );
    }

    private void DrawTextBox(TextBox text, Color? color = null) {
        var c = color ?? _btn * Semitransparent;
        var outline = new Color(c.R * 2, c.G * 2, c.B * 2, c.A);
        _spriteBatch.Draw(
            _pixel,
            text.Bounds.Exflate(-1, -1),
            c
        );

        // outline
        // left
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(text.Bounds.X, text.Bounds.Y, 1, text.Bounds.Height),
            outline
        );
        // top
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(text.Bounds.X + 1, text.Bounds.Y, text.Bounds.Width - 2, 1),
            outline
        );
        // right
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(text.Bounds.X + text.Bounds.Width, text.Bounds.Y, 1, text.Bounds.Height),
            outline
        );
        // bottom
        _spriteBatch.Draw(
            _pixel,
            new Rectangle(text.Bounds.X + 1, text.Bounds.Y + text.Bounds.Height, text.Bounds.Width - 2, 1),
            outline
        );
    }
    private void DrawTextBoxText(TextBox text, double seconds, Color? color = null) {
        GraphicsDevice.ScissorRectangle = text.Bounds;
        var c = color ?? Color.White;

        if (text.SelectionStart >= 0) {
            string before = text.Text[..text.SelectionStart];
            string selection = text.Text[text.SelectionStart..text.SelectionEnd];
            var bounds = new Rectangle((int)(_font.MeasureString(before).X * 0.3f) + text.Bounds.X, text.Bounds.Y, (int)(_font.MeasureString(selection).X * 0.3f), text.Bounds.Height);
            _spriteBatch.Draw(
                _pixel,
                bounds,
                Color.LightBlue
            );
        }

        _spriteBatch.DrawString(
            _font,
            text.Text,
            text.Bounds.Location.ToVector2() + new Vector2(3, 0),
            c,
            0,
            Vector2.Zero,
            0.3f,
            SpriteEffects.None,
            0
        );

        // caret
        if (text.IsFocused && double.Sin(seconds * 3) > 0) {
            string before = text.Text[..text.Caret];
            var bounds = new Rectangle((int)(_font.MeasureString(before).X * 0.3f) + text.Bounds.X, text.Bounds.Y, 1, text.Bounds.Height);
            _spriteBatch.Draw(
                _pixel,
                bounds,
                c
            );
        }
    }

    private void DrawContextMenu(Point position) {
        _currentMenuItems = _generalCtx ? position.X < CategoryListSize ?
            _catListItems : _mainItems : _ctxCat != null ?
            _catMenuItems : _menuItems;

        int totalHeight =
            menuItemHeight * _currentMenuItems.Length +
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
        for (int i = 0; i < _currentMenuItems.Length; i++) {
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
                _currentMenuItems[i].name,
                new Vector2(itemRect.X + horizontalPadding, itemRect.Y + 8),
                Color.White,
                0,
                Vector2.Zero,
                0.12f,
                SpriteEffects.None,
                0
            );
        }

        var divider = new Rectangle(
            position.X + 12,
            position.Y + menuItemHeight * _currentMenuItems.Length - 1,
            menuWidth - 24,
            2
        );

        _spriteBatch.Draw(_pixel, divider, Color.White * 0.08f);

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
                Color.White, 0, Vector2.Zero, 0.12f,
                SpriteEffects.None, 0
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

    private void DrawSettings() {
        GraphicsDevice.SetRenderTarget(null);
        // draw the background but blurred
        _spriteBatch.Begin(SpriteSortMode.Deferred, effect: _blur);
        _spriteBatch.Draw(
            blurTarget,
            new Rectangle(0, 0, blurTarget.Width, blurTarget.Height),
            _blurColor
        );
        _spriteBatch.End();

        // draw rounded settings modal
        var texVec = _rounded.Parameters["TextureSize"].GetValueVector2();
        _rounded.Parameters["TextureSize"].SetValue(new Vector2(_settings.Width, _settings.Height));
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, effect: _rounded);
        _spriteBatch.Draw(_pixel, _settings, Preferences.BackgroundColor * _settingsColor);
        _spriteBatch.End();
        _rounded.Parameters["TextureSize"].SetValue(texVec);

        // clip settings items
        GraphicsDevice.ScissorRectangle = _settings;
        _spriteBatch.Begin(SpriteSortMode.Immediate, rasterizerState: Scissors);
        for (int i = 0; i < _settingsItems.Count; i++) {
            var item = _settingsItems[i];
            var row = new Rectangle(
                _settings.X + SettingsPadding,
                _settings.Y + SettingsPadding + i * SettingsRowHeight + _settingsScroll,
                _settings.Width - SettingsPadding * 2,
                SettingsRowHeight
            );

            _spriteBatch.DrawString(_font, item.Name, row.Location.ToVector2(), _settingsColor, 0, Vector2.Zero, 0.12f, SpriteEffects.None, 0);

            switch (item.ControlType) {
                case SettingsControlType.Bool:
                    DrawBool(item, row);
                    break;
                case SettingsControlType.Int:
                    var av = item.Property.GetCustomAttribute<AllowedValuesAttribute>();
                    if (av != null)
                        DrawCycle(item, row, av.Values);
                    else {
                        var range = item.Property.GetCustomAttribute<RangeAttribute>();
                        DrawSlider(item, row, 0, 256);
                    }
                    break;
                case SettingsControlType.Float:
                    av = item.Property.GetCustomAttribute<AllowedValuesAttribute>();
                    if (av != null)
                        DrawCycle(item, row, av.Values);
                    else {
                        var range = item.Property.GetCustomAttribute<RangeAttribute>();
                        DrawSlider(item, row, 0, 10);
                    }
                    break;
                case SettingsControlType.Enum:
                    DrawCycle(item, row, Enum.GetValuesAsUnderlyingType(item.Property.PropertyType));
                    break;
                case SettingsControlType.Color:
                    DrawColor(item, row);
                    break;
            }
        }

        _spriteBatch.End();
    }

    private bool LeftClick() => _mouse.LeftButton == ButtonState.Pressed && (!_ctxRect.HasValue || !_ctxRect.Value.Contains(_mouse.Position)) && !_settingsOpen && _draggingIdx < 0;
    private bool MouseHover(Rectangle rect) => rect.Contains(_mouse.Position) && (!_ctxRect.HasValue || !_ctxRect.Value.Contains(_mouse.Position)) && !_settingsOpen && _draggingIdx < 0;
    private bool Clicked(Rectangle r) =>
        _mouse.LeftButton == ButtonState.Pressed &&
        _previousMouse.LeftButton == ButtonState.Released &&
        r.Contains(_mouse.Position);

    private Rectangle GetMenuItemRect(int i) => new(_ctxRect.Value.X, _ctxRect.Value.Y + i * menuItemHeight, menuWidth, menuItemHeight);
    private Rectangle GetSliderRect(ContextMenuSlider slider) => new(
        _ctxRect.Value.X,
        _ctxRect.Value.Y +
            menuItemHeight * _menuItems.Length +
            _ctxSliders.IndexOf(slider) * sliderHeight,
        _ctxRect.Value.Width,
        sliderHeight
    );

    private void ResetCtxMenu() {
        _ctxCat = null;
        _ctxItem = null;
        _ctxRect = null;
        _ctxSliders.Clear();
        _generalCtx = false;
    }

    private void OnResized() {
        _blur.Parameters["TextureSize"].SetValue(new Vector2(ScreenWidth, ScreenHeight));
        _menu.Parameters["Resolution"]?.SetValue(new Vector2(ScreenWidth, ScreenHeight));
        _menu.Parameters["AspectRatio"]?.SetValue((float)ScreenWidth / ScreenHeight);
        BuildSoundboardCategories();
        BuildSoundboardGrid();
        BuildSettingsItems();
        CalculateScroll();
        _text.Bounds = new Rectangle(ScreenWidth / 2 - 120, ScreenHeight / 2 - 30, 240, 60);
        int size = CategoryListSize - 16;
        _settingsButton = new Rectangle(16, ScreenHeight - 8 - size, size, size);
        _voiceChangerButton = new Rectangle(16, ScreenHeight - 16 - size * 2, size, size);

        int settingsWidth = (int)(ScreenWidth * 0.6f);
        int settingsHeight = (int)(ScreenHeight * 0.7f);

        _settings = new Rectangle(
            (ScreenWidth - settingsWidth) / 2,
            (ScreenHeight - settingsHeight) / 2,
            settingsWidth,
            settingsHeight
        );
        blurTarget.Dispose();
        blurTarget = new(GraphicsDevice, ScreenWidth, ScreenHeight);
    }

    private void DoClickyClacky() {
        // closing the settings menu
        if (_mouse.LeftButton == ButtonState.Pressed &&
            _previousMouse.LeftButton == ButtonState.Released &&
            !_settings.Contains(_mouse.Position) &&
            _settingsOpen &&
            IsActive &&
            new Rectangle(Point.Zero, Window.ClientBounds.Size).Contains(_mouse.Position)
        ) {
            var param = _blur.Parameters["BlurStrength"];
            Interpolation.To("blur", param.SetValue, param.GetValueSingle(), 0, 0.5f);
            Interpolation.To("blurColor", col => _blurColor = col, _blurColor, Color.White, 0.5f);
            Interpolation.To("setColor", col => _settingsColor = col, _settingsColor, Color.Transparent, 0.5f);
            Task.Run(async () => {
                await Task.Delay(500);
                _settingsOpen = false;
            });
        }

        bool skipPressed = !_wasActive && IsActive;
        bool lc = LeftClick();
        bool click = _mouse.LeftButton == ButtonState.Pressed &&
            (_previousMouse.LeftButton == ButtonState.Released || skipPressed);

        // context menu clicky clackies
        if ((_ctxItem != null || _ctxCat != null || _generalCtx) && _ctxRect.HasValue && IsActive) {
            bool boolean = _mouse.LeftButton == ButtonState.Released &&
                    (_previousMouse.LeftButton == ButtonState.Pressed || skipPressed);
            for (int i = 0; i < _currentMenuItems.Length; i++) {
                Rectangle rect = GetMenuItemRect(i);

                if (boolean && rect.Contains(_mouse.Position)) {
                    _currentMenuItems[i].onClick.Invoke();
                    ResetCtxMenu();
                    return;
                }
            }

            foreach (var slider in _ctxSliders) {
                Rectangle sliderRect = GetSliderRect(slider);

                if (click && sliderRect.Contains(_mouse.Position)) {
                    _activeSlider = slider;
                    slider.SetIsDragging(true);
                }
            }

            if (_activeSlider.HasValue) {
                Rectangle sliderRect = GetSliderRect(_activeSlider.Value);

                int barX = sliderRect.X + 16;
                float barWidth = sliderRect.Width - 32;

                float t = (_mouse.X - barX) / barWidth;
                _activeSlider.Value.SetFromNormalized(t);
                return;
            }
        }
        // dragging soundboard buttons to reposition/move them into a new category
        if (_draggingIdx < 0) {
            if (lc) {
                if (!_dragStart.HasValue)
                    _dragStart = _mouse.Position;
                else if (float.Abs(Vector2.Distance(_dragStart.Value.ToVector2(), _mouse.Position.ToVector2())) > DragThreshold) {
                    ResetCtxMenu();
                    for (int i = 0; i < _buttons.Length; i++)
                        if (_buttons[i].Bounds.Contains(_dragStart.Value))
                            _draggingIdx = i;
                    if (_draggingIdx >= 0)
                        _dragOffset = _mouse.Position - _buttons[_draggingIdx].Bounds.Location;
                    _dragStart = null;
                }
            } else if (_dragStart.HasValue) {
                _dragStart = null;
            }
        }
        bool left = _mouse.LeftButton == ButtonState.Released &&
            (_previousMouse.LeftButton == ButtonState.Pressed || skipPressed);
        if (left && _draggingIdx >= 0) {
            // applying drag stuff
            int moveIdx = GetHoverIndex();
            if (moveIdx != _draggingIdx) {
                if (moveIdx == -1) {
                    // category moving
                } else {
                    SoundboardItem sound = _sounds[_draggingIdx];
                    int soundIdx = _sounds.IndexOf(sound);
                    if (moveIdx >= soundIdx)
                        moveIdx--;
                    _sounds.Remove(sound);
                    _sounds.Insert(moveIdx, sound);
                    SaveSounds();
                    BuildSoundboardGrid();
                }
            }
            _draggingIdx = -1;
        } else if ((left ||
            _mouse.RightButton == ButtonState.Released &&
            (_previousMouse.RightButton == ButtonState.Pressed || skipPressed)) &&
            (!_ctxRect.HasValue || !_ctxRect.Value.Contains(_mouse.Position)) &&
            !_settingsOpen && IsActive
        ) {
            ResetCtxMenu();

            // clicking soundboard categories
            foreach (var cat in _catButtons) {
                if (MouseHover(cat.Bounds)) {
                    if (left) {
                        _currentCat = cat.Item.UUID;
                        BuildSoundboardGrid();
                    } else {
                        _ctxPos = _mouse.Position;
                        _ctxCat = cat.Item;
                    }
                }
            }

            // clicking soundboad buttons
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

            if (_ctxItem == null && _ctxCat == null && !left) {
                _ctxPos = _mouse.Position;
                _generalCtx = true;
            }

            // opening settings menu
            if (MouseHover(_settingsButton)) {
                _settingsOpen = true;
                var param = _blur.Parameters["BlurStrength"];
                Interpolation.To("blur", param.SetValue, param.GetValueSingle(), 3000, 0.5f);
                Interpolation.To("blurColor", col => _blurColor = col, _blurColor, Color.DarkGray, 0.5f);
                Interpolation.To("setColor", col => _settingsColor = col, _settingsColor, Color.White, 0.5f);
            } else if (MouseHover(_voiceChangerButton)) {

            } else if (MouseHover(_text.Bounds)) {
                _text.Focus();
            }
        }

        // resetting ctx menu sliders
        if (_mouse.LeftButton == ButtonState.Released && _activeSlider != null) {
            _activeSlider.Value.SetIsDragging(false);
            _activeSlider = null;
            SaveSounds();
        }

        // calculating scroll delta stuff
        if (_mouse.ScrollWheelValue != _previousMouse.ScrollWheelValue && (!Preferences.PreventScrollWhenContextMenuIsOpen || _ctxItem == null))
            CalculateScroll();
    }

    private void CalculateScroll() {
        int sp = _settingsOpen ? _settingsScroll : _mouse.X < CategoryListSize ? _catScroll : _scrollPosition;
        int offset = (int)((_previousMouse.ScrollWheelValue - _mouse.ScrollWheelValue) / (_settingsOpen ? 5f : 10f));
        int newScroll = int.Clamp(
            sp - offset, -(_settingsOpen ?
                int.Max((_settingsItems.Count * SettingsRowHeight) - (int)(_settings.Height * 0.9f), 0) :
                _mouse.X < CategoryListSize ? (_catButtons.Length - 1) * (CategoryListSize - 8) :
                Rows * _buttonSize),
            0
        );
        int delta = newScroll - sp;
        if (_settingsOpen) {
            _settingsScroll = newScroll;
            return;
        } else if (_mouse.X < CategoryListSize) {
            _catScroll = newScroll;
            foreach (var btn in _catButtons)
                btn.Bounds.Y += delta;
        } else {
            _scrollPosition = newScroll;
            foreach (var btn in _buttons)
                btn.Bounds.Y += delta;
        }
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
        _btnTransparent = _btn * new Color(128, 128, 128, 128);
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
    public void AddSoundAt(SoundboardItem sound, int idx) {
        _sounds.Insert(idx, sound);
        SaveSounds();
        Log.Info($"Insert sound at position '{idx + 1}': '{sound.Name}'");
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
            bw.Write(Category.FILE_VERSION);
            bw.Write(SoundboardItem.FILE_VERSION);
            foreach (var c in _categories)
                c.WriteBinary(bw);
            bw.Write((byte)0); // delimiter
            foreach (var i in _sounds)
                i.WriteBinary(bw);
        }
    }

    private void ImportSound(string path) {
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
            Exit();
        }).Start();
    }

    #region Builders
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

    public void BuildSoundboardCategories() {
        int size = CategoryListSize - 16;
        var buttons = new Button<Category>[_categories.Count + 1];
        buttons[0] = new Button<Category> {
            Item = new Category {
                UUID = null
            },
            Bounds = new Rectangle(16, 8 + _catScroll, size, size),
            Icon = _defaultIcon
        };
        for (int i = 0; i < _categories.Count; i++) {
            var bounds = new Rectangle(
                16,
                8 + size * (i + 1) + _catScroll,
                size,
                size
            );

            var cat = _categories[i];
            var oldCat = _catButtons.Find(b => b.Item.UUID == cat.UUID);

            Texture2D icon = oldCat?.Icon;
            if ((oldCat?.IconLocation != cat.IconLocation) && !string.IsNullOrEmpty(cat.IconLocation) && File.Exists(cat.IconLocation)) {
                using var fs = File.OpenRead(cat.IconLocation);
                icon = Texture2D.FromStream(GraphicsDevice, fs);
            }

            buttons[i + 1] = new Button<Category> {
                Item = cat,
                Bounds = bounds,
                Icon = icon,
                IconLocation = cat.IconLocation
            };
        }

        _catButtons = buttons;
    }

    public void BuildSoundboardGrid() {
        int availableWidth = ScreenWidth - Preferences.Margin * 2 - CategoryListSize;

        List<SoundboardItem> availableSounds = _sounds.FindAll(s => s.CategoryID == _currentCat);

        int maxColumns = int.Min(
            int.Max(1,
                (availableWidth + Preferences.Padding) / (Preferences.MinButtonSize + Preferences.Padding)
            ),
            int.Max(1, availableSounds.Count)
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

        var buttons = new Button<SoundboardItem>[availableSounds.Count];
        for (int i = 0; i < availableSounds.Count; i++) {
            int col = i % columns;
            int row = i / columns;

            var bounds = new Rectangle(
                Preferences.Margin + col * (_buttonSize + Preferences.Padding) + CategoryListSize,
                Preferences.Margin + row * (_buttonSize + Preferences.Padding) + _scrollPosition,
                _buttonSize,
                _buttonSize
            );

            var sound = availableSounds[i];
            var oldButton = _buttons.Find(b => b.Item.UUID == sound.UUID);

            Texture2D icon = oldButton?.Icon;
            if ((oldButton?.IconLocation != sound.IconLocation) && !string.IsNullOrEmpty(sound.IconLocation) && File.Exists(sound.IconLocation)) {
                using var fs = File.OpenRead(sound.IconLocation);
                icon = Texture2D.FromStream(GraphicsDevice, fs);
            }

            buttons[i] = new Button<SoundboardItem> {
                Item = sound,
                Bounds = bounds,
                Icon = icon,
                IconLocation = sound.IconLocation
            };
        }

        _buttons = buttons;
    }

    private void BuildSettingsItems() {
        _settingsItems.Clear();

        foreach (var prop in typeof(Preferences).GetProperties(BindingFlags.Public | BindingFlags.Static)) {
            if (!prop.CanRead || !prop.CanWrite)
                continue;

            var desc = prop.GetCustomAttribute<DescriptionAttribute>();

            SettingsControlType type =
                prop.PropertyType == typeof(bool) ? SettingsControlType.Bool :
                prop.PropertyType == typeof(int) ? SettingsControlType.Int :
                prop.PropertyType == typeof(float) ? SettingsControlType.Float :
                prop.PropertyType == typeof(Color) ? SettingsControlType.Color :
                prop.PropertyType.IsEnum ? SettingsControlType.Enum :
                throw new NotSupportedException(prop.PropertyType.Name);

            _settingsItems.Add(new SettingsItem(prop, prop.Name, desc?.Description, type));
        }
    }
    #region Settings drawers
    private void DrawBool(SettingsItem item, Rectangle row) {
        var box = new Rectangle(row.Right - 32, row.Y + 12, 24, 24);

        if (Clicked(box.Exflate(3, 3)) && _settings.Contains(_mouse.Position))
            item.Set(!(bool)item.Get());

        _spriteBatch.Draw(_pixel, box, _btn * _settingsColor);
        if ((bool)item.Get())
            _spriteBatch.Draw(_pixel, box.Exflate(-6, -6), _settingsColor);
    }

    private void DrawSlider(SettingsItem item, Rectangle row, float min, float max) {
        var bar = new Rectangle(
            row.X + SettingsLabelWidth,
            row.Y + row.Height / 2,
            row.Width - SettingsLabelWidth - 16,
            6
        );

        float value = Convert.ToSingle(item.Get());
        float t = (value - min) / (max - min);

        if (_mouse.LeftButton == ButtonState.Pressed && _settings.Contains(_mouse.Position) && bar.Exflate(3, 6).Contains(_mouse.Position)) {
            float nt = (_mouse.X - bar.X) / (float)bar.Width;
            nt = MathHelper.Clamp(nt, 0, 1);
            float newValue = MathHelper.Lerp(min, max, nt);

            if (item.ControlType == SettingsControlType.Int)
                item.Set((int)newValue);
            else
                item.Set(newValue);
        }

        _spriteBatch.Draw(_pixel, bar, _settingsColor * 0.25f);
        _spriteBatch.Draw(_pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * t), bar.Height), _settingsColor);
    }

    private void DrawCycle(SettingsItem item, Rectangle row, Array values) {
        var button = new Rectangle(row.Right - 160, row.Y + 10, 150, 32);

        if (Clicked(button.Exflate(3, 3)) && _settings.Contains(_mouse.Position)) {
            int idx = Array.IndexOf(values, item.Get());
            item.Set(values.GetValue((idx + 1) % values.Length));
        }

        _spriteBatch.Draw(_pixel, button, _btn * _settingsColor);
        _spriteBatch.DrawString(_font,
            item.Get().ToString(),
            new Vector2(button.X + 8, button.Y + 6),
            _settingsColor, 0, Vector2.Zero, 0.12f, SpriteEffects.None, 0
        );
    }

    private void DrawColor(SettingsItem item, Rectangle row) {
        Color c = (Color)item.Get();

        float DrawChannel(string label, int y, byte value) {
            var bar = new Rectangle(row.X + SettingsLabelWidth, y, 200, 6);
            float t = value / 255f;

            if (_mouse.LeftButton == ButtonState.Pressed && bar.Exflate(0, 3).Contains(_mouse.Position) && _settings.Contains(_mouse.Position))
                t = MathHelper.Clamp((_mouse.X - bar.X) / (float)bar.Width, 0, 1);

            _spriteBatch.Draw(_pixel, bar, _settingsColor * 0.25f);
            _spriteBatch.Draw(_pixel, new Rectangle(bar.X, bar.Y, (int)(bar.Width * t), bar.Height), _settingsColor);
            return t;
        }

        float r = DrawChannel("R", row.Y + 10, c.R);
        float g = DrawChannel("G", row.Y + 26, c.G);
        float b = DrawChannel("B", row.Y + 42, c.B);

        item.Set(new Color(r, g, b));
    }
    #endregion
    #endregion

    #region SDL hacking
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
    #endregion
}
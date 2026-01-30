using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static SDL2.SDL;

namespace Hyleus.Soundboard.Framework;
public class SettingsWindow : GameWindow, IGraphicsDeviceService {
    private static GraphicsDevice graphicsDevice;
    private static PresentationParameters parameters;
    private GameWindow _parent;
    private readonly IntPtr _handle;
    public override IntPtr Handle => _handle;
    private SpriteBatch sb;

    public GraphicsDevice GraphicsDevice => graphicsDevice;

    public SettingsWindow(GameWindow parent, GraphicsDevice device, int width, int height) {
        _handle = SDL_CreateWindow("Settings", parent.Position.X, parent.Position.Y, width, height, SDL_WindowFlags.SDL_WINDOW_OPENGL | SDL_WindowFlags.SDL_WINDOW_SHOWN);
        graphicsDevice = device;
        parameters = device.PresentationParameters.Clone();
        parameters.DeviceWindowHandle = Handle;
        _parent = parent;
    }

    public delegate void DrawEvent(SettingsWindow window, SpriteBatch spriteBatch);
    public event EventHandler OnInitialize;
    public event DrawEvent OnDraw;

    protected void Initialize() => OnInitialize?.Invoke(this, null);
    public void Draw() => OnDraw?.Invoke(this, sb);

    public bool CanDraw() {
        if (graphicsDevice == null || !IsDeviceAvailable())
            return false;

        object ctx = GraphicsDevice.GetType().GetProperty("Context", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GraphicsDevice);
        ctx.GetType().GetMethod("MakeCurrent", BindingFlags.Instance | BindingFlags.Public).Invoke(ctx, [new WindowInfo(Handle)]);
        GraphicsDevice.GetType().GetMethod("ApplyRenderTargets", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(GraphicsDevice, [null]);
        sb ??= new(GraphicsDevice);
        GraphicsDevice.Viewport = new Viewport {
            X = ClientBounds.X,
            Y = ClientBounds.Y,
            Width = GraphicsDevice.Viewport.Bounds.Width,
            Height = GraphicsDevice.Viewport.Bounds.Height,
            MinDepth = 0,
            MaxDepth = 1
        };
        GraphicsDevice.SetRenderTarget(null);

        return true;
    }

    public void FinishDraw() {
        try {
            GraphicsDevice.Present();
            object ctx = GraphicsDevice.GetType().GetProperty("Context", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(GraphicsDevice);
            ctx.GetType().GetMethod("MakeCurrent", BindingFlags.Instance | BindingFlags.Public).Invoke(ctx, [new WindowInfo(_parent.Handle)]);
            GraphicsDevice.GetType().GetMethod("ApplyRenderTargets", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(GraphicsDevice, [null]);
        } catch {
            Log.Error("Failed to finalize draw");
        }
    }

    private bool IsDeviceAvailable() {
        bool deviceNeedsReset;

        switch (graphicsDevice.GraphicsDeviceStatus) {
            case GraphicsDeviceStatus.Lost:
                return false;
            case GraphicsDeviceStatus.NotReset:
                deviceNeedsReset = true;
                break;
            default:
                deviceNeedsReset = (GraphicsDevice.Viewport.Bounds.Width > parameters.BackBufferWidth) || (GraphicsDevice.Viewport.Bounds.Height > parameters.BackBufferHeight);
                break;
        }

        if (deviceNeedsReset) {
            try {
                ResetDevice(GraphicsDevice.Viewport.Bounds.Width, GraphicsDevice.Viewport.Bounds.Height);
            } catch {
                return false;
            }
        }

        return true;
    }

    public event EventHandler<EventArgs> DeviceCreated;
    public event EventHandler<EventArgs> DeviceDisposing;
    public event EventHandler<EventArgs> DeviceReset;
    public event EventHandler<EventArgs> DeviceResetting;

    public void ResetDevice(int width, int height) {
        DeviceResetting?.Invoke(this, EventArgs.Empty);

        parameters.BackBufferWidth = Math.Max(parameters.BackBufferWidth, width);
        parameters.BackBufferHeight = Math.Max(parameters.BackBufferHeight, height);

        graphicsDevice.Reset(parameters);
        DeviceReset?.Invoke(this, EventArgs.Empty);
    }

    public override bool AllowUserResizing { get; set; }

    public override Rectangle ClientBounds => graphicsDevice.Viewport.Bounds;

    public override Point Position { get; set; }

    public override DisplayOrientation CurrentOrientation => throw new NotImplementedException();

    public override string ScreenDeviceName => throw new NotImplementedException();

    public override void BeginScreenDeviceChange(bool willBeFullScreen) {
        if (willBeFullScreen) {
            uint flags = SDL_GetWindowFlags(Handle);
            Log.Info("fullscreening", SDL_SetWindowFullscreen(Handle, flags));
        } else {
            SDL_RestoreWindow(Handle);
        }
    }

    public override void EndScreenDeviceChange(string screenDeviceName, int clientWidth, int clientHeight) {
        if (CanDraw()) {
            Draw();
            FinishDraw();
        }
    }

    protected override void SetSupportedOrientations(DisplayOrientation orientations) {
        
    }

    protected override void SetTitle(string title) {
        SDL_SetWindowTitle(Handle, title);
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Hyleus.Soundboard.Audio;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Hyleus.Soundboard;
class Program {
    [STAThread]
    static void Main(string[] args) {
        using Main main = new();
        main.Run();
    }
}

public class Main : Game {
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch;
    private AudioEngine _audio;
    private KeyboardState _previousKeyboard;
    public static Main Instance { get; private set; }

    public static string DataFolder { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Hyleus", "Data");

    public Main() {
        if (Instance != null)
            throw new InvalidOperationException("Only a single Main instance may be created. To create a new Main, set Main.Instance to null. Note that this will most likely have unintended consequences.");

        Thread.CurrentThread.Name = "Main";

        Instance = this;
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
    }

    protected override void Initialize() {
        try {
            _audio = new AudioEngine(44100);
        } catch (Exception e) {
            if (e.Message.Contains("VB-Audio Cable")) {
                ShowInstallPrompt();
                Exit();
            }
            return;
        }

        base.Initialize();
    }

    protected override void LoadContent() {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
    }

    protected override void Update(GameTime gameTime) {
        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime) {
        GraphicsDevice.Clear(Color.White);

        base.Draw(gameTime);
    }

    public static void ShowInstallPrompt() {
        Task.Run(async () => {
            var result = await MessageBox.Show(
                "VB-Audio Virtual Cable was not detected.\n\n" +
                "This application requires VB-Audio Cable to route audio correctly.\n\n" +
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
}
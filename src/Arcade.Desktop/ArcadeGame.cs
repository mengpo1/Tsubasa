using System.Diagnostics;
using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Core.Football;
using Arcade.Core.Input;
using Arcade.Core.Simulation;
using Arcade.Desktop.Camera;
using Arcade.Desktop.Hosting;
using Arcade.Desktop.Input;
using Arcade.Desktop.Rendering;
using Arcade.Desktop.Scenes;
using Arcade.Desktop.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Num = System.Numerics;

namespace Arcade.Desktop;

/// <summary>
/// MonoGame host: owns the window, reads physical input, advances the simulation with a fixed-step clock
/// (independent of the display rate) and draws the interpolated state. Holds no game rules.
/// </summary>
internal sealed class ArcadeGame : Game
{
    private const string DefaultTeam = "reds";
    private const string DefaultPitch = "normal";
    private const ulong Seed = 20240611;
    private const float NoticeSeconds = 3f;

    private readonly GraphicsDeviceManager _graphics;
    private readonly LaunchOptions _options;
    private readonly string _gameRoot;
    private readonly UserSettings _settings;
    private readonly GameLog _log;
    private readonly ScriptedInput? _script;
    private readonly HashSet<long> _captureTicks;
    private readonly IReadOnlyList<string> _configuredMods;
    private readonly DebugOverlay _debug = new();
    private readonly Stopwatch _simulationWatch = new();
    private readonly List<string> _notices = [];

    private GameData _data = null!;
    private TextureStore _textures = null!;
    private FontSet _fonts = null!;
    private InputSampler _input = null!;
    private Camera2D _camera = null!;
    private PitchRenderer _pitchRenderer = null!;
    private WorldRenderer _worldRenderer = null!;
    private TrainingHud _hud = null!;
    private TrainingScene _scene = null!;
    private FixedStepClock _clock = null!;
    private SpriteBatch _batch = null!;
    private RenderTarget2D _worldTarget = null!;
    private RenderTarget2D? _frameTarget;
    private bool _modsEnabled;
    private bool _radarVisible;
    private bool _captureRequested;
    private float _transientNoticeTime;
    private string? _transientNotice;

    public ArcadeGame(LaunchOptions options, string gameRoot, UserSettings settings, GameLog log)
    {
        _options = options;
        _gameRoot = gameRoot;
        _settings = settings;
        _log = log;
        _graphics = new GraphicsDeviceManager(this) { SynchronizeWithVerticalRetrace = true };
        _configuredMods = options.Mods ?? settings.Mods;
        _modsEnabled = !options.NoMods;
        _captureTicks = [.. options.CaptureTicks];
        if (options.ScriptPath is not null)
        {
            _script = ScriptedInput.Load(options.ScriptPath);
        }

        // Scripted runs are reproducible: exactly one simulation step per update at 60 Hz.
        IsFixedTimeStep = _script is not null;
        TargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60.0);
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
    }

    public int ExitCode { get; private set; }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        LoadData(_settings.Language ?? "en", initial: true);
        int scale = _options.WindowScale ?? _settings.WindowScale ?? _data.Display.WindowScale;
        _graphics.PreferredBackBufferWidth = _data.Display.VirtualWidth * scale;
        _graphics.PreferredBackBufferHeight = _data.Display.VirtualHeight * scale;
        _graphics.IsFullScreen = _options.Fullscreen;
        _graphics.ApplyChanges();
    }

    protected override void Update(GameTime gameTime)
    {
        double frameSeconds = gameTime.ElapsedGameTime.TotalSeconds;
        _input.Update();
        HandleSystemActions();

        int steps = _script is not null ? 1 : _clock.Advance(frameSeconds);
        _simulationWatch.Restart();
        for (int i = 0; i < steps; i++)
        {
            FootballWorld world = _scene.World;
            ControlInput control = _script?.At(world.Tick) ?? _input.ReadControl(0);
            _scene.Step(control);
            DrainEvents(world);
            if (_captureTicks.Contains(world.Tick))
            {
                _captureRequested = true;
            }
        }

        _simulationWatch.Stop();
        _debug.RecordFrame(frameSeconds, _simulationWatch.Elapsed.TotalMilliseconds);
        _hud.Update((float)frameSeconds);
        _transientNoticeTime = MathF.Max(0f, _transientNoticeTime - (float)frameSeconds);
        UpdateCamera((float)frameSeconds);

        if (_options.ExitTick is long exitTick && _scene.World.Tick >= exitTick)
        {
            Exit();
        }

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        FootballWorld world = _scene.World;
        float alpha = _script is not null ? 1f : _clock.Alpha;
        ViewTransform view = CreateView();

        GraphicsDevice.SetRenderTarget(_worldTarget);
        GraphicsDevice.Clear(Color.Black);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointWrap, DepthStencilState.None, RasterizerState.CullNone);
        _pitchRenderer.Draw(_batch, _textures, world.Pitch, view);
        float? charge = _scene.Session.Controller.Phase == Core.Football.Control.ControlPhase.Holding && _scene.Session.Controller.AimSeconds > 0f
            ? _scene.Session.Controller.AimSeconds / MathF.Max(0.01f, world.Controls.Pass.ChargeTime)
            : null;
        _worldRenderer.Draw(_batch, _textures, world, _scene.Kits, _scene.Session.Player, alpha, view, charge);
        _debug.DrawWorld(_batch, _textures.Pixel, world, _scene.Session.Controller, alpha, view);
        _batch.End();

        EnsureFrameTarget();
        RenderTarget2D frame = _frameTarget!;
        GraphicsDevice.SetRenderTarget(frame);
        GraphicsDevice.Clear(Color.Black);
        Rectangle destination = FitVirtualScreen(frame.Width, frame.Height);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
        _batch.Draw(_worldTarget, destination, Color.White);
        _batch.End();

        float ui = destination.Height / (float)_data.Display.VirtualHeight;
        var screen = new Rectangle(0, 0, frame.Width, frame.Height);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        if (_radarVisible)
        {
            float width = _data.Display.Radar.Width * ui;
            float height = width * world.Pitch.HalfLength / world.Pitch.HalfWidth;
            var area = new RectangleF(destination.Right - width - (6f * ui), destination.Bottom - height - (6f * ui), width, height);
            Radar.Draw(_batch, _textures.Pixel, world, _scene.Kits, area, _data.Display.Radar.Opacity, view);
        }

        _hud.Draw(_batch, _textures.Pixel, _scene.Session, screen, ui, CurrentNotices());
        _debug.DrawText(_batch, _textures.Pixel, _fonts.Regular(6.5f * ui), world, _scene.Session.Controller, _data.PackIds, 8f * ui, 40f * ui);
        if (_clock.Paused)
        {
            _hud.DrawPaused(_batch, _textures.Pixel, screen, ui);
        }

        _batch.End();

        GraphicsDevice.SetRenderTarget(null);
        GraphicsDevice.Clear(Color.Black);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
        _batch.Draw(frame, Vector2.Zero, Color.White);
        _batch.End();

        if (_captureRequested)
        {
            _captureRequested = false;
            SaveScreenshot(frame, world.Tick);
        }

        base.Draw(gameTime);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _textures?.Dispose();
            _fonts?.Dispose();
            _worldTarget?.Dispose();
            _frameTarget?.Dispose();
            _batch?.Dispose();
            _graphics.Dispose();
        }

        base.Dispose(disposing);
    }

    private void LoadData(string language, bool initial)
    {
        IReadOnlyList<string> mods = !_modsEnabled
            ? []
            : _configuredMods.Count > 0 || initial ? _configuredMods : ContentLoader.ListInstalledMods(_gameRoot);
        GameData data = GameData.Load(_gameRoot, mods, _options.Language ?? language, _log);
        string pitch = _scene?.PitchId ?? _options.Pitch ?? DefaultPitch;
        if (!data.Football.Pitches.ContainsKey(pitch))
        {
            pitch = DefaultPitch;
        }

        var scene = new TrainingScene(data.Football, _options.Team ?? DefaultTeam, pitch, _settings.Aftertouch ?? data.Football.Controls.Aftertouch.Enabled, Seed);

        _textures?.Dispose();
        _fonts?.Dispose();
        _data = data;
        _textures = new TextureStore(GraphicsDevice, data.Repository, data.Assets, _log);
        _fonts = new FontSet(data.Repository, data.Assets);
        _input = new InputSampler(data.Input);
        _camera = new Camera2D(data.Display.Camera);
        _pitchRenderer = new PitchRenderer(data.Display.PixelsPerMetre);
        _worldRenderer = new WorldRenderer(data.Display.PixelsPerMetre);
        _hud = new TrainingHud(data.Localizer, _fonts);
        _scene = scene;
        _clock = new FixedStepClock(data.Football.Physics.StepsPerSecond);
        _radarVisible = data.Display.Radar.Visible;
        _worldTarget?.Dispose();
        _worldTarget = new RenderTarget2D(GraphicsDevice, data.Display.VirtualWidth, data.Display.VirtualHeight);
        _camera.SnapTo(new Num.Vector2(_scene.World.Ball.Position.X, _scene.World.Ball.Position.Y));
        _notices.Clear();
        int problems = data.Issues.Count;
        if (problems > 0)
        {
            _notices.Add(data.Localizer.GetPlural("content.problems", problems));
            foreach (ContentIssue issue in data.Issues)
            {
                _log.Warn("content", issue.Message, ("pack", issue.PackId));
            }
        }

        if (data.PackIds.Count > 1)
        {
            _notices.Add(data.Localizer.Get("content.modsActive", ("list", string.Join(", ", data.PackIds.Skip(1)))));
        }

        Window.Title = data.Localizer.Get("app.title");
    }

    private void Reload()
    {
        try
        {
            LoadData(_data.Localizer.Language, initial: false);
            ShowNotice(_data.Localizer.Get("content.reloaded"));
        }
        catch (ContentException exception)
        {
            _log.Error("content", "Reload failed; keeping the current data", ("error", exception.Message));
            ShowNotice(exception.Message);
        }
    }

    private void HandleSystemActions()
    {
        if (_input.WasPressed(SystemAction.Quit))
        {
            Exit();
        }

        if (_input.WasPressed(SystemAction.Pause))
        {
            _clock.Paused = !_clock.Paused;
        }

        if (_input.WasPressed(SystemAction.Debug))
        {
            _debug.Enabled = !_debug.Enabled;
        }

        if (_input.WasPressed(SystemAction.Reload))
        {
            Reload();
        }

        if (_input.WasPressed(SystemAction.ToggleMods))
        {
            _modsEnabled = !_modsEnabled;
            Reload();
        }

        if (_input.WasPressed(SystemAction.ResetBall))
        {
            _scene.Session.ResetBall();
        }

        if (_input.WasPressed(SystemAction.ToggleAftertouch))
        {
            _scene.Session.Controller.AftertouchEnabled = !_scene.Session.Controller.AftertouchEnabled;
        }

        if (_input.WasPressed(SystemAction.NextPitch))
        {
            _scene.NextPitch();
        }

        if (_input.WasPressed(SystemAction.NextLanguage))
        {
            var languages = _data.Localizer.Languages;
            int index = languages.ToList().FindIndex(language => language.Code == _data.Localizer.Language);
            _data.Localizer.SetLanguage(languages[(index + 1) % languages.Count].Code);
            _hud.LanguageChanged();
            Window.Title = _data.Localizer.Get("app.title");
        }

        if (_input.WasPressed(SystemAction.ToggleRadar))
        {
            _radarVisible = !_radarVisible;
        }

        if (_input.WasPressed(SystemAction.ZoomIn))
        {
            _camera.ChangeZoom(1);
        }

        if (_input.WasPressed(SystemAction.ZoomOut))
        {
            _camera.ChangeZoom(-1);
        }

        if (_input.WasPressed(SystemAction.Fullscreen))
        {
            _graphics.ToggleFullScreen();
        }
    }

    private void DrainEvents(FootballWorld world)
    {
        foreach (MatchEvent matchEvent in world.Events.Items)
        {
            _hud.OnEvent(matchEvent);
            _debug.OnEvent(matchEvent);
        }

        world.Events.Clear();
    }

    private void UpdateCamera(float seconds)
    {
        FootballWorld world = _scene.World;
        float scale = _data.Display.PixelsPerMetre * _camera.Zoom;
        var viewSize = new Num.Vector2(_data.Display.VirtualWidth / scale, _data.Display.VirtualHeight / scale);
        var ball = new Num.Vector2(world.Ball.Position.X, world.Ball.Position.Y);
        var velocity = new Num.Vector2(world.Ball.Velocity.X, world.Ball.Velocity.Y);
        _camera.Update(seconds, ball, velocity, world.Pitch, viewSize);
    }

    private ViewTransform CreateView() => new(
        _camera.Centre,
        _data.Display.PixelsPerMetre * _camera.Zoom,
        _data.Display.HeightScale,
        new Point(_data.Display.VirtualWidth, _data.Display.VirtualHeight));

    private Rectangle FitVirtualScreen(int width, int height)
    {
        int virtualWidth = _data.Display.VirtualWidth;
        int virtualHeight = _data.Display.VirtualHeight;
        int integer = Math.Min(width / virtualWidth, height / virtualHeight);
        float scale = integer >= 1 ? integer : MathF.Min(width / (float)virtualWidth, height / (float)virtualHeight);
        int w = (int)(virtualWidth * scale);
        int h = (int)(virtualHeight * scale);
        return new Rectangle((width - w) / 2, (height - h) / 2, w, h);
    }

    private void EnsureFrameTarget()
    {
        int width = Math.Max(1, GraphicsDevice.PresentationParameters.BackBufferWidth);
        int height = Math.Max(1, GraphicsDevice.PresentationParameters.BackBufferHeight);
        if (_frameTarget is null || _frameTarget.Width != width || _frameTarget.Height != height)
        {
            _frameTarget?.Dispose();
            _frameTarget = new RenderTarget2D(GraphicsDevice, width, height);
        }
    }

    private List<string> CurrentNotices()
    {
        var notices = new List<string>(_notices);
        if (_transientNoticeTime > 0f && _transientNotice is not null)
        {
            notices.Add(_transientNotice);
        }

        return notices;
    }

    private void ShowNotice(string text)
    {
        _transientNotice = text;
        _transientNoticeTime = NoticeSeconds;
    }

    private void SaveScreenshot(RenderTarget2D frame, long tick)
    {
        string directory = Path.GetFullPath(_options.CaptureDirectory);
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"tick-{tick:D5}.png");
        using FileStream stream = File.Create(path);
        frame.SaveAsPng(stream, frame.Width, frame.Height);
        _log.Info("capture", "Screenshot saved", ("path", path));
    }
}

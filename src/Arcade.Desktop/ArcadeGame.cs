using System.Diagnostics;
using Arcade.Core.Common;
using Arcade.Core.Content;
using Arcade.Core.Football;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Match;
using Arcade.Core.Input;
using Arcade.Core.Localization;
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
    private const string DefaultTheme = "custom";

    /// <summary>The HUD is laid out for this virtual height, whatever the look's resolution.</summary>
    private const float HudReferenceHeight = 360f;

    private static readonly SystemAction[] SceneActions = [SystemAction.ResetBall, SystemAction.ToggleAftertouch, SystemAction.NextPitch];

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
    private Localizer _localizer = null!;
    private string? _theme;
    private TextureStore _textures = null!;
    private FontSet _fonts = null!;
    private InputSampler _input = null!;
    private Camera2D _camera = null!;
    private PitchRenderer _pitchRenderer = null!;
    private WorldRenderer _worldRenderer = null!;
    private IGameScene _scene = null!;
    private ControlInput[] _inputs = [];
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
        // Borderless desktop full screen: no display mode change, which behaves best on macOS (and with integer scaling).
        _graphics = new GraphicsDeviceManager(this) { SynchronizeWithVerticalRetrace = true, HardwareModeSwitch = false };
        _configuredMods = options.Mods ?? settings.Mods;
        _modsEnabled = !options.NoMods;
        _captureTicks = [.. options.CaptureTicks];
        _theme = options.Theme ?? settings.Theme ?? (ContentLoader.ListThemes(gameRoot).Contains(DefaultTheme) ? DefaultTheme : null);
        _debug.Enabled = options.Debug;
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
        _graphics.IsFullScreen = _options.Fullscreen;
        ApplyWindowSize();
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
            for (int set = 0; set < _inputs.Length; set++)
            {
                _inputs[set] = _input.ReadControl(set);
            }

            if (_script is not null)
            {
                _inputs[0] = _script.At(world.Tick);
            }

            _scene.Step(_inputs);
            world = _scene.World;
            DrainEvents(world);
            if (_captureTicks.Contains(world.Tick))
            {
                _captureRequested = true;
            }
        }

        _simulationWatch.Stop();
        _debug.RecordFrame(frameSeconds, _simulationWatch.Elapsed.TotalMilliseconds);
        _scene.UpdateHud((float)frameSeconds);
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
        _worldRenderer.Draw(_batch, _textures, world, _scene.Kits, _scene.Controllers, alpha, view);
        ContextualController? primary = _scene.Controllers.Count > 0 ? _scene.Controllers[0] : null;
        _debug.DrawWorld(_batch, _textures.Pixel, world, primary, _scene.Match, alpha, view);
        _batch.End();

        EnsureFrameTarget();
        RenderTarget2D frame = _frameTarget!;
        GraphicsDevice.SetRenderTarget(frame);
        GraphicsDevice.Clear(Color.Black);
        Rectangle destination = FitVirtualScreen(frame.Width, frame.Height);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
        _batch.Draw(_worldTarget, destination, Color.White);
        _batch.End();

        float ui = destination.Height / HudReferenceHeight;
        var screen = new Rectangle(0, 0, frame.Width, frame.Height);
        _batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp);
        if (_radarVisible)
        {
            float width = _data.Display.Radar.Width * ui;
            float height = width * world.Pitch.HalfLength / world.Pitch.HalfWidth;
            var area = new RectangleF(destination.Right - width - (6f * ui), destination.Bottom - height - (6f * ui), width, height);
            Radar.Draw(_batch, _textures.Pixel, world, _scene.Kits, area, _data.Display.Radar.Opacity, view);
        }

        _scene.DrawHud(_batch, _textures.Pixel, screen, ui, CurrentNotices());
        float pixelScale = destination.Width / (float)_data.Display.VirtualWidth;
        _debug.DrawAiLabels(_batch, _fonts.Regular(5.5f * ui), _scene.Match, view, v => new Vector2(destination.X + (v.X * pixelScale), destination.Y + (v.Y * pixelScale)));
        _debug.DrawText(_batch, _textures.Pixel, _fonts.Regular(6.5f * ui), world, primary, _scene.Match, _data.PackIds, 8f * ui, 40f * ui);
        if (_clock.Paused)
        {
            HudCommon.DrawPaused(_batch, _textures.Pixel, _fonts, _localizer, screen, ui);
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
        GameData data = GameData.Load(_gameRoot, mods, _theme, _options.Language ?? language, _log);
        string pitch = (_scene as TrainingScene)?.PitchId ?? _options.Pitch ?? DefaultPitch;
        if (!data.Football.Pitches.ContainsKey(pitch))
        {
            pitch = DefaultPitch;
        }

        var fonts = new FontSet(data.Repository, data.Assets);
        bool aftertouch = _settings.Aftertouch ?? data.Football.Controls.Aftertouch.Enabled;
        IGameScene scene;
        try
        {
            scene = _options.Mode == "training"
                ? new TrainingScene(data.Football, _options.Team ?? DefaultTeam, pitch, aftertouch, Seed, data.Localizer, fonts)
                : new MatchScene(data.Football, CreateMatchSettings(pitch, aftertouch), data.Localizer, fonts);
        }
        catch
        {
            fonts.Dispose();
            throw;
        }

        _fonts?.Dispose();
        _data = data;
        _localizer = data.Localizer;
        _fonts = fonts;
        _input = new InputSampler(data.Input);
        _inputs = new ControlInput[_input.ControlSets];
        _scene = scene;
        _clock = new FixedStepClock(data.Football.Physics.StepsPerSecond);
        _radarVisible = data.Display.Radar.Visible;
        foreach (ContentIssue issue in data.Issues)
        {
            _log.Warn("content", issue.Message, ("pack", issue.PackId));
        }

        ApplyVisuals(data);
        Window.Title = _localizer.Get("app.title");
    }

    /// <summary>
    /// (Re)creates everything that depends on the look: textures, animation mapping, renderers, camera and the virtual
    /// screen. The scene, and therefore the simulation, is untouched: both looks share exactly the same game.
    /// </summary>
    private void ApplyVisuals(GameData data)
    {
        TextureStore? defaultLook = data.DefaultRepository is not null && data.DefaultAssets is not null
            ? new TextureStore(GraphicsDevice, data.DefaultRepository, data.DefaultAssets, _log)
            : null;
        var textures = new TextureStore(GraphicsDevice, data.Repository, data.Assets, _log, defaultLook);
        var animations = AnimationStateMap.Resolve(data.Assets, textures.IsAvailable, data.DefaultAssets, defaultLook is null ? null : defaultLook.IsAvailable);
        foreach (string note in animations.Notes)
        {
            _log.Debug("assets", note, ("look", data.ThemeId ?? "default"));
        }

        IReadOnlyList<string> missing = data.ThemeId is null ? [] : textures.MissingTextures();
        foreach (string id in missing)
        {
            _log.Warn("assets", "Theme resource not installed, temporary replacement shown", ("theme", data.ThemeId), ("id", id), ("path", data.Assets.Textures[id]));
        }

        _textures?.Dispose();
        _textures = textures;
        _camera = new Camera2D(data.Display.Camera);
        _pitchRenderer = new PitchRenderer(data.Display.PixelsPerMetre, data.Display.PitchBitmap);
        _worldRenderer = new WorldRenderer(data.Display, animations);
        _worldTarget?.Dispose();
        _worldTarget = new RenderTarget2D(GraphicsDevice, data.Display.VirtualWidth, data.Display.VirtualHeight);
        _camera.SnapTo(new Num.Vector2(_scene.World.Ball.Position.X, _scene.World.Ball.Position.Y));

        _notices.Clear();
        int problems = data.Issues.Count;
        if (problems > 0)
        {
            _notices.Add(_localizer.GetPlural("content.problems", problems));
        }

        string[] mods = [.. data.Repository.Packs.Where(pack => pack.Kind == PackKind.Mod).Select(pack => pack.Id)];
        if (mods.Length > 0)
        {
            _notices.Add(_localizer.Get("content.modsActive", ("list", string.Join(", ", mods))));
        }

        if (missing.Count > 0)
        {
            _notices.Add(_localizer.GetPlural("theme.missing", missing.Count, ("name", data.ThemeName ?? data.ThemeId ?? "?")));
        }
    }

    /// <summary>Switches to the next installed theme without restarting the match (F8).</summary>
    private void NextTheme()
    {
        IReadOnlyList<string> themes = ContentLoader.ListThemes(_gameRoot);
        if (themes.Count == 0)
        {
            return;
        }

        int index = _theme is null ? -1 : themes.ToList().IndexOf(_theme);
        string next = themes[(index + 1) % themes.Count];
        string[] mods = [.. _data.Repository.Packs.Where(pack => pack.Kind == PackKind.Mod).Select(pack => pack.Id)];
        try
        {
            GameData data = GameData.Load(_gameRoot, mods, next, _localizer.Language, _log);
            _theme = next;
            _data = data;
            ApplyVisuals(data);
            if (!_graphics.IsFullScreen)
            {
                ApplyWindowSize();
            }

            ShowNotice(_localizer.Get("theme.active", ("name", data.ThemeName ?? next)));
        }
        catch (ContentException exception)
        {
            _log.Error("content", "Theme switch failed; keeping the current look", ("error", exception.Message));
            ShowNotice(exception.Message);
        }
    }

    private void ApplyWindowSize()
    {
        int scale = _options.WindowScale ?? _settings.WindowScale ?? _data.Display.WindowScale;
        _graphics.PreferredBackBufferWidth = (int)MathF.Round(_data.Display.VirtualWidth * _data.Display.PixelAspect * scale);
        _graphics.PreferredBackBufferHeight = _data.Display.VirtualHeight * scale;
        _graphics.ApplyChanges();
    }

    private MatchSettings CreateMatchSettings(string pitch, bool aftertouch)
    {
        HumanSlot[] humans = _options.Players switch
        {
            0 => [],
            2 => [new HumanSlot(0, 0), new HumanSlot(1, 1)],
            _ => [new HumanSlot(0, 0)],
        };
        return new MatchSettings
        {
            HomeTeamId = _options.Home,
            AwayTeamId = _options.Away,
            PitchId = pitch,
            AiLevel = _options.Level,
            HalfLengthSeconds = _options.HalfMinutes * 60f,
            Humans = humans,
            Aftertouch = aftertouch,
            Seed = _options.Seed ?? Seed,
        };
    }

    private void Reload()
    {
        try
        {
            LoadData(_localizer.Language, initial: false);
            ShowNotice(_localizer.Get("content.reloaded"));
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

        foreach (SystemAction action in SceneActions)
        {
            if (_input.WasPressed(action))
            {
                _scene.HandleAction(action);
            }
        }

        if (_input.WasPressed(SystemAction.NextLanguage))
        {
            var languages = _localizer.Languages;
            int index = languages.ToList().FindIndex(language => language.Code == _localizer.Language);
            _localizer.SetLanguage(languages[(index + 1) % languages.Count].Code);
            _scene.LanguageChanged();
            Window.Title = _localizer.Get("app.title");
        }

        if (_input.WasPressed(SystemAction.NextTheme))
        {
            NextTheme();
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
            _scene.OnEvent(matchEvent);
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
        // Integer scaling of the virtual screen (nearest neighbour, see Draw); a pixel aspect other than 1 widens
        // the image horizontally, which makes the column widths uneven by at most one pixel.
        float virtualWidth = _data.Display.VirtualWidth * _data.Display.PixelAspect;
        int virtualHeight = _data.Display.VirtualHeight;
        int integer = Math.Min((int)(width / virtualWidth), height / virtualHeight);
        float scale = integer >= 1 ? integer : MathF.Min(width / virtualWidth, height / (float)virtualHeight);
        int w = (int)MathF.Round(virtualWidth * scale);
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

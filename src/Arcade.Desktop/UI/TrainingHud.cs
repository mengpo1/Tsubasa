using System.Globalization;
using Arcade.Core.Football;
using Arcade.Core.Football.Control;
using Arcade.Core.Localization;
using Arcade.Desktop.Rendering;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Arcade.Core.Football.Training;

namespace Arcade.Desktop.UI;

/// <summary>Scoreboard, clock, training information and messages. All texts come from the localizer.</summary>
internal sealed class TrainingHud
{
    /// <summary>How long the "goal" banner and decision messages stay on screen (s).</summary>
    private const float BannerSeconds = 1.6f;

    private readonly FontSet _fonts;
    private readonly CachedText<int> _clock;
    private readonly CachedText<int> _ballSpeed;
    private readonly CachedText<int> _energy;
    private readonly CachedText<string> _pitchName;
    private readonly CachedText<int> _board;
    private string _teamShortName = string.Empty;
    private Localizer _localizer;
    private float _goalBanner;
    private int _version;

    public TrainingHud(Localizer localizer, FontSet fonts)
    {
        _localizer = localizer;
        _fonts = fonts;
        _clock = new CachedText<int>(seconds => _localizer.Get(
            "hud.time",
            ("minutes", (seconds / 60).ToString("00", CultureInfo.InvariantCulture)),
            ("seconds", (seconds % 60).ToString("00", CultureInfo.InvariantCulture))));
        _ballSpeed = new CachedText<int>(speed => _localizer.Get("hud.ballSpeed", ("speed", speed)));
        _energy = new CachedText<int>(percent => _localizer.Get("hud.energy", ("percent", percent)));
        _pitchName = new CachedText<string>(key => _localizer.Get("hud.pitch", ("name", _localizer.Get(key))));
        _board = new CachedText<int>(goals => $"{_teamShortName}  {_localizer.GetPlural("hud.goals", goals)}");
    }

    public void SetLocalizer(Localizer localizer)
    {
        _localizer = localizer;
        _version++;
    }

    public void LanguageChanged() => _version++;

    public void OnEvent(MatchEvent matchEvent)
    {
        if (matchEvent.Type == MatchEventType.Goal)
        {
            _goalBanner = BannerSeconds;
        }
    }

    public void Update(float realSeconds) => _goalBanner = MathF.Max(0f, _goalBanner - realSeconds);

    public void Draw(SpriteBatch batch, Texture2D pixel, TrainingSession session, Rectangle screen, float ui, IReadOnlyList<string> notices)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(notices);
        FootballWorld world = session.World;
        float margin = 6f * ui;

        // Scoreboard: team, goals and clock.
        SpriteFontBase bold = _fonts.Bold(9f * ui);
        SpriteFontBase small = _fonts.Regular(7f * ui);
        string title = _localizer.Get("hud.training");
        string clock = _clock.Get((int)(world.Tick * world.StepSeconds), _version);
        _teamShortName = session.Team.ShortName;
        string board = _board.Get(session.Goals, _version);
        float boardWidth = MathF.Max(bold.MeasureString(board).X, small.MeasureString(title).X) + (margin * 2f) + bold.MeasureString(clock).X + margin;
        var panel = new RectangleF(margin, margin, boardWidth, (bold.LineHeight + small.LineHeight) + (margin * 1.2f));
        Shapes.Rectangle(batch, pixel, panel, Color.Black * 0.55f);
        small.DrawText(batch, title, new Vector2(panel.X + margin, panel.Y + (margin * 0.4f)), Color.LightGray);
        bold.DrawText(batch, board, new Vector2(panel.X + margin, panel.Y + (margin * 0.4f) + small.LineHeight), Color.White);
        bold.DrawText(batch, clock, new Vector2(panel.Right - margin - bold.MeasureString(clock).X, panel.Y + (margin * 0.4f) + small.LineHeight), new Color(255, 230, 60));

        // Training information (bottom left).
        ContextualController controller = session.Controller;
        string pitchName = _pitchName.Get(world.Pitch.Definition.NameKey, _version);
        string aftertouch = _localizer.Get(controller.AftertouchEnabled ? "hud.aftertouch.on" : "hud.aftertouch.off");
        string speed = _ballSpeed.Get((int)MathF.Round(world.Ball.Velocity.Length() * 3.6f), _version);
        string energy = _energy.Get((int)MathF.Round(session.Player.Energy * 100f), _version);
        string phase = _localizer.Get(MatchHud.PhaseKey(controller.Phase));
        ReadOnlySpan<string> lines = [phase, speed, energy, aftertouch, pitchName];
        float y = screen.Height - margin - (lines.Length * small.LineHeight) - small.LineHeight;
        foreach (string line in lines)
        {
            small.DrawText(batch, line, new Vector2(margin + 1f, y + 1f), Color.Black * 0.7f);
            small.DrawText(batch, line, new Vector2(margin, y), Color.White);
            y += small.LineHeight;
        }

        // Help line.
        SpriteFontBase tiny = _fonts.Regular(6f * ui);
        string help = _localizer.Get("hud.help");
        tiny.DrawText(batch, help, new Vector2(margin, screen.Height - margin - tiny.LineHeight), Color.White * 0.75f);

        // Decision feedback (centre top) and goal banner (centre).
        if (controller.LastDecision is not ControlDecision.None and not ControlDecision.TrapArmed
            && world.SecondsSince(controller.LastDecisionTick) < BannerSeconds)
        {
            string decision = _localizer.Get(DecisionKey(controller.LastDecision));
            SpriteFontBase font = _fonts.Bold(8f * ui);
            Vector2 size = font.MeasureString(decision);
            font.DrawText(batch, decision, new Vector2((screen.Width - size.X) / 2f, margin * 2f), Color.White);
        }

        if (_goalBanner > 0f)
        {
            string text = _localizer.Get("hud.goal");
            SpriteFontBase big = _fonts.Bold(28f * ui);
            Vector2 size = big.MeasureString(text);
            var at = new Vector2((screen.Width - size.X) / 2f, (screen.Height - size.Y) / 2.5f);
            big.DrawText(batch, text, at + new Vector2(2f * ui, 2f * ui), Color.Black * 0.7f);
            big.DrawText(batch, text, at, new Color(255, 230, 60));
        }

        // Notices (content problems, reloads...).
        float noticeY = margin;
        foreach (string notice in notices)
        {
            Vector2 size = small.MeasureString(notice);
            var box = new RectangleF(screen.Width - size.X - (margin * 2f), noticeY, size.X + margin, size.Y + (margin * 0.5f));
            Shapes.Rectangle(batch, pixel, box, new Color(120, 20, 20) * 0.8f);
            small.DrawText(batch, notice, new Vector2(box.X + (margin * 0.5f), box.Y + (margin * 0.25f)), Color.White);
            noticeY += box.Height + (margin * 0.5f);
        }
    }

    private static string DecisionKey(ControlDecision decision) => decision switch
    {
        ControlDecision.Shot => "decision.shot",
        ControlDecision.Pass => "decision.pass",
        ControlDecision.Chip => "decision.chip",
        ControlDecision.Trapped => "decision.trapped",
        ControlDecision.DribbleAway => "decision.dribbleAway",
        ControlDecision.BallLost => "decision.ballLost",
        _ => "decision.trapped",
    };
}

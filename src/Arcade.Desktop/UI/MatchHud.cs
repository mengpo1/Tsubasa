using Arcade.Core.Football;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Players;
using Arcade.Core.Localization;
using Arcade.Desktop.Rendering;
using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Arcade.Desktop.UI;

/// <summary>Match scoreboard, clock, restart and goal banners, full-time result. All texts are translated.</summary>
internal sealed class MatchHud
{
    private const float GoalBannerSeconds = 3f;

    private static readonly Color Gold = new(255, 230, 60);

    private readonly FontSet _fonts;
    private readonly CachedText<int> _score;
    private readonly CachedText<int> _minute;
    private readonly Localizer _localizer;
    private int _version;
    private float _goalBanner;
    private string _scorer = string.Empty;
    private string _home = string.Empty;
    private string _away = string.Empty;

    public MatchHud(Localizer localizer, FontSet fonts)
    {
        _localizer = localizer;
        _fonts = fonts;
        _score = new CachedText<int>(packed => _localizer.Get(
            "hud.score",
            ("home", _home),
            ("away", _away),
            ("homeGoals", packed / 1000),
            ("awayGoals", packed % 1000)));
        _minute = new CachedText<int>(minute => _localizer.Get("hud.minute", ("minute", minute)));
    }

    public void LanguageChanged() => _version++;

    public void OnEvent(MatchEvent matchEvent, FootballMatch match)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (matchEvent.Type == MatchEventType.GoalScored && matchEvent.PlayerId != Player.NoPlayer)
        {
            Player scorer = match.World.Players[matchEvent.PlayerId];
            _scorer = _localizer.Get("hud.scorer", ("number", scorer.Profile.Number), ("name", scorer.Profile.Name));
            if (scorer.TeamIndex != matchEvent.Detail)
            {
                _scorer = _localizer.Get("hud.ownGoal", ("scorer", _scorer));
            }

            _goalBanner = GoalBannerSeconds;
        }
    }

    public void Update(float realSeconds) => _goalBanner = MathF.Max(0f, _goalBanner - realSeconds);

    public void Draw(SpriteBatch batch, Texture2D pixel, FootballMatch match, Rectangle screen, float ui, IReadOnlyList<string> notices)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(notices);
        float margin = 6f * ui;
        SpriteFontBase bold = _fonts.Bold(9f * ui);
        SpriteFontBase small = _fonts.Regular(7f * ui);

        // Scoreboard and clock.
        _home = match.Teams[0].Definition.ShortName;
        _away = match.Teams[1].Definition.ShortName;
        string score = _score.Get((match.Teams[0].Goals * 1000) + match.Teams[1].Goals, _version);
        string clock = _minute.Get((int)match.DisplayedMinutes, _version);
        string half = _localizer.Get(match.Half == 1 ? "hud.half.1" : "hud.half.2");
        float width = bold.MeasureString(score).X + bold.MeasureString(clock).X + (margin * 3f);
        var panel = new RectangleF(margin, margin, MathF.Max(width, small.MeasureString(half).X + (margin * 2f)), bold.LineHeight + small.LineHeight + (margin * 1.2f));
        Shapes.Rectangle(batch, pixel, panel, Color.Black * 0.55f);
        small.DrawText(batch, half, new Vector2(panel.X + margin, panel.Y + (margin * 0.4f)), Color.LightGray);
        bold.DrawText(batch, score, new Vector2(panel.X + margin, panel.Y + (margin * 0.4f) + small.LineHeight), Color.White);
        bold.DrawText(batch, clock, new Vector2(panel.Right - margin - bold.MeasureString(clock).X, panel.Y + (margin * 0.4f) + small.LineHeight), Gold);

        DrawBanner(batch, match, screen, ui);

        // Control hint of the first human player and help line.
        if (match.Humans.Count > 0)
        {
            string phase = _localizer.Get(PhaseKey(match.Humans[0].Controller.Phase));
            float y = screen.Height - margin - (small.LineHeight * 2f);
            small.DrawText(batch, phase, new Vector2(margin + 1f, y + 1f), Color.Black * 0.7f);
            small.DrawText(batch, phase, new Vector2(margin, y), Color.White);
        }

        SpriteFontBase tiny = _fonts.Regular(6f * ui);
        tiny.DrawText(batch, _localizer.Get("hud.match.help"), new Vector2(margin, screen.Height - margin - tiny.LineHeight), Color.White * 0.75f);

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

    private void DrawBanner(SpriteBatch batch, FootballMatch match, Rectangle screen, float ui)
    {
        string? title = null;
        string? subtitle = null;
        if (_goalBanner > 0f && match.Phase == MatchPhase.GoalCelebration)
        {
            title = _localizer.Get("hud.goal");
            subtitle = _scorer;
        }
        else
        {
            switch (match.Phase)
            {
                case MatchPhase.HalfTime:
                    title = _localizer.Get("hud.halfTime");
                    break;
                case MatchPhase.FullTime:
                    title = _localizer.Get("hud.fullTime");
                    subtitle = _localizer.Get("hud.playAgain");
                    break;
                case MatchPhase.KickOffSetup:
                case MatchPhase.BallOut:
                case MatchPhase.RestartSetup:
                case MatchPhase.RestartReady:
                    if (match.CurrentRestart is Restart restart)
                    {
                        title = _localizer.Get(RestartKey(restart.Kind));
                        subtitle = match.Teams[restart.TeamIndex].Definition.Name;
                    }

                    break;
            }
        }

        if (title is null)
        {
            return;
        }

        bool big = match.Phase is MatchPhase.GoalCelebration or MatchPhase.HalfTime or MatchPhase.FullTime;
        SpriteFontBase font = _fonts.Bold((big ? 24f : 10f) * ui);
        Vector2 size = font.MeasureString(title);
        float y = big ? (screen.Height - size.Y) / 2.6f : 26f * ui;
        var at = new Vector2((screen.Width - size.X) / 2f, y);
        font.DrawText(batch, title, at + new Vector2(2f * ui, 2f * ui), Color.Black * 0.7f);
        font.DrawText(batch, title, at, big ? Gold : Color.White);
        if (!string.IsNullOrEmpty(subtitle))
        {
            SpriteFontBase small = _fonts.Bold(8f * ui);
            Vector2 subSize = small.MeasureString(subtitle);
            var subAt = new Vector2((screen.Width - subSize.X) / 2f, at.Y + size.Y + (2f * ui));
            small.DrawText(batch, subtitle, subAt + new Vector2(1f * ui, 1f * ui), Color.Black * 0.7f);
            small.DrawText(batch, subtitle, subAt, Color.White);
        }
    }

    private static string RestartKey(SetPieceKind kind) => kind switch
    {
        SetPieceKind.KickOff => "hud.kickOff",
        SetPieceKind.ThrowIn => "hud.throwIn",
        SetPieceKind.Corner => "hud.corner",
        _ => "hud.goalKick",
    };

    internal static string PhaseKey(ControlPhase phase) => phase switch
    {
        ControlPhase.TrapArmed => "control.trapArmed",
        ControlPhase.Holding => "control.holding",
        ControlPhase.AtFeet => "control.atFeet",
        ControlPhase.SetPiece => "control.setPiece",
        _ => "control.free",
    };
}

using Arcade.Core.Football;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Teams;
using Arcade.Core.Input;
using Arcade.Core.Localization;
using Arcade.Desktop.Input;
using Arcade.Desktop.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Arcade.Desktop.Scenes;

/// <summary>A friendly match (Lot 2). After full time, fire starts a new match.</summary>
internal sealed class MatchScene : IGameScene
{
    private readonly FootballContent _content;
    private readonly MatchHud _hud;
    private MatchSettings _settings;
    private bool _previousFire = true;

    public MatchScene(FootballContent content, MatchSettings settings, Localizer localizer, FontSet fonts)
    {
        _content = content;
        _settings = settings;
        _hud = new MatchHud(localizer, fonts);
        Match = new FootballMatch(content, settings);
        Kits = [Match.Teams[0].Definition.Kit, Match.Teams[1].Definition.Kit];
        Controllers = [.. Match.Humans.Select(human => human.Controller)];
    }

    public FootballMatch Match { get; private set; }

    public FootballWorld World => Match.World;

    public IReadOnlyList<KitDefinition> Kits { get; private set; }

    public IReadOnlyList<ContextualController> Controllers { get; private set; }

    FootballMatch? IGameScene.Match => Match;

    public void Step(ReadOnlySpan<ControlInput> inputs)
    {
        bool fire = inputs.Length > 0 && inputs[0].Fire;
        if (Match.IsOver && Match.PhaseSeconds > 1f && fire && !_previousFire)
        {
            _settings = _settings with { Seed = _settings.Seed + 1 };
            Match = new FootballMatch(_content, _settings);
            Kits = [Match.Teams[0].Definition.Kit, Match.Teams[1].Definition.Kit];
            Controllers = [.. Match.Humans.Select(human => human.Controller)];
        }

        _previousFire = fire;
        Match.Step(inputs);
    }

    public bool HandleAction(SystemAction action)
    {
        if (action != SystemAction.ToggleAftertouch)
        {
            return false;
        }

        foreach (ContextualController controller in Controllers)
        {
            controller.AftertouchEnabled = !controller.AftertouchEnabled;
        }

        return true;
    }

    public void OnEvent(MatchEvent matchEvent) => _hud.OnEvent(matchEvent, Match);

    public void UpdateHud(float realSeconds) => _hud.Update(realSeconds);

    public void DrawHud(SpriteBatch batch, Texture2D pixel, Rectangle screen, float ui, IReadOnlyList<string> notices) =>
        _hud.Draw(batch, pixel, Match, screen, ui, notices);

    public void LanguageChanged() => _hud.LanguageChanged();
}

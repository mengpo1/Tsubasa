using Arcade.Core.Football;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Teams;
using Arcade.Core.Input;
using Arcade.Desktop.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Arcade.Desktop.Scenes;

/// <summary>A playable screen (training or match): advances its simulation and draws its own HUD.</summary>
internal interface IGameScene
{
    FootballWorld World { get; }

    /// <summary>Kit of each team index.</summary>
    IReadOnlyList<KitDefinition> Kits { get; }

    /// <summary>Human controllers, in control-set order (markers, gauges, debug).</summary>
    IReadOnlyList<ContextualController> Controllers { get; }

    /// <summary>The match, when the scene is one (debug display of the AI).</summary>
    FootballMatch? Match { get; }

    /// <summary>One simulation step. <paramref name="inputs"/> is indexed by control set.</summary>
    void Step(ReadOnlySpan<ControlInput> inputs);

    /// <summary>Scene-specific keys (reset ball, aftertouch, pitch...). Returns true when handled.</summary>
    bool HandleAction(SystemAction action);

    void OnEvent(MatchEvent matchEvent);

    void UpdateHud(float realSeconds);

    void DrawHud(SpriteBatch batch, Texture2D pixel, Rectangle screen, float ui, IReadOnlyList<string> notices);

    /// <summary>Cached texts must be rebuilt after a language change.</summary>
    void LanguageChanged();
}

using Arcade.Core.Football;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Teams;
using Arcade.Core.Football.Training;
using Arcade.Core.Input;
using Arcade.Core.Localization;
using Arcade.Desktop.Input;
using Arcade.Desktop.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Arcade.Desktop.Scenes;

/// <summary>Glue between the training session (simulation), its options and its HUD.</summary>
internal sealed class TrainingScene : IGameScene
{
    private readonly TrainingHud _hud;

    public TrainingScene(FootballContent content, string teamId, string pitchId, bool aftertouch, ulong seed, Localizer localizer, FontSet fonts)
    {
        Session = new TrainingSession(content, teamId, pitchId, seed);
        Session.Controller.AftertouchEnabled = aftertouch;
        Kits = [Session.Team.Kit];
        Controllers = [Session.Controller];
        PitchIds = [.. content.Pitches.Keys.Order(StringComparer.Ordinal)];
        PitchId = pitchId;
        _hud = new TrainingHud(localizer, fonts);
    }

    public TrainingSession Session { get; }

    public FootballWorld World => Session.World;

    public IReadOnlyList<KitDefinition> Kits { get; }

    public IReadOnlyList<ContextualController> Controllers { get; }

    public FootballMatch? Match => null;

    public IReadOnlyList<string> PitchIds { get; }

    public string PitchId { get; private set; }

    public void Step(ReadOnlySpan<ControlInput> inputs) => Session.Step(inputs.Length > 0 ? inputs[0] : ControlInput.Neutral);

    public bool HandleAction(SystemAction action)
    {
        switch (action)
        {
            case SystemAction.ResetBall:
                Session.ResetBall();
                return true;
            case SystemAction.ToggleAftertouch:
                Session.Controller.AftertouchEnabled = !Session.Controller.AftertouchEnabled;
                return true;
            case SystemAction.NextPitch:
                NextPitch();
                return true;
            default:
                return false;
        }
    }

    public void OnEvent(MatchEvent matchEvent) => _hud.OnEvent(matchEvent);

    public void UpdateHud(float realSeconds) => _hud.Update(realSeconds);

    public void DrawHud(SpriteBatch batch, Texture2D pixel, Rectangle screen, float ui, IReadOnlyList<string> notices) =>
        _hud.Draw(batch, pixel, Session, screen, ui, notices);

    public void LanguageChanged() => _hud.LanguageChanged();

    private void NextPitch()
    {
        int index = (PitchIds.ToList().IndexOf(PitchId) + 1) % PitchIds.Count;
        PitchId = PitchIds[index];
        PitchDefinition pitch = World.Content.Pitches[PitchId];
        Session.SetPitch(pitch);
    }
}

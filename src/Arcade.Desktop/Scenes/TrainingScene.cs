using Arcade.Core.Football;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Teams;
using Arcade.Core.Football.Training;
using Arcade.Core.Input;

namespace Arcade.Desktop.Scenes;

/// <summary>Glue between the training session (simulation) and the desktop options (pitch, aftertouch).</summary>
internal sealed class TrainingScene
{
    public TrainingScene(FootballContent content, string teamId, string pitchId, bool aftertouch, ulong seed)
    {
        Session = new TrainingSession(content, teamId, pitchId, seed);
        Session.Controller.AftertouchEnabled = aftertouch;
        Kits = [Session.Team.Kit];
        PitchIds = [.. content.Pitches.Keys.Order(StringComparer.Ordinal)];
        PitchId = pitchId;
    }

    public TrainingSession Session { get; }

    public FootballWorld World => Session.World;

    public IReadOnlyList<KitDefinition> Kits { get; }

    public IReadOnlyList<string> PitchIds { get; }

    public string PitchId { get; private set; }

    public void Step(ControlInput input) => Session.Step(input);

    public void NextPitch()
    {
        int index = (PitchIds.ToList().IndexOf(PitchId) + 1) % PitchIds.Count;
        PitchId = PitchIds[index];
        PitchDefinition pitch = World.Content.Pitches[PitchId];
        Session.SetPitch(pitch);
    }
}

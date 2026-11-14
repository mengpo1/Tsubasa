using System.Numerics;
using Arcade.Core.Content;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Football.Teams;
using Arcade.Core.Input;

namespace Arcade.Core.Football.Training;

/// <summary>
/// Practice pitch for the fundamental feel (Lot 1): one human player, one ball, two goals.
/// Counts goals and puts the ball back in front of the player after a goal or on request.
/// </summary>
public sealed class TrainingSession
{
    /// <summary>Delay between a goal and the automatic ball reset (s).</summary>
    public const float GoalResetDelay = 1.5f;

    /// <summary>Where a reset ball is placed, in front of the player (m).</summary>
    public const float ResetBallDistance = 2f;

    private long _resetTick = -1;

    public TrainingSession(FootballContent content, string teamId, string pitchId, ulong seed)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.Teams.TryGetValue(teamId, out TeamDefinition? team))
        {
            throw new ContentException($"Unknown team '{teamId}' (known: {string.Join(", ", content.Teams.Keys)}).");
        }

        if (!content.Pitches.TryGetValue(pitchId, out PitchDefinition? pitch))
        {
            throw new ContentException($"Unknown pitch '{pitchId}' (known: {string.Join(", ", content.Pitches.Keys)}).");
        }

        Team = team;
        World = new FootballWorld(content, pitch, seed);
        PlayerProfile profile = team.Players.FirstOrDefault(p => p.Role == PlayerRole.Forward) ?? team.Players[0];
        Player = World.AddPlayer(0, profile);
        Player.DefendsEnd = GoalEnd.Bottom; // The training player attacks the top goal.
        Controller = new ContextualController(Player, content.Controls.Aftertouch.Enabled);
        ResetPositions();
    }

    public FootballWorld World { get; }

    public TeamDefinition Team { get; }

    public Player Player { get; }

    public ContextualController Controller { get; }

    public int Goals { get; private set; }

    public void Step(ControlInput input)
    {
        if (_resetTick >= 0 && World.Tick >= _resetTick)
        {
            _resetTick = -1;
            ResetBall();
        }

        Controller.Update(input, World);
        World.Step();
        if (World.BallInGoal is not null && _resetTick < 0)
        {
            Goals++;
            _resetTick = World.Tick + (long)(GoalResetDelay / World.StepSeconds);
        }
    }

    /// <summary>Puts the ball at rest in front of the player.</summary>
    public void ResetBall()
    {
        Vector2 spot = Player.Position + (Player.Facing.ToVector() * ResetBallDistance);
        float limitX = World.Pitch.HalfWidth - 1f;
        float limitY = World.Pitch.HalfLength - 1f;
        spot = new Vector2(Math.Clamp(spot.X, -limitX, limitX), Math.Clamp(spot.Y, -limitY, limitY));
        World.Ball.PlaceAt(spot);
        Controller.Reset();
    }

    /// <summary>Player in his own half facing the top goal, ball just ahead.</summary>
    public void ResetPositions()
    {
        Player.PlaceAt(new Vector2(0f, World.Pitch.HalfLength * 0.35f), Direction8.Up);
        ResetBall();
    }

    public void SetPitch(PitchDefinition pitch) => World.SetPitch(pitch);
}

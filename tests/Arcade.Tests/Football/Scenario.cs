using System.Numerics;
using Arcade.Core.Football;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Tests.Football;

/// <summary>A small world (one human player, one ball) driven by scripted inputs.</summary>
internal sealed class Scenario
{
    public Scenario(string pitch = "normal", FootballContent? content = null, ulong seed = 1, string team = "reds", int playerIndex = 9)
    {
        Content = content ?? TestPaths.BaseContent;
        World = new FootballWorld(Content, Content.Pitches[pitch], seed);
        Player = World.AddPlayer(0, Content.Teams[team].Players[playerIndex]);
        Controller = new ContextualController(Player, aftertouchEnabled: true);
        Player.PlaceAt(new Vector2(0f, 20f), Direction8.Up);
        World.Ball.PlaceAt(new Vector2(0f, 30f));
    }

    public FootballContent Content { get; }

    public FootballWorld World { get; }

    public Player Player { get; }

    public ContextualController Controller { get; }

    public List<MatchEvent> Events { get; } = [];

    public Vector2 BallXY => new(World.Ball.Position.X, World.Ball.Position.Y);

    public float Seconds(int ticks) => ticks * World.StepSeconds;

    public int Ticks(float seconds) => (int)MathF.Round(seconds / World.StepSeconds);

    public static ControlInput Stick(Direction8 direction, bool fire = false) => new(direction, fire);

    public void Step(ControlInput input)
    {
        Controller.Update(input, World);
        World.Step();
        Events.AddRange(World.Events.Items);
        World.Events.Clear();
    }

    public void Run(float seconds, ControlInput input)
    {
        int ticks = Ticks(seconds);
        for (int i = 0; i < ticks; i++)
        {
            Step(input);
        }
    }

    /// <summary>Runs until the condition holds; returns the number of ticks, or -1 on timeout.</summary>
    public int RunUntil(Func<bool> condition, ControlInput input, float timeoutSeconds = 5f)
    {
        int max = Ticks(timeoutSeconds);
        for (int i = 0; i < max; i++)
        {
            if (condition())
            {
                return i;
            }

            Step(input);
        }

        return condition() ? max : -1;
    }

    public IEnumerable<MatchEvent> Kicks(KickKind kind) =>
        Events.Where(e => e.Type == MatchEventType.BallKicked && e.Detail == (int)kind);

    public IEnumerable<MatchEvent> Touches(BallTouchKind kind) =>
        Events.Where(e => e.Type == MatchEventType.BallTouched && e.Detail == (int)kind);

    /// <summary>Puts the player just behind the ball, running up the pitch at full speed.</summary>
    public void StartDribblingUp()
    {
        Player.PlaceAt(new Vector2(0f, 30f), Direction8.Up);
        World.Ball.PlaceAt(new Vector2(0f, 26f));
        RunUntil(() => Touches(BallTouchKind.Dribble).Any(), Stick(Direction8.Up));
    }
}

using System.Numerics;
using Arcade.Core.Football;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Tests.Football.Match;

/// <summary>Runs a match with scripted inputs and records its events.</summary>
internal sealed class MatchHarness
{
    public MatchHarness(int humans = 0, float halfSeconds = 30f, ulong seed = 3, string level = "normal")
    {
        HumanSlot[] slots = humans switch
        {
            0 => [],
            1 => [new HumanSlot(0, 0)],
            _ => [new HumanSlot(0, 0), new HumanSlot(1, 1)],
        };
        Match = new FootballMatch(TestPaths.BaseContent, new MatchSettings
        {
            HomeTeamId = "reds",
            AwayTeamId = "blues",
            PitchId = "normal",
            HalfLengthSeconds = halfSeconds,
            Humans = slots,
            AiLevel = level,
            Seed = seed,
        });
    }

    public FootballMatch Match { get; }

    public FootballWorld World => Match.World;

    public List<MatchEvent> Events { get; } = [];

    public ControlInput[] Inputs { get; } = new ControlInput[2];

    public void Step()
    {
        Match.Step(Inputs);
        Events.AddRange(World.Events.Items);
        World.Events.Clear();
    }

    public int Ticks(float seconds) => (int)MathF.Round(seconds / World.StepSeconds);

    public void Run(float seconds)
    {
        for (int i = 0, n = Ticks(seconds); i < n; i++)
        {
            Step();
        }
    }

    /// <summary>Runs until the condition holds; returns false on timeout.</summary>
    public bool RunUntil(Func<bool> condition, float timeoutSeconds = 20f)
    {
        for (int i = 0, n = Ticks(timeoutSeconds); i < n; i++)
        {
            if (condition())
            {
                return true;
            }

            Step();
        }

        return condition();
    }

    public IEnumerable<MatchEvent> Of(MatchEventType type) => Events.Where(e => e.Type == type);

    /// <summary>Waits for the kick-off to be taken (open play).</summary>
    public void StartPlaying() => Assert.True(RunUntil(() => Match.Phase == MatchPhase.Playing, 15f), $"still in {Match.Phase}");

    /// <summary>Puts the ball live at a point and kicks it with a player of the given team.</summary>
    public void KickFrom(int team, Vector2 from, Vector2 velocity, float lift = 0f)
    {
        Player kicker = Match.Teams[team].Players[9];
        World.Ball.PlaceAt(from);
        World.Kick(kicker, velocity, velocity.Length(), lift, KickKind.Pass, 0f);
    }
}

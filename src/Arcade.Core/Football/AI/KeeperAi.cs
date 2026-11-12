using System.Numerics;
using Arcade.Core.Common;
using Arcade.Core.Football.Ball;
using Arcade.Core.Football.Control;
using Arcade.Core.Football.Match;
using Arcade.Core.Football.Pitch;
using Arcade.Core.Football.Players;
using Arcade.Core.Input;

namespace Arcade.Core.Football.AI;

/// <summary>
/// Elementary goalkeeper (Lot 2): stands on the line between the ball and the goal centre, moves across to
/// where a shot will cross his line once he has reacted, collects slow loose balls in his area, and clears
/// the ball upfield after holding it. Catches and parries are resolved by the world (hands in his area).
/// No dives yet: he can only reach what his run and arms allow, so he never makes impossible saves.
/// </summary>
public static class KeeperAi
{
    public static void Update(FootballMatch match, TeamAi team, Player keeper, AiBrain brain)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(keeper);
        ArgumentNullException.ThrowIfNull(brain);
        FootballWorld world = match.World;
        AiKeeper config = team.Config.Keeper;
        GoalEnd end = team.Team.Defends;
        float line = world.Pitch.GoalLineY(end);
        float outward = PitchGeometry.Outward(end);
        Vector2 goal = world.Pitch.GoalCentre(end);
        Vector2 ball = VectorMath.Horizontal(world.Ball.Position);

        if (world.Ball.Status == BallStatus.Carried && world.Ball.CarrierId == keeper.Id)
        {
            brain.Enter(AiState.KeeperHolding, world.Tick);
            Direction8 up = Direction8Extensions.FromVector(team.Team.AttackDirection, 1e-3f);
            keeper.Intent = new PlayerIntent { Move = Direction8.None, Face = up };
            if (world.SecondsSince(brain.StateTick) >= config.HoldSeconds)
            {
                Clear(match, team, keeper, brain);
            }

            return;
        }

        brain.Enter(AiState.Keeping, world.Tick);
        float distanceToGoal = Vector2.Distance(ball, goal);
        float depth = Math.Clamp(config.MinLineDistance + (config.AdvanceFactor * distanceToGoal), config.MinLineDistance, config.MaxLineDistance);
        float keeperLine = line - (outward * depth);
        Vector2 target;
        bool shotComing = world.Ball.Status == BallStatus.Live
            && world.Ball.Velocity.Y * outward > 3f
            && world.SecondsSince(world.Ball.LastTouchTick) >= team.Level.KeeperReactionSeconds;
        var crossing = shotComing ? match.Forecast.Crossing(keeperLine, outward) : null;
        if (crossing is { } c && MathF.Abs(c.X) < world.Pitch.GoalHalfWidth + 2f)
        {
            target = new Vector2(Math.Clamp(c.X, -world.Pitch.GoalHalfWidth - 0.5f, world.Pitch.GoalHalfWidth + 0.5f), keeperLine);
            brain.Decision = "save";
        }
        else if (ShouldCollect(match, team, keeper, ball, end))
        {
            (_, target) = match.Forecast.Intercept(keeper, TeamAi.TopSpeed(world, keeper), team.Level.KeeperReactionSeconds, 0.5f);
            brain.Decision = "collect";
        }
        else
        {
            Vector2 toBall = ball - goal;
            Vector2 direction = toBall.LengthSquared() > 1e-4f ? Vector2.Normalize(toBall) : -new Vector2(0f, outward);
            if (direction.Y * outward > -0.2f)
            {
                direction = Vector2.Normalize(new Vector2(direction.X, -0.2f * outward));
            }

            target = goal + (direction * depth);
            brain.Decision = "position";
        }

        brain.Target = target;
        TeamAi.MoveTo(keeper, target, 0.25f, ball);
    }

    private static bool ShouldCollect(FootballMatch match, TeamAi team, Player keeper, Vector2 ball, GoalEnd end)
    {
        FootballWorld world = match.World;
        if (world.Ball.Status != BallStatus.Live
            || !world.Pitch.IsInPenaltyArea(ball, end)
            || world.Ball.Velocity.Length() > team.Config.Keeper.RushBallSpeed)
        {
            return false;
        }

        float keeperTime = match.Forecast.Intercept(keeper, TeamAi.TopSpeed(world, keeper), team.Level.KeeperReactionSeconds, 0.5f).Time;
        foreach (Player opponent in match.Teams[1 - team.Team.Index].Players)
        {
            float time = match.Forecast.Intercept(opponent, TeamAi.TopSpeed(world, opponent), 0f, 0.5f).Time;
            if (time < keeperTime - 0.2f)
            {
                return false;
            }
        }

        return true;
    }

    private static void Clear(FootballMatch match, TeamAi team, Player keeper, AiBrain brain)
    {
        FootballWorld world = match.World;
        AiKeeper config = team.Config.Keeper;
        // Kick to the most advanced free teammate within reach, else upfield towards the wing with fewer opponents.
        Vector2 from = keeper.Position;
        Vector2? target = team.LongTarget(match, from);
        if (target is null)
        {
            int left = 0;
            foreach (Player opponent in match.Teams[1 - team.Team.Index].Players)
            {
                left += opponent.Position.X < 0f ? 1 : -1;
            }

            float side = left < 0 ? -1f : 1f;
            target = from + (team.Team.AttackDirection * 30f) + new Vector2(side * world.Random.Range(6f, 14f), 0f);
        }

        Vector2 offset = target.Value - from;
        float charge = BallActions.SetPieceChargeForDistance(world, SetPieceKind.GoalKick, offset.Length());
        SetPieceKickConfig kick = world.Controls.SetPieces.GoalKick;
        float speed = VectorMath.Lerp(kick.MinSpeed, kick.MaxSpeed, charge) * (config.KickSpeed / kick.MaxSpeed);
        float lift = VectorMath.Lerp(kick.MinLift, kick.MaxLift, charge) * (config.KickLift / kick.MaxLift);
        world.Kick(keeper, offset, speed, lift, KickKind.KeeperKick, 0f);
        brain.Decision = "clear";
    }
}

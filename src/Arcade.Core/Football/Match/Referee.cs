using System.Numerics;
using Arcade.Core.Football.Pitch;

namespace Arcade.Core.Football.Match;

/// <summary>A restart awarded by the referee.</summary>
/// <param name="Kind">Kick-off, throw-in, corner or goal kick.</param>
/// <param name="TeamIndex">Team taking the restart.</param>
/// <param name="Spot">Where the ball is placed.</param>
/// <param name="Inward">Unit vector pointing into the pitch (zero for a kick-off: any direction).</param>
public readonly record struct Restart(SetPieceKind Kind, int TeamIndex, Vector2 Spot, Vector2 Inward);

/// <summary>Laws of the game that decide restarts. Pure functions of the ball position and the last touch.</summary>
public static class Referee
{
    /// <summary>Distance of corner and throw-in spots from the lines (m), so that the ball is clearly in.</summary>
    private const float SpotInset = 0.3f;

    /// <summary>
    /// Returns the restart when the whole ball has crossed a touchline or a goal line outside the goal,
    /// or null while the ball is in play. <paramref name="defendingTeamOf"/> gives the team defending a goal end.
    /// </summary>
    public static Restart? CheckOutOfPlay(Vector3 ball, float radius, PitchGeometry pitch, int lastToucherTeam, Func<GoalEnd, int> defendingTeamOf)
    {
        ArgumentNullException.ThrowIfNull(pitch);
        ArgumentNullException.ThrowIfNull(defendingTeamOf);
        float side = ball.X >= 0f ? 1f : -1f;
        if (MathF.Abs(ball.Y) > pitch.HalfLength + radius)
        {
            GoalEnd end = ball.Y < 0f ? GoalEnd.Top : GoalEnd.Bottom;
            if (pitch.IsBallInGoal(ball, radius, out _))
            {
                return null;
            }

            int defending = defendingTeamOf(end);
            float line = pitch.GoalLineY(end);
            float outward = PitchGeometry.Outward(end);
            if (lastToucherTeam == defending)
            {
                var corner = new Vector2(side * (pitch.HalfWidth - SpotInset), line - (outward * SpotInset));
                Vector2 inward = Vector2.Normalize(new Vector2(-side, -outward));
                return new Restart(SetPieceKind.Corner, 1 - defending, corner, inward);
            }

            float goalAreaDepth = pitch.Definition.GoalAreaDepth;
            var goalKick = new Vector2(side * pitch.Definition.GoalAreaWidth / 4f, line - (outward * (goalAreaDepth - 0.5f)));
            return new Restart(SetPieceKind.GoalKick, defending, goalKick, new Vector2(0f, -outward));
        }

        if (MathF.Abs(ball.X) > pitch.HalfWidth + radius)
        {
            float y = Math.Clamp(ball.Y, -pitch.HalfLength + 1f, pitch.HalfLength - 1f);
            int team = lastToucherTeam < 0 ? 0 : 1 - lastToucherTeam;
            return new Restart(SetPieceKind.ThrowIn, team, new Vector2(side * pitch.HalfWidth, y), new Vector2(-side, 0f));
        }

        return null;
    }
}

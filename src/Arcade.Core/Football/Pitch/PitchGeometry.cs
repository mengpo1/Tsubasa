using System.Numerics;

namespace Arcade.Core.Football.Pitch;

/// <summary>The two goal ends. The pitch's long axis is vertical on screen, as in Kick Off 2.</summary>
public enum GoalEnd
{
    /// <summary>Goal at the top of the screen (negative Y).</summary>
    Top,

    /// <summary>Goal at the bottom of the screen (positive Y).</summary>
    Bottom,
}

/// <summary>
/// Runtime geometry of a pitch, derived from a <see cref="PitchDefinition"/>.
/// Origin at the centre spot; +X towards the right touchline, +Y towards the bottom goal, Z up.
/// </summary>
public sealed class PitchGeometry
{
    public PitchGeometry(PitchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        Definition = definition;
        HalfLength = definition.Length / 2f;
        HalfWidth = definition.Width / 2f;
        GoalHalfWidth = definition.GoalWidth / 2f;
        GoalHeight = definition.GoalHeight;
        GoalDepth = definition.GoalDepth;
        PostRadius = definition.PostRadius;
        BoardsHalfWidth = HalfWidth + definition.BoundaryMargin;
        BoardsHalfLength = HalfLength + definition.BoundaryMargin;
    }

    public PitchDefinition Definition { get; }

    public float HalfLength { get; }

    public float HalfWidth { get; }

    public float GoalHalfWidth { get; }

    public float GoalHeight { get; }

    public float GoalDepth { get; }

    public float PostRadius { get; }

    public float BoardsHalfWidth { get; }

    public float BoardsHalfLength { get; }

    /// <summary>Y coordinate of the goal line of an end.</summary>
    public float GoalLineY(GoalEnd end) => end == GoalEnd.Top ? -HalfLength : HalfLength;

    /// <summary>+1 or -1: the direction (along Y) going from the pitch into the goal.</summary>
    public static float Outward(GoalEnd end) => end == GoalEnd.Top ? -1f : 1f;

    /// <summary>Centre of the goal line of an end.</summary>
    public Vector2 GoalCentre(GoalEnd end) => new(0f, GoalLineY(end));

    public Vector2 PenaltySpot(GoalEnd end) => new(0f, GoalLineY(end) - (Outward(end) * Definition.PenaltySpotDistance));

    /// <summary>True when the point is inside the lines (lines belong to the pitch).</summary>
    public bool IsInsideLines(Vector2 point) => MathF.Abs(point.X) <= HalfWidth && MathF.Abs(point.Y) <= HalfLength;

    /// <summary>
    /// True when the whole ball has crossed the goal line between the posts and under the bar,
    /// i.e. it is inside a goal. <paramref name="ballBottom"/> is the height of the ball's lowest point.
    /// </summary>
    public bool IsBallInGoal(Vector3 ballBottom, float ballRadius, out GoalEnd end)
    {
        foreach (GoalEnd candidate in Ends)
        {
            float depth = (ballBottom.Y - GoalLineY(candidate)) * Outward(candidate);
            if (depth > ballRadius
                && MathF.Abs(ballBottom.X) < GoalHalfWidth
                && ballBottom.Z + ballRadius < GoalHeight)
            {
                end = candidate;
                return true;
            }
        }

        end = GoalEnd.Top;
        return false;
    }

    public static IReadOnlyList<GoalEnd> Ends { get; } = [GoalEnd.Top, GoalEnd.Bottom];
}

using Arcade.Core.Content;

namespace Arcade.Core.Football.Pitch;

/// <summary>A pitch: dimensions, surface and look (<c>pitches/&lt;id&gt;.json</c>). Dimensions in metres.</summary>
public sealed record PitchDefinition : IContentData
{
    public const string Folder = "pitches";

    public required string Id { get; init; }

    public required string NameKey { get; init; }

    /// <summary>Id of the <see cref="SurfaceDefinition"/> used by this pitch.</summary>
    public required string Surface { get; init; }

    /// <summary>Goal line to goal line (the long axis, vertical on screen).</summary>
    public required float Length { get; init; }

    /// <summary>Touchline to touchline.</summary>
    public required float Width { get; init; }

    public required float GoalWidth { get; init; }

    public required float GoalHeight { get; init; }

    public required float GoalDepth { get; init; }

    public required float PostRadius { get; init; }

    public required float PenaltyAreaDepth { get; init; }

    public required float PenaltyAreaWidth { get; init; }

    public required float GoalAreaDepth { get; init; }

    public required float GoalAreaWidth { get; init; }

    public required float PenaltySpotDistance { get; init; }

    public required float CentreCircleRadius { get; init; }

    public required float CornerArcRadius { get; init; }

    public required float LineWidth { get; init; }

    /// <summary>Distance between the lines and the boards around the pitch.</summary>
    public required float BoundaryMargin { get; init; }

    public required PitchVisual Visual { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.NotEmpty(problems, "id", Id);
        DataChecks.NotEmpty(problems, "nameKey", NameKey);
        DataChecks.NotEmpty(problems, "surface", Surface);
        DataChecks.InRange(problems, "length", Length, 20f, 300f);
        DataChecks.InRange(problems, "width", Width, 10f, 200f);
        DataChecks.InRange(problems, "goalWidth", GoalWidth, 1f, Width * 0.8f);
        DataChecks.InRange(problems, "goalHeight", GoalHeight, 0.5f, 10f);
        DataChecks.InRange(problems, "goalDepth", GoalDepth, 0.3f, 10f);
        DataChecks.InRange(problems, "postRadius", PostRadius, 0.01f, 0.5f);
        DataChecks.InRange(problems, "penaltyAreaDepth", PenaltyAreaDepth, 0f, Length / 2f);
        DataChecks.InRange(problems, "penaltyAreaWidth", PenaltyAreaWidth, 0f, Width);
        DataChecks.InRange(problems, "goalAreaDepth", GoalAreaDepth, 0f, PenaltyAreaDepth);
        DataChecks.InRange(problems, "goalAreaWidth", GoalAreaWidth, 0f, PenaltyAreaWidth);
        DataChecks.InRange(problems, "penaltySpotDistance", PenaltySpotDistance, 0f, Length / 2f);
        DataChecks.InRange(problems, "centreCircleRadius", CentreCircleRadius, 0f, Width / 2f);
        DataChecks.NonNegative(problems, "cornerArcRadius", CornerArcRadius);
        DataChecks.InRange(problems, "lineWidth", LineWidth, 0.01f, 1f);
        DataChecks.InRange(problems, "boundaryMargin", BoundaryMargin, GoalDepth + 0.5f, 50f);
        DataChecks.Nested(problems, "visual", Visual);
    }
}

/// <summary>Look of a pitch, as logical asset ids and colours. Read by the renderer only.</summary>
public sealed record PitchVisual : IContentData
{
    /// <summary>Texture id tiled over the playing area.</summary>
    public required string GrassTexture { get; init; }

    /// <summary>Texture id tiled around the pitch (beyond the lines).</summary>
    public required string SurroundTexture { get; init; }

    /// <summary>Width (m) of the mowing stripes, 0 for none.</summary>
    public required float StripeWidth { get; init; }

    /// <summary>Brightness multiplier of every other stripe.</summary>
    public required float StripeShade { get; init; }

    public required string LineColor { get; init; }

    public required string GoalColor { get; init; }

    public required string NetTexture { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.NotEmpty(problems, "grassTexture", GrassTexture);
        DataChecks.NotEmpty(problems, "surroundTexture", SurroundTexture);
        DataChecks.NonNegative(problems, "stripeWidth", StripeWidth);
        DataChecks.InRange(problems, "stripeShade", StripeShade, 0f, 2f);
        if (!ColorRgb.TryParse(LineColor, out _))
        {
            problems.Add($"lineColor '{LineColor}' is not a #RRGGBB colour");
        }

        if (!ColorRgb.TryParse(GoalColor, out _))
        {
            problems.Add($"goalColor '{GoalColor}' is not a #RRGGBB colour");
        }

        DataChecks.NotEmpty(problems, "netTexture", NetTexture);
    }
}

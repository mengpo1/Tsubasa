using Arcade.Core.Content;

namespace Arcade.Core.Football.Pitch;

/// <summary>
/// Physical behaviour of a playing surface (<c>surfaces/&lt;id&gt;.json</c>). Every field is a multiplier or an
/// amount applied by the generic physics; the physics never tests surface names, so mods can add surfaces freely.
/// </summary>
public sealed record SurfaceDefinition : IContentData
{
    public const string Folder = "surfaces";

    public required string Id { get; init; }

    /// <summary>Localisation key of the display name.</summary>
    public required string NameKey { get; init; }

    public required float RollingResistanceScale { get; init; }

    public required float RollingDragScale { get; init; }

    public required float BounceRestitutionScale { get; init; }

    public required float BounceFrictionScale { get; init; }

    public required float AirDragScale { get; init; }

    /// <summary>Maximum random change of direction when the ball lands (degrees). Bumpy pitches.</summary>
    public required float BounceDeviationDegrees { get; init; }

    /// <summary>Maximum random change of direction per metre rolled (degrees).</summary>
    public required float RollingDeviationDegreesPerMetre { get; init; }

    /// <summary>Multiplier of the player fatigue drain.</summary>
    public required float StaminaDrainScale { get; init; }

    /// <summary>Multiplier of player acceleration, braking and turning (grip).</summary>
    public required float PlayerGripScale { get; init; }

    public void Validate(ICollection<string> problems)
    {
        DataChecks.NotEmpty(problems, "id", Id);
        DataChecks.NotEmpty(problems, "nameKey", NameKey);
        DataChecks.InRange(problems, "rollingResistanceScale", RollingResistanceScale, 0f, 20f);
        DataChecks.InRange(problems, "rollingDragScale", RollingDragScale, 0f, 20f);
        DataChecks.InRange(problems, "bounceRestitutionScale", BounceRestitutionScale, 0f, 1.8f);
        DataChecks.InRange(problems, "bounceFrictionScale", BounceFrictionScale, 0f, 1.2f);
        DataChecks.InRange(problems, "airDragScale", AirDragScale, 0f, 20f);
        DataChecks.InRange(problems, "bounceDeviationDegrees", BounceDeviationDegrees, 0f, 90f);
        DataChecks.InRange(problems, "rollingDeviationDegreesPerMetre", RollingDeviationDegreesPerMetre, 0f, 45f);
        DataChecks.InRange(problems, "staminaDrainScale", StaminaDrainScale, 0f, 10f);
        DataChecks.InRange(problems, "playerGripScale", PlayerGripScale, 0.05f, 3f);
    }
}

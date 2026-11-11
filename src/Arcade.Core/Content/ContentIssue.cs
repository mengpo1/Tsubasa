namespace Arcade.Core.Content;

public enum ContentIssueSeverity
{
    Warning,
    Error,
}

/// <summary>A problem found while loading content, kept so that the game can show it to the player.</summary>
public sealed record ContentIssue(ContentIssueSeverity Severity, string PackId, string Message);

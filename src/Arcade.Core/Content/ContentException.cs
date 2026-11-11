namespace Arcade.Core.Content;

/// <summary>Raised when game data or a mod is missing, malformed or invalid. The message always names the file involved.</summary>
public sealed class ContentException : Exception
{
    public ContentException()
    {
    }

    public ContentException(string message)
        : base(message)
    {
    }

    public ContentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

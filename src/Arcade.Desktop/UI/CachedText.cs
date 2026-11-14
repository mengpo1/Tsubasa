namespace Arcade.Desktop.UI;

/// <summary>Re-formats a HUD string only when the value it shows changes (no per-frame string allocation).</summary>
internal sealed class CachedText<T>
    where T : IEquatable<T>
{
    private readonly Func<T, string> _format;
    private T? _value;
    private string? _text;
    private int _version = -1;

    public CachedText(Func<T, string> format)
    {
        _format = format;
    }

    /// <param name="version">Bump to force a refresh (e.g. after a language change).</param>
    public string Get(T value, int version = 0)
    {
        if (_text is null || version != _version || !value.Equals(_value))
        {
            _value = value;
            _version = version;
            _text = _format(value);
        }

        return _text;
    }
}

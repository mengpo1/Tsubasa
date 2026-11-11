using System.Globalization;
using System.Text;

namespace Arcade.Core.Common;

public enum LogLevel
{
    Debug,
    Info,
    Warning,
    Error,
}

/// <summary>One structured log record: a fixed message plus key/value context.</summary>
public sealed record LogEntry(LogLevel Level, string Category, string Message, IReadOnlyList<KeyValuePair<string, object?>> Fields)
{
    public string Format()
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"[{Level}] {Category}: {Message}");
        foreach (KeyValuePair<string, object?> field in Fields)
        {
            builder.Append(CultureInfo.InvariantCulture, $" {field.Key}={field.Value}");
        }

        return builder.ToString();
    }
}

/// <summary>
/// Minimal structured logger. Passed explicitly to the components that need it (no global instance).
/// Keeps the most recent entries in memory so the application can display problems on screen.
/// </summary>
public sealed class GameLog
{
    private readonly int _capacity;
    private readonly Queue<LogEntry> _recent = new();
    private readonly List<Action<LogEntry>> _sinks = [];

    public GameLog(int capacity = 200)
    {
        _capacity = capacity;
    }

    public IReadOnlyCollection<LogEntry> Recent => _recent;

    public int ErrorCount { get; private set; }

    public int WarningCount { get; private set; }

    public void AddSink(Action<LogEntry> sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        _sinks.Add(sink);
    }

    public void Debug(string category, string message, params (string Key, object? Value)[] fields) => Write(LogLevel.Debug, category, message, fields);

    public void Info(string category, string message, params (string Key, object? Value)[] fields) => Write(LogLevel.Info, category, message, fields);

    public void Warn(string category, string message, params (string Key, object? Value)[] fields) => Write(LogLevel.Warning, category, message, fields);

    public void Error(string category, string message, params (string Key, object? Value)[] fields) => Write(LogLevel.Error, category, message, fields);

    public void Write(LogLevel level, string category, string message, params (string Key, object? Value)[] fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var pairs = new KeyValuePair<string, object?>[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            pairs[i] = new KeyValuePair<string, object?>(fields[i].Key, fields[i].Value);
        }

        var entry = new LogEntry(level, category, message, pairs);
        if (level == LogLevel.Error)
        {
            ErrorCount++;
        }
        else if (level == LogLevel.Warning)
        {
            WarningCount++;
        }

        _recent.Enqueue(entry);
        while (_recent.Count > _capacity)
        {
            _recent.Dequeue();
        }

        foreach (Action<LogEntry> sink in _sinks)
        {
            sink(entry);
        }
    }
}

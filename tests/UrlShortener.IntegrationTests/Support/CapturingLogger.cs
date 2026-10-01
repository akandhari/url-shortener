using Microsoft.Extensions.Logging;

namespace UrlShortener.IntegrationTests.Support;

/// <summary>Keeps every log entry so tests can assert that nothing went wrong silently.</summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        lock (Entries)
        {
            Entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}

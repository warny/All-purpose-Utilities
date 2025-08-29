using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace UtilsTest.Net;

/// <summary>
/// Simple logger that stores formatted entries for inspection during tests.
/// </summary>
internal sealed class ListLogger : ILogger
{
    /// <summary>
    /// Gets the log entries recorded by this logger.
    /// </summary>
    public List<string> Entries { get; } = new();

    /// <inheritdoc/>
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        Entries.Add(formatter(state, exception));
    }
}

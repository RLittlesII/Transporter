using Microsoft.Extensions.Logging;

namespace Transporter.UnitTests.Integrations.OpenSky;

/// <summary>
/// An <see cref="ILogger{TCategoryName}"/> that keeps what was written, so a test can assert on a
/// log line rather than on a call.
/// </summary>
/// <typeparam name="T">The category the logger is for.</typeparam>
/// <remarks>
/// Hand-written rather than substituted because <c>ILogger.Log&lt;TState&gt;</c> takes the message
/// as a generic struct the framework builds: a substitute can be asked whether it was called, but
/// the assertion cannot read the rendered text, which is exactly what B-027 is about — the credit
/// is in the line and a secret is not. This records a framework interface, not a contract of this
/// repository's.
/// </remarks>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    /// <summary>Gets what was written, in order.</summary>
    internal List<(LogLevel Level, string Message)> Entries { get; } = [];

    /// <summary>Gets every line written, whatever its level.</summary>
    internal IEnumerable<string> Messages => Entries.Select(static entry => entry.Message);

    /// <inheritdoc/>
    public IDisposable BeginScope<TState>(TState state)
        where TState : notnull => new Scope();

    /// <inheritdoc/>
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc/>
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        Entries.Add((logLevel, formatter(state, exception)));

    /// <summary>Gets the lines written at one level.</summary>
    /// <param name="level">The level to read.</param>
    /// <returns>The messages written at that level.</returns>
    internal IEnumerable<string> At(LogLevel level) =>
        Entries.Where(entry => entry.Level == level).Select(static entry => entry.Message);

    private sealed class Scope : IDisposable
    {
        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}

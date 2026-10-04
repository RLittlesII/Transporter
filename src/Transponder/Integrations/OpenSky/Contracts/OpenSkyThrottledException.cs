namespace Transponder.Integrations.OpenSky.Contracts;

/// <summary>
/// Thrown when OpenSky answers with a throttle rather than a payload. Catching it is part of the contract's
/// surface: <see cref="RetryAfter"/> is the delay the provider asked for, never one this code invented.
/// </summary>
internal sealed class OpenSkyThrottledException : Exception
{
    public OpenSkyThrottledException(TimeSpan retryAfter)
        : base($"OpenSky throttled the request and asked to be left alone for {retryAfter}.") =>
        RetryAfter = retryAfter;

    /// <summary>
    /// Gets the delay the provider asked for.
    /// </summary>
    public TimeSpan RetryAfter { get; }
}

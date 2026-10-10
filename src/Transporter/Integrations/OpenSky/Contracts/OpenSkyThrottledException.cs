using System;

namespace Transporter.Integrations.OpenSky.Contracts;

// The three conventional constructors would each build this exception with no delay, and the delay
// is the reason it exists: RCS1194's fix adds them, so the rule is off for this type.
#pragma warning disable RCS1194
/// <summary>
/// Thrown when OpenSky answers with a throttle rather than a payload. Catching it is part of the contract's
/// surface: <see cref="RetryAfter"/> is the delay the provider asked for, never one this code invented.
/// </summary>
internal sealed class OpenSkyThrottledException : Exception
#pragma warning restore RCS1194
{
    public OpenSkyThrottledException(TimeSpan retryAfter)
        : base($"OpenSky throttled the request and asked to be left alone for {retryAfter}.") =>
        RetryAfter = retryAfter;

    /// <summary>
    /// Gets the delay the provider asked for.
    /// </summary>
    public TimeSpan RetryAfter { get; }
}

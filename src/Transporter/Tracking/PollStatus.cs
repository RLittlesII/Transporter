using System;
using LanguageExt;

namespace Transporter.Tracking;

/// <summary>
/// What a source says about its polling: when the next poll is due, and the interval a provider
/// asked for while it refuses (fleet-pipeline B-040).
/// </summary>
/// <remarks>
/// One value rather than two streams, because one poll reports both (<c>aircraft-source</c> B-055
/// moves the due instant when it refuses), so a consumer cannot pair a refusal with the wrong
/// instant. Nothing here counts down: "seconds left" changes with no report, and the countdown is
/// the view's animation toward <see cref="NextDue"/> (<c>fleet-dashboard</c> B-036).
/// </remarks>
public sealed record PollStatus
{
    /// <summary>Gets the status of a source that does not poll, or has not yet said when it will.</summary>
    public static PollStatus None { get; } = new();

    /// <summary>Gets when the next poll is due, if the source polls and has said.</summary>
    public Option<DateTimeOffset> NextDue { get; init; }

    /// <summary>Gets the interval the provider asked for while it refuses, and none once a poll is applied.</summary>
    public Option<TimeSpan> RefusedFor { get; init; }
}

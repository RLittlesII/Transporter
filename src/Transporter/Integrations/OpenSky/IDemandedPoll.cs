using System;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;

namespace Transporter.Integrations.OpenSky;

/// <summary>What a demanded poll needs of the poller: when the next poll is due, and one call now.</summary>
/// <remarks>
/// A seam of its own rather than two members on <see cref="IAircraftSnapshotClient"/>, which stays
/// the one method a strategy's subscription owns (B-053, ADR-0012).
/// </remarks>
internal interface IDemandedPoll
{
    /// <summary>Gets when the provider was last called, cadence polls included; absent until one has been.</summary>
    Option<DateTimeOffset> LastPoll { get; }

    /// <summary>
    /// Gets when the next poll is due — the interval after the last poll, or the instant a <c>429</c>
    /// deferred it to (B-028) — absent until the provider has been called.
    /// </summary>
    Option<DateTimeOffset> NextDue { get; }

    /// <summary>Performs one poll now, whoever asked for it.</summary>
    /// <param name="cancellationToken">Stops the poll.</param>
    /// <returns>The running poll.</returns>
    Task PollNow(CancellationToken cancellationToken);
}

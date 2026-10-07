using System;
using System.Threading;
using System.Threading.Tasks;
using LanguageExt;

namespace Transponder.Integrations.OpenSky;

/// <summary>What a demanded poll needs of the poller: when it last called the provider, and one call now.</summary>
/// <remarks>
/// A seam of its own rather than two members on <see cref="IAircraftSnapshotClient"/>, which stays
/// the one method a strategy's subscription owns (B-053, ADR-0012).
/// </remarks>
internal interface IDemandedPoll
{
    /// <summary>Gets when the provider was last called, cadence polls included; absent until one has been.</summary>
    Option<DateTimeOffset> LastPoll { get; }

    /// <summary>Performs one poll now, whoever asked for it.</summary>
    /// <param name="cancellationToken">Stops the poll.</param>
    /// <returns>The running poll.</returns>
    Task PollNow(CancellationToken cancellationToken);
}

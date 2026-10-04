using LanguageExt;
using Transponder.Integrations.OpenSky.Contracts;

namespace Transponder.UnitTests.Integrations.OpenSky;

/// <summary>
/// The hand-written double for the OpenSky contract. It is deliberately not a mock: an unset response throws
/// and names what was not set, so a test above this seam cannot pass on a value nobody arranged.
/// </summary>
internal sealed class OpenSkyApiFake : IOpenSkyApi
{
    public Either<OpenSkyThrottled, OpenSkyStatesResponse>? NextStates { get; set; }

    /// <inheritdoc/>
    Task<Either<OpenSkyThrottled, OpenSkyStatesResponse>> IOpenSkyApi.GetStates(
        double lamin,
        double lomin,
        double lamax,
        double lomax,
        bool extended,
        CancellationToken cancellationToken) =>
        NextStates is { } response
            ? Task.FromResult(response)
            : throw new InvalidOperationException(
                $"{nameof(OpenSkyApiFake)} was asked for {nameof(IOpenSkyApi.GetStates)} with no response set. "
                + $"Assign {nameof(NextStates)} before the call.");
}

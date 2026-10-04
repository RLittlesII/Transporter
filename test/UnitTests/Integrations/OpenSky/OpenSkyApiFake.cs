using Transponder.Integrations.OpenSky.Contracts;

namespace Transponder.UnitTests.Integrations.OpenSky;

/// <summary>
/// The hand-written double for the OpenSky contract. It is deliberately not a mock: a double built with no
/// response throws and names what was not set, so a test above this seam cannot pass on a value nobody arranged.
/// </summary>
internal sealed class OpenSkyApiFake : IOpenSkyApi
{
    public OpenSkyApiFake(OpenSkyStatesResponse? response = null) => _response = response;

    /// <inheritdoc/>
    Task<OpenSkyStatesResponse> IOpenSkyApi.GetStates(
        double lamin,
        double lomin,
        double lamax,
        double lomax,
        bool extended,
        CancellationToken cancellationToken) =>
        _response is { } configured
            ? Task.FromResult(configured)
            : throw new InvalidOperationException(
                $"{nameof(OpenSkyApiFake)} was asked for {nameof(IOpenSkyApi.GetStates)} with no "
                + $"response set. Build it with an {nameof(OpenSkyStatesResponse)} before the call.");

    private readonly OpenSkyStatesResponse? _response;
}

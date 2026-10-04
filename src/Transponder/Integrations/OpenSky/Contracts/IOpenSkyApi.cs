using LanguageExt;

namespace Transponder.Integrations.OpenSky.Contracts;

/// <summary>
/// The OpenSky API contract: one method per endpoint this application consumes.
/// </summary>
internal interface IOpenSkyApi
{
    /// <summary>
    /// Reads the state vectors inside a bounding box.
    /// </summary>
    /// <param name="lamin">The box's lower latitude bound, in degrees.</param>
    /// <param name="lomin">The box's lower longitude bound, in degrees.</param>
    /// <param name="lamax">The box's upper latitude bound, in degrees.</param>
    /// <param name="lomax">The box's upper longitude bound, in degrees.</param>
    /// <param name="extended">Whether to ask the provider for the aircraft category element.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The provider's response, or the throttle it answered with.</returns>
    Task<Either<OpenSkyThrottled, OpenSkyStatesResponse>> GetStates(
        double lamin,
        double lomin,
        double lamax,
        double lomax,
        bool extended,
        CancellationToken cancellationToken);
}

using System.Threading;
using System.Threading.Tasks;

namespace Transporter.Integrations.OpenSky.Contracts;

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
    /// <returns>What the provider sent.</returns>
    /// <exception cref="OpenSkyThrottledException">The provider answered with a throttle instead of a payload.</exception>
    Task<OpenSkyStatesResponse> GetStates(
        double lamin,
        double lomin,
        double lamax,
        double lomax,
        bool extended,
        CancellationToken cancellationToken);
}

using System.Threading;
using System.Threading.Tasks;

namespace Transponder.Integrations.OpenSky.Authentication;

/// <summary>
/// The one place a bearer token is obtained and held. One token cache, owned here rather than at
/// a call site: § 4 row 7 rules out a token fetched once at startup and any call site attaching
/// its own credential.
/// </summary>
internal interface IOpenSkyTokenSource
{
    /// <summary>
    /// Gets the current token, obtaining one when there is none and when the one held has expired.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request that obtains a token.</param>
    /// <returns>A token to attach to a request.</returns>
    Task<string> Current(CancellationToken cancellationToken);

    /// <summary>
    /// Discards the token held, so the next <see cref="Current"/> obtains a new one.
    /// </summary>
    /// <remarks>
    /// What a <c>401</c> calls. A token can die before it expires, so expiry alone is not enough
    /// to decide a refresh is needed (B-026).
    /// </remarks>
    void Invalidate();
}

using System.Threading;
using System.Threading.Tasks;

namespace Transponder.Recording;

/// <summary>
/// Releases a recording's payloads at the spacing they arrived with (B-007).
/// </summary>
/// <remarks>
/// The payload is answered still raw, which is what keeps a recording replayable after a converter
/// fix (B-014) and what lets one pacer serve both feeds: parsing belongs to whatever substitutes
/// over this, at a depth that differs per transport (B-022, B-023).
/// </remarks>
internal interface IRecordingPacer
{
    /// <summary>
    /// Waits until the next payload is due, then answers it verbatim.
    /// </summary>
    /// <param name="cancellationToken">Stops the wait.</param>
    /// <returns>The payload as the provider sent it; the recording rewinds rather than ending.</returns>
    Task<string> Next(CancellationToken cancellationToken);
}

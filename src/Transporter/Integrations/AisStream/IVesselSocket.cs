namespace Transporter.Integrations.AisStream;

/// <summary>One socket connection carrying text frames.</summary>
internal interface IVesselSocket : IAsyncDisposable
{
    /// <summary>Opens the socket with compression requested.</summary>
    /// <param name="endpoint">The stream address.</param>
    /// <param name="cancellation">Stops the connection attempt.</param>
    /// <returns>The completion of the connection attempt.</returns>
    Task Open(Uri endpoint, CancellationToken cancellation);

    /// <summary>Sends one complete text frame.</summary>
    /// <param name="frame">The text to send.</param>
    /// <param name="cancellation">Stops the send.</param>
    /// <returns>The completion of the send.</returns>
    Task Send(string frame, CancellationToken cancellation);

    /// <summary>Reads complete text frames until the peer closes.</summary>
    /// <param name="cancellation">Stops receiving frames.</param>
    /// <returns>The received text frames.</returns>
    IAsyncEnumerable<string> Frames(CancellationToken cancellation);
}

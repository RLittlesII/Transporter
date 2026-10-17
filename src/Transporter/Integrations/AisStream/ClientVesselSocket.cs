using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;

namespace Transporter.Integrations.AisStream;

internal sealed class ClientVesselSocket : IVesselSocket
{
    public ClientVesselSocket()
        : this(new ClientWebSocket())
    {
    }

    public ClientVesselSocket(WebSocket socket)
    {
        _socket = socket;
    }

    /// <inheritdoc/>
    async Task IVesselSocket.Open(Uri endpoint, CancellationToken cancellation)
    {
        if (_socket is not ClientWebSocket socket)
        {
            throw new InvalidOperationException("Only a production client socket can open a connection.");
        }

        socket.Options.DangerousDeflateOptions = new WebSocketDeflateOptions();
        await socket.ConnectAsync(endpoint, cancellation).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    async Task IVesselSocket.Send(string frame, CancellationToken cancellation) =>
        await _socket.SendAsync(Encoding.UTF8.GetBytes(frame).AsMemory(), WebSocketMessageType.Text, true, cancellation).ConfigureAwait(false);

    /// <inheritdoc/>
    async IAsyncEnumerable<string> IVesselSocket.Frames([EnumeratorCancellation] CancellationToken cancellation)
    {
        var buffer = new byte[8192];
        using var message = new MemoryStream();
        while (!cancellation.IsCancellationRequested)
        {
            var received = await _socket.ReceiveAsync(buffer.AsMemory(), cancellation).ConfigureAwait(false);
            if (received.MessageType == WebSocketMessageType.Close)
            {
                yield break;
            }

            if (received.MessageType is not (WebSocketMessageType.Text or WebSocketMessageType.Binary))
            {
                continue;
            }

            message.Write(buffer, 0, received.Count);
            if (received.EndOfMessage)
            {
                yield return Encoding.UTF8.GetString(message.GetBuffer(), 0, checked((int) message.Length));
                message.SetLength(0);
            }
        }
    }

    /// <inheritdoc/>
    async ValueTask IAsyncDisposable.DisposeAsync()
    {
        try
        {
            if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None).ConfigureAwait(false);
            }
        }
        finally
        {
            _socket.Dispose();
        }
    }

    private readonly WebSocket _socket;
}

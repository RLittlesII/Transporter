using System.Net.WebSockets;
using AwesomeAssertions;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.AisStream;

namespace Transporter.UnitTests.Integrations.AisStream;

public class ClientVesselSocketTests
{
    /// <summary>B-007 and § 4 row 15: receive fragments are assembled before UTF-8 JSON is decoded.</summary>
    /// <param name="messageType">The service's binary frame type, or compatible text.</param>
    /// <param name="fragments">Wire bytes, possibly splitting a multibyte character.</param>
    /// <param name="expected">The complete JSON confirmation the client must receive.</param>
    /// <returns>The completion of the scripted receive sequence.</returns>
    [Theory]
    [ClassData(typeof(VesselSocketFrameCases))]
    public async Task GivenUtf8JsonReceiveFragments_WhenFramesAreRead_ThenTheWholeMessageReachesTheClient(
        WebSocketMessageType messageType,
        byte[][] fragments,
        string expected)
    {
        // Given
        var socket = Substitute.For<WebSocket>();
        socket.State.Returns(WebSocketState.Open);
        socket.CloseOutputAsync(Arg.Any<WebSocketCloseStatus>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var received = 0;
        socket.ReceiveAsync(Arg.Any<Memory<byte>>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (received == fragments.Length)
            {
                return new ValueTask<ValueWebSocketReceiveResult>(new ValueWebSocketReceiveResult(0, WebSocketMessageType.Close, true));
            }

            var fragment = fragments[received++];
            fragment.CopyTo(call.Arg<Memory<byte>>().Span);
            return new ValueTask<ValueWebSocketReceiveResult>(
                new ValueWebSocketReceiveResult(fragment.Length, messageType, received == fragments.Length));
        });
        ClientVesselSocket subject = new ClientVesselSocketFixture().WithSocket(socket);
        await using IVesselSocket sut = subject;
        var messages = new List<string>();

        // When
        await foreach (var frame in sut.Frames(CancellationToken.None))
        {
            messages.Add(frame);
        }

        // Then
        messages.Should().Equal(expected);
        received.Should().Be(fragments.Length);
    }
}

/// <summary>Builds the adapter over a controlled BCL WebSocket.</summary>
[AutoFixture(typeof(ClientVesselSocket))]
internal partial class ClientVesselSocketFixture;

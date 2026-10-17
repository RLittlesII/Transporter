using System.Net.WebSockets;
using System.Text;

namespace Transporter.UnitTests.Integrations.AisStream;

/// <summary>Binary JSON, split UTF-8, payloads larger than the receive buffer, and text compatibility.</summary>
public sealed class VesselSocketFrameCases : TheoryData<WebSocketMessageType, byte[][], string>
{
    public VesselSocketFrameCases()
    {
        var bytes = Encoding.UTF8.GetBytes(Confirmation);
        Add(WebSocketMessageType.Binary, [bytes], Confirmation);
        var firstCharacterByte = Array.IndexOf(bytes, (byte) 0xc3);
        Add(WebSocketMessageType.Binary, [bytes[..(firstCharacterByte + 1)], bytes[(firstCharacterByte + 1)..]], Confirmation);
        var largeConfirmation = Confirmation.Replace("BAY CAFÉ", new string('x', 9000), StringComparison.Ordinal);
        var largeBytes = Encoding.UTF8.GetBytes(largeConfirmation);
        Add(WebSocketMessageType.Binary, [largeBytes[..8192], largeBytes[8192..]], largeConfirmation);
        Add(WebSocketMessageType.Text, [bytes], Confirmation);
    }

    private const string Confirmation = """
        {"MessageType":"SubscriptionConfirmation","Message":{"CompressionEnabled":false,"Comment":"BAY CAFÉ"}}
        """;
}

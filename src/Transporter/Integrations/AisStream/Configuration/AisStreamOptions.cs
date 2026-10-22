using LanguageExt;

namespace Transporter.Integrations.AisStream.Configuration;

internal sealed class AisStreamOptions
{
    internal const string Section = "AisStream";

    public Uri Endpoint { get; init; } = new("wss://stream.aisstream.io/v0/stream");

    public Option<string> ApiKey { get; init; }

    public TimeSpan ReconnectAfter { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>How long a vessel can be silent before the fleet removes it.</summary>
    public TimeSpan SilentAfter { get; init; } = TimeSpan.FromMinutes(10);
}

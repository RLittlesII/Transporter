namespace Transporter.Integrations.AisStream;

internal sealed record VesselSnapshot
{
    public required string Mmsi { get; init; }
}

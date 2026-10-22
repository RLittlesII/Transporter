namespace Transporter.UnitTests.Integrations.AisStream;

/// <summary>Go UTC text, including captured nanosecond precision, for each supported message kind.</summary>
public sealed class VesselReportedTimeCases : TheoryData<string, string, DateTimeOffset>
{
    public VesselReportedTimeCases()
    {
        foreach (var frame in new[] { VesselPayloads.PositionReport, VesselPayloads.StaticData })
        {
            var second = new DateTimeOffset(2026, 10, 20, 14, 32, 25, TimeSpan.Zero);
            Add(frame, "2026-10-20 14:32:25 +0000 UTC", second);
            Add(frame, "2026-10-20 14:32:25.1 +0000 UTC", second.AddTicks(1_000_000));
            Add(frame, "2026-10-20 14:32:25.1234567 +0000 UTC", second.AddTicks(1_234_567));
            Add(frame, "2026-10-20 14:32:25.12345678 +0000 UTC", second.AddTicks(1_234_567));
            Add(frame, "2026-10-20 14:32:25.123456789 +0000 UTC", second.AddTicks(1_234_567));
            Add(frame, "2026-10-20 14:32:25.000000001 +0000 UTC", second);
        }
    }
}

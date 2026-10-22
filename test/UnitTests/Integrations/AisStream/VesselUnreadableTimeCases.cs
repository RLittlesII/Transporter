namespace Transporter.UnitTests.Integrations.AisStream;

/// <summary>Missing meaning, wrong JSON types, invalid dates and malformed fractions must not create an instant.</summary>
public sealed class VesselUnreadableTimeCases : TheoryData<string>
{
    public VesselUnreadableTimeCases()
    {
        Add("null");
        Add("42");
        Add("true");
        Add("{}");
        Add("[]");
        Add("\"\"");
        Add("\"not a time\"");
        Add("\"2026-02-30 14:32:25 +0000 UTC\"");
        Add("\"2026-10-20 14:32:25.1234567x9 +0000 UTC\"");
        Add("\"2026-10-20 14:32:25.1234567891 +0000 UTC\"");
        Add("\"2026-10-20 14:32:25. +0000 UTC\"");
        Add("\"2026-10-20 14:32:25.123456789 +0100 CET\"");
    }
}

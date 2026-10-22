using LanguageExt;

namespace Transporter.UnitTests.Tracking;

/// <summary>The cache supplies a normalized name or absence to the projection.</summary>
internal sealed class VesselCachedNameCases : TheoryData<Option<string>, string?, string>
{
    public VesselCachedNameCases()
    {
        Add("BAY RUNNER", "BAY RUNNER", "BAY RUNNER");
        Add(Option<string>.None, null, "366999005");
    }
}

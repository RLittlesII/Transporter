using Transponder.UnitTests.Integrations.OpenSky;

namespace Transponder.UnitTests.Recording;

/// <summary>
/// The payloads B-002 is about: what the provider sent has to be what the line holds, whatever
/// the provider sent.
/// </summary>
/// <remarks>
/// Each case is a payload that a recorder tempted to reshape would reshape differently — an empty
/// set invites being written as nothing, a row of nulls invites being compacted, and a
/// seventeen-element row invites being padded to eighteen. None of them may be.
/// </remarks>
internal sealed class RecordingWriterCases : TheoryData<string, string>
{
    /// <summary>Initializes a new instance of the <see cref="RecordingWriterCases"/> class.</summary>
    public RecordingWriterCases()
    {
        Add("a payload reporting no aircraft at all", OpenSkyPayloads.NoRows);
        Add("a row whose optional elements are all absent", OpenSkyPayloads.Row(category: null, callsign: "null", squawk: "null"));
        Add("a seventeen-element row, the provider answering without the category", OpenSkyPayloads.Row(category: null));
        Add("an eighteen-element row carrying a category of zero", OpenSkyPayloads.Row(category: "0"));
        Add("a callsign that is padding and nothing else", OpenSkyPayloads.Row(category: "1", callsign: "\"        \""));
    }
}

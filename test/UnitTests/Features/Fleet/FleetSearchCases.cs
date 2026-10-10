namespace Transporter.UnitTests.Features.Fleet;

/// <summary>
/// What the user typed against whether an aircraft labelled <c>FLT0421</c> and registered in
/// Germany survives it (B-010).
/// </summary>
/// <remarks>
/// Each case is a clause of the claim with its own way of failing quietly. Lowercase and uppercase
/// bracket the case-insensitive comparison; the padded one is what a paste brings; empty and
/// whitespace-only are the gesture the audience is most likely to make, and an implementation that
/// matched nothing for them shows an empty grid the moment the box is cleared. <c>germany</c> is the
/// clause B-010 gained on 2026-10-07 — search reads every column's cell, not the label alone — and
/// <c>QFA</c> is the case that fails if matching ever becomes "admit everything".
/// </remarks>
public sealed class SearchTextCases : TheoryData<string, bool>
{
    public SearchTextCases()
    {
        Add("flt0421", true);
        Add("FLT0421", true);
        Add("  FLT04  ", true);
        Add(string.Empty, true);
        Add("   ", true);
        Add("germany", true);
        Add("QFA", false);
    }
}

/// <summary>
/// The search text, whether the on-the-ground choice is taken, which vehicle is offered, and whether
/// it survives the composed predicate (B-011).
/// </summary>
/// <remarks>
/// Three vehicles: <c>both</c> matches the search and is on the ground, <c>search</c> matches the
/// search and is airborne, <c>filter</c> is on the ground with a callsign the search misses. The
/// first three cases are the conjunction; the next two clear the search and the last two clear the
/// choice. The failure these catch is one input overwriting the predicate the other set — which
/// passes every test that only ever sets one, and reads to the person watching as a dropdown that
/// silently empties the search box.
/// </remarks>
public sealed class ComposedPredicateCases : TheoryData<string, bool, string, bool>
{
    public ComposedPredicateCases()
    {
        Add("FLT04", true, "both", true);
        Add("FLT04", true, "search", false);
        Add("FLT04", true, "filter", false);
        Add(string.Empty, true, "filter", true);
        Add(string.Empty, true, "search", false);
        Add("FLT04", false, "search", true);
        Add("FLT04", false, "filter", false);
    }
}

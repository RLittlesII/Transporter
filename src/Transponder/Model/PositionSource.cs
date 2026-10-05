namespace Transponder.Model;

/// <summary>How the source fixed an aircraft's position, by the codes README.md § "Response shape" index 16 names.</summary>
/// <remarks>
/// No <c>Unknown</c> member: that would be a value the provider never sent. A code this build does
/// not name is absent instead, which is a different fact and the one B-018 warns about.
/// </remarks>
public enum PositionSource
{
    /// <summary>Code 0 — reported by the aircraft itself.</summary>
    AdsB = 0,

    /// <summary>Code 1 — from a surveillance data exchange feed.</summary>
    Asterix = 1,

    /// <summary>Code 2 — multilaterated from receiver timing.</summary>
    Mlat = 2,

    /// <summary>Code 3 — from a FLARM collision-avoidance beacon.</summary>
    Flarm = 3,
}

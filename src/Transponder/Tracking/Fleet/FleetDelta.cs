using LanguageExt;
using Transponder.Model;

namespace Transponder.Tracking.Fleet;

/// <summary>The change between two vehicles, already formatted, or none where the cell did not change (B-042).</summary>
/// <param name="replaced">The vehicle the last update replaced.</param>
/// <param name="current">The vehicle now.</param>
/// <returns>The change, or none.</returns>
/// <remarks>A delegate rather than a <c>Func</c>, so the order of two arguments of one type is in the signature.</remarks>
public delegate Option<string> FleetDelta(TransportVehicle replaced, TransportVehicle current);

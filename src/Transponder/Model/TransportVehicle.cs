using System;
using LanguageExt;

namespace Transponder.Model;

/// <summary>
/// One tracked thing, whichever source reported it. The cache, the filters, the comparers, the
/// aggregates and the bindings are written against this type and never against a concrete one,
/// which is what makes a source swap a registration change (ADR-0005).
/// </summary>
/// <remarks>
/// Shared derivation is non-abstract here and the per-source answers are <see langword="abstract"/>,
/// so a new source is a compile error until it is finished.
/// </remarks>
public abstract class TransportVehicle
{
    /// <summary>Initializes a new instance of the <see cref="TransportVehicle"/> class.</summary>
    /// <param name="key">The cache key, stable for the life of the item.</param>
    /// <param name="lastContact">The instant the source last heard from the item.</param>
    /// <remarks>Both are set here so no derived type can produce a keyless item (ADR-0005 item 3).</remarks>
    protected TransportVehicle(string key, DateTimeOffset lastContact)
    {
        Key = key;
        LastContact = lastContact;
    }

    /// <summary>Gets the cache key — never null, never reused, and a plain string because a key that might be absent is not a key.</summary>
    public string Key { get; }

    /// <summary>Gets the instant the source last heard from this item.</summary>
    public DateTimeOffset LastContact { get; }

    /// <summary>Gets or sets the reported position, absent when the source has no fix for it.</summary>
    /// <remarks>Absent and <c>0,0</c> are different facts; the Gulf of Guinea is a real place.</remarks>
    public Option<GeoPosition> Position { get; set; }

    /// <summary>Gets what the identity column shows for this item.</summary>
    public abstract string Label { get; }

    /// <summary>Gets what a view groups this item by (fleet-pipeline B-013).</summary>
    /// <remarks>
    /// Abstract so a new source cannot inherit an answer (ADR-0005 item 2), and a string because a
    /// grouping key is a label; which key is being asked for is the description's (B-020).
    /// </remarks>
    public abstract string GroupKey { get; }

    /// <summary>Answers whether this item has been silent longer than the threshold allows.</summary>
    /// <param name="asOf">The instant to measure against.</param>
    /// <param name="threshold">How long silence is tolerated.</param>
    /// <returns><see langword="true"/> when the item is stale.</returns>
    /// <remarks>
    /// Derived, never stored, and the instant arrives as a parameter because the clock belongs to
    /// <c>IFleetTracker</c> rather than to a model type (B-043, ADR-0005 item 1).
    /// </remarks>
    public bool IsStale(DateTimeOffset asOf, TimeSpan threshold) => asOf - LastContact > threshold;
}

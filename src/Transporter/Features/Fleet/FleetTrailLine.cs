using LanguageExt;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;

namespace Transporter.Features.Fleet;

/// <summary>Turns a bounded trail into visible segments and reports the map edits needed after a poll (B-034).</summary>
public static class FleetTrailLine
{
    /// <summary>Reads the joined pairs, leaving a break before every point that follows a gap.</summary>
    /// <param name="trail">The published points in oldest-first order.</param>
    /// <param name="measure">The source's range for shading.</param>
    /// <returns>The visible segments in trail order.</returns>
    public static IReadOnlyList<FleetTrailSegment> Segments(IReadOnlyList<TrailPoint> trail, FleetTrailMeasure measure)
    {
        var segments = new List<FleetTrailSegment>(Math.Max(0, trail.Count - 1));
        for (var index = 1; index < trail.Count; index++)
        {
            var later = trail[index];
            if (!later.FollowsGap)
            {
                segments.Add(new FleetTrailSegment(trail[index - 1], later, Shade(later.Measure, measure)));
            }
        }

        return segments;
    }

    /// <summary>Compares two published trails by shared point identity, preserving the map's common segments.</summary>
    /// <param name="previous">The last published trail.</param>
    /// <param name="current">The new published trail.</param>
    /// <param name="measure">The source's range for shading.</param>
    /// <returns>Only the tail removals and head additions, or a new selection's redraw.</returns>
    public static FleetTrailChange Follow(
        IReadOnlyList<TrailPoint> previous,
        IReadOnlyList<TrailPoint> current,
        FleetTrailMeasure measure)
    {
        var before = Segments(previous, measure);
        var after = Segments(current, measure);
        if (!previous.Any(old => current.Any(now => ReferenceEquals(old, now))))
        {
            return new FleetTrailChange(true, before.Count, after);
        }

        for (var oldIndex = 0; oldIndex < before.Count; oldIndex++)
        {
            for (var newIndex = 0; newIndex < after.Count; newIndex++)
            {
                if (!SamePair(before[oldIndex], after[newIndex]))
                {
                    continue;
                }

                var common = 0;
                while (oldIndex + common < before.Count &&
                       newIndex + common < after.Count &&
                       SamePair(before[oldIndex + common], after[newIndex + common]))
                {
                    common++;
                }

                if (oldIndex + common == before.Count && newIndex + common <= after.Count)
                {
                    return new FleetTrailChange(false, oldIndex, [.. after.Skip(newIndex + common)]);
                }
            }
        }

        // A one-point trail or a gap can have no shared line even when its points overlap.
        return new FleetTrailChange(false, before.Count, after);
    }

    /// <summary>Places a reported measure in the source's range, or leaves an absent measure uncoloured.</summary>
    /// <param name="value">The point's reported measure.</param>
    /// <param name="measure">The source's range.</param>
    /// <returns>A clamped fraction, or none when the point has no measure.</returns>
    public static Option<double> Shade(Option<double> value, FleetTrailMeasure measure) =>
        value.Match(
            some => Option<double>.Some(measure.Maximum <= measure.Minimum
                ? 0
                : Math.Clamp((some - measure.Minimum) / (measure.Maximum - measure.Minimum), 0, 1)),
            static () => Option<double>.None);

    private static bool SamePair(FleetTrailSegment first, FleetTrailSegment second) =>
        ReferenceEquals(first.From, second.From) && ReferenceEquals(first.To, second.To) && first.Shade == second.Shade;
}

/// <summary>One visible edge; the later point decides its colour.</summary>
public sealed record FleetTrailSegment(TrailPoint From, TrailPoint To, Option<double> Shade);

/// <summary>Remove leading edges, then append these edges; redraw only when no point is shared.</summary>
public sealed record FleetTrailChange(bool Redraw, int Removed, IReadOnlyList<FleetTrailSegment> Added);

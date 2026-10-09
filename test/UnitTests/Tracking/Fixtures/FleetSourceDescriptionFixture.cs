using System.Collections.Generic;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Tracking.Fleet;

namespace Transponder.UnitTests.Tracking.Fixtures;

/// <summary>
/// Builds a <see cref="FleetSourceDescription"/> — what a live source offers a view, which is the
/// value a swap replaces.
/// </summary>
/// <remarks>
/// Hand-written for the reason the other fixtures here are, and its defaults are the shape every
/// consumer test needs: two columns whose cells differ, one grouping, and no filter choices, so a
/// test that is about filters is the one that says so. No detail lines either, for the same reason.
/// </remarks>
internal sealed class FleetSourceDescriptionFixture : AutoFixtureBase<FleetSourceDescriptionFixture>
{
    /// <summary>Sets the columns, in the order a grid shows them.</summary>
    /// <param name="columns">The columns.</param>
    /// <returns>The fixture, so building chains.</returns>
    public FleetSourceDescriptionFixture WithColumns(IReadOnlyList<FleetColumn> columns) => With(ref _columns, columns);

    /// <summary>Sets what the source can be grouped by.</summary>
    /// <param name="groupings">The groupings.</param>
    /// <returns>The fixture, so building chains.</returns>
    public FleetSourceDescriptionFixture WithGroupings(IReadOnlyList<FleetGrouping> groupings) => With(ref _groupings, groupings);

    /// <summary>Sets the filter choices the source offers (B-029).</summary>
    /// <param name="filters">The choices.</param>
    /// <returns>The fixture, so building chains.</returns>
    public FleetSourceDescriptionFixture WithFilters(IReadOnlyList<FleetFilterChoice> filters) => With(ref _filters, filters);

    /// <summary>Sets the detail pane's lines (B-043).</summary>
    /// <param name="detail">The lines, in reading order.</param>
    /// <returns>The fixture, so building chains.</returns>
    public FleetSourceDescriptionFixture WithDetail(IReadOnlyList<FleetColumn> detail) => With(ref _detail, detail);

    /// <summary>Takes the subject, as every fixture here is taken.</summary>
    /// <param name="fixture">The fixture to build.</param>
    public static implicit operator FleetSourceDescription(FleetSourceDescriptionFixture fixture) => fixture.Build();

    private FleetSourceDescription Build() =>
        new()
        {
            Columns = _columns,
            Groupings = _groupings,
            Filters = _filters,
            Detail = _detail,
        };

    private IReadOnlyList<FleetColumn> _columns =
    [
        new FleetColumn { Name = "Callsign", Value = static vehicle => vehicle.Label },
        new FleetColumn { Name = "Origin country", Value = static vehicle => vehicle.GroupKey },
    ];

    private IReadOnlyList<FleetGrouping> _groupings =
    [
        new FleetGrouping { Name = "Origin country", Key = static vehicle => vehicle.GroupKey },
    ];

    private IReadOnlyList<FleetFilterChoice> _filters = [];

    private IReadOnlyList<FleetColumn> _detail = [];
}

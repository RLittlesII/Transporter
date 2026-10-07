using System;
using System.Collections.Generic;
using AwesomeAssertions;
using LanguageExt;
using Transponder.Features.Fleet;
using Transponder.Model;
using Transponder.Tracking.Fleet;

namespace Transponder.UnitTests.Features.Fleet;

public class FleetSearchTests
{
    /// <summary>
    /// B-010. The predicate is a function of the text and the description's columns, so it is tested
    /// by calling it. Three clauses, and each has its own way of failing quietly: a case-sensitive
    /// comparison looks correct until someone types in lowercase, an untrimmed one until a paste
    /// brings a space, and empty text matching nothing shows an empty grid the moment the box is
    /// cleared — the gesture the audience is most likely to make.
    /// </summary>
    /// <param name="text">What the user typed.</param>
    /// <param name="expected">Whether the aircraft survives it.</param>
    [Theory]
    [InlineData("flt0421", true)]
    [InlineData("FLT0421", true)]
    [InlineData("  FLT04  ", true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("germany", true)]
    [InlineData("QFA", false)]
    public void GivenSearchText_WhenItIsMatched_ThenMatchingIsCaseInsensitiveTrimmedAndEmptyMatchesEverything(string text, bool expected)
    {
        // Given
        TransportVehicle vehicle = new Aircraft("a1b2c3", LastContact) { Callsign = "FLT0421", OriginCountry = "Germany" };

        // When
        var matches = FleetSearch.Matching(text, Columns)(vehicle);

        // Then
        matches.Should().Be(expected, "the label and every column's cell are what search reads, case-insensitively and trimmed");
    }

    /// <summary>
    /// B-011. The two inputs are independent predicates joined by conjunction, which is the whole
    /// claim: clearing one leaves the other standing. A dropdown that silently resets the search box
    /// reads as a bug to the person watching, and the implementation that causes it — one input
    /// overwriting the predicate the other set — passes every test that only ever sets one.
    /// </summary>
    [Fact]
    public void GivenASearchAndAFilter_WhenEitherIsCleared_ThenTheOtherStillApplies()
    {
        // Given
        var onTheGround = new FleetFilterChoice { Name = "On the ground", Matches = static vehicle => vehicle is Aircraft { OnGround: true } };
        TransportVehicle matchesBoth = new Aircraft("a1b2c3", LastContact) { Callsign = "FLT0421", OriginCountry = "Germany", OnGround = true };
        TransportVehicle searchOnly = new Aircraft("d4e5f6", LastContact) { Callsign = "FLT0422", OriginCountry = "Germany", OnGround = false };
        TransportVehicle filterOnly = new Aircraft("f7a8b9", LastContact) { Callsign = "QFA12", OriginCountry = "Australia", OnGround = true };

        // When
        var both = FleetSearch.Composed("FLT04", Columns, onTheGround);
        var searchCleared = FleetSearch.Composed(string.Empty, Columns, onTheGround);
        var filterCleared = FleetSearch.Composed("FLT04", Columns, Option<FleetFilterChoice>.None);

        // Then
        both(matchesBoth).Should().BeTrue("the vehicle satisfies the search and the choice");
        both(searchOnly).Should().BeFalse("the choice still applies when the search matches");
        both(filterOnly).Should().BeFalse("the search still applies when the choice matches");
        searchCleared(filterOnly).Should().BeTrue("clearing the search leaves the choice applied");
        searchCleared(searchOnly).Should().BeFalse("the choice did not go with the search");
        filterCleared(searchOnly).Should().BeTrue("clearing the choice leaves the search applied");
        filterCleared(filterOnly).Should().BeFalse("the search did not go with the choice");
    }

    /// <summary>The columns a description publishes here: the label, the group key, and one that is neither.</summary>
    private static readonly IReadOnlyList<FleetColumn> Columns =
    [
        new FleetColumn { Name = "Callsign", Value = static vehicle => vehicle.Label },
        new FleetColumn { Name = "Origin country", Value = static vehicle => vehicle.GroupKey },
        new FleetColumn { Name = "Key", Value = static vehicle => vehicle.Key },
    ];

    /// <summary>The instant the vehicles here were last heard from.</summary>
    private static readonly DateTimeOffset LastContact = new(2026, 10, 7, 9, 15, 0, TimeSpan.Zero);
}

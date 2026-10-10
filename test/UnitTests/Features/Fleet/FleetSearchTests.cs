using AwesomeAssertions;
using LanguageExt;
using Transporter.Features.Fleet;
using Transporter.Model;
using Transporter.Tracking.Fleet;
using Transporter.UnitTests.Model.Fixtures;
using Transporter.UnitTests.Tracking.Fixtures;

namespace Transporter.UnitTests.Features.Fleet;

public class FleetSearchTests
{
    /// <summary>
    /// B-010. The predicate is a function of the text and the description's columns, so it is tested
    /// by calling it. Which clause each case covers, and how each fails quietly, is written in
    /// <see cref="SearchTextCases"/> beside the cases themselves.
    /// </summary>
    /// <param name="text">What the user typed.</param>
    /// <param name="expected">Whether the aircraft survives it.</param>
    [Theory]
    [ClassData(typeof(SearchTextCases))]
    public void GivenSearchText_WhenItIsMatched_ThenMatchingIsCaseInsensitiveTrimmedAndEmptyMatchesEverything(string text, bool expected)
    {
        // Given
        FleetSourceDescription description = new FleetSourceDescriptionFixture();
        TransportVehicle vehicle = new AircraftFixture().WithCallsign("FLT0421").WithOriginCountry("Germany");

        // When
        var matches = FleetSearch.Matching(text, description.Columns)(vehicle);

        // Then
        matches.Should().Be(expected, "the label and every column's cell are what search reads, case-insensitively and trimmed");
    }

    /// <summary>
    /// B-011. The two inputs are independent predicates joined by conjunction, which is the whole
    /// claim: clearing one leaves the other standing. The three vehicles and what each case proves
    /// are written in <see cref="ComposedPredicateCases"/>.
    /// </summary>
    /// <param name="text">What the user typed.</param>
    /// <param name="chosen">Whether the on-the-ground choice is taken.</param>
    /// <param name="vehicle">Which of the three vehicles is offered to the predicate.</param>
    /// <param name="expected">Whether it survives.</param>
    [Theory]
    [ClassData(typeof(ComposedPredicateCases))]
    public void GivenASearchAndAFilter_WhenEitherIsCleared_ThenTheOtherStillApplies(string text, bool chosen, string vehicle, bool expected)
    {
        // Given
        var onTheGround = new FleetFilterChoice { Name = "On the ground", Matches = static candidate => candidate is Aircraft { OnGround: true } };
        FleetSourceDescription description = new FleetSourceDescriptionFixture().WithFilters([onTheGround]);
        var choice = chosen ? onTheGround : Option<FleetFilterChoice>.None;

        // When
        var composed = FleetSearch.Composed(text, description.Columns, choice);

        // Then
        composed(Offered(vehicle)).Should().Be(expected, "the search and the choice are independent predicates joined by conjunction");
    }

    /// <summary>One of the three vehicles the cases name.</summary>
    /// <param name="which">Which one the case asked for.</param>
    /// <returns>The vehicle.</returns>
    private static TransportVehicle Offered(string which) =>
        which switch
        {
            "both" => new AircraftFixture().WithKey("a1b2c3").WithCallsign("FLT0421").WithOnGround(true),
            "search" => new AircraftFixture().WithKey("d4e5f6").WithCallsign("FLT0422").WithOnGround(false),
            _ => new AircraftFixture().WithKey("f7a8b9").WithCallsign("QFA12").WithOnGround(true),
        };
}

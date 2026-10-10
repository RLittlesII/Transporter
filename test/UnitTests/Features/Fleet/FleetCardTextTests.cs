using System;
using AwesomeAssertions;
using LanguageExt;
using Transporter.Features.Fleet;

namespace Transporter.UnitTests.Features.Fleet;

public class FleetCardTextTests
{
    /// <summary>
    /// B-030. The pipeline carries metres and the card reads kilometres to a tenth. The hazard is a
    /// distance the pipeline left absent — a vehicle just added, or one with no position — shown as
    /// "0.0 km", which says the aircraft stood still when nothing was measured at all.
    /// </summary>
    /// <param name="metres">The distance on the element, negative for absent.</param>
    /// <param name="expected">What the card reads.</param>
    [Theory]
    [InlineData(12_449.0, "12.4 km")]
    [InlineData(1_234_567.0, "1,234.6 km")]
    [InlineData(0.0, "0.0 km")]
    [InlineData(-1.0, "\u2014")]
    public void GivenADistanceInMetres_WhenItIsFormatted_ThenItReadsInKilometresOrAsMissing(double metres, string expected)
    {
        // Given
        var distance = metres < 0 ? Option<double>.None : Option<double>.Some(metres);

        // When
        var text = FleetCardText.Distance(distance);

        // Then
        text.Should().Be(expected);
    }

    /// <summary>
    /// B-031. Age is the observed instant minus the last contact. The hazards are the two ways it goes
    /// wrong in front of an audience: a replay measured against the device clock, which this cannot
    /// do because it is handed both instants, and the page before the first poll, when there is no
    /// observed instant and every card would otherwise read as two thousand years old.
    /// </summary>
    /// <param name="seconds">Seconds between the last contact and the observed instant.</param>
    /// <param name="expected">What the card reads.</param>
    [Theory]
    [InlineData(42, "42s")]
    [InlineData(252, "4m 12s")]
    [InlineData(3_780, "1h 03m")]
    [InlineData(-5, "0s")]
    public void GivenAnObservedInstant_WhenTheAgeIsFormatted_ThenItReadsTheGapSinceLastContact(int seconds, string expected)
    {
        // Given
        var observed = new DateTimeOffset(2026, 10, 8, 14, 32, 10, TimeSpan.Zero);

        // When
        var text = FleetCardText.Age(observed, observed.AddSeconds(-seconds));

        // Then
        text.Should().Be(expected);
    }

    /// <summary>B-031. Before any poll has landed there is no instant to measure from, so no age is shown.</summary>
    [Fact]
    public void GivenNoObservedInstant_WhenTheAgeIsFormatted_ThenItReadsAsMissing()
    {
        // Given
        var lastContact = new DateTimeOffset(2026, 10, 8, 14, 32, 10, TimeSpan.Zero);

        // When
        var text = FleetCardText.Age(DateTimeOffset.MinValue, lastContact);

        // Then
        text.Should().Be(FleetCardText.Missing);
    }
}

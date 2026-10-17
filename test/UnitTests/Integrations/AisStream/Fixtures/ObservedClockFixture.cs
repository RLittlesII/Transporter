using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Tracking;

namespace Transporter.UnitTests.Integrations.AisStream.Fixtures;

/// <summary>Builds the application's real observed clock.</summary>
/// <remarks>The generator has no declared constructor to inspect on this type.</remarks>
internal sealed class ObservedClockFixture : AutoFixtureBase<ObservedClockFixture>
{
    public static implicit operator ObservedClock(ObservedClockFixture fixture) => new();
}

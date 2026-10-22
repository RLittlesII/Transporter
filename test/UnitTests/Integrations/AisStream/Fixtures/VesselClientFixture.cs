using DynamicData;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.AisStream;
using Transporter.Integrations.AisStream.Configuration;
using Transporter.Integrations.OpenSky;
using Transporter.Recording;
using Transporter.Scheduling;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Integrations.AisStream.Fixtures;

/// <summary>Builds the vessel client with synthetic configuration and a controlled clock.</summary>
[AutoFixture(typeof(VesselClient))]
internal partial class VesselClientFixture
{
    public VesselClientFixture()
    {
        WithSockets(static () => throw new InvalidOperationException("No socket was scripted."));
        WithVessels(new SourceCache<VesselSnapshot, string>(static vessel => vessel.Mmsi));
        WithWriter((Transporter.Tracking.ObservedClock) new ObservedClockFixture());
        WithOptions(Options.Create(new AisStreamOptions { ApiKey = "synthetic-credential" }));
        WithOptions(Options.Create(new OpenSkyOptions { Box = new BoundingBoxFixture() }));
        WithProvider((SchedulerProvider) new SchedulerProviderFixture().WithTestScheduler(new TestScheduler()));
        WithWriter((IRecordingWriter) new UnrecordedPayloads());
    }
}

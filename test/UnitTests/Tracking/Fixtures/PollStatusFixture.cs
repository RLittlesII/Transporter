using LanguageExt;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Tracking;

namespace Transporter.UnitTests.Tracking.Fixtures;

/// <summary>Builds one complete synthetic report, with none as the unconfigured default.</summary>
internal sealed class PollStatusFixture : AutoFixtureBase<PollStatusFixture>
{
    public PollStatusFixture WithPolledAt(Option<DateTimeOffset> value) => With(ref _polledAt, value);

    public PollStatusFixture WithNextDue(Option<DateTimeOffset> value) => With(ref _nextDue, value);

    public PollStatusFixture WithRefusedFor(Option<TimeSpan> value) => With(ref _refusedFor, value);

    public static implicit operator PollStatus(PollStatusFixture fixture) => new()
    {
        PolledAt = fixture._polledAt,
        NextDue = fixture._nextDue,
        RefusedFor = fixture._refusedFor,
    };

    private Option<DateTimeOffset> _polledAt;
    private Option<DateTimeOffset> _nextDue;
    private Option<TimeSpan> _refusedFor;
}

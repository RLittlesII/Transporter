using System.Reactive.Linq;
using AwesomeAssertions;
using DynamicData;
using LanguageExt;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Integrations.OpenSky;
using Transporter.Integrations.OpenSky.Contracts;
using Transporter.Integrations.OpenSky.Model;
using Transporter.Scheduling;
using Transporter.Tracking;
using Transporter.UnitTests.Scheduling;

namespace Transporter.UnitTests.Integrations.OpenSky;

public class AircraftSnapshotClientTests
{
    /// <summary>
    /// B-003. The write side of the clock is the only source of an instant this client has — it is
    /// handed that and nothing else — so a client reading an ambient clock could not make this pass.
    /// Staleness downstream is measured against what the provider said, which is what lets a
    /// recording age its fleet the way the live feed does.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAResponseReportingAnInstant_WhenTheSetIsApplied_ThenThatInstantIsTheObservedOne()
    {
        // Given
        var clock = new ObservedClock();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(Answering(OpenSkyPayloads.ThreeRows))
            .WithWriter(clock);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        ((IObservedClock) clock).Current.Should().Be(OpenSkyPayloads.ReportedInstant);
    }

    /// <summary>
    /// B-015. The contract handed in is the one called and the items land in the very cache instance
    /// the test holds — neither of which a client constructing its own collaborators could do.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenTheClient_WhenItIsConstructed_ThenItTakesContractAndCacheAndConstructsNeither()
    {
        // Given
        var api = Answering(OpenSkyPayloads.ThreeRows);
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(api).WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        await api.Received(1).GetStates(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        cache.Items.Should().HaveCount(3);
    }

    /// <summary>
    /// B-016. Every member against the index <c>README.md</c> § "Response shape" reads it from, so a
    /// reader that shifted by one — the failure index 12 invites — fails here rather than putting an
    /// aircraft at a plausible wrong altitude.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAnEighteenElementRow_WhenItIsRead_ThenEveryMemberComesFromItsOwnIndex()
    {
        // Given
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(Answering(OpenSkyPayloads.ThreeRows))
            .WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        var snapshot = cache.Lookup("a1b2c3").Value;
        snapshot.Icao24.Should().Be("a1b2c3");
        Some(snapshot.Callsign, "TRN0001");
        snapshot.OriginCountry.Should().Be("Testland");
        Some(snapshot.TimePosition, 1791124315L);
        snapshot.LastContact.Should().Be(1791124320L);
        Some(snapshot.Longitude, -95.3698);
        Some(snapshot.Latitude, 29.7604);
        Some(snapshot.BarometricAltitude, 1234.5);
        snapshot.OnGround.Should().BeFalse();
        Some(snapshot.Velocity, 128.6);
        Some(snapshot.TrueTrack, 91.2);
        Some(snapshot.VerticalRate, -1.3);
        Some(snapshot.GeometricAltitude, 1250.0);
        Some(snapshot.Squawk, "0021");
        snapshot.Spi.Should().BeFalse();
        snapshot.PositionSource.Should().Be(0);
        Some(snapshot.Category, 1);
    }

    /// <summary>
    /// B-017. The second row of <see cref="OpenSkyPayloads.ThreeRows"/> reports null velocity, true
    /// track and vertical rate. Absent is not 0: an aircraft reported at 0 m/s is stationary, which
    /// is a different answer and a plausible one.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenANullElement_WhenTheRowIsRead_ThenTheValueIsAbsentRatherThanADefault()
    {
        // Given
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(Answering(OpenSkyPayloads.ThreeRows))
            .WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        var snapshot = cache.Lookup("d4e5f6").Value;
        None(snapshot.Velocity);
        None(snapshot.TrueTrack);
        None(snapshot.VerticalRate);
        NotSome(snapshot.Velocity, 0d);
        NotSome(snapshot.TrueTrack, 0d);
        NotSome(snapshot.VerticalRate, 0d);
    }

    /// <summary>
    /// B-018. Presence is decided by element count and element count alone — never by whether
    /// <c>extended=1</c> was asked for, because the two can disagree. A converter that collapsed
    /// absent and present-with-zero would pass every other test in this class.
    /// </summary>
    /// <param name="payload">The row, carrying index 17 or stopping before it.</param>
    /// <param name="category">The category it reads as, or <see langword="null"/> for absent.</param>
    /// <returns>The running test.</returns>
    [Theory]
    [ClassData(typeof(CategoryCases))]
    public async Task GivenASeventeenElementRowAndAnEighteenElementRowEndingInZero_WhenBothAreRead_ThenTheirCategoriesDiffer(
        OpenSkyPayload payload,
        int? category)
    {
        // Given
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(Answering(payload)).WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        var read = cache.Lookup("a1b2c3").Value.Category;

        if (category is null)
        {
            None(read);
        }
        else
        {
            Some(read, category.Value);
        }
    }

    /// <summary>
    /// B-019. The wire pads a callsign to eight characters, so padding alone is no callsign: it is
    /// absent, never an empty string and never whitespace a view would render as a blank cell.
    /// </summary>
    /// <param name="payload">The row, carrying index 1.</param>
    /// <param name="callsign">The callsign it reads as, or <see langword="null"/> for absent.</param>
    /// <returns>The running test.</returns>
    [Theory]
    [ClassData(typeof(CallsignCases))]
    public async Task GivenAPaddedCallsign_WhenTheRowIsRead_ThenPaddingIsRemovedAndPaddingAloneIsAbsent(
        OpenSkyPayload payload,
        string? callsign)
    {
        // Given
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(Answering(payload)).WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        var read = cache.Lookup("a1b2c3").Value.Callsign;

        if (callsign is null)
        {
            None(read);
            NotSome(read, string.Empty);
        }
        else
        {
            Some(read, callsign);
        }
    }

    /// <summary>
    /// B-020. A squawk is a code rather than a number, so it is read with <c>GetString()</c>: four
    /// characters, leading zeros intact, and <c>"0021"</c> never the number 21.
    /// </summary>
    /// <param name="payload">The row, carrying index 14.</param>
    /// <param name="squawk">The squawk it reads as, or <see langword="null"/> for absent.</param>
    /// <returns>The running test.</returns>
    [Theory]
    [ClassData(typeof(SquawkCases))]
    public async Task GivenASquawkOfZeroZeroTwoOne_WhenTheRowIsRead_ThenItIsFourCharactersAndNotTwentyOne(
        OpenSkyPayload payload,
        string? squawk)
    {
        // Given
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(Answering(payload)).WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        var read = cache.Lookup("a1b2c3").Value.Squawk;

        if (squawk is null)
        {
            None(read);
        }
        else
        {
            Some(read, squawk);
            read.IfSome(static value => value.Length.Should().Be(4));
        }
    }

    /// <summary>
    /// B-021. Index 12 carries a value here, which is the case a reader that forgot it shifts on.
    /// Nothing carries <c>sensors</c>, and the members either side of it still hold their own
    /// values — which is what separates an exclusion stated in the reader from a gap nobody noticed.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenARowWithSensors_WhenItIsRead_ThenIndexTwelveReachesNoSnapshotOrVehicleMember()
    {
        // Given
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(Answering(OpenSkyPayloads.Row("1", sensors: "[1, 2, 3]")))
            .WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        var snapshot = cache.Lookup("a1b2c3").Value;
        Some(snapshot.VerticalRate, -1.3);
        Some(snapshot.GeometricAltitude, 1250.0);
        Some(snapshot.Squawk, "0021");
    }

    /// <summary>
    /// B-022. A malformed row is data about the provider, not a reason to lose the rows beside it:
    /// it is excluded and counted, and one bad row does not cost a poll its other three.
    /// </summary>
    /// <param name="payload">Four rows, one of them defective.</param>
    /// <returns>The running test.</returns>
    [Theory]
    [ClassData(typeof(UnreadableRowCases))]
    public async Task GivenOneUnreadableRowAmongSeveral_WhenTheSetIsRead_ThenItIsExcludedAndCountedAndTheRestSurvive(
        OpenSkyPayload payload)
    {
        // Given
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(Answering(payload)).WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        cache.Items.Should().HaveCount(3);
        sut.UnreadableRows.Should().Be(1);
        cache.Lookup("a1b2c3").HasValue.Should().BeTrue();
        cache.Lookup("d4e5f6").HasValue.Should().BeTrue();
        cache.Lookup("070809").HasValue.Should().BeTrue();
    }

    /// <summary>
    /// B-023, and the claim the demo's core idea rests on. A cache already holding three aircraft
    /// takes a second fetch in which one is unchanged, one changed, one gone and one new, and what
    /// reaches a subscriber is one changeset of exactly three changes. The unchanged aircraft is not
    /// in it, which is the whole point of a differential write over the whole set.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAFetchedSet_WhenItIsApplied_ThenTheCacheTakesOneDifferentialUpdateOverTheWholeSet()
    {
        // Given
        var cache = Cache();
        var polls = 0;
        var api = Substitute.For<IOpenSkyApi>();
        api.GetStates(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult((++polls == 1 ? OpenSkyPayloads.ThreeRows : OpenSkyPayloads.SecondPoll).Response));
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(api).WithCache(cache);
        await sut.Fetch(CancellationToken.None);

        var changes = new List<IChangeSet<AircraftSnapshot, string>>();
        using var subscription = sut.Snapshots.Skip(1).Subscribe(changes.Add);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        changes.Should().ContainSingle();
        var changeset = changes.Single();
        changeset.Should().HaveCount(3);
        changeset.Should().ContainSingle(change => change.Key == "b1c2d3" && change.Reason == ChangeReason.Add);
        changeset.Should().ContainSingle(change => change.Key == "d4e5f6" && change.Reason == ChangeReason.Update);
        changeset.Should().ContainSingle(change => change.Key == "070809" && change.Reason == ChangeReason.Remove);
        changeset.Should().NotContain(change => change.Key == "a1b2c3");
    }

    /// <summary>
    /// B-024. The box and the interval are options input, and the contract takes four loose
    /// coordinates — which is what keeps a bounding box off it (B-006). Thirty seconds rather than
    /// the fifteen-second default, so the assertion fails if the client polls on a cadence of its
    /// own rather than the one it was given.
    /// </summary>
    [Fact]
    public void GivenABoxAndAnInterval_WhenTheClientIsBuilt_ThenBothArriveAsInputAndNeitherIsOnTheContract()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var api = Answering(OpenSkyPayloads.ThreeRows);
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithProvider(schedulers)
            .WithOptions(Options.Create(new OpenSkyOptions { Box = Houston, PollInterval = TimeSpan.FromSeconds(30) }));

        // When
        using var polling = sut.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(29).Ticks);
        var beforeTheInterval = api.ReceivedCalls().Count();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

        // Then
        beforeTheInterval.Should().Be(1, "the first poll is immediate and the second waits out the interval");
        api.ReceivedCalls().Should().HaveCount(2);
        api.Received().GetStates(
            Houston.LatitudeMinimum,
            Houston.LongitudeMinimum,
            Houston.LatitudeMaximum,
            Houston.LongitudeMaximum,
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// B-025. The flag reaches the contract, and a request without it yields snapshots whose
    /// category is absent rather than defaulted — the two halves of the claim, which only hold
    /// together because B-018 decides presence by element count.
    /// </summary>
    /// <param name="offered">Whether the application offers grouping by category.</param>
    /// <param name="payload">What the provider answers with when asked that way.</param>
    /// <param name="category">The category the snapshot carries, or <see langword="null"/>.</param>
    /// <returns>The running test.</returns>
    [Theory]
    [ClassData(typeof(CategoryGroupingCases))]
    public async Task GivenCategoryGroupingIsOffered_WhenThePollIsSent_ThenTheRequestAsksForExtendedRows(
        bool offered,
        OpenSkyPayload payload,
        int? category)
    {
        // Given
        var api = Answering(payload);
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithCache(cache)
            .WithOptions(Options.Create(new OpenSkyOptions { Box = Houston, RequestsCategory = offered }));

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        await api.Received(1).GetStates(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            offered,
            Arg.Any<CancellationToken>());
        var read = cache.Lookup("a1b2c3").Value.Category;

        if (category is null)
        {
            None(read);
        }
        else
        {
            Some(read, category.Value);
        }
    }

    /// <summary>
    /// B-029, second half. One poll times out and the next succeeds: the subscriber receives the
    /// following poll's changes and nothing ended the stream in between, because a demo that loses
    /// its collection to one bad response ends on a blank grid.
    /// </summary>
    [Fact]
    public void GivenATimedOutPoll_WhenItFails_ThenTheStreamNeitherCompletesNorErrors()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var polls = 0;
        var api = Substitute.For<IOpenSkyApi>();
        api.GetStates(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(
                _ => ++polls == 1
                    ? throw new TaskCanceledException("The request timed out.")
                    : Task.FromResult(OpenSkyPayloads.ThreeRows.Response));
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithProvider(schedulers)
            .WithOptions(Options.Create(new OpenSkyOptions { Box = Houston, PollInterval = TimeSpan.FromSeconds(1) }));

        var changes = new List<IChangeSet<AircraftSnapshot, string>>();
        var completed = false;
        Exception? errored = null;
        using var subscription = sut.Snapshots.Subscribe(changes.Add, failure => errored = failure, () => completed = true);

        // When
        using var polling = sut.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(3).Ticks);

        // Then
        errored.Should().BeNull("a failed poll is not a failed stream");
        completed.Should().BeFalse();
        changes.SelectMany(static changeset => changeset)
            .Select(static change => change.Key)
            .Should()
            .Contain(["a1b2c3", "d4e5f6", "070809"]);
    }

    /// <summary>
    /// B-053's guarantee, from the cadence's side: the loop waits out the interval from the last
    /// poll the source made, whoever made it. A loop keeping a clock of its own would poll at
    /// thirty seconds as well as at the demanded twenty, which is two credits inside one interval
    /// and the thing demand is not allowed to buy.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAPollDemandedBetweenTwoScheduledOnes_WhenTheCadenceRuns_ThenItWaitsOutTheIntervalFromTheDemandedPoll()
    {
        // Given
        var scheduler = new TestScheduler();
        SchedulerProvider schedulers = new SchedulerProviderFixture().WithTestScheduler(scheduler);
        var api = Substitute.For<IOpenSkyApi>();
        api.GetStates(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(static _ => Task.FromResult(OpenSkyPayloads.ThreeRows.Response));
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithProvider(schedulers)
            .WithOptions(Options.Create(new OpenSkyOptions { Box = Houston, PollInterval = TimeSpan.FromSeconds(30) }));

        // When
        using var polling = sut.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(20).Ticks);
        await sut.PollNow(CancellationToken.None);
        var afterTheDemandedPoll = api.ReceivedCalls().Count();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(10).Ticks);
        var whereTheLoopsOwnIntervalFell = api.ReceivedCalls().Count();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(20).Ticks);

        // Then
        afterTheDemandedPoll.Should().Be(2, "the first poll is immediate and the demanded one is the second");
        whereTheLoopsOwnIntervalFell.Should().Be(2, "thirty seconds after the first poll is ten after the demanded one");
        api.ReceivedCalls().Should().HaveCount(3, "the cadence polls one interval after the demanded poll");
    }

    /// <summary>Asserts an optional value is present, and what it holds.</summary>
    /// <typeparam name="T">What the option holds.</typeparam>
    /// <param name="actual">The option read from a snapshot.</param>
    /// <param name="expected">What it should hold.</param>
    /// <remarks>
    /// Asserted through <see cref="object"/> because <c>Option&lt;T&gt;</c> is both an
    /// <see cref="IEnumerable{T}"/> and an <see cref="IComparable{T}"/>, which makes
    /// <c>Should()</c> ambiguous on it. The equality is the option's own.
    /// </remarks>
    private static void Some<T>(Option<T> actual, T expected) =>
        ((object) actual).Should().Be(Option<T>.Some(expected));

    /// <summary>Asserts an optional value is absent.</summary>
    /// <typeparam name="T">What the option would have held.</typeparam>
    /// <param name="actual">The option read from a snapshot.</param>
    private static void None<T>(Option<T> actual) => ((object) actual).Should().Be(Option<T>.None);

    /// <summary>Asserts an optional value is not one particular present value.</summary>
    /// <typeparam name="T">What the option holds.</typeparam>
    /// <param name="actual">The option read from a snapshot.</param>
    /// <param name="unwanted">The value a defaulting reader would have produced.</param>
    private static void NotSome<T>(Option<T> actual, T unwanted) =>
        ((object) actual).Should().NotBe(Option<T>.Some(unwanted));

    /// <summary>A cache keyed the way the registration keys it.</summary>
    /// <returns>The cache a client writes into.</returns>
    private static SourceCache<AircraftSnapshot, string> Cache() => new(static snapshot => snapshot.Icao24);

    /// <summary>A contract answering every poll with one payload.</summary>
    /// <param name="payload">What the provider sent.</param>
    /// <returns>The substituted contract.</returns>
    private static IOpenSkyApi Answering(OpenSkyPayload payload)
    {
        var api = Substitute.For<IOpenSkyApi>();

        api.GetStates(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(payload.Response));

        return api;
    }

    /// <summary>The box decisions/0001 chose, so a test asserting the request asserts a real box.</summary>
    private static readonly BoundingBox Houston = new()
    {
        LatitudeMinimum = 28.8,
        LongitudeMinimum = -96.0,
        LatitudeMaximum = 30.4,
        LongitudeMaximum = -94.2,
    };
}

/// <summary>Builds the client, so a constructor change edits this and not every test.</summary>
[AutoFixture(typeof(AircraftSnapshotClient))]
internal partial class AircraftSnapshotClientFixture
{
    public AircraftSnapshotClientFixture()
    {
        WithCache(new SourceCache<AircraftSnapshot, string>(static snapshot => snapshot.Icao24));
        WithWriter(new ObservedClock());
        WithLogger(new RecordingLogger<AircraftSnapshotClient>());
        WithProvider(DefaultSchedulers);
        WithOptions(
            Options.Create(
                new OpenSkyOptions
                {
                    Box = new BoundingBox
                    {
                        LatitudeMinimum = 28.8,
                        LongitudeMinimum = -96.0,
                        LatitudeMaximum = 30.4,
                        LongitudeMaximum = -94.2,
                    },
                }));
    }

    /// <summary>The schedulers a test that never advances time still has to be given.</summary>
    private static SchedulerProvider DefaultSchedulers => new SchedulerProviderFixture();
}

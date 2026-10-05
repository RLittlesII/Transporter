using System.Reactive.Linq;
using System.Text.Json;
using AwesomeAssertions;
using DynamicData;
using LanguageExt;
using Microsoft.Extensions.Options;
using Microsoft.Reactive.Testing;
using NSubstitute;
using Rocket.Surgery.Airframe;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transponder.Integrations.OpenSky;
using Transponder.Integrations.OpenSky.Contracts;
using Transponder.Tracking;

namespace Transponder.UnitTests.Integrations.OpenSky;

public class AircraftSnapshotClientTests
{
    [Fact]
    public async Task GivenAResponseReportingAnInstant_WhenTheSetIsApplied_ThenThatInstantIsTheObservedOne()
    {
        // Given. The write side of the clock is the only source of an instant this client has, so a
        // client reading an ambient one could not make this pass.
        var clock = new ObservedClock();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(Answering(Payload(ThreeRows)))
            .WithWriter(clock);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        ((IObservedClock) clock).Current
            .Should()
            .Be(
                new DateTimeOffset(2026, 10, 4, 14, 32, 10, TimeSpan.Zero),
                "the envelope reported 1791124330, and staleness is measured against what the provider said");
    }

    [Fact]
    public async Task GivenTheClient_WhenItIsConstructed_ThenItTakesContractAndCacheAndConstructsNeither()
    {
        // Given
        var api = Answering(Payload(ThreeRows));
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(api).WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then. The contract handed in was the one called, and the items landed in the very cache
        // instance this test holds — neither of which a client constructing its own could do.
        await api.Received(1).GetStates(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        cache.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task GivenAnEighteenElementRow_WhenItIsRead_ThenEveryMemberComesFromItsOwnIndex()
    {
        // Given
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(Answering(Payload(ThreeRows))).WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then. Every member against the index README.md § "Response shape" reads it from.
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

    [Fact]
    public async Task GivenANullElement_WhenTheRowIsRead_ThenTheValueIsAbsentRatherThanADefault()
    {
        // Given. The second row reports null velocity, true track and vertical rate.
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(Answering(Payload(ThreeRows))).WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then. Absent, not 0 — an aircraft reported at 0 m/s is stationary, which is a different
        // answer and a plausible one.
        var snapshot = cache.Lookup("d4e5f6").Value;
        None(snapshot.Velocity);
        None(snapshot.TrueTrack);
        None(snapshot.VerticalRate);
        NotSome(snapshot.Velocity, 0d);
        NotSome(snapshot.TrueTrack, 0d);
        NotSome(snapshot.VerticalRate, 0d);
    }

    [Theory]
    [InlineData(17, null, null)]
    [InlineData(18, "null", null)]
    [InlineData(18, "0", 0)]
    [InlineData(18, "3", 3)]
    public async Task GivenASeventeenElementRowAndAnEighteenElementRowEndingInZero_WhenBothAreRead_ThenTheirCategoriesDiffer(
        int elements,
        string? indexSeventeen,
        int? category)
    {
        // Given. Presence is decided by element count and element count alone — never by whether
        // extended=1 was asked for, because the two can disagree.
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(Answering(Row(elements == 18 ? indexSeventeen : null)))
            .WithCache(cache);

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

    [Theory]
    [InlineData("\"FLT0421\"", "FLT0421")]
    [InlineData("\"FLT42   \"", "FLT42")]
    [InlineData("\"        \"", null)]
    [InlineData("null", null)]
    public async Task GivenAPaddedCallsign_WhenTheRowIsRead_ThenPaddingIsRemovedAndPaddingAloneIsAbsent(
        string wireValue,
        string? callsign)
    {
        // Given
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(Answering(Row("1", callsign: wireValue)))
            .WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then. Padding alone is absent — never an empty string and never whitespace.
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

    [Theory]
    [InlineData("\"0021\"", "0021")]
    [InlineData("\"7700\"", "7700")]
    [InlineData("\"0000\"", "0000")]
    [InlineData("null", null)]
    public async Task GivenASquawkOfZeroZeroTwoOne_WhenTheRowIsRead_ThenItIsFourCharactersAndNotTwentyOne(
        string wireValue,
        string? squawk)
    {
        // Given
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(Answering(Row("1", squawk: wireValue)))
            .WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then. A squawk is a code: four characters, leading zeros intact, never the number 21.
        var read = cache.Lookup("a1b2c3").Value.Squawk;

        if (squawk is null)
        {
            None(read);
        }
        else
        {
            Some(read, squawk);
            read.IfSome(static value => value.Length.Should().Be(4, "a squawk the provider sent as four characters stays four"));
        }
    }

    [Fact]
    public async Task GivenARowWithSensors_WhenItIsRead_ThenIndexTwelveReachesNoSnapshotOrVehicleMember()
    {
        // Given. Index 12 carries a value, which is the case a reader that forgot it shifts on.
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(Answering(Row("1", sensors: "[1, 2, 3]")))
            .WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then. Nothing carries it, and the members either side of index 12 still hold their own
        // values — a reader that skipped it by accident rather than by statement would shift them.
        var snapshot = cache.Lookup("a1b2c3").Value;
        Some(snapshot.VerticalRate, -1.3);
        Some(snapshot.GeometricAltitude, 1250.0);
        Some(snapshot.Squawk, "0021");
    }

    [Theory]
    [InlineData("twelve elements")]
    [InlineData("twenty-five elements")]
    [InlineData("index 0 is null")]
    [InlineData("index 8 is a string")]
    public async Task GivenOneUnreadableRowAmongSeveral_WhenTheSetIsRead_ThenItIsExcludedAndCountedAndTheRestSurvive(string defect)
    {
        // Given. Four rows, one of them defective in one of the ways B-022 names.
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(Answering(FourRowsOneDefective(defect))).WithCache(cache);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then
        cache.Items.Should().HaveCount(3, "the readable rows are unaffected by the one beside them");
        sut.UnreadableRows.Should().Be(1, "an excluded row is counted, not silently dropped");
        cache.Lookup("a1b2c3").HasValue.Should().BeTrue();
        cache.Lookup("d4e5f6").HasValue.Should().BeTrue();
        cache.Lookup("070809").HasValue.Should().BeTrue();
    }

    [Fact]
    public async Task GivenAFetchedSet_WhenItIsApplied_ThenTheCacheTakesOneDifferentialUpdateOverTheWholeSet()
    {
        // Given. A cache already holding three aircraft, and a second fetch in which one is
        // unchanged, one changed, one gone and one new.
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
            .Returns(_ => Task.FromResult(Deserialize(++polls == 1 ? Payload(ThreeRows) : SecondPoll)));
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture().WithApi(api).WithCache(cache);
        await sut.Fetch(CancellationToken.None);

        var changes = new List<IChangeSet<AircraftSnapshot, string>>();
        using var subscription = sut.Snapshots.Skip(1).Subscribe(changes.Add);

        // When
        await sut.Fetch(CancellationToken.None);

        // Then. One changeset carrying exactly three changes, and the unchanged aircraft is not in
        // it — which is the whole point of a differential write over the whole set.
        changes.Should().ContainSingle("the whole set is applied as one differential update");
        var changeset = changes.Single();
        changeset.Should().HaveCount(3);
        changeset.Should().ContainSingle(change => change.Key == "b1c2d3" && change.Reason == ChangeReason.Add);
        changeset.Should().ContainSingle(change => change.Key == "d4e5f6" && change.Reason == ChangeReason.Update);
        changeset.Should().ContainSingle(change => change.Key == "070809" && change.Reason == ChangeReason.Remove);
        changeset.Should().NotContain(change => change.Key == "a1b2c3");
    }

    [Fact]
    public void GivenABoxAndAnInterval_WhenTheClientIsBuilt_ThenBothArriveAsInputAndNeitherIsOnTheContract()
    {
        // Given. The box and the interval are options input; the contract takes four loose
        // coordinates, which is what keeps a bounding box off it (B-006, B-024).
        var scheduler = new TestScheduler();
        var api = Answering(Payload(ThreeRows));
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithProvider(Scheduling(scheduler))
            .WithOptions(Options.Create(new OpenSkyOptions { Box = Houston, PollInterval = TimeSpan.FromSeconds(30) }));

        // When
        using var polling = sut.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(29).Ticks);
        var beforeTheInterval = api.ReceivedCalls().Count();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(2).Ticks);

        // Then
        beforeTheInterval.Should().Be(1, "the first poll is immediate and the second waits out the interval");
        api.ReceivedCalls().Should().HaveCount(2, "thirty seconds on, the second poll is due");
        api.Received().GetStates(
            Houston.LatitudeMinimum,
            Houston.LongitudeMinimum,
            Houston.LatitudeMaximum,
            Houston.LongitudeMaximum,
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GivenCategoryGroupingIsOffered_WhenThePollIsSent_ThenTheRequestAsksForExtendedRows(bool offered)
    {
        // Given
        var api = Answering(Payload(offered ? ThreeRows : SeventeenElementRow));
        var cache = Cache();
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithCache(cache)
            .WithOptions(Options.Create(new OpenSkyOptions { Box = Houston, RequestsCategory = offered }));

        // When
        await sut.Fetch(CancellationToken.None);

        // Then. The flag reaches the contract, and a request without it yields snapshots whose
        // category is absent rather than defaulted.
        await api.Received(1).GetStates(
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            Arg.Any<double>(),
            offered,
            Arg.Any<CancellationToken>());
        var read = cache.Lookup("a1b2c3").Value.Category;

        if (offered)
        {
            Some(read, 1);
        }
        else
        {
            None(read);
        }
    }

    [Fact]
    public void GivenATimedOutPoll_WhenItFails_ThenTheStreamNeitherCompletesNorErrors()
    {
        // Given. The first poll times out and the next succeeds.
        var scheduler = new TestScheduler();
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
                    : Task.FromResult(Deserialize(Payload(ThreeRows))));
        AircraftSnapshotClient sut = new AircraftSnapshotClientFixture()
            .WithApi(api)
            .WithProvider(Scheduling(scheduler))
            .WithOptions(Options.Create(new OpenSkyOptions { Box = Houston, PollInterval = TimeSpan.FromSeconds(1) }));

        var changes = new List<IChangeSet<AircraftSnapshot, string>>();
        var completed = false;
        Exception? errored = null;
        using var subscription = sut.Snapshots.Subscribe(changes.Add, failure => errored = failure, () => completed = true);

        // When
        using var polling = sut.Poll();
        scheduler.AdvanceBy(TimeSpan.FromSeconds(3).Ticks);

        // Then. The subscriber received the following poll's changes, and nothing ended the stream
        // in between — a demo that loses its collection to one bad response ends on a blank grid.
        errored.Should().BeNull("a failed poll is not a failed stream");
        completed.Should().BeFalse();
        changes.SelectMany(static changeset => changeset)
            .Select(static change => change.Key)
            .Should()
            .Contain(["a1b2c3", "d4e5f6", "070809"]);
    }

    private static void Some<T>(Option<T> actual, T expected) =>
        ((object) actual).Should().Be(Option<T>.Some(expected));

    private static void None<T>(Option<T> actual) => ((object) actual).Should().Be(Option<T>.None);

    private static void NotSome<T>(Option<T> actual, T unwanted) =>
        ((object) actual).Should().NotBe(Option<T>.Some(unwanted));

    private static SourceCache<AircraftSnapshot, string> Cache() => new(static snapshot => snapshot.Icao24);

    private static IOpenSkyApi Answering(string payload)
    {
        var api = Substitute.For<IOpenSkyApi>();

        api.GetStates(
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<double>(),
                Arg.Any<bool>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Deserialize(payload)));

        return api;
    }

    private static ISchedulerProvider Scheduling(TestScheduler scheduler)
    {
        var schedulers = Substitute.For<ISchedulerProvider>();

        schedulers.BackgroundThread.Returns(scheduler);
        schedulers.UserInterfaceThread.Returns(scheduler);

        return schedulers;
    }

    private static OpenSkyStatesResponse Deserialize(string payload) =>
        JsonSerializer.Deserialize<OpenSkyStatesResponse>(payload)
        ?? throw new InvalidOperationException("The fixture did not deserialize.");

    private static string Payload(string fixture) =>
        File.ReadAllText(Path.Combine("Integrations", "OpenSky", "Fixtures", fixture));

    /// <summary>The three readable rows of the committed fixture, plus one defective row.</summary>
    /// <param name="defect">Which of B-022's defects the fourth row carries.</param>
    /// <returns>A response payload of four rows.</returns>
    private static string FourRowsOneDefective(string defect)
    {
        var readable = JsonDocument.Parse(Payload(ThreeRows))
            .RootElement.GetProperty("states")
            .GetRawText()
            .Trim()
            .Trim('[', ']');

        var defective = defect switch
        {
            "twelve elements" => "[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]",
            "twenty-five elements" => "[" + string.Join(", ", Enumerable.Repeat("0", 25)) + "]",
            "index 0 is null" => Elements("null", "false"),
            "index 8 is a string" => Elements("\"ff0011\"", "\"yes\""),
            _ => throw new ArgumentOutOfRangeException(nameof(defect), defect, "No such defect."),
        };

        return $"{{ \"time\": 1791124330, \"states\": [{readable}, {defective}] }}";
    }

    /// <summary>One eighteen-element row, varying only what a defect needs it to.</summary>
    /// <param name="icao24">Index 0's raw JSON.</param>
    /// <param name="onGround">Index 8's raw JSON.</param>
    /// <returns>The row, as raw JSON.</returns>
    private static string Elements(string icao24, string onGround) =>
        "[" + string.Join(
            ", ",
            icao24,
            "\"TRN0009 \"",
            "\"Testland\"",
            "null",
            "1791124320",
            "null",
            "null",
            "null",
            onGround,
            "null",
            "null",
            "null",
            "null",
            "null",
            "null",
            "false",
            "0",
            "1") + "]";

    /// <summary>One row, with the elements a test varies spelled as the wire spells them.</summary>
    /// <param name="category">Index 17's raw JSON, or <see langword="null"/> for a 17-element row.</param>
    /// <param name="callsign">Index 1's raw JSON.</param>
    /// <param name="squawk">Index 14's raw JSON.</param>
    /// <param name="sensors">Index 12's raw JSON.</param>
    /// <returns>A response payload carrying that one row.</returns>
    private static string Row(
        string? category,
        string callsign = "\"TRN0001 \"",
        string squawk = "\"0021\"",
        string sensors = "null")
    {
        var elements = string.Join(
            ", ",
            "\"a1b2c3\"",
            callsign,
            "\"Testland\"",
            "1791124315",
            "1791124320",
            "-95.3698",
            "29.7604",
            "1234.5",
            "false",
            "128.6",
            "91.2",
            "-1.3",
            sensors,
            "1250.0",
            squawk,
            "false",
            "0");

        return category is null
            ? $"{{ \"time\": 1791124330, \"states\": [[{elements}]] }}"
            : $"{{ \"time\": 1791124330, \"states\": [[{elements}, {category}]] }}";
    }

    private const string ThreeRows = "states-three-rows.json";

    private const string SeventeenElementRow = "states-seventeen-element-row.json";

    /// <summary>
    /// The second poll: "a1b2c3" unchanged, "d4e5f6" at a new barometric altitude, "070809" gone,
    /// "b1c2d3" newly present.
    /// </summary>
    private const string SecondPoll = """
        {
          "time": 1791124345,
          "states": [
            ["a1b2c3", "TRN0001 ", "Testland", 1791124315, 1791124320, -95.3698, 29.7604, 1234.5, false, 128.6, 91.2, -1.3, null, 1250.0, "0021", false, 0, 1],
            ["d4e5f6", null, "Testland", null, 1791124310, null, null, 10668.0, true, null, null, null, null, null, null, false, 0, 2],
            ["b1c2d3", "TRN0004 ", "Testland", 1791124340, 1791124344, -95.2, 29.8, 600.0, false, 90.0, 180.0, 1.0, null, 610.0, "1200", false, 0, 1]
          ]
        }
        """;

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
}

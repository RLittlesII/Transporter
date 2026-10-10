using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Rocket.Surgery.Extensions.Testing.AutoFixtures;
using Transporter.Recording;
using Transporter.UnitTests.Integrations.OpenSky;

namespace Transporter.UnitTests.Recording;

public class RecordingWriterTests
{
    /// <summary>
    /// B-001. The line is ADR-0004's: one JSON object, the arrival instant under
    /// <c>receivedAt</c> and the provider's payload under <c>body</c>, and the payload is spliced
    /// in as an object rather than quoted as a string.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAPayloadAndTheInstantItArrived_WhenTheLineIsWritten_ThenItHoldsBothInTheFormatADR0004Fixes()
    {
        // Given
        using var destination = new StringWriter();
        RecordingWriter sut = new RecordingWriterFixture().WithDestination(destination);
        var arrived = new DateTimeOffset(2026, 10, 4, 14, 22, 1, 113, TimeSpan.Zero);

        // When
        await sut.Write(arrived, OpenSkyPayloads.NoRows);

        // Then
        destination.ToString().Should()
            .Be($"{{\"receivedAt\":\"2026-10-04T14:22:01.113Z\",\"body\":{OpenSkyPayloads.NoRows.Json}}}\n");
    }

    /// <summary>
    /// B-001. The arrival instant is written in UTC however it arrives, so two recorders in two
    /// time zones produce lines a reader can order against each other.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAnArrivalInstantOffFromUtc_WhenTheLineIsWritten_ThenTheInstantIsWrittenAsUtc()
    {
        // Given
        using var destination = new StringWriter();
        RecordingWriter sut = new RecordingWriterFixture().WithDestination(destination);
        var arrived = new DateTimeOffset(2026, 10, 4, 9, 22, 1, 113, TimeSpan.FromHours(-5));

        // When
        await sut.Write(arrived, OpenSkyPayloads.NoRows);

        // Then
        destination.ToString().Should().StartWith("{\"receivedAt\":\"2026-10-04T14:22:01.113Z\"");
    }

    /// <summary>
    /// B-001. One line per payload, so three polls are three lines and a reader that stopped at
    /// the first newline has one payload rather than all of them.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenThreePayloads_WhenEachIsRecorded_ThenThereIsOneLinePerPayload()
    {
        // Given
        using var destination = new StringWriter();
        RecordingWriter sut = new RecordingWriterFixture().WithDestination(destination);

        // When
        await sut.Write(DateTimeOffset.UnixEpoch, OpenSkyPayloads.Row(category: "1"));
        await sut.Write(DateTimeOffset.UnixEpoch.AddSeconds(15), OpenSkyPayloads.Row(category: "2"));
        await sut.Write(DateTimeOffset.UnixEpoch.AddSeconds(30), OpenSkyPayloads.NoRows);

        // Then
        destination.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(3);
    }

    /// <summary>
    /// B-002. The payload reaches the line byte for byte — not re-serialized, not compacted, not
    /// padded — which is what keeps a recording replayable after a converter fix (B-014).
    /// </summary>
    /// <param name="shape">What makes this payload worth recording, named by the case.</param>
    /// <param name="payload">The payload, as the provider would have sent it.</param>
    /// <returns>The running test.</returns>
    [Theory]
    [ClassData(typeof(RecordingWriterCases))]
    public async Task GivenAPayloadTheProviderSent_WhenItIsRecorded_ThenTheLineHoldsThoseBytesUnchanged(string shape, string payload)
    {
        // Given
        using var destination = new StringWriter();
        RecordingWriter sut = new RecordingWriterFixture().WithDestination(destination);

        // When
        await sut.Write(DateTimeOffset.UnixEpoch, payload);

        // Then
        var line = destination.ToString().TrimEnd('\n');
        var body = line["{\"receivedAt\":\"1970-01-01T00:00:00.000Z\",\"body\":".Length..^1];

        body.Should().Be(payload, $"B-002: {shape} is what the provider sent");
    }

    /// <summary>
    /// B-004. A destination that fails takes nothing with it: recording is observationally
    /// transparent, and a poll whose write failed is a poll that still fed the fleet. The task
    /// completes rather than faulting, because the tap awaits it.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenADestinationThatThrows_WhenAPayloadIsRecorded_ThenTheTaskCompletesAndTheFailureIsLogged()
    {
        // Given
        var logger = new RecordingLogger<RecordingWriter>();
        RecordingWriter sut = new RecordingWriterFixture().WithDestination(new FailingWriter()).WithLogger(logger);

        // When
        var recording = async () => await sut.Write(DateTimeOffset.UnixEpoch, OpenSkyPayloads.NoRows);

        // Then
        await recording.Should().NotThrowAsync();
        logger.At(LogLevel.Warning).Should().ContainSingle().Which.Should().Contain("could not be recorded");
    }

    /// <summary>
    /// B-001 against B-002, where the two cannot both hold. A payload carrying a line break
    /// breaks the one-line-per-payload shape, and the payload is written anyway — omitting it is
    /// what B-002 forbids outright — so the condition is logged rather than left silent.
    /// </summary>
    /// <returns>The running test.</returns>
    [Fact]
    public async Task GivenAPayloadCarryingALineBreak_WhenItIsRecorded_ThenItIsWrittenAndTheBrokenShapeIsLogged()
    {
        // Given
        using var destination = new StringWriter();
        var logger = new RecordingLogger<RecordingWriter>();
        RecordingWriter sut = new RecordingWriterFixture().WithDestination(destination).WithLogger(logger);

        // When
        await sut.Write(DateTimeOffset.UnixEpoch, OpenSkyPayloads.ThreeRows);

        // Then
        destination.ToString().Should().Contain(OpenSkyPayloads.ThreeRows.Json);
        logger.At(LogLevel.Warning).Should().ContainSingle().Which.Should().Contain("line break");
    }

    /// <summary>A destination whose every write fails, so B-004's transparency is provable.</summary>
    private sealed class FailingWriter : StringWriter
    {
        public override Task WriteAsync(string? value) => throw new IOException("The disk is full.");
    }
}

/// <summary>Builds the writer, so a constructor change edits this and not every test.</summary>
[AutoFixture(typeof(RecordingWriter))]
internal partial class RecordingWriterFixture
{
    public RecordingWriterFixture()
    {
        WithDestination(new StringWriter());
        WithLogger(new RecordingLogger<RecordingWriter>());
    }
}

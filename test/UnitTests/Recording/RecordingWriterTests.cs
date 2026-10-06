using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Transponder.Recording;
using Transponder.UnitTests.Integrations.OpenSky;

namespace Transponder.UnitTests.Recording;

public class RecordingWriterTests
{
    /// <summary>
    /// B-001. The line is ADR-0004's: one JSON object, the arrival instant under
    /// <c>receivedAt</c> and the provider's payload under <c>body</c>, and the payload is spliced
    /// in as an object rather than quoted as a string.
    /// </summary>
    [Fact]
    public void GivenAPayloadAndTheInstantItArrived_WhenTheLineIsWritten_ThenItHoldsBothInTheFormatADR0004Fixes()
    {
        // Given
        using var destination = new StringWriter();
        var logger = new RecordingLogger<RecordingWriter>();
        var writer = new RecordingWriter(destination, logger);
        var arrived = new DateTimeOffset(2026, 10, 4, 14, 22, 1, 113, TimeSpan.Zero);

        // When
        ((IRecordingWriter) writer).Write(arrived, OpenSkyPayloads.NoRows);

        // Then
        destination.ToString().Should()
            .Be($"{{\"receivedAt\":\"2026-10-04T14:22:01.113Z\",\"body\":{OpenSkyPayloads.NoRows.Json}}}\n");
    }

    /// <summary>
    /// B-001. The arrival instant is written in UTC however it arrives, so two recorders in two
    /// time zones produce lines a reader can order against each other.
    /// </summary>
    [Fact]
    public void GivenAnArrivalInstantOffFromUtc_WhenTheLineIsWritten_ThenTheInstantIsWrittenAsUtc()
    {
        // Given
        using var destination = new StringWriter();
        var writer = new RecordingWriter(destination, new RecordingLogger<RecordingWriter>());
        var arrived = new DateTimeOffset(2026, 10, 4, 9, 22, 1, 113, TimeSpan.FromHours(-5));

        // When
        ((IRecordingWriter) writer).Write(arrived, OpenSkyPayloads.NoRows);

        // Then
        destination.ToString().Should().StartWith("{\"receivedAt\":\"2026-10-04T14:22:01.113Z\"");
    }

    /// <summary>
    /// B-001. One line per payload, so three polls are three lines and a reader that stopped at
    /// the first newline has one payload rather than all of them.
    /// </summary>
    [Fact]
    public void GivenThreePayloads_WhenEachIsRecorded_ThenThereIsOneLinePerPayload()
    {
        // Given
        using var destination = new StringWriter();
        IRecordingWriter writer = new RecordingWriter(destination, new RecordingLogger<RecordingWriter>());

        // When
        writer.Write(DateTimeOffset.UnixEpoch, OpenSkyPayloads.Row(category: "1"));
        writer.Write(DateTimeOffset.UnixEpoch.AddSeconds(15), OpenSkyPayloads.Row(category: "2"));
        writer.Write(DateTimeOffset.UnixEpoch.AddSeconds(30), OpenSkyPayloads.NoRows);

        // Then
        destination.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Should().HaveCount(3);
    }

    /// <summary>
    /// B-002. The payload reaches the line byte for byte — not re-serialized, not compacted, not
    /// padded — which is what keeps a recording replayable after a converter fix (B-014).
    /// </summary>
    /// <param name="shape">What makes this payload worth recording, named by the case.</param>
    /// <param name="payload">The payload, as the provider would have sent it.</param>
    [Theory]
    [ClassData(typeof(RecordingWriterCases))]
    public void GivenAPayloadTheProviderSent_WhenItIsRecorded_ThenTheLineHoldsThoseBytesUnchanged(string shape, string payload)
    {
        // Given
        using var destination = new StringWriter();
        IRecordingWriter writer = new RecordingWriter(destination, new RecordingLogger<RecordingWriter>());

        // When
        writer.Write(DateTimeOffset.UnixEpoch, payload);

        // Then
        var line = destination.ToString().TrimEnd('\n');
        var body = line["{\"receivedAt\":\"1970-01-01T00:00:00.000Z\",\"body\":".Length..^1];

        body.Should().Be(payload, $"B-002: {shape} is what the provider sent");
    }

    /// <summary>
    /// B-004. A destination that fails takes nothing with it: recording is observationally
    /// transparent, and a poll whose write failed is a poll that still fed the fleet.
    /// </summary>
    [Fact]
    public void GivenADestinationThatThrows_WhenAPayloadIsRecorded_ThenNothingIsThrownAndTheFailureIsLogged()
    {
        // Given
        var logger = new RecordingLogger<RecordingWriter>();
        IRecordingWriter writer = new RecordingWriter(new FailingWriter(), logger);

        // When
        var recording = () => writer.Write(DateTimeOffset.UnixEpoch, OpenSkyPayloads.NoRows);

        // Then
        recording.Should().NotThrow();
        logger.At(LogLevel.Warning).Should().ContainSingle().Which.Should().Contain("could not be recorded");
    }

    /// <summary>
    /// B-001 against B-002, where the two cannot both hold. A payload carrying a line break
    /// breaks the one-line-per-payload shape, and the payload is written anyway — omitting it is
    /// what B-002 forbids outright — so the condition is logged rather than left silent.
    /// </summary>
    [Fact]
    public void GivenAPayloadCarryingALineBreak_WhenItIsRecorded_ThenItIsWrittenAndTheBrokenShapeIsLogged()
    {
        // Given
        using var destination = new StringWriter();
        var logger = new RecordingLogger<RecordingWriter>();
        IRecordingWriter writer = new RecordingWriter(destination, logger);

        // When
        writer.Write(DateTimeOffset.UnixEpoch, OpenSkyPayloads.ThreeRows);

        // Then
        destination.ToString().Should().Contain(OpenSkyPayloads.ThreeRows.Json);
        logger.At(LogLevel.Warning).Should().ContainSingle().Which.Should().Contain("line break");
    }

    /// <summary>A destination whose every write fails, so B-004's transparency is provable.</summary>
    private sealed class FailingWriter : StringWriter
    {
        public override void Write(string? value) => throw new IOException("The disk is full.");
    }
}

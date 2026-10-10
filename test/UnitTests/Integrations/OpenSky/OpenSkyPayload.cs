using Transporter.Integrations.OpenSky.Contracts;

namespace Transporter.UnitTests.Integrations.OpenSky;

/// <summary>
/// One synthetic <c>/states/all</c> payload: the raw JSON, and the envelope it reads as.
/// </summary>
/// <param name="Json">The payload, exactly as the provider would have sent it.</param>
/// <remarks>
/// The parsing is here rather than in each test class, through <see cref="IJson"/>'s default
/// implementation, so a test names a payload and gets an envelope without carrying a serializer
/// around. Reading through the real converter is the point: the positional row arrives the way the
/// transport would have produced it, so a reader that mis-indexed would fail here too.
/// </remarks>
public readonly record struct OpenSkyPayload(string Json) : IJson
{
    /// <summary>Gets the envelope this payload reads as.</summary>
    internal OpenSkyStatesResponse Response => ((IJson) this).Read<OpenSkyStatesResponse>(Json);

    /// <inheritdoc/>
    public override string ToString() => Json;

    /// <summary>Takes the raw JSON, for an assertion or a response that wants the text.</summary>
    /// <param name="payload">The payload.</param>
    public static implicit operator string(OpenSkyPayload payload) => payload.Json;

    /// <summary>Makes a payload of raw JSON.</summary>
    /// <param name="json">The payload.</param>
    public static implicit operator OpenSkyPayload(string json) => new(json);
}

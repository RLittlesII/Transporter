using System.Text.Json;

namespace Transporter.UnitTests;

/// <summary>
/// Reading a payload, once, for anything that holds one.
/// </summary>
/// <remarks>
/// A default implementation rather than a helper every test class copies: the behaviour — which
/// serializer options, and what an unreadable payload does — lives here and nowhere else, and a
/// type gains it by declaring the interface. The options are
/// <see cref="JsonSerializerOptions.Default"/> deliberately: a <c>[JsonConverter]</c> attribute on
/// the type being read is honoured by those, which is how the provider's positional row is read
/// here exactly as the transport reads it.
/// <para>
/// It sits in the test project because nothing in production parses JSON itself — Flurl owns the
/// one boundary that exists. When a second one appears that is this repository's rather than a
/// library's, the replay recorder being the known candidate, this moves to that layer and gains a
/// concrete marker type to register as <see cref="IJson"/>, since an interface whose members are
/// all default implementations has nothing to register. Recorded as chore <c>0029</c> rather than
/// built ahead of a caller.
/// </para>
/// </remarks>
public interface IJson
{
    /// <summary>Gets the options a payload is read with.</summary>
    JsonSerializerOptions Options => JsonSerializerOptions.Default;

    /// <summary>Reads a payload as <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">What the payload is.</typeparam>
    /// <param name="json">The payload.</param>
    /// <returns>What the payload holds.</returns>
    /// <exception cref="InvalidOperationException">
    /// The payload read as <see langword="null"/>. A test arranged on nothing would otherwise fail
    /// later and somewhere else, which is the failure this message exists to replace.
    /// </exception>
    T Read<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options)
        ?? throw new InvalidOperationException($"The payload supplied did not read as {typeof(T).Name}.");
}

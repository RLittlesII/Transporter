namespace Transponder.Integrations.OpenSky.Polling;

/// <summary>Told to <see cref="AircraftPollActor"/> to say a poll is wanted.</summary>
/// <remarks>No payload: what is demanded is one poll of the configured box, and who demanded it changes nothing.</remarks>
internal sealed class PollAircraft
{
    private PollAircraft()
    {
    }

    /// <summary>Gets the message to tell.</summary>
    public static PollAircraft Instance { get; } = new();
}

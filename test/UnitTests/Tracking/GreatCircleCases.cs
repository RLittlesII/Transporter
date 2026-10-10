namespace Transporter.UnitTests.Tracking;

/// <summary>
/// Two positions and the great-circle distance between them in metres, each computed from R times
/// the central angle with R = 6,371,008.8 m rather than read off a map (fleet-pipeline B-032).
/// </summary>
/// <remarks>
/// The last two are the cases worth having: every other one moves in latitude alone or sits on the
/// equator, where the cosine of the latitude is one, so a wrong, squared or degrees-valued cosine
/// term passes them.
/// </remarks>
public sealed class GreatCircleCases : TheoryData<double, double, double, double, double>
{
    public GreatCircleCases()
    {
        Add(29.70, -95.40, 29.70, -95.40, 0);
        Add(29.70, -95.40, 29.80, -95.40, 11_119.5);
        Add(0, 0, 0, 1, 111_195.1);
        Add(29.70, -95.40, 29.70, -95.30, 9_658.8);
        Add(29.70, -95.40, 29.80, -95.30, 14_725.6);
    }
}

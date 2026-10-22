using LanguageExt;

namespace Transporter.Model;

/// <summary>One vessel, in the values AIS reports and with its stable MMSI key.</summary>
public sealed class Vessel : TransportVehicle
{
    /// <summary>Initializes a new instance of the <see cref="Vessel"/> class.</summary>
    /// <param name="mmsi">The stable nine-digit identifier.</param>
    /// <param name="lastContact">The latest reported instant.</param>
    public Vessel(string mmsi, DateTimeOffset lastContact)
        : base(mmsi, lastContact)
    {
    }

    public Option<string> Name { get; set; }

    public Option<int> ShipType { get; set; }

    public Option<double> SpeedOverGround { get; set; }

    public Option<double> CourseOverGround { get; set; }

    public Option<double> TrueHeading { get; set; }

    public string ShipTypeName => ShipType.Bind(ShipTypes.Category).IfNone("Unknown");

    public string FlagState => MaritimeIdentificationDigits.State(Key).IfNone("Unknown");

    /// <inheritdoc/>
    public override string Label => Name.Map(static name => name.Trim().TrimEnd('@').Trim())
        .Filter(static name => name.Length > 0)
        .IfNone(Key);

    /// <inheritdoc/>
    public override string GroupKey => ShipTypeName;
}

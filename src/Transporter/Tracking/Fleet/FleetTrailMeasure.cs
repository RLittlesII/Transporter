using System;
using LanguageExt;
using Transporter.Model;

namespace Transporter.Tracking.Fleet;

/// <summary>The source's measure and range for colouring a trail.</summary>
public sealed record FleetTrailMeasure
{
    public required string Name { get; init; }

    public required double Minimum { get; init; }

    public required double Maximum { get; init; }

    public required Func<TransportVehicle, Option<double>> Value { get; init; }
}

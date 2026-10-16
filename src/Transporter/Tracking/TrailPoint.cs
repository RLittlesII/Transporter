using System;
using LanguageExt;
using Transporter.Model;

namespace Transporter.Tracking;

/// <summary>A reported position and the values captured when it joined the trail.</summary>
public sealed record TrailPoint
{
    public required GeoPosition Position { get; init; }

    public required DateTimeOffset LastContact { get; init; }

    public required Option<double> Measure { get; init; }

    public required Option<double> Distance { get; init; }

    public required bool FollowsGap { get; init; }
}

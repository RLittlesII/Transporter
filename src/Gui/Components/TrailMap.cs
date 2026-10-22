using System.Globalization;
using Gui.Theme;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Transporter.Features.Fleet;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;
using Map = Microsoft.Maui.Controls.Maps.Map;

namespace Gui.Components;

/// <summary>The selected vehicle's bounded, gap-aware trail on a native map (B-034).</summary>
public sealed class TrailMap : ContentView
{
    /// <summary>Initializes a new instance of the <see cref="TrailMap"/> class.</summary>
    public TrailMap()
    {
        _map = new Map { MapType = MapType.Street, HeightRequest = 360, IsShowingUser = false };
        _map.Loaded += (_, _) => ShowPendingRegion();
        _legend = new Label { Style = FlightDeckStyles.Caption, TextColor = FlightDeck.InkSecondary };
        Content = new VerticalStackLayout
        {
            Spacing = FlightDeck.Space2,
            Children = { _map, _legend },
        };
        IsVisible = false;
    }

    /// <summary>The selected element; each replacement follows its trail without clearing shared edges.</summary>
    public static readonly BindableProperty ElementProperty = BindableProperty.Create(
        nameof(Element),
        typeof(TrackedVehicle),
        typeof(TrailMap),
        null,
        propertyChanged: static (view, _, _) => ((TrailMap) view).Render(false));

    /// <summary>The source's range and name for colouring the trail.</summary>
    public static readonly BindableProperty MeasureProperty = BindableProperty.Create(
        nameof(TrailMeasure),
        typeof(FleetTrailMeasure),
        typeof(TrailMap),
        null,
        propertyChanged: static (view, _, _) => ((TrailMap) view).Render(true));

    /// <summary>Gets or sets the selected element.</summary>
    public TrackedVehicle? Element
    {
        get => (TrackedVehicle?) GetValue(ElementProperty);
        set => SetValue(ElementProperty, value);
    }

    /// <summary>Gets or sets the source's trail measure.</summary>
    public FleetTrailMeasure? TrailMeasure
    {
        get => (FleetTrailMeasure?) GetValue(MeasureProperty);
        set => SetValue(MeasureProperty, value);
    }

    private static MapSpan? Region(IReadOnlyList<TrailPoint> trail)
    {
        if (trail.Count == 0)
        {
            return null;
        }

        var south = trail.Min(static point => point.Position.Latitude);
        var north = trail.Max(static point => point.Position.Latitude);
        var west = trail.Min(static point => point.Position.Longitude);
        var east = trail.Max(static point => point.Position.Longitude);
        return new MapSpan(
            new Location((south + north) / 2, (west + east) / 2),
            Math.Clamp((north - south) * 1.3, 0.05, 180),
            Math.Clamp((east - west) * 1.3, 0.05, 360));
    }

    private void Render(bool measureChanged)
    {
        var element = Element;
        var measure = TrailMeasure;
        IsVisible = element is not null;
        if (element is null)
        {
            _map.MapElements.Clear();
            _previous = [];
            return;
        }

        if (measure is null)
        {
            return;
        }

        var change = measureChanged
            ? new FleetTrailChange(true, _map.MapElements.Count, FleetTrailLine.Segments(element.Trail, measure))
            : FleetTrailLine.Follow(_previous, element.Trail, measure);
        if (change.Redraw)
        {
            _map.MapElements.Clear();
            _pendingRegion = Region(element.Trail);
            ShowPendingRegion();
        }
        else
        {
            for (var index = 0; index < change.Removed; index++)
            {
                _map.MapElements.RemoveAt(0);
            }
        }

        foreach (var segment in change.Added)
        {
            _map.MapElements.Add(new Polyline
            {
                StrokeColor = segment.Shade.Match(FlightDeck.Ramp, static () => FlightDeck.InkMuted),
                StrokeWidth = 4,
                Geopath =
                {
                    new Location(segment.From.Position.Latitude, segment.From.Position.Longitude),
                    new Location(segment.To.Position.Latitude, segment.To.Position.Longitude),
                },
            });
        }

        _previous = element.Trail;
        _legend.Text = string.Create(CultureInfo.InvariantCulture, $"{measure.Name} · {measure.Minimum:0}–{measure.Maximum:0}");
    }

    private void ShowPendingRegion()
    {
        if (_map.Handler is not null && _pendingRegion is { } region)
        {
            _map.MoveToRegion(region);
            _pendingRegion = null;
        }
    }

    private readonly Map _map;
    private readonly Label _legend;
    private IReadOnlyList<TrailPoint> _previous = [];
    private MapSpan? _pendingRegion;
}

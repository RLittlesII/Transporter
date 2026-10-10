using System;
using System.Collections.Generic;
using System.Linq;
using Gui.Theme;
using LanguageExt;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Transporter.Features.Fleet;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;

namespace Gui.Components;

/// <summary>One tracked vehicle as a card, laid out from the description's card roles (B-029, `fleet-pipeline` B-036).</summary>
/// <remarks>
/// The layout is built when the roles change — on a swap — and never on a poll: a new element only
/// sets text, so a real feed does not flicker the fleet (B-006). The card reads columns through the
/// description and names no subclass of <c>TransportVehicle</c> (B-022).
/// </remarks>
public sealed class AircraftCard : ContentView
{
    /// <summary>Initializes a new instance of the <see cref="AircraftCard"/> class.</summary>
    public AircraftCard()
    {
        _frame = new Border
        {
            Background = FlightDeck.SurfaceRaised,
            Stroke = FlightDeck.Line,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = FlightDeck.RadiusMedium },
            Padding = 0,
            MinimumWidthRequest = FlightDeck.CardMinimumWidth,
        };
        _edge = new BoxView { Color = FlightDeck.StatusStale, WidthRequest = 6, IsVisible = false };
        _title = new Label { Style = FlightDeckStyles.BodyStrong, FontSize = 18, LineBreakMode = LineBreakMode.TailTruncation };
        _subtitle = new Label { Style = FlightDeckStyles.Caption, LineBreakMode = LineBreakMode.TailTruncation };
        _place = new Label { Style = FlightDeckStyles.Body, LineBreakMode = LineBreakMode.TailTruncation };
        _badge = new StatusBadge();
        _leg = new Readout { Caption = "Leg" };
        _travelled = new Readout { Caption = "Flown" };
        _age = new Label { Style = FlightDeckStyles.DataSmall };
        Content = _frame;
        Padding = 0;

        VisualStateManager.SetVisualStateGroups(this, new VisualStateGroupList
        {
            new VisualStateGroup
            {
                Name = "CommonStates",
                States =
                {
                    new VisualState { Name = "Normal" },
                    new VisualState
                    {
                        Name = "Selected",
                        Setters =
                        {
                            new Setter { Property = IsChosenProperty, Value = true },
                            new Setter { Property = BackgroundColorProperty, Value = FlightDeck.Clear },
                        },
                    },
                },
            },
        });
        Lay();
    }

    /// <summary>The element the card shows.</summary>
    public static readonly BindableProperty VehicleProperty = BindableProperty.Create(
        nameof(Vehicle),
        typeof(TrackedVehicle),
        typeof(AircraftCard),
        propertyChanged: static (card, before, _) => ((AircraftCard) card).Fill(before as TrackedVehicle));

    /// <summary>Which columns fill the card's roles.</summary>
    public static readonly BindableProperty CardProperty = BindableProperty.Create(
        nameof(Card),
        typeof(FleetCard),
        typeof(AircraftCard),
        propertyChanged: static (card, _, _) => ((AircraftCard) card).Lay());

    /// <summary>The instant the provider last reported, which the age is measured from (B-031).</summary>
    public static readonly BindableProperty ObservedProperty = BindableProperty.Create(
        nameof(Observed),
        typeof(DateTimeOffset),
        typeof(AircraftCard),
        DateTimeOffset.MinValue,
        propertyChanged: static (card, _, _) => ((AircraftCard) card).Age());

    /// <summary>Whether the collection has the card selected; set by its visual state, not by a handler.</summary>
    public static readonly BindableProperty IsChosenProperty = BindableProperty.Create(
        nameof(IsChosen),
        typeof(bool),
        typeof(AircraftCard),
        false,
        propertyChanged: static (card, _, _) => ((AircraftCard) card).Mark());

    /// <summary>Gets or sets the element the card shows.</summary>
    public TrackedVehicle? Vehicle
    {
        get => (TrackedVehicle?) GetValue(VehicleProperty);
        set => SetValue(VehicleProperty, value);
    }

    /// <summary>Gets or sets which columns fill the card's roles.</summary>
    public FleetCard? Card
    {
        get => (FleetCard?) GetValue(CardProperty);
        set => SetValue(CardProperty, value);
    }

    /// <summary>Gets or sets the instant the age is measured from.</summary>
    public DateTimeOffset Observed
    {
        get => (DateTimeOffset) GetValue(ObservedProperty);
        set => SetValue(ObservedProperty, value);
    }

    /// <summary>Gets or sets a value indicating whether the card is selected.</summary>
    public bool IsChosen
    {
        get => (bool) GetValue(IsChosenProperty);
        set => SetValue(IsChosenProperty, value);
    }

    private static ColumnDefinitionCollection Columns(int count)
    {
        var columns = new ColumnDefinitionCollection();
        for (var index = 0; index < count; index++)
        {
            columns.Add(new ColumnDefinition(GridLength.Star));
        }

        return columns;
    }

    /// <summary>Builds the layout for the roles in force: the only place a child is created.</summary>
    private void Lay()
    {
        var card = Card ?? new FleetCard();
        _readouts.Clear();
        foreach (var readout in card.Readouts)
        {
            _readouts.Add((readout, new Readout { Caption = readout.Column.Name }));
        }

        var values = new Grid
        {
            ColumnDefinitions = Columns(3),
            ColumnSpacing = FlightDeck.Space3,
            RowSpacing = FlightDeck.Space3,
        };
        var cells = _readouts.Select(static pair => (View) pair.View).Concat([_leg, _travelled]).ToArray();
        for (var index = 0; index < cells.Length; index++)
        {
            if (index % 3 == 0)
            {
                values.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            values.Add(cells[index], index % 3, index / 3);
        }

        _place.IsVisible = card.Place.IsSome;
        _subtitle.IsVisible = card.Subtitle.IsSome;

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)),
            ColumnSpacing = FlightDeck.Space2,
        };
        header.Add(new VerticalStackLayout { Spacing = 0, Children = { _title, _subtitle } }, 0, 0);
        header.Add(_badge, 1, 0);

        var body = new VerticalStackLayout
        {
            Padding = FlightDeck.Space3,
            Spacing = FlightDeck.Space3,
            Children =
            {
                header,
                _place,
                values,
                new BoxView { Color = FlightDeck.Line, HeightRequest = 1 },
                new HorizontalStackLayout
                {
                    Spacing = FlightDeck.Space2,
                    Children = { new Label { Text = "Last contact", Style = FlightDeckStyles.Caption }, _age },
                },
            },
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitionCollection(new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)),
        };
        row.Add(_edge, 0, 0);
        row.Add(body, 1, 0);
        _frame.Content = row;

        Fill(before: null);
    }

    /// <summary>Sets every text from the element; creates nothing.</summary>
    /// <param name="before">The element the card showed until now, or none.</param>
    /// <remarks>Whether a readout pulses is <see cref="FleetCardMotion"/>'s decision; a re-lay passes none, so a swap moves nothing (B-038).</remarks>
    private void Fill(TrackedVehicle? before)
    {
        var card = Card ?? new FleetCard();
        var tracked = Vehicle;
        if (tracked is null)
        {
            return;
        }

        var vehicle = tracked.Vehicle;
        _title.Text = card.Title.Match(column => column.Value(vehicle), () => vehicle.Label);
        _subtitle.Text = card.Subtitle.Match(column => column.Value(vehicle), static () => string.Empty);
        _place.Text = card.Place.Match(column => column.Value(vehicle), static () => string.Empty);
        var shown = Prelude.Optional(before);
        foreach (var (readout, view) in _readouts)
        {
            var change = readout.Change(tracked);
            view.Value = readout.Column.Value(vehicle);
            view.Change = change.IfNone(string.Empty);
            view.Dim(tracked.IsStale);
            if (FleetCardMotion.Pulses(readout, shown, tracked))
            {
                view.Pulse();
            }
        }

        _leg.Value = FleetCardText.Distance(tracked.Leg);
        _travelled.Value = FleetCardText.Distance(Option<double>.Some(tracked.Travelled));
        _leg.Dim(tracked.IsStale);
        _travelled.Dim(tracked.IsStale);
        var mark = FleetCardStatus.Of(card, tracked);
        _badge.Kind = mark switch
        {
            FleetCardMark.Stale => StatusKind.Stale,
            FleetCardMark.NoFix => StatusKind.NoFix,
            FleetCardMark.Updated => StatusKind.Updated,
            _ => StatusKind.Fresh,
        };
        SemanticProperties.SetDescription(this, $"{_title.Text}, {(mark == FleetCardMark.NoFix ? "no fix" : mark.ToString().ToLowerInvariant())}");
        Mark();
        Age();
    }

    /// <summary>Sets the age text from the observed instant; the only arithmetic, and it is <see cref="FleetCardText"/>'s.</summary>
    private void Age() =>
        _age.Text = Vehicle is { } tracked ? FleetCardText.Age(Observed, tracked.Vehicle.LastContact) : FleetCardText.Missing;

    /// <summary>The border says selected, stale or neither; the stale edge shows with it (B-008, B-032).</summary>
    private void Mark()
    {
        var stale = Vehicle?.IsStale ?? false;
        _edge.IsVisible = stale;
        _frame.Stroke = IsChosen ? FlightDeck.Accent : stale ? FlightDeck.StatusStale : FlightDeck.Line;
        _frame.StrokeThickness = IsChosen ? 2 : 1;
        _frame.Background = IsChosen ? FlightDeck.AccentSoft : FlightDeck.SurfaceRaised;
    }

    private readonly List<(FleetReadout Readout, Readout View)> _readouts = [];
    private readonly Border _frame;
    private readonly BoxView _edge;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _place;
    private readonly StatusBadge _badge;
    private readonly Readout _leg;
    private readonly Readout _travelled;
    private readonly Label _age;
}

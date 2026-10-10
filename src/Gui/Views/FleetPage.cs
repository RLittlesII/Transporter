using System;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CommunityToolkit.Maui.Markup;
using Gui.Components;
using Gui.Theme;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Layouts;
using ReactiveMarbles.ObservableEvents;
using Transporter.Features.Fleet.ViewModels;
using Transporter.Tracking;
using Transporter.Tracking.Fleet;

namespace Gui.Views;

/// <summary>The dashboard: the controls, the summary, the fleet as cards, and the detail pane (B-001, B-029).</summary>
/// <remarks>
/// Each card is laid out from the description's roles, so a swap edits no markup here (B-007,
/// B-021). The collection is the view model's one bound collection; cards update in place.
/// </remarks>
public class FleetPage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="FleetPage"/> class.</summary>
    /// <param name="viewModel">The page's view model, by constructor and resolved from nothing (B-003).</param>
    /// <param name="summary">The summary strip's view model, which projects the tracker's counts (B-015).</param>
    public FleetPage(FleetViewModel viewModel, FleetSummaryViewModel summary)
    {
        BindingContext = ViewModel = viewModel;
        Title = "Fleet";

        _layout = new GridItemsLayout(1, ItemsLayoutOrientation.Vertical)
        {
            HorizontalItemSpacing = FlightDeck.Space4,
            VerticalItemSpacing = FlightDeck.Space4,
        };
        var fleet = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
            ItemsLayout = _layout,
            EmptyView = Empty(),
            ItemTemplate = new DataTemplate(() => new AircraftCard()
                .Bind(AircraftCard.VehicleProperty, ".")
                .Bind(AircraftCard.CardProperty, nameof(FleetViewModel.Card), source: viewModel)
                .Bind(AircraftCard.ObservedProperty, nameof(FleetViewModel.Observed), source: viewModel)),
        }
            .Bind(ItemsView.ItemsSourceProperty, static (FleetViewModel model) => model.Fleet)
            .Bind(
                SelectableItemsView.SelectedItemProperty,
                static (FleetViewModel model) => model.Selected,
                static (FleetViewModel model, TrackedVehicle? selected) => model.Selected = selected,
                BindingMode.TwoWay);
        fleet
            .Events()
            .SizeChanged
            .Subscribe(_ => Span(fleet.Width))
            .DisposeWith(_garbage);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitionCollection(
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)),
            RowSpacing = FlightDeck.Space4,
            Padding = FlightDeck.Space4,
            Children =
            {
                Controls().Row(0),
                Summary(summary).Row(1),
                fleet.Row(2),
                Detail(viewModel.Detail).Row(3),
            },
        };
    }

    /// <summary>Gets the view model this page was handed.</summary>
    public FleetViewModel ViewModel { get; }

    /// <inheritdoc/>
    /// <remarks>The page's subscriptions end with its handler, so nothing outlives the view it was made for.</remarks>
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        if (Handler is null)
        {
            _garbage.Dispose();
        }
    }

    /// <summary>The search box, the filter and grouping choosers and the refresh control, on one panel (B-001, B-028).</summary>
    /// <returns>The controls bar.</returns>
    /// <remarks>
    /// The refresh button binds a command and the indicator beside it binds a value; neither reads
    /// the fleet, and no handler here decides whether a poll happens (B-028, ADR-0012).
    /// </remarks>
    private static Border Controls() =>
        Panel(new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            AlignItems = FlexAlignItems.Center,
            Children =
            {
                new Label { Text = "Fleet", Style = FlightDeckStyles.Title }.Margins(right: FlightDeck.Space6),
                new Entry { Placeholder = "Search", WidthRequest = 240 }
                    .Bind(
                        Entry.TextProperty,
                        static (FleetViewModel model) => model.SearchText,
                        static (model, text) => model.SearchText = text ?? string.Empty)
                    .Margins(right: FlightDeck.Space3, bottom: FlightDeck.Space1),
                new Picker { Title = "Filter", ItemDisplayBinding = new Binding(nameof(FleetFilterChoice.Name)), WidthRequest = 180 }
                    .Bind(Picker.ItemsSourceProperty, static (FleetViewModel model) => model.Filters)
                    .Margins(right: FlightDeck.Space3, bottom: FlightDeck.Space1),
                new Picker { Title = "Group by", ItemDisplayBinding = new Binding(nameof(FleetGrouping.Name)), WidthRequest = 180 }
                    .Bind(Picker.ItemsSourceProperty, static (FleetViewModel model) => model.Groupings)
                    .Margins(right: FlightDeck.Space3, bottom: FlightDeck.Space1),
                new Button { Text = "Refresh" }
                    .Bind(Button.CommandProperty, static (FleetViewModel model) => model.RefreshCommand)
                    .Margins(right: FlightDeck.Space2, bottom: FlightDeck.Space1),
                new ActivityIndicator { WidthRequest = 24, HeightRequest = 24 }
                    .Bind(ActivityIndicator.IsRunningProperty, static (FleetViewModel model) => model.IsRefreshing),
            },
        });

    /// <summary>The summary strip: three counts the tracker derived, bound and never recounted here (B-015).</summary>
    /// <param name="summary">The strip's view model.</param>
    /// <returns>The summary panel.</returns>
    private static Border Summary(FleetSummaryViewModel summary) =>
        Panel(new HorizontalStackLayout
        {
            Spacing = FlightDeck.Space6,
            Children =
            {
                new Readout { Caption = "Tracked" }.Bind(Readout.ValueProperty, nameof(FleetSummaryViewModel.Tracked), source: summary, stringFormat: "{0:N0}"),
                new Readout { Caption = "Stale" }.Bind(Readout.ValueProperty, nameof(FleetSummaryViewModel.Stale), source: summary, stringFormat: "{0:N0}"),
                new Readout { Caption = "Groups" }.Bind(Readout.ValueProperty, nameof(FleetSummaryViewModel.Groups), source: summary, stringFormat: "{0:N0}"),
            },
        });

    /// <summary>The detail pane: the selected vehicle's own fields, or a prompt when nothing is selected (B-013, B-014).</summary>
    /// <param name="detail">The pane's view model.</param>
    /// <returns>The detail panel.</returns>
    /// <remarks>The rows are the view model's; this names no subclass and converts no unit (B-019).</remarks>
    private static Border Detail(FleetDetailViewModel detail)
    {
        var rows = new FlexLayout { Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Start }
            .Bind(BindableLayout.ItemsSourceProperty, nameof(FleetDetailViewModel.Rows), source: detail);
        BindableLayout.SetItemTemplate(rows, new DataTemplate(static () => new Readout
        {
            WidthRequest = DetailCellWidth,
            Margin = new Thickness(0, 0, FlightDeck.Space4, FlightDeck.Space2),
        }
            .Bind(Readout.CaptionProperty, nameof(FleetDetailRow.Label))
            .Bind(Readout.ValueProperty, nameof(FleetDetailRow.Value))));

        return Panel(new VerticalStackLayout
        {
            Spacing = FlightDeck.Space3,
            Children =
            {
                new Label { Text = "Select an aircraft", Style = FlightDeckStyles.Caption }
                    .Bind(IsVisibleProperty, nameof(FleetDetailViewModel.IsEmpty), source: detail),
                new Label { Style = FlightDeckStyles.Heading }
                    .Bind(Label.TextProperty, nameof(FleetDetailViewModel.Title), source: detail),
                rows,
            },
        });
    }

    /// <summary>What the grid shows before the first poll lands, or when a filter admits nothing.</summary>
    /// <returns>The empty state.</returns>
    private static View Empty() =>
        new VerticalStackLayout
        {
            Padding = FlightDeck.Space6 * 2,
            Spacing = FlightDeck.Space2,
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "No aircraft yet", Style = FlightDeckStyles.Heading, HorizontalTextAlignment = TextAlignment.Center },
                new Label
                {
                    Text = "Waiting for the first poll, or nothing matches the filter.",
                    Style = FlightDeckStyles.Caption,
                    HorizontalTextAlignment = TextAlignment.Center,
                },
            },
        };

    /// <summary>A surface panel around a piece of the page.</summary>
    /// <param name="content">What the panel holds.</param>
    /// <returns>The panel.</returns>
    private static Border Panel(View content) =>
        new()
        {
            Background = FlightDeck.Surface,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = FlightDeck.RadiusLarge },
            Padding = FlightDeck.Space3,
            Content = content,
        };

    /// <summary>One more column each time another card's least width fits.</summary>
    /// <param name="width">The collection's width.</param>
    /// <remarks>Layout only: it reads no item and decides nothing about the fleet.</remarks>
    private void Span(double width)
    {
        var fits = Math.Floor((width + FlightDeck.Space4) / (FlightDeck.CardMinimumWidth + FlightDeck.Space4));
        var span = Math.Max(1, (int) fits);
        if (_layout.Span != span)
        {
            _layout.Span = span;
        }
    }

    private const double DetailCellWidth = 150;

    private readonly CompositeDisposable _garbage = [];
    private readonly GridItemsLayout _layout;
}

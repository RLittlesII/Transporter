using System;
using System.Globalization;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Converters;
using CommunityToolkit.Maui.Core;
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

/// <summary>The dashboard: the controls, the summary, the banner, the fleet as cards, and the detail pane (B-001, B-023, B-029).</summary>
/// <remarks>
/// Each card is laid out from the description's roles, so a swap edits no markup here (B-007,
/// B-021). The collection is the view model's one bound collection; cards update in place.
/// </remarks>
public class FleetPage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="FleetPage"/> class.</summary>
    /// <param name="viewModel">The page's view model, by constructor and resolved from nothing (B-003).</param>
    /// <param name="summary">The summary strip's view model, which projects the tracker's counts (B-015).</param>
    /// <param name="banner">The banner's and the toast's notices, each at its own cadence (B-023 – B-026, B-037).</param>
    /// <param name="poll">When the next poll is due, and whether the provider is refusing (B-036).</param>
    public FleetPage(FleetViewModel viewModel, FleetSummaryViewModel summary, FleetBannerViewModel banner, FleetPollViewModel poll)
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

        // The toast is the page's to render: the view models publish what interrupts and name no
        // toast type (B-025, B-027).
        banner.Interrupting
            .Subscribe(static notice => Interrupt(notice.Kind == FleetNoticeKind.Quiet
                ? $"Feed quiet since {notice.Instant:HH:mm:ss}"
                : $"Feed resumed · {notice.Tracked:N0} tracked"))
            .DisposeWith(_garbage);
        viewModel.Swapped
            .Subscribe(static _ => Interrupt("Source swapped"))
            .DisposeWith(_garbage);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitionCollection(
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)),
            RowSpacing = FlightDeck.Space4,
            Padding = FlightDeck.Space4,
            Children =
            {
                Controls().Row(0),
                Summary(summary, poll).Row(1),
                Banner(banner).Row(2),
                fleet.Row(3),
                Detail(viewModel.Detail).Row(4),
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

    /// <summary>The search box, the filter, grouping and sort choosers and the refresh control, on one panel (B-001, B-011, B-012, B-028).</summary>
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
                    .Bind(
                        Picker.SelectedItemProperty,
                        static (FleetViewModel model) => model.SelectedFilter,
                        static (FleetViewModel model, FleetFilterChoice? choice) => model.SelectedFilter = choice,
                        BindingMode.TwoWay)
                    .Margins(right: FlightDeck.Space1, bottom: FlightDeck.Space1),
                new Button { Text = "Clear" }
                    .Bind(Button.CommandProperty, static (FleetViewModel model) => model.ClearFilterCommand)
                    .Margins(right: FlightDeck.Space3, bottom: FlightDeck.Space1),
                new Picker { Title = "Group by", ItemDisplayBinding = new Binding(nameof(FleetGrouping.Name)), WidthRequest = 180 }
                    .Bind(Picker.ItemsSourceProperty, static (FleetViewModel model) => model.Groupings)
                    .Bind(
                        Picker.SelectedItemProperty,
                        static (FleetViewModel model) => model.SelectedGrouping,
                        static (FleetViewModel model, FleetGrouping? grouping) => model.SelectedGrouping = grouping,
                        BindingMode.TwoWay)
                    .Margins(right: FlightDeck.Space3, bottom: FlightDeck.Space1),
                new Picker { Title = "Sort by", ItemDisplayBinding = new Binding(nameof(FleetColumn.Name)), WidthRequest = 180 }
                    .Bind(Picker.ItemsSourceProperty, static (FleetViewModel model) => model.Columns)
                    .Bind(
                        Picker.SelectedItemProperty,
                        static (FleetViewModel model) => model.SortColumn,
                        static (FleetViewModel model, FleetColumn? column) => model.SortColumn = column,
                        BindingMode.TwoWay)
                    .Margins(right: FlightDeck.Space1, bottom: FlightDeck.Space1),
                new Button { Text = "Reverse" }
                    .Bind(Button.CommandProperty, static (FleetViewModel model) => model.ReverseCommand)
                    .Margins(right: FlightDeck.Space3, bottom: FlightDeck.Space1),
                new Picker { Title = "Source", WidthRequest = 200 }
                    .Bind(Picker.ItemsSourceProperty, static (FleetViewModel model) => model.Targets)
                    .Bind(VisualElement.IsEnabledProperty, static (FleetViewModel model) => model.HasTargets)
                    .Bind(
                        Picker.SelectedIndexProperty,
                        static (FleetViewModel model) => model.SelectedTarget,
                        static (FleetViewModel model, int position) => model.SelectedTarget = position,
                        BindingMode.TwoWay)
                    .Margins(right: FlightDeck.Space2, bottom: FlightDeck.Space1),
                new ActivityIndicator { WidthRequest = 24, HeightRequest = 24 }
                    .Bind(ActivityIndicator.IsRunningProperty, static (FleetViewModel model) => model.IsSwapping)
                    .Bind(VisualElement.IsVisibleProperty, static (FleetViewModel model) => model.IsSwapping)
                    .Margins(right: FlightDeck.Space2),
                new Button { Text = "Refresh" }
                    .Bind(Button.CommandProperty, static (FleetViewModel model) => model.RefreshCommand)
                    .Margins(right: FlightDeck.Space2, bottom: FlightDeck.Space1),
                new ActivityIndicator { WidthRequest = 24, HeightRequest = 24 }
                    .Bind(ActivityIndicator.IsRunningProperty, static (FleetViewModel model) => model.IsRefreshing),
            },
        });

    /// <summary>
    /// The summary strip: three counts the tracker derived, bound and never recounted here (B-015),
    /// and when the next poll is due and whether the provider is refusing (B-036).
    /// </summary>
    /// <param name="summary">The strip's view model.</param>
    /// <param name="poll">The poll status's view model; the countdown and the badge hide together for a source that does not poll.</param>
    /// <returns>The summary panel.</returns>
    private static Border Summary(FleetSummaryViewModel summary, FleetPollViewModel poll) =>
        Panel(new HorizontalStackLayout
        {
            Spacing = FlightDeck.Space6,
            Children =
            {
                new Readout { Caption = "Tracked" }.Bind(Readout.ValueProperty, nameof(FleetSummaryViewModel.Tracked), source: summary, stringFormat: "{0:N0}"),
                new Readout { Caption = "Stale" }.Bind(Readout.ValueProperty, nameof(FleetSummaryViewModel.Stale), source: summary, stringFormat: "{0:N0}"),
                new Readout { Caption = "Groups" }.Bind(Readout.ValueProperty, nameof(FleetSummaryViewModel.Groups), source: summary, stringFormat: "{0:N0}"),
                new HorizontalStackLayout
                {
                    Spacing = FlightDeck.Space2,
                    Children =
                    {
                        new PollCountdown()
                            .Bind(PollCountdown.StatusProperty, nameof(FleetPollViewModel.Status), source: poll),
                        new StatusBadge { Kind = StatusKind.Throttled }
                            .Bind(IsVisibleProperty, nameof(FleetPollViewModel.RefusedFor), source: poll, converter: new IsNotNullConverter()),
                        new Label { Style = FlightDeckStyles.Caption, VerticalTextAlignment = TextAlignment.Center }
                            .Bind(Label.TextProperty, nameof(FleetPollViewModel.RefusedFor), source: poll, stringFormat: "for {0:m\\:ss}")
                            .Bind(IsVisibleProperty, nameof(FleetPollViewModel.RefusedFor), source: poll, converter: new IsNotNullConverter()),
                    },
                }
                    .Bind(IsVisibleProperty, nameof(FleetPollViewModel.IsPolling), source: poll),
            },
        });

    /// <summary>
    /// The banner: the latest notice and the window's totals, each under its own heading, and the
    /// two intervals edited while running (B-023, B-024, B-026, B-037).
    /// </summary>
    /// <param name="banner">The banner's view model.</param>
    /// <returns>The banner panel, in the page's own rows so it overlays no card (B-024).</returns>
    /// <remarks>Every number is bound as the tracker raised it; this computes and sums none (B-023, B-037).</remarks>
    private static Border Banner(FleetBannerViewModel banner) =>
        Panel(new FlexLayout
        {
            Wrap = FlexWrap.Wrap,
            AlignItems = FlexAlignItems.Start,
            Children =
            {
                Group(
                    "Latest update",
                    Count(banner, "At", "Latest.Instant", "{0:HH:mm:ss}"),
                    Count(banner, "Tracked", "Latest.Tracked"),
                    Count(banner, "Added", "Latest.Added"),
                    Count(banner, "Updated", "Latest.Updated"),
                    Count(banner, "Removed", "Latest.Removed")),
                Group(
                    "Last 20 updates · whole fleet",
                    Count(banner, "Added", "Window.Added"),
                    Count(banner, "Updated", "Window.Updated"),
                    Count(banner, "Removed", "Window.Removed")),
                Group(
                    "Pacing",
                    Interval(banner, "Banner every (s)", nameof(FleetBannerViewModel.BannerInterval)),
                    Interval(banner, "Toast every (s)", nameof(FleetBannerViewModel.ToastInterval))),
            },
        });

    /// <summary>One headed group of the banner, so neither group reads as the other's subtotal (B-037).</summary>
    /// <param name="heading">The group's heading, which is the page's text.</param>
    /// <param name="items">What the group shows.</param>
    /// <returns>The group.</returns>
    private static View Group(string heading, params View[] items)
    {
        var row = new HorizontalStackLayout { Spacing = FlightDeck.Space4 };
        foreach (var item in items)
        {
            row.Children.Add(item);
        }

        return new VerticalStackLayout
        {
            Spacing = FlightDeck.Space1,
            Children = { new Label { Text = heading, Style = FlightDeckStyles.Caption }, row },
        }.Margins(right: FlightDeck.Space6, bottom: FlightDeck.Space2);
    }

    /// <summary>One number the banner shows, bound off the view model and formatted only.</summary>
    /// <param name="banner">The banner's view model.</param>
    /// <param name="caption">What the number is.</param>
    /// <param name="path">Where it is read; empty until the first notice.</param>
    /// <param name="format">How it is written.</param>
    /// <returns>The readout.</returns>
    private static Readout Count(FleetBannerViewModel banner, string caption, string path, string format = "{0:N0}") =>
        new Readout { Caption = caption }.Bind(Readout.ValueProperty, path, source: banner, stringFormat: format);

    /// <summary>One interval, edited in seconds while the application runs (B-026).</summary>
    /// <param name="banner">The banner's view model.</param>
    /// <param name="caption">Which surface it paces.</param>
    /// <param name="path">The interval property.</param>
    /// <returns>The input.</returns>
    /// <remarks>A value that is not a positive number of seconds leaves the interval as it was.</remarks>
    private static View Interval(FleetBannerViewModel banner, string caption, string path) =>
        new VerticalStackLayout
        {
            Children =
            {
                new Label { Text = caption, Style = FlightDeckStyles.Caption },
                new Entry { Keyboard = Keyboard.Numeric, WidthRequest = 80 }
                    .Bind(Entry.TextProperty, path, BindingMode.TwoWay, Seconds, source: banner),
            },
        };

    /// <summary>Shows a toast; the one platform notification call in the application (B-027).</summary>
    /// <param name="text">What it says.</param>
    private static void Interrupt(string text) => _ = Toast.Make(text, ToastDuration.Long).Show();

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

    /// <summary>An interval as seconds in an entry, and back only when the entry holds a positive number.</summary>
    /// <remarks>Anything else is <see cref="Binding.DoNothing"/>, so a half-typed value leaves the pacing as it was.</remarks>
    private sealed class SecondsConverter : IValueConverter
    {
        /// <inheritdoc/>
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is TimeSpan interval ? interval.TotalSeconds.ToString(culture) : string.Empty;

        /// <inheritdoc/>
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            double.TryParse(value as string, NumberStyles.Float, culture, out var seconds) && seconds > 0
                ? TimeSpan.FromSeconds(seconds)
                : Binding.DoNothing;
    }

    private const double DetailCellWidth = 150;

    /// <summary>The one converter the interval entries share.</summary>
    private static readonly SecondsConverter Seconds = new();

    private readonly CompositeDisposable _garbage = [];
    private readonly GridItemsLayout _layout;
}

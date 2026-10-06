using System.Collections.Generic;
using CommunityToolkit.Maui.Markup;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Transponder.Features.Fleet;
using Transponder.Features.Fleet.ViewModels;
using Transponder.Tracking;
using Transponder.Tracking.Fleet;

namespace Gui.Views;

/// <summary>The dashboard: the grid, the search and filter controls, the grouping selection, the summary and the detail pane (B-001).</summary>
/// <remarks>
/// Every column, header and cell is built from the description the live source published, so a swap
/// edits no markup here (B-007, B-021).
/// </remarks>
public class FleetPage : ContentPage
{
    /// <summary>Initializes a new instance of the <see cref="FleetPage"/> class.</summary>
    /// <param name="viewModel">The grid's view model, by constructor and resolved from nothing (B-003).</param>
    public FleetPage(FleetViewModel viewModel)
    {
        BindingContext = ViewModel = viewModel;
        Title = "Fleet";

        _fleet = new CollectionView
        {
            SelectionMode = SelectionMode.Single,
        }.Bind(ItemsView.ItemsSourceProperty, static (FleetViewModel model) => model.Fleet);

        Content = new Grid
        {
            RowDefinitions = new RowDefinitionCollection(
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)),
            RowSpacing = 8,
            Padding = 12,
            Children =
            {
                Controls().Row(0),
                Summary().Row(1),
                _fleet.Row(2),
                Detail().Row(3),
            },
        };

        Describe(viewModel.Columns);
        viewModel.PropertyChanged += (_, arguments) =>
        {
            if (arguments.PropertyName == nameof(FleetViewModel.Columns))
            {
                Describe(viewModel.Columns);
            }
        };
    }

    /// <summary>Gets the view model this page was handed.</summary>
    public FleetViewModel ViewModel { get; }

    /// <summary>The row the description draws: one label per column, in the order it published them (B-007).</summary>
    /// <param name="columns">The live source's columns.</param>
    /// <returns>The header and the item template built from them.</returns>
    private static (IView Header, DataTemplate Row) Build(IReadOnlyList<FleetColumn> columns)
    {
        var header = new HorizontalStackLayout { Spacing = 16 };
        foreach (var column in columns)
        {
            header.Add(new Label { Text = column.Name, FontAttributes = FontAttributes.Bold, WidthRequest = CellWidth });
        }

        return (header, new DataTemplate(() =>
        {
            var row = new HorizontalStackLayout { Spacing = 16 };
            foreach (var column in columns)
            {
                row.Add(new Label { WidthRequest = CellWidth }.Bind(
                    Label.TextProperty,
                    static (TrackedVehicle tracked) => tracked.Vehicle,
                    convert: vehicle => vehicle is null ? string.Empty : column.Value(vehicle)));
            }

            return row;
        }));
    }

    /// <summary>The search box, the filter dropdown and the grouping dropdown; what they publish is item 0037's (B-001).</summary>
    /// <returns>The controls bar.</returns>
    private static HorizontalStackLayout Controls() =>
        new HorizontalStackLayout
        {
            Spacing = 12,
            Children =
            {
                new Entry { Placeholder = "Search", WidthRequest = 240 }.Bind(
                    Entry.TextProperty,
                    static (FleetViewModel model) => model.SearchText,
                    static (model, text) => model.SearchText = text ?? string.Empty),
                new Picker { Title = "Filter", ItemDisplayBinding = new Binding(nameof(FleetFilterChoice.Name)) }
                    .Bind(Picker.ItemsSourceProperty, static (FleetViewModel model) => model.Filters),
                new Picker { Title = "Group by", ItemDisplayBinding = new Binding(nameof(FleetGrouping.Name)) }
                    .Bind(Picker.ItemsSourceProperty, static (FleetViewModel model) => model.Groupings),
            },
        };

    /// <summary>The summary surface; item 0038 projects the tracker's counts into it (B-001, B-015).</summary>
    /// <returns>The summary bar.</returns>
    private static Label Summary() => new() { Text = "Summary" };

    /// <summary>The detail pane, the one surface allowed to name a subclass; item 0038 fills it (B-001, B-013).</summary>
    /// <returns>The detail pane.</returns>
    private static Label Detail() => new() { Text = "Select a vehicle" };

    private void Describe(IReadOnlyList<FleetColumn> columns)
    {
        var (header, row) = Build(columns);
        _fleet.Header = header;
        _fleet.ItemTemplate = row;
    }

    /// <summary>Every cell is the same width, so the header labels line up with the row beneath them.</summary>
    private const double CellWidth = 140;

    private readonly CollectionView _fleet;
}

namespace Transponder.Features.Fleet.ViewModels;

/// <summary>One line of the detail pane: a label and its value, already formatted for display (B-013).</summary>
/// <param name="Label">What the value is.</param>
/// <param name="Value">The value as the pane shows it.</param>
/// <remarks>A record rather than a tuple, because a binding reads properties and a tuple's are named <c>Item1</c> and <c>Item2</c>.</remarks>
public sealed record FleetDetailRow(string Label, string Value);

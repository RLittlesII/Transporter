using System;
using System.Collections.Generic;
using LanguageExt;
using Transponder.Model;
using Transponder.Tracking.Fleet;

namespace Transponder.Features.Fleet;

/// <summary>
/// The predicate the user's two inputs compose into: what the search text admits, and what it
/// admits once the control's choice is applied as well (B-009 - B-011).
/// </summary>
/// <remarks>
/// A static of its own rather than a method on the view model, for two reasons. A function of its
/// arguments is tested by calling it, which is what lets B-010 and B-011 be proved without
/// constructing a view model or a scheduler. And "empty text matches everything" is the clause a
/// later edit breaks silently, so it is held still by a test of its own.
/// <para>
/// Nothing here enumerates a collection: the result is handed to <c>IFleetTracker.Filter</c> and
/// the pipeline is what re-evaluates it (B-009, B-018).
/// </para>
/// </remarks>
public static class FleetSearch
{
    /// <summary>What the search text alone admits (B-010).</summary>
    /// <param name="text">What the user typed; surrounding whitespace is ignored and empty admits everything.</param>
    /// <param name="columns">The live source's columns, whose cells are the rest of what search reads (B-007).</param>
    /// <returns>A predicate over the abstract vehicle.</returns>
    /// <remarks>
    /// The label and every column's cell, compared case-insensitively. Every column rather than a
    /// marked subset: the description carries no searchable flag, and a source that publishes a
    /// column has already said the value is worth showing (§ 11 row 5).
    /// </remarks>
    public static Func<TransportVehicle, bool> Matching(string text, IReadOnlyList<FleetColumn> columns)
    {
        var wanted = text?.Trim() ?? string.Empty;

        if (wanted.Length is 0)
        {
            return static _ => true;
        }

        var searched = columns ?? [];

        return vehicle => Contains(vehicle.Label, wanted) || AnyCell(searched, vehicle, wanted);
    }

    /// <summary>What the search text and the control's choice admit together (B-009, B-011).</summary>
    /// <param name="text">What the user typed.</param>
    /// <param name="columns">The live source's columns.</param>
    /// <param name="choice">The control's choice, absent when the user has cleared it.</param>
    /// <returns>The one predicate handed to the tracker.</returns>
    /// <remarks>
    /// A conjunction of two independent predicates, which is what makes clearing one leave the other
    /// standing: neither input reads the other, so neither can reset it (B-011).
    /// </remarks>
    public static Func<TransportVehicle, bool> Composed(
        string text,
        IReadOnlyList<FleetColumn> columns,
        Option<FleetFilterChoice> choice)
    {
        var matching = Matching(text, columns);

        return choice.Match(
            chosen => vehicle => matching(vehicle) && chosen.Matches(vehicle),
            () => matching);
    }

    /// <summary>Whether a cell of any column contains what was typed.</summary>
    /// <param name="columns">The columns to read.</param>
    /// <param name="vehicle">The vehicle to read them for.</param>
    /// <param name="wanted">The trimmed text.</param>
    /// <returns><see langword="true"/> when one of them contains it.</returns>
    private static bool AnyCell(IReadOnlyList<FleetColumn> columns, TransportVehicle vehicle, string wanted)
    {
        for (var index = 0; index < columns.Count; index++)
        {
            if (Contains(columns[index].Value(vehicle), wanted))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether one string contains another, ignoring case.</summary>
    /// <param name="value">The value to search.</param>
    /// <param name="wanted">The text to find.</param>
    /// <returns><see langword="true"/> when it is there.</returns>
    private static bool Contains(string value, string wanted) =>
        value is not null && value.Contains(wanted, StringComparison.OrdinalIgnoreCase);
}

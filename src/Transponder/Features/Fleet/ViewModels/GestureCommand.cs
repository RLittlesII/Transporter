using System;
using System.Windows.Input;

namespace Transponder.Features.Fleet.ViewModels;

/// <summary>A command that is a gesture and nothing else: always executable, and it runs one action.</summary>
/// <param name="pressed">What the press does, which is to publish it.</param>
/// <remarks>
/// Here because a view model exposes commands and this repository had none, and small because the
/// gesture's whole job is to reach a stream (`fleet-dashboard` B-028). It never disables itself: a
/// press that cannot cause a poll is refused by the actor that owns the throttle
/// (`aircraft-source` B-053), and a button that disabled itself here would state a rule in a second
/// place and would have to guess when the window closed.
/// </remarks>
internal sealed class GestureCommand(Action pressed) : ICommand
{
    /// <inheritdoc/>
    /// <remarks>
    /// Subscribing to it does nothing, because the command's answer never changes and no view ever
    /// has to re-ask. A field-backed event here would be one nothing raises.
    /// </remarks>
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => true;

    /// <inheritdoc/>
    public void Execute(object? parameter) => pressed();
}

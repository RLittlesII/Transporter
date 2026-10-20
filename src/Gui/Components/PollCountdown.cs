using System.Globalization;
using Gui.Theme;
using Transporter.Features.Fleet;
using Transporter.Tracking;

namespace Gui.Components;

/// <summary>Counts down to the next poll by animating toward the bound due instant (B-036).</summary>
/// <remarks>
/// One animation per due instant, aborted and restarted when a new one arrives; the view model
/// publishes one value per status and never a tick (B-017). Under reduced motion it shows the due
/// time as still text and moves nothing, as a readout does (B-038).
/// </remarks>
public sealed class PollCountdown : ContentView
{
    /// <summary>Initializes a new instance of the <see cref="PollCountdown"/> class.</summary>
    public PollCountdown()
    {
        _caption = new Label { Text = "Next poll", Style = FlightDeckStyles.Overline };
        _seconds = new Label { Style = FlightDeckStyles.DataLarge, LineBreakMode = LineBreakMode.NoWrap, Text = FleetCardText.Missing };
        _bar = new ProgressBar { ProgressColor = FlightDeck.Accent, WidthRequest = 96, Progress = 0 };
        Content = new VerticalStackLayout { Spacing = 2, Children = { _caption, _seconds, _bar } };
    }

    /// <summary>The complete report whose two instants determine one countdown.</summary>
    public static readonly BindableProperty StatusProperty = BindableProperty.Create(
        nameof(Status),
        typeof(PollStatus),
        typeof(PollCountdown),
        PollStatus.None,
        propertyChanged: static (countdown, _, _) => ((PollCountdown) countdown).Restart());

    /// <summary>Gets or sets the complete report for the next countdown.</summary>
    public PollStatus Status
    {
        get => (PollStatus) GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    /// <summary>Starts one animation over the time left to the new due instant, replacing the running one.</summary>
    private void Restart()
    {
        this.AbortAnimation(CountdownName);
        var status = Status;
        if (status.NextDue.ToNullable() is not { } due)
        {
            _seconds.Text = FleetCardText.Missing;
            _bar.Progress = 0;
            return;
        }

        if (Motion.IsReduced())
        {
            _seconds.Text = due.ToLocalTime().ToString("HH:mm:ss", CultureInfo.CurrentCulture);
            _bar.Progress = 0;
            return;
        }

        var left = FleetCardText.DueIn(due, status.PolledAt.ToNullable());
        Show(left > TimeSpan.Zero ? 1 : 0, left);
        if (left <= TimeSpan.Zero)
        {
            return;
        }

        new Animation(fraction => Show(fraction, left), 1, 0)
            .Commit(this, CountdownName, rate: 100, length: (uint) left.TotalMilliseconds, easing: Easing.Linear);
    }

    /// <summary>Writes the seconds left and the bar for a point in the animation.</summary>
    /// <param name="fraction">How much of the wait is left, from one to zero.</param>
    /// <param name="left">The whole wait.</param>
    private void Show(double fraction, TimeSpan left)
    {
        _bar.Progress = fraction;
        _seconds.Text = string.Create(CultureInfo.InvariantCulture, $"{Math.Ceiling(left.TotalSeconds * fraction):0}s");
    }

    private const string CountdownName = "poll-countdown";

    private readonly Label _caption;
    private readonly Label _seconds;
    private readonly ProgressBar _bar;
}

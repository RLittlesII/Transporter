using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Transponder.Recording;

/// <summary>
/// Says what each configured recording is, when the application starts rather than when the
/// presenter swaps (B-026).
/// </summary>
/// <remarks>
/// <para>
/// A report and not a refusal. This deliberately departs from
/// <c>OpenSkyConfigurationValidator</c>, which fails the host: an absent credential means nothing
/// works, while an unusable recording means the fallback is gone and the live demo is fine —
/// stopping the application for it would turn a degraded talk into no talk.
/// </para>
/// <para>
/// The checks themselves ran where the sources were registered, because whether a source is
/// registered at all depends on them (B-024). What is left for startup is saying so out loud, which
/// is why this holds rows it did not produce.
/// </para>
/// </remarks>
internal sealed class ReplayStartupReport : IHostedService
{
    /// <summary>Initializes a new instance of the <see cref="ReplayStartupReport"/> class.</summary>
    /// <param name="recordings">Every configured recording, as the registration checked it.</param>
    /// <param name="logger">Where the report is written.</param>
    public ReplayStartupReport(IReadOnlyList<CheckedRecording> recordings, ILogger<ReplayStartupReport> logger)
    {
        Recordings = recordings;
        _logger = logger;
    }

    /// <summary>Gets what was found, one row per replay source configuration names a key for.</summary>
    public IReadOnlyList<CheckedRecording> Recordings { get; }

    /// <inheritdoc/>
    /// <remarks>
    /// Each row names the recording and the defect, so the line a presenter reads says which key to
    /// edit. A usable recording is reported too: "replay is available" before a talk is worth as
    /// much as the warning, and an absent line is not evidence of a working fallback.
    /// </remarks>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var recording in Recordings)
        {
            switch (recording.Defect)
            {
                case RecordingDefect.None:
                    _logger.LogInformation(
                        "The {Source} replay source will read {Path}, which covers {Seconds} seconds.",
                        recording.Source,
                        recording.Path,
                        recording.Span.TotalSeconds);
                    break;
                case RecordingDefect.Unnamed:
                    _logger.LogInformation(
                        "No recording is named under {Section}:{Source}, so that replay source is not selectable.",
                        ReplayOptions.Section,
                        recording.Source);
                    break;
                case RecordingDefect.Absent:
                    _logger.LogWarning(
                        "The {Source} replay source is not registered: {Path} is not there.",
                        recording.Source,
                        recording.Path);
                    break;
                case RecordingDefect.Unreadable:
                    _logger.LogWarning(
                        "The {Source} replay source is not registered: {Path} could not be read, or holds no recorded line.",
                        recording.Source,
                        recording.Path);
                    break;
                case RecordingDefect.TooShort:
                    _logger.LogWarning(
                        "The {Source} replay source is not registered: {Path} covers {Seconds} seconds, which is less "
                        + "than the staleness threshold, so nothing in it ever goes quiet.",
                        recording.Source,
                        recording.Path,
                        recording.Span.TotalSeconds);
                    break;
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private readonly ILogger<ReplayStartupReport> _logger;
}

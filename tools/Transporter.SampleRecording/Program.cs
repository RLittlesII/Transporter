using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Transporter.Recording;
using Transporter.Tracking;

namespace Transporter.SampleRecording;

/// <summary>
/// Writes the sample recording the arguments name. Invoked deliberately — never by a build.
/// </summary>
internal static class Program
{
    /// <summary>The recording's file name unless an argument names another.</summary>
    internal const string DefaultName = "aircraft-sample.ndjson";

    /// <summary>The seed unless an argument names another.</summary>
    internal const int DefaultSeed = 1;

    /// <summary>Writes one recording.</summary>
    /// <param name="args">
    /// <c>--seed &lt;number&gt;</c>, <c>--output &lt;path&gt;</c>, <c>--polls &lt;number&gt;</c>
    /// and <c>--interval &lt;seconds&gt;</c>, all optional.
    /// </param>
    /// <returns>Zero when the recording was written.</returns>
    private static async Task<int> Main(string[] args)
    {
        if (Whole(args, "--seed", DefaultSeed) is not { } seed
            || Whole(args, "--polls", SyntheticRecording.DefaultPolls) is not { } polls
            || Seconds(args, "--interval", SyntheticRecording.DefaultInterval) is not { } interval)
        {
            return 1;
        }

        var span = interval * (polls - 1);

        if (span <= FleetTracker.DefaultStaleAfter)
        {
            var threshold = FleetTracker.DefaultStaleAfter;

            Console.Error.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{polls} payloads {interval} apart span {span}, which does not outlast the {threshold} staleness threshold."));
            Console.Error.WriteLine("A recording that short cannot show an aircraft go quiet, and the startup check refuses it.");

            return 1;
        }

        var output = Argument(args, "--output") ?? Path.Combine(RecordingOptions.DefaultRoot, DefaultName);

        if (Path.GetDirectoryName(output) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        await using (var destination = new StreamWriter(output, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            var recorder = new RecordingWriter(destination, NullLogger<RecordingWriter>.Instance);
            var recording = new SyntheticRecording(recorder, seed, polls, interval);

            await recording.Write().ConfigureAwait(false);

            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"Wrote {recording.Polls} payloads spanning {recording.Span} from seed {seed} to {Path.GetFullPath(output)}"));
        }

        return 0;
    }

    /// <summary>Reads the value after a named argument.</summary>
    /// <param name="args">What the command line carried.</param>
    /// <param name="name">The argument's name, including its dashes.</param>
    /// <returns>The value, or <see langword="null"/> when the argument was not given.</returns>
    private static string? Argument(string[] args, string name)
    {
        for (var index = 0; index < args.Length - 1; index++)
        {
            if (string.Equals(args[index], name, StringComparison.Ordinal))
            {
                return args[index + 1];
            }
        }

        return null;
    }

    /// <summary>Reads a whole number argument.</summary>
    /// <param name="args">What the command line carried.</param>
    /// <param name="name">The argument's name, including its dashes.</param>
    /// <param name="fallback">What it is when the argument was not given.</param>
    /// <returns>The number, or <see langword="null"/> when the argument was given and is not one.</returns>
    private static int? Whole(string[] args, string name, int fallback)
    {
        if (Argument(args, name) is not { } value)
        {
            return fallback;
        }

        if (int.TryParse(value, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        Console.Error.WriteLine($"{name} takes a number greater than zero; '{value}' is not one.");

        return null;
    }

    /// <summary>Reads an argument given in seconds.</summary>
    /// <param name="args">What the command line carried.</param>
    /// <param name="name">The argument's name, including its dashes.</param>
    /// <param name="fallback">What it is when the argument was not given.</param>
    /// <returns>The span, or <see langword="null"/> when the argument was given and is not a number of seconds.</returns>
    private static TimeSpan? Seconds(string[] args, string name, TimeSpan fallback)
    {
        if (Argument(args, name) is not { } value)
        {
            return fallback;
        }

        if (double.TryParse(value, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            return TimeSpan.FromSeconds(parsed);
        }

        Console.Error.WriteLine($"{name} takes a number of seconds greater than zero; '{value}' is not one.");

        return null;
    }
}

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Transponder.Recording;

namespace Transponder.SampleRecording;

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
    /// <param name="args"><c>--seed &lt;number&gt;</c> and <c>--output &lt;path&gt;</c>, both optional.</param>
    /// <returns>Zero when the recording was written.</returns>
    private static async Task<int> Main(string[] args)
    {
        var requested = Argument(args, "--seed");

        if (requested is not null && !int.TryParse(requested, CultureInfo.InvariantCulture, out _))
        {
            Console.Error.WriteLine($"--seed takes a number; '{requested}' is not one.");

            return 1;
        }

        var seed = requested is null ? DefaultSeed : int.Parse(requested, CultureInfo.InvariantCulture);
        var output = Argument(args, "--output") ?? Path.Combine(RecordingOptions.DefaultRoot, DefaultName);

        if (Path.GetDirectoryName(output) is { Length: > 0 } directory)
        {
            Directory.CreateDirectory(directory);
        }

        await using (var destination = new StreamWriter(output, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            var recorder = new RecordingWriter(destination, NullLogger<RecordingWriter>.Instance);

            await new SyntheticRecording(recorder, seed).Write().ConfigureAwait(false);
        }

        var span = SyntheticRecording.Interval * (SyntheticRecording.Polls - 1);

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Wrote {SyntheticRecording.Polls} payloads spanning {span} from seed {seed} to {Path.GetFullPath(output)}"));

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
}

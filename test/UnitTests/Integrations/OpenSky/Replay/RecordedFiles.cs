using System.Text;
using Transponder.Recording;

namespace Transponder.UnitTests.Integrations.OpenSky.Replay;

/// <summary>
/// A recordings root held in memory: the names a test configured, and the lines behind each one.
/// </summary>
/// <remarks>
/// A rehearsal recording is operational data that is never a fixture and never committed
/// (replay-source B-006), so a test of the registration has no file to point at. Resolution against
/// a real root is <see cref="RecordingLibrary"/>'s and is asserted against that type, which is
/// B-025; what this stands in for is the directory, not the resolving.
/// </remarks>
internal sealed class RecordedFiles : IRecordingLibrary
{
    /// <summary>Initializes a new instance of the <see cref="RecordedFiles"/> class.</summary>
    /// <param name="root">The root the names resolve under, as the report names it.</param>
    internal RecordedFiles(string root = "recordings")
    {
        _root = root;
    }

    /// <inheritdoc/>
    public string Resolve(string name) => Path.Combine(_root, name);

    /// <inheritdoc/>
    public bool Holds(string name) => _recordings.ContainsKey(name);

    /// <inheritdoc/>
    public Stream? Open(string name) =>
        _recordings.TryGetValue(name, out var lines) ? new MemoryStream(Encoding.UTF8.GetBytes(lines)) : null;

    /// <summary>Puts a recording in the root under a name.</summary>
    /// <param name="name">The name configuration will spell.</param>
    /// <param name="lines">The recorded lines.</param>
    /// <returns>The same root, so arranging chains.</returns>
    internal RecordedFiles Holding(string name, string lines)
    {
        _recordings[name] = lines;

        return this;
    }

    private readonly Dictionary<string, string> _recordings = [];
    private readonly string _root;
}

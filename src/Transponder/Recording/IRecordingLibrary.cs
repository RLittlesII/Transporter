using System.IO;

namespace Transponder.Recording;

/// <summary>
/// The recordings root, by name: where a configured recording resolves to and how it is opened
/// (B-025).
/// </summary>
/// <remarks>
/// A seam rather than a static call on <see cref="File"/>, because the startup report and the
/// registration below it run over recordings a test supplies — and a rehearsal recording is
/// operational data that is never a fixture (B-006), so no test may have a file to point at.
/// </remarks>
internal interface IRecordingLibrary
{
    /// <summary>Where a named recording resolves to under the root.</summary>
    /// <param name="name">The recording's file name, as configuration spells it.</param>
    /// <returns>The resolved path, which is what the report names.</returns>
    string Resolve(string name);

    /// <summary>Whether the root holds that recording.</summary>
    /// <param name="name">The recording's file name.</param>
    /// <returns><see langword="true"/> when it is there to be opened.</returns>
    bool Holds(string name);

    /// <summary>Opens it for reading.</summary>
    /// <param name="name">The recording's file name.</param>
    /// <returns>The opened, seekable stream; <see langword="null"/> when it could not be opened.</returns>
    Stream? Open(string name);
}

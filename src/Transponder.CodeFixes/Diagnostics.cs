namespace Transponder.CodeFixes;

/// <summary>The diagnostic ids these fixes answer.</summary>
/// <remarks>
/// Literals rather than a reference to <c>Transponder.Analyzers</c>: the two assemblies both sit in
/// the compiler's analyzer load path from different directories, and a cross-reference between them
/// is resolved by the host rather than by the build. <c>CodeFixCoverageTests</c> is what keeps the
/// lists in step, so the duplication is checked rather than hoped for.
/// </remarks>
internal static class Diagnostics
{
    /// <summary>One contract method per endpoint, returning <c>Task&lt;T&gt;</c>, cancellation last.</summary>
    internal const string ContractMethodShape = "TRN0009";

    /// <summary>One internal sealed implementation per transport, implemented explicitly.</summary>
    internal const string ContractImplementationShape = "TRN0011";

    /// <summary>One cache per client, at the application's lifetime.</summary>
    internal const string CacheLifetime = "TRN0017";
}

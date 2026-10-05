namespace Transponder.UnitTests.Analyzers;

/// <summary>Sources the boundary rules are tested against, one layer per constant.</summary>
/// <remarks>Shared the way Airframe's <c>DesignTestData</c> is: each namespace is one <c>Layers</c> classification.</remarks>
internal static class BoundaryTestData
{
    // lang=csharp
    internal const string WireType = """
        namespace Transponder.Integrations.OpenSky.Contracts;

        internal sealed class OpenSkyStateRow
        {
            public int Index { get; set; }
        }
        """;

    // lang=csharp
    internal const string ViewModelNamingTheRow = """
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Features.Demo.ViewModels;

        internal sealed class DemoViewModel
        {
            public int Count()
            {
                var row = new OpenSkyStateRow();

                return row.Index;
            }
        }
        """;

    // lang=csharp
    internal const string DomainTypeNamingTheRow = """
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Model;

        internal sealed class TransportVehicle
        {
            public int Key()
            {
                OpenSkyStateRow row = new();

                return row.Index;
            }
        }
        """;

    // lang=csharp
    internal const string IntegrationCodeNamingTheRow = """
        using Transponder.Integrations.OpenSky.Contracts;

        namespace Transponder.Integrations.OpenSky.Http;

        internal sealed class OpenSkyHttpApi
        {
            public int Read()
            {
                var row = new OpenSkyStateRow();

                return row.Index;
            }
        }
        """;
}

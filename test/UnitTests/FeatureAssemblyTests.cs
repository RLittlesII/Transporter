using System.Linq;
using AwesomeAssertions;
using Transponder.Model;

namespace Transponder.UnitTests;

public class FeatureAssemblyTests
{
    /// <summary>
    /// Item 0076. The feature assembly is plain .NET and a page is the host's, so nothing in it may
    /// compile against MAUI. An unused package reference is compiled away and passes this test; the
    /// rule in <c>transponder-conventions</c> references/coding.md covers that half by review.
    /// </summary>
    [Fact]
    public void GivenTheFeatureAssembly_WhenItsReferencesAreRead_ThenNoneIsMaui()
    {
        // Given
        var assembly = typeof(TransportVehicle).Assembly;

        // When
        var references = assembly.GetReferencedAssemblies().Select(static reference => reference.Name);

        // Then
        references.Should().NotContain(static name => name!.StartsWith("Microsoft.Maui", StringComparison.Ordinal));
    }
}

using System.Linq;
using AwesomeAssertions;
using Transporter.Model;

namespace Transporter.UnitTests;

public class FeatureAssemblyTests
{
    /// <summary>
    /// Item 0076. The feature assembly is plain .NET and a page is the host's, so nothing in it may
    /// compile against MAUI. An unused package reference is compiled away and passes this test; the
    /// rule in <c>transporter-conventions</c> references/coding.md covers that half by review.
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

    /// <summary>
    /// Item 0085, preserving `aircraft-source` B-041 and `fleet-dashboard` B-020. A view model names
    /// each of these, and <c>TRN0006</c> reports one that names a class under <c>Tracking</c> the
    /// seam does not publish, so each is declared in <c>Messages</c> and nowhere else.
    /// </summary>
    /// <param name="name">The message's type name.</param>
    [Theory]
    [ClassData(typeof(SwapMessageCases))]
    public void GivenAMessageAViewModelNames_WhenItsDeclarationIsRead_ThenItIsInMessages(string name)
    {
        // Given
        var assembly = typeof(TransportVehicle).Assembly;

        // When
        var namespaces = assembly.GetTypes()
            .Where(type => type.Name == name)
            .Select(static type => type.Namespace);

        // Then
        namespaces.Should().Equal("Transporter.Messages");
    }
}

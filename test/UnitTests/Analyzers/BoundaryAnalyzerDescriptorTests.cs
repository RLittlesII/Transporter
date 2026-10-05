using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Transponder.Analyzers;

namespace Transponder.UnitTests.Analyzers;

public class BoundaryAnalyzerDescriptorTests
{
    // The repository's convention is that a system under test is built by a generated fixture
    // rather than a constructor call. It does not apply here: the AutoFixtures generator builds a
    // type from its constructor parameters, and an analyzer has none — nothing to arrange, nothing
    // for a constructor change to ripple through. Declaring a fixture for it would add a file that
    // holds nothing.

    [Fact]
    public void GivenTheSupportedDiagnostics_WhenEachIsMappedToAClaim_ThenTheMappingIsOneToOne()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var claims = sut.SupportedDiagnostics.Select(ClaimOf).ToArray();

        // Then
        claims.Should().OnlyHaveUniqueItems("a claim is enforced by one diagnostic and a diagnostic enforces one claim");
        claims.Should().HaveSameCount(sut.SupportedDiagnostics);
    }

    [Fact]
    public void GivenTheSupportedDiagnostics_WhenTheIdsAreRead_ThenEachIsTrnPrefixedAndCarriesNoClaimNumber()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var ids = sut.SupportedDiagnostics.Select(static descriptor => descriptor.Id).ToArray();

        // Then
        ids.Should().AllSatisfy(static id => id.Should().MatchRegex(@"^TRN\d{4}$"));
        ids.Should().BeEquivalentTo(
            Enumerable.Range(1, ids.Length).Select(static number => $"TRN{number:D4}"),
            static options => options.WithStrictOrdering(),
            "the ids run on the analyzer's own sequence from TRN0001");
        sut.SupportedDiagnostics.Should().AllSatisfy(static descriptor =>
            NumberOf(descriptor.Id).Should().NotBe(
                NumberOf(ClaimOf(descriptor)),
                "a diagnostic id that encodes a claim number collides the moment a second specification numbers from B-001"));
    }

    [Fact]
    public void GivenTheSupportedDiagnostics_WhenTheTitlesAndMessagesAreRead_ThenEachNamesItsSpecificationAndClaim()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var descriptors = sut.SupportedDiagnostics;

        // Then
        descriptors.Should().AllSatisfy(static descriptor =>
        {
            var claim = ClaimOf(descriptor);

            claim.Should().StartWith("aircraft-source ", "a build message says which specification's agreement broke");
            descriptor.MessageFormat.ToString().Should().Contain(claim);
            descriptor.Description.ToString().Should().Contain(claim);
        });
    }

    [Fact]
    public void GivenTheSupportedDiagnostics_WhenTheDefaultSeveritiesAreRead_ThenEveryOneIsError()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var descriptors = sut.SupportedDiagnostics;

        // Then
        descriptors.Should().AllSatisfy(static descriptor =>
        {
            descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error, "a boundary already crossed is not a suggestion");
            descriptor.IsEnabledByDefault.Should().BeTrue();
        });
    }

    [Fact]
    public void GivenTheSupportedDiagnostics_WhenTheIdsAreCompared_ThenNoneIsDuplicatedAndNoRetiredIdIsReused()
    {
        // Given
        var sut = new BoundaryAnalyzer();
        var released = SpecificationTables
            .Read("src/Transponder.Analyzers/AnalyzerReleases.Unshipped.md")
            .Split('\n')
            .Select(static line => Regex.Match(line, @"^\|?\s*(TRN\d{4})\s*(?:\||$)"))
            .Where(static match => match.Success)
            .Select(static match => match.Groups[1].Value)
            .ToArray();

        // When
        var ids = sut.SupportedDiagnostics.Select(static descriptor => descriptor.Id).ToArray();

        // Then
        ids.Should().OnlyHaveUniqueItems();
        released.Should().OnlyHaveUniqueItems();
        ids.Should().BeSubsetOf(released, "an id that is not in the release record is one nothing stops being reused later");
    }

    [Fact]
    public void GivenTheMappingTable_WhenComparedWithTheAssignedClaims_ThenItIsExactlyTheEighteenAndNothingMore()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var enforced = sut.SupportedDiagnostics.Select(ClaimOf).Select(ClaimId).ToArray();

        // Then
        enforced.Should().BeEquivalentTo(
            AssignedClaims(),
            "ADR-0006 assigns the set; a rule no claim asked for and a claim with no rule are both defects");
    }

    [Fact]
    public void GivenTheMappingTable_WhenEachClaimIsClassified_ThenNoneIsAClaimAboutAComputedValue()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var enforced = sut.SupportedDiagnostics.Select(ClaimOf).Select(ClaimId).ToArray();

        // Then
        enforced.Should().NotIntersectWith(
            BehaviouralClaims(),
            "a claim another specification proves with an xUnit test is not a claim this analyzer may assert as well");
    }

    [Fact]
    public void GivenADiagnosticWithNoClaimInTheMappingTable_WhenTheDescriptorsAreRead_ThenItIsReportedAsUnclaimed()
    {
        // Given
        var sut = new BoundaryAnalyzer();

        // When
        var unclaimed = sut.SupportedDiagnostics
            .Where(static descriptor => !Regex.IsMatch(descriptor.Title.ToString(), @"^[a-z-]+ B-\d{3} — "))
            .Select(static descriptor => descriptor.Id)
            .ToArray();

        // Then
        unclaimed.Should().BeEmpty("a rule exists because a claim asked for it, and the claim is what its title names");
    }

    [Fact]
    public void GivenTheMappingTable_WhenARowIsRead_ThenItNamesASpecificationAndClaimAndRestatesNeitherTextNorTest()
    {
        // Given
        var sut = new BoundaryAnalyzer();
        // The Enforces cell is what makes a row the mapping table's: § 7 also carries a table of
        // which diagnostics can have a code fix, and its rows open with a TRN id too.
        var rows = SpecificationTables.Rows(Specification, @"^\|\s*`TRN\d{4}`\s*\|\s*`?aircraft-source`? B-\d{3}");

        // When
        var mapped = rows.Select(static cells => (Diagnostic: cells[0].Trim('`'), Claim: cells[1])).ToArray();

        // Then
        mapped.Select(static row => row.Diagnostic).Should().BeEquivalentTo(
            sut.SupportedDiagnostics.Select(static descriptor => descriptor.Id),
            "the table and the descriptors are one mapping, not two");
        mapped.Should().AllSatisfy(row =>
        {
            row.Claim.Should().MatchRegex(@"^`?aircraft-source`? B-\d{3}$");
            row.Claim.Should().NotContain("SHALL", "the claim's text lives in that specification's § 3");
            row.Claim.Should().NotContain("BoundaryAnalyzerTests", "the test that proves it lives in that specification's § 9");
        });
        mapped.Should().AllSatisfy(row =>
            ClaimOf(sut.SupportedDiagnostics.Single(descriptor => descriptor.Id == row.Diagnostic))
                .Should()
                .Be(ClaimReference(row.Claim)));
    }

    private static string ClaimOf(DiagnosticDescriptor descriptor) =>
        descriptor.Title.ToString().Split(Diagnostics.ClaimSeparator)[0];

    private static string ClaimId(string claimReference) => claimReference.Split(' ').Last();

    private static string ClaimReference(string cell) => cell.Replace("`", string.Empty);

    private static int NumberOf(string identifier) =>
        int.Parse(Regex.Match(identifier, @"\d+").Value);

    private static IEnumerable<string> AssignedClaims() => MatrixRows(static test => test.Contains("analyzer"));

    private static IEnumerable<string> BehaviouralClaims() => MatrixRows(static test => !test.Contains("analyzer"));

    private static IEnumerable<string> MatrixRows(Func<string, bool> takeTest) =>
        SpecificationTables
            .Rows(AircraftSpecification, @"^\|\s*B-\d{3}\s*\|\s*`@B-\d{3}`")
            .Where(cells => takeTest(cells[2]))
            .Select(static cells => cells[0]);


    private const string SpecificationPath = "features/boundary-analyzer/.spec/README.md";

    private const string AircraftSpecificationPath = "features/aircraft-source/.spec/README.md";

    private static readonly string Specification = SpecificationTables.Read(SpecificationPath);

    private static readonly string AircraftSpecification = SpecificationTables.Read(AircraftSpecificationPath);
}

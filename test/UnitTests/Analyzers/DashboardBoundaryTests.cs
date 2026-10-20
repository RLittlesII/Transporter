using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Rocket.Surgery.Extensions.Testing.SourceGenerators;
using Transporter.Analyzers;

namespace Transporter.UnitTests.Analyzers;

public class DashboardBoundaryTests
{
    /// <summary>B-017, B-018 and B-022 report the violating semantic operation, including head views and feature statics.</summary>
    /// <param name="source">The synthetic compilation.</param>
    /// <param name="expected">The expected boundary diagnostic.</param>
    /// <returns>The completed analysis.</returns>
    [Theory]
    [ClassData(typeof(DashboardBoundaryCases))]
    public async Task GivenAForbiddenPresentationOperation_WhenAnalyzed_ThenItsBoundaryDiagnosticIsReported(string source, string expected)
    {
        // Given
        var builder = GeneratorTestContextBuilder.Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(HttpClient), typeof(Console), typeof(Enumerable), typeof(System.Net.Sockets.TcpClient), typeof(System.Timers.Timer))
            .AddSource("Presentation.cs", source);

        // When
        var results = await builder.GenerateAsync();

        // Then
        results.InputDiagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal))
            .Should().BeEmpty("a compiler error cannot prove a boundary diagnostic");
        results.AnalyzerResults[typeof(BoundaryAnalyzer)].Diagnostics.Should().Contain(diagnostic => diagnostic.Id == expected);
    }

    /// <summary>B-017, B-018 and B-022 leave bounded presentation deadlines, input projection and source descriptions intact.</summary>
    /// <param name="source">The synthetic compilation.</param>
    /// <returns>The completed analysis.</returns>
    [Theory]
    [ClassData(typeof(DashboardAllowedCases))]
    public async Task GivenAnAllowedPresentationOperation_WhenAnalyzed_ThenNoBoundaryDiagnosticIsReported(string source)
    {
        // Given
        var builder = GeneratorTestContextBuilder.Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(typeof(HttpClient), typeof(Console), typeof(Enumerable), typeof(System.Net.Sockets.TcpClient), typeof(System.Timers.Timer))
            .AddSource("Presentation.cs", source);

        // When
        var results = await builder.GenerateAsync();

        // Then
        results.InputDiagnostics
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal))
            .Should().BeEmpty();
        results.AnalyzerResults[typeof(BoundaryAnalyzer)].Diagnostics.Should().BeEmpty();
    }
}

/// <summary>Real reactive and DynamicData references prove the collection boundary rather than fixture signatures.</summary>
public class DashboardDataBoundaryTests
{
    /// <summary>B-017 and B-018 reject collection ownership operations in a presentation type.</summary>
    /// <param name="body">The operation to compile.</param>
    /// <param name="expected">The expected diagnostic.</param>
    /// <returns>The analysis.</returns>
    [Theory]
    [InlineData("cache.AddOrUpdate(\"row\");", "TRN0019")]
    [InlineData("cache.Edit(updater => updater.Clear());", "TRN0019")]
    [InlineData("cache.Connect().Filter(_ => true);", "TRN0020")]
    [InlineData("cache.Connect().Sort(Comparer<string>.Default);", "TRN0020")]
    [InlineData("cache.Connect().Group(item => item[0]);", "TRN0020")]
    [InlineData("cache.ExpireAfter(_ => TimeSpan.FromSeconds(10), scheduler: scheduler);", "TRN0020")]
    public async Task GivenARealCacheOperation_WhenUsedByPresentation_ThenItsBoundaryIsReported(string body, string expected)
    {
        // Given
        var source = Source(body);
        var builder = GeneratorTestContextBuilder.Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(
                typeof(DynamicData.SourceCache<,>),
                typeof(System.Reactive.Linq.Observable),
                typeof(System.Collections.ObjectModel.ReadOnlyObservableCollection<>),
                typeof(Enumerable))
            .AddSource("RealData.cs", source);

        // When
        var results = await builder.GenerateAsync();

        // Then
        results.InputDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
            && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal)).Should().BeEmpty();
        results.AnalyzerResults[typeof(BoundaryAnalyzer)].Diagnostics.Should().Contain(diagnostic => diagnostic.Id == expected);
    }

    /// <summary>B-018: binding with the pipeline's comparer is presentation rather than deriving fleet order.</summary>
    /// <returns>The analysis.</returns>
    [Fact]
    public async Task GivenSortAndBind_WhenUsedByPresentation_ThenNoBoundaryIsReported()
    {
        // Given
        var builder = GeneratorTestContextBuilder.Create()
            .WithAnalyzer<BoundaryAnalyzer>()
            .WithDiagnosticSeverity(DiagnosticSeverity.Error)
            .AddReferences(
                typeof(DynamicData.SourceCache<,>),
                typeof(System.Reactive.Linq.Observable),
                typeof(System.Collections.ObjectModel.ReadOnlyObservableCollection<>),
                typeof(Enumerable))
            .AddSource("RealData.cs", Source(
                "fleet.SortAndBind(out var rows, Comparer<string>.Default).Subscribe();",
                "IObservable<IChangeSet<string, string>> fleet"));

        // When
        var results = await builder.GenerateAsync();

        // Then
        results.InputDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
            && diagnostic.Id.StartsWith("CS", StringComparison.Ordinal)).Should().BeEmpty();
        results.AnalyzerResults[typeof(BoundaryAnalyzer)].Diagnostics.Should().BeEmpty();
    }

    private static string Source(string body, string input = "SourceCache<string, string> cache") => """
        using System;
        using System.Collections.Generic;
        using System.Reactive.Linq;
        using System.Reactive.Concurrency;
        using DynamicData;
        namespace ReactiveMarbles.Mvvm { public class RxObject { } }
        namespace Transporter.Features.Fleet.Presentation
        {
            class Surface : ReactiveMarbles.Mvvm.RxObject
            {
                void Run(
        """ + input + """
        , IScheduler scheduler)
                {
        """ + body + " } } }";
}

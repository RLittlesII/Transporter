using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Transporter.Analyzers;

/// <summary>Enforces fleet-dashboard presentation boundaries without depending on UI or reactive assemblies.</summary>
internal static class DashboardBoundaryRules
{
    /// <summary>Registers semantic checks for presentation operations and concrete-vehicle tests.</summary>
    /// <param name="start">The compilation registrations.</param>
    internal static void Register(CompilationStartAnalysisContext start)
    {
        // Airframe RSA1007 cannot name Roslyn's syntax registrations (Airframe#403).
#pragma warning disable RSA1007
        start.RegisterSyntaxNodeAction(AnalyzeType, SyntaxKind.IdentifierName, SyntaxKind.GenericName);
        start.RegisterSyntaxNodeAction(AnalyzeCall, SyntaxKind.InvocationExpression);
        start.RegisterSyntaxNodeAction(AnalyzeMember, SyntaxKind.SimpleMemberAccessExpression);
        start.RegisterSyntaxNodeAction(AnalyzeAge, SyntaxKind.SubtractExpression);
        start.RegisterSyntaxNodeAction(
            AnalyzeVehicleTest,
            SyntaxKind.CastExpression,
            SyntaxKind.AsExpression,
            SyntaxKind.IsExpression,
            SyntaxKind.DeclarationPattern,
            SyntaxKind.TypePattern,
            SyntaxKind.RecursivePattern);
#pragma warning restore RSA1007
    }

    /// <summary>Identifies a view or reactive view model by its base types rather than its filename.</summary>
    /// <param name="symbol">The type or member to classify.</param>
    /// <returns>Whether the symbol belongs to presentation.</returns>
    internal static bool IsPresentation(ISymbol symbol)
    {
        for (var type = symbol as INamedTypeSymbol ?? symbol.ContainingType; type is not null; type = type.BaseType)
        {
            var ns = type.ContainingNamespace.ToDisplayString();
            if ((type.Name == "RxObject" && ns.StartsWith("ReactiveMarbles.", StringComparison.Ordinal))
                || (ns == "Microsoft.Maui.Controls" && type.Name is "Element" or "VisualElement" or "ContentPage" or "ContentView"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool InPresentation(SyntaxNodeAnalysisContext context) =>
        context.SemanticModel.GetEnclosingSymbol(context.Node.SpanStart, context.CancellationToken) is { } enclosing
        && !Layers.IsTest(enclosing)
        && !Layers.IsCompositionRoot(enclosing)
        && IsPresentation(enclosing);

    private static void AnalyzeType(SyntaxNodeAnalysisContext context)
    {
        if (!InPresentation(context)
            || context.Node is IdentifierNameSyntax { IsVar: true }
            || context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is not INamedTypeSymbol type)
        {
            return;
        }

        var ns = type.ContainingNamespace.ToDisplayString();
        if ((ns == "System.Net.Http" && type.Name is "HttpClient" or "HttpMessageInvoker")
            || (ns == "System.Net.Sockets" && type.Name is "Socket" or "TcpClient" or "UdpClient")
            || (ns == "System.Net.WebSockets" && type.Name is "WebSocket" or "ClientWebSocket")
            || (type.Name is "Timer" or "PeriodicTimer" && ns is "System.Threading" or "System.Timers")
            || (ns == "Microsoft.Maui.Dispatching" && type.Name == "IDispatcherTimer"))
        {
            Report(context, Diagnostics.PresentationOwnsTransportOrTime);
        }
    }

    private static void AnalyzeCall(SyntaxNodeAnalysisContext context)
    {
        if (!InPresentation(context)
            || context.Node is not InvocationExpressionSyntax invocation
            || context.SemanticModel.GetOperation(invocation, context.CancellationToken) is not IInvocationOperation operation)
        {
            return;
        }

        var method = operation.TargetMethod;
        var owner = method.ContainingType;
        var ns = owner.ContainingNamespace.ToDisplayString();
        if ((ns == "System.Threading.Tasks" && method.Name is "Wait" or "WaitAll" or "WaitAny" or "Delay")
            || (ns == "System.Runtime.CompilerServices" && method.Name == "GetResult")
            || (ns == "System.Threading" && method.Name is "Sleep" or "WaitOne" or "WaitAll" or "WaitAny")
            || (method.Name == "Ask" && ns.StartsWith("Akka.Actor", StringComparison.Ordinal) && !HasTimeout(operation))
            || (ns == "System.Reactive.Linq" && method.Name == "Interval")
            || (ns == "System.Reactive.Linq" && method.Name == "Timer" && !IsPresentationDeadline(context, invocation, operation))
            || (ns.StartsWith("Microsoft.Maui", StringComparison.Ordinal) && method.Name is "CreateTimer" or "StartTimer")
            || IsCacheWrite(method))
        {
            Report(context, Diagnostics.PresentationOwnsTransportOrTime);
        }

        if ((ns == "System.Linq" && method.Name is "Where" or "OrderBy" or "OrderByDescending" or "ThenBy" or "ThenByDescending" or "GroupBy")
            || (ns.StartsWith("DynamicData", StringComparison.Ordinal) && method.Name is "Filter" or "Sort" or "Group" or "GroupOn" or "ExpireAfter")
            || (ns == "System.Collections.Generic" && method.Name == "Sort")
            || (owner.SpecialType == SpecialType.System_Array && method.Name == "Sort"))
        {
            Report(context, Diagnostics.PresentationDerivesFleetState);
        }
    }

    private static bool HasTimeout(IInvocationOperation invocation) =>
        invocation.Arguments.Any(static argument =>
            argument.ArgumentKind == ArgumentKind.Explicit
            && (argument.Parameter?.Type.Name == "TimeSpan"
                || argument.Parameter?.Type is INamedTypeSymbol { Name: "Nullable", TypeArguments.Length: 1 } nullable
                && nullable.TypeArguments[0].Name == "TimeSpan")
            && !(argument.Value.ConstantValue.HasValue && argument.Value.ConstantValue.Value is null));

    private static bool IsCacheWrite(IMethodSymbol method)
    {
        var original = method.ReducedFrom ?? method;

        return original.Name is "Edit" or "AddOrUpdate" or "Remove" or "RemoveKey" or "Clear" or "Refresh"
            && (Layers.IsCache(original.ContainingType)
                || original.IsExtensionMethod && original.Parameters.Length != 0 && Layers.IsCache(original.Parameters[0].Type));
    }

    private static bool IsPresentationDeadline(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax timer,
        IInvocationOperation operation)
    {
        if (operation.Arguments.Count(static argument => argument.ArgumentKind == ArgumentKind.Explicit) != 2
            || operation.Arguments.Last().Value.Type?.Name != "IScheduler"
            || !InjectedScheduler(context, operation.Arguments.Last().Syntax, 0)
            || timer.Parent is not MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax select }
            || context.SemanticModel.GetSymbolInfo(select, context.CancellationToken).Symbol is not IMethodSymbol { Name: "Select" } method
            || method.ContainingNamespace.ToDisplayString() != "System.Reactive.Linq")
        {
            return false;
        }

        var lambda = select.ArgumentList.Arguments.LastOrDefault()?.Expression as LambdaExpressionSyntax;

        return lambda?.Body is ExpressionSyntax expression
            && context.SemanticModel.GetConstantValue(expression, context.CancellationToken) is { HasValue: true, Value: false }
            && PresentationFlow(context, select, new HashSet<ISymbol>(SymbolEqualityComparer.Default), 0);
    }

    // Follow the projected deadline through fluent operators, argument positions and local aliases.
    // Only the presentation wiring used by the indicator is allowed; arbitrary callbacks are not.
    private static bool PresentationFlow(
        SyntaxNodeAnalysisContext context,
        ExpressionSyntax flow,
        HashSet<ISymbol> visited,
        int depth)
    {
        if (depth > 32)
        {
            return false;
        }

        InvocationExpressionSyntax? next = flow.Parent switch
        {
            MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax call } => call,
            ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax call } } => call,
            _ => null,
        };
        if (next is not null)
        {
            if (context.SemanticModel.GetSymbolInfo(next, context.CancellationToken).Symbol is not IMethodSymbol method)
            {
                return false;
            }

            var ns = method.ContainingNamespace.ToDisplayString();
            var reactive = ns is "System.Reactive.Linq" || ns.StartsWith("ReactiveMarbles.", StringComparison.Ordinal)
                || ns == "System" && method.ContainingType.Name is "ObservableExtensions" or "IObservable";
            if (!reactive || method.Name is not ("Merge" or "Take" or "StartWith" or "Switch" or "ObserveOn" or "AsValue" or "Subscribe"))
            {
                return false;
            }

            if (method.Name == "Subscribe" && method.Parameters.Any(static parameter => parameter.Type.TypeKind == TypeKind.Delegate))
            {
                return false;
            }

            foreach (var argument in next.ArgumentList.Arguments)
            {
                if (argument.Expression is not LambdaExpressionSyntax callback)
                {
                    if (context.SemanticModel.GetTypeInfo(argument.Expression, context.CancellationToken).ConvertedType?.TypeKind == TypeKind.Delegate)
                    {
                        return false;
                    }

                    continue;
                }

                // Only property notifications and the constant initial false are presentation callbacks.
                if (method.Name != "AsValue" || !PresentationCallback(context, callback))
                {
                    return false;
                }
            }

            return method.Name is "Subscribe" or "AsValue" || PresentationFlow(context, next, visited, depth + 1);
        }

        if (flow.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable }
            && context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken) is ILocalSymbol local)
        {
            if (!visited.Add(local))
            {
                return true;
            }

            var scope = variable.Ancestors().FirstOrDefault(static ancestor => ancestor is BaseMethodDeclarationSyntax);

            return scope is not null && scope.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(identifier => SymbolEqualityComparer.Default.Equals(
                    context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol, local))
                .All(identifier => PresentationFlow(context, identifier, visited, depth + 1));
        }

        if (flow.Parent is LambdaExpressionSyntax
            {
                Parent: ArgumentSyntax
                {
                    Parent: ArgumentListSyntax
                    { Parent: InvocationExpressionSyntax outer }
                }
            }
            && context.SemanticModel.GetSymbolInfo(outer, context.CancellationToken).Symbol is IMethodSymbol { Name: "Select" } select
            && select.ContainingNamespace.ToDisplayString() == "System.Reactive.Linq")
        {
            return PresentationFlow(context, outer, visited, depth + 1);
        }

        return flow.Parent is ExpressionStatementSyntax;
    }

    private static bool PresentationCallback(SyntaxNodeAnalysisContext context, LambdaExpressionSyntax callback)
    {
        if (callback.Body is BlockSyntax block)
        {
            return block.Statements.All(statement => statement is ExpressionStatementSyntax expression
                && PropertyNotification(context, expression.Expression));
        }

        return callback.Body is ExpressionSyntax value
            && (context.SemanticModel.GetConstantValue(value, context.CancellationToken) is { HasValue: true, Value: false }
                || PropertyNotification(context, value));
    }

    private static bool PropertyNotification(SyntaxNodeAnalysisContext context, ExpressionSyntax expression) =>
        expression is InvocationExpressionSyntax invocation
        && context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol { Name: "RaisePropertyChanged" }
        && invocation.ArgumentList.Arguments.All(static argument => argument.Expression is LiteralExpressionSyntax
            or InvocationExpressionSyntax { Expression: IdentifierNameSyntax { Identifier.ValueText: "nameof" } });

    private static bool InjectedScheduler(SyntaxNodeAnalysisContext context, SyntaxNode expression, int depth)
    {
        if (depth > 4)
        {
            return false;
        }

        if (expression is ArgumentSyntax argument)
        {
            return InjectedScheduler(context, argument.Expression, depth + 1);
        }

        if (expression is MemberAccessExpressionSyntax member)
        {
            return InjectedScheduler(context, member.Expression, depth + 1);
        }

        var symbol = context.SemanticModel.GetSymbolInfo(expression, context.CancellationToken).Symbol;
        if (symbol is IParameterSymbol parameter)
        {
            return parameter.Type.Name is "IScheduler" or "ISchedulerProvider";
        }

        if (symbol is ILocalSymbol local
            && local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken)
                is VariableDeclaratorSyntax { Initializer: { } initializer })
        {
            return InjectedScheduler(context, initializer.Value, depth + 1);
        }

        return false;
    }

    private static void AnalyzeMember(SyntaxNodeAnalysisContext context)
    {
        if (InPresentation(context)
            && context.SemanticModel.GetSymbolInfo(context.Node, context.CancellationToken).Symbol is IPropertySymbol { Name: "Result" } property
            && property.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks")
        {
            Report(context, Diagnostics.PresentationOwnsTransportOrTime);
        }
    }

    private static void AnalyzeAge(SyntaxNodeAnalysisContext context)
    {
        if (InPresentation(context)
            && context.Node is BinaryExpressionSyntax binary
            && context.SemanticModel.GetTypeInfo(binary.Left, context.CancellationToken).Type?.Name is "DateTime" or "DateTimeOffset"
            && context.SemanticModel.GetTypeInfo(binary.Right, context.CancellationToken).Type?.Name is "DateTime" or "DateTimeOffset")
        {
            Report(context, Diagnostics.PresentationDerivesFleetState);
        }
    }

    private static void AnalyzeVehicleTest(SyntaxNodeAnalysisContext context)
    {
        var enclosing = context.SemanticModel.GetEnclosingSymbol(context.Node.SpanStart, context.CancellationToken);
        if (enclosing is null || Layers.IsTest(enclosing))
        {
            return;
        }

        var ns = enclosing.ContainingNamespace.ToDisplayString();
        if (ns != "Transporter.Features.Fleet" && !ns.StartsWith("Transporter.Features.Fleet.", StringComparison.Ordinal) && !IsPresentation(enclosing))
        {
            return;
        }

        var syntax = context.Node switch
        {
            CastExpressionSyntax cast => cast.Type,
            BinaryExpressionSyntax binary => binary.Right as TypeSyntax,
            DeclarationPatternSyntax pattern => pattern.Type,
            TypePatternSyntax pattern => pattern.Type,
            RecursivePatternSyntax pattern => pattern.Type,
            _ => null,
        };
        if (syntax is null || context.SemanticModel.GetTypeInfo(syntax, context.CancellationToken).Type is not INamedTypeSymbol candidate)
        {
            return;
        }

        for (var parent = candidate.BaseType; parent is not null; parent = parent.BaseType)
        {
            if (parent.ToDisplayString() == "Transporter.Model.TransportVehicle")
            {
                Report(context, Diagnostics.PresentationNamesConcreteVehicle);

                return;
            }
        }
    }

    private static void Report(SyntaxNodeAnalysisContext context, DiagnosticDescriptor rule) =>
        context.ReportDiagnostic(Diagnostic.Create(rule, context.Node.GetLocation()));
}

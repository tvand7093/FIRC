using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ForceInheritance
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ForceInheritanceAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "ControllerInheritance";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = "Invalid Parent Class";
        private static readonly LocalizableString MessageFormat = "{0} does not inherit from the {1} class.";
        private static readonly LocalizableString Description = "All controllers must inherit from an AgencyRM _BaseController";
        private const string Category = "Inheritance";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat,
            Category, DiagnosticSeverity.Error,
            isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            var node = context.Node as ClassDeclarationSyntax;
            var classNameNode = node.ChildTokens().FirstOrDefault(t => t.IsKind(SyntaxKind.IdentifierToken));
            if (classNameNode == null) return;

            var className = classNameNode.ValueText;

            if (node.BaseList.Types.Count == 0) return;

            var parentNameNode = node.BaseList.Types.FirstOrDefault(t => t.IsKind(SyntaxKind.IdentifierToken));
            if (parentNameNode == null) return;

            //TODO: find a way to get the Parent Controllers name.
            //var parentClass = parentNameNode as IdentifierNameSyntax;
            //if (parentClass == null) return;
            
            //parentClass

        }
    }
}

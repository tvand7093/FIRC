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
        private static readonly LocalizableString MessageFormat = "{0} does not inherit from a valid {1} Controller.";
        private static readonly LocalizableString Description = "All controllers must inherit from an AgencyRM Base Controller";
        private const string Category = "Inheritance";

        private static DiagnosticDescriptor Rule = new DiagnosticDescriptor(
            DiagnosticId, Title, MessageFormat,
            Category, DiagnosticSeverity.Error,
            isEnabledByDefault: true, description: Description);

        private const string ApiFolder = "apicontrollers/";
        private const string ControllersFolder = "controllers/";
        private const string ApiClassName = "apicontroller";
        private const string ControllerClassName = "controller";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
        }

        /// <summary>
        /// Gets the class name that is currently being inspected.
        /// </summary>
        /// <param name="node">The current class decleration</param>
        /// <returns>The name of the class.</returns>
        private static string GetImplementorName(ClassDeclarationSyntax node)
        {
            //get the class that is currently delcared
            var classNameNode = node.ChildTokens().FirstOrDefault(t => t.IsKind(SyntaxKind.IdentifierToken));
            if (classNameNode == null) return null;

            //get the actual name if we have one.
            return classNameNode.ValueText;
        }

        /// <summary>
        /// Gets the current parent for the class decleration.
        /// </summary>
        /// <param name="node">The current class decleration to inspect.</param>
        /// <returns>The parent class simple name node.</returns>
        private static SimpleNameSyntax GetParent(ClassDeclarationSyntax node)
        {
            var parentNameNode = node.BaseList?.Types.FirstOrDefault(t => t.IsKind(SyntaxKind.SimpleBaseType));
            //Try to get the parent class
            return parentNameNode?.Type as SimpleNameSyntax;
        }

        /// <summary>
        /// Determines what folder the current file belongs too.
        /// </summary>
        /// <param name="node">The root node to get the full file path from.</param>
        /// <returns>a string name of the directory followed by a forward slash. This will
        /// return null if the file isn't within access (external library) or if the path couldn't be determined.
        /// </returns>
        private static string GetSourceDirectory(SyntaxNode node)
        {
            try
            {
                var path = node.SyntaxTree.FilePath;
                var uri = new Uri(path);
                var dir = uri.Segments[uri.Segments.Count() - 2].ToLower();
                if (dir != ControllersFolder && dir != ApiFolder)
                {
                    return null;
                }
                return dir;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Analyzes the current node. This is the main method that gets called.
        /// All inspection and detection of context will happen in this 
        /// method or supporting methods.
        /// </summary>
        /// <param name="context">The current context with respect to the current Node.</param>
        private static void AnalyzeNode(SyntaxNodeAnalysisContext context)
        {
            //get directory. Bail if one couldn't be determined.
            var dir = GetSourceDirectory(context.Node);
            if (dir == null) return;

            var node = context.Node as ClassDeclarationSyntax;

            //get the current class name.
            var className = GetImplementorName(node);

            //bail if it doesn't inherit anything or the class name couldn't be determined.
            if (className == null || node.BaseList?.Types.Count == 0) return;

            //now get the parent class if one exists.
            var parentClass = GetParent(node);

            //we have a parent or it is null, so get try to get the name.
            var parentClassName = parentClass?.Identifier.ValueText.ToLower();

            //detect the parent class name. Specifically if it starts with _Base or any variation
            //of _BASE. This is forced as a coding standard.
            var isBase = parentClassName?.StartsWith("_");
            var hasBaseParent = isBase.HasValue ? isBase.Value : false;

            Diagnostic diagnostic = null;
            //get the child token 
            var child = node.ChildTokens().FirstOrDefault(t => t.IsKind(SyntaxKind.IdentifierToken));

            if (!hasBaseParent && dir == ControllersFolder)
            {
                //this is a class that needs a parent class. It resides in the Controllers folder
                //so we prompt for a regular _BaseController of some type.
                diagnostic =
                    Diagnostic.Create(Rule,
                        child.GetLocation(),
                        className, "base");
            }

            if(!hasBaseParent && dir == ApiFolder)
            {
                //this is a class that needs a parent class. It resides in the Controllers/API folder
                //so we prompt for a regular _BaseAPIController of some type.
                diagnostic =
                    Diagnostic.Create(Rule,
                        child.GetLocation(),
                        className, "Api base");
            }
            
            if((parentClassName == ApiClassName || parentClassName == ControllerClassName)
                && className.StartsWith("_"))
            {
                //means we can allow this, so remove the previous diagnostic
                diagnostic = null;
            }


            //if any issue was detected, prompt now.
            if (diagnostic != null) context.ReportDiagnostic(diagnostic);
        }
    }
}

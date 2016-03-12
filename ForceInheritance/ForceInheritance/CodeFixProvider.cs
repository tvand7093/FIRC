using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;

namespace ForceInheritance
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ForceInheritanceCodeFixProvider)), Shared]
    public class ForceInheritanceCodeFixProvider : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds
        {
            get { return ImmutableArray.Create(ForceInheritanceAnalyzer.DiagnosticId); }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {           
            //get all documents in same folder as the current source
            var potentialSuggestions = context.Document.Project.Documents
                .Where(d => d.Folders == context.Document.Folders);

            //loop over documents and look for appropriate base classes to use
            foreach (var suggestion in potentialSuggestions)
            {
                //get the tree for each class file
                var tree = await suggestion.GetSyntaxRootAsync(context.CancellationToken);

                //search the tree for each file for a class that begins with _base in the class name.
                var declerations = from n in tree.DescendantNodes()
                                   where n.IsKind(SyntaxKind.ClassDeclaration)
                                   where (n as ClassDeclarationSyntax).Identifier
                                        .ValueText.ToLower().StartsWith("_base")
                                   select n as ClassDeclarationSyntax;

                //for every _base type class, add a suggestion.
                foreach (var decleration in declerations)
                {
                    var className = decleration.Identifier.ValueText;
                    var title = $"Inherit from {className}";
                    // Register a code action that will invoke the fix.
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: title,
                            createChangedDocument: c => ConvertToBaseController(
                                context.Document, context.Diagnostics, className, c),
                            equivalenceKey: title),
                        context.Diagnostics.First());
                }
                
            }

        }

        /// <summary>
        /// Generates the new class decleration for replacing an old parent class
        /// </summary>
        /// <param name="toReplace">The original decleration to be replaced.</param>
        /// <param name="newParentClass">The name of the class to replace the existing parent with.</param>
        /// <returns>The new decleration for adding into a syntax tree.</returns>
        private ClassDeclarationSyntax CreateNewClassDecleration(ClassDeclarationSyntax toReplace, string newParentClass)
        {
            //create the new class type to be the parent based on the passed in name.
            var identifier = SyntaxFactory.IdentifierName(newParentClass);
            var simpleBaseType = SyntaxFactory.SimpleBaseType(identifier);

            // Get the symbol representing the type to be renamed.
            var toRemove = toReplace.BaseList?.Types.FirstOrDefault(t => t.IsKind(SyntaxKind.SimpleBaseType));

            if(toRemove == null)
            {
                //insert new parent class
                //create new base list
                var newBaseList = SyntaxFactory.BaseList();
                
                //add the new parent to the base list
                var updated = newBaseList.Types.Add(simpleBaseType);

                //update the types to the final list
                var finalBaseList = newBaseList.WithoutLeadingTrivia().WithTypes(updated);

                //add whitespace before colon
                var preSpace = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ");

                //append space between class name and colon
                var newNode = toReplace.ChildTokens().FirstOrDefault(t => t.IsKind(SyntaxKind.IdentifierToken));

                //trickle up the changes
                return toReplace.ReplaceToken(newNode, newNode.WithTrailingTrivia(preSpace)).WithBaseList(finalBaseList);
            }
            else
            {
                var preSpace = SyntaxFactory.SyntaxTrivia(SyntaxKind.WhitespaceTrivia, " ");

                //insert new parent class with specified one. 
                var updated = toReplace.BaseList.Types.Insert(0, simpleBaseType.WithTrailingTrivia(preSpace));

                //trickle up the changes
                var newList = toReplace.BaseList.WithTypes(updated);

                //return the final class decleration that uses our new class.
                return toReplace.WithBaseList(newList);
            }
        }

        /// <summary>
        /// Converts an existing class decleration node to use a different base class.
        /// </summary>
        /// <param name="document">The original document being modified.</param>
        /// <param name="diagnostics">The diagnostic records of the request.</param>
        /// <param name="newClass">The name of the parent class to replace the existing with.</param>
        /// <param name="cancellationToken">The systems cancellation token.</param>
        /// <returns></returns>
        private async Task<Document> ConvertToBaseController(Document document,
            ImmutableArray<Diagnostic> diagnostics,
            string newClass,
            CancellationToken cancellationToken)
        {
            //get the root of our syntax tree.
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

            //get the first diagnostic message so we know where to start our search
            var diagnostic = diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            // Find the type declaration identified by the diagnostic.
            var oldClassDecleration = root.FindToken(diagnosticSpan.Start).Parent
                .AncestorsAndSelf()
                .OfType<TypeDeclarationSyntax>()
                .First() as ClassDeclarationSyntax;

            //create the new class decleration
            var newClassDecleration = CreateNewClassDecleration(oldClassDecleration, newClass);

            //create the new root node of our syntax tree.
            var newRoot = root.ReplaceNode(oldClassDecleration, newClassDecleration);
            
            //replace and commit our change to the document.
            return document.WithSyntaxRoot(newRoot);
        }
    }
}
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
using System.Text;

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

        private string ParseQualifiedNamespace(SyntaxNode toParse)
        {
            var beginNamespace = toParse as QualifiedNameSyntax;
            if (beginNamespace == null)
            {
                var node = toParse as IdentifierNameSyntax;
                //no left, so it is just a namespace like: using System;
                return node.Identifier.ValueText;
               // parsed.Add(node.Identifier.ValueText);
            }

            QualifiedNameSyntax child = beginNamespace;
            NameSyntax current = null;

            while (child != null)
            {
                current = child;
                child = child.Left as QualifiedNameSyntax;
            }
            StringBuilder fullName = new StringBuilder();
            //found bottom of tree, so grab start ns and parse upwards

            var bottom = current as QualifiedNameSyntax;

            var startOfNamespace = (bottom.Left as IdentifierNameSyntax).Identifier.ValueText;
            var secondPartOfNamespace = bottom.Right.Identifier.ValueText;

            fullName.Append($"{startOfNamespace}.{secondPartOfNamespace}");

            QualifiedNameSyntax parent = bottom.Parent as QualifiedNameSyntax;
            //recurse upwards
            while (parent != null)
            {
                //append next section of namespace plus the trivia on left side.
                fullName.Append($".{parent.Right.Identifier.ValueText}");
                parent = parent.Parent as QualifiedNameSyntax;
            }
            return fullName.ToString();
        }

        private HashSet<string> ParseNamespaces(IEnumerable<UsingDirectiveSyntax> namespaces)
        {
            HashSet<string> parsed = new HashSet<string>();

            foreach (var ns in namespaces)
            {
                var fullName = new StringBuilder();
                var firstNode = ns.ChildNodes().ElementAt(0);
                //var beginNamespace = firstNode as QualifiedNameSyntax;
                //if (beginNamespace == null)
                //{
                //    var node = firstNode as IdentifierNameSyntax;
                //    //no left, so it is just a namespace like: using System;
                //    parsed.Add(node.Identifier.ValueText);
                //    continue;
                //}

                //QualifiedNameSyntax child = beginNamespace;
                //NameSyntax current = null;

                //while (child != null)
                //{
                //    child = child.Left as QualifiedNameSyntax;
                //    current = child;
                //}

                ////found bottom of tree, so grab start ns and parse upwards
                //var startOfNamespace = (current as IdentifierNameSyntax).Identifier.ValueText;
                //fullName.Append(startOfNamespace);

                //QualifiedNameSyntax parent = null;
                ////recurse upwards
                //while (parent != null)
                //{
                //    //append next section of namespace plus the trivia on left side.
                //    fullName.Append($".{parent.Right.Identifier.ValueText}");
                //    parent = parent.Parent as QualifiedNameSyntax;
                //}
                parsed.Add(ParseQualifiedNamespace(firstNode));
                //done parsing, so return insert into hash table.
                //parsed.Add(fullName.ToString());
            }
            return parsed;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            //get all namespaces to see if the Controller or ApiController classes exist and need to be in here.
            var documentRoot = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
            var namespaces = documentRoot.ChildNodes()
                .Where(n => n.IsKind(SyntaxKind.UsingDirective))
                .Cast<UsingDirectiveSyntax>();

            var parsedNamespaces = ParseNamespaces(namespaces);

            //documentRoot.na

            //get all documents in same folder as the current source
            var potentialSuggestions = context.Document.Project.Documents
                .Where(d => d.Folders == context.Document.Folders);

            //loop over documents and look for appropriate base classes to use
            foreach (var suggestion in potentialSuggestions)
            {
                //get the tree for each class file
                var tree = await suggestion.GetSyntaxRootAsync(context.CancellationToken);
                var error = context.Diagnostics.First();

                //search the tree for each file for a class that begins with _base in the class name.
                var declerations = from n in tree.DescendantNodes()
                                   where n.IsKind(SyntaxKind.ClassDeclaration)
                                   where (n as ClassDeclarationSyntax).Identifier
                                        .ValueText.StartsWith("_")
                                   where (n as ClassDeclarationSyntax).Identifier.SpanStart
                                        != error.Location.SourceSpan.Start
                                   select n as ClassDeclarationSyntax;

                //for every _base type class, add a suggestion.
                foreach (var decleration in declerations)
                {
                    var className = decleration.Identifier.ValueText;
                    var title = $"Inherit from {className}";
                    //get the class namespace
                    var classNamespace = decleration.FirstAncestorOrSelf<NamespaceDeclarationSyntax>(n => n.IsKind(SyntaxKind.NamespaceDeclaration));
                    var namespaceName = classNamespace.ChildNodes();

                    var fullyQualifiedNamespace = ParseQualifiedNamespace(namespaceName.ElementAt(0));


                    if (parsedNamespaces.Contains(fullyQualifiedNamespace))
                    {
                        //set namespace to null. This means it is already imported so we don't need to do anything else.
                        fullyQualifiedNamespace = null;
                    }

                    // Register a code action that will invoke the fix.
                    context.RegisterCodeFix(
                        CodeAction.Create(
                            title: title,
                            createChangedDocument: c => ConvertToBaseController(
                                context.Document, context.Diagnostics, fullyQualifiedNamespace, className, c),
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

        private CompilationUnitSyntax CreateNewUsingStatement(CompilationUnitSyntax root, string toInsert)
        {

            var sections = toInsert.Split('.');
            QualifiedNameSyntax toAttach = null;

            if (sections.Length == 1)
            {
                //only insert identifier name
                return root.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.IdentifierName(toInsert)));
            }

            for (int i = 0; i < sections.Length; i++)
            {
                

                if(toAttach == null)
                {
                    //attach like the folowing: System.Net
                    toAttach = SyntaxFactory.QualifiedName(
                        SyntaxFactory.IdentifierName(sections[i]),
                        SyntaxFactory.IdentifierName(sections[++i]))
                        ;
                }
                else
                {
                    //attach 'toAttach' to an existing node as the left node
                    toAttach = SyntaxFactory.QualifiedName(
                        toAttach,
                        SyntaxFactory.IdentifierName(sections[i])).WithLeadingTrivia(SyntaxFactory.Space);
                }
            }

            return root.AddUsings(SyntaxFactory.UsingDirective(toAttach));
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
            string fullyQualifiedNamespace,
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
                .FirstOrDefault() as ClassDeclarationSyntax;

            //create the new class decleration
            var newClassDecleration = CreateNewClassDecleration(oldClassDecleration, newClass);

            //create the new root node of our syntax tree.
            root = root.ReplaceNode(oldClassDecleration, newClassDecleration);

            //append using statement if necessary
            if (!String.IsNullOrEmpty(fullyQualifiedNamespace))
            {
                var comp = root as CompilationUnitSyntax;
                root = CreateNewUsingStatement(comp, fullyQualifiedNamespace);
            }

            //replace and commit our change to the document.
            return document.WithSyntaxRoot(root);
        }
    }
}
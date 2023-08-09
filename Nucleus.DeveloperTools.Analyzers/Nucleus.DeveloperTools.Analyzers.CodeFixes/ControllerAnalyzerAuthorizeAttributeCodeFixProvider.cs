using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Nucleus.DeveloperTools.Analyzers
{
  [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ControllerAnalyzerAuthorizeAttributeCodeFixProvider)), Shared]
  public class ControllerAnalyzerAuthorizeAttributeCodeFixProvider : CodeFixProvider
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds
    {
      get { return ImmutableArray.Create(DiagnosticMessages.CONTROLLER_ADMIN_NO_AUTHORIZE_ATTRIBUTE.Id); }
    }

    public sealed override FixAllProvider GetFixAllProvider()
    {
      // See https://github.com/dotnet/roslyn/blob/main/docs/analyzers/FixAllProvider.md for more information on Fix All Providers
      return WellKnownFixAllProviders.BatchFixer;
    }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
      var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

      Diagnostic diagnostic = context.Diagnostics.First();
      TextSpan diagnosticSpan = diagnostic.Location.SourceSpan;

      // Find the method declaration which triggered the diagnostic
      MethodDeclarationSyntax declaration = root.FindToken(diagnosticSpan.Start)
        .Parent.AncestorsAndSelf()
        .OfType<MethodDeclarationSyntax>()
        .FirstOrDefault();

      if (declaration != null)
      {
        // Register a code action that will invoke the fix.
        context.RegisterCodeFix
        (
          CodeAction.Create
          (
            title: CodeFixResources.NUCLEUS301_CODEFIXTITLE,
            createChangedDocument: cancellationToken => AddAuthorizeAttribute(context.Document, declaration, cancellationToken),
            equivalenceKey: nameof(CodeFixResources.NUCLEUS301_CODEFIXTITLE)
          ),
          diagnostic
        );
      }
    }

    /// <summary>
    /// Add an [Authorize] attribute to the method.
    /// </summary>
    /// <param name="document"></param>
    /// <param name="methodDeclaration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<Document> AddAuthorizeAttribute(Document document, MethodDeclarationSyntax methodDeclaration, CancellationToken cancellationToken)
    {      
      SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken);

      return document.WithSyntaxRoot
      (
        root.ReplaceNode
        (
          methodDeclaration,
          methodDeclaration.AddAttribute("Authorize", new string[] { "Policy=Nucleus.Abstractions.Authorization.Constants.MODULE_EDIT_POLICY" })
      //.WithTriviaFrom(methodDeclaration)
      )); 
    }    
  }
}

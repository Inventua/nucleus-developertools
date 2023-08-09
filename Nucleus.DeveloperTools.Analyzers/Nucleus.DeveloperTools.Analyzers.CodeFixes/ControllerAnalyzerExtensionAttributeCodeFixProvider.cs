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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Nucleus.DeveloperTools.Analyzers
{
  [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ControllerAnalyzerExtensionAttributeCodeFixProvider)), Shared]
  public class ControllerAnalyzerExtensionAttributeCodeFixProvider : CodeFixProvider
  {
    public sealed override ImmutableArray<string> FixableDiagnosticIds
    {
      get { return ImmutableArray.Create(DiagnosticMessages.CONTROLLER_NO_EXTENSION_ATTRIBUTE.Id); }
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
      
      // Find the class declaration which triggered the diagnostic
      ClassDeclarationSyntax declaration = root.FindToken(diagnosticSpan.Start)
        .Parent.AncestorsAndSelf()
        .OfType<ClassDeclarationSyntax>()
        .FirstOrDefault();

      if (declaration != null)
      {
        // Register a code action that will invoke the fix.
        context.RegisterCodeFix
        (
          CodeAction.Create
          (
            title: CodeFixResources.NUCLEUS300_CODEFIXTITLE,
            createChangedDocument: cancellationToken => AddExtensionAttribute(context.Document, declaration, cancellationToken),
            equivalenceKey: nameof(CodeFixResources.NUCLEUS300_CODEFIXTITLE)
          ),
          diagnostic
        );
      }
    }

    /// <summary>
    /// Add an [Extension] attribute to the class
    /// </summary>
    /// <param name="document"></param>
    /// <param name="classDeclaration"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<Document> AddExtensionAttribute(Document document, ClassDeclarationSyntax classDeclaration, CancellationToken cancellationToken)
    {
      SyntaxNode root = await document.GetSyntaxRootAsync(cancellationToken);        

      return document.WithSyntaxRoot
      (
        root.ReplaceNode
        (
          classDeclaration,
          classDeclaration.AddAttribute("Extension", new string[] { $"\"{await GetExtensionName(document, cancellationToken)}\"" })
            //.WithTriviaFrom(classDeclaration)
      ));
    }
    
    /// <summary>
    /// Find any "extension" element in the manifest and return its value.  If there are no extension elements, try a "component" element 
    /// folderName attribute.  If all else fails, return "your-extension-name".
    /// </summary>
    /// <param name="document"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<string> GetExtensionName(Document document, CancellationToken cancellationToken)
    {
      const string MANIFEST_FILENAME = "package.xml";
      XElement extensionElement = null;
      XElement componentElement = null;
      XAttribute folderAttribute = null;

      TextDocument packageFile = document.Project.AdditionalDocuments
        .Where(file => Path.GetFileName(file.Name).Equals(MANIFEST_FILENAME, StringComparison.OrdinalIgnoreCase))
        .FirstOrDefault();

      if (packageFile != null)
      {
        SourceText packageFileText = await packageFile.GetTextAsync(cancellationToken);

        // Write the additional file back to stream and load it into an XDocument.
        using (MemoryStream stream = new MemoryStream())
        {
          using (StreamWriter writer = new StreamWriter(stream, System.Text.Encoding.UTF8, 4096, true))
          {
            packageFileText.Write(writer, cancellationToken);
          }

          stream.Position = 0;
          XDocument manifest = XDocument.Load(stream, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);

          extensionElement = manifest.Descendants(manifest.Root.Name.Namespace + "extension").FirstOrDefault();
          componentElement = manifest.Descendants(manifest.Root.Name.Namespace + "component").FirstOrDefault();
          folderAttribute = componentElement.Attribute("folderName");

        }
      }

      return extensionElement?.Value ?? folderAttribute?.Value ?? "your-extension-name";
      //return extensionElement == null ? "your-extension-name" : extensionElement.Value;
    }
  }
}

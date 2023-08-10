using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Nucleus.DeveloperTools.Analyzers.Models;
using Nucleus.DeveloperTools.Analyzers.Nucleus.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Nucleus.DeveloperTools.Analyzers
{
  /// <summary>
  /// Analyzer for common Nucleus extension manifest (package.xml) file problems.
  /// </summary>
  [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
  public class ControllerAnalyzer : DiagnosticAnalyzer
  {
    private static string[] ADMIN_METHODS = { "Save", "Delete", "Remove" };

    public static readonly ImmutableArray<DiagnosticDescriptor> MESSAGES = ImmutableArray.Create
    (
      DiagnosticMessages.CONTROLLER_NO_EXTENSION_ATTRIBUTE,
      DiagnosticMessages.CONTROLLER_ADMIN_NO_AUTHORIZE_ATTRIBUTE
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
      get
      {
        return MESSAGES;
      }
    }

    public override void Initialize(AnalysisContext context)
    {
      context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
      context.EnableConcurrentExecution();

      context.RegisterSymbolAction(AnalyzeSymbols, SymbolKind.NamedType);
    }

    /// <summary>
    /// Analyze controller classes
    /// </summary>
    /// <param name="context"></param>
    private static void AnalyzeSymbols(SymbolAnalysisContext context)
    {
      INamedTypeSymbol symbol = context.Symbol as INamedTypeSymbol;
      if (symbol != null && symbol.DeclaredAccessibility.HasFlag(Accessibility.Public))
      {
        // report a warning if there are controller classes which don't have an [Extension] attribute.
        if (SymbolEqualityComparer.Default.Equals(symbol.BaseType, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Controller")))
        {
          if (!symbol.GetAttributes()
            .Where(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, context.Compilation.GetTypeByMetadataName("Nucleus.Abstractions.ExtensionAttribute")))
            .Any())
          {
            context.ReportDiagnostic
            (
              Diagnostic.Create
              (
                DiagnosticMessages.CONTROLLER_NO_EXTENSION_ATTRIBUTE,
                symbol.Locations.FirstOrDefault(),
                symbol.Name
              )
            );
          }
        }

        // report an information message if the class has a method generally associated with an "admin" action (save/delete),
        // and there is no [Authorize] attribute at the class level or on the method.
        if (!symbol.GetAttributes()
          .Where(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.AuthorizeAttribute")))
          .Any())
        {
          CheckMethodAuthentication(context, symbol, ADMIN_METHODS);
        }
      }
    }


    /// <summary>
    /// Report an information message if the specified method contains the specified value, is public and does not have 
    /// an [Authorize] attribute.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="classSymbol"></param>
    /// <param name="methodNamePart"></param>
    private static void CheckMethodAuthentication(SymbolAnalysisContext context, INamedTypeSymbol classSymbol, IEnumerable<string> methodNameParts)
    {
      // Check if a method exists which matches any of the "well known" method name parts and is public
      foreach (ISymbol methodSymbol in classSymbol.GetMembers()
        .Where(member => member.DeclaredAccessibility.HasFlag(Accessibility.Public))
        .Where(member => ContainsAny(member.Name, ADMIN_METHODS)))
      {
        // Check if the method has an attribute derived from HttpMethodAttribute (like [HttpPost])
        if (methodSymbol.GetAttributes()
            .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass.BaseType, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute")))
            .Any())
        {
          // Check if the method does not have an [Authorize] attribute
          if (!methodSymbol.GetAttributes()
            .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.AuthorizeAttribute")))
            .Any())
          {
            context.ReportDiagnostic
            (
              Diagnostic.Create
              (
                DiagnosticMessages.CONTROLLER_ADMIN_NO_AUTHORIZE_ATTRIBUTE,
                methodSymbol.Locations.FirstOrDefault(),
                classSymbol.Name,
                methodSymbol.Name
              )
            );
          }
        }
      }
    }

    private static Boolean ContainsAny(string part, IEnumerable<string> values)
    {
      foreach (string value in values)
      {
        // we have to use .IndexOf because .NET standard 2.0 doesn't have a String.Contains(string, StringComparison) overload.
        if (part.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
        {
          return true;
        }
      }
      return false;
    }
  }
}


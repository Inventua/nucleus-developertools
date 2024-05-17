using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.CodeAnalysis.Shared.Extensions;
using System.Security.AccessControl;

//https://github.com/dotnet/roslyn/blob/0c933d5b15cf01be78814cac04c6259da63be480/src/Workspaces/SharedUtilitiesAndExtensions/Compiler/Core/Extensions/ITypeSymbolExtensions.cs#L112

namespace Nucleus.DeveloperTools.Analyzers
{
  /// <summary>
  /// Analyzer for common Nucleus controller class problems.
  /// </summary>
  [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
  public class ControllerAnalyzer : DiagnosticAnalyzer
  {
    private static readonly string[] ADMIN_METHODS = { "Save", "Delete", "Remove", "Update", "Store" };

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
      if (context.Symbol is INamedTypeSymbol symbol && symbol.DeclaredAccessibility.HasFlag(Accessibility.Public) && !symbol.IsAbstract)
      {
        if (IsNucleusExtension(context.Symbol.Locations))
        {
          // run Analyzers for controller classes.
          //if (SymbolEqualityComparer.Default.Equals(symbol.BaseType, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Controller")))
          if (InheritsOrIsType(symbol.BaseType, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Controller")))            
          {
            CheckExtension(context, symbol);

            // report an information message if the class has a method generally associated with an "admin" action (save/delete),
            // and there is no [Authorize] attribute at the class level or on the method.
            CheckMethodAuthentication(context, symbol);
          }
        }
      }
    }

    /// <summary>
    /// Report a warning message if the specified class does not have an [Extension] attribute, and does not have an [ApiController] or [Route] attribute.
    /// </summary>
    /// <param name="context"></param>
    /// <param name="classSymbol"></param>
    private static void CheckExtension(SymbolAnalysisContext context, INamedTypeSymbol classSymbol)
    {
      ImmutableArray<AttributeData> symbolAttributes = classSymbol.GetAttributes();
      //.Where(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, context.Compilation.GetTypeByMetadataName("Nucleus.Abstractions.ExtensionAttribute")))
      
      //if (!symbolAttributes
      //  .Where(attr => attr.AttributeClass.FindImplementationForInterfaceMember(context.Compilation.GetTypeByMetadataName("Nucleus.Abstractions.ExtensionAttribute")) != null)        
      //  .Any())
      if (!symbolAttributes
        .Where(attr => InheritsOrIsType(attr.AttributeClass,context.Compilation.GetTypeByMetadataName("Nucleus.Abstractions.ExtensionAttribute")))
        .Any())
      {
        // don't display a warning if the controller has an [ApiController] attribute
        if (!symbolAttributes
         .Where(attr =>
           InheritsOrIsType(attr.AttributeClass, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ApiControllerAttribute")) ||
           InheritsOrIsType(attr.AttributeClass, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.RouteAttribute"))
        ).Any())
        {
          context.ReportDiagnostic
          (
            Diagnostic.Create
            (
              DiagnosticMessages.CONTROLLER_NO_EXTENSION_ATTRIBUTE,
              classSymbol.Locations.FirstOrDefault(),
              classSymbol.Name
            )
          );
        }
      }

    }

    /// <summary>
    /// Report an information message if the class has a method name which contains one of the "well known" data-update values, has a Http Method attribute, and does not have 
    /// an attribute which implements IAuthorizationFilter (like the [Authorize] attribute).
    /// </summary>
    /// <param name="context"></param>
    /// <param name="classSymbol"></param>
    /// <param name="methodNamePart"></param>
    private static void CheckMethodAuthentication(SymbolAnalysisContext context, INamedTypeSymbol classSymbol)
    {
      // if the class does not have any attribute which implements IAuthorizeData
      if (!classSymbol.GetAttributes().Any(attribute => 
        Implements(attribute.AttributeClass, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.IAuthorizeData")) ||
        Implements(attribute.AttributeClass, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.IAllowAnonymous")) 
        ))
      {
        // Check if a method exists which matches any of the "well known" method name parts and is public
        foreach (ISymbol methodSymbol in classSymbol.GetMembers()
          .Where(member => member.DeclaredAccessibility.HasFlag(Accessibility.Public) && ContainsAny(member.Name, ADMIN_METHODS)))
        {
          // Check if the method has an attribute derived from HttpMethodAttribute (like [HttpPost])
          if (methodSymbol.GetAttributes()
              .Any(attribute => InheritsOrIsType(attribute.AttributeClass.BaseType, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute"))))
          {
            // Check if the method does not have an attribute which implements IAuthorizeData
            if (!methodSymbol.GetAttributes().Any(attribute => 
              Implements(attribute.AttributeClass, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.IAuthorizeData")) ||
              Implements(attribute.AttributeClass, context.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Authorization.IAllowAnonymous"))
              ))
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
    }

    private static Boolean IsNucleusExtension(ImmutableArray<Location> locations)
    {
      foreach (Location location in locations)
      {
        if (FindPackageFile(System.IO.Path.GetDirectoryName(location.SourceTree.FilePath))) return true;
      }
      return false;
    }

    private static Boolean FindPackageFile(string path)
    {
#pragma warning disable RS1035 // Do not use APIs banned for analyzers
      if (System.IO.Directory.EnumerateFiles(path, "package.xml").Any())
      {
        // found package.xml
        return true;
      }
      else if (System.IO.Directory.EnumerateFiles(path, "*.csproj").Any())
      {
        // folder contains a project file, but not a package.xml, so this project is not a Nucleus extension
        return false;
      }
#pragma warning restore RS1035 // Do not use APIs banned for analyzers

      return FindPackageFile(System.IO.Path.GetDirectoryName(path));
    }

		private static Boolean Implements(INamedTypeSymbol symbol, INamedTypeSymbol interfaceType)
    {
			return symbol.AllInterfaces.Where(type => SymbolEqualityComparer.Default.Equals(type, interfaceType)).Any();       
    }

    private static Boolean InheritsOrIsType(INamedTypeSymbol symbol, INamedTypeSymbol baseType)
    {
      return GetBaseTypesAndThis(symbol).Where(originalType => SymbolEqualityComparer.Default.Equals(originalType, baseType)).Any();
    }

    public static IEnumerable<INamedTypeSymbol> GetBaseTypesAndThis(INamedTypeSymbol type)
    {
      var current = type;
      while (current != null)
      {
        yield return current;
        current = current.BaseType;
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


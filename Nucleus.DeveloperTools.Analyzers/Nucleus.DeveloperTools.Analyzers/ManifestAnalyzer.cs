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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace Nucleus.DeveloperTools.Analyzers
{
  /// <summary>
  /// Analyzer for common Nucleus extension manifest (package.xml) file problems.
  /// </summary>
  /// <remarks>
  /// For this to work, the project file must contain:
  ///   <ItemGroup>
	///     <AdditionalFiles Include = "Package.xml" />
  ///   </ItemGroup>
  /// </remarks>
  [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
  public class ManifestAnalyzer : DiagnosticAnalyzer
  {
    private static string[] NUCLEUS_PACKAGES =
    {
      "Nucleus.Abstractions",
      "Nucleus.Extensions",
      "Nucleus.ViewFeatures",
      "Nucleus.Data.Common",
      "Nucleus.Data.EntityFramework",
      "Nucleus.Data.MySql",
      "Nucleus.Data.PostgreSql",
      "Nucleus.Data.Sqlite",
      "Nucleus.Data.SqlServer"
    };

    private const string MANIFEST_FILENAME = "package.xml";
    private const string MANIFEST_NAMESPACE_PREFIX = "urn:nucleus/schemas/package/";

    private const string MANIFEST_COMPATIBILITY_ELEMENT_NAME = "compatibility";
    private const string MANIFEST_COMPATIBILITY_ELEMENT_MINVERSION_ATTRIBUTE = "minVersion";

    private const string MANIFEST_PACKAGE_ID_ATTRIBUTE = "id";
    private const string MANIFEST_PACKAGE_NAME_ELEMENT = "name";
    private const string MANIFEST_PACKAGE_VERSION_ELEMENT = "version";


    private const string MANIFEST_COMPONENTS_ELEMENT_NAME = "components";
    private const string MANIFEST_COMPONENT_ELEMENT_NAME = "component";

    private const string MANIFEST_FOLDER_ELEMENT_NAME = "folder";
    private const string MANIFEST_FILE_ELEMENT_NAME = "file";

    public static readonly ImmutableArray<DiagnosticDescriptor> MESSAGES = ImmutableArray.Create
    (
      DiagnosticMessages.MANIFEST_PACKAGE_ID_EMPTY,
      DiagnosticMessages.MANIFEST_PACKAGE_ID_INVALID,
      DiagnosticMessages.MANIFEST_PACKAGE_NAME_EMPTY,
      DiagnosticMessages.MANIFEST_PACKAGE_VERSION_EMPTY,
      DiagnosticMessages.MANIFEST_PACKAGE_VERSION_INVALID,

      DiagnosticMessages.MANIFEST_COMPATIBILITY_MINVERSION_TOOLOW,

      DiagnosticMessages.MANIFEST_PACKAGE_NO_COMPONENTS_ELEMENT,
      DiagnosticMessages.MANIFEST_PACKAGE_NO_COMPONENT_ELEMENTS,

      DiagnosticMessages.MANIFEST_PACKAGE_FILE_NOT_FOUND
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
      
      context.RegisterCompilationAction(AnalyzeManifest);
    }

    /// <summary>
    /// Analyze the manifest (package.xml) file.
    /// </summary>
    /// <remarks>
    /// This function calls multiple analysis functions, rather than having multiple simpler Analyzer classes for each 
    /// analysis, to improve perormance by only reading the manifest file contents once.
    /// 
    /// Manifest analysis warnings and errors do not have a corresponing CodeFixProvider, because CodeFixProviders can 
    /// only work with C# and VB source files (not XML files).
    /// </remarks>
    /// <param name="context"></param>
    private static void AnalyzeManifest(CompilationAnalysisContext context)
    {
      List<Diagnostic> results = new List<Diagnostic>();

      Models.Manifest manifest = ReadManifest(context.Options.AdditionalFiles, context.CancellationToken);
      
      if (manifest.IsValid)
      {
        results.AddRange(AnalyzeManifestMinVersion(context.Compilation.ReferencedAssemblyNames, manifest));
        results.AddRange(AnalyzeManifestPackageId(manifest));
        results.AddRange(AnalyzeManifestPackageName(manifest));
        results.AddRange(AnalyzeManifestPackageVersion(manifest));
        results.AddRange(AnalyzeManifestComponents(manifest));
      }

      foreach (Diagnostic result in results)
      {
        context.ReportDiagnostic(result);
      }
    }

    /// <summary>
    /// Read values from the manifest (package.xml) file.
    /// </summary>
    /// <param name="additionalFiles"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static Models.Manifest ReadManifest(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
    {
      Models.Manifest result = new Models.Manifest();

      // read package.xml
      AdditionalText packageFile = additionalFiles
        .Where(file => Path.GetFileName(file.Path).Equals(MANIFEST_FILENAME, StringComparison.OrdinalIgnoreCase))
        .FirstOrDefault();

      if (packageFile != null)
      {
        SourceText packageFileText = packageFile.GetText(cancellationToken);

        // Write the additional file back to stream and load it into an XDocument.
        using (MemoryStream stream = new MemoryStream())
        {
          using (StreamWriter writer = new StreamWriter(stream, System.Text.Encoding.UTF8, 4096, true))
          {
            packageFileText.Write(writer, cancellationToken);
          }

          stream.Position = 0;
          result.PackageDocument = XDocument.Load(stream, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
      }

      result.Path = packageFile.Path;

      // Check that package.xml is a Nucleus manifest.
      result.IsValid = result.PackageDocument.Root.Name.Namespace.NamespaceName.StartsWith(MANIFEST_NAMESPACE_PREFIX);

      return result;
    }

    /// <summary>
    /// Compare the manifest <compatibility minVersion="n.n.n.n" ... value with the version of referenced well-known 
    /// Nucleus assemblies and generate a warning if a referenced assembly has a version number which is greater than the
    /// minVersion value.
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private static List<Diagnostic> AnalyzeManifestMinVersion(IEnumerable<AssemblyIdentity> references, Models.Manifest manifest)
    {
      List<Diagnostic> results = new List<Diagnostic>();

      XElement compatibilityElement = manifest.PackageDocument.Root.Element(manifest.PackageDocument.Root.Name.Namespace + MANIFEST_COMPATIBILITY_ELEMENT_NAME);

      if (compatibilityElement != null)
      {
        // get and parse the manifest minVersion
        XAttribute minVersionAttribute = compatibilityElement.Attribute(MANIFEST_COMPATIBILITY_ELEMENT_MINVERSION_ATTRIBUTE);
        System.Version minVersion = minVersionAttribute.Value.Parse(false);

        if (!(minVersion.IsEmpty() || minVersionAttribute == null))
        {
          // only check well-known Nucleus packages
          foreach (AssemblyIdentity reference in references
            .Where(reference => NUCLEUS_PACKAGES.Contains(reference.Name, StringComparer.OrdinalIgnoreCase) && !reference.Version.IsEmpty() && minVersion < reference.Version))
          {
            results.Add
            (
              Diagnostic.Create
              (
                DiagnosticMessages.MANIFEST_COMPATIBILITY_MINVERSION_TOOLOW,
                Location.Create(manifest.Path, new TextSpan(), BuildLinePositionSpan(minVersionAttribute, MANIFEST_COMPATIBILITY_ELEMENT_MINVERSION_ATTRIBUTE)),
                reference.Name,
                reference.Version,
                minVersion
              )
            );
          }
        }
      }

      return results;
    }

    /// <summary>
    /// Check the manifest for an empty id.
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private static List<Diagnostic> AnalyzeManifestPackageId(Models.Manifest manifest)
    {
      List<Diagnostic> results = new List<Diagnostic>();

      XElement packageElement = manifest.PackageDocument.Root;

      if (packageElement != null)
      {
        XAttribute packageIdAttribute = packageElement.Attribute(MANIFEST_PACKAGE_ID_ATTRIBUTE);

        if (String.IsNullOrEmpty(packageIdAttribute?.Value))
        {
          results.Add
          (
            Diagnostic.Create
            (
              DiagnosticMessages.MANIFEST_PACKAGE_ID_EMPTY,
              Location.Create(manifest.Path, new TextSpan(), BuildLinePositionSpan(packageIdAttribute, MANIFEST_PACKAGE_ID_ATTRIBUTE))
            )
          );
        }
        else if (!Guid.TryParse(packageIdAttribute.Value, out Guid _))
        {
          results.Add
          (
            Diagnostic.Create
            (
              DiagnosticMessages.MANIFEST_PACKAGE_ID_INVALID,
              Location.Create(manifest.Path, new TextSpan(), BuildLinePositionSpan(packageIdAttribute, MANIFEST_PACKAGE_ID_ATTRIBUTE))
            )
          );
        }
      }

      return results;
    }

    /// <summary>
    /// Check the manifest for an empty name.
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private static List<Diagnostic> AnalyzeManifestPackageName(Models.Manifest manifest)
    {
      List<Diagnostic> results = new List<Diagnostic>();

      XElement packageElement = manifest.PackageDocument.Root;

      if (packageElement != null)
      {
        XElement packageNameElement = packageElement.Element(manifest.PackageDocument.Root.Name.Namespace + MANIFEST_PACKAGE_NAME_ELEMENT);

        if (String.IsNullOrWhiteSpace(packageNameElement?.Value))
        {
          results.Add
          (
            Diagnostic.Create
            (
              DiagnosticMessages.MANIFEST_PACKAGE_NAME_EMPTY,
              Location.Create(manifest.Path, new TextSpan(), BuildLinePositionSpan(packageNameElement, MANIFEST_PACKAGE_NAME_ELEMENT))
            )
          );
        }
      }

      return results;
    }

    /// <summary>
    /// Check the manifest for an empty version.
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private static List<Diagnostic> AnalyzeManifestPackageVersion(Models.Manifest manifest)
    {
      List<Diagnostic> results = new List<Diagnostic>();

      XElement packageElement = manifest.PackageDocument.Root;

      if (packageElement != null)
      {
        XElement packageVersionElement = packageElement.Element(manifest.PackageDocument.Root.Name.Namespace + MANIFEST_PACKAGE_VERSION_ELEMENT);

        if (String.IsNullOrWhiteSpace(packageVersionElement?.Value))
        {
          results.Add
          (
            Diagnostic.Create
            (
              DiagnosticMessages.MANIFEST_PACKAGE_VERSION_EMPTY,
              Location.Create(manifest.Path, new TextSpan(), BuildLinePositionSpan(packageVersionElement, MANIFEST_PACKAGE_VERSION_ELEMENT))
            )
          );
        }
        else if (!Version.TryParse(packageVersionElement.Value, out Version _))
        {
          results.Add
          (
            Diagnostic.Create
            (
              DiagnosticMessages.MANIFEST_PACKAGE_VERSION_INVALID,
              Location.Create(manifest.Path, new TextSpan(), BuildLinePositionSpan(packageVersionElement, MANIFEST_PACKAGE_VERSION_ELEMENT))
            )
          );
        }
      }

      return results;
    }

    /// <summary>
    /// Check the manifest for an empty version.
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private static List<Diagnostic> AnalyzeManifestComponents(Models.Manifest manifest)
    {
      List<Diagnostic> results = new List<Diagnostic>();

      XElement packageElement = manifest.PackageDocument.Root;

      if (packageElement != null)
      {
        XElement componentsElement = packageElement.Element(manifest.PackageDocument.Root.Name.Namespace + MANIFEST_COMPONENTS_ELEMENT_NAME);

        if (componentsElement == null)
        {
          results.Add
          (
            Diagnostic.Create
            (
              DiagnosticMessages.MANIFEST_PACKAGE_NO_COMPONENTS_ELEMENT,
              Location.Create(manifest.Path, new TextSpan(), new LinePositionSpan())
            )
          );
        }
        else
        {
          IEnumerable<XElement> components = componentsElement.Descendants(manifest.PackageDocument.Root.Name.Namespace + MANIFEST_COMPONENT_ELEMENT_NAME);

          if (!components.Any())
          {
            results.Add
            (
              Diagnostic.Create
              (
                DiagnosticMessages.MANIFEST_PACKAGE_NO_COMPONENT_ELEMENTS,
                Location.Create(manifest.Path, new TextSpan(), BuildLinePositionSpan(componentsElement, MANIFEST_COMPONENTS_ELEMENT_NAME))
              )
            );
          }
          else
          {
            // check that files exist
            foreach (XElement component in components)
            {
              string rootPath = System.IO.Path.GetDirectoryName(manifest.Path);
              results.AddRange(CheckFileExists(manifest.PackageDocument.Root.Name.Namespace, component, manifest.Path, rootPath));
            }
          }
        }
      }

      return results;
    }

    private static List<Diagnostic> CheckFileExists(XNamespace ns, XElement parentElement, string manifestPath, string path)
    {
      List<Diagnostic> results = new List<Diagnostic>();
     
      foreach (XElement fileElement in parentElement.Elements(ns + MANIFEST_FILE_ELEMENT_NAME))
      {
        string fileName = $"{path}\\{fileElement.Attribute("name").Value}";
        
        // Use of IO.File is "banned" in Analyzers.  But we're doing it anyway.

#pragma warning disable RS1035 // Do not use APIs banned for analyzers
        if (!System.IO.File.Exists(fileName))
        {
          results.Add
            (
              Diagnostic.Create
              (
                DiagnosticMessages.MANIFEST_PACKAGE_FILE_NOT_FOUND,
                Location.Create(manifestPath, new TextSpan(), BuildLinePositionSpan(fileElement, MANIFEST_FILE_ELEMENT_NAME)),
                fileName.Replace(System.IO.Path.GetDirectoryName(manifestPath), "")
              )
            );
        }
#pragma warning restore RS1035 // Do not use APIs banned for analyzers
      }

      foreach (XElement folderElement in parentElement.Elements(ns + MANIFEST_FOLDER_ELEMENT_NAME))
      {
        string subFolderName = folderElement.Attribute("name")?.Value;
        if (!subFolderName.Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
          results.AddRange(CheckFileExists(ns, folderElement, manifestPath, $"{path}\\{subFolderName}"));
        }
      }

      return results;
    }

    private static LinePositionSpan BuildLinePositionSpan(XElement element, string elementName)
    {
      IXmlLineInfo lineInfo = (IXmlLineInfo)element;

      int startPosition = lineInfo.HasLineInfo() ? lineInfo.LinePosition + elementName.Length : 0;
      int endPosition = lineInfo.HasLineInfo() ? startPosition + element.Value.Length : 0;

      return new LinePositionSpan
      (
        new LinePosition(lineInfo.LineNumber - 1, startPosition),
        new LinePosition(lineInfo.LineNumber - 1, endPosition)
      );
    }

    private static LinePositionSpan BuildLinePositionSpan(XAttribute attribute, string elementName)
    {
      IXmlLineInfo lineInfo = (IXmlLineInfo)attribute;

      // +1 is for the opening quote character around the attribute value, so that the start position is
      // at the start of the value and not at the opening quote.
      int startPosition = lineInfo.HasLineInfo() ? lineInfo.LinePosition + elementName.Length + 1 : 0;
      int endPosition = lineInfo.HasLineInfo() ? startPosition + attribute.Value.Length : 0;

      return new LinePositionSpan
      (
        new LinePosition(lineInfo.LineNumber - 1, startPosition),
        new LinePosition(lineInfo.LineNumber - 1, endPosition)
      );
    }
  }
}

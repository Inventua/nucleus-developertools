using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using Nucleus.DeveloperTools.MSBuild.Properties;
using Microsoft.Build.Framework;
using Nucleus.DeveloperTools.Shared;

namespace Nucleus.DeveloperTools.MSBuild
{
  /// <summary>
  /// MSBuild task to read the package.xml file for a Nucleus extension, validate it, and return a list of files referenced by package.xml.
  /// </summary>
  /// <example>
  //  <Target Name = "GetPackageContent" DependsOnTargets="ResolveReferences" AfterTargets="PostBuildEvent">
  //    <GetNucleusPackageContent PackageFile = "Package.xml" ProjectFile="$(MSBuildProjectFile)">        
  //      <Output TaskParameter = "Content" ItemName="PackageContent" />
  //    </GetNucleusPackageContent>
  //  </Target>
  //  <UsingTask
  //    TaskName = "GetNucleusPackageContent"
  //    AssemblyFile="$(NUCLEUS_DEVTOOLS_PATH)\Nucleus.DeveloperTools.MSBuild.dll">
  //  </UsingTask>
  /// </example>
  public class GetNucleusPackageContent : Microsoft.Build.Utilities.Task
  {
    private static string[] NUCLEUS_PACKAGES =
    {
      "Inventua.Nucleus.Abstractions",
      "Inventua.Nucleus.Extensions",
      "Inventua.Nucleus.ViewFeatures",
      "Inventua.Nucleus.Data.Common",
      "Inventua.Nucleus.Data.EntityFramework",
      "Inventua.Nucleus.Data.MySql",
      "Inventua.Nucleus.Data.PostgreSql",
      "Inventua.Nucleus.Data.Sqlite",
      "Inventua.Nucleus.Data.SqlServer"
    };
    

    // project file (csproj) elements
    private const string PROJECT_PACKAGE_REFERENCE_ELEMENT = "PackageReference";

    // error codes help link
    private const string DEVELOPER_TOOLS_ERROR_REFERENCE_URL = "https://www.nucleus-cms.com/references/dev-tools-error-codes/";

    [Required]
    public ITaskItem PackageFile { get; set; }

    [Required]
    public ITaskItem ProjectFile { get; set; }
        
    [Output]
    public ITaskItem[] Content
    {
      get
      {
        return this.Items.ToArray();
      }
    }

    // storage for files detected in package.xml, returned as an array by the Content property.
    private List<Microsoft.Build.Utilities.TaskItem> Items = new List<Microsoft.Build.Utilities.TaskItem>();

    /// <summary>
    /// Analyze the manifest (package.xml) file.
    /// </summary>
    public override Boolean Execute()
    {
      Boolean result = true;

      try
      {
        //XDocument manifest = ReadManifest();
        Manifest manifest = Manifest.FromFile(this.PackageFile.ItemSpec);

        if (manifest.IsValidPackage())
        {
          if (!AnalyzeManifestMinVersion(manifest))
          {
            result = false;
          };

          if (!AnalyzeManifestPackageId(manifest))
          {
            result = false;
          };

          if (!AnalyzeManifestPackageName(manifest))
          {
            result = false;
          };

          if (!AnalyzeManifestPackageVersion(manifest))
          {
            result = false;
          };

          if (!AnalyzeManifestComponents(manifest))
          {
            result = false;
          };
        }
        else
        {
          LogError("NUCL001");
          result = false;
        }
      }
      catch (Exception ex)
      {
        Log.LogErrorFromException(ex);
        result = false;
      }
      
      return result;
    }

    /// <summary>
    /// Read the project (.csproj) file.
    /// </summary>
    private XDocument ReadProject()
    {
      if (!System.IO.File.Exists(this.ProjectFile.ItemSpec))
      {
        throw new FileNotFoundException(this.ProjectFile.ItemSpec);
      }
      else
      {
        using (System.IO.Stream stream = System.IO.File.OpenRead(this.ProjectFile.ItemSpec))
        {
          return XDocument.Load(stream, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
        }
      }      
    }

    /// <summary>
    /// Compare the manifest <compatibility minVersion="n.n.n.n" ... value with the version of referenced well-known 
    /// Nucleus assemblies and generate an error if a referenced assembly has a version number which is greater than the
    /// minVersion value.
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private Boolean AnalyzeManifestMinVersion(Manifest manifest)
    {
      Boolean result = true;
            
      XDocument project = ReadProject();

      XElement compatibilityElement = manifest.GetCompatibilityElement();

      if (compatibilityElement != null)
      {
        // get and parse the manifest minVersion
        XAttribute minVersionAttribute = compatibilityElement.Attribute(Manifest.COMPATIBILITY_ELEMENT_MINVERSION_ATTRIBUTE);
        System.Version minVersion = minVersionAttribute.Value.Parse(false);

        if (!(minVersion.IsEmpty() || minVersionAttribute == null))
        {
          foreach (XElement packageReferenceElement in project.Root.Descendants(PROJECT_PACKAGE_REFERENCE_ELEMENT))
          {
            string referenceName = packageReferenceElement.Attribute("Include").Value;
            System.Version referenceVersion = System.Version.Parse(packageReferenceElement.Attribute("Version").Value).ZeroUndefinedElements();

            // check well-known Nucleus packages
            if (NUCLEUS_PACKAGES.Contains(referenceName, StringComparer.OrdinalIgnoreCase) && !referenceVersion.IsEmpty() && minVersion < referenceVersion)
            {
              this.LogError
              (
                "NUCL200",
                minVersionAttribute,
                referenceName,
                referenceVersion,
                minVersion
              );
              result = false;
            }
          }
        }
      }

      return result;
    }

    /// <summary>
    /// Check the manifest for an empty or invalid id attribute.
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private Boolean AnalyzeManifestPackageId(Manifest manifest)
    {
      Boolean result = true;
      XElement packageElement = manifest.GetPackageElement();// Root;

      if (packageElement != null)
      {
        XAttribute packageIdAttribute = packageElement.Attribute(Manifest.PACKAGE_ID_ATTRIBUTE);

        if (String.IsNullOrEmpty(packageIdAttribute?.Value))
        {
          this.LogError("NUCL100", packageIdAttribute);
          result = false;
        }
        else if (!Guid.TryParse(packageIdAttribute.Value, out Guid _))
        {
          this.LogError("NUCL103", packageIdAttribute);
          result = false;
        }
      }

      return result;
    }

    /// <summary>
    /// Check the manifest for an empty name element.
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private Boolean AnalyzeManifestPackageName(Manifest manifest)
    {
      Boolean result = true;

      XElement packageElement = manifest.GetPackageElement();

      if (packageElement != null)
      {
        XElement packageNameElement = manifest.GetElement(packageElement, Manifest.PACKAGE_NAME_ELEMENT);

        if (String.IsNullOrWhiteSpace(packageNameElement?.Value))
        {
          this.LogError("NUCL101", packageNameElement);
          result = false;          
        }
      }

      return result;
    }

    /// <summary>
    /// Check the manifest for an empty version or an invalid version format.
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private Boolean AnalyzeManifestPackageVersion(Manifest manifest)
    {
      Boolean result = true;

      XElement packageElement = manifest.GetPackageElement();

      if (packageElement != null)
      {
        XElement packageVersionElement = manifest.GetElement(packageElement, Manifest.PACKAGE_VERSION_ELEMENT);

        if (String.IsNullOrWhiteSpace(packageVersionElement?.Value))
        {
          this.LogError("NUCL102", packageVersionElement);
          result = false;
        }
        else if (!Version.TryParse(packageVersionElement.Value, out Version _))
        {
          this.LogError("NUCL104", packageVersionElement);
          result = false;
        }
      }

      return result;
    }

    /// <summary>
    /// Check that the manifest contains a &lt;components&gt; element and at least one &lt;component&gt; element and
    /// check that all files referenced in components are present on the file system.  Add files to the result (this.Items).
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private Boolean AnalyzeManifestComponents(Manifest manifest)
    {
      Boolean result = true;

      XElement packageElement = manifest.GetPackageElement();

      if (packageElement != null)
      {
        // Always include package.xml in the output
        this.Items.Add(new Microsoft.Build.Utilities.TaskItem() { ItemSpec = this.PackageFile.ItemSpec });

        // check that there is a <components> element
        XElement componentsElement = manifest.GetElement(packageElement, Manifest.COMPONENTS_ELEMENT_NAME);

        if (componentsElement == null)
        {
          this.LogError("NUCL105", packageElement);
          result = false;
        }
        else
        {
          // check that the <components> element contains at least one <component>
          IEnumerable<XElement> components = manifest.GetDescendants(componentsElement, Manifest.COMPONENT_ELEMENT_NAME);

          if (!components.Any())
          {
            this.LogError("NUCL106", componentsElement);
            result = false;
          }
          else
          {
            // check that files exist
            CheckFilesExist(manifest);            
          }
        }
      }

      return result;
    }

    /// <summary>
    /// Check that the files in <paramref name="parentElement"/> are present, and add them to the result (this.Items) 
    /// or log an error if they are not present.
    /// </summary>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private Boolean CheckFilesExist(Manifest manifest)
    {
      Boolean result = true;
      string projectPath = System.IO.Path.GetDirectoryName(this.ProjectFile.ItemSpec);

      foreach (ManifestFile file in manifest.Files)
      {
        string fileName = System.IO.Path.Combine(projectPath, file.FileName);

        if (System.IO.File.Exists(fileName))
        {
          this.Items.Add(new Microsoft.Build.Utilities.TaskItem() { ItemSpec = fileName });
        }
        else
        {
          this.LogError("NUCL110", file.Element, fileName);
          result = false;
        }
      }

      return result;
    }

    /// <summary>
    /// Log an error for the specified <paramref name="code"/> with no source code location specified.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="args"></param>
    private void LogError(string code, params object[] args)
    {
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT");
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY");

      Log.LogError
      (
        subcategory: category,
        code,
        code,
        DEVELOPER_TOOLS_ERROR_REFERENCE_URL,
        this.PackageFile.ItemSpec,
        0,0,0,0,
        message,
        args
      );
    }

    /// <summary>
    /// Log an error for the specified <paramref name="code"/> and <paramref name="element"/>.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="element"></param>
    /// <param name="args"></param>
    private void LogError(string code, XElement element, params object[] args)
    {
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT");
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY");

      Models.SourceCodeLocation location = BuildLocation(element);
      Log.LogError
      (
        subcategory: category,
        code,
        code,
        DEVELOPER_TOOLS_ERROR_REFERENCE_URL,
        this.PackageFile.ItemSpec,
        location.StartLineNumber,
        location.StartColumnNumber,
        location.EndLineNumber,
        location.EndColumnNumber,
        message,
        args
      );
     }

    /// <summary>
    /// Log an error for the specified <paramref name="code"/> and <paramref name="attribute"/>.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="attribute"></param>
    /// <param name="args"></param>
    private void LogError(string code, XAttribute attribute, params object[] args)
    {
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT");
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY");

      Models.SourceCodeLocation location = BuildLocation(attribute);
      Log.LogError
      (
        subcategory: category,
        code,
        code,
        DEVELOPER_TOOLS_ERROR_REFERENCE_URL,
        this.PackageFile.ItemSpec,
        location.StartLineNumber,
        location.StartColumnNumber,
        location.EndLineNumber,
        location.EndColumnNumber,
        message,
        args
      );
    }

    /// <summary>
    /// Return a SourceCodeLocation for the specified <paramref name="element"/> value.
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    private Models.SourceCodeLocation BuildLocation(XElement element)
    {
      string elementName = element.Name.LocalName;
      IXmlLineInfo lineInfo = (IXmlLineInfo)element;

      // +1 is for the > character following the element value
      int startPosition = lineInfo.HasLineInfo() ? lineInfo.LinePosition + elementName.Length + 1 : 0;
      int endPosition = lineInfo.HasLineInfo() ? startPosition + element.Value.Length : 0;

      return new Models.SourceCodeLocation
      {
        StartLineNumber = lineInfo.LineNumber,
        StartColumnNumber = startPosition,
        EndLineNumber = lineInfo.LineNumber,
        EndColumnNumber = endPosition
      };
    }

    /// <summary>
    /// Return a SourceCodeLocation for the specified <paramref name="attribute"/> value.
    /// </summary>
    /// <param name="attribute"></param>
    /// <returns></returns>
    private Models.SourceCodeLocation BuildLocation(XAttribute attribute)
    {
      string attributeName = attribute.Name.LocalName;
      IXmlLineInfo lineInfo = (IXmlLineInfo)attribute;

      // +2 is for the equals and quote (=") character before the attribute value, so that the start position is
      // at the start of the value
      int startPosition = lineInfo.HasLineInfo() ? lineInfo.LinePosition + attributeName.Length + 2 : 0;
      int endPosition = lineInfo.HasLineInfo() ? startPosition + attribute.Value.Length : 0;

      return new Models.SourceCodeLocation
      {
        StartLineNumber = lineInfo.LineNumber,
        StartColumnNumber = startPosition,
        EndLineNumber = lineInfo.LineNumber,
        EndColumnNumber = endPosition
      };
    }
  }
}

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
    private static readonly string[] NUCLEUS_PACKAGES =
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
    private readonly List<Microsoft.Build.Utilities.TaskItem> Items = new List<Microsoft.Build.Utilities.TaskItem>();

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
                    
          if (!AnalyzeManifestPublisher(manifest))
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
          // package.xml is not valid
          LogError(Resources.NUCL001_CODE);
          result = false;
        }
      }
      catch (Exception ex)
      {
        Log.LogErrorFromException(ex);
        // an otherwise-unhandled error (that is, a bug in this class) should not prevent the build from succeeding, so we do not
        // set result=false here.        
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
            System.Version referenceVersion = packageReferenceElement.Attribute("Version").Value.Parse(true);

            // check well-known Nucleus packages
            if (NUCLEUS_PACKAGES.Contains(referenceName, StringComparer.OrdinalIgnoreCase) && !referenceVersion.IsEmpty() && minVersion < referenceVersion)
            {
              this.LogError
              (
                Resources.NUCL200_CODE,
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
          this.LogWarning(Resources.NUCL100_CODE, packageIdAttribute);
          result = false;
        }
        else if (!Guid.TryParse(packageIdAttribute.Value, out Guid _))
        {
          this.LogWarning(Resources.NUCL103_CODE, packageIdAttribute);
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
          this.LogError(Resources.NUCL101_CODE, packageNameElement);
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
          this.LogError(Resources.NUCL102_CODE, packageVersionElement);
          result = false;
        }
        else if (!Version.TryParse(packageVersionElement.Value, out Version _))
        {
          this.LogError(Resources.NUCL104_CODE, packageVersionElement);
          result = false;
        }
      }

      return result;
    }

    /// <summary>
    /// Check the manifest for an empty publisher name, email or url.
    /// </summary>
    /// <param name="references"></param>
    /// <param name="manifest"></param>
    /// <returns></returns>
    private Boolean AnalyzeManifestPublisher(Manifest manifest)
    {
      Boolean result = true;
      XElement publisherElement = manifest.GetPublisherElement();

      if (publisherElement != null)
      {
        XAttribute publisherNameAttribute = publisherElement.Attribute(Manifest.NAME_ATTRIBUTE_NAME);

        if (String.IsNullOrEmpty(publisherNameAttribute?.Value))
        {
          this.LogWarning(Resources.NUCL210_CODE, publisherNameAttribute);          
        }

        XAttribute publisherEmailAttribute = publisherElement.Attribute(Manifest.EMAIL_ATTRIBUTE_NAME);

        if (!Guid.TryParse(publisherEmailAttribute.Value, out Guid _))
        {
          this.LogWarning(Resources.NUCL211_CODE, publisherEmailAttribute);
        }

        XAttribute publisherUrlAttribute = publisherElement.Attribute(Manifest.URL_ATTRIBUTE_NAME);
        
        if (!Guid.TryParse(publisherUrlAttribute.Value, out Guid _))
        {
          this.LogWarning(Resources.NUCL212_CODE, publisherUrlAttribute);
        }
      }
      else
      {
        this.LogError(Resources.NUCL213_CODE, manifest.GetPackageElement());
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
          this.LogError(Resources.NUCL105_CODE, packageElement);
          result = false;
        }
        else
        {
          // check that the <components> element contains at least one <component>
          IEnumerable<XElement> components = manifest.GetDescendants(componentsElement, Manifest.COMPONENT_ELEMENT_NAME);

          if (!components.Any())
          {
            this.LogError(Resources.NUCL106_CODE, componentsElement);
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
          this.LogError(Resources.NUCL110_CODE, file.Element, fileName);
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
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT") ?? "";
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY") ?? "";

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
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT") ?? "";
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY") ?? "";

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
    /// Log a warning for the specified <paramref name="code"/> and <paramref name="attribute"/>.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="attribute"></param>
    /// <param name="args"></param>
    private void LogWarning(string code, XAttribute attribute, params object[] args)
    {
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT") ?? "";
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY") ?? "";

      Models.SourceCodeLocation location = BuildLocation(attribute);
      Log.LogWarning
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Nucleus.DeveloperTools.Shared
{
  public class Manifest
  {
    private const string MANIFEST_NAMESPACE_PREFIX = "urn:nucleus/schemas/package/";

    // manifest (package.xml) elements and attributes

    public const string PACKAGE_ID_ATTRIBUTE = "id";
    public const string PACKAGE_NAME_ELEMENT = NAME_ATTRIBUTE_NAME;
    public const string PACKAGE_VERSION_ELEMENT = "version";

    public const string COMPATIBILITY_ELEMENT_NAME = "compatibility";
    public const string COMPATIBILITY_ELEMENT_MINVERSION_ATTRIBUTE = "minVersion";

    public const string PUBLISHER_ELEMENT_NAME = "publisher";

    public const string COMPONENTS_ELEMENT_NAME = "components";
    public const string COMPONENT_ELEMENT_NAME = "component";

    public const string EXTENSION_ELEMENT_NAME = "extension";

    public const string FOLDER_ELEMENT_NAME = "folder";
    public const string FILE_ELEMENT_NAME = "file";

    public const string NAME_ATTRIBUTE_NAME = "name";
    public const string EMAIL_ATTRIBUTE_NAME = "email";
    public const string URL_ATTRIBUTE_NAME = "url";

    [Flags]
    public enum ManifestFilesFilters
    {
      ContentFiles = 1,
      Binaries = 2
    }

    private Manifest()
    {
    //  this.FileName = filename;
    }

    public static Manifest FromFile(string filePath)
    {
      if (!System.IO.File.Exists(filePath))
      {
        throw new FileNotFoundException(filePath);
      }

      Manifest manifest = new Manifest();

      using (System.IO.Stream stream = System.IO.File.OpenRead(filePath))
      {
        manifest.Document = XDocument.Load(stream, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);        
      }
            
      return manifest;
    }

    public static Manifest FromString(string contents)
    {
      Manifest manifest = new Manifest();
        
      using (StringReader reader = new StringReader(contents))
      {
        manifest.Document = XDocument.Load(reader, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace);
      }

      return manifest;
    }

    public XDocument Document { get; private set; }

    public XNamespace Namespace
    {
      get
      {
        return this.Document.Root.Name.Namespace;
      }
    }

    /// <summary>
    /// Return whether the specified <paramref name="package"/> matches the Nucleus manifest file namespace.
    /// </summary>
    /// <param name="package"></param>
    /// <returns></returns>
    public Boolean IsValidPackage()
    {
      return this.Namespace.NamespaceName.StartsWith(MANIFEST_NAMESPACE_PREFIX);
    }

    public IEnumerable<XElement> GetElements(XElement parent, string elementName)
    {
      return parent.Elements(this.Namespace + elementName);
    }

    public XElement GetElement(XElement parent, string elementName)
    {
      return parent.Element(this.Namespace + elementName);
    }

    public IEnumerable<XElement> GetDescendants(string elementName)
    {
      return this.Document.Root.Descendants(this.Namespace + elementName);
    }

    public IEnumerable<XElement> GetDescendants(XElement parent, string elementName)
    {
      return parent.Descendants(this.Namespace + elementName);
    }

    public XElement GetPackageElement()
    {
      return this.Document.Root;
    }

    public XElement GetPublisherElement()
    {
      return this.Document.Root.Element(this.Namespace + PUBLISHER_ELEMENT_NAME);
    }

    public XElement GetCompatibilityElement()
    {
      return this.Document.Root.Element(this.Namespace + COMPATIBILITY_ELEMENT_NAME);
    }

    public XElement GetComponentsElement()
    {
      return this.Document.Root.Element(this.Namespace + COMPONENTS_ELEMENT_NAME);
    }

    public IEnumerable<XElement> GetComponentsElements()
    {
      return this.GetComponentsElement().Elements(this.Namespace + COMPONENT_ELEMENT_NAME);
    }

    /// <summary>
    /// Return a list of files in the manifest.
    /// </summary
    /// <returns></returns>
    public List<ManifestFile> Files(ManifestFilesFilters filter)
    {
      List<ManifestFile> results = new List<ManifestFile>();
 
      foreach (XElement component in this.GetComponentsElements())
      {
        results.AddRange(GetFiles(component, "", filter));
      } 

      return results; 
    }

    /// <summary>
    /// Return a list of files in the manifest.
    /// </summary>
    /// <param name="parentElement"></param>
    /// <param name="path"></param>
    /// <param name="results"></param>
    private List<ManifestFile> GetFiles(XElement parentElement, string path, ManifestFilesFilters filter)
    {
      List<ManifestFile> results = new List<ManifestFile>();

      string pathStart = path.Split(new char[] { '/', '\\' }).FirstOrDefault();
      Boolean isBinFolder = pathStart?.Equals("bin", StringComparison.OrdinalIgnoreCase) == true;

      if ((!isBinFolder && filter.HasFlag(ManifestFilesFilters.ContentFiles)) || (isBinFolder && filter.HasFlag(ManifestFilesFilters.Binaries)))
      {
        foreach (XElement fileElement in parentElement.Elements(this.Namespace + FILE_ELEMENT_NAME))
        {
          string fileName = System.IO.Path.Combine(path, fileElement.Attribute(NAME_ATTRIBUTE_NAME).Value);

          results.Add(new ManifestFile(fileName, fileElement));
        }
      }

      foreach (XElement folderElement in parentElement.Elements(this.Namespace + FOLDER_ELEMENT_NAME))
      {
        string subFolderName = folderElement.Attribute(NAME_ATTRIBUTE_NAME)?.Value;
        results.AddRange(GetFiles(folderElement, System.IO.Path.Combine(path, subFolderName), filter));
      }

      return results;
    }

    /// <summary>
    /// Look for a Folder matching the path of the specified <paramref name="relativePath"/> and add the file to it, or create a new 
    /// set of folder elements and add the file.
    /// </summary>
    /// <param name="relativeFilePath">Path of the file to add, relative to the project root folder.</param>
    /// <returns>
    /// The added xml element.
    /// </returns>
    public XElement AddFile(string relativeFilePath)
    {
      // Homogenize path separators.  We don't have to worry about Windows vs Linux path separators here, because the entries
      // in package.xml end up in a zip file which is generated by System.IO.Compression.ZipArchive, and ZipArchive always
      // uses a forward slash ("/") as the path separator.
      // Reference: https://learn.microsoft.com/en-us/dotnet/framework/migration-guide/mitigation-ziparchiveentry-fullname-path-separator
      relativeFilePath = relativeFilePath.Replace("/", "\\");

      XElement folder = FindFileFolderElement(relativeFilePath, true);

      if (folder != null)
      {
        XElement fileElement = new XElement(XName.Get(FILE_ELEMENT_NAME, this.Namespace.NamespaceName));
        fileElement.SetAttributeValue(NAME_ATTRIBUTE_NAME, System.IO.Path.GetFileName(relativeFilePath));
        
        // position the new <file> element before any child <folder> elements
        XElement existingSubFolder = this.GetElement(folder, FOLDER_ELEMENT_NAME);

        if (existingSubFolder == null)
        {
          folder.Add(fileElement);
        }
        else
        {
          existingSubFolder.AddBeforeSelf(fileElement);
        }

        return fileElement;        
      }

      return null;
    }

    /// <summary>
    /// Find the (component or folder) element which represents the path of the specified <paramref name="relativeFilePath"/>, 
    /// or create it if it is not present.
    /// </summary>
    /// <param name="relativeFilePath"></param>
    /// <returns></returns>
    private XElement FindFileFolderElement(string relativeFilePath, Boolean create)
    {
      string path = System.IO.Path.GetDirectoryName(relativeFilePath);
      string[] pathFolders = path.Split(new char[] {'/' ,'\\'}, StringSplitOptions.RemoveEmptyEntries);
      
      if (pathFolders.Length == 0)
      {
        // file is at the root, return the first <component>
        return this.GetComponentsElements().FirstOrDefault();
      }
      else
      {
        int pathFoldersIndex = 0;
        XElement current = this.GetComponentsElements().FirstOrDefault();
        string foundPath = "";

        while (pathFoldersIndex < pathFolders.Length) 
        {

          XElement folderElement = this.GetElements(current, FOLDER_ELEMENT_NAME)
            .Where(folder => folder.Attribute(NAME_ATTRIBUTE_NAME)?.Value.Equals(pathFolders[pathFoldersIndex], StringComparison.OrdinalIgnoreCase) == true)
            .FirstOrDefault();

          if (folderElement == null)
          {
            if (create)
            {
              XElement newFolderElement = new XElement(XName.Get(FOLDER_ELEMENT_NAME, this.Namespace.NamespaceName));
              newFolderElement.SetAttributeValue(NAME_ATTRIBUTE_NAME, pathFolders[pathFoldersIndex]);
              current.Add(newFolderElement);
              current = newFolderElement;
            }
            else
            {
              return null;
            }
          }
          else
          {
            current = folderElement;
          }

          foundPath += $"{(String.IsNullOrEmpty(foundPath) ? "" : "\\")}{pathFolders[pathFoldersIndex]}";
          pathFoldersIndex++;

          if (foundPath.Equals(path, StringComparison.OrdinalIgnoreCase))
          {
            return current;
          }
        }
      }

      return null;
    }

    public override string ToString()
    {
      // XDocument.ToString does not "pretty print" after using XElement.Add/XElement.AddAfterSelf.  It indents property, but the new item
      // does not have a line feed after it.  This technique (of parsing the XDocument contents first, before calling ToString) outputs XML
      // which is formatted as expected.
      this.Document = XDocument.Parse(this.Document.ToString());
      return this.Document.ToString(SaveOptions.OmitDuplicateNamespaces);
    }
  }
}

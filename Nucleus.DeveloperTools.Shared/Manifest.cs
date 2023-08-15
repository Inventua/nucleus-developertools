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
    //private string FileName { get; set; }    
    //private XDocument _document { get; set; }
    private List<ManifestFile> _files {  get; set; }

    private const string MANIFEST_NAMESPACE_PREFIX = "urn:nucleus/schemas/package/";

    // manifest (package.xml) elements and attributes
    public const string COMPATIBILITY_ELEMENT_NAME = "compatibility";
    public const string COMPATIBILITY_ELEMENT_MINVERSION_ATTRIBUTE = "minVersion";

    public const string PACKAGE_ID_ATTRIBUTE = "id";
    public const string PACKAGE_NAME_ELEMENT = NAME_ATTRIBUTE_NAME;
    public const string PACKAGE_VERSION_ELEMENT = "version";

    public const string COMPONENTS_ELEMENT_NAME = "components";
    public const string COMPONENT_ELEMENT_NAME = "component";

    public const string EXTENSION_ELEMENT_NAME = "extension";

    public const string FOLDER_ELEMENT_NAME = "folder";
    public const string FILE_ELEMENT_NAME = "file";

    public const string NAME_ATTRIBUTE_NAME = "name";

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
    public List<ManifestFile> Files
    {
      get
      {
        if (this._files == null)
        {
          List<ManifestFile> results = new List<ManifestFile>();
          foreach (XElement component in this.GetComponentsElements())
          {
            GetFiles(component, "", results);
          }
          this._files = results;
        }
        return this._files;
      }
    }
          
    private void GetFiles(XElement parentElement, string path, List<ManifestFile> results)
    {

      foreach (XElement fileElement in parentElement.Elements(this.Namespace + FILE_ELEMENT_NAME))
      {
        string fileName = System.IO.Path.Combine(path, fileElement.Attribute(NAME_ATTRIBUTE_NAME).Value);

        results.Add(new ManifestFile(fileName, fileElement));
      }

      foreach (XElement folderElement in parentElement.Elements(this.Namespace + FOLDER_ELEMENT_NAME))
      {
        string subFolderName = folderElement.Attribute(NAME_ATTRIBUTE_NAME)?.Value;
        if (!subFolderName.Equals("bin", StringComparison.OrdinalIgnoreCase))
        {
          GetFiles(folderElement, System.IO.Path.Combine(path, subFolderName), results);
        }
      }
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
        using (System.Xml.XmlWriter writer = folder.CreateWriter())
        {
          writer.WriteStartElement(FILE_ELEMENT_NAME, this.Namespace.NamespaceName);
          writer.WriteAttributeString(NAME_ATTRIBUTE_NAME, System.IO.Path.GetFileName(relativeFilePath));
          writer.WriteEndElement();
        }

        XElement file = this.GetElements(folder, FILE_ELEMENT_NAME)
          .Where(elem => elem.Attribute(NAME_ATTRIBUTE_NAME)?.Value.Equals(System.IO.Path.GetFileName(relativeFilePath)) == true)
          .FirstOrDefault();

        return file;        
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
      string[] pathFolders = path.Split(new char[] {'/' ,'\\'});
      
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
              using (System.Xml.XmlWriter writer = current.CreateWriter())
              {
                writer.WriteStartElement(FOLDER_ELEMENT_NAME, this.Namespace.NamespaceName);
                writer.WriteAttributeString(NAME_ATTRIBUTE_NAME, pathFolders[pathFoldersIndex]);
                writer.WriteEndElement();                
              }
              
              current = this.GetElements(current, FOLDER_ELEMENT_NAME)
                .Where(folder => folder.Attribute(NAME_ATTRIBUTE_NAME)?.Value.Equals(pathFolders[pathFoldersIndex], StringComparison.OrdinalIgnoreCase) == true)
                .FirstOrDefault();
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
      StringBuilder builder = new StringBuilder();
      StringWriter writer = new StringWriter(builder);
            
      this.Document.Save(writer, SaveOptions.OmitDuplicateNamespaces);
      return builder.ToString();
    }
  }
}

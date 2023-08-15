using System;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;

namespace Nucleus.DeveloperTools.Shared
{
  public class ManifestFile
  {
    public string FileName { get; private set; }

    public XElement Element { get; private set; }
    
    public ManifestFile(string filename, XElement element) 
    {
      this.FileName = filename;
      this.Element = element;
    }

  
  }
}

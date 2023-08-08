using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace Nucleus.DeveloperTools.Analyzers.Models
{
  internal class Manifest
  {
    public string Path { get; set; }

    public XDocument PackageDocument { get; set; }

    public Boolean IsValid { get; set; }
  }
}

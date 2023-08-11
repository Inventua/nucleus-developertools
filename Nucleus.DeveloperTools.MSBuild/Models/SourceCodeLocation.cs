using System;
using System.Collections.Generic;
using System.Text;

namespace Nucleus.DeveloperTools.MSBuild.Models
{
  internal class SourceCodeLocation
  {
    public int StartLineNumber { get; set; }
    public int StartColumnNumber { get; set; }

    public int EndLineNumber { get; set; }
    public int EndColumnNumber { get; set; }
  }
}

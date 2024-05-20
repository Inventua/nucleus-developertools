using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.DeveloperTools.VisualStudio.Commands.Models;

public class ProjectReference 
{
  public Boolean IsSelected { get; set; }

  public string FileName { get; }
  public string ReferenceName { get; }

  public string Description { get; }
  public string Version { get; }
  public string Path { get; }

  public ProjectReference(VSLangProj.Reference reference, string filename)
  {
    this.FileName = filename;
    this.ReferenceName = reference.Name;
    this.Version = reference.Version; 
    this.Path = reference.Path;
    this.Description = reference.Description;
    
    this.IsSelected = true;
  }

  //public string AssemblyFileName { get => this.Name + ".dll"; }
}

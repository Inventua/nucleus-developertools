using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Nucleus.DeveloperTools.Shared;
using VSLangProj;

namespace Nucleus.DeveloperTools.VisualStudio.Commands.ViewModels;

public class AddReferencesToPackage
{
  public IEnumerable<Models.ProjectReference> ExistingReferences { get; set; }
  public IEnumerable<Models.ProjectReference> NewReferences { get; set; }

  public Visibility IsListVisible 
  { 
    get 
    {
      return this.NewReferences.Any() ? Visibility.Visible : Visibility.Collapsed; 
    } 
  }

  public Visibility IsWarningVisible
  {
    get
    {
      return !this.NewReferences.Any() ? Visibility.Visible : Visibility.Collapsed;
    }
  }
}

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

  public Visibility IsNewReferencesListVisible 
  { 
    get 
    {
      return this.NewReferences.Any() ? Visibility.Visible : Visibility.Collapsed; 
    } 
  }

  public Visibility IsExistingReferencesListVisible
  {
    get
    {
      return this.ExistingReferences.Any() ? Visibility.Visible : Visibility.Collapsed;
    }
  }

  public Visibility IsNewReferencesWarningVisible
  {
    get
    {
      return !this.NewReferences.Any() ? Visibility.Visible : Visibility.Collapsed;
    }
  }

  public Visibility IsExistingReferencesWarningVisible
  {
    get
    {
      return !this.ExistingReferences.Any() ? Visibility.Visible : Visibility.Collapsed;
    }
  }
}

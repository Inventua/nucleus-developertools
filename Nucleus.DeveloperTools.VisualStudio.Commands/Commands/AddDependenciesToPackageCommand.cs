using EnvDTE;
using System.Collections.Generic;
using Nucleus.DeveloperTools.Shared;
using System.Linq;
using Microsoft.VisualStudio.Threading;
using System.Diagnostics;
using VSLangProj;

namespace Nucleus.DeveloperTools.VisualStudio.Commands;

[Command(PackageIds.AddDependenciesToPackageCommand)]
internal sealed class AddDependenciesToPackage : BaseCommand<AddDependenciesToPackage>
{
  private const string MANIFEST_FILENAME = "package.xml";
  private static readonly string[] NUCLEUS_REFERENCE_SEARCH_PATHS = ["", "bin\\{environment}"];
  private const string NUCLEUS_PATH_ENV_VARIABLE = "NUCLEUS_PATH";
  private const string NUCLEUS_MAIN_ASSEMBLY_NAME = "Nucleus.Web.dll";
  private const string DOTNET_INSTALL_PATH = "C:\\Program Files\\dotnet\\packs\\";

  private EnvDTE80.DTE2 DTE => this.Package.GetService<DTE, DTE>() as EnvDTE80.DTE2;

  protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
  {
    List<VSLangProj.Reference> references = [];
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

    if (this.DTE.IsNucleusProject())
    {
      Manifest manifest = this.DTE.GetManifest();
      if (manifest?.IsValidPackage() == false) return;

      if (this.DTE.SelectedItems.Item(1).Project?.Object is not VSLangProj.VSProject project) return;

      string nucleusReferencePath = GetReferencePath();

      // if the nucleus reference path can't be found, then display an error
      if (String.IsNullOrEmpty(nucleusReferencePath))
      {
        this.ShowInfoBarMessage($"The Nucleus reference path could not be found.  Please check that you have installed a reference instance of Nucleus and that the {NUCLEUS_PATH_ENV_VARIABLE} environment variable is set.");
      }
      else
      {
        // filter out references which are part of .net core or are part of Nucleus
        foreach (VSLangProj.Reference reference in project.References)
        {
          // exclude references that are part of .net core
          if (!reference.Path.StartsWith(DOTNET_INSTALL_PATH, StringComparison.OrdinalIgnoreCase) && !IsShippedWithNucleus(reference, nucleusReferencePath))
          {
            Debug.WriteLine($"{reference.Name} {reference.Version} {reference.Description} {reference.Identity} {reference.Path}");
            references.Add(reference);
          }
        }
      }

      List<ManifestFile> manifestFiles = manifest.Files(Manifest.ManifestFilesFilters.Binaries);

      // get a list of references which are already in the manifest
      IEnumerable<VSLangProj.Reference> existingReferences = references
        .Where(reference => manifestFiles.Any(manifestFile => System.IO.Path.GetFileName(manifestFile.FileName).Equals(reference.AssemblyFileName(), StringComparison.OrdinalIgnoreCase)));

      // get a list of references which are not in the manifest
      IEnumerable<Models.ProjectReference> newReferences = references
        .Where(reference => !manifestFiles.Any(manifestFile => System.IO.Path.GetFileName(manifestFile.FileName).Equals(reference.AssemblyFileName(), StringComparison.OrdinalIgnoreCase)))
        .Select(reference => new Models.ProjectReference(reference));

      // display the references selection dialog
      Views.AddReferencesToPackage view = new();
      ViewModels.AddReferencesToPackage viewModel = new() { NewReferences = newReferences, ExistingReferences = existingReferences };

      view.DataContext = viewModel;
      view.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner;

      if (view.ShowModal() == true)
      {
        // Add selected references to the manifest
        foreach (Models.ProjectReference referenceToAdd in viewModel.NewReferences.Where(newReference => newReference.IsSelected))
        {
          if (!manifestFiles.Where(file => file.FileName.Equals(referenceToAdd.AssemblyFileName)).Any())
          {
            System.Xml.Linq.XElement addedElement = manifest.AddFile("bin\\" + referenceToAdd.AssemblyFileName);
          }
        }

        // update package.xml with the changed manifest contents
        await this.DTE.UpdateManifest(manifest);
      }
    }
  }

  /// <summary>
  /// Signal whether the command is available for the current selection.
  /// </summary>
  /// <param name="e"></param>
  protected override void BeforeQueryStatus(EventArgs e)
  {
    ThreadHelper.ThrowIfNotOnUIThread();
    this.Command.Visible = CanExecute();
    this.Command.Enabled = !AreAllReferencesInManifest() && this.Command.Visible;

    base.BeforeQueryStatus(e);
  }

  /// <summary>
  /// Return whether the command is available for the current selection.
  /// </summary>
  /// <param name="e"></param>
  private Boolean CanExecute()
  {
    try
    {
      return this.DTE.IsNucleusProject();
    }
    catch (System.IO.FileNotFoundException)
    {
      // if package.xml was not found, treat the project as "not a nucleus extension". No error is reported, 
      // we just don't enable the command.
      return false;
    }
    catch (Exception ex)
    {
      this.ShowInfoBarMessage(ex.Message);
    }
    return false;
  }

  /// <summary>
  /// Returns whether all references are in the package manifest already.
  /// </summary>
  /// <returns></returns>
  private Boolean AreAllReferencesInManifest()
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    try
    {
      Manifest manifest = this.DTE.GetManifest();

      string nucleusReferencePath = GetReferencePath();

      if (String.IsNullOrEmpty(nucleusReferencePath)) return false;

      if (this.DTE.SelectedItems.Item(1).Project?.Object is not VSLangProj.VSProject project) return false;

      List<ManifestFile> manifestFiles = manifest.Files(Manifest.ManifestFilesFilters.Binaries);
      foreach (VSLangProj.Reference reference in project.References)
      {
        // find any references that are not part of .net core, are not shipped with Nucleus, and are not already in the manifest
        if (!reference.Path.StartsWith(DOTNET_INSTALL_PATH, StringComparison.OrdinalIgnoreCase) && !manifestFiles.Where(file => System.IO.Path.GetFileName(file.FileName).Equals(reference.AssemblyFileName(), StringComparison.OrdinalIgnoreCase)).Any() && !IsShippedWithNucleus(reference, nucleusReferencePath))
        {
          return false;
        }
      }
    }
    catch (InvalidOperationException)
    {
      return false;
    }

    return true;
  }

  /// <summary>
  /// Return whether the specified reference is shipped in the Nucleus install.
  /// </summary>
  /// <param name="reference"></param>
  /// <param name="nucleusReferencePath"></param>
  /// <returns></returns>
  private Boolean IsShippedWithNucleus(VSLangProj.Reference reference, string nucleusReferencePath)
  {
    string assemblyFullPath = System.IO.Path.Combine(nucleusReferencePath, reference.AssemblyFileName());

    if (System.IO.File.Exists(assemblyFullPath))
    {
      return System.Reflection.AssemblyName.GetAssemblyName(assemblyFullPath).Version >= System.Version.Parse(reference.Version);
    }

    return false;
  }

  /// <summary>
  /// Return the full path to the installed reference copy of Nucleus.  If the reference path can't be determined, return an empty string.
  /// </summary>
  /// <returns></returns>
  /// <remarks>
  /// The return value is used to determine whether references are shipped as part of the Nucleus install and therefore don't need to 
  /// be added to the manifest.
  /// </remarks>
  private string GetReferencePath()
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    string nucleusPath = Environment.GetEnvironmentVariable(NUCLEUS_PATH_ENV_VARIABLE);
    string targetFramework = this.DTE.GetSelectedProject().Properties.Item("FriendlyTargetFramework")?.Value?.ToString();
    string activeConfiguration = this.DTE.GetSelectedProject().ConfigurationManager.ActiveConfiguration.ConfigurationName;

    if (!String.IsNullOrEmpty(nucleusPath) && !String.IsNullOrEmpty(targetFramework) && !String.IsNullOrEmpty(activeConfiguration))
    {
      foreach (string path in NUCLEUS_REFERENCE_SEARCH_PATHS)
      {
        string searchPath = System.IO.Path.Combine(nucleusPath, path.Replace("{environment}", activeConfiguration + "\\" + targetFramework));
        if (System.IO.Directory.Exists(searchPath) && System.IO.File.Exists(System.IO.Path.Combine(searchPath, NUCLEUS_MAIN_ASSEMBLY_NAME)))
        {
          return searchPath;
        }
      }
    }
    return "";
  }
}

using EnvDTE;
using System.Collections.Generic;
using Nucleus.DeveloperTools.Shared;
using System.Linq;

namespace Nucleus.DeveloperTools.VisualStudio.Commands;

[Command(PackageIds.AddToPackageCommand)]
internal sealed class AddToPackageCommand : BaseCommand<AddToPackageCommand>
{
  private EnvDTE80.DTE2 DTE => this.Package.GetService<DTE, DTE>() as EnvDTE80.DTE2;

  protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
  {
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

    string projectFolder = this.DTE.GetProjectPath();

    if (String.IsNullOrEmpty(projectFolder)) return;

    List<string> selectedFiles = this.DTE.GetSelectedFiles(projectFolder);

    if (selectedFiles.Any())
    {
      Manifest manifest = this.DTE.GetManifest();
      if (!manifest.IsValidPackage()) return;

      List<ManifestFile> manifestFiles = manifest.Files(Manifest.ManifestFilesFilters.ContentFiles);

      foreach (string fileToAdd in selectedFiles)
      {
        if (IsValidFileType(fileToAdd) &&  !manifestFiles.Where(file => file.FileName.Equals(fileToAdd)).Any())
        {
          System.Xml.Linq.XElement addedElement = manifest.AddFile(fileToAdd);
        }
      }

      await this.DTE.UpdateManifest(manifest);
    }
  }
  
  protected override void BeforeQueryStatus(EventArgs e)
  {
    ThreadHelper.ThrowIfNotOnUIThread();
    this.Command.Visible = CanExecute();

    base.BeforeQueryStatus(e);
  }  

  public Boolean CanExecute()
  {
    try
    {
      ThreadHelper.ThrowIfNotOnUIThread();
      return this.DTE.IsNucleusProject() && AreAnySelectedFilesValidForManifest() && !AreAllSelectedFilesInManifest();
    }
    catch (System.IO.FileNotFoundException)
    {
      // if package.xml was not found treat the project as "not a nucleus extension", so no error is reported, 
      // we just don't enable any Nucleus extensions.
      return false;
    }
    catch (Exception ex)
    {
      this.ShowInfoBarMessage(ex.Message);
    }
      
    return false;
  }

  

  /// <summary>
  /// Returns whether all selected files ares in the package manifest already.
  /// </summary>
  /// <returns></returns>
  private Boolean AreAllSelectedFilesInManifest()
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    string projectFolder = this.DTE.GetProjectPath();

    if (!String.IsNullOrEmpty(projectFolder))
    {        
      Manifest manifest = this.DTE.GetManifest();

      List<ManifestFile> manifestFiles = manifest.Files(Manifest.ManifestFilesFilters.ContentFiles);
      List<string> packageFileNames = manifestFiles.Select(file => file.FileName).ToList();
      List<string> selectedFileNames = this.DTE.GetSelectedFiles(projectFolder);

      foreach (string selectedFile in selectedFileNames)
      {
        if (!packageFileNames.Contains(selectedFile))
        {
          return false;
        }
      }
    }

    return true;
  }

  /// <summary>
  /// Returns whether any of the selected file are in the package manifest.
  /// </summary>
  /// <returns></returns>
  private Boolean AreAnySelectedFilesValidForManifest()
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    string projectFolder = this.DTE.GetProjectPath();

    if (!String.IsNullOrEmpty(projectFolder))
    {
      foreach(string file in this.DTE.GetSelectedFiles(projectFolder))
      {
        if (IsValidFileType(file)) return true;
      }
    }
    return false;
  }

  /// <summary>
  /// Return whether the file has a type which can be added to the manifest (package.xml).
  /// </summary>
  /// <param name="fileName"></param>
  /// <returns></returns>
  Boolean IsValidFileType(string fileName)
  {
    // package.xml should not be added to package.xml
    if (fileName.Equals(DteExtensions.MANIFEST_FILENAME, StringComparison.OrdinalIgnoreCase)) return false;

    switch (System.IO.Path.GetExtension(fileName).ToLower())
    {
      case ".cshtml":
      case ".css":
      case ".txt":
      case ".xml":
      case ".json":
      case ".png":
      case ".jpg":
      case ".gif":
      case ".js":
      case ".webp":
      case ".md":
        return true;

      default:
        return false;
    }
  }
}

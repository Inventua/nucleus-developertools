using EnvDTE;
using Microsoft.VisualStudio.Imaging;
using System.Collections.Generic;
using Nucleus.DeveloperTools.Shared;
using System.Linq;
using Microsoft.VisualStudio;
using Community.VisualStudio.Toolkit;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Shell;
using EnvDTE80;
using Microsoft.VisualStudio.PlatformUI;

namespace Nucleus.DeveloperTools.VisualStudio.Commands;

[Command(PackageIds.AddToPackageCommand)]
internal sealed class AddToPackageCommand : BaseCommand<AddToPackageCommand>
{
  private const string MANIFEST_FILENAME = "package.xml";

  private static readonly Guid PhysicalFile_guid = Guid.Parse("6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C");
  private static readonly Guid PhysicalFolder_guid =  Guid.Parse("6BB5F8EF-4483-11D3-8BCF-00C04F8EC28C");

  private InfoBar InfoBar { get; set; }
  private Boolean IsShowingInfoBar { get; set; }
  private readonly object lockObj = new();

  private EnvDTE80.DTE2 DTE
  {
    get
    {
      return this.Package.GetService<DTE, DTE>() as EnvDTE80.DTE2;
    }
  }

  protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
  {
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

    string projectFolder = GetProjectPath();
    List<string> selectedFiles = GetSelectedFiles(projectFolder);

    if (selectedFiles.Any())
    {
      string manifestFilePath = System.IO.Path.Combine(projectFolder, MANIFEST_FILENAME);

      System.Xml.Linq.XElement addedElement = null;

      // open the package file and use its contents to create a manifest object.  This handles cases where
      // the user has package.xml open & has edited it but has not saved changes
      DocumentView view = await VS.Documents.OpenViaProjectAsync(manifestFilePath);
      Manifest manifest = Manifest.FromString(view.TextBuffer.CurrentSnapshot.GetText());
      List<ManifestFile> manifestFiles = manifest.Files;

      if (!manifest.IsValidPackage()) return;

      foreach (string fileToAdd in selectedFiles)
      {
        if (IsValidFile(fileToAdd) &&  !manifestFiles.Where(file => file.FileName.Equals(fileToAdd)).Any())
        {
          addedElement = manifest.AddFile(fileToAdd);
        }
      }

      // update package.xml with the changed manifest contents
      view.Document.TextBuffer.Replace(new Microsoft.VisualStudio.Text.Span(0, view.Document.TextBuffer.CurrentSnapshot.Length), manifest.ToString());

      // use the visual studio "format document" command to auto-format the document, because the manifest class (XDocument) does not 
      // "pretty format" with tabs and white space.
      await view.WindowFrame.ShowAsync();

      // The delay is needed because WindowFrame.ShowAsync does not execute immediately, and the format document command doesn't work unless
      // the windows is visible and activated.
      JoinableTask task = ThreadHelper.JoinableTaskFactory.StartOnIdle(async () =>
      {
        await Task.Delay(100);
        await VS.Commands.ExecuteAsync(VSConstants.VSStd2KCmdID.FORMATDOCUMENT, "-1");
      });

    }
  }

  protected override void BeforeQueryStatus(EventArgs e)
  {
    ThreadHelper.ThrowIfNotOnUIThread();
    this.Command.Visible = CanExecute();

    base.BeforeQueryStatus(e);
  }

  private string GetProjectPath()
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    EnvDTE80.DTE2 dte = this.DTE;
    if (dte.SelectedItems.Count > 0)
    {
      return System.IO.Path.GetDirectoryName(dte.SelectedItems.Item(1).ProjectItem.ContainingProject.FullName);
    }

    return "";
  }

  private List<string> GetSelectedFiles(string projectPath)
  {
    ThreadHelper.ThrowIfNotOnUIThread();
    
    List<string> results = new();
    EnvDTE80.DTE2 dte = this.DTE;
    if (dte.SelectedItems.Count > 0)
    {
      // get a list of selected files 

      // dte.SelectedItems is 1-based not 0-based.  
      for (int index = 1; index <= dte.SelectedItems.Count; index++)
      {
        SelectedItem selectedItem = dte.SelectedItems.Item(index);

        if (Guid.Parse(selectedItem.ProjectItem.Kind) == PhysicalFolder_guid) 
        {
          results.AddRange(GetFolderFiles(dte, selectedItem.ProjectItem, projectPath));
        }
        else if (Guid.Parse(selectedItem.ProjectItem.Kind) == PhysicalFile_guid)
        {          
          results.Add(GetRelativePath(selectedItem.ProjectItem, projectPath));
        }
      }
    }

    return results;
  }

  private List<string> GetFolderFiles(EnvDTE80.DTE2 dte, ProjectItem folderItem, string projectPath)
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    List<string> results = new();

    foreach (ProjectItem item in folderItem.ProjectItems)
    {
      if (Guid.Parse(item.Kind) == PhysicalFolder_guid)
      {
        results.AddRange(GetFolderFiles(dte, item, projectPath));
      }
      else if (Guid.Parse(item.Kind) == PhysicalFile_guid)
      {
        results.Add(GetRelativePath(item, projectPath));
      }
    }

    return results;
  }

  private string GetRelativePath(ProjectItem projectItem, string projectPath)
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    string selectedItemFullPath = (string)projectItem.Properties.Item("LocalPath").Value;
    return selectedItemFullPath.Substring(projectPath.Length + 1);
  }

  public Boolean CanExecute()
  {
    try
    {
      return AreAnySelectedFilesValidForManifest() && !AreAllSelectedFilesInManifest();
    }
    catch (System.IO.FileNotFoundException)
    {
      // if package.xml was not found treat the project as "not a nucleus extension", so no error is reported, 
      // we just don't enable any Nucleus extensions.
      return false;
    }
    catch (Exception ex)
    {
      // the lock and IsVisible check are to prevent the error message from being shown multiple times, because
      // BeforeQueryStatus is called several times when the user right-clicks an item in the solution explorer.
      // Because BeforeQueryStatus is not async, we can't await the task, so this function returns before the 
      if (!this.IsShowingInfoBar)
      {
        lock (lockObj)
        {
          if (!this.IsShowingInfoBar)
          {
            this.IsShowingInfoBar = true;
            Task task = Task.Run(async () =>
            {
              await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
              this.InfoBar ??= await CreatePackageErrorInfoBarAsync(ex.Message);

              if (!this.InfoBar.IsVisible)
              {
                await this.InfoBar.TryShowInfoBarUIAsync();
              }
              this.IsShowingInfoBar = false;
            });
          }
        }
      }
    }
    return false;
  }

  private async Task<InfoBar> CreatePackageErrorInfoBarAsync(string message)
  {
    return await Community.VisualStudio.Toolkit.VS.InfoBar.CreateAsync
    (
      ToolWindowGuids80.SolutionExplorer,
      new InfoBarModel
      (
        new[]
        {
          new InfoBarTextSpan($"{MANIFEST_FILENAME}: {message}")
        },
        KnownMonikers.PlayStepGroup,
        true
      )
    );
  }

  /// <summary>
  /// Returns whether all selected files ares in the package manifest already.
  /// </summary>
  /// <returns></returns>
  private Boolean AreAllSelectedFilesInManifest()
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    string projectFolder = this.GetProjectPath();

    if (!String.IsNullOrEmpty(projectFolder))
    {
      
      DocumentView view = ThreadHelper.JoinableTaskFactory.Run(async delegate
      {
        string manifestFilePath = System.IO.Path.Combine(projectFolder, MANIFEST_FILENAME);
        return await VS.Documents.OpenViaProjectAsync(manifestFilePath);
      });

      Manifest manifest = Manifest.FromString(view.TextBuffer.CurrentSnapshot.GetText());
      List<string> packageFiles = manifest.Files.Select(file => file.FileName).ToList();
      List<string> selectedFiles = GetSelectedFiles(projectFolder);

      foreach (string selectedFile in selectedFiles)
      {
        if (!packageFiles.Contains(selectedFile))
        {
          return false;
        }
      }
    }

    return true;
  }


  /// <summary>
  /// Returns whether the selected file is in the package manifest.
  /// </summary>
  /// <returns></returns>
  private Boolean AreAnySelectedFilesValidForManifest()
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    string projectFolder = GetProjectPath();

    if (!String.IsNullOrEmpty(projectFolder))
    {
      foreach(string file in GetSelectedFiles(projectFolder))
      {
        if (IsValidFile(file)) return true;
      }
    }
    return false;
  }

  Boolean IsValidFile(string fileName)
  {
    // package.xml should not be added to package.xml!
    if (fileName.Equals(MANIFEST_FILENAME, StringComparison.OrdinalIgnoreCase)) return false;

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

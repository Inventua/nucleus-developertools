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

namespace Nucleus.DeveloperTools.VisualStudio.Commands;

[Command(PackageIds.AddToPackageCommand)]
internal sealed class AddToPackageCommand : BaseCommand<AddToPackageCommand>
{
  private const string MANIFEST_FILENAME = "package.xml";

  private DateTime? _packageReadDate { get; set; }
  private List<string> _packageFiles { get; set; }

  private InfoBar InfoBar { get; set; }
  private Boolean IsShowingInfoBar { get; set; }
  private object lockObj = new();

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

      Manifest manifest;
      System.Xml.Linq.XElement addedElement = null;

      // open the package file and use its contents to create a manifest object.  This handles cases where
      // the user has package.xml open & has edited it but has not saved changes
      DocumentView view = await VS.Documents.OpenViaProjectAsync(manifestFilePath);      
      manifest = Manifest.FromString(view.TextBuffer.CurrentSnapshot.GetText());

      if (!manifest.IsValidPackage()) return;

      foreach (string fileToAdd in selectedFiles)
      {
        if (!manifest.Files.Where(file => file.FileName.Equals(selectedFiles)).Any())
        {
          addedElement = manifest.AddFile(fileToAdd.Substring(projectFolder.Length + 1));
        }
      }

      // update package.xml with the changed manifest contents
      view.Document.TextBuffer.Replace(new Microsoft.VisualStudio.Text.Span(0, view.Document.TextBuffer.CurrentSnapshot.Length), manifest.ToString());
                  
      // use the visual studio "format document" command to auto-format the document, because the manifest class (XDocument) does not 
      // "pretty format" with tabs and white space.
      await view.WindowFrame.ShowAsync();
      await VS.Commands.ExecuteAsync(VSConstants.VSStd2KCmdID.FORMATDOCUMENT, "-1");         
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

    List<string> results = new List<string>();
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

    List<string> results = new List<string>();
    EnvDTE80.DTE2 dte = this.DTE;
    if (dte.SelectedItems.Count > 0)
    {

      // get a list of files to add to the manifest
      // dte.SelectedItems is 1-based not 0-based.  
      for (int index = 1; index <= dte.SelectedItems.Count; index++)
      {
        SelectedItem selectedItem = dte.SelectedItems.Item(index);
        string selectedItemFullPath = (string)selectedItem.ProjectItem.Properties.Item("LocalPath").Value;
        string selectedItemRelativePath = selectedItemFullPath.Substring(projectPath.Length + 1, selectedItemFullPath.Length - projectPath.Length - 1);
        results.Add(selectedItemFullPath);
      }
    }

    return results;
  }

  public Boolean CanExecute()
  {
    ThreadHelper.ThrowIfNotOnUIThread();
    try
    {
      return !AreAllSelectedFilesInManifest();
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
              if (this.InfoBar == null)
              {
                this.InfoBar = await CreatePackageErrorInfoBarAsync(ex.Message);
              }

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
  /// Returns whether the selected file is in the package manifest.
  /// </summary>
  /// <returns></returns>
  private Boolean AreAllSelectedFilesInManifest()
  {
    ThreadHelper.ThrowIfNotOnUIThread();
    EnvDTE80.DTE2 dte = this.DTE;
    string projectFolder;
    string manifestFilePath;

    if (dte.SelectedItems.Count == 0)
    {
      return false;
    }

    projectFolder = System.IO.Path.GetDirectoryName(dte.SelectedItems.Item(1).ProjectItem.ContainingProject.FullName);
    manifestFilePath = System.IO.Path.Combine(projectFolder, MANIFEST_FILENAME);
    List<string> packageFiles = this.GetPackageFiles(manifestFilePath);

    // dte.SelectedItems is 1-based not 0-based.  Return false if *any* of the selected items are not in the manifest.
    for (int index = 1; index <= dte.SelectedItems.Count; index++)
    {
      SelectedItem selectedItem = dte.SelectedItems.Item(index);

      string selectedItemFullPath = (string)selectedItem.ProjectItem.Properties.Item("LocalPath").Value;
      string selectedItemRelativePath = selectedItemFullPath.Substring(projectFolder.Length + 1, selectedItemFullPath.Length - projectFolder.Length - 1);

      if (!packageFiles.Contains(selectedItemRelativePath))
      {
        return false;
      }
    }

    return true;
  }


  List<string> GetPackageFiles(string filename)
  {
    System.IO.FileInfo fileInfo = new System.IO.FileInfo(filename);

    if (!this._packageReadDate.HasValue || this._packageReadDate.Value < fileInfo.LastWriteTimeUtc)
    {
      this._packageFiles = ReadManifestFiles(filename);
      this._packageReadDate = DateTime.UtcNow;
    }

    return this._packageFiles;
  }

  /// <summary>
  /// Read the manifest (package.xml) file.
  /// </summary>
  /// <param name="filename"></param>
  /// <returns></returns>
  private List<string> ReadManifestFiles(string filename)
  {
    Manifest manifest = Manifest.FromFile(filename);
    return manifest.Files.Select(file => file.FileName).ToList();
  }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using EnvDTE;
using EnvDTE80;
using Nucleus.DeveloperTools.Shared;

namespace Nucleus.DeveloperTools.VisualStudio.Commands;

internal static class DteExtensions  
{
  public const string MANIFEST_FILENAME = "package.xml";

  private static readonly Guid PhysicalFile_guid = Guid.Parse("6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C");
  private static readonly Guid PhysicalFolder_guid = Guid.Parse("6BB5F8EF-4483-11D3-8BCF-00C04F8EC28C");

  /// <summary>
  /// Return the full path to the project file for the selected item.
  /// </summary>
  /// <param name="dte"></param>
  /// <returns></returns>
  /// <remarks>
  /// This function can return an empty string if there is no item selected or the selected item is not related to a project.
  /// </remarks>
  public static string GetProjectPath(this EnvDTE80.DTE2 dte)
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    EnvDTE.Project project = GetSelectedProject(dte);
    if (project == null) return "";
    return System.IO.Path.GetDirectoryName(project.FullName);

    // project item(s) are selected
    //if (dte.SelectedItems.Count > 0 && !String.IsNullOrEmpty(dte.SelectedItems?.Item(1)?.ProjectItem?.ContainingProject?.FullName))
    //{
    //  return System.IO.Path.GetDirectoryName(dte.SelectedItems.Item(1).ProjectItem.ContainingProject.FullName);
    //}

    // todo: https://github.com/VsixCommunity/Community.VisualStudio.Toolkit/blob/master/demo/VSSDK.TestExtension/Commands/EditSelectedItemLabelCommand.cs


    //if (dte.SelectedItems.Count > 0 && !String.IsNullOrEmpty(dte.SelectedItems?.Item(1).Project?.FullName))
    //{
    //  return System.IO.Path.GetDirectoryName(dte.SelectedItems.Item(1).Project.FullName);
    //}

    //return "";
  }

  /// <summary>
  /// Read the manifest (package.xml) from an open view, if it is open in the editor, otherwise return it from the file contents.
  /// </summary>
  /// <param name="dte"></param>
  /// <returns></returns>
  public static Manifest GetManifest(this EnvDTE80.DTE2 dte)
  {
    ThreadHelper.ThrowIfNotOnUIThread();
    string projectPath = dte.GetProjectPath();

    // immediately after executing a command, Visual studio can call .BeforeQueryStatus (which calls this) when there is no current selection 
    if (String.IsNullOrEmpty(projectPath)) throw new InvalidOperationException("Project not selected");

#pragma warning disable VSTHRD102 // Implement internal logic asynchronously  
      string manifestFilePath = System.IO.Path.Combine(dte.GetProjectPath(), DteExtensions.MANIFEST_FILENAME);
      
    DocumentView view = ThreadHelper.JoinableTaskFactory.Run(async delegate
    {      
      return await VS.Documents.GetDocumentViewAsync(manifestFilePath);      
    });
#pragma warning restore VSTHRD102 // Implement internal logic asynchronously

    if (view != null)
    {
      return Manifest.FromString(view.TextBuffer.CurrentSnapshot.GetText());
    }
    else
    {
      return Manifest.FromFile(manifestFilePath);
    }
  }

  public static async Task UpdateManifest(this EnvDTE80.DTE2 dte, Manifest manifest)
  {
    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

    string manifestFilePath = System.IO.Path.Combine(GetProjectPath(dte), DteExtensions.MANIFEST_FILENAME);

    // update package.xml with the changed manifest contents
    DocumentView view = await VS.Documents.OpenViaProjectAsync(manifestFilePath);
    await view.WindowFrame.ShowAsync();
    view.Document.TextBuffer.Replace(new Microsoft.VisualStudio.Text.Span(0, view.Document.TextBuffer.CurrentSnapshot.Length), manifest.ToString());
  }

  public static Boolean IsProjectNodeSelected(this DTE2 dte)
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    return (dte.SelectedItems.Count == 1 && dte.SelectedItems.Item(1).ProjectItem.Kind == "project");
  }

  public static EnvDTE.Project GetSelectedProject(this DTE2 dte)
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    return dte.SelectedItems?.Item(1)?.Project;
  }

  public static List<string> GetSelectedFiles(this DTE2 dte, string projectPath)
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    List<string> results = [];
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


  /// <summary>
  /// Returns whether the project has a package.xml file at the project root.
  /// </summary>
  /// <returns></returns>
  public static Boolean IsNucleusProject(this DTE2 dte)
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    if (dte.SelectedItems.Count > 0 && !String.IsNullOrEmpty(dte.SelectedItems?.Item(1).Project?.FullName))
    {
      EnvDTE.Project project = dte.SelectedItems?.Item(1).Project;
      foreach (EnvDTE.ProjectItem item in project.ProjectItems)
      {
        if (item.Name.Equals(MANIFEST_FILENAME, StringComparison.OrdinalIgnoreCase))
        {
          return true;
        }
      }
    }

    return false;
  }

  public static List<string> GetFolderFiles(this EnvDTE80.DTE2 dte, ProjectItem folderItem, string projectPath)
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    List<string> results = [];

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

  private static string GetRelativePath(ProjectItem projectItem, string projectPath)
  {
    ThreadHelper.ThrowIfNotOnUIThread();

    string selectedItemFullPath = (string)projectItem.Properties.Item("LocalPath").Value;
    return selectedItemFullPath.Substring(projectPath.Length + 1);
  }
}

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using System.Xml;
using Nucleus.DeveloperTools.MSBuild.Properties;
using Microsoft.Build.Framework;
using Nucleus.DeveloperTools.Shared;

namespace Nucleus.DeveloperTools.MSBuild
{
  public class CheckMissingNucleusContent : Microsoft.Build.Utilities.Task
  {
    [Required]
    public ITaskItem PackageFile { get; set; }

    [Required]
    public ITaskItem[] ProjectContent { get; set; }

    [Required]
    public ITaskItem[] ProjectEmbedded { get; set; }

    public override bool Execute()
    {
      Manifest manifest = Manifest.FromFile(this.PackageFile.ItemSpec);
      List<ManifestFile> packageContent = manifest.Files(Manifest.ManifestFilesFilters.ContentFiles | Manifest.ManifestFilesFilters.EmbeddedFiles);
      List<string> projectContent = this.ProjectContent.Select(item => item.ToString()).ToList();
      List<string> projectEmbedded = this.ProjectEmbedded.Select(item => item.ToString()).ToList();

      // check for content files which are in the project, but not in the package
      foreach (string missingItem in projectContent.Where(item => !packageContent.Where(packageItem => packageItem.FileName.Equals(item, StringComparison.OrdinalIgnoreCase)).Any()))
      {
        if (!missingItem.Equals(this.PackageFile.ItemSpec, StringComparison.OrdinalIgnoreCase))
        {
          this.LogWarning(this.PackageFile.ItemSpec, Resources.NUCL112_CODE, missingItem);
        }
      }

      // check for files which are in the package, but are not in the project/not marked as content
      foreach (ManifestFile missingItem in packageContent.Where(item => !projectContent.Contains(item.FileName, StringComparer.OrdinalIgnoreCase)))
      {
        // check that the file is also missing from the list of embedded files
        if (!projectEmbedded.Any(embeddedItem => embeddedItem.Equals(missingItem.FileName, StringComparison.OrdinalIgnoreCase)))
        {
          this.LogWarning(this.PackageFile.ItemSpec, Resources.NUCL113_CODE, missingItem.Element, missingItem.FileName);
        }
      }

      return true;
    }
  }
}

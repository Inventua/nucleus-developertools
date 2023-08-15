global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using System;
global using Task = System.Threading.Tasks.Task;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio;
using System.Runtime.InteropServices;
using System.Threading;

/// Install the "Extensibility Essentials 2022" extensions.
/// https://marketplace.visualstudio.com/items?itemName=MadsKristensen.ExtensibilityEssentials2022

namespace Nucleus.DeveloperTools.VisualStudio.Commands;

[PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
[InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
[ProvideMenuResource("Menus.ctmenu", 1)]
[Guid(PackageGuids.NucleusCommandsPackageString)]
[ProvideAutoLoad(Microsoft.VisualStudio.Shell.Interop.UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
public sealed class CommandsPackage : ToolkitPackage, IOleCommandTarget
{
  protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
  {
    await this.RegisterCommandsAsync();
  }
}
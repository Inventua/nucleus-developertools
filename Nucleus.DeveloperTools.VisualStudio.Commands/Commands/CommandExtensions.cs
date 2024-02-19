using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Imaging;
using Microsoft.VisualStudio.Shell.Interop;

namespace Nucleus.DeveloperTools.VisualStudio.Commands;
internal static class CommandExtensions
{
  private static InfoBar InfoBar { get; set; }
  private static Boolean IsShowingInfoBar { get; set; }
  private static readonly object lockObj = new();

#pragma warning disable IDE0060 // Remove unused parameter
  public static void ShowInfoBarMessage(this BaseCommand command, string message)
#pragma warning restore IDE0060 // Remove unused parameter
  {
    if (!IsShowingInfoBar)
    {
      lock (lockObj)
      {
        if (!IsShowingInfoBar)
        {
          IsShowingInfoBar = true;
          Task task = Task.Run(async () =>
          {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            InfoBar ??= await CreatePackageErrorInfoBarAsync(message);

            if (!InfoBar.IsVisible)
            {
              await InfoBar.TryShowInfoBarUIAsync();
            }
            IsShowingInfoBar = false;
          });
        }
      }
    }
  }

  private static async Task<InfoBar> CreatePackageErrorInfoBarAsync(string message)
  {
    return await Community.VisualStudio.Toolkit.VS.InfoBar.CreateAsync
    (
      ToolWindowGuids80.SolutionExplorer,
      new InfoBarModel
      (
        new[]
        {
          new InfoBarTextSpan($"{DteExtensions.MANIFEST_FILENAME}: {message}")
        },
        KnownMonikers.PlayStepGroup,
        true
      )
    );
  }
}

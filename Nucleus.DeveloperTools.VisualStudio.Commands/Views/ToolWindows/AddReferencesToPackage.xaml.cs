using Microsoft.VisualStudio.PlatformUI;

namespace Nucleus.DeveloperTools.VisualStudio.Commands.Views;
public partial class AddReferencesToPackage : DialogWindow
{
  public AddReferencesToPackage()
  {
    InitializeComponent();
  }

  private void Cancel_Clicked(object sender, System.Windows.RoutedEventArgs e)
  {
    this.DialogResult = false;
    this.Close();
  }

  private void OK_Clicked(object sender, System.Windows.RoutedEventArgs e)
  {
    this.DialogResult = true;
    this.Close();
  }
}

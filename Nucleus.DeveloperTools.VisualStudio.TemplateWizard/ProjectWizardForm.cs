using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Nucleus.DeveloperTools.VisualStudio.TemplateWizard
{
  public partial class ProjectWizardForm : Form
  {   
    public void SetProjectType(string type)
    {
      switch (type)
      {
        case "Complex Extension":
          this.ClassNameEnabled = true;
          break;

        case "Simple Extension":
          this.ClassNameEnabled = false;
          break;
        
        case "Empty Extension":
          this.ClassNameEnabled = false;
          break;
        
        case "Layout Extension":
          this.ClassNameEnabled = false;
          this.ExtensionNamespaceEnabled = false;
          break;
      }
    }

    public string ExtensionNamespace
    {
      get
      {
        return this.txtExtensionNamespace.Text;
      }
      set
      {
        this.txtExtensionNamespace.Text = value;
      }
    }

    public Boolean ExtensionNamespaceEnabled
    {
      get
      {
        return this.txtExtensionNamespace.Visible;
      }
      set
      {
        this.txtExtensionNamespace.Visible = value;
        this.lblExtensionNamespace.Visible = value;
      }
    }

    public string ExtensionName
    {
      get
      {
        return this.txtExtensionName.Text;
      }
      set
      {
        this.txtExtensionName.Text = value;
      }
    }

    public string FriendlyName
    {
      get
      {
        return this.txtFriendlyName.Text;
      }
      set
      {
        this.txtFriendlyName.Text = value;
      }
    }

    public Boolean ClassNameEnabled
    {
      get
      {
        return this.txtModelName.Visible;
      }
      set
      {
        this.txtModelName.Visible = value;
        this.lblModelName.Visible = value;
      }
    }

    public string ModelClassName
    {
      get
      {
        return this.txtModelName.Text;
      }
      set
      {
        this.txtModelName.Text = value;
      }
    }

    public string ExtensionDescription
    {
      get
      {
        return this.txtExtensionDescription.Text;
      }
      set
      {
        this.txtExtensionDescription.Text = value;
      }
    }

    public string PublisherName
    {
      get
      {
        return this.txtPublisherName.Text;
      }
      set
      {
        this.txtPublisherName.Text = value;
      }
    }

    public string PublisherUrl
    {
      get
      {
        return this.txtPublisherUrl.Text;
      }
      set
      {
        this.txtPublisherUrl.Text = value;
      }
    }

    public string PublisherEmail
    {
      get
      {
        return this.txtPublisherEmail.Text;
      }
      set
      {
        this.txtPublisherEmail.Text = value;
      }
    }

    public string SourceLocation
    {
      get
      {
        return this.tipLocation.ToolTipTitle;
      }
      set
      {
        tipLocation.SetToolTip(this.lblVersion, value);
      }
    }
    public ProjectWizardForm()
    {
      InitializeComponent();
      this.lblVersion.Text = this.GetType().Assembly.GetName().Version.ToString();
    }

    private void cmdNext_Click(object sender, EventArgs e)
    {
      
    }

    private void txtExtensionName_Validating(object sender, CancelEventArgs e)
    {
      if (!this.txtExtensionName.Text.Validate(true))
      {        
        MessageBox.Show("Extension names can contain letters, numbers, dots and the underscore character.","Invalid Characters", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        e.Cancel = true;
      }
    }

    private void txtModelName_Validating(object sender, CancelEventArgs e)
    {
      if (!this.txtModelName.Text.Validate(false))
      {
        MessageBox.Show("Model class names can contain letters, numbers and the underscore character.", "Invalid Characters", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        e.Cancel = true;
      }
    }

    private void txtExtensionNamespace_Validating(object sender, CancelEventArgs e)
    {
      if (!this.txtExtensionNamespace.Text.Validate(true))
      {
        MessageBox.Show("Namespace can contain letters, numbers, dots and the underscore character.", "Invalid Characters", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
        e.Cancel = true;
      }
    }

    private void cmdBack_Click(object sender, EventArgs e)
    {
      // prevent validation when cancelling the dialog
      this.AutoValidate = AutoValidate.Disable;
    }
  }
}
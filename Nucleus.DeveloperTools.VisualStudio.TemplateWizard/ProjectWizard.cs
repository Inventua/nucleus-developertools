using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TemplateWizard;
using System.Windows.Forms;
using EnvDTE;
using System.Linq;
using EnvDTE80;

namespace Nucleus.DeveloperTools.VisualStudio.TemplateWizard
{
  /// <summary>
  /// IWizard implementation for Nucleus templates.  Handles Nucleus project and item templates.
  /// </summary>
	public class ProjectWizard : IWizard
	{
		public void BeforeOpeningFile(ProjectItem projectItem)
		{
		}

    /// <summary>
    /// This function is called after a project template has been run to generate a project.  It auto-formats 
    /// all project items (files) to compensate for an issue where Visual Studio doesn't always apply the 
    /// users preference for tab spacing.
    /// </summary>
    /// <param name="project"></param>
    /// <remarks>
    /// Only source files which are open in the editor can be formatted.  Other project items throw an exception, 
    /// which is handled/ignored.
    /// </remarks>
		public void ProjectFinishedGenerating(Project project)
		{
      try
      {
			  Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
        foreach (ProjectItem projectItem in project.ProjectItems)
        {
          ProjectItemFinishedGenerating(projectItem);
        }        
      }
      catch (Exception)
      {
        // this is a non-critical function, ignore errors
      }
		}

    /// <summary>
    /// This function is called after an item template has been run to generate a single project item.  It auto-formats 
    /// the specified project (source code file) to compensate for an issue where Visual Studio doesn't always apply the 
    /// users preference for tab spacing.
    /// </summary>
		public void ProjectItemFinishedGenerating(ProjectItem projectItem)
		{
      try
      {
        Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
        projectItem.Document.DTE.ExecuteCommand("Edit.FormatDocument");
      }
      catch (Exception)
      {
        // this is a non-critical function, ignore errors
      }
		}

    /// <summary>
    /// Set NUCLEUS_PATH if it is not already set.
    /// </summary>
		public void RunFinished()
		{
			if (String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("NUCLEUS_PATH")))
			{
				if (MessageBox.Show("The NUCLEUS_PATH environment variable is not set.  Nucleus build scripts require this path in order to find required resources.  Do you want to automatically set NUCLEUS_PATH?", "Set Path", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
				{
					System.Environment.SetEnvironmentVariable("NUCLEUS_PATH", System.IO.Path.GetDirectoryName(this.GetType().Assembly.Location), EnvironmentVariableTarget.User);
				}
			}

      // set the NUCLEUS_TOOLS_PATH environment variable without asking, if it is not already set.
      // By default, NUCLEUS_PATH is set to the same folder as NUCLEUS_TOOLS_PATH, but it can be changed later to point to a dev instance of Nucleus.  NUCLEUS_TOOLS_PATH is 
      // meant to always point to the install location of the developer tools so that it can be used to locate Visual Studio and MSBuild extensions.
      if (String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("NUCLEUS_TOOLS_PATH")))
      {
        System.Environment.SetEnvironmentVariable("NUCLEUS_TOOLS_PATH", System.IO.Path.GetDirectoryName(this.GetType().Assembly.Location), EnvironmentVariableTarget.User);        
      }
    }

		private string Get(Dictionary<string, string> replacementsDictionary, string key)
		{
			if (replacementsDictionary.ContainsKey(key))
			{
				return replacementsDictionary[key];
			}
			return "";
		}

    /// <summary>
    /// For project templates, display the wizard UI to collect additional Nucleus-related settings.  
    /// For Item templates, read the project (csproj) file and set Nucleus-related template replacement tokens so that they can be used when Visual Studio replaces
    /// tokens during item creation, and create required project folders for the relevant item template.
    /// </summary>
    /// <param name="automationObject"></param>
    /// <param name="replacementsDictionary"></param>
    /// <param name="runKind"></param>
    /// <param name="customParams"></param>
    /// <remarks>
    /// We check/create project folders for item templates because Visual Studio does not create folders when executing an item template.  The .vstemplate "CreateInPlace" 
    /// element is supposed to work for this, but instead we get a null reference exception message from Visual Studio if the folder does not exist.
    /// </remarks>
		public void RunStarted(object automationObject, Dictionary<string, string> replacementsDictionary, WizardRunKind runKind, object[] customParams)
		{
			Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();

			_DTE projectProperties = automationObject as _DTE;

      try
			{
				if (runKind == WizardRunKind.AsNewProject)
				{
          string projectType="";

          // parse the project template file name to get the project type
					if (customParams != null && customParams.Length > 0)
					{
            projectType = System.IO.Path.GetDirectoryName(customParams[0].ToString()).Split(new char[] { '/','\\' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
          }

          // set default values based on what the user has entered for the project 
					string defaultExtensionName = Get(replacementsDictionary, "$safeprojectname$");
					string[] defaultExtensionNameParts = defaultExtensionName.Split('.');
					if (defaultExtensionNameParts.Length > 1)
					{
						defaultExtensionName = defaultExtensionNameParts.Last();
					}
          
          string friendlyName = defaultExtensionName;
          string defaultModelName = defaultExtensionName.ReplaceInvalidCharacters(true);

          // show the wizard form
					ProjectWizardForm projectOptionsForm = new ProjectWizardForm
					{
						ClassNameEnabled = true, 
						ExtensionNamespace = Get(replacementsDictionary, "$safeprojectname$").ReplaceInvalidCharacters(true),
						ExtensionName = defaultExtensionName.ReplaceInvalidCharacters(false),
						FriendlyName = friendlyName,
						PublisherName = Get(replacementsDictionary, "$registeredorganization$"),
						ModelClassName = defaultModelName,
            SourceLocation = (string)customParams.FirstOrDefault() ?? ""
          };

          projectOptionsForm.SetProjectType(projectType);

          if (projectOptionsForm.ShowDialog() == DialogResult.OK)
					{
            // Add custom tokens
            replacementsDictionary.Add("$nucleus.extension.namespace$", projectOptionsForm.ExtensionNamespace);

            AddExtensionNameTokens(replacementsDictionary, projectOptionsForm.ExtensionName);

            replacementsDictionary.Add("$nucleus.extension.description$", projectOptionsForm.ExtensionDescription);
						replacementsDictionary.Add("$nucleus.extension.friendlyname$", projectOptionsForm.FriendlyName);

            // If the user selects a model name which matches the end of the namespace, add a "Models" prefix to the
            // model name so that the compiler can defferentiate between the model class name and the namespace.
            if (projectOptionsForm.ExtensionNamespace.EndsWith(projectOptionsForm.ModelClassName))
            {
              replacementsDictionary.Add("$nucleus.extension.model_class_namespace$", "Models.");
            }
            else
            {
              replacementsDictionary.Add("$nucleus.extension.model_class_namespace$", "");
            }

            replacementsDictionary.Add("$nucleus.extension.model_class_name$", projectOptionsForm.ModelClassName);
						replacementsDictionary.Add("$nucleus.extension.model_class_name.camelcase$", projectOptionsForm.ModelClassName.ToCamelCase());
            replacementsDictionary.Add("$nucleus.extension.model_class_name.lowercase$", projectOptionsForm.ModelClassName.ToLower());

            replacementsDictionary.Add("$publisher.name$", projectOptionsForm.PublisherName);
						replacementsDictionary.Add("$publisher.url$", projectOptionsForm.PublisherUrl);
						replacementsDictionary.Add("$publisher.email$", projectOptionsForm.PublisherEmail);
					}
					else
					{
						throw new WizardBackoutException();
					}
				}
				else if (runKind == WizardRunKind.AsNewItem)
				{
					string projectFile = "";
          string itemType = "";
                    
          if (customParams != null && customParams.Length > 0)
          {
            itemType = System.IO.Path.GetDirectoryName(customParams[0].ToString()).Split(new char[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
          }

          // Get the active project, read the project filename and create required project folders
          Array activeProjects = (Array)projectProperties.ActiveSolutionProjects;

          if (activeProjects.Length > 0)
					{
						Project activeProj = (Project)activeProjects.GetValue(0);
						projectFile = activeProj.FileName;
            
            switch (itemType)
            {
              case "Controller":
                CheckAndCreateFolder(activeProj, "Controllers");
                break;
              case "Layout":
                CheckAndCreateFolder(activeProj, "Layouts");
                break;
              case "Container":
                CheckAndCreateFolder(activeProj, "Containers");
                break;
              case "View":
                CheckAndCreateFolder(activeProj, "Views");
                break;
            }
          }

          // read the project file to find the <ExtensionFolder> element, which contains the extension name, and add nucleus extension tokens to 
          // the replacements dictionary.
          if (!String.IsNullOrEmpty(projectFile))
					{
						System.Xml.XmlDocument projectFileXml = new System.Xml.XmlDocument();
						projectFileXml.Load(projectFile);

						System.Xml.XmlNode pluginFolderXml = projectFileXml.SelectSingleNode("//ExtensionFolder");
						if (pluginFolderXml != null)
						{
              string extensionName = pluginFolderXml.InnerText;

              AddExtensionNameTokens(replacementsDictionary, extensionName);
            }
          }
				}
			}
			catch (WizardBackoutException)
			{
				throw;
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.ToString());
			}
		}

    /// <summary>
    /// Specifies whether we should add the item from an item template to the project.
    /// </summary>
    /// <param name="filePath"></param>
    /// <returns></returns>
		public bool ShouldAddProjectItem(string filePath)
		{
			return true;
		}

    /// <summary>
    /// Check for a required folder, and create it if it does not exist.
    /// </summary>
    /// <param name="activeProj"></param>
    /// <param name="folder"></param>
    private void CheckAndCreateFolder(Project activeProj, string folder)
    {
      Microsoft.VisualStudio.Shell.ThreadHelper.ThrowIfNotOnUIThread();
      string projectPath = System.IO.Path.GetDirectoryName(activeProj.FullName);

      string fullPath = System.IO.Path.Combine(projectPath, folder);
           
      if (!System.IO.Directory.Exists(fullPath))
      {
        activeProj.ProjectItems.AddFolder(folder);
      }
    }

    /// <summary>
    /// Add nucleus extension name tokens to the replacements dictionary.
    /// </summary>
    /// <param name="replacementsDictionary"></param>
    /// <param name="extensionName"></param>
    private void AddExtensionNameTokens(Dictionary<string, string> replacementsDictionary, string extensionName)
    {
      replacementsDictionary.Add("$nucleus.extension.name$", extensionName);
      replacementsDictionary.Add("$nucleus.extension.name.camelcase$", extensionName.ToCamelCase());
      replacementsDictionary.Add("$nucleus.extension.name.lowercase$", extensionName.ToLower());

      string extensionNameSingular = extensionName.ToSingular();

      replacementsDictionary.Add("$nucleus.extension.name-singular$", extensionNameSingular);
      replacementsDictionary.Add("$nucleus.extension.name-singular.camelcase$", extensionNameSingular.ToCamelCase());
      replacementsDictionary.Add("$nucleus.extension.name-singular.lowercase$", extensionNameSingular.ToLower());
    }
  }
}

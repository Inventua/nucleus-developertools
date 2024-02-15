This project consists of Analyzers, Code Fix Providers, MSBuild tasks, Visual Studio Command Extensions, the "module.build.targets" MSBuild targets file used 
for extension packaging, Nucleus Project/Item templates and the template Wizard. 

PREREQUISITES
1. Visual Studio extension development requires the Visual Studio SDK (VSSDK), which you can install with the "Tools -> Get Tools and Features" menu item in
Visual Studio.  Choose the "Visual Studio extension development" workload in the "Other Toolsets" section to install the VSSDK.

2.  We also use the "Extensibility Essentials 2022" extensions collection, which can be installed from:
https://marketplace.visualstudio.com/items?itemName=MadsKristensen.ExtensibilityEssentials2022

DEBUGGING
1. You can debug the Analyzers and Code Fix providers in isolation by running the "Nucleus.DeveloperTools.Analyzers.Vsix" project.  
2. To debug Analyzers, in the Visual Studio experimental instance, go to Tools / Options / Text Editor / C# / Advanced, and un-select "Run code analysis in a seperate process".
3. You can debug the Templates in isolation by running the "Nuclesu.DeveloperTools.Templates" project.
4. You can debug the Visual Studio command extensions in isolation by running the "Nucleus.DeveloperTools.VisualStudio.Commands" project.

5. Or (most common) you can debug everything by running the "Nuclueus.DeveloperTools.Vsix" project.  This project is also used to create the production install set.

When debugging Nucleus.DeveloperTools.Vsix, we use a Visual Studio experimental instance with name "Roslyn".  This is controlled by the project item 
<VSSDKTargetPlatformRegRootSuffix>.  This is required for analyzers and code fix providers to work, because it tells Visual Studio to use the Roslyn 
compiler.  If you need to reset the Roslyn experimental instance, use the command:

"C:\Program Files\Microsoft Visual Studio\2022\Professional\VSSDK\VisualStudioIntegration\Tools\Bin\CreateExpInstance.exe" /Reset /VSInstance=17.0_8bf49766 /RootSuffix=Roslyn && PAUSE

6. To debug MSBuild tasks, debug Nucleus.DeveloperTools.Vsix as normal (run it from Visual Studio), then when the Visual Studio experimental instance is 
running, use "Debug -> Attach to Process" to attach to all "MSBUILD" processes.


NOTES
1.  "Nuclueus.DeveloperTools.Vsix" project creates a Vsix (install package) using build outputs from the other projects.  The Vsix build tools generally work OK 
with outputs from referenced projects, but do not automatically include the Nucleus.DeveloperTools.VisualStudio.Commands.pkgdef file from the 
"Nucleus.DeveloperTools.VisualStudio.Commands" project.  Custom steps in the vsix.build.targets file manually copy and include this file in the build output 
(Nuclueus.DeveloperTools.Vsix file).

2.  The project and item templates are packaged into ZIP files by Nucleus.DeveloperTools.VisualStudioTemplates/templates.build.targets
when you are using that project to debug the templates in isolation.  The main proejct build packaging project is "Nucleus.DeveloperTools.Vsix", 
which also zips and copies the templates files in Nucleus.DeveloperTools.Vsix\vsix.build.targets.

ISSUES
1.  After resetting the Visual Studio experimental instance and debugging, no Nucleus Visual Studio extensions are installed.
This typically happens the first time after a reset.  Rebuild the solution (Build =. Rebuild Solution) and try again.  In the experimental instance, use 
the "Extensions -> Manage Extensions" ("Installed" section) to check that the Nucleus Developer Tools are installed in the experimental instance.

2.  After starting debugging, the analyzers and command extensions are not immediately available.
Visual Studio loads and initializes extensions in the background.  This process is slower when you have a debugger attached to Visual Studio.  Wait about 
30 seconds for the extensions to become available.

3.  MSB4018	The "GetDeploymentPathFromVsixManifest" task failed unexpectedly.  System.MissingMethodException: Method not found: 'Void Microsoft.VisualStudio.ExtensionManager.ExtensionEngineImpl..ctor(Microsoft.VisualStudio.ExtensionManager.IEngineHost, Microsoft.VisualStudio.ExtensionManager.EngineMode)'.
This error can be encountered intermittently when debugging Nucleus.DeveloperTools.Vsix.  If you re-compile and re-start debugging (press F5 in Visual Studio) it 
generally goes away without any action being taken.

4.  The vsixmanifest editor does not work with projects that use the "new" style project (.csproj) file format.
Edit vsixmanifest files with the XML editor.

5.  You may intermittently get a file-locking error when compiling.
Close all Visual studio instances, then open the "Nucleus.DeveloperTools" solution and rebuild.

6.  Warning	NU1701: Package 'Microsoft.CodeAnalysis.Workspaces.MSBuild 4.8.0' was restored using '.NETFramework,Version=v4.6.1 ... .NETFramework,Version=v4.8.1' instead 
of the project target framework '.NETStandard,Version=v2.0'. This package may not be fully compatible with your project.	

This warning should be ignored. 

References:
https://github.com/dotnet/roslyn/pull/59342
https://github.com/dotnet/roslyn/pull/59230


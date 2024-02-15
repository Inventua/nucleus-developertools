using System;
using System.Collections.Generic;
using System.Text;
using Nucleus.DeveloperTools.MSBuild.Properties;
using System.Xml.Linq;
using System.Xml;

namespace Nucleus.DeveloperTools.MSBuild
{
  internal static class BuiltTaskExtensions
  {
    // error codes help link
    private const string DEVELOPER_TOOLS_ERROR_REFERENCE_URL = "https://www.nucleus-cms.com/references/dev-tools-error-codes/";

    /// <summary>
    /// Log an error for the specified <paramref name="code"/> with no source code location specified.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="args"></param>
    public static void LogError(this Microsoft.Build.Utilities.Task task, string sourceFile, string code, params object[] args)
    {
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT");
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY");

      task.Log.LogError
      (
        subcategory: category,
        code,
        code,
        DEVELOPER_TOOLS_ERROR_REFERENCE_URL,
        sourceFile,
        0, 0, 0, 0,
        message,
        args
      );
    }

    /// <summary>
    /// Log an error for the specified <paramref name="code"/> and <paramref name="element"/>.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="element"></param>
    /// <param name="args"></param>
    public static void LogError(this Microsoft.Build.Utilities.Task task, string sourceFile, string code, XElement element, params object[] args)
    {
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT") ?? "";
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY") ?? "";

      Models.SourceCodeLocation location = BuildLocation(element);
      task.Log.LogError
      (
        subcategory: category,
        code,
        code,
        DEVELOPER_TOOLS_ERROR_REFERENCE_URL,
        sourceFile,
        location.StartLineNumber,
        location.StartColumnNumber,
        location.EndLineNumber,
        location.EndColumnNumber,
        message,
        args
      );
    }

    /// <summary>
    /// Log an error for the specified <paramref name="code"/> and <paramref name="attribute"/>.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="attribute"></param>
    /// <param name="args"></param>
    public static void LogError(this Microsoft.Build.Utilities.Task task, string sourceFile, string code, XAttribute attribute, params object[] args)
    {
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT") ?? "";
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY") ?? "";

      Models.SourceCodeLocation location = BuildLocation(attribute);
      task.Log.LogError
      (
        subcategory: category,
        code,
        code,
        DEVELOPER_TOOLS_ERROR_REFERENCE_URL,
        sourceFile,
        location.StartLineNumber,
        location.StartColumnNumber,
        location.EndLineNumber,
        location.EndColumnNumber,
        message,
        args
      );
    }

    /// <summary>
    /// Log a warning for the specified <paramref name="code"/> and <paramref name="attribute"/>.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="attribute"></param>
    /// <param name="args"></param>
    public static void LogWarning(this Microsoft.Build.Utilities.Task task, string sourceFile, string code, XAttribute attribute, params object[] args)
    {
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT") ?? "";
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY") ?? "";

      Models.SourceCodeLocation location = BuildLocation(attribute);
      task.Log.LogWarning
      (
        subcategory: category,
        code,
        code,
        DEVELOPER_TOOLS_ERROR_REFERENCE_URL,
        sourceFile,
        location.StartLineNumber,
        location.StartColumnNumber,
        location.EndLineNumber,
        location.EndColumnNumber,
        message,
        args
      );
    }

    /// <summary>
    /// Log an error for the specified <paramref name="code"/> and <paramref name="element"/>.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="element"></param>
    /// <param name="args"></param>
    public static void LogWarning(this Microsoft.Build.Utilities.Task task, string sourceFile, string code, XElement element, params object[] args)
    {
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT") ?? "";
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY") ?? "";

      Models.SourceCodeLocation location = BuildLocation(element);
      task.Log.LogWarning
      (
        subcategory: category,
        code,
        code,
        DEVELOPER_TOOLS_ERROR_REFERENCE_URL,
        sourceFile,
        location.StartLineNumber,
        location.StartColumnNumber,
        location.EndLineNumber,
        location.EndColumnNumber,
        message,
        args
      );
    }

    /// <summary>
    /// Log an error for the specified <paramref name="code"/> and <paramref name="element"/>.
    /// </summary>
    /// <param name="code"></param>
    /// <param name="args"></param>
    public static void LogWarning(this Microsoft.Build.Utilities.Task task, string sourceFile, string code, params object[] args)
    {
      string message = Resources.ResourceManager.GetString($"{code}_MESSAGEFORMAT") ?? "";
      string category = Resources.ResourceManager.GetString($"{code}_CATEGORY") ?? "";

      task.Log.LogWarning
      (
        subcategory: category,
        code,
        code,
        DEVELOPER_TOOLS_ERROR_REFERENCE_URL,
        sourceFile,
        0,
        0,
        0,
        0,
        message,
        args
      );
    }

    /// <summary>
    /// Return a SourceCodeLocation for the specified <paramref name="element"/> value.
    /// </summary>
    /// <param name="element"></param>
    /// <returns></returns>
    private static Models.SourceCodeLocation BuildLocation(XElement element)
    {
      string elementName = element.Name.LocalName;
      IXmlLineInfo lineInfo = (IXmlLineInfo)element;

      // +1 is for the > character following the element value
      int startPosition = lineInfo.HasLineInfo() ? lineInfo.LinePosition + elementName.Length + 1 : 0;
      int endPosition = lineInfo.HasLineInfo() ? startPosition + element.Value.Length : 0;

      return new Models.SourceCodeLocation
      {
        StartLineNumber = lineInfo.LineNumber,
        StartColumnNumber = startPosition,
        EndLineNumber = lineInfo.LineNumber,
        EndColumnNumber = endPosition
      };
    }

    /// <summary>
    /// Return a SourceCodeLocation for the specified <paramref name="attribute"/> value.
    /// </summary>
    /// <param name="attribute"></param>
    /// <returns></returns>
    private static Models.SourceCodeLocation BuildLocation(XAttribute attribute)
    {
      string attributeName = attribute.Name.LocalName;
      IXmlLineInfo lineInfo = (IXmlLineInfo)attribute;

      // +2 is for the equals and quote (=") character before the attribute value, so that the start position is
      // at the start of the value
      int startPosition = lineInfo.HasLineInfo() ? lineInfo.LinePosition + attributeName.Length + 2 : 0;
      int endPosition = lineInfo.HasLineInfo() ? startPosition + attribute.Value.Length : 0;

      return new Models.SourceCodeLocation
      {
        StartLineNumber = lineInfo.LineNumber,
        StartColumnNumber = startPosition,
        EndLineNumber = lineInfo.LineNumber,
        EndColumnNumber = endPosition
      };
    }
  }
}

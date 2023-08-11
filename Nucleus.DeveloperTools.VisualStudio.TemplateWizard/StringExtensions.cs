using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.DeveloperTools.VisualStudio.TemplateWizard
{
  internal static class StringExtensions
  {

    public static string ReplaceInvalidCharacters(this string value, Boolean allowDot)
    {
      string pattern = "([^A-Za-z0-9_])";

      if (allowDot)
      {
        pattern = "([^A-Za-z0-9._])";
      }

      return System.Text.RegularExpressions.Regex.Replace(value, pattern, "");
    }

    public static Boolean Validate(this string value, Boolean allowDot)
    {
      string pattern = "^([A-Za-z0-9_])*$";

      if (allowDot)
      {
        pattern = "^([A-Za-z0-9._])*$";
      }

      return System.Text.RegularExpressions.Regex.IsMatch(value, pattern);
    }

    public static string ToCamelCase(this string value)
    {
      if (string.IsNullOrEmpty(value)) return "";

      return value.Substring(0, 1).ToLower() + (value.Length > 1 ? value.Substring(1) : "");
    }

    public static string ToSingular(this string value)
    {
      if (string.IsNullOrEmpty(value)) return "";

      if (value.Length > 1 && value.EndsWith("s"))
      {
        return value.Substring(0, value.Length - 1);
      }

      return value;
    }

  }
}

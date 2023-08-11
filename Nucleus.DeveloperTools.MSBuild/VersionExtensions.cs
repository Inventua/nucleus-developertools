using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.DeveloperTools.MSBuild
{
  /// <summary>
  /// Extensions for the System.Version class.
  /// </summary>
  static internal class VersionExtensions
  {
    /// <summary>
    /// Parse a version, handle versions like 1.2.1.8-pre-a3 by ignoring the suffix.  Also handle "*" in place 
    /// of any part of the version.  Version must contain at least major.minor but can omit build/revision.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="minMax">False for minVersion, <see langword="true"/>for maxVersion</param>
    /// <returns></returns>
    static public System.Version Parse(this string value, Boolean minMax)
    {
      // handle versions like 1.2.1.8-pre-a3 by ignoring the suffix.  Also handle "*" in place of any part of the version
      System.Text.RegularExpressions.Match versionMatch = System.Text.RegularExpressions.Regex.Match(value, "^(?<major>[0-9*]{1,5}).(?<minor>[0-9*]{1,5}).(?<build>[0-9*]{0,5}).(?<revision>[0-9*]{0,5})(?<suffix>.*)$");
      if (versionMatch.Success)
      {
        int major = ParseVersionPart(versionMatch.Groups["major"].Value, minMax);
        int minor = ParseVersionPart(versionMatch.Groups["minor"].Value, minMax);
        int build = ParseVersionPart(versionMatch.Groups["build"].Value, minMax);
        int revision = ParseVersionPart(versionMatch.Groups["revision"].Value, minMax);
        return new System.Version(major, minor, build, revision);
      }
      else
      {
        return new System.Version(0, 0, 0, 0);
      }
    }

    /// <summary>
    /// Parse a single part of a version.
    /// </summary>
    /// <param name="value"></param>
    /// <param name="minMax">False for minVersion, <see langword="true"/>for maxVersion</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    private static int ParseVersionPart(string value, Boolean minMax)
    {
      if (String.IsNullOrEmpty(value) || value == "*")
      {
        return minMax ? 0 : 65535;
      }
      else
      {
        if (int.TryParse(value, out int part))
        {
          return part;
        }
        else
        {
          // version is not parseable
          throw new InvalidOperationException($"Cannot parse version: '{value}' is not a number.");
        }
      }
    }

    /// <summary>
    /// Returns a valid indicating whether the specified version is empty (0.0.0.0).
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    static public Boolean IsEmpty(this System.Version version)
    {
      return (version == null || version.Equals(new System.Version(0, 0, 0, 0)));
    }


    /// <summary>
    /// Returns a valid indicating whether the specified string represents a version which is less than this version.
    /// </summary>
    /// <param name="version"></param>
    /// <param name="compareToVersion"></param>
    /// <returns></returns>
    /// <remarks>
    /// compareToVersion should contain a string in the form of a version, but can contain a '*' in place of any of the version fields.
    /// </remarks>
    static public Boolean IsLessThan(this System.Version version, string compareToVersion)
    {
      if (compareToVersion == "*") compareToVersion = "*.*";
      System.Version compareTo = Version.Parse(compareToVersion.Replace("*", "0"));
      return ZeroUndefinedElements(version).CompareTo(ZeroUndefinedElements(compareTo)) < 0;
    }

    /// <summary>
    /// Returns a valid indicating whether the specified string represents a version which is greater than this version.
    /// </summary>
    /// <param name="version"></param>
    /// <param name="compareToVersion"></param>
    /// <returns></returns>
    /// <remarks>
    /// compareToVersion should contain a string in the form of a version, but can contain a '*' in place of any of the version fields.
    /// </remarks>
    static public Boolean IsGreaterThan(this System.Version version, string compareToVersion)
    {
      if (compareToVersion == "*") compareToVersion = "*.*";
      System.Version compareTo = Version.Parse(compareToVersion.Replace("*", "65535"));
      return ZeroUndefinedElements(version).CompareTo(ZeroUndefinedElements(compareTo)) > 0;
    }

    /// <summary>
    /// Replace version elements which have been set to -1 with zero, so that comparisons work properly
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    static public Version ZeroUndefinedElements(this System.Version version)
    {
      return new Version(version.Major, version.Minor == -1 ? 0 : version.Minor, version.Build == -1 ? 0 : version.Build, version.Revision == -1 ? 0 : version.Revision);
    }
  }
}



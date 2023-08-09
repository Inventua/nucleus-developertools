using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Nucleus.DeveloperTools.Analyzers
{
  public static class DiagnosticMessages
  {
    public static readonly DiagnosticDescriptor MANIFEST_PACKAGE_ID_EMPTY = Create
    (
      "NUCLEUS100",
      DiagnosticSeverity.Error,
      "Manifest (package.xml)"
    );

    public static readonly DiagnosticDescriptor MANIFEST_PACKAGE_NAME_EMPTY = Create
    (
      "NUCLEUS101",
      DiagnosticSeverity.Error,
      "Manifest (package.xml)"
    );

    public static readonly DiagnosticDescriptor MANIFEST_PACKAGE_VERSION_EMPTY = Create
    (
      "NUCLEUS102",
      DiagnosticSeverity.Error,
      "Manifest (package.xml)"
    );

    public static readonly DiagnosticDescriptor MANIFEST_PACKAGE_ID_INVALID = Create
    (
      "NUCLEUS103",
      DiagnosticSeverity.Error,
      "Manifest (package.xml)"
    );

    public static readonly DiagnosticDescriptor MANIFEST_PACKAGE_VERSION_INVALID = Create
    (
      "NUCLEUS104",
      DiagnosticSeverity.Error,
      "Manifest (package.xml)"
    );

    public static readonly DiagnosticDescriptor MANIFEST_COMPATIBILITY_MINVERSION_TOOLOW = Create
    (
      "NUCLEUS200",
      DiagnosticSeverity.Warning,
      "Manifest (package.xml)"
    );

    public static readonly DiagnosticDescriptor CONTROLLER_NO_EXTENSION_ATTRIBUTE = Create
    (
      "NUCLEUS300",
      DiagnosticSeverity.Warning,
      "Controllers"
    );

    public static readonly DiagnosticDescriptor CONTROLLER_ADMIN_NO_AUTHORIZE_ATTRIBUTE = Create
    (
      "NUCLEUS301",
      DiagnosticSeverity.Info,
      "Controllers"
    );

    


    private static DiagnosticDescriptor Create(string diagnosticId, DiagnosticSeverity severity, string category)
    {
      return Create
      (
        diagnosticId,
        severity,
        category,
        $"{diagnosticId}_TITLE",
        $"{diagnosticId}_MESSAGEFORMAT",
        $"{diagnosticId}_DESCRIPTION"
      );
    }


    private static DiagnosticDescriptor Create(string diagnosticId, DiagnosticSeverity severity, string category, string titleResourceKey, string messageFormatResourceKey, string descriptionResourceKey)
    {
      return new DiagnosticDescriptor
      (
        diagnosticId,
        new LocalizableResourceString(titleResourceKey, Resources.ResourceManager, typeof(Resources)),
        new LocalizableResourceString(messageFormatResourceKey, Resources.ResourceManager, typeof(Resources)),
        category,
        severity,
        true,
        new LocalizableResourceString(descriptionResourceKey, Resources.ResourceManager, typeof(Resources))
      );
    }
  }
}

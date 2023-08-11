using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace Nucleus.DeveloperTools.Analyzers
{
  public static class DiagnosticMessages
  {
    public static readonly DiagnosticDescriptor CONTROLLER_NO_EXTENSION_ATTRIBUTE = Create
    (
      "NUCL300",
      DiagnosticSeverity.Warning,
      "Controllers"
    );

    public static readonly DiagnosticDescriptor CONTROLLER_ADMIN_NO_AUTHORIZE_ATTRIBUTE = Create
    (
      "NUCL301",
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

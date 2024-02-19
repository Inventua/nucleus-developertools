using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nucleus.DeveloperTools.VisualStudio.Commands;
internal static class ReferenceExtensions
{
  public static string AssemblyFileName(this VSLangProj.Reference reference) => reference.Name + ".dll";


}

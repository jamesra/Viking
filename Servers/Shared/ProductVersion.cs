#nullable disable
using System;
using System.Diagnostics;
using System.Reflection;

namespace Viking.ProductVersioning
{
    /// <summary>
    /// Reads the version stamped on a product assembly so startup logs, HTTP
    /// <c>/version</c>, and response headers quote the same string.
    /// Callers pass <c>typeof(TheirType).Assembly</c> (IIS entry assemblies are often null).
    /// File version wins when informational version is the SDK default 1.0.0 while
    /// the csproj set a different <c>FileVersion</c>. A <c>+</c> git suffix is kept
    /// when it extends that file version.
    /// </summary>
    internal static class ProductVersion
    {
        /// <summary>
        /// Assembly name plus <see cref="VersionOf"/>, for startup logs.
        /// </summary>
        internal static string Describe(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            var name = assembly.GetName().Name;
            if (string.IsNullOrEmpty(name))
                name = "unknown";

            return name + " " + VersionOf(assembly);
        }

        /// <summary>
        /// User-facing version of <paramref name="assembly"/>. Does not throw if the
        /// file version cannot be read; falls through to informational, then assembly version.
        /// </summary>
        internal static string VersionOf(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            var fileVersion = ReadFileVersion(assembly);
            var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational)
                && InformationalExtendsFileVersion(informational, fileVersion))
            {
                return informational;
            }

            if (!string.IsNullOrWhiteSpace(fileVersion))
                return fileVersion;

            if (!string.IsNullOrWhiteSpace(informational))
                return informational;

            var assemblyVersion = assembly.GetName().Version;
            return assemblyVersion == null ? "unknown" : assemblyVersion.ToString();
        }

        private static string ReadFileVersion(Assembly assembly)
        {
            if (string.IsNullOrEmpty(assembly.Location))
                return string.Empty;

            try
            {
                return FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion ?? string.Empty;
            }
            catch (System.IO.IOException)
            {
                return string.Empty;
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// True when informational version is <c>{fileVersion}+metadata</c>.
        /// File versions often end in <c>.0</c> while the SDK informational prefix does not.
        /// </summary>
        private static bool InformationalExtendsFileVersion(string informational, string fileVersion)
        {
            var plus = informational.IndexOf('+');
            if (plus <= 0)
                return false;

            if (string.IsNullOrWhiteSpace(fileVersion))
                return true;

            var prefix = informational.Substring(0, plus);
            var trimmed = fileVersion;
            if (trimmed.EndsWith(".0", StringComparison.Ordinal))
                trimmed = trimmed.Substring(0, trimmed.Length - 2);

            return prefix == fileVersion || prefix == trimmed;
        }
    }
}

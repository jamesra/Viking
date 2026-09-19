using System;

namespace Viking.VolumeModel
{
    /// <summary>
    /// Builds volume tile and stos paths. HTTP(S) hosts always use '/' so Windows
    /// <see cref="System.IO.Path.DirectorySeparatorChar"/> is never inserted into URLs.
    /// Local file hosts keep the OS separator. Callers that write cache files still use Path.Combine.
    /// </summary>
    public static class VolumePath
    {
        /// <summary>
        /// True when <paramref name="path"/> is an http or https URL.
        /// </summary>
        public static bool IsHttp(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// '/' for HTTP hosts, otherwise the OS directory separator.
        /// </summary>
        public static char Separator(string hostHint) => IsHttp(hostHint) ? '/' : System.IO.Path.DirectorySeparatorChar;

        /// <summary>
        /// Joins relative segments using the separator implied by <paramref name="hostHint"/>.
        /// Does not prepend the host.
        /// </summary>
        public static string JoinRelative(string hostHint, params string[] parts)
        {
            char sep = Separator(hostHint);
            string result = string.Empty;
            if (parts is null)
                return result;

            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part))
                    continue;

                string normalized = part.Replace('\\', sep).Replace('/', sep).Trim(sep);
                if (normalized.Length == 0)
                    continue;

                result = result.Length == 0 ? normalized : result + sep + normalized;
            }

            return result;
        }

        /// <summary>
        /// Prepends <paramref name="host"/> and joins remaining segments with the separator for that host.
        /// </summary>
        public static string Combine(string host, params string[] relativeParts)
        {
            string head = (host ?? string.Empty).TrimEnd('/', '\\');
            string tail = JoinRelative(host, relativeParts);
            if (head.Length == 0)
                return tail;
            if (tail.Length == 0)
                return head;

            return head + Separator(host) + tail;
        }
    }
}

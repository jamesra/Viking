using System;

namespace Viking.Identity.Models
{
    /// <summary>
    /// OAuth scope tokens and Permissions URL keys cannot contain spaces.
    /// Display <see cref="Resource.Name"/> may include spaces; encode them as '-' at that boundary.
    /// Keep in sync with Viking.Common.ResourceScopeNames.
    /// </summary>
    public static class ResourceScopeNames
    {
        public const char SpaceReplacement = '-';

        public static string ToScopePrefix(string resourceName)
        {
            if (string.IsNullOrEmpty(resourceName))
                return resourceName;

            return resourceName.Replace(' ', SpaceReplacement);
        }

        public static string ToScope(string resourceName, string permissionId)
            => $"{ToScopePrefix(resourceName)}.{ToScopePrefix(permissionId)}";

        /// <summary>
        /// Maps an encoded permission suffix back to the stored PermissionId (e.g. Access-Manager → Access Manager).
        /// </summary>
        public static string ToPermissionId(string encodedPermission)
        {
            if (string.IsNullOrEmpty(encodedPermission))
                return encodedPermission;

            return encodedPermission.Replace(SpaceReplacement, ' ');
        }

        public static bool TryParse(string scope, out string prefix, out string encodedPermission)
        {
            prefix = null;
            encodedPermission = null;
            if (string.IsNullOrEmpty(scope))
                return false;

            var separator = scope.LastIndexOf('.');
            if (separator <= 0 || separator == scope.Length - 1)
                return false;

            prefix = scope.Substring(0, separator);
            encodedPermission = scope.Substring(separator + 1);
            return true;
        }

        public static bool ScopePrefixesCollide(string nameA, string nameB)
        {
            if (string.IsNullOrEmpty(nameA) || string.IsNullOrEmpty(nameB))
                return false;

            return string.Equals(ToScopePrefix(nameA), ToScopePrefix(nameB), StringComparison.OrdinalIgnoreCase);
        }
    }
}

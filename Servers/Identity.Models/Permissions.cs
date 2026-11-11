using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Viking.Identity.Models
{
    /// <summary>
    /// The string stored in the database and sent in tokens and APIs for a permission enum member. Renaming the member
    /// must not change what is persisted, so the string is always explicit; numeric values are never persisted or sent.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class PermissionIdAttribute : Attribute
    {
        public PermissionIdAttribute(string id) => Id = id;

        public string Id { get; }
    }

    /// <summary>Rights to one annotation database. Token scopes ({ServerName}.Annotate) are built from these.</summary>
    public enum AnnotationServerPermission
    {
        [PermissionId("Read"), Description("View annotations and the images of every linked volume")]
        Read,

        [PermissionId("Annotate"), Description("Create and edit annotations; implies Read")]
        Annotate,

        [PermissionId("Review"), Description("Review and correct annotations; implies Read")]
        Review
    }

    /// <summary>
    /// The only permission on an annotation context. It decides whether the context is listed when a user launches
    /// Viking, and is required to open it. Rights to the data live on the context's <see cref="AnnotationServer"/>.
    /// </summary>
    public enum AnnotationContextPermission
    {
        [PermissionId("Access"), Description("See this context in Viking and open it; data rights come from its annotation server")]
        Access
    }

    public enum GroupPermission
    {
        [PermissionId("Access Manager"), Description("Add/Remove group members")]
        AccessManager
    }

    public enum OrgUnitPermission
    {
        [PermissionId("Administrator"), Description("Edit all aspects of an organizational unit and CRUD operations on all child resources")]
        Admin
    }

    public enum SegmentationServicePermission
    {
        [PermissionId("Access Manager")]
        AccessManager,

        [PermissionId("User")]
        User
    }

    public enum CorrectionServicePermission
    {
        [PermissionId("Access Manager"), Description("Manage access to correction service resources")]
        AccessManager,

        [PermissionId("User"), Description("Use the correction service")]
        User
    }

    /// <summary>One permission of a resource type as persisted: its string id and description.</summary>
    public readonly struct PermissionEntry
    {
        public PermissionEntry(string id, string description)
        {
            Id = id;
            Description = description;
        }

        public string Id { get; }
        public string Description { get; }
    }

    /// <summary>
    /// Single source of truth for which resource types have permissions and what those are. Register a new resource
    /// type's permission enum in <see cref="Types"/>; seeding of ResourceTypePermission, token scopes, the permission
    /// catalog routes and <c>?permission=</c> validation all derive from it.
    /// </summary>
    public static class PermissionRegistry
    {
        /// <summary>Resource type name to its permission enum.</summary>
        public static readonly IReadOnlyDictionary<string, Type> Types = new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            [nameof(AnnotationServer)] = typeof(AnnotationServerPermission),
            [nameof(AnnotationContext)] = typeof(AnnotationContextPermission),
            [nameof(Group)] = typeof(GroupPermission),
            [nameof(OrganizationalUnit)] = typeof(OrgUnitPermission),
            [nameof(SegmentationService)] = typeof(SegmentationServicePermission),
            [nameof(CorrectionService)] = typeof(CorrectionServicePermission)
        };

        /// <summary>The string persisted and sent for <paramref name="permission"/>.</summary>
        public static string Id(this Enum permission)
        {
            var field = permission.GetType().GetField(permission.ToString(), BindingFlags.Public | BindingFlags.Static);
            var attribute = field?.GetCustomAttribute<PermissionIdAttribute>();
            if (attribute == null)
                throw new InvalidOperationException($"{permission.GetType().Name}.{permission} has no [PermissionId].");
            return attribute.Id;
        }

        /// <summary>Description of <paramref name="permission"/>, or null.</summary>
        public static string DescriptionOf(this Enum permission) =>
            permission.GetType().GetField(permission.ToString(), BindingFlags.Public | BindingFlags.Static)?
                .GetCustomAttribute<DescriptionAttribute>()?.Description;

        /// <summary>The persisted ids of every member of <typeparamref name="T"/>, in declaration order.</summary>
        public static string[] Ids<T>() where T : struct, Enum =>
            Enum.GetValues(typeof(T)).Cast<Enum>().Select(Id).ToArray();

        /// <summary>The permissions of a resource type in declaration order; empty when the type defines none.</summary>
        public static IReadOnlyList<PermissionEntry> Entries(string resourceTypeId) =>
            resourceTypeId != null && Types.TryGetValue(resourceTypeId, out var enumType)
                ? Enum.GetValues(enumType).Cast<Enum>().Select(e => new PermissionEntry(e.Id(), e.DescriptionOf())).ToList()
                : new List<PermissionEntry>();

        /// <summary>Maps a persisted id back to the enum member, ignoring case.</summary>
        public static bool TryParse<T>(string id, out T permission) where T : struct, Enum
        {
            foreach (T candidate in Enum.GetValues(typeof(T)))
            {
                if (string.Equals(candidate.Id(), id, StringComparison.OrdinalIgnoreCase))
                {
                    permission = candidate;
                    return true;
                }
            }

            permission = default;
            return false;
        }

        /// <summary>
        /// Error text for a <c>?permission=</c> filter naming something <paramref name="resourceTypeId"/> does not define
        /// (listing the valid ids), or null when every name is valid.
        /// </summary>
        public static string FilterError(string resourceTypeId, IEnumerable<string> requested)
        {
            var valid = Entries(resourceTypeId).Select(e => e.Id).ToList();
            var unknown = requested.Where(r => !valid.Contains(r, StringComparer.OrdinalIgnoreCase)).ToList();
            if (unknown.Count == 0)
                return null;

            return $"Resource type '{resourceTypeId}' has no permission '{string.Join("', '", unknown)}'. "
                + $"Valid permissions: {(valid.Count == 0 ? "(none)" : string.Join(", ", valid))}.";
        }

        /// <summary>Every ResourceTypePermission row the enums define; the EF seed data.</summary>
        public static IEnumerable<ResourceTypePermission> SeedRows() =>
            Types.SelectMany(t => Entries(t.Key).Select(e => new ResourceTypePermission
            {
                ResourceTypeId = t.Key,
                PermissionId = e.Id,
                Description = e.Description
            }));
    }
}

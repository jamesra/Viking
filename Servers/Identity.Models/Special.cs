namespace Viking.Identity.Models
{
    public readonly struct GroupInfo
    {
        public GroupInfo(long id, string name)
        {
            Name = name;
            Id = id;
        }

        public readonly string Name;
        public readonly long Id;

        public static implicit operator long(GroupInfo gi) => gi.Id;
        public static implicit operator string(GroupInfo gi) => gi.Name;
    }

    public static class Special
    {
        public static class Roles
        {
            public const string Admin = "Administrator";
            public const string AdminId = "cdf2b676-7edc-4d96-9ebb-8d1968734482";
            // Note: AdminPasswordHash, SecurityStamp, and ConcurrencyStamp removed - 
            // admin user is created dynamically on first login, not seeded statically
        }

        public static class Groups
        {
            public static GroupInfo Everyone = new GroupInfo(-1, "Everyone");
            public static GroupInfo Anonymous = new GroupInfo(-2, "Anonymous");

            //public const long AdminId = -2;
            //public const string Admin = "Administrators";
        }

        public static class ResourceTypes
        {
            /// <summary>
            /// Resource types that become Duende ApiResources with {Name}.{Permission} scopes.
            /// Order is the preference when one name matches several types: the annotation server
            /// that holds the grants wins over an annotation context that points at it.
            /// Connectome is intentionally excluded so a connectome named RC1 does not steal scopes
            /// from the default annotation context / annotation server also named RC1.
            /// </summary>
            public static readonly string[] ApiFacing =
            {
                nameof(Models.AnnotationServer),
                nameof(Models.AnnotationContext),
                nameof(Models.SegmentationService)
            };
        }

        public static class Permissions
        {
            public static class Group
            {
                public const string AccessManager = "Access Manager";
            }
            
            public static class OrgUnit
            {
                public const string Admin = "Administrator";
            }

            /// <summary>
            /// The only permission on an annotation context. It decides whether the context is listed when a user
            /// launches Viking, and is required to open it. Rights to the data (Read / Annotate / Review) live on the
            /// context's <see cref="AnnotationServer"/>.
            /// </summary>
            public static class AnnotationContext
            {
                public const string Access = "Access";

                public static readonly string[] All = { Access };
            }

            /// <summary>
            /// Rights to one annotation database. Token scopes ({ServerName}.Annotate) are built from these.
            /// </summary>
            public static class AnnotationServer
            {
                public const string Read = "Read";
                public const string Annotate = "Annotate";
                public const string Review = "Review";

                public static readonly string[] All = { Read, Annotate, Review };
            }

            public static class SegmentationService
            {
                public const string AccessManager = "Access Manager";
                public const string User = "User";
            }

            public static class CorrectionService
            {
                public const string AccessManager = "Access Manager";
                public const string User = "User";
            }
        }

    }
}

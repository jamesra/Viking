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
            public const string AdminPasswordHash = "AQAAAAIAAYagAAAAEMyM4WI9BttHIkXcpkuoazVKSOkiTP/C1/3b3enpOUCFaGvS4QEPfqLj8zDlSX/dIA==";
            public const string SecurityStamp = "00000000-0000-0000-0000-000000000001";
            public const string ConcurrencyStamp = "00000000-0000-0000-0000-000000000002";
        }

        public static class Groups
        {
            public static GroupInfo Everyone = new GroupInfo(-1, "Everyone"); 
             
            //public const long AdminId = -2;
            //public const string Admin = "Administrators";
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

            public static class Volume
            {
                public const string Read = "Read";
                public const string Annotate = "Annotate";
                public const string Review = "Review";
            }
        }

    }
}
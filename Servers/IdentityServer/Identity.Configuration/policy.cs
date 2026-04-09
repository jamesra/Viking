using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Viking.Identity
{
    /// <summary>
    /// </summary>
    public readonly struct Policy
    {
        public const string GroupAccessManager = "Access Manager";
        public const string OrgUnitAdmin = "Administrator";
        public const string BearerToken = "BearerToken";
    }
}

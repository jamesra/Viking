using System;
using System.Linq;
using Duende.IdentityServer.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Viking.Identity.Data;
using Viking.Identity.Models;

namespace Viking.Identity.Server.WebManagement
{
    public class ParameterizedScopeParser : DefaultScopeParser
    {
        ApplicationDbContext _context; 
        public ParameterizedScopeParser(ApplicationDbContext context, ILogger<DefaultScopeParser> logger) : base(logger)
        {
            _context = context; 
        }

        public override void ParseScopeValue(ParseScopeContext scopeContext)
        {
            const string Viking = "Viking";
            const string resource = "resource";
            const string permission = "permission";

            var scopeValue = scopeContext.RawValue;

            if (!ResourceScopeNames.TryParse(scopeValue, out var resourceName, out var encodedPermission))
            {
                base.ParseScopeValue(scopeContext);
                return;
            }

            if (string.Equals(resourceName, Viking, StringComparison.OrdinalIgnoreCase))
            {
                base.ParseScopeValue(scopeContext);
                return;
            }

            var permissionId = ResourceScopeNames.ToPermissionId(encodedPermission);

            var resourceObj = _context.Resource
                .Include(r => r.ResourceType).ThenInclude(rt => rt.Permissions)
                .Where(r => r.ResourceTypeId == nameof(Volume) || r.ResourceTypeId == nameof(SegmentationService))
                .AsEnumerable()
                .FirstOrDefault(r =>
                    string.Equals(r.Name, resourceName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ResourceScopeNames.ToScopePrefix(r.Name), resourceName, StringComparison.OrdinalIgnoreCase));

            if (resourceObj == null)
            {
                scopeContext.SetIgnore();
                return;
            }

            if (resourceObj.AvailablePermissions.Any(ap => ap.PermissionId == permissionId))
            {
                scopeContext.SetParsedValues(resource, resourceName);
                scopeContext.SetParsedValues(permission, permissionId);
                return;
            }

            scopeContext.SetError("resource scope specifies unknown permission");
        }
    }
}

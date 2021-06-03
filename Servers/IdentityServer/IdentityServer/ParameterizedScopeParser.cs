using IdentityServer.Data;
using IdentityServer4.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace IdentityServer
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
            const char separator = '.';

            var scopeValue = scopeContext.RawValue;

            var parts = scopeValue.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2)
            {
                // we get in here with a scope like "resource.permission"
                var resourceName = parts[0];
                var permissionId = parts[1];

                if (resourceName == Viking)
                {
                    //This is the old Viking.Annotation scope, which I'm just passing through
                    base.ParseScopeValue(scopeContext);
                    return;
                }

                var resourceObj = _context.Resource.Include(r => r.ResourceType).ThenInclude(rt => rt.Permissions).FirstOrDefault(r => r.Name == resourceName);
                if(resourceObj == null)
                {
                    //Unknown resource, ignore it and do not add it to the results
                    scopeContext.SetIgnore();
                    return;
                }

                if(resourceObj.AvailablePermissions.Any(ap => ap.PermissionId == permissionId))
                {
                    scopeContext.SetParsedValues(resource, resourceName);
                    scopeContext.SetParsedValues(permission, permissionId);
                    return;
                }
                else
                {
                    scopeContext.SetError("resource scope specifies unknown permission");
                }
            }
            else
            {
                // we get in here with a scope not like "resource.permission"
                base.ParseScopeValue(scopeContext);
            }
        }
    }
}

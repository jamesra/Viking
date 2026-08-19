using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server;

namespace Viking.Identity.Server.Extensions.Services
{
    public class AuthenticationService : IAuthenticationService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuthenticationService> _logger;
        private readonly IDebugLoggingService _debugLoggingService;

        public AuthenticationService(ApplicationDbContext context, ILogger<AuthenticationService> logger, IDebugLoggingService debugLoggingService)
        {
            _context = context;
            _logger = logger;
            _debugLoggingService = debugLoggingService;
        }

        public async Task<ApplicationUser> GetApplicationUserAsync(ClaimsPrincipal user, HttpContext httpContext)
        {
            // Manually trigger authentication if not already authenticated and HttpContext is provided
            if (httpContext != null && (user.Identity == null || !user.Identity.IsAuthenticated))
            {
                // Debug: Check if there's an Authorization header
                var authHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault();
                _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Authentication, "Authorization header: {Header}", RedactAuthorizationHeader(authHeader));
                
                // Debug: List available authentication schemes
                _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Authentication, "Trying to authenticate with available schemes...");
                
                // Try to authenticate using the default scheme
                var authResult = await httpContext.AuthenticateAsync();
                _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Authentication, "Default authentication result: Succeeded={Succeeded}, Principal={HasPrincipal}", 
                    authResult.Succeeded, authResult.Principal != null);
                
                // If default scheme fails, try Bearer scheme (which is what's actually registered)
                if (!authResult.Succeeded)
                {
                    _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Authentication, "Default authentication failed, trying Bearer scheme");
                    authResult = await httpContext.AuthenticateAsync("Bearer");
                    _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Authentication, "Bearer result: Succeeded={Succeeded}, Principal={HasPrincipal}", 
                        authResult.Succeeded, authResult.Principal != null);
                }
                
                if (authResult.Succeeded && authResult.Principal != null)
                {
                    // Set the authenticated user
                    httpContext.User = authResult.Principal;
                    _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Authentication, "Set authenticated user: {UserName}", httpContext.User.Identity.Name);
                    user = httpContext.User;
                }
                else
                {
                    _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Authentication, "Authentication failed - returning null");
                    return null; // Return null for unauthenticated users
                }
            }
            
            if (user?.Identity == null)
            {
                return null;
            }
            
            var appUser = await FindApplicationUserAsync(user);
            _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Authentication, "Found user in database: {Found}", appUser != null);
            return appUser;
        }

        public async Task<string> GetCurrentUserIdAsync(ClaimsPrincipal user)
        {
            var appUser = await FindApplicationUserAsync(user);
            return appUser?.Id ?? "Anonymous";
        }

        private async Task<ApplicationUser> FindApplicationUserAsync(ClaimsPrincipal user)
        {
            var username = user.Identity?.GetUsername();
            _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Authentication, "Username from identity: {Username}", username);
            if (!string.IsNullOrEmpty(username))
            {
                var byName = await _context.Users.FirstOrDefaultAsync(u => u.UserName == username);
                if (byName != null)
                    return byName;
            }

            var subjectId = user.FindFirst("sub")?.Value
                ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(subjectId))
                return null;

            _logger.LogDebugIfEnabled(_debugLoggingService, DebugLogCategory.Authentication, "Falling back to subject claim: {SubjectId}", subjectId);
            return await _context.Users.FirstOrDefaultAsync(u => u.Id == subjectId);
        }

        private static string RedactAuthorizationHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
                return "(none)";

            var space = header.IndexOf(' ');
            if (space <= 0)
                return $"present (length {header.Length})";

            var scheme = header.Substring(0, space);
            var tokenLength = header.Length - space - 1;
            return $"{scheme} token length {tokenLength}";
        }
    }
}


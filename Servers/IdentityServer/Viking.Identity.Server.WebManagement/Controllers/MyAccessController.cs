using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.WebManagement.Models.UserViewModels;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    [Authorize]
    public class MyAccessController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MyAccessController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var model = new MyAccessViewModel
            {
                IsAuthenticated = true,
                Username = User.Identity?.Name ?? "Guest"
            };

            var userId = _userManager.GetUserId(User);
            if (string.IsNullOrEmpty(userId))
            {
                return View(model);
            }

            var volumePermissions = await _context.UserResourcePermissionsByType(
                userId,
                new[] { nameof(Volume) });

            if (volumePermissions.Count > 0)
            {
                var volumeIds = volumePermissions.Keys.ToArray();
                var volumes = await _context.Volume
                    .Where(v => volumeIds.Contains(v.Id))
                    .ToListAsync();

                model.AccessibleVolumes = volumes
                    .Select(v => new VolumeAccessInfo
                    {
                        Id = v.Id,
                        Name = v.Name,
                        Description = v.Description ?? string.Empty,
                        Endpoint = v.Endpoint?.ToString() ?? string.Empty,
                        Permissions = volumePermissions.TryGetValue(v.Id, out var perms)
                            ? perms.ToList()
                            : new List<string>()
                    })
                    .OrderBy(v => v.Name)
                    .ToList();
            }

            var segmentationPermissions = await _context.UserResourcePermissionsByType(
                userId,
                new[] { nameof(SegmentationService) });

            if (segmentationPermissions.Count > 0)
            {
                var serviceIds = segmentationPermissions.Keys.ToArray();
                var services = await _context.SegmentationServices
                    .Where(s => serviceIds.Contains(s.Id))
                    .ToListAsync();

                model.AccessibleSegmentationServices = services
                    .Select(s => new SegmentationServiceAccessInfo
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Description = s.Description ?? string.Empty,
                        Endpoint = s.Endpoint?.ToString() ?? string.Empty,
                        Permissions = segmentationPermissions.TryGetValue(s.Id, out var perms)
                            ? perms.ToList()
                            : new List<string>()
                    })
                    .OrderBy(s => s.Name)
                    .ToList();
            }

            return View(model);
        }
    }
}

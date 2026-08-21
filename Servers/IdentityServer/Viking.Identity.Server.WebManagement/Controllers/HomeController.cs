using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.Authorization;
using Viking.Identity.Server.WebManagement.Models;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    public class HomeController : Controller
    {
        private const int DashboardPreviewCount = 10;

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IAuthorizationService _authorization;

        public HomeController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IAuthorizationService authorization)
        {
            _context = context;
            _userManager = userManager;
            _authorization = authorization;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                Username = User.Identity?.Name ?? "Guest",
                IsAdmin = User.IsInRole(Special.Roles.Admin)
            };

            if (model.IsAuthenticated)
            {
                var userId = _userManager.GetUserId(User);
                if (!string.IsNullOrEmpty(userId))
                {
                    var volumes = await GetAccessibleVolumesAsync();
                    var segmentationServices = await GetAccessibleSegmentationServicesAsync();
                    var organizations = await GetAccessibleOrganizationsAsync(userId, volumes, segmentationServices);
                    var groups = await GetCallerGroupsAsync(userId);

                    model.TotalVolumes = volumes.Count;
                    model.TotalSegmentationServices = segmentationServices.Count;
                    model.TotalOrganizations = organizations.Count;
                    model.TotalGroups = groups.Count;
                    model.UserVolumes = volumes.Take(DashboardPreviewCount).ToList();
                    model.UserSegmentationServices = segmentationServices.Take(DashboardPreviewCount).ToList();
                    model.UserOrganizations = organizations.Take(DashboardPreviewCount).ToList();
                    model.UserGroups = groups.Take(DashboardPreviewCount).ToList();
                }

                if (model.IsAdmin)
                {
                    model.TotalUsers = await _context.Users.CountAsync();
                }
            }

            ViewData["Title"] = "Dashboard";
            return View(model);
        }

        public IActionResult About()
        {
            ViewData["Message"] = "";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "";

            return View();
        }

        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        /// <summary>
        /// Volumes the caller may access via grants, group membership, site admin, or parent-org administration.
        /// </summary>
        private async Task<List<Volume>> GetAccessibleVolumesAsync()
        {
            var volumes = await _context.Volume.Include(v => v.Parent).ToListAsync();
            var accessible = await _authorization.FilterAccessibleResourcesAsync(
                _context, User, volumes, nameof(Volume));
            return accessible.OrderBy(v => v.Name).ToList();
        }

        /// <summary>
        /// Segmentation services the caller may access via grants, group membership, site admin, or parent-org administration.
        /// </summary>
        private async Task<List<SegmentationService>> GetAccessibleSegmentationServicesAsync()
        {
            var services = await _context.SegmentationServices.Include(s => s.Parent).ToListAsync();
            var accessible = await _authorization.FilterAccessibleResourcesAsync(
                _context, User, services, nameof(SegmentationService));
            return accessible.OrderBy(s => s.Name).ToList();
        }

        /// <summary>
        /// Organizations the caller administers, plus parents of volumes/services they can already access.
        /// </summary>
        private async Task<List<OrganizationalUnit>> GetAccessibleOrganizationsAsync(
            string userId,
            IReadOnlyCollection<Volume> accessibleVolumes,
            IReadOnlyCollection<SegmentationService> accessibleServices)
        {
            var grantedIds = (await _context.UserResourcePermissionsByType(userId, new[] { nameof(OrganizationalUnit) })).Keys.ToHashSet();
            foreach (var parentId in accessibleVolumes.Select(v => v.ParentID)
                .Concat(accessibleServices.Select(s => s.ParentID))
                .Where(id => id.HasValue)
                .Select(id => id.Value))
            {
                grantedIds.Add(parentId);
            }

            var orgs = await _context.OrgUnit.Include(o => o.Parent).ToListAsync();
            return orgs
                .Where(o => grantedIds.Contains(o.Id))
                .OrderBy(o => o.Name)
                .ToList();
        }

        /// <summary>
        /// Groups the caller belongs to, excluding the virtual Anonymous group.
        /// </summary>
        private async Task<List<Group>> GetCallerGroupsAsync(string userId)
        {
            var groups = await _context.RecursiveMemberOfGroups(userId);
            return groups
                .Where(g => g.Id != Special.Groups.Anonymous.Id)
                .GroupBy(g => g.Id)
                .Select(g => g.First())
                .OrderBy(g => g.Name)
                .ToList();
        }
    }
}

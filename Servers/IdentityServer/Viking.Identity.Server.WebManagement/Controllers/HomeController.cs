using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.WebManagement.Models;

namespace Viking.Identity.Server.WebManagement.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var model = new DashboardViewModel
            {
                IsAuthenticated = User.Identity?.IsAuthenticated ?? false,
                Username = User.Identity?.Name ?? "Guest",
                IsAdmin = User.IsInRole("Admin")
            };

            if (model.IsAuthenticated)
            {
                // Get total counts
                model.TotalUsers = await _context.Users.CountAsync();
                model.TotalOrganizations = await _context.OrgUnit.CountAsync();
                model.TotalVolumes = await _context.Volume.CountAsync();
                model.TotalSegmentationServices = await _context.SegmentationServices.CountAsync();
                model.TotalGroups = await _context.Group.CountAsync();

                // Get user's organizations and volumes - simplified for now
                // This will be enhanced when we have the full permission system in place
                if (!string.IsNullOrEmpty(model.Username))
                {
                    var user = await _userManager.FindByNameAsync(model.Username);
                    if (user != null)
                    {
                        // Get volumes user has direct access to
                        var userVolumeIds = await _context.GrantedUserPermissions
                            .Where(gup => gup.UserId == user.Id)
                            .Select(gup => gup.ResourceId)
                            .Distinct()
                            .ToListAsync();

                        if (userVolumeIds.Any())
                        {
                            model.UserVolumes = await _context.Volume
                                .Where(v => userVolumeIds.Contains(v.Id))
                                .Include(v => v.Parent)
                                .Take(10)
                                .ToListAsync();
                        }

                        var userSegmentationIds = await _context.GrantedUserPermissions
                            .Where(gup => gup.UserId == user.Id)
                            .Select(gup => gup.ResourceId)
                            .Distinct()
                            .ToListAsync();

                        if (userSegmentationIds.Any())
                        {
                            model.UserSegmentationServices = await _context.SegmentationServices
                                .Where(s => userSegmentationIds.Contains(s.Id))
                                .Include(s => s.Parent)
                                .Take(10)
                                .ToListAsync();
                        }
                    }
                }
            }
            else
            {
                // For non-authenticated users, show public stats only if admin, otherwise 0
                model.TotalUsers = 0;
                model.TotalOrganizations = 0;
                model.TotalVolumes = 0;
                model.TotalSegmentationServices = 0;
                model.TotalGroups = 0;
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
    }
}

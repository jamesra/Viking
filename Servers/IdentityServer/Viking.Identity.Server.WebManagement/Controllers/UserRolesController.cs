using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Viking.Identity.Data;
using Viking.Identity.Models;
using Viking.Identity.Server.WebManagement.Models;
using Viking.Identity.Server.WebManagement.Models.UserViewModels;

namespace Viking.Identity.Server.WebManagement.Controllers
{

    [Route("[controller]/[action]")]
    [Authorize]
    public class UserRolesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger _logger;

        public UserRolesController(ApplicationDbContext context,
            ILogger<UserRolesController> logger)
        {
            _context = context;
            _logger = logger;
        }
          
        // GET: UserRoles 
        public Task<IActionResult> Index()
        {
            List<ApplicationRole> AvailableRoles = _context.ApplicationRole.ToList();

            var UserRolesModels = (from user in _context.ApplicationUser 
                                  select new UserRolesViewModel
                                  {
                                      Username = user.UserName,
                                      Roles = _context.UserRoles.Where(ur => ur.UserId == user.Id).Select(ur => ur.RoleId).ToList()
                                  }).ToList();

            var listRoles = new ListUserRolesViewModel() { AvailableRoles = AvailableRoles, UsersRoles = UserRolesModels };

            return Task<IActionResult>.FromResult((IActionResult)View(listRoles));
        }

        private bool IsChecked(Microsoft.Extensions.Primitives.StringValues val)
        {
            return val[0] != "false";
        }

        // POST: UserRoles/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = Special.Roles.Admin)]
        public ActionResult Edit(UserRolesViewModel id, IFormCollection collection)
        {
            try
            {
                var User = _context.ApplicationUser.FirstOrDefault(u => u.UserName == id.Username);
                List<ApplicationRole> AvailableRoles = _context.ApplicationRole.ToList();
                var listUserRoles = _context.UserRoles.Where(ur => ur.UserId == User.Id).ToList();
                
                foreach(var userRole in AvailableRoles)
                {
                    var form = collection[userRole.Id];
                    bool check = IsChecked(form);
                    if(check && !listUserRoles.Any(ur => ur.RoleId == userRole.Id))
                    {
                        _context.UserRoles.Add(new IdentityUserRole<string>() { UserId = User.Id, RoleId = userRole.Id });
                    }
                    else if(!check && listUserRoles.Any(ur => ur.RoleId == userRole.Id))
                    {
                        //Safety check, make sure we do not remove the last admin user from the admin role
                        if(userRole.Name == Special.Roles.Admin)
                        {
                            var adminRoleId = _context.Roles.FirstOrDefault(ur => ur.Name == Special.Roles.Admin).Id;
                            bool otherAdminUsers = _context.UserRoles.Where(ur => ur.RoleId == adminRoleId && ur.UserId != User.Id).Any();
                            if(otherAdminUsers== false)
                            {
                                _logger.LogWarning("Cannot remove the last admin user");
                                throw new ArgumentException("Cannot remove the last admin user");
                            }
                        }
                        var urToRemove = listUserRoles.First(ur => ur.RoleId == userRole.Id && ur.UserId == User.Id);
                        _context.UserRoles.Remove(urToRemove);
                    }
                }

                _context.SaveChanges();

                return RedirectToAction(nameof(Index));
            }
            catch(Exception e)
            {
                ErrorViewModel errorModel = new ErrorViewModel
                {
                    RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                    Details = e.Message
                };

                return View("~/Views/Shared/Error.cshtml",errorModel); 
            }
        }
    }
}

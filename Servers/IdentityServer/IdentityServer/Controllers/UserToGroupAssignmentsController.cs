using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using IdentityServer.Data;
using IdentityServer.Models;
using IdentityServer.Authorization;

namespace IdentityServer.Controllers
{
    public class UserToGroupAssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAuthorizationService _authorization;

        public UserToGroupAssignmentsController(ApplicationDbContext context, IAuthorizationService authorization)
        {
            _context = context;
            _authorization = authorization;
        }

        // GET: UserToGroupAssignments
        public async Task<IActionResult> Index(long? GroupId = null)
        {
            IQueryable<UserToGroupAssignment> applicationDbContext;
            if (GroupId.HasValue)
                applicationDbContext = _context.UserToGroupAssignments.Include(u => u.Group).Include(u => u.User).Where(utg => utg.GroupId == GroupId.Value);
            else
                applicationDbContext = _context.UserToGroupAssignments.Include(u => u.Group).Include(u => u.User);

            return View(await applicationDbContext.ToListAsync());
        }

        /*
        // GET: UserToGroupAssignments/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userToGroupAssignment = await _context.UserToGroupAssignments
                .Include(u => u.Group)
                .Include(u => u.User)
                .FirstOrDefaultAsync(m => m.GroupId == id);
            if (userToGroupAssignment == null)
            {
                return NotFound();
            }

            return View(userToGroupAssignment);
        }
        */

        // GET: UserToGroupAssignments/Create
        public IActionResult Create(long? GroupId = null)
        {
            ViewData["GroupId"] = new SelectList(_context.Group, "Id", "Name");
            ViewData["UserId"] = new SelectList(_context.ApplicationUser, "Id", "Id");
            return View();
        }

        // POST: UserToGroupAssignments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UserId,GroupId")] UserToGroupAssignment userToGroupAssignment)
        {
            var authResult = await _authorization.AuthorizeAsync(HttpContext.User, userToGroupAssignment.GroupId, IdentityServer.Authorization.Operations.GroupAccessManager);
            if (false == authResult.Succeeded)
            {
                return Unauthorized();
            }

            if (ModelState.IsValid)
            {
                _context.Add(userToGroupAssignment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            ViewData["GroupId"] = new SelectList(_context.Group, "Id", "Name", userToGroupAssignment.GroupId);
            ViewData["UserId"] = new SelectList(_context.ApplicationUser, "Id", "Id", userToGroupAssignment.UserId);
            return View(userToGroupAssignment);
        }

        // GET: UserToGroupAssignments/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userToGroupAssignment = await _context.UserToGroupAssignments.FindAsync(id);
            if (userToGroupAssignment == null)
            {
                return NotFound();
            }
            ViewData["GroupId"] = new SelectList(_context.Group, "Id", "Name", userToGroupAssignment.GroupId);
            ViewData["UserId"] = new SelectList(_context.ApplicationUser, "Id", "Id", userToGroupAssignment.UserId);
            return View(userToGroupAssignment);
        }

        // POST: UserToGroupAssignments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("UserId,GroupId")] UserToGroupAssignment userToGroupAssignment)
        {
            if (id != userToGroupAssignment.GroupId)
            {
                return NotFound();
            }

            var authResult = await _authorization.AuthorizeAsync(HttpContext.User, userToGroupAssignment.GroupId, IdentityServer.Authorization.Operations.GroupAccessManager);
            if (authResult.Succeeded == false)
            {
                return Unauthorized();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(userToGroupAssignment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserToGroupAssignmentExists(userToGroupAssignment.GroupId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["GroupId"] = new SelectList(_context.Group, "Id", "Name", userToGroupAssignment.GroupId);
            ViewData["UserId"] = new SelectList(_context.ApplicationUser, "Id", "Id", userToGroupAssignment.UserId);
            return View(userToGroupAssignment);
        }

        /*
        // GET: UserToGroupAssignments/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userToGroupAssignment = await _context.UserToGroupAssignments
                .Include(u => u.Group)
                .Include(u => u.User)
                .FirstOrDefaultAsync(m => m.GroupId == id);
            if (userToGroupAssignment == null)
            {
                return NotFound();
            }

            return View(userToGroupAssignment);
        }

        // POST: UserToGroupAssignments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        { 

            var userToGroupAssignment = await _context.UserToGroupAssignments.FindAsync(id);
            _context.UserToGroupAssignments.Remove(userToGroupAssignment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        */ 
        private bool UserToGroupAssignmentExists(long id)
        {
            return _context.UserToGroupAssignments.Any(e => e.GroupId == id);
        }
    }
}

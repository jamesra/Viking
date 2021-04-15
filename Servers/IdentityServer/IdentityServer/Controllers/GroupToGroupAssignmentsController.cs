using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IdentityServer.Data;
using IdentityServer.Models;

namespace IdentityServer.Controllers
{
    public class GroupToGroupAssignmentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GroupToGroupAssignmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: GroupToGroupAssignments
        public async Task<IActionResult> Index()
        {
            var applicationDbContext = _context.GroupToGroupAssignments.Include(g => g.Container).Include(g => g.Member);
            return View(await applicationDbContext.ToListAsync());
        }

        // GET: GroupToGroupAssignments/Details/5
        public async Task<IActionResult> Details(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var groupToGroupAssignment = await _context.GroupToGroupAssignments
                .Include(g => g.Container)
                .Include(g => g.Member)
                .FirstOrDefaultAsync(m => m.ContainerGroupId == id);
            if (groupToGroupAssignment == null)
            {
                return NotFound();
            }

            return View(groupToGroupAssignment);
        }

        // GET: GroupToGroupAssignments/Create
        public IActionResult Create()
        {
            ViewData["ContainerGroupId"] = new SelectList(_context.Group, "Id", "Name");
            ViewData["MemberGroupId"] = new SelectList(_context.Group, "Id", "Name");
            return View();
        }

        // POST: GroupToGroupAssignments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MemberGroupId,ContainerGroupId")] GroupToGroupAssignment groupToGroupAssignment)
        {
            if (ModelState.IsValid)
            {
                _context.Add(groupToGroupAssignment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ContainerGroupId"] = new SelectList(_context.Group, "Id", "Name", groupToGroupAssignment.ContainerGroupId);
            ViewData["MemberGroupId"] = new SelectList(_context.Group, "Id", "Name", groupToGroupAssignment.MemberGroupId);
            return View(groupToGroupAssignment);
        }

        // GET: GroupToGroupAssignments/Edit/5
        public async Task<IActionResult> Edit(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var groupToGroupAssignment = await _context.GroupToGroupAssignments.FindAsync(id);
            if (groupToGroupAssignment == null)
            {
                return NotFound();
            }
            ViewData["ContainerGroupId"] = new SelectList(_context.Group, "Id", "Name", groupToGroupAssignment.ContainerGroupId);
            ViewData["MemberGroupId"] = new SelectList(_context.Group, "Id", "Name", groupToGroupAssignment.MemberGroupId);
            return View(groupToGroupAssignment);
        }

        // POST: GroupToGroupAssignments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(long id, [Bind("MemberGroupId,ContainerGroupId")] GroupToGroupAssignment groupToGroupAssignment)
        {
            if (id != groupToGroupAssignment.ContainerGroupId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(groupToGroupAssignment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!GroupToGroupAssignmentExists(groupToGroupAssignment.ContainerGroupId))
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
            ViewData["ContainerGroupId"] = new SelectList(_context.Group, "Id", "Name", groupToGroupAssignment.ContainerGroupId);
            ViewData["MemberGroupId"] = new SelectList(_context.Group, "Id", "Name", groupToGroupAssignment.MemberGroupId);
            return View(groupToGroupAssignment);
        }

        // GET: GroupToGroupAssignments/Delete/5
        public async Task<IActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var groupToGroupAssignment = await _context.GroupToGroupAssignments
                .Include(g => g.Container)
                .Include(g => g.Member)
                .FirstOrDefaultAsync(m => m.ContainerGroupId == id);
            if (groupToGroupAssignment == null)
            {
                return NotFound();
            }

            return View(groupToGroupAssignment);
        }

        // POST: GroupToGroupAssignments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(long id)
        {
            var groupToGroupAssignment = await _context.GroupToGroupAssignments.FindAsync(id);
            _context.GroupToGroupAssignments.Remove(groupToGroupAssignment);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool GroupToGroupAssignmentExists(long id)
        {
            return _context.GroupToGroupAssignments.Any(e => e.ContainerGroupId == id);
        }
    }
}

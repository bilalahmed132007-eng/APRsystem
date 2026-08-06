using APRsystem.Authorization;
using APRsystem.Data;
using APRsystem.Models;
using APRsystem.Models.Identity;
using APRsystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


namespace APRsystem.Controllers
{
    public class PostingKPIsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public PostingKPIsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Access rule: HR/Admin can manage KPIs on any posting. Otherwise, only the
        // employee's own supervisor (relationship-based, same pattern as the rest of
        // this app — not a role check) can add KPIs for their direct report's posting.
        private async Task<bool> CanManageKpisForPostingAsync(int postingId)
        {
            var posting = await _context.Postings
                .Include(p => p.Employee)
                .FirstOrDefaultAsync(p => p.Id == postingId);

            if (posting == null)
                return false;

            if (User.IsInRole("HR") || User.IsInRole("Admin"))
                return true;

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            return currentEmployee != null && posting.Employee.SupervisorId == currentEmployee.Id;
        }

        public async Task<IActionResult> Create(int postingId)
        {
            var postingExists = await _context.Postings.AnyAsync(p => p.Id == postingId);
            if (!postingExists)
                return NotFound();

            if (!await CanManageKpisForPostingAsync(postingId))
                return Forbid();

            var model = new PostingKPICreateViewModel
            {
                PostingId = postingId
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PostingKPICreateViewModel model)
        {
            var postingExists = await _context.Postings.AnyAsync(p => p.Id == model.PostingId);
            if (!postingExists)
                return NotFound();

            if (!await CanManageKpisForPostingAsync(model.PostingId))
                return Forbid();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var duplicateExists = await _context.PostingKPIs
                .AnyAsync(pk => pk.PostingId == model.PostingId && pk.Title == model.Title && pk.IsActive);

            if (duplicateExists)
            {
                ModelState.AddModelError(nameof(model.Title), "A KPI with this title already exists for this posting.");
                return View(model);
            }

            var postingKPI = new PostingKPI
            {
                PostingId = model.PostingId,
                Title = model.Title,
                Description = model.Description,
                Weight = model.Weight,
                IsActive = true
            };

            _context.PostingKPIs.Add(postingKPI);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Postings", new { id = model.PostingId });
        }
    }
}
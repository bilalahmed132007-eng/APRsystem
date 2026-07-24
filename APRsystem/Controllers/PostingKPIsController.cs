using APRsystem.Data;
using APRsystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APRsystem.ViewModels;

namespace APRsystem.Controllers
{
    public class PostingKPIsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PostingKPIsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Create(int postingId)
        {
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
            if (!ModelState.IsValid)
            {
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
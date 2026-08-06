using APRsystem.Data;
using APRsystem.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APRsystem.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public NotificationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: Notifications/Recent
        // Returns JSON for the bell dropdown: the current user's most recent notifications + unread count.
        [HttpGet]
        public async Task<IActionResult> Recent()
        {
            var currentEmployee = await GetCurrentEmployeeAsync();
            if (currentEmployee == null)
                return Json(new { unreadCount = 0, items = Array.Empty<object>() });

            var notifications = await _context.Notifications
                .Where(n => n.RecipientEmployeeId == currentEmployee.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(15)
                .ToListAsync();

            var unreadCount = notifications.Count(n => !n.IsRead);

            return Json(new
            {
                unreadCount,
                items = notifications.Select(n => new
                {
                    n.Id,
                    n.Message,
                    n.Url,
                    n.IsRead,
                    createdAt = n.CreatedAt.ToString("dd MMM, HH:mm")
                })
            });
        }

        // POST: Notifications/MarkRead/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkRead(int id)
        {
            var currentEmployee = await GetCurrentEmployeeAsync();
            if (currentEmployee == null) return Forbid();

            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.RecipientEmployeeId == currentEmployee.Id);

            if (notification == null) return NotFound();

            notification.IsRead = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: Notifications/MarkAllRead
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var currentEmployee = await GetCurrentEmployeeAsync();
            if (currentEmployee == null) return Forbid();

            var unread = await _context.Notifications
                .Where(n => n.RecipientEmployeeId == currentEmployee.Id && !n.IsRead)
                .ToListAsync();

            foreach (var n in unread)
                n.IsRead = true;

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        private async Task<APRsystem.Models.Employee?> GetCurrentEmployeeAsync()
        {
            var currentUserId = _userManager.GetUserId(User);
            return await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);
        }
    }
}
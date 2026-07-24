using APRsystem.Authorization;
using APRsystem.Data;
using APRsystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace APRsystem.Controllers
{
    [Authorize(Policy = Permissions.AuditLogsView)]
    public class AuditLogsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: AuditLogs
        // GET: AuditLogs
        public async Task<IActionResult> Index(AuditLogFilterViewModel filter)
        {
            var query = _context.AuditLogs.AsQueryable();

            ViewBag.DebugConnectionString = _context.Database.GetConnectionString();
            ViewBag.DebugRawCount = await _context.AuditLogs.CountAsync();
            ViewBag.DebugFilterState =
                $"UserName='{filter.UserName}' | " +
                $"Action='{filter.Action}' | " +
                $"EntityName='{filter.EntityName}' | " +
                $"FromDate='{filter.FromDate}' | " +
                $"ToDate='{filter.ToDate}' | " +
                $"Page={filter.Page} | " +
                $"PageSize={filter.PageSize}";

            if (!string.IsNullOrWhiteSpace(filter.UserName))
            {
                query = query.Where(a => a.UserName != null && a.UserName.Contains(filter.UserName));
            }

            if (!string.IsNullOrWhiteSpace(filter.Action))
            {
                query = query.Where(a => a.Action == filter.Action);
            }

            if (!string.IsNullOrWhiteSpace(filter.EntityName))
            {
                query = query.Where(a => a.EntityName == filter.EntityName);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(a => a.Timestamp >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                // Include the entire ToDate day
                var inclusiveEnd = filter.ToDate.Value.Date.AddDays(1);
                query = query.Where(a => a.Timestamp < inclusiveEnd);
            }

            filter.TotalCount = await query.CountAsync();

            if (filter.Page < 1) filter.Page = 1;
            if (filter.PageSize < 1) filter.PageSize = 25;

            filter.Results = await query
                .OrderByDescending(a => a.Timestamp)
                .Skip((filter.Page - 1) * filter.PageSize)
                .Take(filter.PageSize)
                .ToListAsync();

            // Populate dropdowns from distinct values actually present in the table
            filter.ActionOptions = await _context.AuditLogs
                .Select(a => a.Action)
                .Distinct()
                .OrderBy(a => a)
                .Select(a => new SelectListItem(a, a))
                .ToListAsync();

            filter.EntityOptions = await _context.AuditLogs
                .Select(a => a.EntityName)
                .Distinct()
                .OrderBy(e => e)
                .Select(e => new SelectListItem(e, e))
                .ToListAsync();

            return View(filter);
        }
        // GET: AuditLogs/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var log = await _context.AuditLogs.FirstOrDefaultAsync(a => a.Id == id);

            if (log == null)
            {
                return NotFound();
            }

            return View(log);
        }
    }
}
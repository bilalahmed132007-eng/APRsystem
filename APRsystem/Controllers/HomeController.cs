using APRsystem.Data;
using APRsystem.Models;
using APRsystem.Models.Identity;
using APRsystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace APRsystem.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var isAdminOrHR = User.IsInRole("Admin") || User.IsInRole("HR");
            var isSupervisor = User.IsInRole("Supervisor");

            var model = new DashboardViewModel
            {
                IsAdminOrHR = isAdminOrHR,
                IsSupervisor = isSupervisor
            };
            if (!isAdminOrHR && !isSupervisor)
            {
                var currentUserId = _userManager.GetUserId(User);
                var myEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

                ViewBag.CurrentEmployeeId = myEmployee?.Id;
            }

            if (isAdminOrHR)
            {
                // Org-wide counts
                model.EmployeeCount = await _context.Employees.CountAsync();
                model.PostingCount = await _context.Postings.CountAsync();
                model.KpiCount = await _context.KPIs.CountAsync();
                model.ContractCount = await _context.Contracts.CountAsync();
                model.DepartmentCount = await _context.Departments.CountAsync();
                model.LookupCount = await _context.Lookups.CountAsync();
            }
            else if (isSupervisor)
            {
                // Scoped to their team
                var currentUserId = _userManager.GetUserId(User);
                var supervisorEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

                var supervisorId = supervisorEmployee?.Id;

                model.EmployeeCount = await _context.Employees
                    .CountAsync(e => e.SupervisorId == supervisorId);

                model.PostingCount = await _context.Postings
                    .CountAsync(p => p.SupervisorId == supervisorId);
            }
            // Regular employees get no stats — just quick links (handled in view)

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
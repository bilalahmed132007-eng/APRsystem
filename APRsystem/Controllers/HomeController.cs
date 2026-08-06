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
            var currentEmployee = await GetCurrentEmployeeAsync();

            var isSupervisor = currentEmployee != null && await _context.Employees
                .AnyAsync(e => e.SupervisorId == currentEmployee.Id);

            var model = new DashboardViewModel
            {
                IsAdminOrHR = isAdminOrHR,
                IsSupervisor = isSupervisor
            };

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
            else
            {
                ViewBag.CurrentEmployeeId = currentEmployee?.Id;

                if (currentEmployee != null)
                {
                    var teamTree = await BuildTeamTreeAsync(currentEmployee);
                    model.TeamTree = teamTree;

                    if (isSupervisor)
                    {
                        // Team stats scoped to this supervisor's direct reports
                        model.EmployeeCount = teamTree.DirectReports.Count;
                        model.PostingCount = await _context.Postings
                            .CountAsync(p => p.SupervisorId == currentEmployee.Id);
                    }
                }
            }

            return View(model);
        }

        // GET: Home/Team — dedicated full-page view of the current user's team tree
        public async Task<IActionResult> Team()
        {
            var currentEmployee = await GetCurrentEmployeeAsync();

            if (currentEmployee == null)
            {
                return Forbid();
            }

            var teamTree = await BuildTeamTreeAsync(currentEmployee);
            return View(teamTree);
        }

        // Looks up the Employee record linked to the logged-in account, with the
        // includes needed to render designation/department on the tree cards.
        private async Task<Employee?> GetCurrentEmployeeAsync()
        {
            var currentUserId = _userManager.GetUserId(User);

            return await _context.Employees
                .Include(e => e.ApplicationUser)
                .Include(e => e.Supervisor)
                .Include(e => e.Postings.Where(p => p.ToDate == null))
                    .ThenInclude(p => p.Designation)
                .Include(e => e.Postings.Where(p => p.ToDate == null))
                    .ThenInclude(p => p.Department)
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);
        }

        // Builds Supervisor, GrandSupervisor, Teammates, and DirectReports for a given
        // employee. Shared by Index() (dashboard preview) and Team() (full page) so the
        // two never drift out of sync.
        private async Task<TeamTreeViewModel> BuildTeamTreeAsync(Employee currentEmployee)
        {
            var teamTree = new TeamTreeViewModel
            {
                CurrentEmployee = currentEmployee,
                TeamSupervisor = currentEmployee.Supervisor
            };

            if (currentEmployee.SupervisorId != null)
            {
                teamTree.Teammates = await _context.Employees
                    .Include(e => e.Postings.Where(p => p.ToDate == null))
                        .ThenInclude(p => p.Designation)
                    .Where(e => e.SupervisorId == currentEmployee.SupervisorId
                             && e.Id != currentEmployee.Id)
                    .ToListAsync();
            }

            teamTree.DirectReports = await _context.Employees
                .Include(e => e.Postings.Where(p => p.ToDate == null))
                    .ThenInclude(p => p.Designation)
                .Where(e => e.SupervisorId == currentEmployee.Id)
                .ToListAsync();

            // Supervisor's own supervisor — one hop further than the Include chain above covers.
            if (currentEmployee.Supervisor?.SupervisorId != null)
            {
                teamTree.GrandSupervisor = await _context.Employees
                    .Include(e => e.Postings.Where(p => p.ToDate == null))
                        .ThenInclude(p => p.Designation)
                    .FirstOrDefaultAsync(e => e.Id == currentEmployee.Supervisor.SupervisorId);
            }

            return teamTree;
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
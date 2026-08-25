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
                Employee = currentEmployee,
                IsSupervisor = isSupervisor
            };

            if (isAdminOrHR)
            {
                model.EmployeeCount = await _context.Employees.CountAsync();
                model.PostingCount = await _context.Postings.CountAsync();
                model.KpiCount = await _context.KPIs.CountAsync();
                model.ContractCount = await _context.Contracts.CountAsync();
                model.DepartmentCount = await _context.Departments.CountAsync();
                model.LookupCount = await _context.Lookups.CountAsync();

                model.TeamTree = await BuildFullOrgTreeAsync();
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
                        model.EmployeeCount = teamTree.DirectReports.Count;
                        model.PostingCount = await _context.Postings
                            .CountAsync(p => p.SupervisorId == currentEmployee.Id);
                    }
                }
            }

            return View(model);
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
        // GET: Home/Team — dedicated full-page view of the current user's team tree
        // GET: Home/Team — dedicated full-page view of the team tree.
        // Admin/HR see the whole org from the top; everyone else sees their own branch.
        public async Task<IActionResult> Team()
        {
            var isAdminOrHR = User.IsInRole("Admin") || User.IsInRole("HR");

            if (isAdminOrHR)
            {
                var orgTree = await BuildFullOrgTreeAsync();
                if (orgTree == null)
                {
                    return NotFound(); // no employees in the system at all
                }
                return View(orgTree);
            }

            var currentEmployee = await GetCurrentEmployeeAsync();

            if (currentEmployee == null)
            {
                return Forbid();
            }

            var teamTree = await BuildTeamTreeAsync(currentEmployee);
            return View(teamTree);
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

            // Full downline (direct reports + their reports + ...), built in one bulk
            // fetch instead of one query per level.
            teamTree.Subordinates = await BuildSubordinateSubtreeAsync(currentEmployee.Id);

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

        // Loads every employee once, groups by SupervisorId in memory, then walks the
        // chain recursively from rootEmployeeId. This is O(1) queries regardless of
        // how many levels deep the hierarchy goes.
        private async Task<List<TeamNode>> BuildSubordinateSubtreeAsync(int rootEmployeeId)
        {
            var allEmployees = await _context.Employees
                .Include(e => e.Postings.Where(p => p.ToDate == null))
                    .ThenInclude(p => p.Designation)
                .ToListAsync();

            var bySupervisor = allEmployees
                .Where(e => e.SupervisorId != null)
                .GroupBy(e => e.SupervisorId!.Value)
                .ToDictionary(g => g.Key, g => g.ToList());

            List<TeamNode> BuildChildren(int supervisorId, HashSet<int> visited)
            {
                if (!bySupervisor.TryGetValue(supervisorId, out var directReports))
                {
                    return new List<TeamNode>();
                }

                var nodes = new List<TeamNode>();
                foreach (var report in directReports)
                {
                    // Guards against a corrupted SupervisorId chain forming a cycle,
                    // which would otherwise recurse forever.
                    if (!visited.Add(report.Id))
                    {
                        continue;
                    }

                    nodes.Add(new TeamNode
                    {
                        Employee = report,
                        Children = BuildChildren(report.Id, visited)
                    });
                }

                return nodes;
            }

            return BuildChildren(rootEmployeeId, new HashSet<int> { rootEmployeeId });
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
        // Builds the tree for Admin/HR: rooted at the top of the org (no SupervisorId),
        // not at the viewer's own record. If there's more than one top-level employee,
        // the first one (by name) is used as the visual root — flagging this below.
        private async Task<TeamTreeViewModel?> BuildFullOrgTreeAsync()
        {
            var topLevelEmployees = await _context.Employees
                .Include(e => e.Postings.Where(p => p.ToDate == null))
                    .ThenInclude(p => p.Designation)
                .Where(e => e.SupervisorId == null)
                .OrderBy(e => e.FullName)
                .ToListAsync();

            var root = topLevelEmployees.FirstOrDefault();
            if (root == null)
            {
                return null;
            }

            var teamTree = new TeamTreeViewModel
            {
                CurrentEmployee = root,
                TeamSupervisor = null,
                GrandSupervisor = null,
                Teammates = topLevelEmployees.Where(e => e.Id != root.Id).ToList(),
                IsOrgWideView = true
            };

            teamTree.DirectReports = await _context.Employees
                .Include(e => e.Postings.Where(p => p.ToDate == null))
                    .ThenInclude(p => p.Designation)
                .Where(e => e.SupervisorId == root.Id)
                .ToListAsync();

            teamTree.Subordinates = await BuildSubordinateSubtreeAsync(root.Id);

            return teamTree;
        }
    }
}
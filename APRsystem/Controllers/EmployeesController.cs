using APRsystem.Data;
using APRsystem.Models;
using APRsystem.Models.Identity;
using APRsystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using APRsystem.Authorization;

public class EmployeesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public EmployeesController(
     ApplicationDbContext context,
     UserManager<ApplicationUser> userManager,
     RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    // GET: EMPLOYEES
    public async Task<IActionResult> Index()
    {
        var employeesQuery = _context.Employees
            .Include(e => e.Supervisor)
            .Include(e => e.Postings.Where(p => p.ToDate == null || p.ToDate >= DateTime.Today))
                .ThenInclude(p => p.Department)
            .AsQueryable();

        if (User.IsInRole("Admin") || User.IsInRole("HR"))
        {
            // Admin and HR see everyone — no filter
        }
        else
        {
            var currentUserId = _userManager.GetUserId(User);

            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            var currentEmployeeId = currentEmployee?.Id;

            // "Supervisor" is no longer a role — it's determined by whether anyone's
            // SupervisorId points at this employee.
            var hasDirectReports = currentEmployeeId != null && await _context.Employees
                .AnyAsync(e => e.SupervisorId == currentEmployeeId);

            ViewBag.SupervisorId = currentEmployeeId;

            if (hasDirectReports && currentEmployeeId != null)
            {
                // Everyone in the caller's full downline (not just direct reports),
                // plus the caller themselves.
                var subordinateIds = await GetAllSubordinateIdsAsync(currentEmployeeId.Value);

                employeesQuery = employeesQuery.Where(e =>
                    e.ApplicationUserId == currentUserId ||
                    subordinateIds.Contains(e.Id));
            }
            else
            {
                employeesQuery = employeesQuery.Where(e => e.ApplicationUserId == currentUserId);
            }
        }

        return View(await employeesQuery.ToListAsync());
    }

    // GET: EMPLOYEES/Details/5
    // Visibility: Admin/HR (anyone), self, my supervisor, my teammates (same supervisor),
    // and anyone in my full downline — i.e. my direct reports, their reports, and so on.
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees
    .Include(e => e.Supervisor)
        .ThenInclude(s => s.Postings.Where(p => p.ToDate == null || p.ToDate >= DateTime.Today))
            .ThenInclude(p => p.Designation)
    .Include(e => e.Supervisor)
        .ThenInclude(s => s.Postings.Where(p => p.ToDate == null || p.ToDate >= DateTime.Today))
            .ThenInclude(p => p.Department)
    .FirstOrDefaultAsync(m => m.Id == id);

        if (employee == null)
        {
            return NotFound();
        }

        var currentPosting = await _context.Postings
            .Include(p => p.Department)
            .Include(p => p.Designation)
            .Include(p => p.Location)
            .Include(p => p.Contract)
            .Include(p => p.Supervisor)
            .Where(p => p.EmployeeId == employee.Id && (p.ToDate == null || p.ToDate >= DateTime.Today))
            .OrderByDescending(p => p.FromDate)
            .FirstOrDefaultAsync();

        ViewBag.CurrentPosting = currentPosting;
        var teamMembers = await _context.Employees
    .Include(e => e.Postings.Where(p => p.ToDate == null || p.ToDate >= DateTime.Today))
        .ThenInclude(p => p.Designation)
    .Where(e => e.SupervisorId == employee.Id)
    .ToListAsync();

        ViewBag.TeamMembers = teamMembers;
        var teammates = new List<Employee>();

        if (employee.SupervisorId != null)
        {
            teammates = await _context.Employees
                .Include(e => e.Postings.Where(p => p.ToDate == null || p.ToDate >= DateTime.Today))
                    .ThenInclude(p => p.Designation)
                .Where(e => e.SupervisorId == employee.SupervisorId && e.Id != employee.Id)
                .ToListAsync();
        }

        ViewBag.Teammates = teammates;
        Employee? grandSupervisor = null;
        if (employee.Supervisor?.SupervisorId != null)
        {
            grandSupervisor = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == employee.Supervisor.SupervisorId);
        }

        if (User.IsInRole("Admin") || User.IsInRole("HR"))
        {
            var vm = new EmployeeDetailsViewModel
            {
                Employee = employee,
                CurrentPosting = currentPosting,
                Supervisor = employee.Supervisor,
                Teammates = teammates,
                GrandSupervisor = grandSupervisor,
                DirectReports = teamMembers,
                CanViewPostings = true
            };

            return View(vm);
        }

        var currentUserId = _userManager.GetUserId(User);

        var currentEmployee = await _context.Employees
            .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

        if (currentEmployee == null)
        {
            return Forbid();
        }

        var isSelf = employee.Id == currentEmployee.Id;
        var isMySupervisor = currentEmployee.SupervisorId == employee.Id;
        var isMyTeammate = employee.SupervisorId != null
                            && employee.SupervisorId == currentEmployee.SupervisorId;

        // Walks employee's Supervisor chain upward (employee -> supervisor -> supervisor's
        // supervisor -> ...) looking for currentEmployee.Id anywhere in it. True for a
        // direct report, a report's report, and so on, at any depth.
        var isMySubordinate = await IsInSupervisorChainAsync(employee.Id, currentEmployee.Id);

        var canViewPostings = isSelf || isMySubordinate;

        if (isSelf || isMySupervisor || isMySubordinate || isMyTeammate)
        {
            var vm = new EmployeeDetailsViewModel
            {
                Employee = employee,
                CurrentPosting = currentPosting,
                Supervisor = employee.Supervisor,
                Teammates = teammates,
                GrandSupervisor = grandSupervisor,
                DirectReports = teamMembers,
                CanViewPostings = canViewPostings

            };

            return View(vm);
        }

        return Forbid();
    }

    // GET: EMPLOYEES/Create
    [Authorize(Policy = Permissions.UsersCreate)]
    public IActionResult Create()
    {
        PopulateDropdowns();
        return View(new CreateEmployeeViewModel());

    }

    // POST: Employees/Create
    [Authorize(Policy = Permissions.UsersCreate)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEmployeeViewModel model)
    {
        if (!ModelState.IsValid)
        {
            PopulateDropdowns();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            IsActive = true
        };

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                PopulateDropdowns();
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, model.SelectedRole);

            var employee = new Employee
            {
                EmployeeNo = model.EmployeeNo,
                FullName = model.FullName,
                CNIC = model.CNIC,
                Email = model.Email,
                Phone = model.Phone,
                JoiningDate = model.JoiningDate,

                IsActive = model.IsActive,
                ApplicationUserId = user.Id,
                SupervisorId = model.SupervisorId

            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync(); // need employee.Id below

            var contract = new Contract
            {
                EmployeeId = employee.Id,
                ContractNumber = model.ContractNumber,
                Type = model.ContractType,
                StartDate = model.ContractStartDate,
                EndDate = model.ContractEndDate,
                IsActive = true
            };

            _context.Contracts.Add(contract);
            await _context.SaveChangesAsync(); // need contract.Id below

            var posting = new Posting
            {
                EmployeeId = employee.Id,
                ContractId = contract.Id,
                DepartmentId = model.DepartmentId,
                DesignationId = model.DesignationId,
                LocationId = model.LocationId,
                Salary = model.Salary,
                SupervisorId = model.SupervisorId,
                FromDate = model.PostingFromDate,
                ToDate = null
            };

            _context.Postings.Add(posting);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            return RedirectToAction(nameof(Index));
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();

            if (!string.IsNullOrEmpty(user.Id))
            {
                var createdUser = await _userManager.FindByIdAsync(user.Id);
                if (createdUser != null)
                {
                    await _userManager.DeleteAsync(createdUser);
                }
            }

            ModelState.AddModelError("", "An error occurred while creating the employee. Please try again.");

            PopulateDropdowns();
            return View(model);
        }
    }

    // GET: EMPLOYEES/Edit/5
    // Edits Employee's own fields only. Department/Designation/Location/Salary changes
    // must go through a Posting transfer, not here — that's what preserves history.
    [Authorize(Policy = Permissions.UsersEdit)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees.FindAsync(id);
        if (employee == null)
        {
            return NotFound();
        }

        PopulateDropdowns(employee);
        return View(employee);
    }

    // POST: EMPLOYEES/Edit/5
    [Authorize(Policy = Permissions.UsersEdit)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,EmployeeNo,FullName,CNIC,Email,Phone,JoiningDate,IsActive,SupervisorId,ApplicationUserId")] Employee employee)
    {
        if (id != employee.Id)
        {
            return NotFound();
        }

        ModelState.Remove("Supervisor");
        ModelState.Remove("ApplicationUser");

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(employee);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EmployeeExists(employee.Id))
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

        PopulateDropdowns(employee);
        return View(employee);
    }

    // GET: EMPLOYEES/Delete/5
    [Authorize(Policy = Permissions.UsersDelete)]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees
            .Include(e => e.Supervisor)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (employee == null)
        {
            return NotFound();
        }

        return View(employee);
    }

    // POST: EMPLOYEES/Delete/5
    [Authorize(Policy = Permissions.UsersDelete)]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var employee = await _context.Employees.FindAsync(id);
        if (employee != null)
        {
            _context.Employees.Remove(employee);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool EmployeeExists(int? id)
    {
        return _context.Employees.Any(e => e.Id == id);
    }

    // Walks upward from startEmployeeId via SupervisorId (startEmployeeId -> its
    // supervisor -> that supervisor's supervisor -> ...) looking for targetAncestorId
    // anywhere in the chain. Used to check "is this employee somewhere in my downline",
    // which a single SupervisorId == currentEmployee.Id check can't answer for anyone
    // more than one level down.
    private async Task<bool> IsInSupervisorChainAsync(int startEmployeeId, int targetAncestorId)
    {
        var currentId = startEmployeeId;
        var visited = new HashSet<int>();

        while (true)
        {
            if (!visited.Add(currentId))
            {
                // Corrupted SupervisorId chain forming a cycle — bail out rather than loop forever.
                return false;
            }

            var supervisorId = await _context.Employees
                .Where(e => e.Id == currentId)
                .Select(e => (int?)e.SupervisorId)
                .FirstOrDefaultAsync();

            if (supervisorId == null)
            {
                return false;
            }

            if (supervisorId == targetAncestorId)
            {
                return true;
            }

            currentId = supervisorId.Value;
        }
    }

    // Returns the full set of employee IDs in rootEmployeeId's downline (direct
    // reports, their reports, and so on), loaded via one bulk fetch + in-memory walk.
    private async Task<HashSet<int>> GetAllSubordinateIdsAsync(int rootEmployeeId)
    {
        var supervisorPairs = await _context.Employees
            .Where(e => e.SupervisorId != null)
            .Select(e => new { e.Id, SupervisorId = e.SupervisorId!.Value })
            .ToListAsync();

        var bySupervisor = supervisorPairs
            .GroupBy(p => p.SupervisorId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());

        var result = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(rootEmployeeId);

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();

            if (!bySupervisor.TryGetValue(currentId, out var directReportIds))
            {
                continue;
            }

            foreach (var reportId in directReportIds)
            {
                if (result.Add(reportId))
                {
                    queue.Enqueue(reportId);
                }
            }
        }

        return result;
    }

    private void PopulateDropdowns(Employee? employee = null)
    {
        ViewBag.DepartmentId = new SelectList(_context.Departments, "Id", "Name");
        ViewBag.DesignationId = new SelectList(
            _context.Lookups.Where(l => l.Category == "Designation"), "Id", "Label");
        ViewBag.LocationId = new SelectList(_context.Locations, "Id", "Name");
        ViewBag.SupervisorId = new SelectList(_context.Employees, "Id", "FullName", employee?.SupervisorId);
        ViewBag.RoleId = new SelectList(_roleManager.Roles.OrderBy(r => r.Name), "Name", "Name");
    }
}
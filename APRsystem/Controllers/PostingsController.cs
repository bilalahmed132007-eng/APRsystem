using APRsystem.Authorization;
using APRsystem.Data;
using APRsystem.Models;
using APRsystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


public class PostingsController : Controller
{
    private readonly ApplicationDbContext _context;

    public PostingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: POSTINGS
    // GET: POSTINGS
    [HttpGet("Postings/{EmployeeId?}")]
    public async Task<IActionResult> Index(PostingFilterViewModel filter)
    {
        var postingsQuery = _context.Postings
            .Include(p => p.Employee)
            .Include(p => p.Department)
            .Include(p => p.Designation)
            .Include(p => p.Location)
            .Include(p => p.Contract)
            .Include(p => p.Supervisor)
            .AsQueryable();

        if (User.IsInRole("Admin") || User.IsInRole("HR"))
        {
            // see everyone's postings
        }
        else
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var currentEmployeeForSupCheck = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            var currentEmployeeId = currentEmployeeForSupCheck?.Id;

            var subordinateIds = currentEmployeeId != null
                ? await GetSubordinateIdsAsync(currentEmployeeId.Value)
                : new HashSet<int>();

            if (subordinateIds.Count > 0)
            {
                postingsQuery = postingsQuery.Where(p =>
                    p.Employee.ApplicationUserId == currentUserId ||
                    subordinateIds.Contains(p.EmployeeId));
            }
            else
            {
                postingsQuery = postingsQuery.Where(p => p.Employee.ApplicationUserId == currentUserId);
            }
        }

        // ---- Filters ----
        if (filter.EmployeeId.HasValue && filter.EmployeeId > 0)
        {
            postingsQuery = postingsQuery.Where(p => p.EmployeeId == filter.EmployeeId);
        }

        if (filter.DepartmentId.HasValue && filter.DepartmentId > 0)
        {
            postingsQuery = postingsQuery.Where(p => p.DepartmentId == filter.DepartmentId);
        }

        if (!string.IsNullOrWhiteSpace(filter.EmployeeName))
        {
            postingsQuery = postingsQuery.Where(p =>
                p.Employee != null && p.Employee.FullName.Contains(filter.EmployeeName));
        }

        filter.Results = await postingsQuery
            .OrderByDescending(p => p.FromDate)
            .ToListAsync();

        // ---- Dropdown sources ----
        filter.EmployeeOptions = await _context.Employees
            .OrderBy(e => e.FullName)
            .Select(e => new SelectListItem(e.FullName, e.Id.ToString()))
            .ToListAsync();

        filter.DepartmentOptions = await _context.Departments
            .OrderBy(d => d.Name)
            .Select(d => new SelectListItem(d.Name, d.Id.ToString()))
            .ToListAsync();

        return View(filter);
    }

    // GET: POSTINGS/History/5  (5 = employeeId)
    [HttpGet("Postings/History/{employeeId?}")]
    public async Task<IActionResult> History(int? employeeId)
    {
        if (employeeId == null)
        {
            return NotFound();
        }

        var employee = await _context.Employees.FindAsync(employeeId);
        if (employee == null)
        {
            return NotFound();
        }

        if (!(User.IsInRole("Admin") || User.IsInRole("HR")))
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            bool isSelf = employee.ApplicationUserId == currentUserId;

            var currentEmployeeForSupCheck = await _context.Employees
      .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            // Direct-supervisor check — kept separate because CanInitiateAppraisal should
            // stay limited to the employee's immediate supervisor, not everyone above them.
            bool isSupervisorOfEmployee = currentEmployeeForSupCheck != null
                && employee.SupervisorId == currentEmployeeForSupCheck.Id;

            // Full-downline check — this is what actually gates whether the page can be
            // viewed at all. A higher-up should see history for anyone below them, not
            // just their direct reports.
            bool isInMyDownline = false;
            if (currentEmployeeForSupCheck != null)
            {
                var subordinateIds = await GetSubordinateIdsAsync(currentEmployeeForSupCheck.Id);
                isInMyDownline = subordinateIds.Contains(employee.Id);
            }

            if (!isSelf && !isInMyDownline)
            {
                return Forbid();
            }
            ViewBag.CanInitiateAppraisal = isSupervisorOfEmployee;
        }
        if (User.IsInRole("Admin") || User.IsInRole("HR"))
        {
            ViewBag.CanInitiateAppraisal = true;
        }

        var history = await _context.Postings
            .Include(p => p.Department)
            .Include(p => p.Designation)
            .Include(p => p.Location)
            .Include(p => p.Contract)
            .Include(p => p.Supervisor)
            .Where(p => p.EmployeeId == employeeId)
            .OrderByDescending(p => p.ToDate == null)
            .ThenByDescending(p => p.FromDate)
            .ToListAsync();

        ViewBag.EmployeeName = employee.FullName;
        ViewBag.EmployeeId = employee.Id;

        return View(history);
    }

    // GET: POSTINGS/Details/5
    [HttpGet("Postings/Details/{id?}")]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var posting = await _context.Postings
            .Include(p => p.Employee)
            .Include(p => p.Department)
            .Include(p => p.Designation)
            .Include(p => p.Location)
            .Include(p => p.Contract)
            .Include(p => p.Supervisor)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (posting == null)
            return NotFound();
        if (User.IsInRole("Admin") || User.IsInRole("HR"))
        {
            ViewBag.CanInitiateAppraisal = true;
        }
        else
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var currentEmployeeForSupCheck = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            // Kept as direct-supervisor-only on purpose — same reasoning as History() above.
            bool isSupervisorOfEmployee = currentEmployeeForSupCheck != null
                && posting.Employee?.SupervisorId == currentEmployeeForSupCheck.Id;

            ViewBag.CanInitiateAppraisal = isSupervisorOfEmployee;
        }
        var viewModel = new PostingDetailsViewModel
        {
            Posting = posting,

            GeneralKPIs = await _context.KPIs
                .Where(k => k.IsGeneral)
                .OrderBy(k => k.Title)
                .ToListAsync(),

            AssignedKPIs = await _context.PostingKPIs
                .Where(pk => pk.PostingId == posting.Id)
                .ToListAsync()
        };

        return View(viewModel);
    }

    // GET: POSTINGS/GetActiveContract/5  (5 = employeeId)
    // Called via AJAX from the Create view when the Employee dropdown changes.
    // Returns the employee's active contract so the form can show it read-only
    // and default ToDate to the contract's end date — without a full page reload.
    [HttpGet("Postings/GetActiveContract/{employeeId}")]
    [Authorize(Policy = Permissions.PostingsManage)]
    public async Task<IActionResult> GetActiveContract(int employeeId)
    {
        var contract = await _context.Contracts
            .Where(c => c.EmployeeId == employeeId && c.IsActive)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefaultAsync();

        if (contract == null)
        {
            return Json(new { found = false, message = "This employee has no active contract." });
        }

        return Json(new
        {
            found = true,
            contractId = contract.Id,
            contractNumber = contract.ContractNumber,
            type = contract.Type.ToString(),
            startDate = contract.StartDate.ToString("yyyy-MM-dd"),
            endDate = contract.EndDate?.ToString("yyyy-MM-dd"),
            endDateDisplay = contract.EndDate?.ToString("dd MMM yyyy") ?? "No end date"
        });
    }

    // GET: POSTINGS/Create
    // EmployeeId is picked via dropdown on the form itself (not known up front),
    // so Contract cannot be resolved server-side at GET time. The Create view's
    // JS calls GetActiveContract whenever the Employee dropdown changes, and
    // fills in the hidden ContractId + read-only contract display + ToDate default.
    [HttpGet("Postings/Create")]
    [Authorize(Policy = Permissions.PostingsManage)]
    public IActionResult Create()
    {
        PopulateDropdowns();

        var model = new CreatePostingViewModel
        {
            FromDate = DateTime.Today
        };

        return View(model);
    }

    // POST: POSTINGS/Create
    // ContractId is never trusted from the posted form — it's re-derived here from
    // the employee's active contract (same lookup as GetActiveContract), so it can't
    // be tampered with or end up mismatched with the employee, regardless of what the
    // client-side JS sent.
    [Authorize(Policy = Permissions.PostingsManage)]
    [HttpPost("Postings/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePostingViewModel model)
    {
        var employee = await _context.Employees.FindAsync(model.EmployeeId);
        if (employee == null)
        {
            ModelState.AddModelError(nameof(CreatePostingViewModel.EmployeeId), "Please select an employee.");
            PopulateDropdowns();
            return View(model);
        }

        var contract = await _context.Contracts
            .Where(c => c.EmployeeId == model.EmployeeId && c.IsActive)
            .OrderByDescending(c => c.StartDate)
            .FirstOrDefaultAsync();

        if (contract == null)
        {
            ModelState.AddModelError("", "This employee has no active contract.");
            PopulateDropdowns(new Posting { EmployeeId = model.EmployeeId });
            return View(model);
        }

        var posting = new Posting
        {
            EmployeeId = model.EmployeeId,
            ContractId = contract.Id,
            DepartmentId = model.DepartmentId,
            DesignationId = model.DesignationId,
            SupervisorId = model.SupervisorId,
            Salary = model.Salary,
            LocationId = model.LocationId,
            FromDate = model.FromDate,
            ToDate = model.ToDate
        };
        ModelState.Remove(nameof(Posting.Employee));
        ModelState.Remove(nameof(Posting.Department));
        ModelState.Remove(nameof(Posting.Designation));
        ModelState.Remove(nameof(Posting.Location));
        ModelState.Remove(nameof(Posting.Contract));
        ModelState.Remove(nameof(Posting.Supervisor));
        ModelState.Remove(nameof(CreatePostingViewModel.ContractId));

        // Posting's dates must fall inside its (derived) contract's date range
        await ValidatePostingDatesAgainstContract(posting.ContractId, posting.FromDate, posting.ToDate);

        if (!ModelState.IsValid)
        {
            PopulateDropdowns(posting);
            return View(model);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Close out every posting that would otherwise still count as "current" —
            // not just the one with ToDate == null — so the employee never ends up with
            // more than one current posting at a time. The new posting becomes the sole
            // current one; anything that overlapped it gets capped the day before it starts.
            var overlappingPostings = await _context.Postings
                .Where(p => p.EmployeeId == posting.EmployeeId &&
                            p.FromDate < posting.FromDate &&
                            (p.ToDate == null || p.ToDate >= posting.FromDate))
                .ToListAsync();

            foreach (var previous in overlappingPostings)
            {
                previous.ToDate = posting.FromDate.AddDays(-1);
                _context.Postings.Update(previous);
            }

            _context.Postings.Add(posting);

            // Keep Employee.SupervisorId in sync when this posting is the current one
            if (posting.FromDate <= DateTime.Today && (posting.ToDate == null || posting.ToDate >= DateTime.Today))
            {
                employee.SupervisorId = posting.SupervisorId;

                _context.Employees.Update(employee);
            }

            await _context.SaveChangesAsync();

            // 👈 We'll insert the "copy previous posting KPIs" code here in the next step.

            await transaction.CommitAsync();

            return RedirectToAction(nameof(Details), new { id = posting.Id });
        }
        catch
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError("", "An error occurred while creating the posting. Please try again.");
            PopulateDropdowns(posting);
            return View(model);
        }
    }




    // GET: POSTINGS/Edit/5
    // NOTE: currently allows editing Department/Designation/Location/Salary/Dates
    // on an existing posting record. Since Postings represent point-in-time history,
    // consider restricting this to Salary/Location corrections only, and require
    // EmployeeId/FromDate/ToDate changes to go through Create (new posting) +
    // closing the old one instead. Flagging for a decision, not changing behavior yet.
    [HttpGet("Postings/Edit/{id?}")]
    [Authorize(Policy = Permissions.PostingsManage)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var posting = await _context.Postings.FindAsync(id);
        if (posting == null) return NotFound();

        PopulateDropdowns(posting);
        return View(posting);
    }

    // POST: POSTINGS/Edit/5
    [Authorize(Policy = Permissions.PostingsManage)]
    [HttpPost("Postings/Edit/{id?}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,EmployeeId,ContractId,DepartmentId,DesignationId,SupervisorId,LocationId,Salary,FromDate,ToDate")] Posting posting)
    {
        if (id != posting.Id) return NotFound();

        ModelState.Remove(nameof(Posting.Employee));
        ModelState.Remove(nameof(Posting.Department));
        ModelState.Remove(nameof(Posting.Designation));
        ModelState.Remove(nameof(Posting.Location));
        ModelState.Remove(nameof(Posting.Contract));
        ModelState.Remove(nameof(Posting.Supervisor));

        // NEW: same contract date-range check on edit
        await ValidatePostingDatesAgainstContract(posting.ContractId, posting.FromDate, posting.ToDate);

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(posting);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PostingExists(posting.Id)) return NotFound();
                throw;
            }
            return RedirectToAction(nameof(Index));
        }

        PopulateDropdowns(posting);
        return View(posting);
    }

    // GET: POSTINGS/Delete/5
    [HttpGet("Postings/Delete/{id?}")]
    [Authorize(Policy = Permissions.PostingsManage)]
    public async Task<IActionResult> Delete(int? id)


    {
        if (id == null) return NotFound();

        var posting = await _context.Postings
            .Include(p => p.Employee)
            .Include(p => p.Department)
            .Include(p => p.Designation)
            .Include(p => p.Location)
            .Include(p => p.Contract)
            .Include(p => p.Supervisor)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (posting == null) return NotFound();

        return View(posting);
    }

    // POST: POSTINGS/Delete/5
    // NOTE: hard-deletes a posting, which erases part of the employee's history.
    // Consider disallowing delete entirely (history should be corrected via Edit
    // or superseded via a new Posting, not removed) — flagging, not changing yet.
    [Authorize(Policy = Permissions.PostingsManage)]
    [HttpGet("Postings/Delete/{id?}")]
    [ValidateAntiForgeryToken]

    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var posting = await _context.Postings.FindAsync(id);
        if (posting != null)
        {
            _context.Postings.Remove(posting);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private bool PostingExists(int id)
    {
        return _context.Postings.Any(p => p.Id == id);
    }

    // Returns every employee ID that sits anywhere below rootEmployeeId in the
    // hierarchy — direct reports, their reports, and so on. Same approach as the
    // equivalent helper in EmployeesController: one lightweight query plus a
    // breadth-first walk in memory.
    private async Task<HashSet<int>> GetSubordinateIdsAsync(int rootEmployeeId)
    {
        var pairs = await _context.Employees
            .Where(e => e.SupervisorId != null)
            .Select(e => new { e.Id, SupervisorId = e.SupervisorId!.Value })
            .ToListAsync();

        var bySupervisor = pairs
            .GroupBy(p => p.SupervisorId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.Id).ToList());

        var result = new HashSet<int>();
        var queue = new Queue<int>();
        queue.Enqueue(rootEmployeeId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!bySupervisor.TryGetValue(current, out var directReports))
            {
                continue;
            }

            foreach (var id in directReports)
            {
                if (result.Add(id))
                {
                    queue.Enqueue(id);
                }
            }
        }

        return result;
    }

    // Shared helper used by both Create and Edit.
    // Confirms the posting's FromDate/ToDate stay inside its Contract's StartDate/EndDate.
    // Adds ModelState errors (so they show up on the form) and returns false if invalid.
    private async Task<bool> ValidatePostingDatesAgainstContract(int contractId, DateTime fromDate, DateTime? toDate)
    {
        var contract = await _context.Contracts.FindAsync(contractId);

        if (contract == null)
        {
            ModelState.AddModelError(nameof(CreatePostingViewModel.ContractId),
                "The selected contract could not be found.");
            return false;
        }

        // Posting cannot start before contract starts
        if (fromDate < contract.StartDate)
        {
            ModelState.AddModelError(nameof(CreatePostingViewModel.FromDate),
                $"Posting start date cannot be before the contract start date ({contract.StartDate:dd MMM yyyy}).");
        }

        // Contract has an end date
        if (contract.EndDate.HasValue)
        {
            if (fromDate > contract.EndDate.Value)
            {
                ModelState.AddModelError(nameof(CreatePostingViewModel.FromDate),
                    $"Posting start date cannot be after the contract end date ({contract.EndDate.Value:dd MMM yyyy}).");
            }

            if (toDate.HasValue && toDate.Value > contract.EndDate.Value)
            {
                ModelState.AddModelError(nameof(CreatePostingViewModel.ToDate),
                    $"Posting end date cannot be after the contract end date ({contract.EndDate.Value:dd MMM yyyy}).");
            }
        }

        return ModelState.IsValid;
    }
    private void PopulateDropdowns(Posting? posting = null)
    {
        ViewBag.EmployeeId = new SelectList(_context.Employees, "Id", "FullName", posting?.EmployeeId);
        ViewBag.ContractId = new SelectList(_context.Contracts, "Id", "ContractNumber", posting?.ContractId);
        ViewBag.DepartmentId = new SelectList(_context.Departments, "Id", "Name", posting?.DepartmentId);
        ViewBag.DesignationId = new SelectList(
            _context.Lookups.Where(l => l.Category == "Designation"), "Id", "Value", posting?.DesignationId);
        ViewBag.LocationId = new SelectList(_context.Locations, "Id", "Name", posting?.LocationId);
        ViewBag.SupervisorId = new SelectList(_context.Employees, "Id", "FullName", posting?.SupervisorId);
    }
}
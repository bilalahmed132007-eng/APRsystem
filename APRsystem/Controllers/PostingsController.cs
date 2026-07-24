using APRsystem.Authorization;
using APRsystem.Data;
using APRsystem.Models;
using APRsystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

[Authorize(Policy = Permissions.PostingsManage)]
public class PostingsController : Controller
{
    private readonly ApplicationDbContext _context;

    public PostingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: POSTINGS
[HttpGet("Postings/{EmployeeId?}")]    
    public async Task<IActionResult> Index(int? EmployeeId)
    {
        
        var postingsQuery = _context.Postings
            .Include(p => p.Employee)
            .Include(p => p.Department)
            .Include(p => p.Designation)
            .Include(p => p.Location)
            .Include(p => p.Contract)
            .Include(p => p.Supervisor)
            .AsQueryable();

        if (EmployeeId > 0)
            postingsQuery = postingsQuery.Where(p => p.EmployeeId == EmployeeId);
        if (User.IsInRole("Admin") || User.IsInRole("HR"))
        {
            // see everyone's postings
        }
        else
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (User.IsInRole("Supervisor"))
            {
                var supervisorEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

                var supervisorId = supervisorEmployee?.Id;

                postingsQuery = postingsQuery.Where(p =>
                    p.Employee.ApplicationUserId == currentUserId ||
                    p.SupervisorId == supervisorId);
            }
            else
            {
                postingsQuery = postingsQuery.Where(p => p.Employee.ApplicationUserId == currentUserId);
            }
        }

        return View(await postingsQuery.OrderByDescending(p => p.FromDate).ToListAsync());
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
            bool isSupervisorOfEmployee = false;

            if (User.IsInRole("Supervisor"))
            {
                var supervisorEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

                isSupervisorOfEmployee = supervisorEmployee != null && employee.SupervisorId == supervisorEmployee.Id;
            }

            if (!isSelf && !isSupervisorOfEmployee)
            {
                return Forbid();
            }
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

    // GET: POSTINGS/Create
    [HttpGet("Postings/Create")]
    [Authorize(Policy = Permissions.PostingsManage)]
    public IActionResult Create()
    {
        PopulateDropdowns();
        return View();
    }

    // POST: POSTINGS/Create
    [Authorize(Policy = Permissions.PostingsManage)]
    [HttpPost("Postings/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePostingViewModel model)

    {
        var posting = new Posting
        {
            EmployeeId = model.EmployeeId,
            ContractId = model.ContractId,
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




        if (!ModelState.IsValid)
        {
            PopulateDropdowns(posting);
            return View(model);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // Close out any currently active posting for this employee
            var activePosting = await _context.Postings
                .Where(p => p.EmployeeId == posting.EmployeeId && p.ToDate == null)
                .FirstOrDefaultAsync();

            if (activePosting != null)
            {
                activePosting.ToDate = posting.FromDate.AddDays(-1);
                _context.Postings.Update(activePosting);
            }

            _context.Postings.Add(posting);
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
    [HttpPost("Postings/Delete/{id?}")]
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
using APRsystem.Authorization;
using APRsystem.Data;
using APRsystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

public class ContractsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ContractsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: Contracts
    [HttpGet("Contracts")]
    [Authorize(Policy = Permissions.ContractsManage)]
    public async Task<IActionResult> Index()
    {
        var contracts = await _context.Contracts
            .Include(c => c.Employee)
            .OrderBy(c => c.Employee.FullName)
            .ToListAsync();

        return View(contracts);
    }

    // GET: Contracts/Details/5
    [HttpGet("Contracts/Details/{id?}")]
    [Authorize(Policy = Permissions.ContractsManage)]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
            return NotFound();

        var contract = await _context.Contracts
            .Include(c => c.Employee)
            .Include(c => c.Postings)
                .ThenInclude(p => p.Department)
            .Include(c => c.Postings)
                .ThenInclude(p => p.Designation)
            .Include(c => c.Postings)
                .ThenInclude(p => p.Supervisor)
            .Include(c => c.Postings)
                .ThenInclude(p => p.Location)
            .OrderByDescending(c => c.Postings.Max(p => p.FromDate))
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract == null)
            return NotFound();

        // Optional: Sort postings newest first
        contract.Postings = contract.Postings
            .OrderByDescending(p => p.FromDate)
            .ToList();

        return View(contract);
    }

    // GET: Contracts/Create
    [HttpGet("Contracts/Create")]
    [Authorize(Policy = Permissions.ContractsManage)]
    public async Task<IActionResult> Create()
    {
        await PopulateDropdowns();
        return View();
    }

    // POST: Contracts/Create
    [HttpPost("Contracts/Create")]
    [Authorize(Policy = Permissions.ContractsManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("EmployeeId,ContractNumber,Type,StartDate,EndDate")] Contract contract)
    {
        ModelState.Remove(nameof(Contract.Employee));
        ModelState.Remove(nameof(Contract.Postings));

        // Permanent contracts shouldn't carry an end date
        if (contract.Type == ContractType.Permanent && contract.EndDate != null)
        {
            ModelState.AddModelError(nameof(Contract.EndDate), "Permanent contracts should not have an end date.");
        }

        // Non-permanent contracts need an end date, and it must be after the start date
        if (contract.Type != ContractType.Permanent)
        {
            if (contract.EndDate == null)
                ModelState.AddModelError(nameof(Contract.EndDate), "End date is required for this contract type.");
            else if (contract.EndDate <= contract.StartDate)
                ModelState.AddModelError(nameof(Contract.EndDate), "End date must be after the start date.");
        }

        // Enforce one contract per employee (server-side check; DB unique index backs this up)
        var alreadyHasContract = await _context.Contracts
            .AnyAsync(c => c.EmployeeId == contract.EmployeeId);

        if (alreadyHasContract)
        {
            ModelState.AddModelError("", "This employee already has a contract. Use Renew/Extend instead of creating a new one.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateDropdowns(contract);
            return View(contract);
        }

        contract.IsActive = true;
        _context.Contracts.Add(contract);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = contract.Id });
    }

    // GET: Contracts/Edit/5   (used for renew/extend, and correcting details)
    [HttpGet("Contracts/Edit/{id?}")]
    [Authorize(Policy = Permissions.ContractsManage)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var contract = await _context.Contracts
            .Include(c => c.Employee)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (contract == null) return NotFound();

        return View(contract);
    }

    // POST: Contracts/Edit/5
    [HttpPost("Contracts/Edit/{id?}")]
    [Authorize(Policy = Permissions.ContractsManage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,
        [Bind("Id,EmployeeId,ContractNumber,Type,StartDate,EndDate,IsActive")] Contract contract)
    {
        if (id != contract.Id) return NotFound();

        ModelState.Remove(nameof(Contract.Employee));
        ModelState.Remove(nameof(Contract.Postings));

        if (contract.Type == ContractType.Permanent && contract.EndDate != null)
        {
            ModelState.AddModelError(nameof(Contract.EndDate), "Permanent contracts should not have an end date.");
        }
        if (contract.Type != ContractType.Permanent)
        {
            if (contract.EndDate == null)
                ModelState.AddModelError(nameof(Contract.EndDate), "End date is required for this contract type.");
            else if (contract.EndDate <= contract.StartDate)
                ModelState.AddModelError(nameof(Contract.EndDate), "End date must be after the start date.");
        }

        // Prevent shrinking the contract's EndDate below any posting already tied to it
        if (contract.EndDate != null)
        {
            var latestPostingEnd = await _context.Postings
                .Where(p => p.ContractId == contract.Id)
                .Select(p => (DateTime?)(p.ToDate ?? p.FromDate))
                .OrderByDescending(d => d)
                .FirstOrDefaultAsync();

            var hasOpenPosting = await _context.Postings
                .AnyAsync(p => p.ContractId == contract.Id && p.ToDate == null);

            if (hasOpenPosting && contract.EndDate < DateTime.Today)
            {
                ModelState.AddModelError(nameof(Contract.EndDate),
                    "This contract has an active posting; end date can't be set in the past.");
            }

            if (latestPostingEnd != null && contract.EndDate < latestPostingEnd)
            {
                ModelState.AddModelError(nameof(Contract.EndDate),
                    $"End date can't be earlier than {latestPostingEnd:d}, the latest posting date under this contract.");
            }
        }

        if (!ModelState.IsValid)
        {
            var employee = await _context.Employees.FindAsync(contract.EmployeeId);
            contract.Employee = employee!;
            return View(contract);
        }

        try
        {
            _context.Update(contract);
            await _context.SaveChangesAsync(); // old values auto-captured in AuditLogs
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!await _context.Contracts.AnyAsync(c => c.Id == contract.Id)) return NotFound();
            throw;
        }

        return RedirectToAction(nameof(Details), new { id = contract.Id });
    }

    private async Task PopulateDropdowns(Contract? contract = null)
    {
        // Only employees who don't already have a contract are eligible for Create
        var eligibleEmployees = await _context.Employees
            .Where(e => !_context.Contracts.Any(c => c.EmployeeId == e.Id))
            .ToListAsync();

        ViewBag.EmployeeId = new SelectList(eligibleEmployees, "Id", "FullName", contract?.EmployeeId);
        ViewBag.Types = new SelectList(Enum.GetValues(typeof(ContractType)));
    }
}
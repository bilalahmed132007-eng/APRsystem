using APRsystem.Data;
using APRsystem.Models;
using APRsystem.Services;
using APRsystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;


namespace APRsystem.Controllers
{
    public class AppraisalsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly WorkflowService _workflow;

        // Category used for all appraisal-status rows in the Lookup table
        private const string StatusCategory = "AppraisalStatus";

        // Entity name as stored in Workflows.Entity
        private const string WorkflowEntity = "Appraisal";


        private readonly AppraisalPdfService _pdfService;

        private readonly AppraisalExcelService _excelService;

        public AppraisalsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            WorkflowService workflow,
            AppraisalPdfService pdfService,
            AppraisalExcelService excelService)   // <-- add this
        {
            _context = context;
            _userManager = userManager;
            _workflow = workflow;
            _pdfService = pdfService;
            _excelService = excelService;         // <-- add this
        }
        // Helper: get the Lookup.Id for a given status value (e.g. "Draft", "Approved")
        // Still used for initial creation (Draft) since that's not a transition — there's no "current state" to transition from.
        private async Task<int> GetStatusIdAsync(string value)
        {
            var lookup = await _context.Lookups
                .FirstOrDefaultAsync(l => l.Category == StatusCategory && l.Value == value && l.IsActive);

            if (lookup == null)
                throw new InvalidOperationException($"Lookup value '{value}' not found for category '{StatusCategory}'. Seed the Lookup table first.");

            return lookup.Id;
        }
        private void NotifyEmployee(int recipientEmployeeId, string message, string? url)
        {
            _context.Notifications.Add(new Notification
            {
                RecipientEmployeeId = recipientEmployeeId,
                Message = message,
                Url = url,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
            // No SaveChangesAsync here on purpose — each caller already calls
            // _context.SaveChangesAsync() once at the end of its action, and this
            // way the notification is saved atomically with the workflow transition
            // it belongs to (no half-saved state if something later in the method fails).
        }

        // HR isn't a single employee — it's a role that can be held by several people.
        // This notifies every employee whose account is in that role.
        private async Task NotifyRoleAsync(string role, string message, string? url)
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role);
            var userIds = usersInRole.Select(u => u.Id).ToList();

            var employees = await _context.Employees
                .Where(e => e.ApplicationUserId != null && userIds.Contains(e.ApplicationUserId))
                .ToListAsync();

            foreach (var emp in employees)
            {
                NotifyEmployee(emp.Id, message, url);
            }
        }
        // Helper: does this employee already have an appraisal that hasn't reached Approved yet?
        private async Task<bool> HasActiveAppraisalAsync(int employeeId)
        {
            var approvedStatusId = await GetStatusIdAsync("Approved");

            return await _context.Appraisals
                .AnyAsync(a => a.EmployeeId == employeeId && a.StatusId != approvedStatusId);
        }

        // ======================================================================
        // GET: Appraisals/BulkInitiate?departmentId=1&designationId=2&contractType=Permanent&asOfDate=2026-08-04&showNoKpisOnly=true
        // HR/Admin only — the "hidden tab" for initiating appraisals for many employees at once.
        //
        // NEW: asOfDate cross-checks each employee's Contract StartDate/EndDate — only employees
        // whose contract actually covers that date show up. This mirrors the same rule that was
        // already silently enforced later in InitiateAppraisalForEmployeeAsync (contract EndDate
        // check), just surfaced up-front as a filter instead of a skip after the fact.
        //
        // NEW: showNoKpisOnly narrows the list to only employees missing Posting-Specific KPIs,
        // so HR can quickly find who needs to be chased before initiating appraisals.
        // ======================================================================
        [HttpGet]
        public async Task<IActionResult> BulkInitiate(
     int? departmentId,
     int? designationId,
     string? contractType,
     DateTime? fromDate,
     DateTime? toDate,
     bool showNoKpisOnly = false)
        {
            if (!User.IsInRole("HR") && !User.IsInRole("Admin"))
                return Forbid();

            var postingsQuery = _context.Postings
                .Include(p => p.Employee)
                .Include(p => p.Department)
                .Include(p => p.Designation)
                .Include(p => p.Contract)
                .Where(p => p.ToDate == null || p.ToDate >= DateTime.Today) // Only current postings
                .AsQueryable();

            // Department Filter
            if (departmentId.HasValue)
                postingsQuery = postingsQuery.Where(p => p.DepartmentId == departmentId);

            // Designation Filter
            if (designationId.HasValue)
                postingsQuery = postingsQuery.Where(p => p.DesignationId == designationId);

            // Contract Type Filter
            if (!string.IsNullOrEmpty(contractType) &&
                Enum.TryParse<ContractType>(contractType, out var parsedType))
            {
                postingsQuery = postingsQuery.Where(p => p.Contract.Type == parsedType);
            }

            // Contract must overlap the appraisal period
            if (fromDate.HasValue && toDate.HasValue)
            {
                var appraisalStart = fromDate.Value.Date;
                var appraisalEnd = toDate.Value.Date;

                postingsQuery = postingsQuery.Where(p =>
                    p.Contract.StartDate <= appraisalEnd &&
                    (
                        p.Contract.EndDate == null ||
                        p.Contract.EndDate >= appraisalStart
                    ));
            }

            var postings = await postingsQuery
                .OrderBy(p => p.Employee.FullName)
                .ToListAsync();

            // Employees having active Posting KPIs
            var postingIds = postings.Select(p => p.Id).ToList();

            var postingIdsWithKpis = await _context.PostingKPIs
                .Where(pk => postingIds.Contains(pk.PostingId) && pk.IsActive)
                .Select(pk => pk.PostingId)
                .Distinct()
                .ToListAsync();

            var approvedStatusId = await GetStatusIdAsync("Approved");
            var employeeIdsInList = postings.Select(p => p.EmployeeId).ToList();

            // CHANGED: pull ALL appraisals for these employees (not just non-Approved ones),
            // so "Completed" appraisals aren't invisible to this screen.
            var allAppraisalsForEmployees = await _context.Appraisals
                .Where(a => employeeIdsInList.Contains(a.EmployeeId))
                .ToListAsync();

            // If an employee somehow has more than one appraisal record, take the most recent
            // by FromDate as the one that represents their current state on this screen.
            var latestAppraisalByEmployee = allAppraisalsForEmployees
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.FromDate).First());

            var employeeRows = postings.Select(p =>
            {
                latestAppraisalByEmployee.TryGetValue(p.EmployeeId, out var latestAppraisal);

                var status = latestAppraisal == null
                    ? BulkAppraisalStatus.Ready
                    : latestAppraisal.StatusId == approvedStatusId
                        ? BulkAppraisalStatus.Completed
                        : BulkAppraisalStatus.InProgress;

                return new BulkEmployeeRow
                {
                    EmployeeId = p.EmployeeId,
                    FullName = p.Employee.FullName,
                    EmployeeNo = p.Employee.EmployeeNo,
                    Department = p.Department?.Name ?? "-",
                    Designation = p.Designation?.Value ?? "-",
                    ContractType = p.Contract?.Type.ToString() ?? "-",
                    HasSpecificKpis = postingIdsWithKpis.Contains(p.Id),
                    Status = status,                          // <-- this was missing
                    AppraisalId = latestAppraisal?.Id
                };
            });

            // Show only employees without KPIs
            if (showNoKpisOnly)
                employeeRows = employeeRows.Where(e => !e.HasSpecificKpis);

            var model = new BulkInitiateAppraisalViewModel
            {
                DepartmentId = departmentId,
                DesignationId = designationId,
                ContractType = contractType,
                FromDate = fromDate ?? DateTime.Today,
                ToDate = toDate ?? DateTime.Today.AddYears(1),
                ShowNoKpisOnly = showNoKpisOnly,
                Employees = employeeRows.ToList()
            };

            ViewBag.DepartmentId = new SelectList(
                _context.Departments,
                "Id",
                "Name",
                departmentId);

            ViewBag.DesignationId = new SelectList(
                _context.Lookups.Where(l => l.Category == "Designation"),
                "Id",
                "Value",
                designationId);

            ViewBag.ContractTypes = Enum.GetNames(typeof(ContractType));
            ViewBag.SelectedContractType = contractType;

            return View(model);
        }
        // GET: Appraisals/ExportPdf/5
        [HttpGet]
        public async Task<IActionResult> ExportPdf(int id)
        {
            if (!User.IsInRole("HR") && !User.IsInRole("Admin"))
                return Forbid();

            var appraisal = await _context.Appraisals
                .Include(a => a.Employee)
                .Include(a => a.Supervisor)
                .Include(a => a.Status)
                .Include(a => a.AppraisalKPIs)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appraisal == null) return NotFound();

            var pdfBytes = _pdfService.GenerateSingle(appraisal);
            var fileName = $"Appraisal_{appraisal.Employee?.FullName?.Replace(" ", "_")}_{appraisal.Id}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }

        // POST: Appraisals/ExportBulkPdf
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportBulkPdf(List<int> selectedAppraisalIds)
        {
            if (!User.IsInRole("HR") && !User.IsInRole("Admin"))
                return Forbid();

            if (selectedAppraisalIds == null || !selectedAppraisalIds.Any())
            {
                TempData["Warning"] = "No appraisals were selected for export.";
                return RedirectToAction(nameof(BulkInitiate));
            }

            var appraisals = await _context.Appraisals
                .Include(a => a.Employee)
                .Include(a => a.Supervisor)
                .Include(a => a.Status)
                .Include(a => a.AppraisalKPIs)
                .Where(a => selectedAppraisalIds.Contains(a.Id))
                .OrderBy(a => a.Employee.FullName)
                .ToListAsync();

            var pdfBytes = _pdfService.GenerateBulk(appraisals);
            var fileName = $"Appraisals_Bulk_{DateTime.Today:yyyyMMdd}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        // POST: Appraisals/ExportBulkExcel
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportBulkExcel(List<int> selectedAppraisalIds)
        {
            if (!User.IsInRole("HR") && !User.IsInRole("Admin"))
                return Forbid();

            if (selectedAppraisalIds == null || !selectedAppraisalIds.Any())
            {
                TempData["Warning"] = "No appraisals were selected for export.";
                return RedirectToAction(nameof(BulkInitiate));
            }

            var appraisals = await _context.Appraisals
                .Include(a => a.Employee)
                .Include(a => a.Supervisor)
                .Include(a => a.Status)
                .Include(a => a.AppraisalKPIs)
                .Include(a => a.Posting).ThenInclude(p => p.Department)
                .Include(a => a.Posting).ThenInclude(p => p.Designation)
                .Where(a => selectedAppraisalIds.Contains(a.Id))
                .OrderBy(a => a.Employee.FullName)
                .ToListAsync();

            var excelBytes = _excelService.GenerateAnalyticsExport(appraisals);
            var fileName = $"Appraisals_Analytics_{DateTime.Today:yyyyMMdd}.xlsx";

            return File(excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }
        // ======================================================================
        // POST: Appraisals/BulkInitiate
        // Runs the same single-employee creation logic (see InitiateAppraisalForEmployeeAsync
        // below) once per selected employee, then reports a per-employee success/skip summary.
        // ======================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkInitiate(List<int> selectedEmployeeIds, DateTime fromDate, DateTime toDate, APRsystem.Models.AppraisalType type)
        {
            if (!User.IsInRole("HR") && !User.IsInRole("Admin"))
                return Forbid();

            if (selectedEmployeeIds == null || !selectedEmployeeIds.Any())
            {
                TempData["Warning"] = "No employees were selected.";
                return RedirectToAction(nameof(BulkInitiate));
            }

            var results = new List<string>();

            foreach (var employeeId in selectedEmployeeIds)
            {
                var (success, message) = await InitiateAppraisalForEmployeeAsync(employeeId, fromDate, toDate, type);
                results.Add(message);
            }

            var successCount = results.Count(r => r.StartsWith("OK:"));
            var skippedCount = results.Count - successCount;

            if (skippedCount == 0)
            {
                TempData["Success"] = $"Successfully initiated {successCount} appraisal(s).";
            }
            else
            {
                TempData["Warning"] =
                    $"Successfully initiated {successCount} appraisal(s). " +
                    $"{skippedCount} employee(s) were skipped because they already have an appraisal in progress or failed validation.";
            }
            TempData["BulkResults"] = string.Join("||", results);

            return RedirectToAction(nameof(BulkInitiate));
        }

        // ======================================================================
        // NEW — POST: Appraisals/NotifySupervisorForMissingKpis
        // Called via AJAX from the BulkInitiate table row for an employee with no
        // Posting-Specific KPIs. Creates an in-app Notification for that employee's
        // supervisor, linking straight to the posting's KPI section.
        // ======================================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifySupervisorForMissingKpis(int employeeId)
        {
            if (!User.IsInRole("HR") && !User.IsInRole("Admin"))
                return Forbid();

            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null)
                return Json(new { success = false, message = "Employee not found." });

            if (employee.SupervisorId == null)
                return Json(new { success = false, message = $"{employee.FullName} has no supervisor assigned." });

            var currentPosting = await _context.Postings
                .Where(p => p.EmployeeId == employeeId && (p.ToDate == null || p.ToDate >= DateTime.Today))
                .OrderByDescending(p => p.FromDate)
                .FirstOrDefaultAsync();

            if (currentPosting == null)
                return Json(new { success = false, message = $"{employee.FullName} has no current posting." });

            var supervisor = await _context.Employees.FindAsync(employee.SupervisorId.Value);

            var notification = new Notification
            {
                RecipientEmployeeId = employee.SupervisorId.Value,
                Message = $"{employee.FullName} needs Posting-Specific KPIs assigned before their appraisal can be initiated.",
                Url = Url.Action("Details", "Postings", new { id = currentPosting.Id }) + "#specific-kpis",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Notified {supervisor?.FullName ?? "supervisor"}." });
        }

        // ======================================================================
        // Shared logic for initiating one appraisal — used by BulkInitiate. NOTE: this duplicates
        // the KPI-snapshot logic currently inline in the single-employee Create(POST) action above.
        // Recommend refactoring Create(POST) to call this same method once you're ready to touch it,
        // so there's only one place that defines "what happens when an appraisal is created."
        // ======================================================================
        private async Task<(bool success, string message)> InitiateAppraisalForEmployeeAsync(
            int employeeId, DateTime fromDate, DateTime toDate, APRsystem.Models.AppraisalType type)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null)
                return (false, $"SKIP: Employee #{employeeId} not found.");

            var currentPosting = await _context.Postings
                .Include(p => p.Contract)
                .Where(p => p.EmployeeId == employeeId && (p.ToDate == null || p.ToDate >= DateTime.Today))
                .OrderByDescending(p => p.FromDate)
                .FirstOrDefaultAsync();

            if (currentPosting == null)
                return (false, $"SKIP: {employee.FullName} — no current posting.");

            var hasSpecificKpis = await _context.PostingKPIs
                .AnyAsync(pk => pk.PostingId == currentPosting.Id && pk.IsActive);

            if (!hasSpecificKpis)
                return (false, $"SKIP: {employee.FullName} — no Posting-Specific KPIs defined.");

            if (currentPosting.Contract != null &&
                (currentPosting.Contract.StartDate > toDate ||
                 (currentPosting.Contract.EndDate != null && currentPosting.Contract.EndDate < fromDate)))
            {
                return (false, $"SKIP: {employee.FullName} — contract does not overlap the appraisal period.");
            }
            if (await HasActiveAppraisalAsync(employeeId))
            {
                return (false,
                    $"SKIP: {employee.FullName} already has an appraisal in progress. Complete or close the current appraisal before initiating a new one.");
            }
            var alreadyExists = await _context.Appraisals.AnyAsync(a =>
                a.PostingId == currentPosting.Id && a.FromDate == fromDate && a.ToDate == toDate);

            if (alreadyExists)
                return (false, $"SKIP: {employee.FullName} — an appraisal for this exact period already exists.");

            Employee? supervisor = employee.SupervisorId != null
                ? await _context.Employees.FindAsync(employee.SupervisorId)
                : null;

            var draftStatusId = await GetStatusIdAsync("Draft");

            var appraisal = new Appraisal
            {
                EmployeeId = employeeId,
                PostingId = currentPosting.Id,
                SupervisorId = employee.SupervisorId ?? 0,
                ReviewerId = supervisor?.SupervisorId,
                FromDate = fromDate,
                ToDate = toDate,
                Type = type,
                StatusId = draftStatusId
            };

            _context.Appraisals.Add(appraisal);
            await _context.SaveChangesAsync();

            var anticipated = await _workflow.GetSoleNextStateAsync(WorkflowEntity, draftStatusId);
            await LogHistoryAsync(appraisal.Id, "Appraisal initiated (bulk, by HR)", "HR", draftStatusId, anticipated);


            var generalKpis = await _context.KPIs.Where(k => k.IsGeneral).ToListAsync();
            foreach (var kpi in generalKpis)
            {
                _context.AppraisalKPIs.Add(new AppraisalKPI
                {
                    AppraisalId = appraisal.Id,
                    Section = KPISection.General,
                    Title = kpi.Title,
                    Description = kpi.Description,
                    Weight = kpi.Weight,
                    SelfComment = string.Empty
                });
            }

            var specificKpis = await _context.PostingKPIs
                .Where(pk => pk.PostingId == currentPosting.Id && pk.IsActive)
                .ToListAsync();

            foreach (var kpi in specificKpis)
            {
                _context.AppraisalKPIs.Add(new AppraisalKPI
                {
                    AppraisalId = appraisal.Id,
                    Section = KPISection.Specific,
                    Title = kpi.Title,
                    Description = kpi.Description,
                    Weight = kpi.Weight,
                    SelfComment = string.Empty
                });
            }

            await _context.SaveChangesAsync();

            return (true, $"OK: {employee.FullName} — appraisal initiated.");
        }
        // GET: Appraisals/Create?employeeId=5
        [HttpGet]
        public async Task<IActionResult> Create(int employeeId)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound();

            var currentPosting = await _context.Postings
                .Where(p => p.EmployeeId == employeeId && (p.ToDate == null || p.ToDate >= DateTime.Today))
                .OrderByDescending(p => p.FromDate)
                .FirstOrDefaultAsync();

            if (currentPosting == null)
            {
                ModelState.AddModelError("", "This employee has no current posting to appraise.");
            }
            else
            {
                var hasSpecificKpis = await _context.PostingKPIs
                    .AnyAsync(pk => pk.PostingId == currentPosting.Id && pk.IsActive);

                if (!hasSpecificKpis)
                {
                    TempData["Warning"] = "This posting has no Posting-Specific KPIs defined yet. Add at least one before initiating an appraisal.";
                    return Redirect(Url.Action("Details", "Postings", new { id = currentPosting.Id }) + "#specific-kpis");
                }
            }
            if (await HasActiveAppraisalAsync(employeeId))
            {
                var activeAppraisal = await _context.Appraisals
                    .Where(a => a.EmployeeId == employeeId)
                    .OrderByDescending(a => a.FromDate)
                    .FirstOrDefaultAsync(); // status filtering already guaranteed by the check above

                TempData["Warning"] = "This employee already has an appraisal in progress. It must be approved (or otherwise closed out) before a new one can be started.";
                return RedirectToAction(nameof(Details), new { id = activeAppraisal!.Id });
            }
            var model = new CreateAppraisalViewModel
            {
                EmployeeId = employeeId,
                PostingId = currentPosting?.Id ?? 0,
                SupervisorId = employee.SupervisorId ?? 0,
                FromDate = DateTime.Today,
                ToDate = DateTime.Today.AddMonths(12)
            };

            ViewBag.EmployeeName = employee.FullName;

            return View(model);
        }

        // POST: Appraisals/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAppraisalViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (await HasActiveAppraisalAsync(model.EmployeeId))
            {
                ModelState.AddModelError("", "This employee already has an appraisal in progress.");
                return View(model);
            }
            // Validate appraisal dates against the posting's contract end date
            var posting = await _context.Postings
                .Include(p => p.Contract)
                .FirstOrDefaultAsync(p => p.Id == model.PostingId);

            if (posting == null || posting.Contract == null)
            {
                ModelState.AddModelError("", "Could not find a contract linked to this posting.");
                return View(model);
            }

            if (posting.Contract.StartDate > model.ToDate ||
                (posting.Contract.EndDate != null && posting.Contract.EndDate < model.FromDate))
            {
                ModelState.AddModelError(nameof(model.ToDate),
                    "The contract does not overlap the appraisal period.");
                return View(model);
            }

            var supervisor = await _context.Employees.FindAsync(model.SupervisorId);

            // Creation isn't a workflow transition (no prior state), so the initial status still comes
            // straight from the Lookup table rather than the Workflows table.
            var draftStatusId = await GetStatusIdAsync("Draft");

            var appraisal = new Appraisal
            {
                EmployeeId = model.EmployeeId,
                PostingId = model.PostingId,
                SupervisorId = model.SupervisorId,
                ReviewerId = supervisor?.SupervisorId,
                FromDate = model.FromDate,
                ToDate = model.ToDate,
                Type = model.Type,
                StatusId = draftStatusId
            };

            _context.Appraisals.Add(appraisal);
            await _context.SaveChangesAsync();

            var anticipatedAfterCreate = await _workflow.GetSoleNextStateAsync(WorkflowEntity, draftStatusId);
            await LogHistoryAsync(appraisal.Id, "Appraisal initiated", "Supervisor",
                draftStatusId, anticipatedAfterCreate);

            // Snapshot General KPIs (same for every employee, always active)
            var generalKpis = await _context.KPIs
                .Where(k => k.IsGeneral)
                .ToListAsync();

            foreach (var kpi in generalKpis)
            {
                {
                    _context.AppraisalKPIs.Add(new AppraisalKPI
                    {
                        AppraisalId = appraisal.Id,
                        Section = KPISection.General,
                        Title = kpi.Title,
                        Description = kpi.Description,
                        Weight = kpi.Weight,
                        SelfRating = 0,
                        SelfScore = 0,
                        Rating = 0,
                        Score = 0,
                        SelfComment = string.Empty
                    });
                }
            }

            // Snapshot Posting-Specific KPIs (only active ones, for this posting)
            var specificKpis = await _context.PostingKPIs
                .Where(pk => pk.PostingId == model.PostingId && pk.IsActive)
                .ToListAsync();

            foreach (var kpi in specificKpis)
            {
                { 
                    _context.AppraisalKPIs.Add(new AppraisalKPI
                    {
                        AppraisalId = appraisal.Id,
                        Section = KPISection.Specific,
                        Title = kpi.Title,
                        Description = kpi.Description,
                        Weight = kpi.Weight,
                        SelfRating = 0,
                        SelfScore = 0,
                        Rating = 0,
                        Score = 0,
                        SelfComment = string.Empty
                    });
                }
            }

            await _context.SaveChangesAsync();

            // Land the supervisor directly on the appraisal they just created — Details already
            // shows the "Allow Self-Assessment" vs "Score Directly" decision for a Draft appraisal
            // where they're the supervisor, so there's no reason to detour through the list first.
            TempData["Success"] = "Appraisal initiated. Choose whether to allow self-assessment or score it directly.";
            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }

        // POST: Appraisals/AllowSelfAssessment
        // Maps to Workflows row Id=1: CurrentStateId=10 (Draft), Action="Submit Self-Assessment",
        // NextStateId=11 (SelfAssessment). CrudPermission on that row should be 'Supervisor' —
        // it's the supervisor turning self-assessment ON for the employee, not the employee submitting.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AllowSelfAssessment(int id)
        {
            var appraisal = await _context.Appraisals.FindAsync(id);
            if (appraisal == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            if (currentEmployee?.Id != appraisal.SupervisorId)
                return Forbid();

            Workflow wf;
            try
            {
                wf = await _workflow.GetTransitionAsync(WorkflowEntity, appraisal.StatusId, "Submit Self-Assessment");
            }
            catch (WorkflowValidationException ex)
            {
                return BadRequest(ex.Message);
            }

            appraisal.SelfAssessmentEnabled = true;
            appraisal.StatusId = wf.NextStateId;

            var anticipatedAfterSelfAssessment = await _workflow.GetSoleNextStateAsync(WorkflowEntity, wf.NextStateId);
            await LogHistoryAsync(appraisal.Id, "Self-assessment enabled for employee", "Supervisor",
                wf.NextStateId, anticipatedAfterSelfAssessment);
            NotifyEmployee(
    appraisal.EmployeeId,
    "Your supervisor has enabled self-assessment for your appraisal. Please complete it.",
    Url.Action("Score", "Appraisals", new { id = appraisal.Id }));


            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }

        // GET: Appraisals/Score/5
        [HttpGet]
        public async Task<IActionResult> Score(int id)
        {
            var appraisal = await _context.Appraisals
                .Include(a => a.Employee)
                .Include(a => a.AppraisalKPIs)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appraisal == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            bool isSupervisor = currentEmployee?.Id == appraisal.SupervisorId;
            bool isEmployee = currentEmployee?.Id == appraisal.EmployeeId;

            var selfAssessmentStatusId = await GetStatusIdAsync("SelfAssessment");     // 11
            var supervisorReviewStatusId = await GetStatusIdAsync("SupervisorReview"); // 12
            var supervisorAssessmentStatusId = await GetStatusIdAsync("SupervisorAssessment"); // 18
            var employeeCommentStatusId = await GetStatusIdAsync("EmployeeComment");   // between 18 and SupervisorRank
            var supervisorRankStatusId = await GetStatusIdAsync("SupervisorRank");     // between EmployeeComment and HR

            bool canEditSelf = appraisal.StatusId == selfAssessmentStatusId && isEmployee;
            bool canEditSupervisor =
    appraisal.StatusId == supervisorAssessmentStatusId && isSupervisor;
            bool canView = appraisal.StatusId == supervisorReviewStatusId && isSupervisor; // read-only: approve/revert decision
            bool canEditEmployeeComment = appraisal.StatusId == employeeCommentStatusId && isEmployee;
            bool canEditSupervisorRank = appraisal.StatusId == supervisorRankStatusId && isSupervisor;

            if (!canEditSelf && !canEditSupervisor && !canView && !canEditEmployeeComment && !canEditSupervisorRank)
                return Forbid();

            var vm = new AppraisalScoreViewModel
            {
                AppraisalId = appraisal.Id,
                EmployeeName = appraisal.Employee.FullName,
                FromDate = appraisal.FromDate,
                EmployeeId = appraisal.EmployeeId,
                ToDate = appraisal.ToDate,
                GeneralKPIs = appraisal.AppraisalKPIs.Where(k => k.Section == KPISection.General).ToList(),
                SpecificKPIs = appraisal.AppraisalKPIs.Where(k => k.Section == KPISection.Specific).ToList(),

                // Always send both, regardless of who's viewing — the view decides what's read-only vs editable
                SelfGeneralComment = appraisal.SelfGeneralComment,
                SelfSpecificComment = appraisal.SelfSpecificComment,

                SupervisorGeneralComment = appraisal.SupervisorGeneralComment,
                SupervisorSpecificComment = appraisal.SupervisorSpecificComment,

                // Legacy fields, kept only for whichever field the CURRENT user is actively editing
                GeneralComment = isEmployee ? appraisal.SelfGeneralComment : appraisal.SupervisorGeneralComment,
                SpecificComment = isEmployee ? appraisal.SelfSpecificComment : appraisal.SupervisorSpecificComment,

                EmployeeFinalComment = appraisal.EmployeeFinalComment,

                SupervisorFinalRank = appraisal.SupervisorFinalRank,
                SupervisorRankComment = appraisal.SupervisorRankComment,

                SupervisorId = appraisal.SupervisorId,
                EditableField = canEditSelf ? "Self"
                    : canEditSupervisor ? "Supervisor"
                    : canEditEmployeeComment ? "EmployeeComment"
                    : canEditSupervisorRank ? "SupervisorRank"
                    : "None"
            };

            return View(vm);
        }

        // POST: Appraisals/ApproveSelfAssessment
        // Maps to Workflows row Id=3: CurrentStateId=12 (SupervisorReview), Action="Approve Self-Assessment",
        // NextStateId=18 (SupervisorRating), CrudPermission=Supervisor, IsCommentMandatory=0.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveSelfAssessment(int id, string? comments)
        {
            var appraisal = await _context.Appraisals.FindAsync(id);
            if (appraisal == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            if (currentEmployee?.Id != appraisal.SupervisorId)
                return Forbid();

            Workflow wf;
            try
            {
                wf = await _workflow.GetTransitionAsync(WorkflowEntity, appraisal.StatusId, "Approve Self-Assessment");
                _workflow.EnsureCommentProvided(wf, comments); // no-op today: IsCommentMandatory=0 on this row
            }
            catch (WorkflowValidationException ex)
            {
                return BadRequest(ex.Message);
            }

            appraisal.StatusId = wf.NextStateId;

            var anticipatedAfterApproveSelf = await _workflow.GetSoleNextStateAsync(WorkflowEntity, wf.NextStateId);
            await LogHistoryAsync(appraisal.Id,
                string.IsNullOrWhiteSpace(comments) ? "Self-assessment approved by supervisor" : $"Self-assessment approved by supervisor: {comments}",
                "Supervisor", wf.NextStateId, anticipatedAfterApproveSelf);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Score), new { id = appraisal.Id });
        }

        // POST: Appraisals/SendBackToEmployee
        // Maps to Workflows row Id=4: CurrentStateId=12 (SupervisorReview), Action="Revert Self-Assessment",
        // NextStateId=11 (SelfAssessment), CrudPermission=Supervisor, IsCommentMandatory=1.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBackToEmployee(int id, string comments)
        {
            var appraisal = await _context.Appraisals.FindAsync(id);
            if (appraisal == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            // Authorization stays relationship-based (this appraisal's supervisor), not a blanket role check.
            bool isSupervisor = currentEmployee?.Id == appraisal.SupervisorId;
            if (!isSupervisor)
                return Forbid();

            if (!appraisal.SelfAssessmentEnabled)
                return BadRequest("This appraisal was not set up for self-assessment.");

            Workflow wf;
            try
            {
                wf = await _workflow.GetTransitionAsync(WorkflowEntity, appraisal.StatusId, "Revert Self-Assessment");
                _workflow.EnsureCommentProvided(wf, comments);
            }
            catch (WorkflowValidationException ex)
            {
                return BadRequest(ex.Message);
            }

            appraisal.StatusId = wf.NextStateId;

            var anticipatedAfterSendBack = await _workflow.GetSoleNextStateAsync(WorkflowEntity, wf.NextStateId);
            await LogHistoryAsync(appraisal.Id, $"Sent back to employee for re-assessment: {comments}", "Supervisor",
                wf.NextStateId, anticipatedAfterSendBack);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }

        // POST: Appraisals/HRReview
        // Maps to Workflows row Id=5: CurrentStateId=13 (HR), Action="Complete HR Review",
        // NextStateId=17 (FinalReview), CrudPermission=HR, IsCommentMandatory=1.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HRReview(int id, string? hrRemarks)
        {
            var appraisal = await _context.Appraisals
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (appraisal == null) return NotFound();

            if (!User.IsInRole("HR"))
                return Forbid();

            Workflow wf;
            try
            {
                wf = await _workflow.GetTransitionAsync(WorkflowEntity, appraisal.StatusId, "Complete HR Review");
                _workflow.EnsureCommentProvided(wf, hrRemarks);
            }
            catch (WorkflowValidationException ex)
            {
                return BadRequest(ex.Message);
            }

            appraisal.StatusId = wf.NextStateId;
            appraisal.HRRemarks = hrRemarks;

            var anticipatedAfterHRReview = await _workflow.GetSoleNextStateAsync(WorkflowEntity, wf.NextStateId);
            await LogHistoryAsync(appraisal.Id, "HR remarks added", "HR",
                wf.NextStateId, anticipatedAfterHRReview);

            // This was missing — every other transition notifies whoever acts next
            // (AllowSelfAssessment -> employee, self-submit -> supervisor, supervisor-submit -> HR),
            // but the final reviewer was never told their turn had come up.
            if (appraisal.ReviewerId.HasValue)
            {
                NotifyEmployee(
                    appraisal.ReviewerId.Value,
                    $"{appraisal.Employee?.FullName ?? "An employee"}'s appraisal has been reviewed by HR and is ready for your final approval.",
                    Url.Action("Details", "Appraisals", new { id = appraisal.Id }));
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }

        // POST: Appraisals/Score
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Score(AppraisalScoreViewModel model)
        {
            var appraisal = await _context.Appraisals
                .Include(a => a.AppraisalKPIs)
                .Include(a => a.Employee)
                .FirstOrDefaultAsync(a => a.Id == model.AppraisalId);

            if (appraisal == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            bool isEmployee = currentEmployee?.Id == appraisal.EmployeeId;
            bool isSupervisor = currentEmployee?.Id == appraisal.SupervisorId;

            var selfAssessmentStatusId = await GetStatusIdAsync("SelfAssessment");     // 11
            var supervisorAssessmentStatusId =
    await GetStatusIdAsync("SupervisorAssessment"); ; // 18
            var employeeCommentStatusId = await GetStatusIdAsync("EmployeeComment");
            var supervisorRankStatusId = await GetStatusIdAsync("SupervisorRank");

            Workflow wf;
            try
            {
                if (appraisal.StatusId == selfAssessmentStatusId && isEmployee)
                {
                    // Employee submitting their self-assessment (11 -> 12). Only SelfRating/SelfScore are touched.
                    foreach (var posted in model.GeneralKPIs.Concat(model.SpecificKPIs))
                    {
                        var kpi = appraisal.AppraisalKPIs.FirstOrDefault(k => k.Id == posted.Id);
                        if (kpi == null) continue;

                        kpi.SelfRating = posted.SelfRating;
                        kpi.SelfScore = kpi.Weight * kpi.SelfRating;
                        kpi.SelfComment = posted.SelfComment;
                    }

                    appraisal.SelfGeneralComment = model.SelfGeneralComment;
                    appraisal.SelfSpecificComment = model.SelfSpecificComment;

                    wf = await _workflow.GetTransitionAsync(WorkflowEntity, appraisal.StatusId, "Submit for Supervisor Review");
                    _workflow.EnsureCommentProvided(wf, model.SelfGeneralComment, model.SelfSpecificComment);

                    appraisal.StatusId = wf.NextStateId;

                    var anticipatedAfterSelfSubmit = await _workflow.GetSoleNextStateAsync(WorkflowEntity, wf.NextStateId);
                    await LogHistoryAsync(appraisal.Id, "Self-assessment submitted by employee", "Employee",
    wf.NextStateId, anticipatedAfterSelfSubmit);

                    NotifyEmployee(
                        appraisal.SupervisorId,
                        $"{currentEmployee?.FullName ?? "An employee"} has completed their self-assessment and it's ready for your review.",
                        Url.Action("Score", "Appraisals", new { id = appraisal.Id }));
                }
                else if (appraisal.StatusId == supervisorAssessmentStatusId && isSupervisor)
                {
                    // Supervisor rating and submitting (18 -> EmployeeComment). Only Rating/Score (official) are touched —
                    // the employee's SelfRating/SelfScore are left exactly as they submitted them.
                    foreach (var posted in model.GeneralKPIs.Concat(model.SpecificKPIs))
                    {
                        var kpi = appraisal.AppraisalKPIs.FirstOrDefault(k => k.Id == posted.Id);
                        if (kpi == null) continue;

                        kpi.Rating = posted.Rating;
                        kpi.Score = kpi.Weight * kpi.Rating;
                    }

                    appraisal.SupervisorGeneralComment = model.SupervisorGeneralComment;
                    appraisal.SupervisorSpecificComment = model.SupervisorSpecificComment;
                    appraisal.RecommendationText = model.RecommendationText;
                    appraisal.RecommendedRank = model.RecommendedRank;

                    // Totals/percentage/rank are calculated from the OFFICIAL (supervisor) Rating/Score only —
                    // this is what the employee, HR, and the final reviewer act on.
                    var generalKpis = appraisal.AppraisalKPIs.Where(k => k.Section == KPISection.General).ToList();
                    var specificKpis = appraisal.AppraisalKPIs.Where(k => k.Section == KPISection.Specific).ToList();

                    appraisal.GeneralTotalScore = generalKpis.Sum(k => k.Score);
                    appraisal.GeneralMaxScore = generalKpis.Sum(k => k.Weight * 4);

                    appraisal.SpecificTotalScore = specificKpis.Sum(k => k.Score);
                    appraisal.SpecificMaxScore = specificKpis.Sum(k => k.Weight * 4);

                    appraisal.GrandTotalScore = appraisal.GeneralTotalScore + appraisal.SpecificTotalScore;
                    appraisal.GrandMaxScore = appraisal.GeneralMaxScore + appraisal.SpecificMaxScore;

                    appraisal.Percentage = appraisal.GrandMaxScore > 0
                        ? Math.Round((appraisal.GrandTotalScore / appraisal.GrandMaxScore) * 100, 2)
                        : 0;

                    appraisal.RankingBand = appraisal.Percentage switch
                    {
                        >= 90 => "Outstanding",
                        >= 75 => "Above Expectations",
                        >= 50 => "Meets Expectations",
                        >= 30 => "Below Expectations",
                        _ => "Needs Improvement"
                    };

                    // NOTE: action name changed from "Submit to HR" to "Submit for Employee Comment" —
                    // this now hands off to the employee first instead of going straight to HR.
                    // See the Workflows-table update in the SQL notes for the matching DB change.
                    wf = await _workflow.GetTransitionAsync(WorkflowEntity, appraisal.StatusId, "Submit for Employee Comment");
                    _workflow.EnsureCommentProvided(wf, model.SupervisorGeneralComment, model.SupervisorSpecificComment);

                    appraisal.StatusId = wf.NextStateId;

                    var anticipatedAfterSupervisorSubmit = await _workflow.GetSoleNextStateAsync(WorkflowEntity, wf.NextStateId);
                    await LogHistoryAsync(appraisal.Id, "KPIs rated by supervisor; awaiting employee comment", "Supervisor",
                        wf.NextStateId, anticipatedAfterSupervisorSubmit);

                    NotifyEmployee(
                        appraisal.EmployeeId,
                        "Your supervisor has completed your rating. Please review it and add your comment.",
                        Url.Action("Score", "Appraisals", new { id = appraisal.Id }));
                }
                else if (appraisal.StatusId == employeeCommentStatusId && isEmployee)
                {
                    // Employee has reviewed the supervisor's rating and is adding their own
                    // closing comment. This now hands off to the supervisor's Final Rank stage,
                    // not straight to HR. No ratings change here.
                    appraisal.EmployeeFinalComment = model.EmployeeFinalComment;

                    wf = await _workflow.GetTransitionAsync(WorkflowEntity, appraisal.StatusId, "Submit Employee Comment");
                    // Comment here is optional (employee is acknowledging, not required to write anything),
                    // so no EnsureCommentProvided call — flip this on if you want it mandatory.

                    appraisal.StatusId = wf.NextStateId;

                    var anticipatedAfterEmployeeComment = await _workflow.GetSoleNextStateAsync(WorkflowEntity, wf.NextStateId);
                    await LogHistoryAsync(appraisal.Id, "Employee reviewed supervisor rating and added a comment", "Employee",
                        wf.NextStateId, anticipatedAfterEmployeeComment);

                    NotifyEmployee(
                        appraisal.SupervisorId,
                        $"{appraisal.Employee?.FullName ?? "An employee"} has added their comment. Please give your final rank.",
                        Url.Action("Score", "Appraisals", new { id = appraisal.Id }));
                }
                else if (appraisal.StatusId == supervisorRankStatusId && isSupervisor)
                {
                    // Supervisor gives their final rank + comment, after seeing the employee's
                    // comment. Separate from RecommendedRank/RecommendationText above. Now
                    // visible read-only to the employee once the appraisal is fully closed out.
                    appraisal.SupervisorFinalRank = model.SupervisorFinalRank;
                    appraisal.SupervisorRankComment = model.SupervisorRankComment;

                    wf = await _workflow.GetTransitionAsync(WorkflowEntity, appraisal.StatusId, "Submit Final Rank");
                    _workflow.EnsureCommentProvided(wf, model.SupervisorRankComment);

                    appraisal.StatusId = wf.NextStateId;

                    var anticipatedAfterSupervisorRank = await _workflow.GetSoleNextStateAsync(WorkflowEntity, wf.NextStateId);
                    await LogHistoryAsync(appraisal.Id, "Supervisor gave final rank", "Supervisor",
                        wf.NextStateId, anticipatedAfterSupervisorRank);

                    await NotifyRoleAsync(
                        "HR",
                        $"{appraisal.Employee?.FullName ?? "An employee"}'s appraisal has been submitted for HR review.",
                        Url.Action("Details", "Appraisals", new { id = appraisal.Id }));
                }
                else
                {
                    return Forbid();
                }
            }
            catch (WorkflowValidationException ex)
            {
                return BadRequest(ex.Message);
            }
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }
        // POST: Appraisals/SaveScoreDraft
        // Lets the employee (self-assessment or final comment) or supervisor (scoring) save
        // whatever they've filled in so far WITHOUT submitting: no workflow transition, no
        // mandatory-comment check, no history entry. The appraisal stays on the same status,
        // so they can come back later via Score(GET) and pick up exactly where they left off —
        // the same fields that Score(POST) writes are updated here, just without moving the
        // workflow forward.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveScoreDraft(AppraisalScoreViewModel model)
        {
            var appraisal = await _context.Appraisals
                .Include(a => a.AppraisalKPIs)
                .FirstOrDefaultAsync(a => a.Id == model.AppraisalId);

            if (appraisal == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            bool isEmployee = currentEmployee?.Id == appraisal.EmployeeId;
            bool isSupervisor = currentEmployee?.Id == appraisal.SupervisorId;

            var selfAssessmentStatusId = await GetStatusIdAsync("SelfAssessment");
            var supervisorAssessmentStatusId = await GetStatusIdAsync("SupervisorAssessment");
            var employeeCommentStatusId = await GetStatusIdAsync("EmployeeComment");
            var supervisorRankStatusId = await GetStatusIdAsync("SupervisorRank");

            if (appraisal.StatusId == selfAssessmentStatusId && isEmployee)
            {
                foreach (var posted in model.GeneralKPIs.Concat(model.SpecificKPIs))
                {
                    var kpi = appraisal.AppraisalKPIs.FirstOrDefault(k => k.Id == posted.Id);
                    if (kpi == null) continue;

                    kpi.SelfRating = posted.SelfRating;
                    kpi.SelfScore = kpi.Weight * kpi.SelfRating;
                }

                appraisal.SelfGeneralComment = model.GeneralComment;
                appraisal.SelfSpecificComment = model.SpecificComment;
            }
            else if (appraisal.StatusId == supervisorAssessmentStatusId && isSupervisor)
            {
                foreach (var posted in model.GeneralKPIs.Concat(model.SpecificKPIs))
                {
                    var kpi = appraisal.AppraisalKPIs.FirstOrDefault(k => k.Id == posted.Id);
                    if (kpi == null) continue;

                    kpi.Rating = posted.Rating;
                    kpi.Score = kpi.Weight * kpi.Rating;
                }

                appraisal.SupervisorGeneralComment = model.GeneralComment;
                appraisal.SupervisorSpecificComment = model.SpecificComment;
                appraisal.RecommendationText = model.RecommendationText;
                appraisal.RecommendedRank = model.RecommendedRank;
            }
            else if (appraisal.StatusId == employeeCommentStatusId && isEmployee)
            {
                appraisal.EmployeeFinalComment = model.EmployeeFinalComment;
            }
            else if (appraisal.StatusId == supervisorRankStatusId && isSupervisor)
            {
                appraisal.SupervisorFinalRank = model.SupervisorFinalRank;
                appraisal.SupervisorRankComment = model.SupervisorRankComment;
            }
            else
            {
                return Forbid();
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Progress saved — you can come back and finish this later.";
            return RedirectToAction(nameof(Score), new { id = appraisal.Id });
        }

        // POST: Appraisals/SaveReviewDraft
        // Same idea as SaveScoreDraft, for the final reviewer's stage: saves the reviewer's
        // remarks/rank/action-required without approving or rejecting, so nothing about the
        // workflow status changes. The appraisal stays at the final-review stage and the
        // reviewer can reopen the Approve/Reject modal later to find their draft still there.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveReviewDraft(int id, string? reviewerComments, string? finalRank, string? actionRequired)
        {
            var appraisal = await _context.Appraisals.FindAsync(id);
            if (appraisal == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            if (appraisal.ReviewerId != currentEmployee?.Id)
                return Forbid();

            var hrReviewedStatusId = await GetStatusIdAsync("HRReviewed");
            if (appraisal.StatusId != hrReviewedStatusId)
                return BadRequest("This appraisal isn't currently at the final review stage.");

            appraisal.ReviewerComments = reviewerComments;
            appraisal.FinalRank = finalRank;
            appraisal.ActionRequired = actionRequired;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Draft saved — you can come back and finish the review later.";
            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }

        // GET: Appraisals/Details/5
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var appraisal = await _context.Appraisals
                .Include(a => a.Employee)
                .Include(a => a.Supervisor)
                .Include(a => a.Reviewer)
                .Include(a => a.AppraisalKPIs)
                .Include(a => a.Status)
                .Include(a => a.History.OrderBy(h => h.Timestamp))
                    .ThenInclude(h => h.Stage)
                .Include(a => a.History)
                    .ThenInclude(h => h.NextStage)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appraisal == null) return NotFound();

            return View(appraisal);
        }

        // GET: Appraisals/History/5
        [HttpGet]
        public async Task<IActionResult> History(int id)
        {
            var appraisal = await _context.Appraisals
                .Include(a => a.Employee)
                .Include(a => a.Status)
                .Include(a => a.History.OrderBy(h => h.Timestamp))
                    .ThenInclude(h => h.Stage)
                .Include(a => a.History)
                    .ThenInclude(h => h.NextStage)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appraisal == null) return NotFound();

            return View(appraisal);
        }

        // POST: Appraisals/Approve
        // Maps to Workflows row Id=6: CurrentStateId=17 (FinalReview), Action="Final Approval",
        // NextStateId=14 (Approved), CrudPermission=HR,Admin (kept ReviewerId-specific auth per your decision).
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string? reviewerComments, string? finalRank, string? actionRequired)
        {
            var appraisal = await _context.Appraisals.FindAsync(id);
            if (appraisal == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            if (appraisal.ReviewerId != currentEmployee?.Id)
                return Forbid();

            Workflow wf;
            try
            {
                wf = await _workflow.GetTransitionAsync(WorkflowEntity, appraisal.StatusId, "Final Approval");
                _workflow.EnsureCommentProvided(wf, reviewerComments);
            }
            catch (WorkflowValidationException ex)
            {
                return BadRequest(ex.Message);
            }

            appraisal.StatusId = wf.NextStateId;
            appraisal.ReviewedOn = DateTime.Now;
            appraisal.ReviewerComments = reviewerComments;
            appraisal.FinalRank = finalRank;
            appraisal.ActionRequired = actionRequired;

            var anticipatedAfterApprove = await _workflow.GetSoleNextStateAsync(WorkflowEntity, wf.NextStateId);
            await LogHistoryAsync(appraisal.Id, "Appraisal approved by reviewer", "Reviewer",
                wf.NextStateId, anticipatedAfterApprove);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }

        // POST: Appraisals/Reject
        // Maps to Workflows row Id=7: CurrentStateId=17 (FinalReview), Action="Send Back for Re-assessment",
        // NextStateId=11 (SelfAssessment) — requires:
        //   UPDATE Workflows SET NextStateId = 11 WHERE Id = 7;
        // Same relationship-based (ReviewerId) authorization as Approve, per your decision.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reviewerComments, string? finalRank, string? actionRequired)
        {
            var appraisal = await _context.Appraisals.FindAsync(id);
            if (appraisal == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            if (appraisal.ReviewerId != currentEmployee?.Id)
                return Forbid();

            Workflow wf;
            try
            {
                wf = await _workflow.GetTransitionAsync(WorkflowEntity, appraisal.StatusId, "Send Back for Re-assessment");
                _workflow.EnsureCommentProvided(wf, reviewerComments);
            }
            catch (WorkflowValidationException ex)
            {
                return BadRequest(ex.Message);
            }

            appraisal.StatusId = wf.NextStateId;
            appraisal.ReviewerComments = reviewerComments;
            appraisal.ReviewedOn = DateTime.Now;
            appraisal.FinalRank = finalRank;
            appraisal.ActionRequired = actionRequired;

            // Landing back on SelfAssessment(11) means the employee needs access again — this flag
            // is what Score(GET)/Score(POST) check for the employee's editing path.
            appraisal.SelfAssessmentEnabled = true;

            var anticipatedAfterReject = await _workflow.GetSoleNextStateAsync(WorkflowEntity, wf.NextStateId);
            await LogHistoryAsync(appraisal.Id, $"Appraisal rejected by reviewer, sent back for re-assessment: {reviewerComments}", "Reviewer",
                wf.NextStateId, anticipatedAfterReject);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }

        // GET: Appraisals
        // GET: Appraisals?scope=own|team|all
        public async Task<IActionResult> Index(string scope = "own")
        {
            var appraisalsQuery = _context.Appraisals
                .Include(a => a.Employee)
                .Include(a => a.Supervisor)
                .Include(a => a.Reviewer)
                .Include(a => a.Status)
                .AsQueryable();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);
            var currentEmployeeId = currentEmployee?.Id;

            bool isAdminOrHR = User.IsInRole("Admin") || User.IsInRole("HR");

            // Only Admin/HR can request "all" — anyone else asking for it silently falls back to "own"
            // rather than erroring, since this is just a filter, not a security boundary (Index already
            // scopes results per-role below regardless of what's in the query string).
            if (scope == "all" && !isAdminOrHR)
            {
                scope = "own";
            }

            switch (scope)
            {
                case "all":
                    // Admin/HR only, reached above — no filter, see everyone's appraisals
                    break;

                case "team":
                    // Appraisals where the current user is acting as Supervisor or Reviewer —
                    // i.e. appraisals belonging to people who report to them, not their own.
                    appraisalsQuery = appraisalsQuery.Where(a =>
                        a.SupervisorId == currentEmployeeId ||
                        a.ReviewerId == currentEmployeeId);
                    break;

                case "own":
                default:
                    appraisalsQuery = appraisalsQuery.Where(a =>
                        a.Employee.ApplicationUserId == currentUserId);
                    break;
            }

            ViewBag.Scope = scope;
            ViewBag.IsAdminOrHR = isAdminOrHR;

            // Only show the "Team" tab if this person actually has anyone reporting to them —
            // no point offering a filter that's always empty.
            ViewBag.HasTeam = currentEmployeeId != null && await _context.Employees
                .AnyAsync(e => e.SupervisorId == currentEmployeeId);

            return View(await appraisalsQuery.OrderByDescending(a => a.FromDate).ToListAsync());
        }


        private async Task LogHistoryAsync(int appraisalId, string comment, string role, int stageId, int? nextStageId)
        {
            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            _context.AppraisalHistories.Add(new AppraisalHistory
            {
                AppraisalId = appraisalId,
                Comments = comment,
                ActionByRole = role,
                ActionByName = currentEmployee?.FullName ?? "System",
                StageId = stageId,
                NextStageId = nextStageId
            });
        }
    }
}
using APRsystem.Data;
using APRsystem.Models;
using APRsystem.Models.Identity;
using APRsystem.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APRsystem.Controllers
{
    public class AppraisalsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        // Category used for all appraisal-status rows in the Lookup table
        private const string StatusCategory = "AppraisalStatus";

        public AppraisalsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // Helper: get the Lookup.Id for a given status value (e.g. "Draft", "Approved")
        private async Task<int> GetStatusIdAsync(string value)
        {
            var lookup = await _context.Lookups
                .FirstOrDefaultAsync(l => l.Category == StatusCategory && l.Value == value && l.IsActive);

            if (lookup == null)
                throw new InvalidOperationException($"Lookup value '{value}' not found for category '{StatusCategory}'. Seed the Lookup table first.");

            return lookup.Id;
        }

        // GET: Appraisals/Create?employeeId=5
        [HttpGet]
        public async Task<IActionResult> Create(int employeeId)
        {
            var employee = await _context.Employees.FindAsync(employeeId);
            if (employee == null) return NotFound();

            var currentPosting = await _context.Postings
                .FirstOrDefaultAsync(p => p.EmployeeId == employeeId && p.ToDate == null);

            if (currentPosting == null)
            {
                ModelState.AddModelError("", "This employee has no current posting to appraise.");
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

            // Validate appraisal dates against the posting's contract end date
            var posting = await _context.Postings
                .Include(p => p.Contract)
                .FirstOrDefaultAsync(p => p.Id == model.PostingId);

            if (posting == null || posting.Contract == null)
            {
                ModelState.AddModelError("", "Could not find a contract linked to this posting.");
                return View(model);
            }

            if (model.ToDate > posting.Contract.EndDate)
            {
                ModelState.AddModelError(nameof(model.ToDate),
                    $"Appraisal end date cannot be after the contract end date ({posting.Contract.EndDate:dd MMM yyyy}).");
                return View(model);
            }

            var supervisor = await _context.Employees.FindAsync(model.SupervisorId);

            var draftStatusId = await GetStatusIdAsync("Draft");
            var supervisorReviewStatusId = await GetStatusIdAsync("SupervisorReview");

            // ... rest of your existing method continues unchanged from here

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

            await LogHistoryAsync(appraisal.Id, "Appraisal initiated", "Supervisor",
                draftStatusId, supervisorReviewStatusId);

            // Snapshot General KPIs (same for every employee, always active)
            var generalKpis = await _context.KPIs
                .Where(k => k.IsGeneral)
                .ToListAsync();

            foreach (var kpi in generalKpis)
            {
                _context.AppraisalKPIs.Add(new AppraisalKPI
                {
                    AppraisalId = appraisal.Id,
                    Section = KPISection.General,
                    Title = kpi.Title,
                    Description = kpi.Description,
                    Weight = kpi.Weight,
                    Rating = 0,
                    Score = 0
                });
            }

            // Snapshot Posting-Specific KPIs (only active ones, for this posting)
            var specificKpis = await _context.PostingKPIs
                .Where(pk => pk.PostingId == model.PostingId && pk.IsActive)
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
                    Rating = 0,
                    Score = 0
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Appraisal initiated. For further actions, go to the Performance tab.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AllowSelfAssessment(int id)
        {
            var appraisal = await _context.Appraisals.FindAsync(id);
            if (appraisal == null) return NotFound();

            var draftStatusId = await GetStatusIdAsync("Draft");
            var selfAssessmentStatusId = await GetStatusIdAsync("SelfAssessment");
            var supervisorReviewStatusId = await GetStatusIdAsync("SupervisorReview");

            if (appraisal.StatusId != draftStatusId)
                return BadRequest("Self-assessment can only be enabled while appraisal is in Draft.");

            appraisal.SelfAssessmentEnabled = true;
            appraisal.StatusId = selfAssessmentStatusId;

            await LogHistoryAsync(appraisal.Id, "Self-assessment enabled for employee", "Supervisor",
                selfAssessmentStatusId, supervisorReviewStatusId);

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
            bool isHR = User.IsInRole("HR");

            var draftStatusId = await GetStatusIdAsync("Draft");
            var selfAssessmentStatusId = await GetStatusIdAsync("SelfAssessment");
            var supervisorReviewStatusId = await GetStatusIdAsync("SupervisorReview");

            bool allowed =
                (appraisal.StatusId == draftStatusId && (isSupervisor || isHR)) ||
                (appraisal.StatusId == selfAssessmentStatusId && isEmployee) ||
                (appraisal.StatusId == supervisorReviewStatusId && isSupervisor);

            if (!allowed)
                return Forbid();

            var vm = new AppraisalScoreViewModel
            {
                AppraisalId = appraisal.Id,
                EmployeeName = appraisal.Employee.FullName,
                FromDate = appraisal.FromDate,
                ToDate = appraisal.ToDate,
                GeneralKPIs = appraisal.AppraisalKPIs.Where(k => k.Section == KPISection.General).ToList(),
                SpecificKPIs = appraisal.AppraisalKPIs.Where(k => k.Section == KPISection.Specific).ToList(),
                GeneralComment = appraisal.GeneralComment,
                SpecificComment = appraisal.SpecificComment,
                SupervisorId = appraisal.SupervisorId
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendBackToEmployee(int id, string comments)
        {
            var appraisal = await _context.Appraisals.FindAsync(id);
            if (appraisal == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            bool isSupervisor = currentEmployee?.Id == appraisal.SupervisorId;

            if (!isSupervisor)
                return Forbid();

            if (!appraisal.SelfAssessmentEnabled)
                return BadRequest("This appraisal was not set up for self-assessment.");

            var supervisorReviewStatusId = await GetStatusIdAsync("SupervisorReview");
            var selfAssessmentStatusId = await GetStatusIdAsync("SelfAssessment");

            if (appraisal.StatusId != supervisorReviewStatusId)
                return BadRequest("This appraisal is not currently in supervisor review.");

            appraisal.StatusId = selfAssessmentStatusId;

            await LogHistoryAsync(appraisal.Id, $"Sent back to employee for re-assessment: {comments}", "Supervisor",
                selfAssessmentStatusId, supervisorReviewStatusId);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> HRReview(int id, string? hrRemarks)
        {
            var appraisal = await _context.Appraisals.FindAsync(id);
            if (appraisal == null) return NotFound();

            if (!User.IsInRole("HR"))
                return Forbid();

            var submittedForReviewStatusId = await GetStatusIdAsync("SubmittedForReview");
            var hrReviewedStatusId = await GetStatusIdAsync("HRReviewed");

            if (appraisal.StatusId != submittedForReviewStatusId)
                return BadRequest("Appraisal is not awaiting HR review.");

            appraisal.StatusId = hrReviewedStatusId;
            appraisal.HRRemarks = hrRemarks;

            await LogHistoryAsync(appraisal.Id, "HR remarks added", "HR",
                hrReviewedStatusId, null);

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
                .FirstOrDefaultAsync(a => a.Id == model.AppraisalId);

            if (appraisal == null) return NotFound();

            foreach (var posted in model.GeneralKPIs.Concat(model.SpecificKPIs))
            {
                var kpi = appraisal.AppraisalKPIs.FirstOrDefault(k => k.Id == posted.Id);
                if (kpi == null) continue;

                kpi.Rating = posted.Rating;
                kpi.Score = kpi.Weight * kpi.Rating;
            }

            appraisal.GeneralComment = model.GeneralComment;
            appraisal.SpecificComment = model.SpecificComment;

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

            var currentUserId = _userManager.GetUserId(User);
            var currentEmployee = await _context.Employees
                .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

            bool isEmployee = currentEmployee?.Id == appraisal.EmployeeId;

            var selfAssessmentStatusId = await GetStatusIdAsync("SelfAssessment");
            var supervisorReviewStatusId = await GetStatusIdAsync("SupervisorReview");
            var submittedForReviewStatusId = await GetStatusIdAsync("SubmittedForReview");
            var approvedStatusId = await GetStatusIdAsync("Approved");

            if (appraisal.StatusId == selfAssessmentStatusId && isEmployee)
            {
                // Employee just finished self-assessment — sends it back to Supervisor
                appraisal.StatusId = supervisorReviewStatusId;

                await LogHistoryAsync(appraisal.Id, "Self-assessment submitted by employee", "Employee",
                    supervisorReviewStatusId, submittedForReviewStatusId);
            }
            else
            {
                // Supervisor finalizing (with or without prior self-assessment)
                appraisal.StatusId = submittedForReviewStatusId;
                appraisal.RecommendationText = model.RecommendationText;
                appraisal.RecommendedRank = model.RecommendedRank;

                await LogHistoryAsync(appraisal.Id, "KPIs reviewed and finalized by supervisor", "Supervisor",
                    submittedForReviewStatusId, approvedStatusId);
            }

            await _context.SaveChangesAsync();

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

            var hrReviewedStatusId = await GetStatusIdAsync("HRReviewed");
            var approvedStatusId = await GetStatusIdAsync("Approved");

            if (appraisal.StatusId != hrReviewedStatusId)
                return BadRequest("Appraisal is not awaiting review.");

            appraisal.StatusId = approvedStatusId;
            appraisal.ReviewedOn = DateTime.Now;
            appraisal.ReviewerComments = reviewerComments;
            appraisal.FinalRank = finalRank;
            appraisal.ActionRequired = actionRequired;

            await LogHistoryAsync(appraisal.Id, "Appraisal approved by reviewer", "Reviewer",
                approvedStatusId, null);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }

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

            var hrReviewedStatusId = await GetStatusIdAsync("HRReviewed");
            var rejectedStatusId = await GetStatusIdAsync("Rejected");

            if (appraisal.StatusId != hrReviewedStatusId)
                return BadRequest("Appraisal is not awaiting review.");

            appraisal.StatusId = rejectedStatusId;
            appraisal.ReviewerComments = reviewerComments;
            appraisal.ReviewedOn = DateTime.Now;
            appraisal.FinalRank = finalRank;
            appraisal.ActionRequired = actionRequired;

            await LogHistoryAsync(appraisal.Id, $"Appraisal rejected by reviewer: {reviewerComments}", "Reviewer",
                rejectedStatusId, null);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = appraisal.Id });
        }

        // GET: Appraisals
        public async Task<IActionResult> Index()
        {
            var appraisalsQuery = _context.Appraisals
                .Include(a => a.Employee)
                .Include(a => a.Supervisor)
                .Include(a => a.Reviewer)
                .Include(a => a.Status)
                .AsQueryable();

            if (User.IsInRole("Admin") || User.IsInRole("HR"))
            {
                // see everyone's appraisals
            }
            else
            {
                var currentUserId = _userManager.GetUserId(User);

                var currentEmployee = await _context.Employees
                    .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

                var currentEmployeeId = currentEmployee?.Id;

                appraisalsQuery = appraisalsQuery.Where(a =>
                    a.Employee.ApplicationUserId == currentUserId ||
                    a.SupervisorId == currentEmployeeId ||
                    a.ReviewerId == currentEmployeeId);
            }

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
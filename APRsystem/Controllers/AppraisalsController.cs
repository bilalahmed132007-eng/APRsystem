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

        public AppraisalsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
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
                SupervisorId = employee.SupervisorId ?? 0
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

            var appraisal = new Appraisal
            {
                EmployeeId = model.EmployeeId,
                PostingId = model.PostingId,
                SupervisorId = model.SupervisorId,
                FromDate = model.FromDate,
                ToDate = model.ToDate
            };

            _context.Appraisals.Add(appraisal);
            await _context.SaveChangesAsync(); // need appraisal.Id below

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

            return RedirectToAction(nameof(Score), new { id = appraisal.Id });
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

            var vm = new AppraisalScoreViewModel
            {
                AppraisalId = appraisal.Id,
                EmployeeName = appraisal.Employee.FullName,
                FromDate = appraisal.FromDate,
                ToDate = appraisal.ToDate,
                GeneralKPIs = appraisal.AppraisalKPIs.Where(k => k.Section == KPISection.General).ToList(),
                SpecificKPIs = appraisal.AppraisalKPIs.Where(k => k.Section == KPISection.Specific).ToList(),
                GeneralComment = appraisal.GeneralComment,
                SpecificComment = appraisal.SpecificComment
            };

            return View(vm);
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

            // Update ratings + score for each KPI row (matched by Id)
            foreach (var posted in model.GeneralKPIs.Concat(model.SpecificKPIs))
            {
                var kpi = appraisal.AppraisalKPIs.FirstOrDefault(k => k.Id == posted.Id);
                if (kpi == null) continue;

                kpi.Rating = posted.Rating;
                kpi.Score = kpi.Weight * kpi.Rating;
            }

            appraisal.GeneralComment = model.GeneralComment;
            appraisal.SpecificComment = model.SpecificComment;

            // Recalculate section totals
            var generalKpis = appraisal.AppraisalKPIs.Where(k => k.Section == KPISection.General).ToList();
            var specificKpis = appraisal.AppraisalKPIs.Where(k => k.Section == KPISection.Specific).ToList();

            appraisal.GeneralTotalScore = generalKpis.Sum(k => k.Score);
            appraisal.GeneralMaxScore = generalKpis.Sum(k => k.Weight * 4);

            appraisal.SpecificTotalScore = specificKpis.Sum(k => k.Score);
            appraisal.SpecificMaxScore = specificKpis.Sum(k => k.Weight * 4);

            // Combined grand total + percentage + ranking band
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
                .Include(a => a.AppraisalKPIs)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appraisal == null) return NotFound();

            return View(appraisal);
        }
        // GET: Appraisals
        public async Task<IActionResult> Index()
        {
            var appraisalsQuery = _context.Appraisals
                .Include(a => a.Employee)
                .Include(a => a.Supervisor)
                .AsQueryable();

            if (User.IsInRole("Admin") || User.IsInRole("HR"))
            {
                // see everyone's appraisals
            }
            else
            {
                var currentUserId = _userManager.GetUserId(User);

                if (User.IsInRole("Supervisor"))
                {
                    var supervisorEmployee = await _context.Employees
                        .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

                    var supervisorId = supervisorEmployee?.Id;

                    appraisalsQuery = appraisalsQuery.Where(a =>
                        a.Employee.ApplicationUserId == currentUserId ||
                        a.SupervisorId == supervisorId);
                }
                else
                {
                    appraisalsQuery = appraisalsQuery.Where(a => a.Employee.ApplicationUserId == currentUserId);
                }
            }

            return View(await appraisalsQuery.OrderByDescending(a => a.FromDate).ToListAsync());
        }
    }
}
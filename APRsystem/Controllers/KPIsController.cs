
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APRsystem.Models;
using APRsystem.Data;

public class KPIsController : Controller
{
    private readonly ApplicationDbContext _context;

    public KPIsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: KPIS
    public async Task<IActionResult> Index()
    {
        var generalKpis = await _context.KPIs
            .Where(k => k.IsGeneral)
            .OrderBy(k => k.Title)
            .ToListAsync();

        return View(generalKpis);
    }

    // GET: KPIS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var kpi = await _context.KPIs
            .FirstOrDefaultAsync(m => m.Id == id);
        if (kpi == null)
        {
            return NotFound();
        }

        return View(kpi);
    }

    // GET: KPIS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: KPIS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Title,Description,Weight")] KPI kpi)
    {
        if (ModelState.IsValid)
        {
            // This is a reusable General KPI
            kpi.IsGeneral = true;

            _context.Add(kpi);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        return View(kpi);
    }

    // GET: KPIS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var kpi = await _context.KPIs.FindAsync(id);
        if (kpi == null)
        {
            return NotFound();
        }
        return View(kpi);
    }

    // POST: KPIS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Title,Description,Weight,IsGeneral")] KPI kpi)
    {
        if (id != kpi.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(kpi);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!KPIExists(kpi.Id))
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
        return View(kpi);
    }

    // GET: KPIS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var kpi = await _context.KPIs
            .FirstOrDefaultAsync(m => m.Id == id);
        if (kpi == null)
        {
            return NotFound();
        }

        return View(kpi);
    }

    // POST: KPIS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var kpi = await _context.KPIs.FindAsync(id);
        if (kpi != null)
        {
            _context.KPIs.Remove(kpi);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool KPIExists(int? id)
    {
        return _context.KPIs.Any(e => e.Id == id);
    }
}
 
    


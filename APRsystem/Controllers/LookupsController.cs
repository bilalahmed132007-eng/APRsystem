
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APRsystem.Models;
using APRsystem.Data;

public class LookupsController : Controller
{
    private readonly ApplicationDbContext _context;

    public LookupsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: LOOKUPS
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Lookups.ToListAsync());
    }

    // GET: LOOKUPS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var lookup = await _context.Lookups
            .FirstOrDefaultAsync(m => m.Id == id);
        if (lookup == null)
        {
            return NotFound();
        }

        return View(lookup);
    }

    // GET: LOOKUPS/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: LOOKUPS/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Id,Category,Value,IsActive")] Lookup lookup)
    {
        if (ModelState.IsValid)
        {
            _context.Add(lookup);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(lookup);
    }

    // GET: LOOKUPS/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var lookup = await _context.Lookups.FindAsync(id);
        if (lookup == null)
        {
            return NotFound();
        }
        return View(lookup);
    }

    // POST: LOOKUPS/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Category,Value,IsActive")] Lookup lookup)
    {
        if (id != lookup.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(lookup);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LookupExists(lookup.Id))
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
        return View(lookup);
    }

    // GET: LOOKUPS/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var lookup = await _context.Lookups
            .FirstOrDefaultAsync(m => m.Id == id);
        if (lookup == null)
        {
            return NotFound();
        }

        return View(lookup);
    }

    // POST: LOOKUPS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var lookup = await _context.Lookups.FindAsync(id);
        if (lookup != null)
        {
            _context.Lookups.Remove(lookup);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool LookupExists(int? id)
    {
        return _context.Lookups.Any(e => e.Id == id);
    }
}

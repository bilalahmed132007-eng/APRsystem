using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using APRsystem.Models;
using APRsystem.Data;
using APRsystem.Authorization;

public class LocationsController : Controller
{
    private readonly ApplicationDbContext _context;

    public LocationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: LOCATIONS
    public async Task<IActionResult> Index()
    {
        return View(await _context.Locations.ToListAsync());
    }

    // GET: LOCATIONS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var location = await _context.Locations
            .FirstOrDefaultAsync(m => m.Id == id);
        if (location == null)
        {
            return NotFound();
        }

        return View(location);
    }

    // GET: LOCATIONS/Create
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public IActionResult Create()
    {
        return View();
    }

    // POST: LOCATIONS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public async Task<IActionResult> Create([Bind("Id,Name,Address,IsActive")] Location location)
    {
        if (ModelState.IsValid)
        {
            _context.Add(location);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(location);
    }

    // GET: LOCATIONS/Edit/5
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var location = await _context.Locations.FindAsync(id);
        if (location == null)
        {
            return NotFound();
        }
        return View(location);
    }

    // POST: LOCATIONS/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Name,Address,IsActive")] Location location)
    {
        if (id != location.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(location);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!LocationExists(location.Id))
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
        return View(location);
    }

    // GET: LOCATIONS/Delete/5
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }               

        var location = await _context.Locations
            .FirstOrDefaultAsync(m => m.Id == id);
        if (location == null)
        {
            return NotFound();
        }

        return View(location);
    }

    // POST: LOCATIONS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var location = await _context.Locations.FindAsync(id);
        if (location == null)
            return NotFound();

        bool inUse = await _context.Postings.AnyAsync(p => p.LocationId == location.Id);
        if (inUse)
        {
            ModelState.AddModelError("", "This location is currently assigned to one or more postings and cannot be deleted. Consider deactivating it instead (set IsActive = false).");
            return View("Delete", location);
        }

        _context.Locations.Remove(location);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool LocationExists(int id)
    {
        return _context.Locations.Any(e => e.Id == id);
    }
}
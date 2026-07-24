using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using APRsystem.Models;
using APRsystem.Data;
using APRsystem.Authorization;

[Authorize(Policy = Permissions.DepartmentsManage)]
public class DepartmentsController : Controller
{
    private readonly ApplicationDbContext _context;

    public DepartmentsController(ApplicationDbContext context)
    {
        _context = context;
    }

    // GET: DEPARTMENTS
    public async Task<IActionResult> Index()
    {
        return View(await _context.Departments.ToListAsync());
    }

    // GET: DEPARTMENTS/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var department = await _context.Departments
            .FirstOrDefaultAsync(m => m.Id == id);
        if (department == null)
        {
            return NotFound();
        }

        return View(department);
    }

    // GET: DEPARTMENTS/Create
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public IActionResult Create()
    {
        return View();
    }

    // POST: DEPARTMENTS/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public async Task<IActionResult> Create(Department department)
    {
        // Ignore navigation property validation
        ModelState.Remove(nameof(Department.Postings));

        if (!ModelState.IsValid)
        {
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    Console.WriteLine($"{state.Key} : {error.ErrorMessage}");
                }
            }

            return View(department);
        }

        _context.Departments.Add(department);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    // GET: DEPARTMENTS/Edit/5
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var department = await _context.Departments.FindAsync(id);
        if (department == null)
        {
            return NotFound();
        }
        return View(department);
    }

    // POST: DEPARTMENTS/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public async Task<IActionResult> Edit(int? id, [Bind("Id,Name,Code,IsActive")] Department department)
    {
        ModelState.Remove(nameof(Department.Postings));
        if (id != department.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(department);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!DepartmentExists(department.Id))
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
        return View(department);
    }

    // GET: DEPARTMENTS/Delete/5
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var department = await _context.Departments
            .FirstOrDefaultAsync(m => m.Id == id);
        if (department == null)
        {
            return NotFound();
        }

        return View(department);
    }

    // POST: DEPARTMENTS/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.DepartmentsManage)]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var department = await _context.Departments.FindAsync(id);
        if (department != null)
        {
            _context.Departments.Remove(department);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool DepartmentExists(int? id)
    {
        return _context.Departments.Any(e => e.Id == id);
    }
}
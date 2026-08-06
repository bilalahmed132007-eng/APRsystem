using APRsystem.Data;
using APRsystem.Models;
using APRsystem.Models.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace APRsystem.Controllers.Api
{
    [Authorize]
    [Route("api/employees")]
    [ApiController]
    public class EmployeesApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public EmployeesApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: api/employees
        // Same visibility rules as the old MVC Index action, just returned as JSON.
        [HttpGet]
        public async Task<IActionResult> GetIndex()
        {
            var employeesQuery = _context.Employees
                .Include(e => e.Supervisor)
                .Include(e => e.Postings.Where(p => p.ToDate == null))
                    .ThenInclude(p => p.Department)
                .Include(e => e.Postings.Where(p => p.ToDate == null))
                    .ThenInclude(p => p.Designation)
                .AsQueryable();

            int? supervisorId = null;

            if (User.IsInRole("Admin") || User.IsInRole("HR"))
            {
                // no filter — see everyone
            }
            else
            {
                var currentUserId = _userManager.GetUserId(User);

                if (User.IsInRole("Supervisor"))
                {
                    var supervisorEmployee = await _context.Employees
                        .FirstOrDefaultAsync(e => e.ApplicationUserId == currentUserId);

                    supervisorId = supervisorEmployee?.Id;

                    employeesQuery = employeesQuery.Where(e =>
                        e.ApplicationUserId == currentUserId ||
                        e.SupervisorId == supervisorId);
                }
                else
                {
                    employeesQuery = employeesQuery.Where(e => e.ApplicationUserId == currentUserId);
                }
            }

            var employees = await employeesQuery.ToListAsync();

            // Shape the response — only send what the UI needs, not full EF entities
            // (avoids circular-reference / over-fetching issues in the JSON payload)
            var result = employees.Select(e =>
            {
                var currentPosting = e.Postings.FirstOrDefault(p => p.ToDate == null);

                return new
                {
                    id = e.Id,
                    employeeNo = e.EmployeeNo,
                    fullName = e.FullName,
                    email = e.Email,
                    phone = e.Phone,
                    isActive = e.IsActive,
                    supervisorName = e.Supervisor?.FullName,
                    department = currentPosting?.Department?.Name,
                    designation = currentPosting?.Designation?.Label // uses Label, not the numeric Value
                };
            });

            return Ok(new
            {
                supervisorId,
                employees = result
            });
        }
    }
}

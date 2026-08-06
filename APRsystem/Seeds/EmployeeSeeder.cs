using APRsystem.Data;
using APRsystem.Models;
using APRsystem.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace APRsystem.Seeds
{
    /// <summary>
    /// TEST DATA ONLY. Seeds a 5-level org chain (CEO -> Director -> Manager ->
    /// Team Lead -> Employee) plus one teammate and one direct report at the
    /// bottom, so the team tree, GrandSupervisor lookup, and breadcrumbs can all
    /// be tested against real relationships in one pass.
    ///
    /// All test accounts use password: Test@123
    /// Emails: ceo.test@apr.local, director.test@apr.local, manager.test@apr.local,
    ///         supervisor.test@apr.local, employee.test@apr.local,
    ///         teammate.test@apr.local, report.test@apr.local
    ///
    /// Safe to run multiple times — checks for existing records by EmployeeNo
    /// before creating anything.
    /// </summary>
    public static class EmployeeSeeder
    {
        private const string TestPassword = "Test@123";

        public static async Task SeedTestHierarchyAsync(IServiceProvider serviceProvider)
        {
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // Bail out early if this has already run (checked via a distinctive EmployeeNo).
            if (await context.Employees.AnyAsync(e => e.EmployeeNo == "TEST-001"))
            {
                return;
            }

            // ---------- Supporting lookups ----------
            var department = await context.Departments.FirstOrDefaultAsync(d => d.Code == "TEST");
            if (department == null)
            {
                department = new Department { Name = "Test Department", Code = "TEST", IsActive = true };
                context.Departments.Add(department);
                await context.SaveChangesAsync();
            }

            var location = await context.Locations.FirstOrDefaultAsync(l => l.Name == "Test Office - Islamabad");
            if (location == null)
            {
                location = new Location { Name = "Test Office - Islamabad", Address = "Test Address", IsActive = true };
                context.Locations.Add(location);
                await context.SaveChangesAsync();
            }

            var designationTitles = new[] { "Chief Executive Officer", "Director", "Manager", "Senior Employee", "Employee" };
            var designations = new Dictionary<string, Lookup>();

            foreach (var title in designationTitles)
            {
                var lookup = await context.Lookups
                    .FirstOrDefaultAsync(l => l.Category == "Designation" && l.Value == title);

                if (lookup == null)
                {
                    lookup = new Lookup { Category = "Designation", Value = title, Label = title, IsActive = true };
                    context.Lookups.Add(lookup);
                    await context.SaveChangesAsync();
                }

                designations[title] = lookup;
            }

            // ---------- Ensure test roles exist (skip silently if RoleSeeder uses different names) ----------
            string[] rolesNeeded = { "Admin", "HR", "Employee" };
            foreach (var roleName in rolesNeeded)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            // ---------- Build the chain, one level at a time ----------
            // Each tuple: (EmployeeNo, FullName, Email, Designation title, Role, SupervisorEmployee-or-null)
            Employee? ceo = await CreateTestEmployeeAsync(context, userManager,
                "TEST-001", "Zafar Iqbal", "ceo.test@apr.local",
                department, location, designations["Chief Executive Officer"],
                role: "Admin", supervisor: null);

            Employee? director = await CreateTestEmployeeAsync(context, userManager,
                "TEST-002", "Ayesha Noor", "director.test@apr.local",
                department, location, designations["Director"],
                role: "Employee", supervisor: ceo);

            Employee? manager = await CreateTestEmployeeAsync(context, userManager,
                "TEST-003", "Usman Tariq", "manager.test@apr.local",
                department, location, designations["Manager"],
                role: "Employee", supervisor: director);

            // NOTE: "Supervisor" designation was removed — this employee now uses "Manager" too.
            // Being someone's supervisor is purely a SupervisorId relationship now, not a title or role.
            Employee? supervisor = await CreateTestEmployeeAsync(context, userManager,
                "TEST-004", "Hina Baig", "supervisor.test@apr.local",
                department, location, designations["Manager"],
                role: "Employee", supervisor: manager);

            Employee? employee = await CreateTestEmployeeAsync(context, userManager,
                "TEST-005", "Ahsan Raza", "employee.test@apr.local",
                department, location, designations["Senior Employee"],
                role: "Employee", supervisor: supervisor);

            // Teammate: also reports to Hina Baig, sits next to Ahsan Raza — tests the "Teammates" row
            await CreateTestEmployeeAsync(context, userManager,
                "TEST-006", "Sara Khan", "teammate.test@apr.local",
                department, location, designations["Senior Employee"],
                role: "Employee", supervisor: supervisor);

            // Direct report: reports to Ahsan Raza — tests the "DirectReports" row and makes
            // Ahsan himself act as a Supervisor for someone below him
            await CreateTestEmployeeAsync(context, userManager,
                "TEST-007", "Bilal Rafiq", "report.test@apr.local",
                department, location, designations["Employee"],
                role: "Employee", supervisor: employee);
        }

        private static async Task<Employee?> CreateTestEmployeeAsync(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            string employeeNo,
            string fullName,
            string email,
            Department department,
            Location location,
            Lookup designation,
            string role,
            Employee? supervisor)
        {
            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FullName = fullName,
                IsActive = true
            };

            var result = await userManager.CreateAsync(user, TestPassword);
            if (!result.Succeeded)
            {
                // Likely already exists from a previous partial run — skip rather than crash the app.
                return null;
            }

            await userManager.AddToRoleAsync(user, role);

            var employee = new Employee
            {
                EmployeeNo = employeeNo,
                FullName = fullName,
                CNIC = "00000-0000000-0",
                Email = email,
                Phone = "0300-0000000",
                JoiningDate = DateTime.Today.AddYears(-2),
                IsActive = true,
                SupervisorId = supervisor?.Id,
                ApplicationUserId = user.Id
            };

            context.Employees.Add(employee);
            await context.SaveChangesAsync();

            var contract = new Contract
            {
                EmployeeId = employee.Id,
                ContractNumber = $"CN-{employeeNo}",
                Type = ContractType.Permanent,
                StartDate = employee.JoiningDate,
                IsActive = true
            };
            context.Contracts.Add(contract);
            await context.SaveChangesAsync();

            var posting = new Posting
            {
                EmployeeId = employee.Id,
                ContractId = contract.Id,
                DepartmentId = department.Id,
                DesignationId = designation.Id,
                LocationId = location.Id,
                SupervisorId = supervisor?.Id,
                Salary = 100000,
                FromDate = employee.JoiningDate,
                ToDate = null
            };
            context.Postings.Add(posting);
            await context.SaveChangesAsync();

            return employee;
        }
    }
}
using APRsystem.Data;
using APRsystem.Models;
using APRsystem.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace APRsystem.Seeds
{
    public static class DemoDataSeeder
    {
        // Simple, predictable demo data. Safe to run more than once — skips anything that already exists
        // (matched by Department.Code and Employee.EmployeeNo).
        public static async Task SeedAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            Console.WriteLine("Seeding demo data...");

            // ---- 1. Location (Postings need one) ----
            var location = await context.Locations.FirstOrDefaultAsync(l => l.Name == "Head Office");
            if (location == null)
            {
                location = new Location { Name = "Head Office", Address = "Islamabad", IsActive = true };
                context.Locations.Add(location);
                await context.SaveChangesAsync();
                Console.WriteLine("  Created Location: Head Office");
            }

            // ---- 2. Designation lookup (must already exist per your setup — just grab whatever's there) ----
            var designations = await context.Lookups
                .Where(l => l.Category == "Designation" && l.IsActive)
                .ToListAsync();

            if (!designations.Any())
            {
                Console.WriteLine("  ERROR: No active Designation lookups found. Seed at least one Designation lookup value first, then re-run.");
                return;
            }

            // ---- 3. Departments ----
            var departmentDefs = new[]
            {
                new { Name = "Finance", Code = "FIN" },
                new { Name = "Information Technology", Code = "IT" },
                new { Name = "Human Resources", Code = "HR" }
            };

            var departments = new List<Department>();
            foreach (var def in departmentDefs)
            {
                var dept = await context.Departments.FirstOrDefaultAsync(d => d.Code == def.Code);
                if (dept == null)
                {
                    dept = new Department { Name = def.Name, Code = def.Code, IsActive = true };
                    context.Departments.Add(dept);
                    await context.SaveChangesAsync();
                    Console.WriteLine($"  Created Department: {def.Name}");
                }
                departments.Add(dept);
            }

            // ---- 4. 3 employees per department: employee 1 = Supervisor, employees 2 & 3 report to them ----
            const string password = "Test@123";
            var today = DateTime.Today;
            var designationIndex = 0;

            foreach (var dept in departments)
            {
                Employee? deptSupervisor = null;

                for (int i = 1; i <= 3; i++)
                {
                    var employeeNo = $"{dept.Code}-{i:000}";
                    bool isSupervisor = (i == 1);

                    var existing = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo);
                    if (existing != null)
                    {
                        Console.WriteLine($"  Skipping {employeeNo} — already exists.");
                        if (isSupervisor) deptSupervisor = existing;
                        continue;
                    }

                    var email = $"{dept.Code.ToLower()}.employee{i}@aprsystem.com";
                    var fullName = isSupervisor
                        ? $"{dept.Name} Supervisor"
                        : $"{dept.Name} Employee {i}";

                    // --- Identity user ---
                    var user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true
                    };

                    var createResult = await userManager.CreateAsync(user, password);
                    if (!createResult.Succeeded)
                    {
                        Console.WriteLine($"  ERROR creating user {email}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                        continue;
                    }

                    await userManager.AddToRoleAsync(user, isSupervisor ? "Supervisor" : "Employee");

                    // --- Employee ---
                    var employee = new Employee
                    {
                        EmployeeNo = employeeNo,
                        FullName = fullName,
                        CNIC = $"35202-{dept.Code}{i:000}-1",
                        Email = email,
                        Phone = $"0300-000{i}{dept.Code.Length}00",
                        JoiningDate = today,
                        IsActive = true,
                        ApplicationUserId = user.Id,
                        SupervisorId = isSupervisor ? null : deptSupervisor?.Id
                    };

                    context.Employees.Add(employee);
                    await context.SaveChangesAsync();

                    if (isSupervisor)
                        deptSupervisor = employee;

                    // --- Contract: 1 year, starting today ---
                    var contract = new Contract
                    {
                        EmployeeId = employee.Id,
                        ContractNumber = $"{employeeNo}-C1",
                        Type = ContractType.Contract,
                        StartDate = today,
                        EndDate = today.AddYears(1),
                        IsActive = true
                    };

                    context.Contracts.Add(contract);
                    await context.SaveChangesAsync();

                    // --- Posting: current posting under this contract ---
                    var designation = designations[designationIndex % designations.Count];
                    designationIndex++;

                    var posting = new Posting
                    {
                        EmployeeId = employee.Id,
                        ContractId = contract.Id,
                        DepartmentId = dept.Id,
                        DesignationId = designation.Id,
                        SupervisorId = isSupervisor ? null : deptSupervisor?.Id,
                        Salary = isSupervisor ? 80000 : 50000,
                        LocationId = location.Id,
                        FromDate = today,
                        ToDate = null
                    };

                    context.Postings.Add(posting);
                    await context.SaveChangesAsync();

                    var roleLabel = isSupervisor ? "Supervisor" : $"reports to {deptSupervisor?.FullName}";
                    Console.WriteLine($"  Created {fullName} ({email} / {password}) — {roleLabel}");
                }
            }

            Console.WriteLine("Demo data seeding complete.");
        }

        // One-off fixup for data seeded BEFORE the supervisor logic existed.
        // Safe to run multiple times — only sets SupervisorId/roles where they're currently missing.
        // Run with: dotnet run -- fix-supervisors
        public static async Task FixSupervisorsAsync(IServiceProvider services)
        {
            var context = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            Console.WriteLine("Fixing up supervisor relationships on existing demo data...");

            var departmentCodes = new[] { "FIN", "IT", "HR" };

            foreach (var code in departmentCodes)
            {
                var supervisor = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeNo == $"{code}-001");
                if (supervisor == null)
                {
                    Console.WriteLine($"  Skipping {code} — no {code}-001 employee found (run the main seed first).");
                    continue;
                }

                // Give employee 1 the Supervisor role (kept in addition to Employee — not removing anything).
                var supervisorUser = await context.Users.FirstOrDefaultAsync(u => u.Id == supervisor.ApplicationUserId);
                if (supervisorUser != null && !await userManager.IsInRoleAsync(supervisorUser, "Supervisor"))
                {
                    await userManager.AddToRoleAsync(supervisorUser, "Supervisor");
                    Console.WriteLine($"  Added 'Supervisor' role to {supervisor.FullName}");
                }

                if (string.IsNullOrEmpty(supervisor.FullName) || !supervisor.FullName.Contains("Supervisor"))
                {
                    supervisor.FullName = supervisor.FullName.Replace("Employee 1", "Supervisor").Trim();
                }

                foreach (var suffix in new[] { "002", "003" })
                {
                    var employeeNo = $"{code}-{suffix}";
                    var employee = await context.Employees.FirstOrDefaultAsync(e => e.EmployeeNo == employeeNo);
                    if (employee == null) continue;

                    if (employee.SupervisorId != supervisor.Id)
                    {
                        employee.SupervisorId = supervisor.Id;
                        Console.WriteLine($"  Linked {employee.FullName} -> reports to {supervisor.FullName}");
                    }

                    var currentPosting = await context.Postings
                        .FirstOrDefaultAsync(p => p.EmployeeId == employee.Id && p.ToDate == null);

                    if (currentPosting != null && currentPosting.SupervisorId != supervisor.Id)
                    {
                        currentPosting.SupervisorId = supervisor.Id;
                        Console.WriteLine($"  Updated {employee.FullName}'s current posting -> supervisor {supervisor.FullName}");
                    }
                }
            }

            await context.SaveChangesAsync();
            Console.WriteLine("Supervisor fixup complete.");
        }
    }
}
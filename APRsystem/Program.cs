using APRsystem.Authorization;
using APRsystem.Data;
using APRsystem.Models.Identity;
using APRsystem.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<WorkflowService>();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddControllersWithViews();

// Explicit AddRazorPages so we can carve out an AllowAnonymous exception for the
// Identity area — without this, the global FallbackPolicy below (which requires
// an authenticated user on every endpoint) also applies to the Login page itself,
// causing an infinite login redirect loop.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AllowAnonymousToAreaFolder("Identity", "/Account");
});

// 👇 Add this block
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    foreach (var permission in Permissions.All)
    {
        options.AddPolicy(permission, policy =>
            policy.RequireClaim(Permissions.ClaimType, permission));
    }
});

// 👇 Optional: customize redirect paths
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    await APRsystem.Seeds.RoleSeeder.SeedRolesAsync(services);
    await APRsystem.Seeds.AdminSeeder.SeedAdminAsync(services);
    await APRsystem.Seeds.PermissionSeeder.SeedPermissionsAsync(services);
    await APRsystem.Seeds.EmployeeSeeder.SeedTestHierarchyAsync(services);

    // Manual demo-data seed: run with `dotnet run -- seed`.
    // Seeds the DB then exits immediately — does not start the web server.
    if (args.Contains("seed"))
    {
        await APRsystem.Seeds.DemoDataSeeder.SeedAsync(services);
        return;
    }

    // One-off fixup for supervisor relationships on already-seeded data.
    // Run with: dotnet run -- fix-supervisors
    if (args.Contains("fix-supervisors"))
    {
        await APRsystem.Seeds.DemoDataSeeder.FixSupervisorsAsync(services);
        return;
    }
}

app.Run();
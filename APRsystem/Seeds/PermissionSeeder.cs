using Microsoft.AspNetCore.Identity;
using APRsystem.Authorization;

namespace APRsystem.Seeds
{
    public static class PermissionSeeder
    {
        public static async Task SeedPermissionsAsync(IServiceProvider services)
        {
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            var defaults = new Dictionary<string, string[]>
            {
                ["Admin"] = Permissions.All, // full access, always

                ["HR"] = new[]
  {
    Permissions.DashboardView,

    Permissions.UsersView,
    Permissions.UsersCreate,
    Permissions.UsersEdit,

    Permissions.DepartmentsManage,
    Permissions.PositionsManage,
    Permissions.PostingsManage,

    Permissions.KPIsManage,

    Permissions.ContractsManage,

    Permissions.AuditLogsView
},

                ["Supervisor"] = new[]
{
    Permissions.DashboardView,

    Permissions.UsersView,
    Permissions.UsersEdit,

    Permissions.KPIsView,
    Permissions.KPIsManage
},

                ["Employee"] = new[]
{
    Permissions.DashboardView,

    Permissions.UsersView,

    Permissions.KPIsView,

    Permissions.ContractsView
},
            };

            foreach (var (roleName, perms) in defaults)
            {
                var role = await roleManager.FindByNameAsync(roleName);
                if (role == null) continue; // RoleSeeder should run before this

                var existingClaims = await roleManager.GetClaimsAsync(role);

                foreach (var perm in perms)
                {
                    bool alreadyHas = existingClaims.Any(c =>
                        c.Type == Permissions.ClaimType && c.Value == perm);

                    if (!alreadyHas)
                    {
                        await roleManager.AddClaimAsync(role,
                            new System.Security.Claims.Claim(Permissions.ClaimType, perm));
                    }
                }
            }
        }
    }
}
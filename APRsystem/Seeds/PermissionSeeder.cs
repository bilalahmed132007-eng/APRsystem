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
                ["Admin"] = Permissions.All, // full access, always — resynced every run

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

                if (roleName == "Admin")
                {
                    // Admin is a safety net: always ensure every permission is present,
                    // regardless of what the admin UI has changed. Prevents anyone from
                    // accidentally locking all admins out of the system.
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
                    continue;
                }

                // All other roles: bootstrap-only. Once this role has ANY permission
                // claims (meaning it's been seeded before, or edited via the admin UI),
                // leave it alone — the admin UI is the source of truth from then on.
                if (existingClaims.Any(c => c.Type == Permissions.ClaimType))
                    continue;

                foreach (var perm in perms)
                {
                    await roleManager.AddClaimAsync(role,
                        new System.Security.Claims.Claim(Permissions.ClaimType, perm));
                }
            }
        }
    }
}
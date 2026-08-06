using APRsystem.Models.Identity;
using APRsystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PermissionsList = APRsystem.Authorization.Permissions;
using System.Security.Claims;

namespace APRsystem.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(
 UserManager<ApplicationUser> userManager,
 RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public IActionResult Index()
    {
        var model = new AdminDashboardViewModel
        {
            TotalUsers = _userManager.Users.Count(),
            ActiveUsers = _userManager.Users.Count(u => u.IsActive)
        };

        return View(model);
    }
    public IActionResult Roles()
    {
        var roles = _roleManager.Roles.ToList();
        return View(roles);
    }
    public async Task<IActionResult> ManageUsers()
    {
        var users = _userManager.Users.ToList();

        var model = new List<ManageUserRolesViewModel>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);

            model.Add(new ManageUserRolesViewModel
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email!,
                CurrentRole = roles.FirstOrDefault() ?? "No Role"
            });
        }

        return View(model);
    }

    // GET: Admin/EditUserRole/5
    public async Task<IActionResult> EditUserRole(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        var allRoles = _roleManager.Roles.Select(r => r.Name ?? string.Empty).ToList();

        var model = new EditUserRoleViewModel
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            CurrentRole = currentRoles.FirstOrDefault() ?? "No Role",
            SelectedRole = currentRoles.FirstOrDefault() ?? string.Empty,
            AvailableRoles = allRoles
        };

        return View(model);
    }

    // POST: Admin/EditUserRole/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditUserRole(string id, string selectedRole)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var currentRoles = await _userManager.GetRolesAsync(user);

        if (currentRoles.Any())
        {
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
        }

        if (!string.IsNullOrEmpty(selectedRole))
        {
            await _userManager.AddToRoleAsync(user, selectedRole);
        }

        TempData["Success"] = $"Role updated for {user.FullName}.";
        return RedirectToAction(nameof(ManageUsers));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRole(RoleViewModel model)
    {
        if (ModelState.IsValid)
        {
            if (!await _roleManager.RoleExistsAsync(model.RoleName))
            {
                await _roleManager.CreateAsync(new IdentityRole(model.RoleName));
            }
        }

        return RedirectToAction(nameof(Roles));
    }

    public async Task<IActionResult> Permissions()
    {
        var roles = _roleManager.Roles.ToList();
        var model = new PermissionsManagementViewModel();

        foreach (var role in roles)
        {
            var roleClaims = await _roleManager.GetClaimsAsync(role);
            var grantedPermissions = roleClaims
                .Where(c => c.Type == PermissionsList.ClaimType)
                .Select(c => c.Value)
                .ToHashSet();

            var rolePermissionVm = new RolePermissionsViewModel
            {
                RoleId = role.Id,
                RoleName = role.Name ?? string.Empty,
                Permissions = PermissionsList.All.Select(p => new PermissionCheckboxViewModel
                {
                    PermissionValue = p,
                    IsGranted = grantedPermissions.Contains(p)
                }).ToList()
            };

            model.Roles.Add(rolePermissionVm);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateRolePermissions(string roleId, List<string> selectedPermissions)
    {
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role == null)
        {
            return NotFound();
        }

        selectedPermissions ??= new List<string>();

        var existingClaims = await _roleManager.GetClaimsAsync(role);
        var existingPermissionClaims = existingClaims
            .Where(c => c.Type == PermissionsList.ClaimType)
            .ToList();

        foreach (var claim in existingPermissionClaims)
        {
            if (!selectedPermissions.Contains(claim.Value))
            {
                await _roleManager.RemoveClaimAsync(role, claim);
            }
        }

        var currentValues = existingPermissionClaims.Select(c => c.Value).ToHashSet();
        foreach (var perm in selectedPermissions)
        {
            if (!currentValues.Contains(perm))
            {
                await _roleManager.AddClaimAsync(role, new Claim(PermissionsList.ClaimType, perm));
            }
        }

        TempData["Success"] = $"Permissions updated for role '{role.Name}'.";
        return RedirectToAction(nameof(Permissions));
    }
}
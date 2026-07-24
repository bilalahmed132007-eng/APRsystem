namespace APRsystem.ViewModels
{
    public class RolePermissionsViewModel
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;
        public List<PermissionCheckboxViewModel> Permissions { get; set; } = new();
    }
}
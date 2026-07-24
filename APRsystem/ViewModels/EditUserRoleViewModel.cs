namespace APRsystem.ViewModels
{
    public class EditUserRoleViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CurrentRole { get; set; } = string.Empty;
        public string SelectedRole { get; set; } = string.Empty;
        public List<string> AvailableRoles { get; set; } = new();
    }
}
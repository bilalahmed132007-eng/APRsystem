using APRsystem.Models;

namespace APRsystem.ViewModels
{
    public class DashboardViewModel
    {
        public bool IsAdminOrHR { get; set; }
        public bool IsSupervisor { get; set; }

        // Org-wide / team stat counts (Admin/HR/Supervisor cards)
        public int EmployeeCount { get; set; }
        public int PostingCount { get; set; }
        public Employee Employee { get; set; } = null!;
        public int KpiCount { get; set; }
        public int ContractCount { get; set; }
        public int DepartmentCount { get; set; }
        public int LookupCount { get; set; }


        // Team tree data — populated for Employee & Supervisor roles
        public TeamTreeViewModel? TeamTree { get; set; }
    }
}
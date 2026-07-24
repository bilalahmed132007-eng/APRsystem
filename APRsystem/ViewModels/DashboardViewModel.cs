namespace APRsystem.ViewModels
{
    public class DashboardViewModel
    {
        public int EmployeeCount { get; set; }
        public int PostingCount { get; set; }
        public int KpiCount { get; set; }
        public int ContractCount { get; set; }
        public int DepartmentCount { get; set; }
        public int LookupCount { get; set; }

        public bool IsAdminOrHR { get; set; }
        public bool IsSupervisor { get; set; }
    }
}
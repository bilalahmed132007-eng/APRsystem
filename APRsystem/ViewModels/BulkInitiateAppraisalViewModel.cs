namespace APRsystem.ViewModels
{
    public class BulkInitiateAppraisalViewModel
    {
        public int? DepartmentId { get; set; }

        public int? DesignationId { get; set; }

        public string? ContractType { get; set; }

        public DateTime FromDate { get; set; } = DateTime.Today;

        public DateTime ToDate { get; set; } = DateTime.Today.AddYears(1);

        public APRsystem.Models.AppraisalType Type { get; set; }
            = APRsystem.Models.AppraisalType.Annual;

        public bool ShowNoKpisOnly { get; set; }

        public List<BulkEmployeeRow> Employees { get; set; } = new();
    }

    // NEW — replaces the old HasActiveAppraisal bool with a real three-state value
    public enum BulkAppraisalStatus
    {
        Ready,
        InProgress,
        Completed
    }

    public class BulkEmployeeRow
    {
        public int EmployeeId { get; set; }
        public int? AppraisalId { get; set; }

        // REMOVED: HasActiveAppraisal — replaced by Status below
        public BulkAppraisalStatus Status { get; set; }

        public string FullName { get; set; } = string.Empty;

        public string EmployeeNo { get; set; } = string.Empty;

        public string Department { get; set; } = "-";

        public string Designation { get; set; } = "-";

        public string ContractType { get; set; } = "-";

        // Employee's current posting has active Posting KPIs
        public bool HasSpecificKpis { get; set; }
    }
}
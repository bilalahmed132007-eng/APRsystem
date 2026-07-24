using APRsystem.Models;

namespace APRsystem.ViewModels
{
    public class AppraisalScoreViewModel
    {
        public int AppraisalId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        public List<AppraisalKPI> GeneralKPIs { get; set; } = new();
        public string? GeneralComment { get; set; }

        public List<AppraisalKPI> SpecificKPIs { get; set; } = new();
        public string? SpecificComment { get; set; }
    }
}
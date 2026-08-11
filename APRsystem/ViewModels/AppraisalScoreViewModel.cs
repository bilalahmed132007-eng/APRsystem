using APRsystem.Models;

namespace APRsystem.ViewModels
{
    public class AppraisalScoreViewModel
    {
        public int AppraisalId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int EmployeeId { get; set; }

        public List<AppraisalKPI> GeneralKPIs { get; set; } = new();
        public string? GeneralComment { get; set; }
        public string? SelfGeneralComment { get; set; }
        public string? SelfSpecificComment { get; set; }

        public string? SupervisorGeneralComment { get; set; }
        public string? SupervisorSpecificComment { get; set; }

        public List<AppraisalKPI> SpecificKPIs { get; set; } = new();
        public string? SpecificComment { get; set; }
        public int SupervisorId { get; set; }

        public string? RecommendationText { get; set; }
        public string? RecommendedRank { get; set; }

        // "Self"       -> employee is editing SelfRating (status = SelfAssessment)
        // "Supervisor" -> supervisor is editing Rating (status = SupervisorRating)
        // "None"       -> read-only (e.g. supervisor viewing during SupervisorReview, deciding Approve/Revert)
        public string EditableField { get; set; } = "None";
    }
}
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

        // Employee's closing comment, added after reviewing the supervisor's rating
        // (bound in the Score view during the EmployeeComment stage).
        public string? EmployeeFinalComment { get; set; }

        // Supervisor's final rank + comment, given after the employee's comment
        // (bound in the Score view during the SupervisorRank stage). Never shown to the employee.
        public string? SupervisorFinalRank { get; set; }
        public string? SupervisorRankComment { get; set; }

        // "Self"             -> employee is editing SelfRating (status = SelfAssessment)
        // "Supervisor"       -> supervisor is editing Rating (status = SupervisorRating)
        // "EmployeeComment"  -> employee is viewing the supervisor's rating (read-only) and
        //                       editing EmployeeFinalComment (status = EmployeeComment)
        // "SupervisorRank"   -> supervisor is editing SupervisorFinalRank/SupervisorRankComment
        //                       (status = SupervisorRank)
        // "None"             -> read-only (e.g. supervisor viewing during SupervisorReview, deciding Approve/Revert)
        public string EditableField { get; set; } = "None";
    }
}
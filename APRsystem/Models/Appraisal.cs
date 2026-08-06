using System.ComponentModel.DataAnnotations.Schema;

namespace APRsystem.Models
{
    public class Appraisal
    {
        public int Id { get; set; }

        public int EmployeeId { get; set; }
        public Employee Employee { get; set; } = null!;

        public int PostingId { get; set; }
        public Posting Posting { get; set; } = null!;

        public int SupervisorId { get; set; }
        public Employee Supervisor { get; set; } = null!;

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }

        // General KPI section
        [Column(TypeName = "decimal(6,2)")]
        public decimal GeneralTotalScore { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal GeneralMaxScore { get; set; }

        

        // Posting-Specific KPI section
        [Column(TypeName = "decimal(6,2)")]
        public decimal SpecificTotalScore { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal SpecificMaxScore { get; set; }

        public string? SelfGeneralComment { get; set; }
        public string? SelfSpecificComment { get; set; }

        public string? SupervisorGeneralComment { get; set; }
        public string? SupervisorSpecificComment { get; set; }

        public string? HRRemarks { get; set; }

        public string? ReviewerComments { get; set; }

        // Combined
        [Column(TypeName = "decimal(6,2)")]
        public decimal GrandTotalScore { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal GrandMaxScore { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal Percentage { get; set; }
        public AppraisalType Type { get; set; }

        public string? RankingBand { get; set; }

        public ICollection<AppraisalKPI> AppraisalKPIs { get; set; } = new List<AppraisalKPI>();
        public int StatusId { get; set; }
        public Lookup Status { get; set; } = null!;

        public int? ReviewerId { get; set; }
        public Employee? Reviewer { get; set; }

       
        public DateTime? ReviewedOn { get; set; }
        // Section 6: Performance Appraisal Ranking (Supervisor only, hidden from employee)
        public string? RecommendationText { get; set; }
        public string? RecommendedRank { get; set; }   // OS, AE, ME, BE, NI

        // Section 7: Final Ranking (HR + Final Reviewer)
     
        public string? FinalRank { get; set; }         // OS, AE, ME, BE, NI
        public string? ActionRequired { get; set; }
        public bool SelfAssessmentEnabled { get; set; } = false;
        public ICollection<AppraisalHistory> History { get; set; } = new List<AppraisalHistory>();
    }
}
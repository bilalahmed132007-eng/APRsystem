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

        public string? GeneralComment { get; set; }

        // Posting-Specific KPI section
        [Column(TypeName = "decimal(6,2)")]
        public decimal SpecificTotalScore { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal SpecificMaxScore { get; set; }

        public string? SpecificComment { get; set; }

        // Combined
        [Column(TypeName = "decimal(6,2)")]
        public decimal GrandTotalScore { get; set; }

        [Column(TypeName = "decimal(6,2)")]
        public decimal GrandMaxScore { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal Percentage { get; set; }

        public string? RankingBand { get; set; }

        public ICollection<AppraisalKPI> AppraisalKPIs { get; set; } = new List<AppraisalKPI>();
    }
}
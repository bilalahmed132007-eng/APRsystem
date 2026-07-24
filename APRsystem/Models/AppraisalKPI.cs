using System.ComponentModel.DataAnnotations.Schema;

namespace APRsystem.Models
{
    public enum KPISection
    {
        General,
        Specific
    }

    public class AppraisalKPI
    {
        public int Id { get; set; }

        public int AppraisalId { get; set; }
        public Appraisal Appraisal { get; set; } = null!;

        public KPISection Section { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal Weight { get; set; }

        // Rating: 0-4, entered by supervisor
        public int Rating { get; set; }

        // Score = Weight * Rating (calculated, stored for convenience)
        [Column(TypeName = "decimal(6,2)")]
        public decimal Score { get; set; }
    }
}
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

        // Employee's self-assessment rating: 0-4. Entered by the employee during SelfAssessment.
        // Never overwritten by the supervisor.
        public int SelfRating { get; set; }

        // SelfScore = Weight * SelfRating (calculated, stored for convenience)
        [Column(TypeName = "decimal(6,2)")]
        public decimal SelfScore { get; set; }

        // Official rating: 0-4, entered by the supervisor. This is what totals/percentage/rank are
        // calculated from.
        public int Rating { get; set; }

        // Score = Weight * Rating (calculated, stored for convenience)
        [Column(TypeName = "decimal(6,2)")]
        public decimal Score { get; set; }
    }
}
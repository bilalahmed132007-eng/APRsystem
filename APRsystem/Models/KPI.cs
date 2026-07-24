using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace APRsystem.Models
{
    public class KPI
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "KPI Title")]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Range(0, 100)]
        [Column(TypeName = "decimal(5,2)")]
        public decimal Weight { get; set; }

        // True = General KPI
        // False = Posting-Specific KPI
        public bool IsGeneral { get; set; }

        // Null for General KPIs
     
        
    }
}
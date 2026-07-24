using System.ComponentModel.DataAnnotations;

namespace APRsystem.ViewModels
{
    public class PostingKPICreateViewModel
    {
        public int PostingId { get; set; }

        [Required]
        [Display(Name = "KPI Title")]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [Range(0, 100)]
        public decimal Weight { get; set; }
    }
}
using System.ComponentModel.DataAnnotations;

namespace APRsystem.Models
{
    public class Lookup
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string Value { get; set; } = string.Empty;

        [Required]
        [StringLength(150)]
        public string Label { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
    }
}
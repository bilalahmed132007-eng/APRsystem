using System.ComponentModel.DataAnnotations;

namespace APRsystem.Models
{
    public class Department
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty; // e.g. "HR", "FIN", "IT"

        public bool IsActive { get; set; } = true;

        public ICollection<Posting> Postings { get; set; } = new List<Posting>();
    }
}
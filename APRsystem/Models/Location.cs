using System.ComponentModel.DataAnnotations;

namespace APRsystem.Models
{
    public class Location
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(250)]
        public string? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Posting> Postings { get; set; } = new List<Posting>();
    }
}
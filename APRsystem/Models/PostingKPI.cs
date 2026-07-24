namespace APRsystem.Models
{
    public class PostingKPI
    {
        public int Id { get; set; }

        public int PostingId { get; set; }
        public Posting Posting { get; set; } = null!;

        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Weight { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
using System.ComponentModel.DataAnnotations;

namespace APRsystem.Models
{
    public class AuditLog
    {
        public int Id { get; set; }

        // Who
        public string? UserId { get; set; }
        public string? UserName { get; set; }

        // What
        [Required]
        public string Action { get; set; } = null!; // "Created", "Updated", "Deleted"

        [Required]
        public string EntityName { get; set; } = null!; // e.g. "Posting", "KPI"

        public string? RecordId { get; set; } // stored as string to support any key type

        // When
        public DateTime Timestamp { get; set; }

        // Change details (JSON-serialized)
        public string? OldValues { get; set; }
        public string? NewValues { get; set; }

        // Comma-separated list of changed column names, for quick display in the Index list
        public string? ChangedColumns { get; set; }
    }
}
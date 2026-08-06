using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace APRsystem.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        public int RecipientEmployeeId { get; set; }

        [ValidateNever]
        public Employee Recipient { get; set; } = null!;

        [Required]
        public string Message { get; set; } = string.Empty;

        // Where clicking the notification should take the user, e.g. Postings/Details/5#specific-kpis
        public string? Url { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
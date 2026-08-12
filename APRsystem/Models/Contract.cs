using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace APRsystem.Models
{
    public enum ContractType
    {
        Permanent,
        Contract,
        Temporary,
        Internship
    }

    public class Contract
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [ValidateNever]
        public Employee Employee { get; set; } = null!;

        [Required]
        public string ContractNumber { get; set; } = string.Empty;

        [Required]
        public ContractType Type { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        // Nullable because Permanent contracts have no fixed end date
        public DateTime? EndDate { get; set; }
        public static ValidationResult? ValidateEndDate(
    DateTime? endDate,
    ValidationContext context)
        {
            if (!endDate.HasValue)
                return ValidationResult.Success;

            var contract = (Contract)context.ObjectInstance;

            if (endDate.Value <= contract.StartDate)
                return new ValidationResult("End date must be after start date.");

            return ValidationResult.Success;
        }

        public bool IsActive { get; set; } = true;

        public ICollection<Posting> Postings { get; set; } = new List<Posting>();
    }
}
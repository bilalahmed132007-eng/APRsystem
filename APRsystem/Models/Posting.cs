using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace APRsystem.Models
{
    public class Posting
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [ValidateNever]
        public Employee Employee { get; set; } = null!;

        [Required]
        public int ContractId { get; set; }

        [ValidateNever]
        public Contract Contract { get; set; } = null!;

        [Required]
        public int DepartmentId { get; set; }

        [ValidateNever]
        public Department Department { get; set; } = null!;

        [Required]
        public int DesignationId { get; set; }

        [ValidateNever]
        public Lookup Designation { get; set; } = null!;
        public ICollection<PostingKPI> PostingKPIs { get; set; } = new List<PostingKPI>();

        public int? SupervisorId { get; set; }

        [ValidateNever]
        public Employee? Supervisor { get; set; }

        [Required]
        [Range(0, double.MaxValue)]
        public decimal Salary { get; set; }

        [Required]
        public int LocationId { get; set; }

        [ValidateNever]
        public Location Location { get; set; } = null!;

        [Required]
        [Display(Name = "From Date")]
        public DateTime FromDate { get; set; }

        [Display(Name = "To Date")]
        public DateTime? ToDate { get; set; }

        public bool IsActive => ToDate == null;
    }
}
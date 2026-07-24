using System.ComponentModel.DataAnnotations;
using APRsystem.Models;

namespace APRsystem.ViewModels
{
    public class CreatePostingViewModel
    {
        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int ContractId { get; set; }

        [Required]
        public int DepartmentId { get; set; }

        [Required]
        public int DesignationId { get; set; }

        public int? SupervisorId { get; set; }

        [Required]
        public decimal Salary { get; set; }

        [Required]
        public int LocationId { get; set; }

        [Required]
        [Display(Name = "From Date")]
        public DateTime FromDate { get; set; }

        [Display(Name = "To Date")]
        public DateTime? ToDate { get; set; }

        // 👇 Only used during creation, not stored in the Posting table
        [Display(Name = "Keep Posting-Specific KPIs from Previous Posting")]
        public bool KeepPreviousPostingKPIs { get; set; }
    }
}
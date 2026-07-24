using System.ComponentModel.DataAnnotations;
using APRsystem.Models;

namespace APRsystem.ViewModels
{
    public class CreateEmployeeViewModel
    {
        [Required]
        public string EmployeeNo { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string Phone { get; set; } = string.Empty;

        [Required]
        public string CNIC { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Joining Date")]
        public DateTime JoiningDate { get; set; }

        public bool IsActive { get; set; } = true;

        [Required]
        [Display(Name = "User Role")]
        public string SelectedRole { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare("Password")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        // ---- Contract fields ----

        [Required]
        [Display(Name = "Contract Number")]
        public string ContractNumber { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Contract Type")]
        public ContractType ContractType { get; set; }

        [Required]
        [Display(Name = "Contract Start Date")]
        public DateTime ContractStartDate { get; set; }

        [Display(Name = "Contract End Date")]
        public DateTime? ContractEndDate { get; set; }

        // ---- Posting fields ----

        [Required]
        [Display(Name = "Department")]
        public int DepartmentId { get; set; }

        [Required]
        [Display(Name = "Designation")]
        public int DesignationId { get; set; }

        [Required]
        [Display(Name = "Location")]
        public int LocationId { get; set; }

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Salary must be a positive value.")]
        public decimal Salary { get; set; }

        [Required]
        [Display(Name = "Posting From Date")]
        public DateTime PostingFromDate { get; set; }

        public int? SupervisorId { get; set; }
        public List<KPI> KPIs { get; set; } = new();
    }
}
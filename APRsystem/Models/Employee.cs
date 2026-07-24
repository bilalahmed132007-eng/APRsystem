using APRsystem.Models.Identity;

namespace APRsystem.Models
{
    public class Employee
    {
        public int Id { get; set; }

        public string EmployeeNo { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string CNIC { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        public DateTime JoiningDate { get; set; }
        public bool IsActive { get; set; } = true;

        public int? SupervisorId { get; set; }
        public Employee? Supervisor { get; set; }

        public string ApplicationUserId { get; set; } = string.Empty;
        public ApplicationUser ApplicationUser { get; set; } = null!;

        public ICollection<Posting> Postings { get; set; } = new List<Posting>();
        public ICollection<Contract> Contracts { get; set; } = new List<Contract>();
        
    }
}
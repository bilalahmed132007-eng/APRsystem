using APRsystem.Models;   // ✅ so it can see Employee and Posting

namespace APRsystem.ViewModels
{
    public class EmployeeDetailsViewModel
    {
        public Employee Employee { get; set; } = null!;

        public Posting? CurrentPosting { get; set; }

        public Employee? Supervisor { get; set; }

        public List<Employee> Teammates { get; set; } = new();

        public List<Employee> DirectReports { get; set; } = new();
        public Employee? GrandSupervisor { get; set; }
        public bool CanViewPostings { get; set; }

    }
}
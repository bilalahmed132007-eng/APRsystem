using APRsystem.Models;

namespace APRsystem.ViewModels
{
    // Generic "team tree" shape — supervisor, teammates (same supervisor), and direct reports —
    // centered on whichever employee is passed in as CurrentEmployee.
    // Reused on the Dashboard (centered on the logged-in user) and on Employee Details
    // (centered on whichever employee is being viewed).
    public class TeamTreeViewModel
    {
        public Employee? CurrentEmployee { get; set; }
        public Employee? TeamSupervisor { get; set; }
        public List<Employee> Teammates { get; set; } = new();
        public List<Employee> DirectReports { get; set; } = new();
        public Employee? GrandSupervisor { get; set; }
        public List<TeamNode> Subordinates { get; set; } = new();
    }
}
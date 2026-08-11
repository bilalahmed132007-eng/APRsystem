using APRsystem.Models;

namespace APRsystem.ViewModels
{
    // One node in a supervisor's full subordinate subtree — the employee plus
    // however many levels of their own reports go down from there.
    public class TeamNode
    {
        public Employee Employee { get; set; } = null!;
        public List<TeamNode> Children { get; set; } = new();
    }
}
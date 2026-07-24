using APRsystem.Models;

namespace APRsystem.ViewModels
{
    public class PostingDetailsViewModel
    {
        public Posting Posting { get; set; } = null!;

        public List<KPI> GeneralKPIs { get; set; } = new();

        public List<PostingKPI> AssignedKPIs { get; set; } = new();
    }
}
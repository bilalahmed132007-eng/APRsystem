using APRsystem.Models;

namespace APRsystem.ViewModels
{
    public class CreateAppraisalViewModel
    {
        public int EmployeeId { get; set; }
        public int PostingId { get; set; }
        public int SupervisorId { get; set; }

        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public AppraisalType Type { get; set; }
    }
}
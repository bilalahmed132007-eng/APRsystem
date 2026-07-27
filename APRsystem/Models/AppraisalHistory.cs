namespace APRsystem.Models
{
    public class AppraisalHistory
    {
        public int Id { get; set; }

        public int AppraisalId { get; set; }
        public Appraisal Appraisal { get; set; } = null!;

        public string Comments { get; set; } = string.Empty;

        public string ActionByRole { get; set; } = string.Empty;   // Supervisor, Employee, Reviewer, HR
        public string ActionByName { get; set; } = string.Empty;   // e.g. "Ali Khan"

        public int StageId { get; set; }
        public Lookup Stage { get; set; } = null!;

        public int? NextStageId { get; set; }
        public Lookup? NextStage { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
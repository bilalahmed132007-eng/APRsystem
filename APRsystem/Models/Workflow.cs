using System.ComponentModel.DataAnnotations.Schema;

namespace APRsystem.Models
{
    public class Workflow
    {
        public int Id { get; set; }

        public string Entity { get; set; } = string.Empty; // e.g. "Appraisal"

        public int CurrentStateId { get; set; }
        [ForeignKey("CurrentStateId")]
        public Lookup CurrentState { get; set; } = null!;

        public string Action { get; set; } = string.Empty; // e.g. "Appraisal Review"

        public int NextStateId { get; set; }
        [ForeignKey("NextStateId")]
        public Lookup NextState { get; set; } = null!;

        public bool IsCommentMandatory { get; set; } = false;

        public string? CrudPermission { get; set; }

        public bool IsActive { get; set; } = true;

        public string? Icon { get; set; }
    }
}
using APRsystem.Data;
using APRsystem.Models;
using Microsoft.EntityFrameworkCore;

namespace APRsystem.Services
{
    /// <summary>
    /// Thrown when a requested workflow transition isn't defined (or isn't active)
    /// in the Workflows table, or when a mandatory comment is missing.
    /// Controllers should catch this and return BadRequest(ex.Message).
    /// </summary>
    public class WorkflowValidationException : Exception
    {
        public WorkflowValidationException(string message) : base(message) { }
    }

    /// <summary>
    /// Single source of truth for entity state transitions, driven by the Workflows table.
    /// Does NOT handle authorization — that stays in the controller, since "Employee"/"Supervisor"
    /// in CrudPermission are relationship-based (e.g. "this appraisal's supervisor"), not
    /// ASP.NET Identity roles like "HR"/"Admin" are.
    /// </summary>
    public class WorkflowService
    {
        private readonly ApplicationDbContext _context;

        public WorkflowService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Looks up the active Workflows row for entity + current state + action.
        /// Throws WorkflowValidationException if no such transition is defined.
        /// </summary>
        public async Task<Workflow> GetTransitionAsync(string entity, int currentStateId, string action)
        {
            var wf = await _context.Workflows
                .FirstOrDefaultAsync(w =>
                    w.Entity == entity &&
                    w.CurrentStateId == currentStateId &&
                    w.Action == action &&
                    w.IsActive);

            if (wf == null)
            {
                throw new WorkflowValidationException(
                    $"No active workflow transition found for entity '{entity}', state {currentStateId}, action '{action}'. " +
                    "Check the Workflows table — the row may be missing, inactive, or the state/action doesn't match.");
            }

            return wf;
        }

        /// <summary>
        /// Looks ahead from a state to see what comes next, purely for display (e.g. an "Anticipated Next Stage"
        /// column in a history log). Returns the single NextStateId if there's exactly one active outgoing
        /// transition from this state. Returns null if there are zero (terminal state) or more than one
        /// (branches — e.g. approve vs. reject — so the real next stage isn't knowable until someone acts).
        /// </summary>
        public async Task<int?> GetSoleNextStateAsync(string entity, int stateId)
        {
            var outgoing = await _context.Workflows
                .Where(w => w.Entity == entity && w.CurrentStateId == stateId && w.IsActive)
                .Select(w => w.NextStateId)
                .Distinct()
                .ToListAsync();

            return outgoing.Count == 1 ? outgoing[0] : (int?)null;
        }

        /// <summary>
        /// Throws if the workflow row requires a comment but none of the supplied values are non-empty.
        /// Pass every comment/remarks field relevant to this action.
        /// </summary>
        public void EnsureCommentProvided(Workflow wf, params string?[] comments)
        {
            if (!wf.IsCommentMandatory)
                return;

            if (comments.All(string.IsNullOrWhiteSpace))
            {
                throw new WorkflowValidationException(
                    $"A comment is required to perform '{wf.Action}'.");
            }
        }
    }
}
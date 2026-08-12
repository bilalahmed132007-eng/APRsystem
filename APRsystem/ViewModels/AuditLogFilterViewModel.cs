using APRsystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace APRsystem.ViewModels
{
    public class AuditLogFilterViewModel
    {
        public string? UserName { get; set; }

        [FromQuery(Name = "Action")]
        public string? Action { get; set; }

        public string? EntityName { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

        public List<AuditLog> Results { get; set; } = new();

        public List<SelectListItem> ActionOptions { get; set; } = new();
        public List<SelectListItem> EntityOptions { get; set; } = new();
        public bool Searched { get; set; }
    }
}
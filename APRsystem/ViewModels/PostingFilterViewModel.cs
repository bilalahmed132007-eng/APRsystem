using APRsystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace APRsystem.ViewModels
{
    public class PostingFilterViewModel
    {
        public int? EmployeeId { get; set; }
        public int? DepartmentId { get; set; }
        public string? EmployeeName { get; set; }

        public List<SelectListItem> EmployeeOptions { get; set; } = new();
        public List<SelectListItem> DepartmentOptions { get; set; } = new();

        public List<Posting> Results { get; set; } = new();
    }
}
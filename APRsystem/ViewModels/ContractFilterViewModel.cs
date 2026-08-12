using APRsystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace APRsystem.ViewModels
{
    public class ContractFilterViewModel
    {
        public string? EmployeeName { get; set; }
        public ContractType? Type { get; set; }
        public string? Status { get; set; } // "Active" | "Expired" | "Inactive"

        public List<SelectListItem> TypeOptions { get; set; } = new();
        public List<SelectListItem> StatusOptions { get; set; } = new();

        public List<Contract> Results { get; set; } = new();
    }
}
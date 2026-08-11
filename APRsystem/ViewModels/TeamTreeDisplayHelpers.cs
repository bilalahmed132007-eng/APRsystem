using APRsystem.Models;

namespace APRsystem.ViewModels
{
    public static class TeamTreeDisplayHelpers
    {
        public static string InitialsOf(Employee e)
        {
            var parts = (e.FullName ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0) return "?";
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpper();
            return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
        }

        public static string DesignationOf(Employee e)
        {
            var posting = e.Postings?.FirstOrDefault(p => p.ToDate == null);
            return posting?.Designation?.Label ?? "";
        }
    }
}
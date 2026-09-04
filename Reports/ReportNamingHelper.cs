namespace PayrollManagement.Reports
{
    public static class ReportNamingHelper
    {
        public static string FilterSummary(ReportFilters filters, string? reportType = null)
        {
            if (!string.IsNullOrEmpty(filters.FromDate) && !string.IsNullOrEmpty(filters.ToDate))
                return $"Date: {DisplayDate(filters.FromDate)} - {DisplayDate(filters.ToDate)}";

            if (reportType == "monthly" && !string.IsNullOrEmpty(filters.Month))
            {
                if (DateTime.TryParseExact(filters.Month, "yyyy-MM", null,
                        System.Globalization.DateTimeStyles.None, out var dt))
                    return $"Month: {dt:MMMM - yyyy}";
                return $"Month: {filters.Month}";
            }

            if (!string.IsNullOrEmpty(filters.Date))
                return $"Date: {DisplayDate(filters.Date)}";
            return "All records";
        }

        public static string DisplayDate(string dateValue)
        {
            if (DateTime.TryParseExact(dateValue, "yyyy-MM-dd", null,
                    System.Globalization.DateTimeStyles.None, out var dt))
                return dt.ToString("dd/MM/yyyy");
            return dateValue ?? "";
        }

        public static string SanitizeFilenameValue(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return System.Text.RegularExpressions.Regex.Replace(value.Trim(), "[<>:\"/\\\\|?*]", "");
        }

        public static string ExportDefaultFilename(string reportTitle, ReportFilters filters)
        {
            var parts = new List<string> { SanitizeFilenameValue(reportTitle) };

            if (!string.IsNullOrEmpty(filters.EmpId))
                parts.Add($"ID={SanitizeFilenameValue(filters.EmpId)}");

            foreach (var (label, value) in new[]
                     { ("Section", filters.Section), ("Designation", filters.Designation),
                       ("Category", filters.Category), ("Shift", filters.Shift) })
            {
                if (value != "All" && !string.IsNullOrEmpty(value))
                    parts.Add($"{label}={SanitizeFilenameValue(value)}");
            }

            if (reportTitle == "Late Report") parts.Add($"LateMoreThan={filters.GraceMinutes}min");
            else if (reportTitle == "Early Report") parts.Add($"Grace={filters.GraceMinutes}min");

            if (!string.IsNullOrEmpty(filters.FromDate) && !string.IsNullOrEmpty(filters.ToDate))
                parts.Add($"Date={SanitizeFilenameValue(filters.FromDate)}_to_{SanitizeFilenameValue(filters.ToDate)}");
            else if (!string.IsNullOrEmpty(filters.Date))
                parts.Add($"Date={SanitizeFilenameValue(filters.Date)}");
            else if (!string.IsNullOrEmpty(filters.Month))
                parts.Add($"Month={SanitizeFilenameValue(filters.Month)}");

            if (parts.Count == 1) parts.Add("All");
            return string.Join(" - ", parts);
        }
    }
}

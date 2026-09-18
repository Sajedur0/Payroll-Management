using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.Reports
{
    public static class ReportQueryBuilder
    {
        private static readonly HashSet<string> AllowedFilterFields =
            new(StringComparer.OrdinalIgnoreCase) { "Section", "Designation", "Category", "Shift", "Department" };

        public static List<string> FilteredValues(string fieldName, ReportFilters filters)
        {
            if (!AllowedFilterFields.Contains(fieldName)) return new();
            if (filters == null) return new();

            var where = new List<string> { $"{fieldName} IS NOT NULL", $"LTRIM(RTRIM({fieldName})) != ''" };
            var parameters = new List<(string, object)>();
            var map = new (string col, string val)[]
            {
                ("Section", filters.Section), ("Designation", filters.Designation),
                ("Category", filters.Category), ("Shift", filters.Shift),
            };
            int i = 0;
            foreach (var (col, val) in map)
            {
                if (string.Equals(col, fieldName, StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrWhiteSpace(val) || val == "All") continue;
                where.Add($"{col} = @p{i}");
                parameters.Add(($"@p{i}", val.Trim()));
                i++;
            }

            var sql = $@"SELECT DISTINCT {fieldName} FROM dbo.EmployeeInfo
                         WHERE {string.Join(" AND ", where)} ORDER BY {fieldName}";
            try
            {
                return DbHelper.FetchRows(sql, parameters.ToArray())
                    .Select(r => r[0]?.ToString()?.Trim() ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct()
                    .ToList();
            }
            catch { return new List<string>(); }
        }

        public static (List<string> where, List<(string, object)> parameters) EmployeeFilterSql(
            ReportFilters filters, string alias = "e")
        {
            var where = new List<string> { "1 = 1" };
            var parameters = new List<(string, object)>();
            int i = 0;

            if (!string.IsNullOrEmpty(filters.EmpId))
            {
                where.Add($"{alias}.EmpID = @p{i}");
                parameters.Add(($"@p{i}", filters.EmpId.Trim()));
                i++;
            }
            var fields = new (string col, string val)[]
            {
                ("Section", filters.Section), ("Designation", filters.Designation),
                ("Category", filters.Category), ("Shift", filters.Shift),
            };
            foreach (var (col, val) in fields)
            {
                if (string.IsNullOrWhiteSpace(val) || val == "All") continue;
                where.Add($"{alias}.{col} = @p{i}");
                parameters.Add(($"@p{i}", val.Trim()));
                i++;
            }
            return (where, parameters);
        }

        public static List<string> DateRangeValues(string startDate, string endDate)
        {
            var start = ShiftLogic.ParseDate(startDate);
            var end = ShiftLogic.ParseDate(endDate);

            // Handle inverted date range gracefully
            if (start > end)
            {
                var temp = start;
                start = end;
                end = temp;
            }

            var dates = new List<string>();
            for (var d = start; d <= end; d = d.AddDays(1))
                dates.Add(d.ToString("yyyy-MM-dd"));
            return dates;
        }
    }
}

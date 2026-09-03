using PayrollManagement.Data;

namespace PayrollManagement.Reports
{
    public static class AbsentReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var columns = new List<string> { "EmpID", "Name", "Section", "Designation", "Category", "Date" };
            var rows = new List<object?[]>();
            var nonAbsentDays = CalendarHelper.NonAbsentDays();
            var holidayMap = CalendarHelper.CompanyHolidayMap(filters.FromDate, filters.ToDate);

            List<string> dates;
            try { dates = ReportQueryBuilder.DateRangeValues(filters.FromDate, filters.ToDate); }
            catch { return new ReportResult { Columns = columns, Rows = rows }; }

            foreach (var dateValue in dates)
            {
                if (holidayMap.ContainsKey(dateValue) || CalendarHelper.IsNonAbsentDate(dateValue, nonAbsentDays))
                    continue;

                var query = $@"
                    SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Category, @dv
                    FROM EmployeeInfo e
                    LEFT JOIN RawData r ON r.EmpID = e.EmpID AND r.Date = @dv2
                    WHERE {string.Join(" AND ", employeeWhere)}
                      AND {EmployeeExitHelper.EmployeeActiveOnDateSql("e", "@dv3")}
                      AND r.LogID IS NULL
                    ORDER BY e.EmpID";

                var parameters = employeeParams
                    .Append(("@dv", (object)dateValue))
                    .Append(("@dv2", (object)dateValue))
                    .Append(("@dv3", (object)dateValue)).ToArray();

                try { rows.AddRange(DbHelper.FetchRows(query, parameters)); }
                catch { }
            }
            return new ReportResult { Columns = columns, Rows = rows };
        }
    }
}

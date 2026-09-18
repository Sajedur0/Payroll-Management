using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.Reports
{
    public enum LateEarlyType { Late, Early }

    public static class LateEarlyReport
    {
        public static ReportResult Fetch(LateEarlyType type, ReportFilters filters)
        {
            var accessColumn = type == LateEarlyType.Late ? "Access" : "Allowed In";
            var columns = new List<string>
                { "EmpID", "Name", "Section", "Designation", "Date", "Shift", accessColumn, "In Time", "Out Time", "Status" };
            var rows = new List<object?[]>();

            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var where = new List<string>(employeeWhere);
            var parameters = new List<(string, object)>(employeeParams);
            int i = employeeParams.Count;

            string fromDate = filters.FromDate;
            string toDate = filters.ToDate;
            if (!string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
            {
                if (string.CompareOrdinal(fromDate, toDate) > 0)
                {
                    var temp = fromDate;
                    fromDate = toDate;
                    toDate = temp;
                }
                where.Add($"r.Date BETWEEN @p{i} AND @p{i + 1}");
                parameters.Add(($"@p{i}", fromDate)); parameters.Add(($"@p{i + 1}", toDate));
                i += 2;
            }
            else if (!string.IsNullOrEmpty(filters.Date))
            {
                where.Add($"r.Date = @p{i}");
                parameters.Add(($"@p{i}", filters.Date));
                i++;
            }
            where.Add(EmployeeExitHelper.EmployeeActiveOnDateSql("e", "r.Date"));

            var query = $@"
                SELECT e.EmpID, e.Name, e.Section, e.Designation,
                       r.Date, e.Shift, r.InTime, r.OutTime
                FROM dbo.RawData r JOIN dbo.EmployeeInfo e ON e.EmpID = r.EmpID
                WHERE {string.Join(" AND ", where)}
                ORDER BY r.Date, e.EmpID";

            int graceMinutes = filters.GraceMinutes;
            List<object[]> data;
            try { data = DbHelper.FetchRows(query, parameters.ToArray()); }
            catch { return new ReportResult { Columns = columns, Rows = rows }; }

            foreach (var r in data)
            {
                if (r[0] == null || r[4] == null) continue;
                var empId = r[0]; var name = r[1]; var section = r[2]; var designation = r[3];
                var dateValue = r[4].ToString()!; var shiftName = r[5] as string;
                var inTimeRaw = r[6] as string; var outTimeRaw = r[7] as string;
                if (string.IsNullOrEmpty(inTimeRaw)) continue;

                TimeSpan actualIn;
                try { actualIn = ShiftLogic.ParseTime(inTimeRaw); } catch { continue; }

                var expectedIn = ShiftExpectationHelper.GetExpectedShiftInTime(shiftName, dateValue);
                if (expectedIn is null) continue;

                var reportDate = ShiftLogic.ParseDate(dateValue);
                var expectedDt = reportDate + expectedIn.Value;
                var thresholdDt = type == LateEarlyType.Early
                    ? expectedDt.AddMinutes(-graceMinutes)
                    : expectedDt;
                var thresholdTime = thresholdDt.TimeOfDay;

                var actualInDt = reportDate + actualIn;
                // If expected shift is in evening/night (>= 12:00) and actual punch is in early morning (< 12:00),
                // it represents an employee arriving after midnight for their night shift
                if (expectedIn.Value.Hours >= 12 && actualIn.Hours < 12)
                {
                    actualInDt = reportDate.AddDays(1) + actualIn;
                }

                string? status = null;
                if (type == LateEarlyType.Late && actualInDt > expectedDt)
                {
                    var delta = actualInDt - expectedDt;
                    if (delta <= TimeSpan.FromMinutes(graceMinutes)) continue;
                    status = ReportTimeHelper.FormatDuration(delta);
                }
                else if (type == LateEarlyType.Early && actualInDt < thresholdDt)
                {
                    bool thresholdIsPm = thresholdTime.Hours >= 12;
                    bool actualIsPm = actualIn.Hours >= 12;
                    if (thresholdIsPm && !actualIsPm) continue;
                    var delta = thresholdDt - actualInDt;
                    status = ReportTimeHelper.FormatDuration(delta);
                }
                if (status is null) continue;

                rows.Add(new object?[]
                {
                    empId, name, section, designation, dateValue, shiftName ?? "",
                    ReportTimeHelper.FormatShiftTime(thresholdTime.ToString(@"hh\:mm\:ss")),
                    ReportTimeHelper.FormatReportTime(inTimeRaw),
                    ReportTimeHelper.FormatReportTime(outTimeRaw),
                    status,
                });
            }
            return new ReportResult { Columns = columns, Rows = rows };
        }
    }
}

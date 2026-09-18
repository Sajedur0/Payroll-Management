using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.Reports
{
    public static class MonthlySummaryReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            string monthStr = filters.Month;
            if (string.IsNullOrWhiteSpace(monthStr))
            {
                if (!string.IsNullOrWhiteSpace(filters.FromDate))
                    monthStr = filters.FromDate.Length >= 7 ? filters.FromDate.Substring(0, 7) : DateTime.Today.ToString("yyyy-MM");
                else
                    monthStr = DateTime.Today.ToString("yyyy-MM");
            }

            int year = DateTime.Today.Year;
            int month = DateTime.Today.Month;
            var parts = monthStr.Split('-');
            if (parts.Length >= 2 && int.TryParse(parts[0], out int y) && int.TryParse(parts[1], out int m))
            {
                year = y;
                month = m;
            }

            if (month < 1 || month > 12) month = DateTime.Today.Month;
            int lastDay = DateTime.DaysInMonth(year, month);

            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var employeeQuery = $@"SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Shift
                                    FROM dbo.EmployeeInfo e WHERE {string.Join(" AND ", employeeWhere)}
                                    ORDER BY e.EmpID";
            List<object[]> employees;
            try { employees = DbHelper.FetchRows(employeeQuery, employeeParams.ToArray()); }
            catch { employees = new List<object[]>(); }

            var empShiftCache = new Dictionary<int, string>();
            foreach (var emp in employees)
            {
                if (emp[0] == null) continue;
                int empId = Convert.ToInt32(emp[0]);
                empShiftCache[empId] = emp.Length > 4 && emp[4] != null ? (emp[4].ToString() ?? "").Trim() : "";
            }

            var startDate = $"{year:D4}-{month:D2}-01";
            var endDate = $"{year:D4}-{month:D2}-{lastDay:D2}";
            var holidayMap = CalendarHelper.CompanyHolidayMap(startDate, endDate);

            var attendanceQuery = $@"
                SELECT e.EmpID, r.Date FROM dbo.RawData r
                JOIN dbo.EmployeeInfo e ON e.EmpID = r.EmpID
                WHERE {string.Join(" AND ", employeeWhere)}
                  AND {EmployeeExitHelper.EmployeeActiveOnDateSql("e", "r.Date")}
                  AND r.Date BETWEEN @dStart AND @dEnd";
            List<object[]> attendanceRows;
            try
            {
                attendanceRows = DbHelper.FetchRows(attendanceQuery,
                    employeeParams.Append(("@dStart", (object)startDate)).Append(("@dEnd", (object)endDate)).ToArray());
            }
            catch { attendanceRows = new List<object[]>(); }

            var presentDates = new Dictionary<int, HashSet<string>>();
            foreach (var r in attendanceRows)
            {
                if (r[0] == null || r[1] == null) continue;
                var empId = Convert.ToInt32(r[0]);
                if (!presentDates.TryGetValue(empId, out var set))
                    presentDates[empId] = set = new HashSet<string>();
                set.Add(r[1].ToString()!);
            }

            var weekendDays = CalendarHelper.WeekendDays();
            var nonAbsentDays = CalendarHelper.NonAbsentDays();
            var dayColumns = Enumerable.Range(1, lastDay).Select(d => d.ToString("D2")).ToList();
            var columns = new List<string> { "EmpID", "Name", "Section", "Designation" }
                .Concat(dayColumns).Concat(new[] { "Present", "WL", "Total P", "Total A" }).ToList();

            var exitDates = EmployeeExitHelper.GetEmployeeExitDateMap();
            var rows = new List<object?[]>();

            foreach (var emp in employees)
            {
                if (emp[0] == null) continue;
                var empId = Convert.ToInt32(emp[0]);
                var name = emp[1]; var section = emp[2]; var designation = emp[3];
                var empPresentDates = presentDates.TryGetValue(empId, out var s) ? s : new HashSet<string>();

                var marks = new List<string>();
                int presentCount = 0, weekendCount = 0, activeDayCount = 0;

                for (int day = 1; day <= lastDay; day++)
                {
                    var dateValue = $"{year:D4}-{month:D2}-{day:D2}";
                    if (!EmployeeExitHelper.IsEmployeeActiveOn(exitDates, empId, dateValue))
                    {
                        marks.Add("");
                        continue;
                    }
                    activeDayCount++;
                    var dayName = ShiftLogic.ParseDate(dateValue).DayOfWeek.ToString();
                    string mark;

                    if (holidayMap.ContainsKey(dateValue))
                    {
                        mark = "P";
                    }
                    else if (nonAbsentDays.Contains(dayName))
                    {
                        if (dayName == "Friday")
                        {
                            var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(empId, dateValue, empShiftCache);
                            if (dutyType == "Night") { mark = "WL"; weekendCount++; }
                            else if (empPresentDates.Contains(dateValue)) mark = "WP";
                            else { mark = "WL"; weekendCount++; }
                        }
                        else
                        {
                            if (weekendDays.Contains(dayName)) weekendCount++;
                            mark = "WL";
                        }
                    }
                    else if (empPresentDates.Contains(dateValue))
                    {
                        mark = "P";
                    }
                    else
                    {
                        mark = "A";
                    }

                    if (mark is "P" or "WP") presentCount++;
                    marks.Add(mark);
                }

                if (activeDayCount == 0) continue;
                int totalAbsent = marks.Count(m => m == "A");

                var row = new List<object?> { empId, name, section, designation };
                row.AddRange(marks);
                row.Add(presentCount); row.Add(weekendCount);
                row.Add(weekendCount + presentCount); row.Add(totalAbsent);
                rows.Add(row.ToArray());
            }

            return new ReportResult { Columns = columns, Rows = rows };
        }
    }
}

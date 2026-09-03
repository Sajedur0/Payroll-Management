using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.Reports
{
    public static class MonthlySummaryReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            var parts = filters.Month.Split('-');
            int year = int.Parse(parts[0]), month = int.Parse(parts[1]);
            int lastDay = DateTime.DaysInMonth(year, month);

            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var employeeQuery = $@"SELECT e.EmpID, e.Name, e.Section, e.Designation
                                    FROM EmployeeInfo e WHERE {string.Join(" AND ", employeeWhere)}
                                    ORDER BY e.EmpID";
            List<object[]> employees;
            try { employees = DbHelper.FetchRows(employeeQuery, employeeParams.ToArray()); }
            catch { employees = new List<object[]>(); }

            var startDate = $"{year:D4}-{month:D2}-01";
            var endDate = $"{year:D4}-{month:D2}-{lastDay:D2}";
            var holidayMap = CalendarHelper.CompanyHolidayMap(startDate, endDate);

            var attendanceQuery = $@"
                SELECT e.EmpID, r.Date FROM RawData r
                JOIN EmployeeInfo e ON e.EmpID = r.EmpID
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
                var empId = Convert.ToInt32(r[0]);
                if (!presentDates.TryGetValue(empId, out var set))
                    presentDates[empId] = set = new HashSet<string>();
                set.Add(r[1]!.ToString()!);
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
                            var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(empId, dateValue);
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

using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.Reports
{
    public static class AttendanceReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var exitDates = EmployeeExitHelper.GetEmployeeExitDateMap();

            string startDate, endDate;
            if (!string.IsNullOrEmpty(filters.FromDate) && !string.IsNullOrEmpty(filters.ToDate))
            {
                startDate = filters.FromDate; endDate = filters.ToDate;
            }
            else if (!string.IsNullOrEmpty(filters.Date))
            {
                startDate = endDate = filters.Date;
            }
            else
            {
                (startDate, endDate) = AttendanceDateBounds();
            }

            if (string.CompareOrdinal(startDate, endDate) > 0)
            {
                var temp = startDate;
                startDate = endDate;
                endDate = temp;
            }

            var holidayMap = CalendarHelper.CompanyHolidayMap(startDate, endDate);
            var columns = new List<string> { "EmpID", "Name", "Section", "Designation", "Category", "Date", "In Time", "Out Time" };
            var rows = new List<object?[]>();

            var empQuery = $@"SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Category, e.Shift
                               FROM dbo.EmployeeInfo e WHERE {string.Join(" AND ", employeeWhere)}
                               ORDER BY e.EmpID";
            List<object[]> employees;
            try { employees = DbHelper.FetchRows(empQuery, employeeParams.ToArray()); }
            catch { employees = new List<object[]>(); }

            var empShiftCache = new Dictionary<int, string>();
            foreach (var emp in employees)
            {
                if (emp[0] == null) continue;
                int empId = Convert.ToInt32(emp[0]);
                empShiftCache[empId] = emp.Length > 5 && emp[5] != null ? (emp[5].ToString() ?? "").Trim() : "";
            }

            var extendedEnd = ShiftLogic.ParseDate(endDate).AddDays(1).ToString("yyyy-MM-dd");

            var attendanceQuery = $@"
                SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Category,
                       r.Date, r.InTime, r.OutTime
                FROM dbo.RawData r
                JOIN dbo.EmployeeInfo e ON e.EmpID = r.EmpID
                WHERE {string.Join(" AND ", employeeWhere)}
                  AND {EmployeeExitHelper.EmployeeActiveOnDateSql("e", "r.Date")}
                  AND r.Date BETWEEN @dStart AND @dEnd
                ORDER BY r.Date, e.EmpID";
            var allRawParams = employeeParams.Append(("@dStart", (object)startDate))
                                              .Append(("@dEnd", (object)extendedEnd)).ToArray();
            List<object[]> allRawRows;
            try { allRawRows = DbHelper.FetchRows(attendanceQuery, allRawParams); }
            catch { allRawRows = new List<object[]>(); }

            var rawTimesMap = new Dictionary<(string, string), (string?, string?)>();
            foreach (var r in allRawRows)
            {
                if (r[0] != null && r[5] != null)
                    rawTimesMap[(r[0].ToString()!, r[5].ToString()!)] = (r[6] as string, r[7] as string);
            }

            var attendanceMap = new Dictionary<(string, string), object?[]>();
            foreach (var raw in allRawRows)
            {
                if (raw[0] == null || raw[5] == null) continue;
                var empId = raw[0]!; var name = raw[1]; var section = raw[2]; var designation = raw[3];
                var category = raw[4]; var dateValue = raw[5].ToString()!;
                var inTimeRaw = raw[6] as string; var outTimeRaw = raw[7] as string;

                if (string.CompareOrdinal(dateValue, endDate) > 0) continue;

                var empIdKey = empId.ToString()!;
                var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(
                    Convert.ToInt32(empId), dateValue, empShiftCache);

                if (dutyType == "Night")
                {
                    if (string.IsNullOrEmpty(outTimeRaw))
                    {
                        var nextDate = ShiftLogic.ParseDate(dateValue).AddDays(1).ToString("yyyy-MM-dd");
                        rawTimesMap.TryGetValue((empIdKey, nextDate), out var nextTimes);
                        foreach (var candidate in new[] { nextTimes.Item1, nextTimes.Item2 })
                        {
                            if (string.IsNullOrEmpty(candidate)) continue;
                            try
                            {
                                var tStr = candidate!.Trim();
                                tStr = tStr.Contains(' ') ? tStr.Split(' ')[^1] : tStr;
                                var t = ShiftLogic.ParseTime(tStr);
                                if (t.Hours < 12) { outTimeRaw = candidate; break; }
                            }
                            catch { }
                        }
                    }

                    var rowDay = ShiftLogic.ParseDate(dateValue).DayOfWeek;
                    if (rowDay == DayOfWeek.Friday && !string.IsNullOrEmpty(inTimeRaw) && string.IsNullOrEmpty(outTimeRaw))
                    {
                        try
                        {
                            var tStr = inTimeRaw!.Trim();
                            tStr = tStr.Contains(' ') ? tStr.Split(' ')[^1] : tStr;
                            var t = ShiftLogic.ParseTime(tStr);
                            if (t.Hours < 12) continue;
                        }
                        catch { }
                    }
                }

                var (inDisplay, outDisplay) = ReportTimeHelper.AttendanceTimesForReport(
                    inTimeRaw, outTimeRaw, dutyType, dateValue);
                attendanceMap[(empIdKey, dateValue)] = new object?[]
                    { empId, name, section, designation, category, dateValue, inDisplay, outDisplay };
            }

            var nonAbsentDays = CalendarHelper.NonAbsentDays();
            foreach (var dateValue in ReportQueryBuilder.DateRangeValues(startDate, endDate))
            {
                var dayName = ShiftLogic.ParseDate(dateValue).DayOfWeek.ToString();
                foreach (var emp in employees)
                {
                    if (emp[0] == null) continue;
                    var empId = Convert.ToInt32(emp[0]);
                    var name = emp[1]; var section = emp[2]; var designation = emp[3]; var category = emp[4];
                    if (!EmployeeExitHelper.IsEmployeeActiveOn(exitDates, empId, dateValue)) continue;
                    var key = (empId.ToString(), dateValue);

                    if (holidayMap.TryGetValue(dateValue, out var festival))
                    {
                        rows.Add(new object?[] { empId, name, section, designation, category, dateValue,
                            CalendarHelper.HolidayMarker(festival), "" });
                        continue;
                    }

                    if (dayName == "Friday")
                    {
                        var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(empId, dateValue, empShiftCache);
                        if (dutyType == "Night")
                        {
                            if (attendanceMap.TryGetValue(key, out var fridayRow))
                            {
                                var friInFmt = fridayRow[6]?.ToString() ?? "";
                                if (friInFmt.ToUpperInvariant().Contains("PM"))
                                {
                                    rows.Add(fridayRow);
                                    continue;
                                }
                            }
                            rows.Add(new object?[] { empId, name, section, designation, category, dateValue,
                                CalendarHelper.FridayMarker, "" });
                        }
                        else if (dutyType == "Day")
                        {
                            rows.Add(new object?[] { empId, name, section, designation, category, dateValue,
                                CalendarHelper.FridayMarker, "" });
                        }
                        else
                        {
                            if (attendanceMap.TryGetValue(key, out var row)) rows.Add(row);
                            else rows.Add(new object?[] { empId, name, section, designation, category, dateValue,
                                CalendarHelper.FridayMarker, "" });
                        }
                        continue;
                    }

                    if (attendanceMap.TryGetValue(key, out var attRow)) rows.Add(attRow);
                    else if (!CalendarHelper.IsNonAbsentDate(dateValue, nonAbsentDays))
                        rows.Add(new object?[] { empId, name, section, designation, category, dateValue,
                            CalendarHelper.AbsentMarker, "" });
                }
            }

            rows = rows.OrderBy(r => r[5]?.ToString() ?? "")
                       .ThenBy(r => NumericSortValue(r[0]))
                       .ToList();
            return new ReportResult { Columns = columns, Rows = rows };
        }

        private static double NumericSortValue(object? value) =>
            double.TryParse(value?.ToString(), out var d) ? d : double.PositiveInfinity;

        private static (string, string) AttendanceDateBounds()
        {
            var today = DateTime.Now.ToString("yyyy-MM-dd");
            try
            {
                var rows = DbHelper.FetchRows(
                    "SELECT MIN(Date), MAX(Date) FROM dbo.RawData WHERE Date IS NOT NULL AND LTRIM(RTRIM(Date)) != ''");
                if (rows.Count == 0 || rows[0][0] is null) return (today, today);
                return (rows[0][0]!.ToString()!, rows[0][1]?.ToString() ?? today);
            }
            catch { return (today, today); }
        }
    }
}

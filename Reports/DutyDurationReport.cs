using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.Reports
{
    public static class DutyDurationReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var columns = new List<string> { "EmpID", "Name", "Section", "Designation", "Date", "In Time", "Out Time", "Duration" };
            var rows = new List<object?[]>();

            string startDate = !string.IsNullOrEmpty(filters.FromDate) ? filters.FromDate : filters.Date;
            string endDate = !string.IsNullOrEmpty(filters.ToDate) ? filters.ToDate : filters.Date;

            if (string.CompareOrdinal(startDate, endDate) > 0)
            {
                var temp = startDate;
                startDate = endDate;
                endDate = temp;
            }

            var holidayMap = CalendarHelper.CompanyHolidayMap(startDate, endDate);
            var exitDates = EmployeeExitHelper.GetEmployeeExitDateMap();

            var empQuery = $@"SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Shift
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
                empShiftCache[empId] = emp.Length > 4 && emp[4] != null ? (emp[4].ToString() ?? "").Trim() : "";
            }

            var extendedEnd = ShiftLogic.ParseDate(endDate).AddDays(1).ToString("yyyy-MM-dd");
            var attendanceQuery = $@"
                SELECT e.EmpID, e.Name, e.Section, e.Designation,
                       r.Date, r.InTime, r.OutTime
                FROM dbo.RawData r JOIN dbo.EmployeeInfo e ON e.EmpID = r.EmpID
                WHERE {string.Join(" AND ", employeeWhere)}
                  AND {EmployeeExitHelper.EmployeeActiveOnDateSql("e", "r.Date")}
                  AND r.Date BETWEEN @dStart AND @dEnd
                ORDER BY r.Date, e.EmpID";
            List<object[]> allRows;
            try
            {
                allRows = DbHelper.FetchRows(attendanceQuery,
                    employeeParams.Append(("@dStart", (object)startDate)).Append(("@dEnd", (object)extendedEnd)).ToArray());
            }
            catch { allRows = new List<object[]>(); }

            var rawTimesMap = new Dictionary<(string, string), (string?, string?)>();
            foreach (var r in allRows)
            {
                if (r[0] != null && r[4] != null)
                    rawTimesMap[(r[0].ToString()!, r[4].ToString()!)] = (r[5] as string, r[6] as string);
            }

            var attendanceMap = new Dictionary<(string, string), object?[]>();
            foreach (var row in allRows)
            {
                if (row[0] == null || row[4] == null) continue;
                var empId = row[0]!; var name = row[1]; var section = row[2]; var designation = row[3];
                var dateValue = row[4].ToString()!;
                var inTimeRaw = row[5] as string; var outTimeRaw = row[6] as string;
                if (string.CompareOrdinal(dateValue, endDate) > 0) continue;

                var empIdKey = empId.ToString()!;
                var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(Convert.ToInt32(empId), dateValue, empShiftCache);

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
                                if (ShiftLogic.ParseTime(tStr).Hours < 12) { outTimeRaw = candidate; break; }
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
                            if (ShiftLogic.ParseTime(tStr).Hours < 12) continue;
                        }
                        catch { }
                    }
                }

                string duration = "";
                if (!string.IsNullOrEmpty(inTimeRaw) && !string.IsNullOrEmpty(outTimeRaw))
                {
                    try
                    {
                        var (inDt, outDt) = ShiftLogic.BuildAttendanceDatetimes(dateValue, inTimeRaw, outTimeRaw);
                        duration = ReportTimeHelper.FormatHoursDuration(ShiftLogic.CalculateHours(inDt, outDt));
                    }
                    catch { duration = ""; }
                }

                var (inDisplay, outDisplay) = ReportTimeHelper.AttendanceTimesForReport(inTimeRaw, outTimeRaw, dutyType, dateValue);
                attendanceMap[(empIdKey, dateValue)] = new object?[] { empId, name, section, designation, dateValue, inDisplay, outDisplay, duration };
            }

            var nonAbsentDays = CalendarHelper.NonAbsentDays();
            foreach (var dateValue in ReportQueryBuilder.DateRangeValues(startDate, endDate))
            {
                var dayName = ShiftLogic.ParseDate(dateValue).DayOfWeek.ToString();
                foreach (var emp in employees)
                {
                    if (emp[0] == null) continue;
                    var empId = Convert.ToInt32(emp[0]);
                    var name = emp[1]; var section = emp[2]; var designation = emp[3];
                    if (!EmployeeExitHelper.IsEmployeeActiveOn(exitDates, empId, dateValue)) continue;
                    var key = (empId.ToString(), dateValue);

                    if (holidayMap.TryGetValue(dateValue, out var festival))
                    {
                        rows.Add(new object?[] { empId, name, section, designation, dateValue,
                            CalendarHelper.HolidayMarker(festival), "", "" });
                        continue;
                    }

                    if (dayName == "Friday")
                    {
                        var dutyType = ShiftExpectationHelper.EmployeeShiftDutyType(empId, dateValue, empShiftCache);
                        if (dutyType == "Night")
                        {
                            if (attendanceMap.TryGetValue(key, out var fridayRow) &&
                                (fridayRow[5]?.ToString() ?? "").ToUpperInvariant().Contains("PM"))
                            {
                                rows.Add(fridayRow);
                                continue;
                            }
                        }
                        rows.Add(new object?[] { empId, name, section, designation, dateValue,
                            CalendarHelper.FridayMarker, "", "" });
                        continue;
                    }

                    if (attendanceMap.TryGetValue(key, out var row)) rows.Add(row);
                    else if (!CalendarHelper.IsNonAbsentDate(dateValue, nonAbsentDays))
                        rows.Add(new object?[] { empId, name, section, designation, dateValue,
                            CalendarHelper.AbsentMarker, "", "" });
                }
            }

            rows = rows.OrderBy(r => r[4]?.ToString() ?? "")
                       .ThenBy(r => double.TryParse(r[0]?.ToString(), out var d) ? d : double.PositiveInfinity)
                       .ToList();
            return new ReportResult { Columns = columns, Rows = rows };
        }
    }
}

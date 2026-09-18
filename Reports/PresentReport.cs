using PayrollManagement.Data;

namespace PayrollManagement.Reports
{
    public static class PresentReport
    {
        public static ReportResult Fetch(ReportFilters filters)
        {
            var (employeeWhere, employeeParams) = ReportQueryBuilder.EmployeeFilterSql(filters);
            var where = new List<string>(employeeWhere);
            var parameters = new List<(string, object)>(employeeParams);
            int i = employeeParams.Count;

            string startDate, endDate;
            if (!string.IsNullOrEmpty(filters.FromDate) && !string.IsNullOrEmpty(filters.ToDate))
            {
                startDate = filters.FromDate;
                endDate = filters.ToDate;
                if (string.CompareOrdinal(startDate, endDate) > 0)
                {
                    var temp = startDate;
                    startDate = endDate;
                    endDate = temp;
                }
                where.Add($"r.Date BETWEEN @p{i} AND @p{i + 1}");
                parameters.Add(($"@p{i}", startDate));
                parameters.Add(($"@p{i + 1}", endDate));
                i += 2;
            }
            else
            {
                where.Add($"r.Date = @p{i}");
                parameters.Add(($"@p{i}", filters.Date));
                startDate = endDate = filters.Date;
                i++;
            }
            where.Add("(COALESCE(r.InTime, '') != '' OR COALESCE(r.OutTime, '') != '')");
            where.Add(EmployeeExitHelper.EmployeeActiveOnDateSql("e", "r.Date"));

            var query = $@"
                SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Category,
                       r.Date, r.InTime, r.OutTime
                FROM dbo.RawData r JOIN dbo.EmployeeInfo e ON e.EmpID = r.EmpID
                WHERE {string.Join(" AND ", where)}
                ORDER BY r.Date, e.EmpID";

            var holidayMap = CalendarHelper.CompanyHolidayMap(startDate, endDate);
            var rows = new List<object?[]>();
            try
            {
                foreach (var r in DbHelper.FetchRows(query, parameters.ToArray()))
                {
                    if (r[5] == null) continue;
                    var rowValues = (object?[])r.Clone();
                    var dateValue = rowValues[5].ToString()!;
                    if (holidayMap.TryGetValue(dateValue, out var festival))
                    {
                        rowValues[6] = CalendarHelper.HolidayMarker(festival);
                        rowValues[7] = "";
                    }
                    else
                    {
                        rowValues[6] = ReportTimeHelper.FormatReportTime(rowValues[6] as string);
                        rowValues[7] = ReportTimeHelper.FormatReportTime(rowValues[7] as string);
                    }
                    rows.Add(rowValues);
                }
            }
            catch { }

            if (holidayMap.Count > 0)
            {
                try
                {
                    var empQuery = $@"SELECT e.EmpID, e.Name, e.Section, e.Designation, e.Category
                                   FROM dbo.EmployeeInfo e WHERE {string.Join(" AND ", employeeWhere)}
                                   ORDER BY e.EmpID";
                    var employees = DbHelper.FetchRows(empQuery, employeeParams.ToArray());
                    var exitDates = EmployeeExitHelper.GetEmployeeExitDateMap();
                    var existingKeys = rows.Where(r => r[0] != null && r[5] != null)
                        .Select(r => (r[0]!.ToString()!, r[5]!.ToString()!)).ToHashSet();

                    foreach (var dateValue in ReportQueryBuilder.DateRangeValues(startDate, endDate))
                    {
                        if (!holidayMap.TryGetValue(dateValue, out var festival)) continue;
                        foreach (var emp in employees)
                        {
                            if (emp[0] == null) continue;
                            var empId = Convert.ToInt32(emp[0]);
                            if (!EmployeeExitHelper.IsEmployeeActiveOn(exitDates, empId, dateValue)) continue;
                            var key = (empId.ToString(), dateValue);
                            if (existingKeys.Contains(key)) continue;
                            rows.Add(new object?[] { emp[0], emp[1], emp[2], emp[3], emp[4], dateValue,
                                CalendarHelper.HolidayMarker(festival), "" });
                            existingKeys.Add(key);
                        }
                    }
                    rows = rows.OrderBy(r => r[5]?.ToString() ?? "")
                               .ThenBy(r => double.TryParse(r[0]?.ToString(), out var d) ? d : double.PositiveInfinity)
                               .ToList();
                }
                catch { }
            }

            return new ReportResult
            {
                Columns = new() { "EmpID", "Name", "Section", "Designation", "Category", "Date", "In Time", "Out Time" },
                Rows = rows,
            };
        }
    }
}

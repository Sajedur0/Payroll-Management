namespace PayrollManagement.Data
{
    public static class EmployeeExitHelper
    {
        public static Dictionary<int, string> GetEmployeeExitDateMap()
        {
            try
            {
                var rows = DbHelper.FetchRows(@"
                    SELECT EmpID, MIN(NULLIF(LTRIM(RTRIM(ExitDate)), ''))
                    FROM EmployeeExitInfo
                    WHERE NULLIF(LTRIM(RTRIM(ExitDate)), '') IS NOT NULL
                    GROUP BY EmpID");
                var map = new Dictionary<int, string>();
                foreach (var r in rows)
                {
                    if (r[0] == null || r[1] == null) continue;
                    map[Convert.ToInt32(r[0])] = r[1].ToString()!;
                }
                return map;
            }
            catch
            {
                return new Dictionary<int, string>();
            }
        }

        public static bool IsEmployeeActiveOn(Dictionary<int, string> exitDates, int empId, string dateValue)
        {
            if (!exitDates.TryGetValue(empId, out var exitDate) || string.IsNullOrEmpty(exitDate))
                return true;
            if (string.IsNullOrEmpty(dateValue))
                return true;
            return string.CompareOrdinal(dateValue, exitDate) <= 0;
        }

        public static string EmployeeActiveOnDateSql(string empAlias, string dateExpr) =>
            $@"NOT EXISTS (
                 SELECT 1 FROM EmployeeExitInfo x
                 WHERE x.EmpID = {empAlias}.EmpID
                   AND NULLIF(LTRIM(RTRIM(x.ExitDate)), '') IS NOT NULL
                   AND x.ExitDate < {dateExpr}
               )";
    }
}

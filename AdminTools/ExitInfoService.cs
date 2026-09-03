using PayrollManagement.Data;

namespace PayrollManagement.AdminTools
{
    public static class ExitReasons
    {
        public static readonly string[] CommonResignReasons =
        {
            "Better career opportunity", "Personal reason", "Family reason", "Relocation",
            "Higher education", "Health issue", "Mutual agreement", "End of contract",
            "Unsatisfactory performance", "Regular absenteeism", "Disciplinary issue",
            "Violation of company policy", "Misconduct", "Workforce reduction",
        };
    }

    public record ExitInfoRow(int ExitId, int EmpId, string Name, string ExitType, string ExitDate, string Reason);

    public class ExitInfoInput
    {
        public int EmpId { get; set; }
        public string ExitType { get; set; } = "Resign";
        public string ExitDate { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    public static class ExitInfoService
    {
        public static List<ExitInfoRow> List(string exitType) =>
            DbHelper.FetchRows(@"
                SELECT x.ExitID, x.EmpID, e.Name, x.ExitType, x.ExitDate, x.Reason
                FROM EmployeeExitInfo x
                LEFT JOIN EmployeeInfo e ON e.EmpID = x.EmpID
                WHERE x.ExitType = @p0
                ORDER BY x.CreatedAt DESC", ("@p0", exitType))
            .Select(r => new ExitInfoRow(Convert.ToInt32(r[0]), Convert.ToInt32(r[1]),
                r[2]?.ToString() ?? "", r[3]?.ToString() ?? "", r[4]?.ToString() ?? "",
                r[5]?.ToString() ?? "")).ToList();

        public static string? LookupEmployeeName(int empId)
        {
            var rows = DbHelper.FetchRows("SELECT Name FROM EmployeeInfo WHERE EmpID = @p0", ("@p0", empId));
            return rows.Count > 0 ? rows[0][0]?.ToString() : null;
        }

        public static (bool ok, string? error) Save(ExitInfoInput input)
        {
            var name = LookupEmployeeName(input.EmpId);
            if (name is null) return (false, "Employee not found");

            DbHelper.ExecuteNonQuery(@"
                MERGE EmployeeExitInfo AS target
                USING (VALUES (@p0,@p1,@p2,@p3)) AS source (EmpID, ExitType, ExitDate, Reason)
                ON target.EmpID = source.EmpID AND target.ExitType = source.ExitType
                WHEN MATCHED THEN
                    UPDATE SET ExitDate=source.ExitDate, Reason=source.Reason
                WHEN NOT MATCHED THEN
                    INSERT (EmpID, ExitType, ExitDate, Reason)
                    VALUES (source.EmpID, source.ExitType, source.ExitDate, source.Reason);",
                ("@p0", input.EmpId), ("@p1", input.ExitType), ("@p2", input.ExitDate), ("@p3", input.Reason.Trim()));
            return (true, null);
        }

        public static void Delete(int exitId) =>
            DbHelper.ExecuteNonQuery("DELETE FROM EmployeeExitInfo WHERE ExitID = @p0", ("@p0", exitId));
    }
}

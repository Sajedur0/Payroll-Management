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
        public static void EnsureTable()
        {
            try
            {
                DbHelper.ExecuteNonQuery(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='EmployeeExitInfo')
                    CREATE TABLE dbo.EmployeeExitInfo (
                        ExitID INT IDENTITY(1,1) PRIMARY KEY,
                        EmpID INT NOT NULL,
                        ExitType NVARCHAR(50) NOT NULL,
                        ExitDate NVARCHAR(20) NOT NULL,
                        Reason NVARCHAR(500) NULL,
                        CreatedAt DATETIME DEFAULT GETDATE()
                    );");
            }
            catch { }
        }

        public static List<ExitInfoRow> List(string exitType)
        {
            EnsureTable();
            try
            {
                return DbHelper.FetchRows(@"
                    SELECT x.ExitID, x.EmpID, e.Name, x.ExitType, x.ExitDate, x.Reason
                    FROM dbo.EmployeeExitInfo x
                    LEFT JOIN dbo.EmployeeInfo e ON e.EmpID = x.EmpID
                    WHERE x.ExitType = @p0
                    ORDER BY x.CreatedAt DESC", ("@p0", exitType))
                .Select(r => new ExitInfoRow(Convert.ToInt32(r[0]), Convert.ToInt32(r[1]),
                    r[2]?.ToString() ?? "", r[3]?.ToString() ?? "", r[4]?.ToString() ?? "",
                    r[5]?.ToString() ?? "")).ToList();
            }
            catch { return new List<ExitInfoRow>(); }
        }

        public static string? LookupEmployeeName(int empId)
        {
            try
            {
                var rows = DbHelper.FetchRows("SELECT Name FROM dbo.EmployeeInfo WHERE EmpID = @p0", ("@p0", empId));
                return rows.Count > 0 ? rows[0][0]?.ToString() : null;
            }
            catch { return null; }
        }

        public static (bool ok, string? error) Save(ExitInfoInput input)
        {
            EnsureTable();
            var name = LookupEmployeeName(input.EmpId);
            if (name is null) return (false, "Employee not found with the given EmpID");

            DbHelper.ExecuteNonQuery(@"
                MERGE dbo.EmployeeExitInfo AS target
                USING (VALUES (@p0,@p1,@p2,@p3)) AS source (EmpID, ExitType, ExitDate, Reason)
                ON target.EmpID = source.EmpID AND target.ExitType = source.ExitType
                WHEN MATCHED THEN
                    UPDATE SET ExitDate=source.ExitDate, Reason=source.Reason
                WHEN NOT MATCHED THEN
                    INSERT (EmpID, ExitType, ExitDate, Reason)
                    VALUES (source.EmpID, source.ExitType, source.ExitDate, source.Reason);",
                ("@p0", input.EmpId), ("@p1", input.ExitType), ("@p2", input.ExitDate), ("@p3", input.Reason.Trim()));

            // Also keep EmployeeInfo.Status in sync with the exit
            if (!string.IsNullOrWhiteSpace(input.ExitType))
            {
                try
                {
                    string status = input.ExitType.Equals("Resign", StringComparison.OrdinalIgnoreCase) ? "Resigned" : input.ExitType;
                    DbHelper.ExecuteNonQuery(
                        "UPDATE dbo.EmployeeInfo SET Status = @p0 WHERE EmpID = @p1",
                        ("@p0", status), ("@p1", input.EmpId));
                }
                catch { }
            }

            return (true, null);
        }

        public static void Delete(int exitId)
        {
            EnsureTable();
            try
            {
                var rows = DbHelper.FetchRows("SELECT EmpID FROM dbo.EmployeeExitInfo WHERE ExitID = @p0", ("@p0", exitId));
                DbHelper.ExecuteNonQuery("DELETE FROM dbo.EmployeeExitInfo WHERE ExitID = @p0", ("@p0", exitId));

                // If no remaining exit records for this employee, restore status to Active
                if (rows.Count > 0 && rows[0][0] != null)
                {
                    int empId = Convert.ToInt32(rows[0][0]);
                    var remaining = DbHelper.FetchRows("SELECT COUNT(*) FROM dbo.EmployeeExitInfo WHERE EmpID = @p0", ("@p0", empId));
                    if (remaining.Count > 0 && Convert.ToInt32(remaining[0][0]) == 0)
                    {
                        DbHelper.ExecuteNonQuery("UPDATE dbo.EmployeeInfo SET Status = 'Active' WHERE EmpID = @p0", ("@p0", empId));
                    }
                }
            }
            catch
            {
                DbHelper.ExecuteNonQuery("DELETE FROM dbo.EmployeeExitInfo WHERE ExitID = @p0", ("@p0", exitId));
            }
        }
    }
}

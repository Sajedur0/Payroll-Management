using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.AdminTools
{
    public record ShiftScheduleRow(int ScheduleId, string ShiftName, string DutyType, string InTime, string OutTime, string Notes);

    public static class ShiftChangeService
    {
        public static void EnsureTable()
        {
            try
            {
                DbHelper.ExecuteNonQuery(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='ShiftSchedule')
                    CREATE TABLE dbo.ShiftSchedule (
                        ScheduleID INT IDENTITY(1,1) PRIMARY KEY,
                        ShiftName NVARCHAR(50) NOT NULL,
                        DutyType NVARCHAR(20) NULL,
                        InTime NVARCHAR(20) NULL,
                        OutTime NVARCHAR(20) NULL,
                        Notes NVARCHAR(200) NULL
                    );");
            }
            catch { }
        }

        public static List<string> UnscheduledShifts()
        {
            EnsureTable();
            try
            {
                return DbHelper.FetchRows(@"
                    SELECT DISTINCT Shift FROM dbo.EmployeeInfo
                    WHERE Shift IS NOT NULL AND LTRIM(RTRIM(Shift)) <> ''
                      AND Shift NOT IN (
                        SELECT ShiftName FROM dbo.ShiftSchedule
                        WHERE ShiftName IS NOT NULL AND LTRIM(RTRIM(ShiftName)) <> ''
                      )
                    ORDER BY 1")
                .Select(r => r[0]!.ToString()!).ToList();
            }
            catch { return new List<string>(); }
        }

        public static List<ShiftScheduleRow> List()
        {
            EnsureTable();
            try
            {
                return DbHelper.FetchRows(
                    "SELECT ScheduleID, ShiftName, DutyType, InTime, OutTime, Notes FROM dbo.ShiftSchedule ORDER BY ShiftName")
                .Select(r => new ShiftScheduleRow(Convert.ToInt32(r[0]), r[1]?.ToString() ?? "",
                    r[2]?.ToString() ?? "", r[3]?.ToString() ?? "", r[4]?.ToString() ?? "", r[5]?.ToString() ?? ""))
                .ToList();
            }
            catch { return new List<ShiftScheduleRow>(); }
        }

        public static (bool ok, string? error) Save(string shiftName, string inTime, string outTime, string notes)
        {
            EnsureTable();
            shiftName = (shiftName ?? "").Trim();
            if (string.IsNullOrEmpty(shiftName)) return (false, "Shift name is required");
            if (inTime == "00:00:00") inTime = "";
            if (outTime == "00:00:00") outTime = "";
            if (string.IsNullOrEmpty(inTime) && string.IsNullOrEmpty(outTime))
                return (false, "In Time and Out Time cannot both be blank");

            try
            {
                ShiftLogic.SaveShiftTime(shiftName, inTime, outTime, (notes ?? "").Trim());
                return (true, null);
            }
            catch (Exception e)
            {
                return (false, $"Failed to save shift time: {e.Message}");
            }
        }

        public static void Delete(int scheduleId)
        {
            EnsureTable();
            DbHelper.ExecuteNonQuery("DELETE FROM dbo.ShiftSchedule WHERE ScheduleID = @p0", ("@p0", scheduleId));
            try
            {
                var count = Convert.ToInt32(DbHelper.ExecuteScalar("SELECT COUNT(*) FROM dbo.ShiftSchedule"));
                if (count == 0)
                    DbHelper.ExecuteNonQuery("DBCC CHECKIDENT ('dbo.ShiftSchedule', RESEED, 0)");
            }
            catch { }
        }
    }
}

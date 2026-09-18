using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.AdminTools
{
    public record ShiftCycleSelection(string ShiftName, string DutyType);

    public record ShiftRotationRow(string ScheduleDate, string ShiftName, string DutyType,
        string InDateTime, string OutDateTime, bool IsOvertime);

    public static class ShiftCycleService
    {
        public static void EnsureTable()
        {
            try
            {
                DbHelper.ExecuteNonQuery(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='ShiftCycleConfig')
                    CREATE TABLE dbo.ShiftCycleConfig (
                        ShiftName NVARCHAR(50) PRIMARY KEY,
                        DutyType NVARCHAR(20) NOT NULL,
                        StartDate NVARCHAR(20) NOT NULL
                    );
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='ShiftRotationSchedule')
                    CREATE TABLE dbo.ShiftRotationSchedule (
                        RotationID INT IDENTITY(1,1) PRIMARY KEY,
                        ScheduleDate NVARCHAR(20) NOT NULL,
                        ShiftName NVARCHAR(50) NOT NULL,
                        DutyType NVARCHAR(20) NOT NULL,
                        InDateTime NVARCHAR(30) NOT NULL,
                        OutDateTime NVARCHAR(30) NOT NULL,
                        IsOvertime INT DEFAULT 0
                    );");
            }
            catch { }
        }

        public static List<string> AllShiftsInEmployeeInfo()
        {
            EnsureTable();
            try
            {
                return DbHelper.FetchRows(@"
                    SELECT DISTINCT Shift FROM dbo.EmployeeInfo
                    WHERE Shift IS NOT NULL AND LTRIM(RTRIM(Shift)) <> '' ORDER BY Shift")
                .Select(r => r[0]!.ToString()!.Trim()).ToList();
            }
            catch { return new List<string>(); }
        }

        public static Dictionary<string, ShiftLogic.CycleConfig> ExistingConfigs()
        {
            EnsureTable();
            return ShiftLogic.GetCycleConfigs();
        }

        public static List<ShiftRotationRow> RecentRotationRows(int top = 500)
        {
            EnsureTable();
            try
            {
                return DbHelper.FetchRows($@"
                    SELECT TOP {top} ScheduleDate, ShiftName, DutyType, InDateTime, OutDateTime, IsOvertime
                    FROM dbo.ShiftRotationSchedule ORDER BY ScheduleDate DESC, ShiftName")
                .Select(r => new ShiftRotationRow(r[0]?.ToString() ?? "", r[1]?.ToString() ?? "",
                    r[2]?.ToString() ?? "", r[3]?.ToString() ?? "", r[4]?.ToString() ?? "",
                    Convert.ToInt32(r[5]) == 1)).ToList();
            }
            catch { return new List<ShiftRotationRow>(); }
        }

        public static (bool ok, string? error, int count) GenerateCycle(
            string startDate, List<ShiftCycleSelection> selectedShifts)
        {
            EnsureTable();
            if (selectedShifts.Count == 0)
                return (false, "Please select at least one shift for the cycle.", 0);

            try
            {
                foreach (var sel in selectedShifts)
                {
                    ShiftLogic.SaveCycleConfig(sel.ShiftName, sel.DutyType, startDate);
                    ShiftLogic.SetShiftSchedule(sel.ShiftName, startDate, sel.DutyType == "Day", weeks: 520);
                }
                return (true, null, selectedShifts.Count);
            }
            catch (Exception e)
            {
                return (false, $"Failed to generate shift cycle: {e.Message}", 0);
            }
        }

        public static void DeleteAll()
        {
            EnsureTable();
            try { DbHelper.ExecuteNonQuery("DELETE FROM dbo.ShiftRotationSchedule"); } catch { }
            try { ShiftLogic.ClearCycleConfigs(); } catch { }
        }
    }
}

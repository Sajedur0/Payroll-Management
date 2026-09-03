using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.AdminTools
{
    public record ShiftScheduleRow(int ScheduleId, string ShiftName, string DutyType, string InTime, string OutTime, string Notes);

    public static class ShiftChangeService
    {
        public static List<string> UnscheduledShifts() =>
            DbHelper.FetchRows(@"
                SELECT DISTINCT Shift FROM EmployeeInfo
                WHERE Shift IS NOT NULL AND TRIM(Shift) <> ''
                  AND Shift NOT IN (
                    SELECT ShiftName FROM ShiftSchedule
                    WHERE ShiftName IS NOT NULL AND TRIM(ShiftName) <> ''
                  )
                ORDER BY 1")
            .Select(r => r[0]!.ToString()!).ToList();

        public static List<ShiftScheduleRow> List() =>
            DbHelper.FetchRows(
                "SELECT ScheduleID, ShiftName, DutyType, InTime, OutTime, Notes FROM ShiftSchedule ORDER BY ShiftName")
            .Select(r => new ShiftScheduleRow(Convert.ToInt32(r[0]), r[1]?.ToString() ?? "",
                r[2]?.ToString() ?? "", r[3]?.ToString() ?? "", r[4]?.ToString() ?? "", r[5]?.ToString() ?? ""))
            .ToList();

        public static (bool ok, string? error) Save(string shiftName, string inTime, string outTime, string notes)
        {
            shiftName = shiftName.Trim();
            if (string.IsNullOrEmpty(shiftName)) return (false, "No shift found in Employee Info");
            if (inTime == "00:00:00") inTime = "";
            if (outTime == "00:00:00") outTime = "";
            if (string.IsNullOrEmpty(inTime) && string.IsNullOrEmpty(outTime))
                return (false, "In Time and Out Time cannot both be blank");

            try
            {
                ShiftLogic.SaveShiftTime(shiftName, inTime, outTime, notes.Trim());
                return (true, null);
            }
            catch (Exception e)
            {
                return (false, $"Failed to save shift time: {e.Message}");
            }
        }

        public static void Delete(int scheduleId)
        {
            DbHelper.ExecuteNonQuery("DELETE FROM ShiftSchedule WHERE ScheduleID = @p0", ("@p0", scheduleId));
            try
            {
                var count = Convert.ToInt32(DbHelper.ExecuteScalar("SELECT COUNT(*) FROM ShiftSchedule"));
                if (count == 0)
                    DbHelper.ExecuteNonQuery("DBCC CHECKIDENT ('ShiftSchedule', RESEED, 0)");
            }
            catch { }
        }
    }
}

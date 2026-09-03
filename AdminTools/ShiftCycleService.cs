using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.AdminTools
{
    public record ShiftCycleSelection(string ShiftName, string DutyType);

    public record ShiftRotationRow(string ScheduleDate, string ShiftName, string DutyType,
        string InDateTime, string OutDateTime, bool IsOvertime);

    public static class ShiftCycleService
    {
        public static List<string> AllShiftsInEmployeeInfo() =>
            DbHelper.FetchRows(@"
                SELECT DISTINCT Shift FROM EmployeeInfo
                WHERE Shift IS NOT NULL AND TRIM(Shift) <> '' ORDER BY Shift")
            .Select(r => r[0]!.ToString()!.Trim()).ToList();

        public static Dictionary<string, ShiftLogic.CycleConfig> ExistingConfigs() =>
            ShiftLogic.GetCycleConfigs();

        public static List<ShiftRotationRow> RecentRotationRows(int top = 500) =>
            DbHelper.FetchRows($@"
                SELECT TOP {top} ScheduleDate, ShiftName, DutyType, InDateTime, OutDateTime, IsOvertime
                FROM ShiftRotationSchedule ORDER BY ScheduleDate DESC, ShiftName")
            .Select(r => new ShiftRotationRow(r[0]?.ToString() ?? "", r[1]?.ToString() ?? "",
                r[2]?.ToString() ?? "", r[3]?.ToString() ?? "", r[4]?.ToString() ?? "",
                Convert.ToInt32(r[5]) == 1)).ToList();

        public static (bool ok, string? error, int count) GenerateCycle(
            string startDate, List<ShiftCycleSelection> selectedShifts)
        {
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
            try { DbHelper.ExecuteNonQuery("DELETE FROM ShiftRotationSchedule"); } catch { }
            try { ShiftLogic.ClearCycleConfigs(); } catch { }
        }
    }
}

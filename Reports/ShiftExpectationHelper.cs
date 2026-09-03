using PayrollManagement.Data;
using PayrollManagement.Shifts;

namespace PayrollManagement.Reports
{
    public static class ShiftExpectationHelper
    {
        public static TimeSpan? GetExpectedShiftInTime(string? shiftName, string dateValue)
        {
            if (string.IsNullOrWhiteSpace(shiftName)) return null;
            shiftName = shiftName.Trim();

            try
            {
                var schedule = ShiftLogic.GetShiftScheduleForDate(shiftName, dateValue);
                if (schedule is not null && !string.IsNullOrEmpty(schedule.InDateTime))
                {
                    var inPart = schedule.InDateTime.Contains(' ')
                        ? schedule.InDateTime.Split(' ', 2)[1] : schedule.InDateTime;
                    return ShiftLogic.ParseTime(inPart);
                }
            }
            catch { }

            try
            {
                var staticShift = ShiftLogic.GetStaticShiftSchedule(shiftName);
                if (staticShift is not null && !string.IsNullOrEmpty(staticShift.InTime))
                    return ShiftLogic.ParseTime(staticShift.InTime.Trim());
            }
            catch { }

            return null;
        }

        public static string? EmployeeShiftDutyType(int empId, string dateValue,
            Dictionary<int, string>? empShiftCache = null)
        {
            empShiftCache ??= new();
            if (!empShiftCache.TryGetValue(empId, out var shiftName))
            {
                try
                {
                    var rows = DbHelper.FetchRows(
                        "SELECT Shift FROM EmployeeInfo WHERE EmpID = @p0", ("@p0", empId));
                    shiftName = rows.Count > 0 ? (rows[0][0] as string ?? "").Trim() : "";
                }
                catch { shiftName = ""; }
                empShiftCache[empId] = shiftName;
            }
            if (string.IsNullOrEmpty(shiftName)) return null;
            try { return ShiftLogic.GetDutyTypeForDate(shiftName, dateValue); }
            catch { return null; }
        }
    }
}

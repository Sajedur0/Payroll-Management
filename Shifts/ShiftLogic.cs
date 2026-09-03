using System.Globalization;
using PayrollManagement.Data;

namespace PayrollManagement.Shifts
{
    public enum DutyType { Day, Night }

    public static class ShiftLogic
    {
        public const string DateFormat = "yyyy-MM-dd";
        public const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";

        public static readonly TimeSpan DayIn = new(8, 0, 0);
        public static readonly TimeSpan DayOut = new(20, 0, 0);
        public static readonly TimeSpan NightIn = new(20, 0, 0);
        public static readonly TimeSpan NightOut = new(8, 0, 0);

        public static DateTime ParseDate(string value) =>
            DateTime.ParseExact(value.Trim(), DateFormat, CultureInfo.InvariantCulture);

        public static TimeSpan ParseTime(string value)
        {
            string[] formats = { "HH:mm:ss", "HH:mm", "hh:mm:ss tt", "hh:mm tt" };
            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(value.Trim(), fmt, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    return dt.TimeOfDay;
            }
            throw new FormatException($"Invalid time: {value}");
        }

        public static string NormalizeShiftName(string shiftName)
        {
            var text = shiftName.Trim().ToUpperInvariant().Replace("SHIFT", "").Trim();
            if (text != "A" && text != "B")
                throw new ArgumentException("Only Shift A and Shift B are supported for auto-rotation");
            return $"Shift {text}";
        }

        public static bool IsAbShift(string shiftName)
        {
            try { NormalizeShiftName(shiftName); return true; }
            catch (ArgumentException) { return false; }
        }

        public static string OppositeShift(string shiftName)
        {
            var normalized = NormalizeShiftName(shiftName);
            return normalized == "Shift A" ? "Shift B" : "Shift A";
        }

        public static DateTime SaturdayOfWeek(DateTime workDate)
        {
            int dow = (int)workDate.DayOfWeek;
            int daysSinceSaturday = ((dow - 6) % 7 + 7) % 7;
            return workDate.Date.AddDays(-daysSinceSaturday);
        }

        public static (DateTime inDt, DateTime outDt) CombineShiftDateTime(
            DateTime workDate, TimeSpan inTime, TimeSpan outTime)
        {
            var inDt = workDate.Date + inTime;
            var outDt = workDate.Date + outTime;
            if (outDt <= inDt) outDt = outDt.AddDays(1);
            return (inDt, outDt);
        }

        public static double CalculateHours(string inDateTimeStr, string outDateTimeStr)
        {
            var inDt = DateTime.ParseExact(inDateTimeStr, DateTimeFormat, CultureInfo.InvariantCulture);
            var outDt = DateTime.ParseExact(outDateTimeStr, DateTimeFormat, CultureInfo.InvariantCulture);
            return (outDt - inDt).TotalHours;
        }

        public static (string inDateTime, string outDateTime) BuildAttendanceDatetimes(
            string workDate, string inTime, string outTime)
        {
            var (inDt, outDt) = CombineShiftDateTime(ParseDate(workDate), ParseTime(inTime), ParseTime(outTime));
            return (inDt.ToString(DateTimeFormat), outDt.ToString(DateTimeFormat));
        }

        public static string DutyTypeForTimes(string inTime, string outTime)
        {
            var inT = ParseTime(inTime);
            var outT = ParseTime(outTime);
            return outT <= inT ? "Night" : "Day";
        }

        public static bool IsFridayNightShift(DateTime? inDt) =>
            inDt.HasValue && inDt.Value.DayOfWeek == DayOfWeek.Friday && inDt.Value.TimeOfDay == NightIn;

        public record CycleConfig(string DutyType, string StartDate);

        public static Dictionary<string, CycleConfig> GetCycleConfigs()
        {
            try
            {
                var rows = DbHelper.FetchRows("SELECT ShiftName, DutyType, StartDate FROM ShiftCycleConfig");
                var configs = new Dictionary<string, CycleConfig>();
                foreach (var r in rows)
                {
                    var name = (r[0] as string ?? "").Trim();
                    if (name == "") continue;
                    configs[name] = new CycleConfig(
                        (r[1] as string ?? "Day").Trim(),
                        (r[2] as string ?? "").Trim());
                }
                return configs;
            }
            catch { return new Dictionary<string, CycleConfig>(); }
        }

        public static void SaveCycleConfig(string shiftName, string dutyType, string startDate) =>
            DbHelper.ExecuteNonQuery(@"
                MERGE ShiftCycleConfig AS target
                USING (VALUES (@p0,@p1,@p2)) AS source (ShiftName, DutyType, StartDate)
                ON target.ShiftName = source.ShiftName
                WHEN MATCHED THEN
                    UPDATE SET DutyType = source.DutyType, StartDate = source.StartDate
                WHEN NOT MATCHED THEN
                    INSERT (ShiftName, DutyType, StartDate)
                    VALUES (source.ShiftName, source.DutyType, source.StartDate);",
                ("@p0", shiftName), ("@p1", dutyType), ("@p2", startDate));

        public static void DeleteCycleConfig(string shiftName) =>
            DbHelper.ExecuteNonQuery("DELETE FROM ShiftCycleConfig WHERE ShiftName = @p0", ("@p0", shiftName));

        public static void ClearCycleConfigs() =>
            DbHelper.ExecuteNonQuery("DELETE FROM ShiftCycleConfig");

        public static bool IsShiftInCycle(string shiftName)
        {
            var normalized = shiftName.Trim().ToUpperInvariant().Replace(" ", "");
            return GetCycleConfigs().Keys.Any(k => k.Trim().ToUpperInvariant().Replace(" ", "") == normalized);
        }

        public static string? GetDutyTypeForDate(string shiftName, string workDateStr,
            Dictionary<string, CycleConfig>? cycleConfigs = null)
        {
            if (string.IsNullOrWhiteSpace(shiftName)) return null;
            cycleConfigs ??= GetCycleConfigs();

            var normalized = shiftName.Trim().ToUpperInvariant().Replace(" ", "");
            var cfg = cycleConfigs.FirstOrDefault(kv =>
                kv.Key.Trim().ToUpperInvariant().Replace(" ", "") == normalized).Value;
            if (cfg is null || string.IsNullOrEmpty(cfg.StartDate)) return null;

            var workDate = ParseDate(workDateStr);
            var startDate = ParseDate(cfg.StartDate);

            var satOfWorkWeek = SaturdayOfWeek(workDate);
            var satOfStartWeek = SaturdayOfWeek(startDate);
            if (satOfWorkWeek < satOfStartWeek) return null;

            int weekIndex = (int)(satOfWorkWeek - satOfStartWeek).TotalDays / 7;
            bool isStartingDay = cfg.DutyType == "Day";
            bool isDay = weekIndex % 2 == 0 ? isStartingDay : !isStartingDay;
            return isDay ? "Day" : "Night";
        }

        public record StaticShift(string DutyType, string InTime, string OutTime);

        public static StaticShift? GetStaticShiftSchedule(string shiftName)
        {
            try
            {
                var rows = DbHelper.FetchRows(@"
                    SELECT DutyType, InTime, OutTime FROM ShiftSchedule
                    WHERE UPPER(REPLACE(ShiftName, ' ', '')) = UPPER(REPLACE(@p0, ' ', ''))",
                    ("@p0", shiftName.Trim()));
                if (rows.Count == 0) return null;
                var r = rows[0];
                return new StaticShift(r[0] as string ?? "", r[1] as string ?? "", r[2] as string ?? "");
            }
            catch { return null; }
        }

        public static void SaveShiftTime(string shiftName, string inTime, string outTime, string notes)
        {
            shiftName = shiftName.Trim();
            if (string.IsNullOrEmpty(inTime) || string.IsNullOrEmpty(outTime)) return;

            var inT = ParseTime(inTime).ToString(@"hh\:mm\:ss");
            var outT = ParseTime(outTime).ToString(@"hh\:mm\:ss");
            var dutyType = DutyTypeForTimes(inT, outT);

            DbHelper.ExecuteNonQuery(@"
                MERGE ShiftSchedule AS target
                USING (VALUES (@p0,@p1,@p2,@p3,@p4))
                      AS source (ShiftName, DutyType, InTime, OutTime, Notes)
                ON target.ShiftName = source.ShiftName
                WHEN MATCHED THEN
                    UPDATE SET DutyType=source.DutyType, InTime=source.InTime,
                               OutTime=source.OutTime, Notes=source.Notes
                WHEN NOT MATCHED THEN
                    INSERT (ShiftName, DutyType, InTime, OutTime, Notes)
                    VALUES (source.ShiftName, source.DutyType, source.InTime,
                            source.OutTime, source.Notes);",
                ("@p0", shiftName), ("@p1", dutyType), ("@p2", inT), ("@p3", outT), ("@p4", notes));
        }

        public record RotationRow(string DutyType, string InDateTime, string OutDateTime, bool IsOvertime);

        public static RotationRow? GetShiftScheduleForDate(string shiftName, string workDateStr)
        {
            try
            {
                var rows = DbHelper.FetchRows(@"
                    SELECT DutyType, InDateTime, OutDateTime, IsOvertime
                    FROM ShiftRotationSchedule
                    WHERE UPPER(REPLACE(ShiftName, ' ', '')) = UPPER(REPLACE(@p0, ' ', ''))
                      AND ScheduleDate = @p1",
                    ("@p0", shiftName.Trim()), ("@p1", ParseDate(workDateStr).ToString(DateFormat)));
                if (rows.Count == 0) return null;
                var r = rows[0];
                return new RotationRow(r[0] as string ?? "", r[1] as string ?? "", r[2] as string ?? "",
                    Convert.ToInt32(r[3]) == 1);
            }
            catch { return null; }
        }

        public static void SetShiftSchedule(string shiftName, string startDateStr, bool isDayShift, int weeks = 520)
        {
            var firstDate = ParseDate(startDateStr);
            var alignedStart = SaturdayOfWeek(firstDate);

            (string name, bool startingIsDay, StaticShift? custom)[] shiftsToGenerate;
            try
            {
                var primary = NormalizeShiftName(shiftName);
                var secondary = OppositeShift(primary);
                shiftsToGenerate = new[]
                {
                    (primary, isDayShift, GetStaticShiftSchedule(primary)),
                    (secondary, !isDayShift, GetStaticShiftSchedule(secondary)),
                };
            }
            catch (ArgumentException)
            {
                shiftsToGenerate = new[] { (shiftName.Trim(), isDayShift, GetStaticShiftSchedule(shiftName)) };
            }

            var rows = new List<(string date, string name, string duty, string inDt, string outDt, int ot)>();
            for (int dayOffset = 0; dayOffset < weeks * 7; dayOffset++)
            {
                var workDate = alignedStart.AddDays(dayOffset);
                int weekIndex = dayOffset / 7;
                foreach (var (name, startingIsDay, custom) in shiftsToGenerate)
                {
                    bool isDay = weekIndex % 2 == 0 ? startingIsDay : !startingIsDay;

                    TimeSpan inTime, outTime;
                    string dutyType;
                    if (custom is not null && !string.IsNullOrEmpty(custom.InTime) && !string.IsNullOrEmpty(custom.OutTime))
                    {
                        inTime = ParseTime(custom.InTime);
                        outTime = ParseTime(custom.OutTime);
                        dutyType = DutyTypeForTimes(custom.InTime, custom.OutTime);
                    }
                    else
                    {
                        inTime = isDay ? DayIn : NightIn;
                        outTime = isDay ? DayOut : NightOut;
                        dutyType = isDay ? "Day" : "Night";
                    }

                    var (inDt, outDt) = CombineShiftDateTime(workDate, inTime, outTime);
                    int isOvertime = IsFridayNightShift(inDt) ? 1 : 0;

                    rows.Add((workDate.ToString(DateFormat), NormalizeOrOriginal(name), dutyType,
                        inDt.ToString(DateTimeFormat), outDt.ToString(DateTimeFormat), isOvertime));
                }
            }

            using var conn = DbHelper.GetConnection();
            using var tx = conn.BeginTransaction();
            foreach (var r in rows)
            {
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    MERGE ShiftRotationSchedule AS target
                    USING (VALUES (@p0,@p1,@p2,@p3,@p4,@p5))
                          AS source (ScheduleDate, ShiftName, DutyType, InDateTime, OutDateTime, IsOvertime)
                    ON target.ScheduleDate = source.ScheduleDate
                       AND target.ShiftName = source.ShiftName
                    WHEN MATCHED THEN
                        UPDATE SET DutyType=source.DutyType, InDateTime=source.InDateTime,
                                   OutDateTime=source.OutDateTime, IsOvertime=source.IsOvertime
                    WHEN NOT MATCHED THEN
                        INSERT (ScheduleDate, ShiftName, DutyType, InDateTime, OutDateTime, IsOvertime)
                        VALUES (source.ScheduleDate, source.ShiftName, source.DutyType,
                                source.InDateTime, source.OutDateTime, source.IsOvertime);", conn, tx);
                cmd.Parameters.AddWithValue("@p0", r.date);
                cmd.Parameters.AddWithValue("@p1", r.name);
                cmd.Parameters.AddWithValue("@p2", r.duty);
                cmd.Parameters.AddWithValue("@p3", r.inDt);
                cmd.Parameters.AddWithValue("@p4", r.outDt);
                cmd.Parameters.AddWithValue("@p5", r.ot);
                cmd.ExecuteNonQuery();
            }

            foreach (var (name, _, _) in shiftsToGenerate)
            {
                var last = rows.LastOrDefault(x => x.name == NormalizeOrOriginal(name));
                if (last.name is null) continue;
                var inTimeOnly = last.inDt[^8..];
                var outTimeOnly = last.outDt[^8..];
                var notes = last.ot == 1 ? "Friday night OT" : "";
                using var cmd = new Microsoft.Data.SqlClient.SqlCommand(@"
                    MERGE ShiftSchedule AS target
                    USING (VALUES (@p0,@p1,@p2,@p3,@p4))
                          AS source (ShiftName, DutyType, InTime, OutTime, Notes)
                    ON target.ShiftName = source.ShiftName
                    WHEN MATCHED THEN
                        UPDATE SET DutyType=source.DutyType, InTime=source.InTime,
                                   OutTime=source.OutTime, Notes=source.Notes
                    WHEN NOT MATCHED THEN
                        INSERT (ShiftName, DutyType, InTime, OutTime, Notes)
                        VALUES (source.ShiftName, source.DutyType, source.InTime,
                                source.OutTime, source.Notes);", conn, tx);
                cmd.Parameters.AddWithValue("@p0", name);
                cmd.Parameters.AddWithValue("@p1", last.duty);
                cmd.Parameters.AddWithValue("@p2", inTimeOnly);
                cmd.Parameters.AddWithValue("@p3", outTimeOnly);
                cmd.Parameters.AddWithValue("@p4", notes);
                cmd.ExecuteNonQuery();
            }

            tx.Commit();

            static string NormalizeOrOriginal(string n)
            {
                try { return NormalizeShiftName(n); } catch { return n.Trim(); }
            }
        }

        public static void PopulateAllShiftSchedules(int weeks = 520)
        {
            foreach (var (shiftName, cfg) in GetCycleConfigs())
            {
                if (string.IsNullOrEmpty(cfg.StartDate)) continue;
                SetShiftSchedule(shiftName, cfg.StartDate, cfg.DutyType == "Day", weeks);
            }
        }
    }
}

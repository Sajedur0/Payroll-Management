namespace PayrollManagement.Reports
{
    public static class ReportTimeHelper
    {
        public static string FormatReportTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string[] formats = { "HH:mm:ss", "HH:mm", "yyyy-MM-dd HH:mm:ss" };
            foreach (var fmt in formats)
                if (DateTime.TryParseExact(value.Trim(), fmt, null,
                        System.Globalization.DateTimeStyles.None, out var dt))
                    return dt.ToString("hh:mm tt");
            return value.Trim();
        }

        public static string FormatShiftTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string[] formats = { "HH:mm:ss", "HH:mm", "hh:mm:ss tt", "hh:mm tt" };
            foreach (var fmt in formats)
                if (DateTime.TryParseExact(value.Trim(), fmt, null,
                        System.Globalization.DateTimeStyles.None, out var dt))
                    return dt.ToString("hh:mm tt");
            return value.Trim();
        }

        public static bool? IsAmTime(string? timeStr)
        {
            if (string.IsNullOrEmpty(timeStr)) return null;
            var text = timeStr.Trim();
            text = text.Contains(' ') ? text.Split(' ')[^1] : text;
            string[] formats = { "HH:mm:ss", "HH:mm" };
            foreach (var fmt in formats)
                if (DateTime.TryParseExact(text, fmt, null,
                        System.Globalization.DateTimeStyles.None, out var dt))
                    return dt.Hour < 12;
            return null;
        }

        public static string FormatDuration(TimeSpan? duration)
        {
            if (duration is null) return "0:00";
            var totalSeconds = (int)Math.Abs(duration.Value.TotalSeconds);
            var minutes = totalSeconds / 60;
            var hours = minutes / 60;
            minutes %= 60;
            return $"{hours}:{minutes:D2}";
        }

        public static string FormatHoursDuration(double hoursValue)
        {
            var totalMinutes = (int)Math.Round(hoursValue * 60);
            var hours = totalMinutes / 60;
            var minutes = totalMinutes % 60;
            return $"{hours}:{minutes:D2}";
        }

        public static (string inDisplay, string outDisplay) AttendanceTimesForReport(
            string? inTimeRaw, string? outTimeRaw, string? dutyType, string dateValue)
        {
            var inFmt = FormatReportTime(inTimeRaw);
            var outFmt = FormatReportTime(outTimeRaw);

            if (dutyType != "Night") return (inFmt, outFmt);

            var inIsAm = IsAmTime(inTimeRaw);
            var outIsAm = IsAmTime(outTimeRaw);

            if (!string.IsNullOrEmpty(inTimeRaw) && !string.IsNullOrEmpty(outTimeRaw))
            {
                if (inIsAm == true && outIsAm == false) return (outFmt, inFmt);
                return (inFmt, outFmt);
            }

            if (!string.IsNullOrEmpty(inTimeRaw) && inIsAm == false) return (inFmt, "");
            if (!string.IsNullOrEmpty(inTimeRaw) && inIsAm == true) return ("", inFmt);
            if (!string.IsNullOrEmpty(outTimeRaw) && outIsAm == false) return (outFmt, "");
            if (!string.IsNullOrEmpty(outTimeRaw) && outIsAm == true) return ("", outFmt);

            return (inFmt, outFmt);
        }
    }
}

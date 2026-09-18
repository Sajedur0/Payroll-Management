using System.Globalization;

namespace PayrollManagement.Reports
{
    public static class ReportTimeHelper
    {
        public static string FormatReportTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string[] formats = {
                "HH:mm:ss", "HH:mm", "H:mm:ss", "H:mm",
                "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd H:mm:ss",
                "hh:mm:ss tt", "hh:mm tt", "h:mm:ss tt", "h:mm tt"
            };
            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(value.Trim(), fmt, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    return dt.ToString("hh:mm tt");
            }

            if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallbackDt))
                return fallbackDt.ToString("hh:mm tt");

            if (TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var ts))
                return DateTime.Today.Add(ts).ToString("hh:mm tt");

            return value.Trim();
        }

        public static string FormatShiftTime(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            string[] formats = {
                "HH:mm:ss", "HH:mm", "H:mm:ss", "H:mm",
                "hh:mm:ss tt", "hh:mm tt", "h:mm:ss tt", "h:mm tt"
            };
            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(value.Trim(), fmt, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    return dt.ToString("hh:mm tt");
            }

            if (DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var fallbackDt))
                return fallbackDt.ToString("hh:mm tt");

            if (TimeSpan.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var ts))
                return DateTime.Today.Add(ts).ToString("hh:mm tt");

            return value.Trim();
        }

        public static bool? IsAmTime(string? timeStr)
        {
            if (string.IsNullOrWhiteSpace(timeStr)) return null;
            var text = timeStr.Trim();

            if (text.IndexOf("AM", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (text.IndexOf("PM", StringComparison.OrdinalIgnoreCase) >= 0) return false;

            if (text.Contains(' '))
            {
                var parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                text = parts[^1];
            }

            string[] formats = { "HH:mm:ss", "HH:mm", "H:mm:ss", "H:mm" };
            foreach (var fmt in formats)
            {
                if (DateTime.TryParseExact(text, fmt, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var dt))
                    return dt.Hour < 12;
            }

            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var ts))
                return ts.Hours < 12;

            if (DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var anyDt))
                return anyDt.Hour < 12;

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

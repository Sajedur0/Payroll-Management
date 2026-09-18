using System.Globalization;
using PayrollManagement.Data;

namespace PayrollManagement.Reports
{
    public static class CalendarHelper
    {
        public static HashSet<string> WeekendDays()
        {
            try
            {
                var days = DbHelper.FetchRows("SELECT DayName FROM dbo.WeekEnd")
                    .Select(r => (r[0] as string ?? "").Trim())
                    .Where(d => !string.IsNullOrEmpty(d))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (days.Count == 0) days.Add("Friday");
                return days;
            }
            catch { return new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Friday" }; }
        }

        public static HashSet<string> NonAbsentDays()
        {
            var days = WeekendDays();
            days.Add("Friday");
            return days;
        }

        public static bool IsNonAbsentDate(string dateValue, HashSet<string> nonAbsentDays)
        {
            if (string.IsNullOrWhiteSpace(dateValue)) return false;
            DateTime dt;
            if (!DateTime.TryParseExact(dateValue.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out dt))
            {
                if (!DateTime.TryParse(dateValue.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out dt))
                    return false;
            }
            return nonAbsentDays.Contains(dt.DayOfWeek.ToString());
        }

        public static Dictionary<string, string> CompanyHolidayMap(string? startDate = null, string? endDate = null)
        {
            try
            {
                var rows = DbHelper.FetchRows(@"
                    SELECT FestivalName, FromDate, ToDate FROM dbo.CompanyHoliday
                    WHERE NULLIF(LTRIM(RTRIM(FromDate)), '') IS NOT NULL");

                var holidays = new Dictionary<string, string>();
                foreach (var r in rows)
                {
                    var festival = (r[0] as string) ?? "Company Holiday";
                    var from = (r[1] as string ?? "").Trim();
                    var to = (r[2] as string ?? from).Trim();
                    if (to == "") to = from;
                    if (from == "") continue;

                    try
                    {
                        foreach (var d in ReportQueryBuilder.DateRangeValues(from, to))
                        {
                            if (startDate != null && string.CompareOrdinal(d, startDate) < 0) continue;
                            if (endDate != null && string.CompareOrdinal(d, endDate) > 0) continue;
                            holidays[d] = string.IsNullOrEmpty(festival) ? "Company Holiday" : festival;
                        }
                    }
                    catch (FormatException) { continue; }
                }
                return holidays;
            }
            catch { return new Dictionary<string, string>(); }
        }

        public const string FridayMarker = "__FRIDAY__";
        public const string AbsentMarker = "__ABSENT__";
        public const string HolidayMarkerPrefix = "__HOLIDAY__:";

        public static string HolidayMarker(string? festivalName) =>
            $"{HolidayMarkerPrefix}{(string.IsNullOrEmpty(festivalName) ? "Company Holiday" : festivalName)}";
    }
}

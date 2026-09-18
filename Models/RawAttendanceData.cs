using System.Globalization;

namespace PayrollManagement.Models
{
    public class RawAttendanceData
    {
        public int LogID { get; set; }
        public int ID { get => LogID; set => LogID = value; } // Alias for LogID
        public string EmpID { get; set; } = "";
        public string Date { get; set; } = "";
        public string? InTime { get; set; }
        public string? OutTime { get; set; }
        public string? InDateTime { get; set; }
        public string? OutDateTime { get; set; }

        // Display helpers
        public string DateDisplay => !string.IsNullOrWhiteSpace(Date) ? Date : "-";
        public string InTimeDisplay => !string.IsNullOrWhiteSpace(InTime) ? InTime : "-";
        public string OutTimeDisplay => !string.IsNullOrWhiteSpace(OutTime) ? OutTime : "-";
        public string InDateTimeDisplay => !string.IsNullOrWhiteSpace(InDateTime) ? InDateTime : "-";
        public string OutDateTimeDisplay => !string.IsNullOrWhiteSpace(OutDateTime) ? OutDateTime : "-";

        public string WorkHoursDisplay
        {
            get
            {
                // First try full InDateTime and OutDateTime for exact timestamp difference
                if (!string.IsNullOrWhiteSpace(InDateTime) && !string.IsNullOrWhiteSpace(OutDateTime))
                {
                    if (DateTime.TryParse(InDateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var inDt) &&
                        DateTime.TryParse(OutDateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var outDt))
                    {
                        if (outDt > inDt)
                        {
                            var diff = outDt - inDt;
                            return $"{(int)diff.TotalHours}h {diff.Minutes:D2}m";
                        }
                    }
                }

                // Fall back to InTime and OutTime
                if (!string.IsNullOrWhiteSpace(InTime) && !string.IsNullOrWhiteSpace(OutTime))
                {
                    if (TimeSpan.TryParse(InTime, out var inT) && TimeSpan.TryParse(OutTime, out var outT))
                    {
                        TimeSpan diff;
                        if (outT >= inT)
                        {
                            diff = outT - inT;
                        }
                        else
                        {
                            // Shift crossed midnight (e.g. In 20:00, Out 08:00)
                            diff = (outT + TimeSpan.FromDays(1)) - inT;
                        }

                        if (diff.TotalMinutes > 0)
                        {
                            return $"{(int)diff.TotalHours}h {diff.Minutes:D2}m";
                        }
                    }
                }
                return "-";
            }
        }
    }
}

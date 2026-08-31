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
                if (!string.IsNullOrWhiteSpace(InTime) && !string.IsNullOrWhiteSpace(OutTime))
                {
                    if (TimeSpan.TryParse(InTime, out var inT) && TimeSpan.TryParse(OutTime, out var outT))
                    {
                        if (outT >= inT)
                        {
                            var diff = outT - inT;
                            return $"{diff.Hours}h {diff.Minutes:D2}m";
                        }
                    }
                }
                return "-";
            }
        }
    }
}

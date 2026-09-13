namespace PayrollManagement.Models
{
    public class DashboardSummary
    {
        public int TotalEmployees { get; set; }
        public int PresentToday { get; set; }
        public int AbsentToday { get; set; }
        public int OnLeave { get; set; }
    }

    public class WeeklyAttendancePoint
    {
        public DateTime AttendanceDate { get; set; }
        public int PresentCount { get; set; }
        public string DayLabel => AttendanceDate.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture);
    }

    }

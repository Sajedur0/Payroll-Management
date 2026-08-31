namespace PayrollManagement.Models
{
    public class AttendanceRecord
    {
        public int AttendanceId { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeCode { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Department { get; set; } = "";
        public DateTime AttendanceDate { get; set; }
        public TimeSpan? CheckIn { get; set; }
        public TimeSpan? CheckOut { get; set; }
        public string Status { get; set; } = ""; // Present, Absent, Leave, Late
        public decimal? WorkHours { get; set; }

        // Display helpers
        public string CheckInDisplay => CheckIn?.ToString(@"hh\:mm") ?? "-";
        public string CheckOutDisplay => CheckOut?.ToString(@"hh\:mm") ?? "-";
        public string DateDisplay => AttendanceDate.ToString("yyyy-MM-dd");
    }
}

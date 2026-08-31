namespace PayrollManagement.Models
{
    public class AttendanceLoadProgress
    {
        public int Processed { get; set; }
        public int Total { get; set; }
        public int Remaining => Math.Max(0, Total - Processed);
        public double Percentage => Total > 0 ? (double)Processed / Total * 100.0 : 0;
        public string Message { get; set; } = "";
    }
}

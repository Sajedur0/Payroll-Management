namespace PayrollManagement.Reports
{
    public class ReportFilters
    {
        public string EmpId { get; set; } = "";
        public string Date { get; set; } = "";
        public string FromDate { get; set; } = "";
        public string ToDate { get; set; } = "";
        public string Month { get; set; } = "";       // "yyyy-MM"
        public int GraceMinutes { get; set; } = 0;
        public string Section { get; set; } = "All";
        public string Designation { get; set; } = "All";
        public string Category { get; set; } = "All";
        public string Shift { get; set; } = "All";
    }
}

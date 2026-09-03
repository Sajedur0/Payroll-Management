namespace PayrollManagement.Reports
{
    public class ReportResult
    {
        public List<string> Columns { get; set; } = new();
        public List<object?[]> Rows { get; set; } = new();
    }
}

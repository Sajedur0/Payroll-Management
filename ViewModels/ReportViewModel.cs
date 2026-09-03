using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Runtime.CompilerServices;
using System.Windows;
using PayrollManagement.Data;
using PayrollManagement.Reports;
using Microsoft.Win32;

namespace PayrollManagement.ViewModels
{
    public class ReportViewModel : INotifyPropertyChanged
    {
        private ReportFilters _filters = new();
        public ReportFilters Filters
        {
            get => _filters;
            set { _filters = value; OnPropertyChanged(); }
        }

        private string _empId = "";
        public string EmpId { get => _empId; set { _empId = value; Filters.EmpId = value; OnPropertyChanged(); } }

        private string _section = "All";
        public string Section { get => _section; set { _section = value; Filters.Section = value; OnPropertyChanged(); RefreshDependentFilters(); } }

        private string _designation = "All";
        public string Designation { get => _designation; set { _designation = value; Filters.Designation = value; OnPropertyChanged(); RefreshDependentFilters(); } }

        private string _category = "All";
        public string Category { get => _category; set { _category = value; Filters.Category = value; OnPropertyChanged(); RefreshDependentFilters(); } }

        private string _shift = "All";
        public string Shift { get => _shift; set { _shift = value; Filters.Shift = value; OnPropertyChanged(); RefreshDependentFilters(); } }

        private DateTime? _fromDate = DateTime.Today.AddDays(-7);
        public DateTime? FromDate { get => _fromDate; set { _fromDate = value; Filters.FromDate = value?.ToString("yyyy-MM-dd") ?? ""; Filters.Date = ""; OnPropertyChanged(); } }

        private DateTime? _toDate = DateTime.Today;
        public DateTime? ToDate { get => _toDate; set { _toDate = value; Filters.ToDate = value?.ToString("yyyy-MM-dd") ?? ""; Filters.Date = ""; OnPropertyChanged(); } }

        private DateTime? _singleDate = DateTime.Today;
        public DateTime? SingleDate { get => _singleDate; set { _singleDate = value; Filters.Date = value?.ToString("yyyy-MM-dd") ?? ""; OnPropertyChanged(); } }

        private string _month = DateTime.Today.ToString("yyyy-MM");
        public string Month { get => _month; set { _month = value; Filters.Month = value; OnPropertyChanged(); } }

        private DateTime _monthPicker = DateTime.Today;
        public DateTime MonthPicker { get => _monthPicker; set { _monthPicker = value; Month = value.ToString("yyyy-MM"); OnPropertyChanged(); OnPropertyChanged(nameof(Month)); } }

        private int _graceMinutes = 10;
        public int GraceMinutes { get => _graceMinutes; set { _graceMinutes = value; Filters.GraceMinutes = value; OnPropertyChanged(); } }

        private bool _useRange = true;
        public bool UseRange { get => _useRange; set { _useRange = value; OnPropertyChanged(); } }

        public ObservableCollection<string> SectionOptions { get; set; } = new();
        public ObservableCollection<string> DesignationOptions { get; set; } = new();
        public ObservableCollection<string> CategoryOptions { get; set; } = new();
        public ObservableCollection<string> ShiftOptions { get; set; } = new();

        private ReportResult? _result;
        public ReportResult? Result { get => _result; set { _result = value; OnPropertyChanged(); OnPropertyChanged(nameof(ResultTable)); OnPropertyChanged(nameof(HasResult)); } }

        public DataView? ResultTable
        {
            get
            {
                if (Result == null) return null;
                var dt = new DataTable();
                foreach (var col in Result.Columns) dt.Columns.Add(col);
                foreach (var row in Result.Rows)
                {
                    var dr = dt.NewRow();
                    for (int i = 0; i < Result.Columns.Count; i++)
                    {
                        var val = i < row.Length ? row[i]?.ToString() ?? "" : "";
                        // Unwrap markers for display
                        if (val == CalendarHelper.AbsentMarker) val = "Absent";
                        else if (val == CalendarHelper.FridayMarker) val = "Friday";
                        else if (val.StartsWith(CalendarHelper.HolidayMarkerPrefix)) val = val.Substring(CalendarHelper.HolidayMarkerPrefix.Length);
                        dr[i] = val;
                    }
                    dt.Rows.Add(dr);
                }
                return dt.DefaultView;
            }
        }

        public bool HasResult => Result != null && Result.Rows.Count > 0;

        private string _reportTitle = "Attendance Report";
        public string ReportTitle { get => _reportTitle; set { _reportTitle = value; OnPropertyChanged(); } }

        private string _statusMessage = "";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        private string? _errorMessage;
        public string? ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public RelayCommand AttendanceCommand { get; }
        public RelayCommand AbsentCommand { get; }
        public RelayCommand PresentCommand { get; }
        public RelayCommand LateCommand { get; }
        public RelayCommand EarlyCommand { get; }
        public RelayCommand DutyDurationCommand { get; }
        public RelayCommand MonthlySummaryCommand { get; }
        public RelayCommand ExportCommand { get; }

        public ReportViewModel()
        {
            Filters.GraceMinutes = _graceMinutes;
            AttendanceCommand = new RelayCommand(_ => RunReport("Attendance"));
            AbsentCommand = new RelayCommand(_ => RunReport("Absent"));
            PresentCommand = new RelayCommand(_ => RunReport("Present"));
            LateCommand = new RelayCommand(_ => RunReport("Late"));
            EarlyCommand = new RelayCommand(_ => RunReport("Early"));
            DutyDurationCommand = new RelayCommand(_ => RunReport("DutyDuration"));
            MonthlySummaryCommand = new RelayCommand(_ => RunReport("Monthly"));
            ExportCommand = new RelayCommand(_ => Export());
            _ = LoadFilterOptionsAsync();
        }

        private async Task LoadFilterOptionsAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    var sections = ReportQueryBuilder.FilteredValues("Section", Filters);
                    var designations = ReportQueryBuilder.FilteredValues("Designation", Filters);
                    var categories = ReportQueryBuilder.FilteredValues("Category", Filters);
                    var shifts = ReportQueryBuilder.FilteredValues("Shift", Filters);
                    Application.Current?.Dispatcher.Invoke(() =>
                    {
                        SectionOptions.Clear(); SectionOptions.Add("All"); foreach (var s in sections) SectionOptions.Add(s);
                        DesignationOptions.Clear(); DesignationOptions.Add("All"); foreach (var s in designations) DesignationOptions.Add(s);
                        CategoryOptions.Clear(); CategoryOptions.Add("All"); foreach (var s in categories) CategoryOptions.Add(s);
                        ShiftOptions.Clear(); ShiftOptions.Add("All"); foreach (var s in shifts) ShiftOptions.Add(s);
                    });
                });
            }
            catch { }
        }

        private void RefreshDependentFilters()
        {
            _ = LoadFilterOptionsAsync();
        }

        private void RunReport(string type)
        {
            IsLoading = true;
            ErrorMessage = null;
            StatusMessage = $"Generating {type} report...";
            try
            {
                // Sync filter dates based on UseRange
                if (type == "Monthly")
                {
                    Filters.Month = Month;
                }
                else
                {
                    if (UseRange)
                    {
                        Filters.FromDate = FromDate?.ToString("yyyy-MM-dd") ?? "";
                        Filters.ToDate = ToDate?.ToString("yyyy-MM-dd") ?? "";
                        Filters.Date = "";
                    }
                    else
                    {
                        Filters.Date = SingleDate?.ToString("yyyy-MM-dd") ?? DateTime.Today.ToString("yyyy-MM-dd");
                        Filters.FromDate = ""; Filters.ToDate = "";
                    }
                }
                Filters.EmpId = EmpId?.Trim() ?? "";
                Filters.GraceMinutes = GraceMinutes;

                ReportResult result;
                switch (type)
                {
                    case "Attendance":
                        ReportTitle = "Attendance Report";
                        EnsureRangeDefaults();
                        result = AttendanceReport.Fetch(Filters);
                        break;
                    case "Absent":
                        ReportTitle = "Absent Report";
                        EnsureRangeDefaults();
                        result = AbsentReport.Fetch(Filters);
                        break;
                    case "Present":
                        ReportTitle = "Present Report";
                        EnsureRangeDefaults();
                        result = PresentReport.Fetch(Filters);
                        break;
                    case "Late":
                        ReportTitle = "Late Report";
                        EnsureRangeDefaults();
                        result = LateEarlyReport.Fetch(LateEarlyType.Late, Filters);
                        break;
                    case "Early":
                        ReportTitle = "Early Report";
                        EnsureRangeDefaults();
                        result = LateEarlyReport.Fetch(LateEarlyType.Early, Filters);
                        break;
                    case "DutyDuration":
                        ReportTitle = "Duty Duration Report";
                        EnsureRangeDefaults();
                        result = DutyDurationReport.Fetch(Filters);
                        break;
                    case "Monthly":
                        ReportTitle = "Monthly Summary Report";
                        result = MonthlySummaryReport.Fetch(Filters);
                        break;
                    default: result = new ReportResult(); break;
                }
                Result = result;
                StatusMessage = $"{ReportTitle}: {result.Rows.Count} rows. {ReportNamingHelper.FilterSummary(Filters, type == "Monthly" ? "monthly" : null)}";
            }
            catch (Exception ex)
            {
                ErrorMessage = ex.Message;
                StatusMessage = $"Error: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        private void EnsureRangeDefaults()
        {
            if (string.IsNullOrEmpty(Filters.FromDate) && string.IsNullOrEmpty(Filters.Date))
            {
                Filters.FromDate = DateTime.Today.AddDays(-7).ToString("yyyy-MM-dd");
                Filters.ToDate = DateTime.Today.ToString("yyyy-MM-dd");
            }
            if (!string.IsNullOrEmpty(Filters.FromDate) && string.IsNullOrEmpty(Filters.ToDate))
                Filters.ToDate = Filters.FromDate;
        }

        private void Export()
        {
            if (Result == null || Result.Rows.Count == 0)
            {
                MessageBox.Show("No data to export. Generate a report first.", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var defaultName = ReportNamingHelper.ExportDefaultFilename(ReportTitle, Filters);
            var dlg = new SaveFileDialog
            {
                FileName = defaultName,
                Filter = "PDF Document (*.pdf)|*.pdf",
                DefaultExt = ".pdf"
            };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var filterSummary = ReportNamingHelper.FilterSummary(Filters, ReportTitle.Contains("Monthly") ? "monthly" : null);
                    ReportPdfBuilder.Build(dlg.FileName, ReportTitle, filterSummary, Result, new ReportPdfOptions());
                    MessageBox.Show($"Exported to {dlg.FileName}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Export", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using PayrollManagement.Data;
using PayrollManagement.Reports;
using PayrollManagement.Views;
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
        public string Section
        {
            get => _section;
            set
            {
                if (value == null) return;
                if (_section == value) return;
                _section = value; Filters.Section = value; OnPropertyChanged();
                RefreshDependentFilters();
            }
        }

        private string _designation = "All";
        public string Designation
        {
            get => _designation;
            set
            {
                if (value == null) return;
                if (_designation == value) return;
                _designation = value; Filters.Designation = value; OnPropertyChanged();
                RefreshDependentFilters();
            }
        }

        private string _category = "All";
        public string Category
        {
            get => _category;
            set
            {
                if (value == null) return;
                if (_category == value) return;
                _category = value; Filters.Category = value; OnPropertyChanged();
                RefreshDependentFilters();
            }
        }

        private string _shift = "All";
        public string Shift
        {
            get => _shift;
            set
            {
                if (value == null) return;
                if (_shift == value) return;
                _shift = value; Filters.Shift = value; OnPropertyChanged();
                RefreshDependentFilters();
            }
        }

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
        public RelayCommand PrintCommand { get; }

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
            PrintCommand = new RelayCommand(_ => Print());
            _ = LoadFilterOptionsAsync();
        }

        private int _optionsLoadSeq;
        private bool _suppressOptionRefresh;

        private async Task LoadFilterOptionsAsync()
        {
            int seq = ++_optionsLoadSeq;
            // Snapshot on UI thread so background query sees a stable filter set.
            string section = _section, designation = _designation, category = _category, shift = _shift;
            var snapshot = new ReportFilters
            {
                Section = section, Designation = designation, Category = category, Shift = shift
            };
            try
            {
                var (sections, designations, categories, shifts) = await Task.Run(() =>
                {
                    var s1 = ReportQueryBuilder.FilteredValues("Section", snapshot);
                    var s2 = ReportQueryBuilder.FilteredValues("Designation", snapshot);
                    var s3 = ReportQueryBuilder.FilteredValues("Category", snapshot);
                    var s4 = ReportQueryBuilder.FilteredValues("Shift", snapshot);
                    return (s1, s2, s3, s4);
                });
                if (seq != _optionsLoadSeq) return; // a newer refresh superseded this one
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null) return;
                await dispatcher.InvokeAsync(() =>
                {
                    if (seq != _optionsLoadSeq) return;
                    _suppressOptionRefresh = true;
                    try
                    {
                        UpdateOptions(SectionOptions, sections, ref _section, nameof(Section));
                        UpdateOptions(DesignationOptions, designations, ref _designation, nameof(Designation));
                        UpdateOptions(CategoryOptions, categories, ref _category, nameof(Category));
                        UpdateOptions(ShiftOptions, shifts, ref _shift, nameof(Shift));
                    }
                    finally { _suppressOptionRefresh = false; }
                });
            }
            catch { }
        }

        private void UpdateOptions(ObservableCollection<string> options, List<string> values, ref string field, string propertyName)
        {
            var fresh = new List<string> { "All" };
            foreach (var v in values)
            {
                var t = v?.Trim();
                if (!string.IsNullOrEmpty(t) && !fresh.Contains(t)) fresh.Add(t);
            }
            // Avoid Clear() when nothing changed: Clear collapses the open popup
            // and pushes SelectedItem=null into the setter (dropdown "কাজ করে না").
            bool same = options.Count == fresh.Count;
            if (same)
            {
                for (int k = 0; k < fresh.Count; k++)
                {
                    if (options[k] != fresh[k]) { same = false; break; }
                }
            }
            if (!same)
            {
                options.Clear();
                foreach (var s in fresh) options.Add(s);
            }
            // Current selection vanished due to dependent narrowing -> fall back to "All".
            if (!fresh.Contains(field))
            {
                field = "All";
                switch (propertyName)
                {
                    case nameof(Section): Filters.Section = "All"; break;
                    case nameof(Designation): Filters.Designation = "All"; break;
                    case nameof(Category): Filters.Category = "All"; break;
                    case nameof(Shift): Filters.Shift = "All"; break;
                }
                OnPropertyChanged(propertyName);
            }
        }

        private void RefreshDependentFilters()
        {
            if (_suppressOptionRefresh) return;
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

        private static Window? OwnerWindow => Application.Current?.MainWindow;

        private void Export()
        {
            // Mirrors Python export_current_report_pdf (reports_page.py:1403-1428).
            if (Result == null || Result.Rows.Count == 0)
            {
                MessageBox.Show("No report data to export", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var options = ReportExportSettingsDialog.ShowSettings(OwnerWindow, "Export", ReportTitle, Filters);
            if (options is null) return; // user pressed Cancel

            var dlg = new SaveFileDialog
            {
                Title = "Save Report",
                FileName = ReportNamingHelper.ExportDefaultFilename(ReportTitle, Filters),
                Filter = "PDF Files (*.pdf)|*.pdf",
                DefaultExt = ".pdf"
            };
            if (dlg.ShowDialog() != true) return;
            string path = dlg.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                ? dlg.FileName : dlg.FileName + ".pdf";

            try
            {
                var filterSummary = ReportNamingHelper.FilterSummary(Filters, ReportTitle.Contains("Monthly") ? "monthly" : null);
                ReportPdfBuilder.Build(path, ReportTitle, filterSummary, Result, options.ToPdfOptions());
                var open = MessageBox.Show($"PDF saved successfully:\n{path}\n\nOpen now?",
                    "Report Saved", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (open == MessageBoxResult.Yes)
                    _ = System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export PDF: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Print()
        {
            // Mirrors Python print_current_report (reports_page.py:1786-1803).
            if (Result == null || Result.Rows.Count == 0)
            {
                MessageBox.Show("No report data to print", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            var options = ReportExportSettingsDialog.ShowSettings(OwnerWindow, "Print", ReportTitle, Filters);
            if (options is null) return; // user pressed Cancel

            string tmp = Path.Combine(Path.GetTempPath(), $"payroll_print_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.pdf");
            try
            {
                var filterSummary = ReportNamingHelper.FilterSummary(Filters, ReportTitle.Contains("Monthly") ? "monthly" : null);
                ReportPdfBuilder.Build(tmp, ReportTitle, filterSummary, Result, options.ToPdfOptions());
                // Open in the default PDF viewer as the print preview; the user prints
                // from there applying options.PaperSize + options.Orientation to page setup.
                _ = System.Diagnostics.Process.Start(
                    new System.Diagnostics.ProcessStartInfo(tmp) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to show preview: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}

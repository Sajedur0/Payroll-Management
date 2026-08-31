using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PayrollManagement.Data;
using PayrollManagement.Models;

namespace PayrollManagement.ViewModels
{
    public class AttendanceLogViewModel : INotifyPropertyChanged
    {
        private readonly AttendanceRepository _repo = new();

        // === Attendance Records ===
        private ObservableCollection<RawAttendanceData> _rawRecords = new();
        public ObservableCollection<RawAttendanceData> RawRecords { get => _rawRecords; set { _rawRecords = value; OnPropertyChanged(); } }

        // === Attendance Records ===
        private ObservableCollection<AttendanceRecord> _records = new();
        public ObservableCollection<AttendanceRecord> Records { get => _records; set { _records = value; OnPropertyChanged(); } }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        private bool _isLoadingFaceId;
        public bool IsLoadingFaceId { get => _isLoadingFaceId; set { _isLoadingFaceId = value; OnPropertyChanged(); } }

        // === Progress Bar Tracking ===
        private bool _isProgressVisible;
        public bool IsProgressVisible { get => _isProgressVisible; set { _isProgressVisible = value; OnPropertyChanged(); } }

        private double _progressValue;
        public double ProgressValue { get => _progressValue; set { _progressValue = value; OnPropertyChanged(); } }

        private string _progressText = "";
        public string ProgressText { get => _progressText; set { _progressText = value; OnPropertyChanged(); } }

        private string _progressPercentageText = "";
        public string ProgressPercentageText { get => _progressPercentageText; set { _progressPercentageText = value; OnPropertyChanged(); } }

        private string? _errorMessage;
        public string? ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        private string? _successMessage;
        public string? SuccessMessage { get => _successMessage; set { _successMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSuccess)); } }
        public bool HasSuccess => !string.IsNullOrEmpty(SuccessMessage);

        private string? _searchText;
        public string? SearchText { get => _searchText; set { _searchText = value; OnPropertyChanged(); } }

        private DateTime _fromDate = DateTime.Today.AddDays(-30);
        public DateTime FromDate { get => _fromDate; set { _fromDate = value; OnPropertyChanged(); } }

        private DateTime _toDate = DateTime.Today;
        public DateTime ToDate { get => _toDate; set { _toDate = value; OnPropertyChanged(); } }

        public RelayCommand LoadCommand { get; }
        public RelayCommand SearchCommand { get; }
        public RelayCommand LoadFromFaceIdCommand { get; }
        public RelayCommand AttendanceDataLoadCommand => LoadFromFaceIdCommand;

        public AttendanceLogViewModel()
        {
            LoadCommand = new RelayCommand(async _ => await LoadAsync());
            SearchCommand = new RelayCommand(async _ => await LoadAsync());
            LoadFromFaceIdCommand = new RelayCommand(async _ => await LoadFromFaceIdDbAsync());
            _ = LoadAsync();
        }

        /// <summary>
        /// Loads attendance data and displays in DataGrid
        /// </summary>
        public async Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            ErrorMessage = null;
            SuccessMessage = null;
            try
            {
                var list = await _repo.GetRawDataAsync(FromDate, ToDate, string.IsNullOrWhiteSpace(SearchText) ? null : SearchText);
                RawRecords = new ObservableCollection<RawAttendanceData>(list);
                if (list.Count == 0)
                    ErrorMessage = "No records found in the selected date range.";
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                RawRecords = new ObservableCollection<RawAttendanceData>();
            }
            finally { IsLoading = false; }
        }

        /// <summary>
        /// Loads external attendance data with live progress reporting
        /// </summary>
        public async Task LoadFromFaceIdDbAsync()
        {
            if (IsLoadingFaceId) return;
            IsLoadingFaceId = true;
            IsProgressVisible = true;
            ProgressValue = 0;
            ProgressText = "Connecting to data source...";
            ProgressPercentageText = "0%";
            ErrorMessage = null;
            SuccessMessage = null;

            try
            {
                var progress = new Progress<AttendanceLoadProgress>(p =>
                {
                    ProgressValue = p.Percentage;
                    if (p.Total > 0)
                    {
                        ProgressText = $"Loading {p.Processed} of {p.Total} records ({p.Remaining} remaining)...";
                        ProgressPercentageText = $"{p.Percentage:0}%";
                    }
                    else
                    {
                        ProgressText = p.Message;
                        ProgressPercentageText = "0%";
                    }
                });

                int count = await _repo.LoadAttendanceFromFaceIdDbAsync(FromDate, ToDate, progress);
                if (count > 0)
                {
                    ProgressValue = 100;
                    ProgressPercentageText = "100%";
                    ProgressText = $"✓ Completed: {count} of {count} records loaded (0 remaining)";
                    SuccessMessage = $"✓ Success! {count} attendance records loaded ({FromDate:yyyy-MM-dd} ~ {ToDate:yyyy-MM-dd})";
                    
                    // Refresh DataGrid
                    await LoadAsync();
                }
                else
                {
                    ErrorMessage = $"No attendance data found for the date range {FromDate:yyyy-MM-dd} ~ {ToDate:yyyy-MM-dd}.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoadingFaceId = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}


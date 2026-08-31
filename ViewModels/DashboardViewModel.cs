using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using PayrollManagement.Data;
using PayrollManagement.Models;

namespace PayrollManagement.ViewModels
{
    // Legacy compatibility
    public class ActivityItem
    {
        public string Description { get; set; } = "";
        public string Time { get; set; } = "";
    }

    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly AttendanceRepository _repo = new();

        public string CurrentDate => DateTime.Now.ToString("dddd, dd MMMM yyyy");

        private int _totalEmployees;
        public int TotalEmployees { get => _totalEmployees; set { _totalEmployees = value; OnPropertyChanged(); } }

        private int _presentToday;
        public int PresentToday { get => _presentToday; set { _presentToday = value; OnPropertyChanged(); } }

        private int _absentToday;
        public int AbsentToday { get => _absentToday; set { _absentToday = value; OnPropertyChanged(); } }

        private int _onLeave;
        public int OnLeave { get => _onLeave; set { _onLeave = value; OnPropertyChanged(); } }

        private ISeries[] _attendanceSeries = Array.Empty<ISeries>();
        public ISeries[] AttendanceSeries { get => _attendanceSeries; set { _attendanceSeries = value; OnPropertyChanged(); } }

        private Axis[] _weekDaysAxis = Array.Empty<Axis>();
        public Axis[] WeekDaysAxis { get => _weekDaysAxis; set { _weekDaysAxis = value; OnPropertyChanged(); } }

        private ObservableCollection<ActivityLogItem> _recentActivities = new();
        public ObservableCollection<ActivityLogItem> RecentActivities { get => _recentActivities; set { _recentActivities = value; OnPropertyChanged(); } }

        // For XAML binding compatibility (old ActivityItem)
        public IEnumerable<ActivityItem> RecentActivitiesLegacy => RecentActivities.Select(a => new ActivityItem { Description = a.Description, Time = a.TimeDisplay });

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusMessage)); } }

        private string? _errorMessage;
        public string? ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(StatusMessage)); } }

        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
        public string StatusMessage => IsLoading ? "Loading data..." : (HasError ? ErrorMessage! : $"Last refresh: {DateTime.Now:T}");

        public RelayCommand RefreshCommand { get; }

        public DashboardViewModel()
        {
            // Placeholder initial data (will be overwritten by DB)
            TotalEmployees = 0;
            PresentToday = 0;
            AbsentToday = 0;
            OnLeave = 0;

            // Default chart to avoid null binding
            AttendanceSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Values = new int[] { 0,0,0,0,0,0,0 },
                    Name = "Attendance",
                    Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(new SkiaSharp.SKColor(79, 70, 229))
                }
            };
            WeekDaysAxis = new Axis[] { new Axis { Labels = new[] { "Sat", "Sun", "Mon", "Tue", "Wed", "Thu", "Fri" } } };

            RefreshCommand = new RelayCommand(async _ => await LoadAsync());

            // Auto load
            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            ErrorMessage = null;

            try
            {
                // Load summary
                var summary = await _repo.GetDashboardSummaryAsync();
                TotalEmployees = summary.TotalEmployees;
                PresentToday = summary.PresentToday;
                AbsentToday = summary.AbsentToday;
                OnLeave = summary.OnLeave;

                // Weekly trend
                var weekly = await _repo.GetWeeklyTrendAsync();
                var values = weekly.Select(w => w.PresentCount).ToArray();
                var labels = weekly.Select(w => w.AttendanceDate.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)).ToArray();

                AttendanceSeries = new ISeries[]
                {
                    new ColumnSeries<int>
                    {
                        Values = values,
                        Name = "Attendance",
                        Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(new SkiaSharp.SKColor(79, 70, 229))
                    }
                };
                WeekDaysAxis = new Axis[]
                {
                    new Axis { Labels = labels }
                };

                // Recent activities
                var activities = await _repo.GetRecentActivitiesAsync(5);
                RecentActivities = new ObservableCollection<ActivityLogItem>(activities);
                OnPropertyChanged(nameof(RecentActivitiesLegacy));
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Error: {ex.Message}";
                // Fallback dummy data so UI not blank
                LoadFallbackData();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void LoadFallbackData()
        {
            // Keep existing fallback if DB fails - show dummy so UI works
            if (TotalEmployees == 0)
            {
                TotalEmployees = 120;
                PresentToday = 104;
                AbsentToday = 9;
                OnLeave = 7;
            }

            AttendanceSeries = new ISeries[]
            {
                new ColumnSeries<int>
                {
                    Values = new int[] { 98, 102, 95, 108, 104, 60, 40 },
                    Name = "Attendance",
                    Fill = new LiveChartsCore.SkiaSharpView.Painting.SolidColorPaint(new SkiaSharp.SKColor(79, 70, 229))
                }
            };
            WeekDaysAxis = new Axis[]
            {
                new Axis { Labels = new[] { "Sat", "Sun", "Mon", "Tue", "Wed", "Thu", "Fri" } }
            };

            if (RecentActivities.Count == 0)
            {
                RecentActivities = new ObservableCollection<ActivityLogItem>
                {
                    new ActivityLogItem { Description = "Rahim Uddin checked in", LogTime = DateTime.Now.AddMinutes(-15) },
                    new ActivityLogItem { Description = "Karim Ahmed applied for leave", LogTime = DateTime.Now.AddMinutes(-30) },
                    new ActivityLogItem { Description = "Salma Khatun checked out", LogTime = DateTime.Now.AddDays(-1) },
                    new ActivityLogItem { Description = "New employee added: Jamal Hossain", LogTime = DateTime.Now.AddDays(-1).AddHours(-2) },
                };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}


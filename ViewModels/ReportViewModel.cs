using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using DashboardApp.Data;
using DashboardApp.Models;

namespace DashboardApp.ViewModels
{
    public class ReportViewModel : INotifyPropertyChanged
    {
        private readonly AttendanceRepository _repo = new();

        private DashboardSummary _summary = new();
        public DashboardSummary Summary { get => _summary; set { _summary = value; OnPropertyChanged(); } }

        private ObservableCollection<Employee> _employees = new();
        public ObservableCollection<Employee> Employees { get => _employees; set { _employees = value; OnPropertyChanged(); } }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        private string? _errorMessage;
        public string? ErrorMessage { get => _errorMessage; set { _errorMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); } }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

        public string ReportDate => DateTime.Now.ToString("dd MMMM yyyy");

        public RelayCommand LoadCommand { get; }

        public ReportViewModel()
        {
            LoadCommand = new RelayCommand(async _ => await LoadAsync());
            _ = LoadAsync();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            ErrorMessage = null;
            try
            {
                Summary = await _repo.GetDashboardSummaryAsync();
                var emps = await _repo.GetEmployeesAsync();
                Employees = new ObservableCollection<Employee>(emps);
            }
            catch (Exception ex)
            {
                ErrorMessage = $"DB Error: {ex.Message}";
            }
            finally { IsLoading = false; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}

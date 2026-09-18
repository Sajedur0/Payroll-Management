using System.Windows;
using PayrollManagement.Views;

namespace PayrollManagement
{
    public partial class MainWindow : Window
    {
        private DashboardView? _dashboardView;
        private EmployeeView? _employeeView;
        private AttendanceLogView? _attendanceView;
        private ReportView? _reportView;
        private AdminToolsView? _adminToolsView;

        public MainWindow()
        {
            InitializeComponent();
            _dashboardView = new DashboardView();
            MainContent.Content = _dashboardView;
            UpdateHeader("Dashboard", "Workforce overview and daily attendance metrics");
            CurrentDateText.Text = DateTime.Now.ToString("dddd, dd MMM yyyy");
        }

        private void UpdateHeader(string title, string subtitle)
        {
            if (PageTitleText != null) PageTitleText.Text = title;
            if (PageSubtitleText != null) PageSubtitleText.Text = subtitle;
        }

        private void NavigateDashboard_Click(object sender, RoutedEventArgs e)
        {
            _dashboardView ??= new DashboardView();
            MainContent.Content = _dashboardView;
            UpdateHeader("Dashboard", "Workforce overview and daily attendance metrics");
        }

        private void NavigateEmployee_Click(object sender, RoutedEventArgs e)
        {
            _employeeView ??= new EmployeeView();
            MainContent.Content = _employeeView;
            UpdateHeader("Employee Directory", "Manage active personnel, payroll structures, and designations");
        }

        private void NavigateAttendance_Click(object sender, RoutedEventArgs e)
        {
            _attendanceView ??= new AttendanceLogView();
            MainContent.Content = _attendanceView;
            UpdateHeader("Attendance Log", "Biometric device synchronization, punch logs, and duty hours");
        }

        private void NavigateReport_Click(object sender, RoutedEventArgs e)
        {
            _reportView ??= new ReportView();
            MainContent.Content = _reportView;
            UpdateHeader("Reports & Exports", "Attendance, absent, late, early, duty duration and monthly PDF reports");
        }

        private void NavigateAdmin_Click(object sender, RoutedEventArgs e)
        {
            _adminToolsView ??= new AdminToolsView();
            MainContent.Content = _adminToolsView;
            UpdateHeader("Admin Tools", "System configurations, company info, weekends, and rotation cycles");
        }
    }
}

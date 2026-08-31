using System.Windows;
using PayrollManagement.Views;

namespace PayrollManagement
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            MainContent.Content = new DashboardView(); // Default view
        }

        private void NavigateDashboard_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new DashboardView();

        private void NavigateEmployee_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new EmployeeView();

        private void NavigateAttendance_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new AttendanceLogView();

        private void NavigateReport_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new ReportView();

        private void NavigateAdmin_Click(object sender, RoutedEventArgs e)
            => MainContent.Content = new AdminToolsView();
    }
}

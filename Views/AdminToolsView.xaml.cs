using System.Windows;
using System.Windows.Controls;
using PayrollManagement.ViewModels;

namespace PayrollManagement.Views
{
    public partial class AdminToolsView : UserControl
    {
        public AdminToolsView()
        {
            InitializeComponent();
            DataContext = new AdminToolsViewModel();
        }

        /// <summary>
        /// Switches the visible tab pane when a tab header RadioButton is clicked.
        /// Every content pane sits in the same StackPanel; only the matching one is shown.
        /// </summary>
        private void OnAdminTab_Click(object sender, RoutedEventArgs e)
        {
            CompanyDetailsPane.Visibility = sender == TabCompanyDetails ? Visibility.Visible : Visibility.Collapsed;
            WeekEndPane.Visibility = sender == TabWeekEnd ? Visibility.Visible : Visibility.Collapsed;
            CompanyHolidayPane.Visibility = sender == TabCompanyHoliday ? Visibility.Visible : Visibility.Collapsed;
            ResignInfoPane.Visibility = sender == TabResignInfo ? Visibility.Visible : Visibility.Collapsed;
            ShiftChangePane.Visibility = sender == TabShiftChange ? Visibility.Visible : Visibility.Collapsed;
            ShiftCyclePane.Visibility = sender == TabShiftCycle ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}

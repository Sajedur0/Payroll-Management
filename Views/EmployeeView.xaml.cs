using System.Windows.Controls;
using System.Windows.Input;
using DashboardApp.ViewModels;

namespace DashboardApp.Views
{
    public partial class EmployeeView : UserControl
    {
        public EmployeeView()
        {
            InitializeComponent();
            DataContext = new EmployeeViewModel();
        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is EmployeeViewModel vm && vm.HasSelectedEmployee)
            {
                vm.OpenEditEmployeeCommand.Execute(null);
            }
        }
    }
}

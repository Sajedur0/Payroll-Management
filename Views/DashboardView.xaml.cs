using System.Windows.Controls;
using PayrollManagement.ViewModels;

namespace PayrollManagement.Views
{
    public partial class DashboardView : UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            DataContext = new DashboardViewModel();
        }
    }
}

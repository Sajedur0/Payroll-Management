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
    }
}

using System.Windows.Controls;
using DashboardApp.ViewModels;

namespace DashboardApp.Views
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

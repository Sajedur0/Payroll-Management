using System.Windows.Controls;
using DashboardApp.ViewModels;

namespace DashboardApp.Views
{
    public partial class ReportView : UserControl
    {
        public ReportView()
        {
            InitializeComponent();
            DataContext = new ReportViewModel();
        }
    }
}

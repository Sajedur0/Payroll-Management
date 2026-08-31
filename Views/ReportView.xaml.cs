using System.Windows.Controls;
using PayrollManagement.ViewModels;

namespace PayrollManagement.Views
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

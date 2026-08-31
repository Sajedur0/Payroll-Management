using System.Windows.Controls;
using PayrollManagement.ViewModels;

namespace PayrollManagement.Views
{
    public partial class AttendanceLogView : UserControl
    {
        public AttendanceLogView()
        {
            InitializeComponent();
            DataContext = new AttendanceLogViewModel();
        }
    }
}

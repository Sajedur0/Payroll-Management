using System.Windows.Controls;
using DashboardApp.ViewModels;

namespace DashboardApp.Views
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

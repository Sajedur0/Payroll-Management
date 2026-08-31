using System.Windows;
using PayrollManagement.ViewModels;

namespace PayrollManagement.Views
{
    public partial class EmployeeFormWindow : Window
    {
        public EmployeeViewModel ViewModel { get; }

        public EmployeeFormWindow(EmployeeViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsEditMode)
            {
                bool ok = await ViewModel.ExecuteUpdateFromDialogAsync();
                if (ok)
                {
                    DialogResult = true;
                    Close();
                }
            }
            else
            {
                bool ok = await ViewModel.ExecuteSaveFromDialogAsync();
                if (ok)
                {
                    DialogResult = true;
                    Close();
                }
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

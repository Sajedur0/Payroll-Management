using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DashboardApp.Data;

namespace DashboardApp.Views
{
    public partial class AdminToolsView : UserControl
    {
        public AdminToolsView()
        {
            InitializeComponent();
            Loaded += AdminToolsView_Loaded;
        }

        private void AdminToolsView_Loaded(object sender, RoutedEventArgs e)
        {
            // Show actual connection string from config
            try
            {
                var csText = FindName("ConnectionStringText") as TextBlock;
                if (csText != null)
                    csText.Text = DbConfig.ConnectionString;
            }
            catch { }
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            var resultText = FindName("TestResultText") as TextBlock;
            if (resultText == null) return;

            resultText.Text = "Testing AttendanceDB connection...";
            resultText.Foreground = new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80));

            bool ok = await DbConfig.TestConnectionAsync();
            if (ok)
            {
                resultText.Text = "✓ Success! AttendanceDB is reachable. All data is being loaded live from the database.";
                resultText.Foreground = new SolidColorBrush(Color.FromRgb(0x05, 0x96, 0x69));
            }
            else
            {
                resultText.Text = "✗ Failed! Could not connect to AttendanceDB. Please check if SQL Server is running, the database is created, and the Server name in App.config is correct.";
                resultText.Foreground = new SolidColorBrush(Color.FromRgb(0xDC, 0x26, 0x26));
            }
        }

        private void OpenScript_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Try project root Database folder
                var possiblePaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "Database"),
                    Path.Combine(Directory.GetCurrentDirectory(), "Database"),
                    @"C:\Users\Zero\AndroidStudioProjects\Payroll CSharp\Database"
                };

                foreach (var p in possiblePaths)
                {
                    var full = Path.GetFullPath(p);
                    if (Directory.Exists(full))
                    {
                        Process.Start(new ProcessStartInfo { FileName = full, UseShellExecute = true });
                        return;
                    }
                }
                // fallback to project root
                var root = @"C:\Users\Zero\AndroidStudioProjects\Payroll CSharp";
                if (Directory.Exists(root))
                    Process.Start(new ProcessStartInfo { FileName = root, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Folder open failed: {ex.Message}", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}


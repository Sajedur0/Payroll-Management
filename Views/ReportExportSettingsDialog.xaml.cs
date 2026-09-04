using System.Windows;
using PayrollManagement.Reports;

namespace PayrollManagement.Views;

public partial class ReportExportSettingsDialog : Window
{
    public ReportExportSettingsDialog(
        string actionLabel,
        string defaultOrientation,
        bool splitByEmployeeDefault,
        Window? owner = null)
    {
        InitializeComponent();
        if (owner != null)
            Owner = owner;

        var vm = new ReportExportSettingsViewModel(defaultOrientation, splitByEmployeeDefault)
        {
            // Mirrors Python: dialog.setWindowTitle(f"{action_label} Settings")
            DialogTitle = $"{actionLabel} Settings",
            ActionLabel = actionLabel
        };

        DataContext = vm;
    }

    private ReportExportSettingsViewModel ViewModel => (ReportExportSettingsViewModel)DataContext;

    public string SelectedPaperSize => ViewModel.PaperSize;
    public string SelectedOrientation => ViewModel.Orientation;
    public bool RepeatCompanyHeader => ViewModel.RepeatCompanyHeader;
    public bool SeparateReport => ViewModel.SeparateReport;

    /// <summary>
    /// Mirrors Python return dict of _report_output_options():
    /// paper_name / orientation / repeat_company_header / separate_report.
    /// Returns null when the user presses Cancel (dialog.exec() != Accepted).
    /// </summary>
    public ReportExportOptions ToOptions() => new(
        PaperSize: SelectedPaperSize,
        Orientation: SelectedOrientation,
        RepeatCompanyHeader: RepeatCompanyHeader,
        SeparateReport: SeparateReport);

    /// <summary>
    /// One-line helper used by Export / Print buttons.
    /// Returns null if the user cancelled.
    /// </summary>
    public static ReportExportOptions? ShowSettings(
        Window? owner,
        string actionLabel,          // "Export" or "Print"
        string currentReportTitle,   // e.g. "Monthly Summary Report"
        IDictionary<string, string?> currentFilters)
    {
        // Mirrors Python _default_report_orientation(): Landscape iff Monthly Summary.
        string defaultOrientation =
            currentReportTitle == "Monthly Summary Report" ? "Landscape" : "Portrait";

        // Mirrors Python split default: true if Section/Designation/Category != "All".
        bool splitDefault = new[] { "section", "designation", "category" }
            .Any(k => currentFilters.TryGetValue(k, out string? v)
                      && !string.IsNullOrWhiteSpace(v)
                      && !string.Equals(v.Trim(), "All", StringComparison.OrdinalIgnoreCase));

        var dlg = new ReportExportSettingsDialog(actionLabel, defaultOrientation, splitDefault, owner);
        return dlg.ShowDialog() == true ? dlg.ToOptions() : null;
    }

    /// <summary>
    /// Typed overload for the app's <see cref="ReportFilters"/> (same default rules as above).
    /// </summary>
    public static ReportExportOptions? ShowSettings(
        Window? owner,
        string actionLabel,
        string currentReportTitle,
        ReportFilters filters)
    {
        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["section"] = filters.Section,
            ["designation"] = filters.Designation,
            ["category"] = filters.Category,
        };
        return ShowSettings(owner, actionLabel, currentReportTitle, dict);
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
        => DialogResult = true;

    private void Cancel_Click(object sender, RoutedEventArgs e)
        => DialogResult = false;
}

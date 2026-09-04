// Mirrors Python dict returned by _report_output_options() in
// payroll_app/reports/reports_page.py:1457-1509
namespace PayrollManagement.Reports;

/// <summary>
/// Result of the Export/Print settings popup.
/// Null return from the dialog means the user pressed Cancel.
/// </summary>
/// <param name="PaperSize">One of A0,A1,A2,A3,A4,A5,A6,B5,Letter,Legal,Executive. Default "A4".</param>
/// <param name="Orientation">"Portrait" or "Landscape".</param>
/// <param name="RepeatCompanyHeader">Checkbox "Company details on every page" (default true).</param>
/// <param name="SeparateReport">Checkbox "Separate report per employee" — true = individual per-employee reports, false = combined report for all.</param>
public sealed record ReportExportOptions(
    string PaperSize,
    string Orientation,
    bool RepeatCompanyHeader,
    bool SeparateReport)
{
    public bool IsLandscape => string.Equals(Orientation, "Landscape", StringComparison.OrdinalIgnoreCase);

    /// <summary>Maps to the existing PDF builder options (ReportPdfOptions.cs).</summary>
    public ReportPdfOptions ToPdfOptions() => new()
    {
        PaperName = PaperSize,
        Orientation = Orientation,
        RepeatCompanyHeaderOnEveryPage = RepeatCompanyHeader,
        SeparateReportPerEmployee = SeparateReport
    };
}

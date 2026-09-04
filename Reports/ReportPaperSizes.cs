// Mirrors Python REPORT_PAPER_SIZES in payroll_app/common/pdf_base.py:17-29
// Sizes in points (1 inch = 72 pt), portrait orientation. Swap W/H for landscape.
namespace PayrollManagement.Reports;

public static class ReportPaperSizes
{
    public static readonly IReadOnlyList<string> Names = new[]
    {
        "A0", "A1", "A2", "A3", "A4", "A5", "A6",
        "B5", "Letter", "Legal", "Executive"
    };

    // Width x Height in points, portrait. Source: ReportLab pagesizes + EXECUTIVE = (7.25", 10.5").
    private static readonly Dictionary<string, (double W, double H)> _sizes = new()
    {
        ["A0"] = (2383.94, 3370.39),
        ["A1"] = (1683.78, 2383.94),
        ["A2"] = (1190.55, 1683.78),
        ["A3"] = (841.89, 1190.55),
        ["A4"] = (595.28, 841.89),
        ["A5"] = (419.53, 595.28),
        ["A6"] = (297.64, 419.53),
        ["B5"] = (498.90, 708.66),
        ["Letter"] = (612.0, 792.0),
        ["Legal"] = (612.0, 1008.0),
        ["Executive"] = (522.0, 756.0), // 7.25*72 x 10.5*72
    };

    public const double MarginPt = 0.3 * 72; // REPORT_HORIZONTAL/VERTICAL_MARGIN

    public static (double Width, double Height) GetPageSize(string paperName, bool landscape)
    {
        if (paperName == null || !_sizes.TryGetValue(paperName, out var s))
            s = _sizes["A4"];
        return landscape ? (s.H, s.W) : (s.W, s.H);
    }
}

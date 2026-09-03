using PayrollManagement.Data;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PayrollManagement.Reports
{
    public class ReportPdfOptions
    {
        public string PaperName { get; set; } = "A4";
        public bool Landscape { get; set; } = false;
        public bool RepeatCompanyHeaderOnEveryPage { get; set; } = true;
        public bool SeparateReportPerEmployee { get; set; } = false;
    }

    public static class ReportPdfBuilder
    {
        public static void Build(string filePath, string reportTitle, string reportFilters,
            ReportResult result, ReportPdfOptions options)
        {
            var company = CompanyHelper.GetCompanyDetails();
            var groups = options.SeparateReportPerEmployee
                ? GroupByEmployee(result)
                : new List<List<object?[]>> { result.Rows };

            QuestPDF.Settings.License = LicenseType.Community;

            // Ensure .pdf extension
            if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                filePath = System.IO.Path.ChangeExtension(filePath, ".pdf");

            Document.Create(container =>
            {
                foreach (var (group, idx) in groups.Select((g, i) => (g, i)))
                {
                    container.Page(page =>
                    {
                        page.Size(options.PaperName == "A4"
                            ? (options.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4)
                            : PageSizes.A4);
                        page.Margin(28);
                        page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Segoe UI"));

                        if (options.RepeatCompanyHeaderOnEveryPage)
                        {
                            page.Header().Column(col =>
                            {
                                col.Item().AlignCenter().Text(company.Name).Bold().FontSize(15).FontColor("#1F2937");
                                if (!string.IsNullOrWhiteSpace(company.Address))
                                    col.Item().AlignCenter().Text(company.Address).FontSize(8).FontColor("#6B7280");
                                if (!string.IsNullOrWhiteSpace(company.Number))
                                    col.Item().AlignCenter().Text($"Number: {company.Number}").FontSize(8).FontColor("#6B7280");
                                col.Item().PaddingTop(6).AlignCenter().Text(reportTitle).Bold().FontSize(12).FontColor("#4F46E5");
                                col.Item().AlignCenter().Text(reportFilters).FontSize(8).FontColor("#6B7280");
                                col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#E5E7EB");
                            });
                        }

                        page.Content().PaddingTop(8).Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                foreach (var _ in result.Columns) cols.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                foreach (var col in result.Columns)
                                {
                                    header.Cell().Background("#1F2937").Padding(4).Text(col).Bold().FontSize(8).FontColor(Colors.White);
                                }
                            });

                            foreach (var row in group)
                            {
                                var inTimeIdx = result.Columns.IndexOf("In Time");
                                var mergedText = inTimeIdx >= 0
                                    ? MergedCellText(row[inTimeIdx]?.ToString())
                                    : null;

                                for (int c = 0; c < result.Columns.Count; c++)
                                {
                                    if (mergedText != null && c == inTimeIdx)
                                    {
                                        table.Cell().ColumnSpan((uint)(result.Columns.Count - c))
                                            .Background("#F3F4F6").Padding(4)
                                            .AlignCenter().Text(mergedText).FontSize(8).Bold().FontColor("#374151");
                                        break;
                                    }
                                    var text = row[c]?.ToString() ?? "";
                                    // zandle markers not in In Time column (e.g., already unwrapped)
                                    if (MergedCellText(text) is string m) text = m;
                                    table.Cell().BorderBottom(0.5f).BorderColor("#F3F4F6").Padding(4).AlignCenter().Text(text).FontSize(8);
                                }
                            }
                        });

                        page.Footer().AlignCenter().Text(t =>
                        {
                            t.Span("Page ").FontSize(7).FontColor("#9CA3AF");
                            t.CurrentPageNumber().FontSize(7).FontColor("#9CA3AF");
                            t.Span(" / ").FontSize(7).FontColor("#9CA3AF");
                            t.TotalPages().FontSize(7).FontColor("#9CA3AF");
                            t.Span("  •  Payroll Management").FontSize(7).FontColor("#9CA3AF");
                        });
                    });
                }
            }).GeneratePdf(filePath);
        }

        private static string? MergedCellText(string? value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            if (value == CalendarHelper.FridayMarker) return "Friday";
            if (value == CalendarHelper.AbsentMarker) return "Absent";
            if (value.StartsWith(CalendarHelper.HolidayMarkerPrefix))
                return value.Substring(CalendarHelper.HolidayMarkerPrefix.Length);
            return null;
        }

        private static List<List<object?[]>> GroupByEmployee(ReportResult result)
        {
            var empIdIdx = result.Columns.IndexOf("EmpID");
            if (empIdIdx < 0) return new List<List<object?[]>> { result.Rows };
            return result.Rows.GroupBy(r => r[empIdIdx]?.ToString())
                .Select(g => g.ToList()).ToList();
        }
    }
}

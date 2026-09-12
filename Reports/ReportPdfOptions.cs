using PayrollManagement.Data;
using PayrollManagement.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PayrollManagement.Reports
{
    public class ReportPdfOptions
    {
        public string PaperName { get; set; } = "A4";
        public string Orientation { get; set; } = "Portrait"; // "Portrait" or "Landscape"
        public bool RepeatCompanyHeaderOnEveryPage { get; set; } = true;
        public bool SeparateReportPerEmployee { get; set; } = false;
    }

    // ── PDF design constants (from Prompt.md) ──────────────────
    public static class PdfDesign
    {
        // Margins (pt). 0.3 inch = 21.6 pt
        // NOTE: QuestPDF Header() renders inside the usable area,
        // so top margin must stay small. Previously 80.64pt caused double gap.
        public const float HorizontalMargin = 21.6f;
        public const float VerticalMargin = 21.6f;
        public const float HeaderTopMargin = 21.6f;
        public const float ExecutiveWidth = 522f;   // 7.25" * 72
        public const float ExecutiveHeight = 756f;  // 10.5" * 72

        // Palette
        public const string NavyText = "#172033";
        public const string TitleBlue = "#0f5f9f";
        public const string GridGray = "#9aa8b8";
        public const string AltRowBg = "#f7fafd";
        public const string MergedCellBg = "#e0e0e0";
        public const string LabelBg = "#eef3f8";
        public const string EmpHeaderBg = "#dfe7f1";
        public const string White = "#ffffff";

        // Fonts
        public const string HeaderFont = "Helvetica";
        public const string BodyFont = "Helvetica";
        public const string PageNumberFont = "Helvetica";

        // Grid
        public const float GridLineWidth = 0.5f;
    }

    public static class ReportPdfBuilder
    {
        // ── Dynamic font size: 6pt >24 cols, 7.5pt >16 cols, 9pt otherwise ──
        public static float GetDynamicFontSize(int columnCount)
        {
            if (columnCount > 24) return 6f;
            if (columnCount > 16) return 7.5f;
            return 9f;
        }

        // ── Column width calculation (content-length based, shrinkable cols) ──
        private static float[] CalculateColumnWidths(List<string> headers, List<List<string>> rows, float availableWidth)
        {
            if (headers == null || headers.Count == 0)
                return new float[0];

            var scores = new List<float>();
            int columnCount = headers.Count;

            for (int colIdx = 0; colIdx < columnCount; colIdx++)
            {
                string headerText = headers[colIdx].Replace("\n", " ").Trim();
                int longest = headerText.Length;

                foreach (var row in rows)
                {
                    if (colIdx < row.Count)
                    {
                        string cellText = row[colIdx].Replace("\n", " ").Trim();
                        longest = Math.Max(longest, cellText.Length);
                    }
                }

                scores.Add(Math.Max(2.5f, (float)longest));
            }

            float totalScore = scores.Sum();
            if (totalScore == 0) totalScore = 1f;

            var widths = scores.Select(s => availableWidth * s / totalScore).ToList();

            float minWidth = columnCount > 20 ? 0.22f * 72f : 0.38f * 72f;
            widths = widths.Select(w => Math.Max(minWidth, w)).ToList();

            float sumWidths = widths.Sum();
            if (sumWidths == 0) sumWidths = availableWidth;
            float scale = availableWidth / sumWidths;
            return widths.Select(w => w * scale).ToArray();
        }

        // ── Paper size mapping ──────────────────────────────────
        private static (float width, float height) GetPaperSize(string paperName)
        {
            switch (paperName.Trim().ToUpperInvariant())
            {
                case "A0": return (2383.94f, 3370.39f);
                case "A1": return (1683.78f, 2383.94f);
                case "A2": return (1190.55f, 1683.78f);
                case "A3": return (841.89f, 1190.55f);
                case "A5": return (419.53f, 595.28f);
                case "A6": return (297.64f, 419.53f);
                case "B5": return (498.90f, 708.66f);
                case "LETTER": case "Letter": return (612f, 792f);
                case "LEGAL": case "Legal": return (612f, 1008f);
                case "EXECUTIVE": case "Executive":
                    return (PdfDesign.ExecutiveWidth, PdfDesign.ExecutiveHeight);
                case "A4":
                default: return (595.28f, 841.89f);
            }
        }

        private static (float w, float h) ResolvePageSizePts(ReportPdfOptions options)
        {
            var (w, h) = GetPaperSize(options.PaperName);
            if (options.Orientation == "Landscape")
                return (h, w);
            return (w, h);
        }

        // ── Company header block (Prompt.md design) ─────────────
        private static void DrawCompanyHeader(ColumnDescriptor col, CompanyDetails company,
            string reportTitle, string filterSummary)
        {
            col.Item().AlignCenter().Text(company.Name ?? "PAYROLL SYSTEM")
                .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(16).FontColor(PdfDesign.NavyText);
            if (!string.IsNullOrWhiteSpace(company.Address))
                col.Item().AlignCenter().Text(company.Address)
                    .FontFamily(PdfDesign.BodyFont).FontSize(9).FontColor(Colors.Black);
            if (!string.IsNullOrWhiteSpace(company.Number))
                col.Item().AlignCenter().Text($"Number: {company.Number}")
                    .FontFamily(PdfDesign.BodyFont).FontSize(9).FontColor(Colors.Black);
            col.Item().PaddingTop(8).AlignCenter().Text(reportTitle ?? "Report")
                .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(12).FontColor(PdfDesign.TitleBlue);
            col.Item().PaddingTop(2).AlignCenter().Text(filterSummary ?? "All records")
                .FontFamily(PdfDesign.BodyFont).FontSize(9).FontColor(Colors.Black);
            col.Item().PaddingTop(6).LineHorizontal(PdfDesign.GridLineWidth).LineColor(PdfDesign.GridGray);
        }

        // ── Build report PDF (Prompt.md design) ─────────────────
        public static void Build(string filePath, string reportTitle, string reportFilters,
            ReportResult result, ReportPdfOptions options)
        {
            var company = CompanyHelper.GetCompanyDetails();
            var groups = options.SeparateReportPerEmployee
                ? GroupByEmployee(result)
                : new List<List<object?[]>> { result.Rows };

            QuestPDF.Settings.License = LicenseType.Community;

            if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                filePath = System.IO.Path.ChangeExtension(filePath, ".pdf");

            // Auto-landscape for wide reports (e.g. Monthly Summary).
            bool useLandscape = options.Orientation == "Landscape"
                                || (options.PaperName == "A4" && result.Columns.Count > 10);

            int colCount = result.Columns.Count;
            float dynamicFont = GetDynamicFontSize(colCount);

            Document.Create(container =>
            {
                foreach (var group in groups)
                {
                    container.Page(page =>
                    {
                        var (pw, ph) = ResolvePageSizePts(options);
                        if (useLandscape) (pw, ph) = (ph, pw);
                        page.Size(pw, ph);

                        float hMargin = PdfDesign.HorizontalMargin;
                        page.MarginHorizontal(hMargin);
                        float topMargin = PdfDesign.VerticalMargin;
                        page.MarginTop(topMargin);
                        page.MarginBottom(PdfDesign.VerticalMargin);

                        page.DefaultTextStyle(x => x.FontSize(dynamicFont).FontFamily(PdfDesign.BodyFont));

                        if (options.RepeatCompanyHeaderOnEveryPage)
                            page.Header().Column(col => DrawCompanyHeader(col, company, reportTitle, reportFilters));

                        page.Content().PaddingTop(4).Table(table =>
                        {
                            var headers = result.Columns;
                            var rowStrings = group.Select(r => headers.Select((h, i) => i < r.Length ? r[i]?.ToString() ?? "" : "").ToList()).ToList();
                            float usableWidth = pw - 2 * hMargin;
                            float[] colWidths = CalculateColumnWidths(headers, rowStrings, usableWidth);

                            var totalWidth = colWidths.Sum();
                            if (totalWidth <= 0) totalWidth = usableWidth;
                            var percents = colWidths.Select(w => w / totalWidth * 100f).ToArray();

                            table.ColumnsDefinition(cols =>
                            {
                                for (int c = 0; c < headers.Count; c++)
                                    cols.RelativeColumn(percents[c]);
                            });

                            // Header row
                            int hdrCount = headers.Count;
                            table.Header(header =>
                            {
                                for (int c = 0; c < hdrCount; c++)
                                {
                                    header.Cell()
                                        .Background(PdfDesign.NavyText)
                                        .PaddingLeft(2).PaddingRight(2).PaddingTop(3).PaddingBottom(3)
                                        .AlignCenter().AlignMiddle()
                                        .Text(headers[c] ?? "")
                                        .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(dynamicFont)
                                        .FontColor(Colors.White);
                                }
                            });

                            // Body rows (alternating + merged markers)
                            for (int r = 0; r < group.Count; r++)
                            {
                                var row = group[r];
                                int inTimeIdx = headers.IndexOf("In Time");
                                string? merged = inTimeIdx >= 0 && inTimeIdx < row.Length
                                    ? MergedCellText(row[inTimeIdx]?.ToString()) : null;

                                for (int c = 0; c < hdrCount; c++)
                                {
                                    if (merged != null && c == inTimeIdx)
                                    {
                                        int span = hdrCount - c;
                                        table.Cell()
                                            .ColumnSpan((uint)span)
                                            .Background(PdfDesign.MergedCellBg)
                                            .PaddingLeft(2).PaddingRight(2).PaddingTop(3).PaddingBottom(3)
                                            .AlignCenter().AlignMiddle()
                                            .Text(merged)
                                            .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(dynamicFont)
                                            .FontColor(PdfDesign.NavyText);
                                        break;
                                    }

                                    string text = c < row.Length ? row[c]?.ToString() ?? "" : "";
                                    string display = MergedCellText(text) ?? text;

                                    // NOTE: QuestPDF containers are single-use — every decorator
                                    // must be chained. Calling .Background() and .Text() on the
                                    // same instance throws "multiple child elements".
                                    IContainer cell = table.Cell();
                                    if (r % 2 == 1)
                                        cell = cell.Background(PdfDesign.AltRowBg);

                                    cell.BorderBottom(PdfDesign.GridLineWidth).BorderColor(PdfDesign.GridGray)
                                        .PaddingLeft(2).PaddingRight(2).PaddingTop(3).PaddingBottom(3)
                                        .AlignCenter().AlignMiddle()
                                        .Text(display)
                                        .FontFamily(PdfDesign.BodyFont).FontSize(dynamicFont)
                                        .FontColor(Colors.Black);
                                }
                            }
                        });

                        // Footer: "Page X of Y" bottom-right, Helvetica 8pt
                        page.Footer().AlignRight().PaddingRight(0).Text(t =>
                        {
                            t.Span("Page ").FontFamily(PdfDesign.PageNumberFont).FontSize(8).FontColor(Colors.Black);
                            t.CurrentPageNumber().FontFamily(PdfDesign.PageNumberFont).FontSize(8).FontColor(Colors.Black);
                            t.Span(" of ").FontFamily(PdfDesign.PageNumberFont).FontSize(8).FontColor(Colors.Black);
                            t.TotalPages().FontFamily(PdfDesign.PageNumberFont).FontSize(8).FontColor(Colors.Black);
                        });
                    });
                }
            }).GeneratePdf(filePath);
        }

        // ── Build employee info PDF (bulk list) ─────────────────
        public static void BuildEmployeeInfoPdf(string filePath, List<Employee> employees,
            CompanyDetails? company = null, ReportPdfOptions? options = null)
        {
            company ??= CompanyHelper.GetCompanyDetails();
            options ??= new ReportPdfOptions();

            QuestPDF.Settings.License = LicenseType.Community;
            if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                filePath = System.IO.Path.ChangeExtension(filePath, ".pdf");

            float fontSize = employees.Count > 14 ? 6.2f : 7.5f;
            float topMargin = PdfDesign.VerticalMargin;

            var columns = new[] { "SL", "Name", "EmpID", "Gender", "Designation", "Section", "Department", "Shift", "Category", "Status" };
            var rows = employees.Select(e => new[]
            {
                e.SL.ToString(), e.Name, e.EmpID?.ToString() ?? "", e.Gender ?? "", e.Designation ?? "",
                e.Section ?? "", e.Department ?? "", e.Shift ?? "", e.Category ?? "", e.Status ?? ""
            }).ToList();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    var (pw, ph) = ResolvePageSizePts(options);
                    page.Size(pw, ph);
                    page.MarginHorizontal(PdfDesign.HorizontalMargin);
                    page.MarginTop(topMargin);
                    page.MarginBottom(PdfDesign.VerticalMargin);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(PdfDesign.BodyFont));

                    if (options.RepeatCompanyHeaderOnEveryPage)
                        page.Header().Column(col => DrawCompanyHeader(col, company,
                            "Employee Information", $"Total Employees: {employees.Count}"));

                    page.Content().PaddingTop(6).Column(col =>
                    {
                        col.Item().AlignCenter().Text("Employee Information")
                            .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(13).FontColor(PdfDesign.TitleBlue);
                        col.Item().AlignCenter().Text($"Total Employees: {employees.Count}")
                            .FontFamily(PdfDesign.BodyFont).FontSize(9);
                        col.Item().PaddingTop(8).Table(table =>
                        {
                            float usableWidth = pw - 2 * PdfDesign.HorizontalMargin;
                            table.ColumnsDefinition(cols =>
                            {
                                for (int c = 0; c < columns.Length; c++)
                                    cols.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                foreach (var h in columns)
                                    header.Cell()
                                        .Background(PdfDesign.EmpHeaderBg)
                                        .PaddingLeft(2).PaddingRight(2).PaddingTop(3).PaddingBottom(3)
                                        .AlignCenter().AlignMiddle()
                                        .Text(h).FontFamily(PdfDesign.HeaderFont).Bold().FontSize(fontSize)
                                        .FontColor(PdfDesign.NavyText).ClampLines(1);
                            });

                            for (int r = 0; r < rows.Count; r++)
                            {
                                for (int c = 0; c < columns.Length; c++)
                                {
                                    // Same single-use rule as above: chain via reassignment.
                                    IContainer cell = table.Cell();
                                    if (r % 2 == 1)
                                        cell = cell.Background(PdfDesign.AltRowBg);
                                    cell.BorderBottom(PdfDesign.GridLineWidth).BorderColor(PdfDesign.GridGray)
                                        .PaddingLeft(2).PaddingRight(2).PaddingTop(3).PaddingBottom(3)
                                        .AlignCenter().AlignMiddle()
                                        .Text(rows[r][c]).FontFamily(PdfDesign.BodyFont).FontSize(fontSize).ClampLines(1);
                                }
                            }
                        });
                    });

                    page.Footer().AlignRight().Text(t =>
                    {
                        t.Span("Page ").FontFamily(PdfDesign.PageNumberFont).FontSize(8);
                        t.CurrentPageNumber().FontFamily(PdfDesign.PageNumberFont).FontSize(8);
                        t.Span(" of ").FontFamily(PdfDesign.PageNumberFont).FontSize(8);
                        t.TotalPages().FontFamily(PdfDesign.PageNumberFont).FontSize(8);
                    });
                });
            }).GeneratePdf(filePath);
        }

        // ── Build individual employee profile PDF ───────────────
        // Shows ALL employee fields in a beautiful, sectioned profile layout.
        public static void BuildEmployeeProfilePdf(string filePath, Employee employee,
            CompanyDetails? company = null, ReportPdfOptions? options = null)
        {
            company ??= CompanyHelper.GetCompanyDetails();
            options ??= new ReportPdfOptions();

            QuestPDF.Settings.License = LicenseType.Community;
            if (!filePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                filePath = System.IO.Path.ChangeExtension(filePath, ".pdf");

            float topMargin = PdfDesign.VerticalMargin;
            string empCode = string.IsNullOrWhiteSpace(employee.EmployeeCode) ? "-" : employee.EmployeeCode.Trim();
            string empName = string.IsNullOrWhiteSpace(employee.Name) ? "-" : employee.Name.Trim();
            string employeeName = $"{empName} ({empCode})";

            string grossDisplay = employee.GrossWagesDisplay;
            if (employee.GrossWages.HasValue)
                grossDisplay += " BDT";

            string dojDisplay = FormatDojDisplay(employee.DOJ, out string serviceLength);

            bool isActive = string.Equals(employee.Status?.Trim(), "Active", StringComparison.OrdinalIgnoreCase);
            bool isResigned = string.Equals(employee.Status?.Trim(), "Resigned", StringComparison.OrdinalIgnoreCase);
            string statusBg = isActive ? "#DCFCE7" : isResigned ? "#FEE2E2" : "#FEF3C7";
            string statusColor = isActive ? "#166534" : isResigned ? "#991B1B" : "#92400E";

            string avatarInitial = empName != "-" ? empName.Trim().Substring(0, 1).ToUpperInvariant() : "?";
            string designationLine = JoinNonEmpty(" • ", employee.Designation, employee.Department);
            if (string.IsNullOrEmpty(designationLine))
                designationLine = DisplayOrDash(employee.Section);
            string subLine = JoinNonEmpty("  |  ", designationLine, string.IsNullOrWhiteSpace(employee.Section) ? null : $"Section: {employee.Section.Trim()}");

            var jobRows = new List<KeyValuePair<string, string>>
            {
                new("SL (Serial)", employee.SL > 0 ? employee.SL.ToString() : "-"),
                new("Emp ID", empCode),
                new("Designation", DisplayOrDash(employee.Designation)),
                new("Section", DisplayOrDash(employee.Section)),
                new("Department", DisplayOrDash(employee.Department)),
                new("Shift", DisplayOrDash(employee.Shift)),
                new("Category", DisplayOrDash(employee.Category)),
                new("Status", DisplayOrDash(employee.Status)),
                new("Date of Joining", dojDisplay),
                new("Service Length", serviceLength),
                new("Gross Wages", grossDisplay),
            };

            var personalRows = new List<KeyValuePair<string, string>>
            {
                new("Full Name", empName),
                new("Father's Name", DisplayOrDash(employee.FatherName)),
                new("Gender", DisplayOrDash(employee.Gender)),
                new("Religion", DisplayOrDash(employee.Religion)),
                new("NID", DisplayOrDash(employee.NID)),
            };

            var paymentRows = new List<KeyValuePair<string, string>>
            {
                new("Rocket Account", DisplayOrDash(employee.RocketAC)),
                new("Present Address", DisplayOrDash(employee.PresAddress)),
                new("Permanent Address", DisplayOrDash(employee.PermAddress)),
            };

            string generatedOn = DateTime.Now.ToString("dd MMM yyyy, hh:mm tt");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    var (pw, ph) = ResolvePageSizePts(options);
                    page.Size(pw, ph);
                    page.MarginHorizontal(PdfDesign.HorizontalMargin);
                    page.MarginTop(topMargin);
                    page.MarginBottom(PdfDesign.VerticalMargin);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily(PdfDesign.BodyFont));

                    if (options.RepeatCompanyHeaderOnEveryPage)
                        page.Header().Column(col => DrawCompanyHeader(col, company,
                            "Employee Profile", employeeName));

                    page.Content().PaddingTop(4).Column(col =>
                    {
                        // ── Title ──
                        col.Item().AlignCenter().Text("Employee Individual Report")
                            .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(14).FontColor(PdfDesign.TitleBlue);
                        col.Item().PaddingTop(1).AlignCenter().Text($"Complete profile information of {employeeName}")
                            .FontFamily(PdfDesign.BodyFont).FontSize(8.5f).FontColor("#6B7280");

                        // ── Identity banner ──
                        col.Item().PaddingTop(8).Background(PdfDesign.NavyText).Padding(12).Row(row =>
                        {
                            row.ConstantItem(52).AlignMiddle().AlignCenter()
                                .Background(Colors.White).Padding(9)
                                .Text(avatarInitial)
                                .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(24).FontColor(PdfDesign.NavyText);

                            row.RelativeItem().PaddingLeft(12).Column(inner =>
                            {
                                inner.Item().Text(empName)
                                    .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(16).FontColor(Colors.White);
                                if (!string.IsNullOrWhiteSpace(subLine))
                                    inner.Item().PaddingTop(2).Text(subLine)
                                        .FontFamily(PdfDesign.BodyFont).FontSize(9).FontColor("#C7D2FE");

                                inner.Item().PaddingTop(7).Row(badges =>
                                {
                                    badges.AutoItem().Background(Colors.White)
                                        .PaddingLeft(9).PaddingRight(9).PaddingTop(3).PaddingBottom(3)
                                        .Text($"ID: {empCode}")
                                        .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(8.5f).FontColor(PdfDesign.NavyText);
                                    badges.AutoItem().PaddingLeft(6).Background(statusBg)
                                        .PaddingLeft(9).PaddingRight(9).PaddingTop(3).PaddingBottom(3)
                                        .Text(DisplayOrDash(employee.Status))
                                        .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(8.5f).FontColor(statusColor);
                                    if (employee.SL > 0)
                                        badges.AutoItem().PaddingLeft(6).Background("#312E81")
                                            .PaddingLeft(9).PaddingRight(9).PaddingTop(3).PaddingBottom(3)
                                            .Text($"SL: {employee.SL}")
                                            .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(8.5f).FontColor(Colors.White);
                                });
                            });
                        });

                        // ── Quick stat cards ──
                        col.Item().PaddingTop(8).Row(stats =>
                        {
                            DrawProfileStatCard(stats.RelativeItem(), "GROSS WAGES", grossDisplay);
                            stats.RelativeItem().PaddingLeft(6).Element(e => DrawProfileStatCard(e, "JOINING DATE", dojDisplay));
                            stats.RelativeItem().PaddingLeft(6).Element(e => DrawProfileStatCard(e, "SERVICE LENGTH", serviceLength));
                            stats.RelativeItem().PaddingLeft(6).Element(e => DrawProfileStatCard(e, "SHIFT / CATEGORY",
                                $"{DisplayOrDash(employee.Shift)} / {DisplayOrDash(employee.Category)}"));
                        });

                        // ── Grouped detail sections (all fields) ──
                        col.Item().PaddingTop(10).Element(e => DrawProfileSection(e, "1. Employment / Job Information", jobRows));
                        col.Item().PaddingTop(8).Element(e => DrawProfileSection(e, "2. Personal Information", personalRows));
                        col.Item().PaddingTop(8).Element(e => DrawProfileSection(e, "3. Address & Payment Information", paymentRows));

                        // ── Signature block ──
                        col.Item().PaddingTop(16).Row(sign =>
                        {
                            DrawSignatureCell(sign.RelativeItem(), "Prepared By");
                            sign.RelativeItem().PaddingLeft(24).Element(e => DrawSignatureCell(e, "Checked By"));
                            sign.RelativeItem().PaddingLeft(24).Element(e => DrawSignatureCell(e, "Approved By"));
                        });
                        col.Item().PaddingTop(6).Text($"Generated on: {generatedOn}  •  Total fields: {jobRows.Count + personalRows.Count + paymentRows.Count}")
                            .FontFamily(PdfDesign.BodyFont).FontSize(7.5f).FontColor("#6B7280");
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().AlignLeft()
                            .Text($"Generated: {generatedOn}")
                            .FontFamily(PdfDesign.PageNumberFont).FontSize(7.5f).FontColor("#6B7280");
                        row.ConstantItem(110).AlignRight().Text(t =>
                        {
                            t.Span("Page ").FontFamily(PdfDesign.PageNumberFont).FontSize(8).FontColor(Colors.Black);
                            t.CurrentPageNumber().FontFamily(PdfDesign.PageNumberFont).FontSize(8).FontColor(Colors.Black);
                            t.Span(" of ").FontFamily(PdfDesign.PageNumberFont).FontSize(8).FontColor(Colors.Black);
                            t.TotalPages().FontFamily(PdfDesign.PageNumberFont).FontSize(8).FontColor(Colors.Black);
                        });
                    });
                });
            }).GeneratePdf(filePath);
        }

        private static string DisplayOrDash(string? value)
            => string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();

        private static string JoinNonEmpty(string separator, params string?[] parts)
            => string.Join(separator, parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

        private static string FormatDojDisplay(string? doj, out string serviceLength)
        {
            serviceLength = "-";
            if (string.IsNullOrWhiteSpace(doj))
                return "-";

            if (!DateTime.TryParse(doj.Trim(), out var joinDate))
                return doj.Trim();

            string formatted = joinDate.ToString("dd MMM yyyy");
            try
            {
                var today = DateTime.Today;
                if (joinDate.Date > today)
                {
                    serviceLength = "0 month(s)";
                    return formatted;
                }
                int years = today.Year - joinDate.Year;
                int months = today.Month - joinDate.Month;
                if (today.Day < joinDate.Day) months--;
                if (months < 0) { years--; months += 12; }
                if (years < 0) { years = 0; months = 0; }
                serviceLength = years > 0 ? $"{years} yr(s) {months} mo(s)" : $"{months} mo(s)";
            }
            catch { serviceLength = "-"; }
            return formatted;
        }

        private static void DrawProfileStatCard(IContainer container, string label, string value)
        {
            container.Background(PdfDesign.LabelBg)
                .Border(PdfDesign.GridLineWidth).BorderColor(PdfDesign.GridGray)
                .PaddingLeft(7).PaddingRight(7).PaddingTop(6).PaddingBottom(6)
                .Column(c =>
                {
                    c.Item().Text(label)
                        .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(7).FontColor("#6B7280");
                    c.Item().PaddingTop(2).Text(string.IsNullOrWhiteSpace(value) ? "-" : value)
                        .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(9).FontColor(PdfDesign.NavyText);
                });
        }

        private static void DrawProfileSection(IContainer container, string title, List<KeyValuePair<string, string>> rows)
        {
            container.Column(col =>
            {
                col.Item().Background(PdfDesign.NavyText)
                    .PaddingLeft(9).PaddingRight(9).PaddingTop(6).PaddingBottom(6)
                    .Text(title)
                    .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(10).FontColor(Colors.White);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(32);
                        cols.RelativeColumn(68);
                    });

                    foreach (var row in rows)
                    {
                        table.Cell()
                            .Background(PdfDesign.LabelBg)
                            .Border(PdfDesign.GridLineWidth).BorderColor(PdfDesign.GridGray)
                            .PaddingLeft(8).PaddingRight(8).PaddingTop(5).PaddingBottom(5)
                            .AlignLeft().AlignMiddle()
                            .Text(row.Key)
                            .FontFamily(PdfDesign.HeaderFont).Bold().FontSize(8.5f).FontColor(PdfDesign.NavyText);

                        table.Cell()
                            .Background(PdfDesign.White)
                            .Border(PdfDesign.GridLineWidth).BorderColor(PdfDesign.GridGray)
                            .PaddingLeft(8).PaddingRight(8).PaddingTop(5).PaddingBottom(5)
                            .AlignLeft().AlignMiddle()
                            .Text(string.IsNullOrWhiteSpace(row.Value) ? "-" : row.Value)
                            .FontFamily(PdfDesign.BodyFont).FontSize(8.5f).FontColor(Colors.Black);
                    }
                });
            });
        }

        private static void DrawSignatureCell(IContainer container, string label)
        {
            container.Column(c =>
            {
                c.Item().PaddingTop(18).LineHorizontal(PdfDesign.GridLineWidth).LineColor("#111827");
                c.Item().PaddingTop(3).AlignCenter().Text(label)
                    .FontFamily(PdfDesign.BodyFont).FontSize(8).FontColor("#374151");
            });
        }

        // ── Helpers ─────────────────────────────────────────────
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

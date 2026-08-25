using APRsystem.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace APRsystem.Services
{
    public class AppraisalPdfService
    {
        private readonly string? _logoPath;

        public AppraisalPdfService(IWebHostEnvironment env)
        {
            var path = Path.Combine(env.WebRootPath, "images", "ppaf-logo.png");
            // Guard against a missing file so a bad deploy doesn't crash every export —
            // ComposeAppraisalPage checks this before trying to render the image.
            _logoPath = File.Exists(path) ? path : null;
        }

        public byte[] GenerateSingle(Appraisal appraisal)
        {
            var doc = Document.Create(container =>
            {
                ComposeAppraisalPage(container, appraisal);
            });

            return doc.GeneratePdf();
        }

        // Multiple appraisals in one file, each starting on its own page.
        public byte[] GenerateBulk(List<Appraisal> appraisals)
        {
            var doc = Document.Create(container =>
            {
                foreach (var appraisal in appraisals)
                {
                    ComposeAppraisalPage(container, appraisal);
                }
            });

            return doc.GeneratePdf();
        }

        private void ComposeAppraisalPage(IDocumentContainer container, Appraisal a)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Row(row =>
                {
                    if (_logoPath != null)
                    {
                        row.ConstantItem(240).Image(_logoPath);
                    }

                    row.RelativeItem().PaddingLeft(_logoPath != null ? 10 : 0).Column(col =>
                    {
                        col.Item().Text($"Appraisal — {a.Employee?.FullName}").FontSize(16).Bold();
                        col.Item().Text($"{a.FromDate:dd MMM yyyy} — {a.ToDate:dd MMM yyyy}   |   Status: {a.Status?.Label}");
                        col.Item().Text($"Supervisor: {a.Supervisor?.FullName ?? "—"}   |   Reviewer: {a.Reviewer?.FullName ?? "—"}");
                        col.Item().PaddingTop(5).LineHorizontal(1);
                    });
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    // ---- Rejection feedback, if applicable ----
                    if (a.Status?.Value == "Rejected" && !string.IsNullOrWhiteSpace(a.ReviewerComments))
                    {
                        col.Item().PaddingBottom(10).Background(Colors.Red.Lighten4).Padding(8).Column(rc =>
                        {
                            rc.Item().Text("Reviewer Feedback").Bold();
                            rc.Item().Text(a.ReviewerComments);
                        });
                    }

                    // ---- General KPIs ----
                    col.Item().Text("General KPIs").Bold().FontSize(12);
                    ComposeKpiTable(col, a.AppraisalKPIs.Where(k => k.Section == KPISection.General).ToList());
                    col.Item().PaddingTop(4).Text($"Total: {a.GeneralTotalScore} / {a.GeneralMaxScore}");

                    col.Item().PaddingTop(10).Text("Employee's Comment (General)").Bold();
                    col.Item().Text(string.IsNullOrWhiteSpace(a.SelfGeneralComment) ? "—" : a.SelfGeneralComment);

                    col.Item().PaddingTop(8).Text("Supervisor's Comment (General)").Bold();
                    col.Item().Text(string.IsNullOrWhiteSpace(a.SupervisorGeneralComment) ? "—" : a.SupervisorGeneralComment);

                    // ---- Posting-Specific KPIs — each KPI's comment sits directly below its own row ----
                    var specificKpis = a.AppraisalKPIs.Where(k => k.Section == KPISection.Specific).ToList();
                    col.Item().PaddingTop(15).Text("Posting-Specific KPIs").Bold().FontSize(12);
                    ComposeSpecificKpiTable(col, specificKpis);
                    col.Item().PaddingTop(4).Text($"Total: {a.SpecificTotalScore} / {a.SpecificMaxScore}");

                    // Supervisor's single collective comment for the whole Specific section
                    col.Item().PaddingTop(8).Text("Supervisor's Comment (Specific)").Bold();
                    col.Item().Text(string.IsNullOrWhiteSpace(a.SupervisorSpecificComment) ? "—" : a.SupervisorSpecificComment);

                    // ---- Grand Total ----
                    col.Item().PaddingTop(15).Text($"Grand Total: {a.GrandTotalScore} / {a.GrandMaxScore}  ({a.Percentage}%)").Bold();
                    col.Item().Text($"Ranking Band: {(string.IsNullOrWhiteSpace(a.RankingBand) ? "—" : a.RankingBand)}");

                    // ---- Employee's closing comment on supervisor's rating — always shown, so the
                    // exported PDF reflects the full workflow even if this stage hasn't been reached yet ----
                    col.Item().PaddingTop(15).Text("Employee's Comment on Supervisor Rating").Bold().FontSize(12);
                    col.Item().Text(string.IsNullOrWhiteSpace(a.EmployeeFinalComment) ? "—" : a.EmployeeFinalComment);

                    // ---- Performance Appraisal Ranking (Supervisor's Remarks, given alongside KPI scoring) ----
                    

                    // ---- Supervisor's final rank (given after employee's comment) ----
                    col.Item().PaddingTop(15).Text("Supervisor's Final Rank").Bold().FontSize(12);
                    col.Item().Text($"Final Rank: {RankLabel(a.SupervisorFinalRank)}");
                    col.Item().Text($"Comment: {(string.IsNullOrWhiteSpace(a.SupervisorRankComment) ? "—" : a.SupervisorRankComment)}");

                    // ---- Final Ranking (HR + Final Reviewer) — always shown ----
                    col.Item().PaddingTop(15).Text("Final Ranking (HR & Final Reviewer)").Bold().FontSize(12);
                    if (a.ReviewedOn.HasValue)
                    {
                        col.Item().Text($"Reviewed on: {a.ReviewedOn.Value:dd MMM yyyy}");
                    }
                    col.Item().Text($"HR Remarks: {(string.IsNullOrWhiteSpace(a.HRRemarks) ? "—" : a.HRRemarks)}");
                    col.Item().Text($"Final Reviewer's Remarks: {(string.IsNullOrWhiteSpace(a.ReviewerComments) ? "—" : a.ReviewerComments)}");
                    col.Item().Text($"Final Rank: {RankLabel(a.FinalRank)}");
                    col.Item().Text($"Action Required: {(string.IsNullOrWhiteSpace(a.ActionRequired) ? "—" : a.ActionRequired)}");
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }

        private static string RankLabel(string? rank) => rank switch
        {
            "OS" => "Outstanding (OS)",
            "AE" => "Above Expectations (AE)",
            "ME" => "Meets Expectations (ME)",
            "BE" => "Below Expectations (BE)",
            "NI" => "Needs Improvement (NI)",
            _ => "—"
        };

        // General KPIs — plain table, no per-row comments (General doesn't have per-KPI comments).
        private void ComposeKpiTable(ColumnDescriptor col, List<AppraisalKPI> kpis)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Title").Bold();
                    header.Cell().Text("Weight").Bold();
                    header.Cell().Text("Self").Bold();
                    header.Cell().Text("Supervisor").Bold();
                });

                foreach (var k in kpis)
                {
                    table.Cell().Text(k.Title);
                    table.Cell().Text(k.Weight.ToString());
                    table.Cell().Text($"{k.SelfRating}/4");
                    table.Cell().Text($"{k.Rating}/4");
                }
            });
        }

        // Posting-Specific KPIs — each KPI's row is immediately followed by a full-width
        // row holding that KPI's own employee comment, so the comment sits directly under
        // the KPI it belongs to instead of in a separate list further down the page.
        private void ComposeSpecificKpiTable(ColumnDescriptor col, List<AppraisalKPI> kpis)
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    header.Cell().Text("Title").Bold();
                    header.Cell().Text("Weight").Bold();
                    header.Cell().Text("Self").Bold();
                    header.Cell().Text("Supervisor").Bold();
                });

                foreach (var k in kpis)
                {
                    table.Cell().Text(k.Title);
                    table.Cell().Text(k.Weight.ToString());
                    table.Cell().Text($"{k.SelfRating}/4");
                    table.Cell().Text($"{k.Rating}/4");

                    table.Cell().ColumnSpan(4).Padding(4).Background(Colors.Grey.Lighten4).Column(cc =>
                    {
                        cc.Item().Text("Employee's Comment").FontSize(8).SemiBold().FontColor(Colors.Grey.Darken1);
                        cc.Item().Text(string.IsNullOrWhiteSpace(k.SelfComment) ? "No comment provided." : k.SelfComment);
                    });
                }
            });
        }
    }
}
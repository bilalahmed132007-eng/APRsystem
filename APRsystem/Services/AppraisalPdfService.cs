using APRsystem.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace APRsystem.Services
{
    public class AppraisalPdfService
    {
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

                page.Header().Column(col =>
                {
                    col.Item().Text($"Appraisal — {a.Employee?.FullName}").FontSize(16).Bold();
                    col.Item().Text($"{a.FromDate:dd MMM yyyy} — {a.ToDate:dd MMM yyyy}   |   Status: {a.Status?.Label}");
                    col.Item().PaddingTop(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Text("General KPIs").Bold().FontSize(12);
                    ComposeKpiTable(col, a.AppraisalKPIs.Where(k => k.Section == KPISection.General).ToList());
                    col.Item().PaddingTop(4).Text($"Total: {a.GeneralTotalScore} / {a.GeneralMaxScore}");

                    col.Item().PaddingTop(15).Text("Posting-Specific KPIs").Bold().FontSize(12);
                    ComposeKpiTable(col, a.AppraisalKPIs.Where(k => k.Section == KPISection.Specific).ToList());
                    col.Item().PaddingTop(4).Text($"Total: {a.SpecificTotalScore} / {a.SpecificMaxScore}");

                    col.Item().PaddingTop(15).Text($"Grand Total: {a.GrandTotalScore} / {a.GrandMaxScore}  ({a.Percentage}%)").Bold();
                    col.Item().Text($"Ranking Band: {a.RankingBand}");

                    col.Item().PaddingTop(15).Text("Employee's Comment (General)").Bold();
                    col.Item().Text(string.IsNullOrWhiteSpace(a.SelfGeneralComment) ? "—" : a.SelfGeneralComment);

                    col.Item().PaddingTop(8).Text("Supervisor's Comment (General)").Bold();
                    col.Item().Text(string.IsNullOrWhiteSpace(a.SupervisorGeneralComment) ? "—" : a.SupervisorGeneralComment);

                    col.Item().PaddingTop(8).Text("Employee's Comment (Specific)").Bold();
                    col.Item().Text(string.IsNullOrWhiteSpace(a.SelfSpecificComment) ? "—" : a.SelfSpecificComment);

                    col.Item().PaddingTop(8).Text("Supervisor's Comment (Specific)").Bold();
                    col.Item().Text(string.IsNullOrWhiteSpace(a.SupervisorSpecificComment) ? "—" : a.SupervisorSpecificComment);

                    if (!string.IsNullOrWhiteSpace(a.FinalRank) || !string.IsNullOrWhiteSpace(a.HRRemarks) || !string.IsNullOrWhiteSpace(a.ReviewerComments))
                    {
                        col.Item().PaddingTop(15).Text("Final Ranking").Bold().FontSize(12);
                        col.Item().Text($"HR Remarks: {(string.IsNullOrWhiteSpace(a.HRRemarks) ? "—" : a.HRRemarks)}");
                        col.Item().Text($"Final Reviewer's Remarks: {(string.IsNullOrWhiteSpace(a.ReviewerComments) ? "—" : a.ReviewerComments)}");
                        col.Item().Text($"Final Rank: {(string.IsNullOrWhiteSpace(a.FinalRank) ? "—" : a.FinalRank)}");
                    }
                });

                page.Footer().AlignCenter().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        }

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
    }
}
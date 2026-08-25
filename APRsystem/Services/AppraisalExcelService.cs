using APRsystem.Models;
using ClosedXML.Excel;

namespace APRsystem.Services
{
    public class AppraisalExcelService
    {
        // Palette — kept in one place so header/section colors stay consistent
        private static readonly XLColor TitleBg = XLColor.FromHtml("#1F3864");
        private static readonly XLColor TitleFg = XLColor.White;
        private static readonly XLColor HeaderBg = XLColor.FromHtml("#2E5090");
        private static readonly XLColor HeaderFg = XLColor.White;
        private static readonly XLColor BandBg = XLColor.FromHtml("#F2F5FA");
        private static readonly XLColor BorderColor = XLColor.FromHtml("#D9D9D9");
        private static readonly XLColor GeneralSectionBg = XLColor.FromHtml("#E7EDF6");
        private static readonly XLColor SpecificSectionBg = XLColor.FromHtml("#EFF7EF");

        public byte[] GenerateAnalyticsExport(List<Appraisal> appraisals)
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Appraisal Analytics");

            // One row per employee — General/Specific are shown as their totals only,
            // not broken down KPI-by-KPI. This is a summary/analytics view, not the
            // detailed KPI sheet (that's what the per-employee PDF export is for).
            string[] headers =
            {
                "Employee No", "Employee Name", "Department", "Designation",
                "From Date", "To Date", "Status",
                "General KPIs Score", "General KPIs Max",
                "Specific KPIs Score", "Specific KPIs Max",
                "Grand Total", "Grand Max", "Percentage", "Ranking Band", "Final Rank",
                "Employee Comment (General)", "Supervisor Comment (General)",
                "Employee Comment (Specific)", "Supervisor Comment (Specific)",
                "HR Remarks", "Final Reviewer's Remarks"
            };

            int colCount = headers.Length;

            // ---- Title block ----------------------------------------------------
            sheet.Range(1, 1, 1, colCount).Merge();
            var titleCell = sheet.Cell(1, 1);
            titleCell.Value = "Appraisal Analytics Report";
            titleCell.Style.Font.FontSize = 16;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontColor = TitleFg;
            titleCell.Style.Fill.BackgroundColor = TitleBg;
            titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            titleCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            sheet.Row(1).Height = 30;

            sheet.Range(2, 1, 2, colCount).Merge();
            var subtitleCell = sheet.Cell(2, 1);
            subtitleCell.Value = $"Generated {DateTime.Today:dd MMM yyyy}  •  {appraisals.Count} appraisal(s)";
            subtitleCell.Style.Font.FontSize = 10;
            subtitleCell.Style.Font.Italic = true;
            subtitleCell.Style.Font.FontColor = XLColor.FromHtml("#5A5A5A");
            subtitleCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");
            subtitleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            sheet.Row(2).Height = 18;

            // ---- Column headers ---------------------------------------------------
            const int headerRow = 4;
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = sheet.Cell(headerRow, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = HeaderFg;
                cell.Style.Fill.BackgroundColor = HeaderBg;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.OutsideBorderColor = BorderColor;
            }
            sheet.Row(headerRow).Height = 28;

            // Light section tint over the two KPI-group header pairs, so it's visually
            // obvious which columns belong to General vs Specific at a glance.
            sheet.Range(headerRow, 8, headerRow, 9).Style.Fill.BackgroundColor = XLColor.FromHtml("#3D6BB3");
            sheet.Range(headerRow, 10, headerRow, 11).Style.Fill.BackgroundColor = XLColor.FromHtml("#3D8B5F");

            // ---- Data rows: one row per employee ----------------------------------
            int row = headerRow + 1;
            int dataStartRow = row;

            foreach (var a in appraisals)
            {
                WriteRow(sheet, row, a);
                row++;
            }

            int lastDataRow = row - 1;

            // ---- Borders + number formatting across the full data range ----------
            if (lastDataRow >= dataStartRow)
            {
                var dataRange = sheet.Range(dataStartRow, 1, lastDataRow, colCount);
                dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.InsideBorderColor = BorderColor;
                dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRange.Style.Border.OutsideBorderColor = BorderColor;
                dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                // comment columns (Employee/Supervisor General & Specific + HR Remarks + Reviewer Remarks)
                sheet.Range(dataStartRow, 17, lastDataRow, 22).Style.Alignment.WrapText = true;

                sheet.Range(dataStartRow, 5, lastDataRow, 6).Style.DateFormat.Format = "dd-MMM-yyyy"; // From/To Date
                sheet.Range(dataStartRow, 14, lastDataRow, 14).Style.NumberFormat.Format = "0.00\"%\"";  // Percentage
                sheet.Range(dataStartRow, 8, lastDataRow, 13).Style.NumberFormat.Format = "0.0";         // score/max columns

                // Center the numeric/short columns
                foreach (var c in new[] { 8, 9, 10, 11, 12, 13, 14, 16 })
                {
                    sheet.Range(dataStartRow, c, lastDataRow, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // Light section tint carried down through the data rows too
                sheet.Range(dataStartRow, 8, lastDataRow, 9).Style.Fill.BackgroundColor = GeneralSectionBg;
                sheet.Range(dataStartRow, 10, lastDataRow, 11).Style.Fill.BackgroundColor = SpecificSectionBg;

                // Color-code Ranking Band so the report is scannable at a glance
                for (int r = dataStartRow; r <= lastDataRow; r++)
                {
                    var bandCell = sheet.Cell(r, 15);
                    var band = bandCell.GetString();
                    bandCell.Style.Font.Bold = true;
                    bandCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    bandCell.Style.Fill.BackgroundColor = band switch
                    {
                        "Outstanding" => XLColor.FromHtml("#C6EFCE"),
                        "Above Expectations" => XLColor.FromHtml("#D9E8FB"),
                        "Meets Expectations" => XLColor.FromHtml("#FFF3CD"),
                        "Below Expectations" => XLColor.FromHtml("#FDE2CE"),
                        "Needs Improvement" => XLColor.FromHtml("#F8D7DA"),
                        _ => XLColor.White
                    };
                }

                // Alternating row banding for the non-KPI columns (KPI columns keep
                // their own dedicated General/Specific tint set above).
                for (int r = dataStartRow; r <= lastDataRow; r++)
                {
                    if ((r - dataStartRow) % 2 == 0) continue; // even rows stay white
                    foreach (var col in new[] { 1, 2, 3, 4, 5, 6, 7, 12, 13, 14, 16, 17, 18, 19, 20, 21, 22 })
                    {
                        sheet.Cell(r, col).Style.Fill.BackgroundColor = BandBg;
                    }
                }
            }

            // ---- Column widths ----------------------------------------------------
            sheet.Column(1).Width = 12;  // Employee No
            sheet.Column(2).Width = 20;  // Employee Name
            sheet.Column(3).Width = 18;  // Department
            sheet.Column(4).Width = 20;  // Designation
            sheet.Column(5).Width = 12;  // From Date
            sheet.Column(6).Width = 12;  // To Date
            sheet.Column(7).Width = 14;  // Status
            sheet.Column(8).Width = 13;  // General KPIs Score
            sheet.Column(9).Width = 12;  // General KPIs Max
            sheet.Column(10).Width = 13; // Specific KPIs Score
            sheet.Column(11).Width = 12; // Specific KPIs Max
            sheet.Column(12).Width = 11; // Grand Total
            sheet.Column(13).Width = 11; // Grand Max
            sheet.Column(14).Width = 11; // Percentage
            sheet.Column(15).Width = 17; // Ranking Band
            sheet.Column(16).Width = 12; // Final Rank
            sheet.Column(17).Width = 30; // Employee Comment (General)
            sheet.Column(18).Width = 30; // Supervisor Comment (General)
            sheet.Column(19).Width = 30; // Employee Comment (Specific)
            sheet.Column(20).Width = 30; // Supervisor Comment (Specific)
            sheet.Column(21).Width = 30; // HR Remarks
            sheet.Column(22).Width = 30; // Final Reviewer's Remarks

            sheet.SheetView.FreezeRows(headerRow);
            if (lastDataRow >= dataStartRow)
            {
                sheet.Range(headerRow, 1, lastDataRow, colCount).SetAutoFilter();
            }

            sheet.ShowGridLines = false;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        private void WriteRow(IXLWorksheet sheet, int row, Appraisal a)
        {
            var c = 1;
            sheet.Cell(row, c++).Value = a.Employee?.EmployeeNo;
            sheet.Cell(row, c++).Value = a.Employee?.FullName;
            sheet.Cell(row, c++).Value = a.Posting?.Department?.Name ?? "-";
            sheet.Cell(row, c++).Value = a.Posting?.Designation?.Value ?? "-";
            sheet.Cell(row, c++).Value = a.FromDate;
            sheet.Cell(row, c++).Value = a.ToDate;
            sheet.Cell(row, c++).Value = a.Status?.Label;
            sheet.Cell(row, c++).Value = a.GeneralTotalScore;
            sheet.Cell(row, c++).Value = a.GeneralMaxScore;
            sheet.Cell(row, c++).Value = a.SpecificTotalScore;
            sheet.Cell(row, c++).Value = a.SpecificMaxScore;
            sheet.Cell(row, c++).Value = a.GrandTotalScore;
            sheet.Cell(row, c++).Value = a.GrandMaxScore;
            sheet.Cell(row, c++).Value = a.Percentage;
            sheet.Cell(row, c++).Value = a.RankingBand;
            sheet.Cell(row, c++).Value = a.FinalRank;
            sheet.Cell(row, c++).Value = a.SelfGeneralComment;
            sheet.Cell(row, c++).Value = a.SupervisorGeneralComment;
            sheet.Cell(row, c++).Value = a.SelfSpecificComment;
            sheet.Cell(row, c++).Value = a.SupervisorSpecificComment;
            sheet.Cell(row, c++).Value = a.HRRemarks;
            sheet.Cell(row, c++).Value = a.ReviewerComments;
        }
    }
}
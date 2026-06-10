using ClosedXML.Excel;
using DigifyCXIntranet.Models;

namespace DigifyCXIntranet.Services;

public class ClosedXmlFileExportService : IFileExportService
{
    public byte[] BuildCanteenBatchWorkbook(
        string worksheetName,
        IEnumerable<CanteenOrder> orders,
        DateTimeOffset generatedAt)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(worksheetName);

        ws.Cell(1, 1).Value = "Employee";
        ws.Cell(1, 2).Value = "Menu Item";
        ws.Cell(1, 3).Value = "Price";
        ws.Cell(1, 4).Value = "Order Time (UTC)";
        ws.Cell(1, 5).Value = "Meal Slot";
        ws.Cell(1, 6).Value = "Status";

        var row = 2;
        foreach (var order in orders)
        {
            ws.Cell(row, 1).Value = order.EmployeeUsername;
            ws.Cell(row, 2).Value = order.ItemSummary;
            ws.Cell(row, 3).Value = order.TotalAmount;
            ws.Cell(row, 4).Value = order.OrderTimeUtc;
            ws.Cell(row, 5).Value = order.MealSlot.ToString();
            ws.Cell(row, 6).Value = order.Status;
            row++;
        }

        ws.Cell(1, 8).Value = "Generated";
        ws.Cell(1, 9).Value = generatedAt.UtcDateTime;
        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] BuildPayrollWorkbook(
        string worksheetName,
        IEnumerable<PayrollExportRow> rows,
        DateTimeOffset generatedAt)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add(worksheetName);

        ws.Cell(1, 1).Value = "Employee";
        ws.Cell(1, 2).Value = "Total Spend";
        ws.Cell(1, 3).Value = "Generated (UTC)";

        var rowNumber = 2;
        foreach (var row in rows)
        {
            ws.Cell(rowNumber, 1).Value = row.EmployeeUsername;
            ws.Cell(rowNumber, 2).Value = row.TotalAmount;
            ws.Cell(rowNumber, 3).Value = generatedAt.UtcDateTime;
            rowNumber++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] BuildCanteenLedgerWorkbook(
        IEnumerable<CanteenLedgerSummaryRow> summaryRows,
        IEnumerable<CanteenOrder> detailRows,
        DateTimeOffset generatedAt)
    {
        using var workbook = new XLWorkbook();

        var summary = workbook.Worksheets.Add("Summary");
        summary.Cell(1, 1).Value = "Employee";
        summary.Cell(1, 2).Value = "Orders";
        summary.Cell(1, 3).Value = "Total Spend";
        summary.Cell(1, 5).Value = "Generated (UTC)";
        summary.Cell(1, 6).Value = generatedAt.UtcDateTime;

        var rowNumber = 2;
        foreach (var row in summaryRows)
        {
            summary.Cell(rowNumber, 1).Value = row.EmployeeUsername;
            summary.Cell(rowNumber, 2).Value = row.OrderCount;
            summary.Cell(rowNumber, 3).Value = row.TotalAmount;
            rowNumber++;
        }

        var detail = workbook.Worksheets.Add("Order Detail");
        detail.Cell(1, 1).Value = "Employee";
        detail.Cell(1, 2).Value = "Order Time (UTC)";
        detail.Cell(1, 3).Value = "Menu Item";
        detail.Cell(1, 4).Value = "Meal Slot";
        detail.Cell(1, 5).Value = "Status";
        detail.Cell(1, 6).Value = "Total";

        rowNumber = 2;
        foreach (var order in detailRows)
        {
            detail.Cell(rowNumber, 1).Value = order.EmployeeUsername;
            detail.Cell(rowNumber, 2).Value = order.OrderTimeUtc;
            detail.Cell(rowNumber, 3).Value = order.ItemSummary;
            detail.Cell(rowNumber, 4).Value = order.MealSlot.ToString();
            detail.Cell(rowNumber, 5).Value = order.Status;
            detail.Cell(rowNumber, 6).Value = order.TotalAmount;
            rowNumber++;
        }

        summary.Columns().AdjustToContents();
        detail.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }
}

using DigifyCXIntranet.Models;

namespace DigifyCXIntranet.Services;

public interface IFileExportService
{
    byte[] BuildCanteenBatchWorkbook(
        string worksheetName,
        IEnumerable<CanteenOrder> orders,
        DateTimeOffset generatedAt);

    byte[] BuildPayrollWorkbook(
        string worksheetName,
        IEnumerable<PayrollExportRow> rows,
        DateTimeOffset generatedAt);

    byte[] BuildCanteenLedgerWorkbook(
        IEnumerable<CanteenLedgerSummaryRow> summaryRows,
        IEnumerable<CanteenOrder> detailRows,
        DateTimeOffset generatedAt);
}

public class PayrollExportRow
{
    public string EmployeeUsername { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

public class CanteenLedgerSummaryRow
{
    public string EmployeeUsername { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal TotalAmount { get; set; }
}

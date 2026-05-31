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
}

public class PayrollExportRow
{
    public string EmployeeUsername { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
}

using DigifyCXIntranet.Models;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Data;

public class CanteenDbContext : DbContext
{
    public CanteenDbContext(DbContextOptions<CanteenDbContext> options)
        : base(options)
    {
    }

    public DbSet<CanteenOrder> CanteenOrders => Set<CanteenOrder>();
    public DbSet<CanteenBatchRun> CanteenBatchRuns => Set<CanteenBatchRun>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<FinanceAuditLog> FinanceAuditLogs => Set<FinanceAuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureCanteenDomain();
    }
}

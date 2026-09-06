using DigifyCXIntranet.Data;
using DigifyCXIntranet.Models;
using DigifyCXIntranet.Options;
using DigifyCXIntranet.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigifyCXIntranet.Tests.Services;

public class FinancialBatchReliabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Canteen_ExistingRunIsNotResentAndIncompleteRunRequiresReview(bool completed)
    {
        using var fixture = new Fixture();
        await using var db = fixture.CreateDb();
        db.CanteenBatchRuns.Add(new CanteenBatchRun { RunKey = "Lunch:20260925", SentSuccessfully = completed });
        await db.SaveChangesAsync();
        var service = fixture.CreateCanteenService(db);
        var run = () => service.RunBatchAsync(MealSlot.Lunch);

        if (completed)
        {
            await run.Should().NotThrowAsync();
        }
        else
        {
            await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Review*email outbox*");
        }

        fixture.Email.SendCount.Should().Be(0);
        fixture.Export.CanteenExportCount.Should().Be(0);
        (await db.CanteenBatchRuns.CountAsync()).Should().Be(1);
    }

    [Theory]
    [InlineData("send")]
    [InlineData("export")]
    [InlineData("cancel")]
    public async Task Canteen_FailureIsPersistedAndPropagatesWithoutResendingOnRetry(string failure)
    {
        using var fixture = new Fixture();
        await using var db = fixture.CreateDb();
        db.CanteenOrders.Add(Order(fixture.Clock.UtcNow.AddHours(-1).UtcDateTime));
        await db.SaveChangesAsync();
        using var cancellation = new CancellationTokenSource();
        fixture.Email.Failure = failure == "send" ? new IOException("Queue unavailable") : null;
        fixture.Export.FailCanteen = failure == "export";
        fixture.Email.BeforeSend = failure == "cancel" ? () => cancellation.Cancel() : null;
        var run = () => fixture.CreateCanteenService(db).RunBatchAsync(MealSlot.Lunch, cancellation.Token);

        if (failure == "cancel")
        {
            await run.Should().ThrowAsync<OperationCanceledException>();
        }
        else
        {
            await run.Should().ThrowAsync<IOException>();
        }

        await using var retryDb = fixture.CreateDb();
        var savedRun = await retryDb.CanteenBatchRuns.SingleAsync();
        savedRun.SentSuccessfully.Should().BeFalse();
        savedRun.ErrorMessage.Should().NotBeNullOrEmpty();
        var retry = () => fixture.CreateCanteenService(retryDb).RunBatchAsync(MealSlot.Lunch);
        await retry.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Review*email outbox*");
        fixture.Email.SendCount.Should().Be(failure == "export" ? 0 : 1);
        fixture.Export.CanteenExportCount.Should().Be(1);
    }

    [Fact]
    public async Task Canteen_SuccessRemainsIdempotentAndKeepsExistingOrderCutoff()
    {
        using var fixture = new Fixture();
        await using var db = fixture.CreateDb();
        var eligible = Order(fixture.Clock.UtcNow.AddDays(-1).UtcDateTime);
        var future = Order(fixture.Clock.UtcNow.AddSeconds(1).UtcDateTime);
        var breakfast = Order(fixture.Clock.UtcNow.AddHours(-1).UtcDateTime);
        breakfast.MealSlot = MealSlot.Breakfast;
        db.CanteenOrders.AddRange(eligible, future, breakfast);
        await db.SaveChangesAsync();
        var service = fixture.CreateCanteenService(db);

        await service.RunBatchAsync(MealSlot.Lunch);
        await service.RunBatchAsync(MealSlot.Lunch);

        fixture.Email.SendCount.Should().Be(1);
        (await db.CanteenBatchRuns.SingleAsync()).SentSuccessfully.Should().BeTrue();
        fixture.Export.CanteenOrderIds.Should().Equal(eligible.Id);
        eligible.CanteenBatchRunId.Should().NotBeNull();
        future.CanteenBatchRunId.Should().BeNull();
        breakfast.CanteenBatchRunId.Should().BeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Payroll_ExistingRunIsNotResentAndIncompleteRunRequiresReview(bool completed)
    {
        using var fixture = new Fixture();
        await using var db = fixture.CreateDb();
        db.PayrollRuns.Add(new PayrollRun { RunKey = "202609:25", SentSuccessfully = completed });
        await db.SaveChangesAsync();
        var run = () => fixture.CreatePayrollRunner().RunAsync();

        if (completed)
        {
            (await run()).Should().Be(0);
        }
        else
        {
            await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Review*email outbox*");
        }

        fixture.Email.SendCount.Should().Be(0);
        fixture.Export.PayrollExportCount.Should().Be(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Payroll_FailureIsPersistedAndRetryRequiresReviewWithoutResending(bool cancel)
    {
        using var fixture = new Fixture();
        using var cancellation = new CancellationTokenSource();
        fixture.Email.Failure = cancel ? null : new IOException("Queue unavailable");
        fixture.Email.BeforeSend = cancel ? () => cancellation.Cancel() : null;
        var run = () => fixture.CreatePayrollRunner().RunAsync(cancellation.Token);

        if (cancel)
        {
            await run.Should().ThrowAsync<OperationCanceledException>();
        }
        else
        {
            await run.Should().ThrowAsync<IOException>();
        }

        await using var db = fixture.CreateDb();
        var savedRun = await db.PayrollRuns.SingleAsync();
        savedRun.SentSuccessfully.Should().BeFalse();
        savedRun.ErrorMessage.Should().NotBeNullOrEmpty();
        var retry = () => fixture.CreatePayrollRunner().RunAsync();
        await retry.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Review*email outbox*");
        fixture.Email.SendCount.Should().Be(1);
        fixture.Export.PayrollExportCount.Should().Be(1);
    }

    [Fact]
    public async Task Payroll_SuccessRemainsIdempotentAndKeepsExistingPeriod()
    {
        using var fixture = new Fixture();
        await using var db = fixture.CreateDb();
        db.CanteenOrders.AddRange(Order(new DateTime(2026, 9, 1)), Order(new DateTime(2026, 8, 31)),
            Order(fixture.Clock.UtcNow.AddSeconds(1).UtcDateTime));
        await db.SaveChangesAsync();
        var runner = fixture.CreatePayrollRunner();

        (await runner.RunAsync()).Should().Be(1);
        (await runner.RunAsync()).Should().Be(0);

        fixture.Email.SendCount.Should().Be(1);
        fixture.Export.PayrollTotal.Should().Be(10m);
        (await db.PayrollRuns.SingleAsync()).SentSuccessfully.Should().BeTrue();
    }

    private static CanteenOrder Order(DateTime timestamp) => new()
    {
        EmployeeUsername = "employee", ItemSummary = "Lunch", OrderTimeUtc = timestamp,
        TotalAmount = 10m, MealSlot = MealSlot.Lunch
    };

    private sealed class Fixture : IDisposable
    {
        private readonly DbContextOptions<CanteenDbContext> _dbOptions;
        private readonly ServiceProvider _services;
        public FakeClock Clock { get; } = new();
        public RecordingEmailSender Email { get; } = new();
        public RecordingExport Export { get; } = new();

        public Fixture()
        {
            _dbOptions = new DbContextOptionsBuilder<CanteenDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString(), new InMemoryDatabaseRoot()).Options;
            _services = new ServiceCollection()
                .AddScoped(_ => CreateDb())
                .AddSingleton<IEmailSender>(Email)
                .AddSingleton<IFileExportService>(Export)
                .AddSingleton<IFinanceAuditService>(new NullAudit())
                .BuildServiceProvider();
        }

        public CanteenDbContext CreateDb() => new(_dbOptions);
        public CanteenBatchService CreateCanteenService(CanteenDbContext db) => new(db, Export, Email,
            new NullAudit(), Microsoft.Extensions.Options.Options.Create(new RoutingInboxesOptions()),
            Microsoft.Extensions.Options.Options.Create(new CanteenBatchingOptions()), Clock);
        public MonthlyPayrollRunner CreatePayrollRunner() => new(_services.GetRequiredService<IServiceScopeFactory>(),
            Microsoft.Extensions.Options.Options.Create(new PayrollOptions()),
            Microsoft.Extensions.Options.Options.Create(new RoutingInboxesOptions()), Clock,
            NullLogger<MonthlyPayrollRunner>.Instance);
        public void Dispose() => _services.Dispose();
    }

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => new(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);
        public DateTimeOffset NowInZone(string timeZoneId) => UtcNow;
        public TimeZoneInfo ResolveTimeZone(string timeZoneId) => TimeZoneInfo.Utc;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public int SendCount { get; private set; }
        public Exception? Failure { get; set; }
        public Action? BeforeSend { get; set; }
        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            SendCount++;
            BeforeSend?.Invoke();
            if (cancellationToken.IsCancellationRequested)
                return Task.FromCanceled<EmailSendResult>(cancellationToken);
            return Failure is not null ? Task.FromException<EmailSendResult>(Failure) :
                Task.FromResult(new EmailSendResult { DeliveredViaOutbox = true, ArtifactPath = "email-outbox:1" });
        }
    }

    private sealed class RecordingExport : IFileExportService
    {
        public bool FailCanteen { get; set; }
        public int CanteenExportCount { get; private set; }
        public int PayrollExportCount { get; private set; }
        public int[] CanteenOrderIds { get; private set; } = [];
        public decimal PayrollTotal { get; private set; }
        public byte[] BuildCanteenBatchWorkbook(string worksheetName, IEnumerable<CanteenOrder> orders, DateTimeOffset generatedAt)
        {
            CanteenExportCount++;
            if (FailCanteen) throw new IOException("Workbook failed");
            CanteenOrderIds = orders.Select(x => x.Id).ToArray();
            return [];
        }
        public byte[] BuildPayrollWorkbook(string worksheetName, IEnumerable<PayrollExportRow> rows, DateTimeOffset generatedAt)
        {
            PayrollExportCount++;
            PayrollTotal = rows.Sum(x => x.TotalAmount);
            return [];
        }
        public byte[] BuildCanteenLedgerWorkbook(IEnumerable<CanteenLedgerSummaryRow> summaryRows,
            IEnumerable<CanteenOrder> detailRows, DateTimeOffset generatedAt) => throw new NotSupportedException();
    }

    private sealed class NullAudit : IFinanceAuditService
    {
        public Task WriteAsync(string actor, string action, string entity, string detail,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

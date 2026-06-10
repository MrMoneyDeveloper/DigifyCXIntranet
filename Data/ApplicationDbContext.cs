using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Data;

public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<CanteenOrder> CanteenOrders => Set<CanteenOrder>();
    public DbSet<CanteenBatchRun> CanteenBatchRuns => Set<CanteenBatchRun>();
    public DbSet<PayrollRun> PayrollRuns => Set<PayrollRun>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<ReferralInvite> ReferralInvites => Set<ReferralInvite>();
    public DbSet<ExternalApplication> ExternalApplications => Set<ExternalApplication>();
    public DbSet<InternalJobApplication> InternalJobApplications => Set<InternalJobApplication>();
    public DbSet<PolicyDocument> PolicyDocuments => Set<PolicyDocument>();
    public DbSet<ZendeskPolicyArticle> ZendeskPolicyArticles => Set<ZendeskPolicyArticle>();
    public DbSet<PolicyAcknowledgement> PolicyAcknowledgements => Set<PolicyAcknowledgement>();
    public DbSet<ZendeskSyncLog> ZendeskSyncLogs => Set<ZendeskSyncLog>();
    public DbSet<FinanceAuditLog> FinanceAuditLogs => Set<FinanceAuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureCanteenDomain();
        builder.ConfigureHrDomain();
        builder.ConfigurePolicyDomain();
    }
}

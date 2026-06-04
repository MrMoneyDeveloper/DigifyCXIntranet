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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Fix the multiple cascade paths error for External Applications using 'builder'
        builder.Entity<ExternalApplication>()
            .HasOne(e => e.ReferralInvite)
            .WithMany()
            .HasForeignKey(e => e.ReferralInviteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<CanteenOrder>()
            .Property(x => x.TotalAmount)
            .HasPrecision(18, 2);

        builder.Entity<MenuItem>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        builder.Entity<ReferralInvite>()
            .HasIndex(x => x.Token)
            .IsUnique();

        builder.Entity<CanteenBatchRun>()
            .HasIndex(x => x.RunKey)
            .IsUnique();

        builder.Entity<PayrollRun>()
            .HasIndex(x => x.RunKey)
            .IsUnique();

        builder.Entity<PolicyAcknowledgement>()
            .HasIndex(x => new { x.EmployeeDomainName, x.PolicyArticleId, x.PolicyVersion })
            .IsUnique();
    }
}
using DigifyCXIntranet.Models;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Data;

public static class DomainModelBuilderExtensions
{
    public static void ConfigureCanteenDomain(this ModelBuilder builder)
    {
        builder.Entity<CanteenOrder>()
            .Property(x => x.TotalAmount)
            .HasPrecision(18, 2);

        builder.Entity<CanteenOrder>()
            .HasIndex(x => new { x.EmployeeUsername, x.OrderTimeUtc });

        builder.Entity<MenuItem>()
            .Property(x => x.Price)
            .HasPrecision(18, 2);

        builder.Entity<MenuItem>()
            .HasIndex(x => new { x.IsDeleted, x.IsActive, x.MealSlot, x.DisplayOrder });

        builder.Entity<CanteenBatchRun>()
            .HasIndex(x => x.RunKey)
            .IsUnique();

        builder.Entity<PayrollRun>()
            .HasIndex(x => x.RunKey)
            .IsUnique();

        builder.Entity<FinanceAuditLog>()
            .HasIndex(x => x.TimestampUtc);
    }

    public static void ConfigureHrDomain(this ModelBuilder builder)
    {
        builder.Entity<ExternalApplication>()
            .HasOne(e => e.ReferralInvite)
            .WithMany()
            .HasForeignKey(e => e.ReferralInviteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<JobPosting>()
            .HasIndex(x => new { x.IsDeleted, x.IsActive, x.ClosingDate });

        builder.Entity<ReferralInvite>()
            .HasIndex(x => x.Token)
            .IsUnique();
    }

    public static void ConfigurePolicyDomain(this ModelBuilder builder)
    {
        builder.Entity<PolicyAcknowledgement>()
            .HasIndex(x => new { x.EmployeeDomainName, x.PolicyArticleId, x.PolicyVersion })
            .IsUnique();

        builder.Entity<ZendeskSyncLog>()
            .HasIndex(x => x.StartedUtc);

        builder.Entity<ZendeskPolicyArticle>()
            .HasIndex(x => new { x.SectionName, x.Title });
    }
}

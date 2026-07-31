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

        builder.Entity<CanteenOrder>()
            .HasIndex(x => new { x.OrderTimeUtc, x.Status, x.CanteenBatchRunId });

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

        builder.Entity<FinanceAuditLog>()
            .HasIndex(x => new { x.Actor, x.Action, x.TimestampUtc });
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

        builder.Entity<JobPosting>()
            .HasIndex(x => new { x.IsDeleted, x.ClosingDate });

        builder.Entity<ReferralInvite>()
            .HasIndex(x => x.Token)
            .IsUnique();
    }

    public static void ConfigurePolicyDomain(this ModelBuilder builder)
    {
        builder.Entity<PolicyAcknowledgement>()
            .HasIndex(x => new { x.EmployeeDomainName, x.PolicyArticleId, x.PolicyVersion })
            .IsUnique();

        builder.Entity<PolicyAcknowledgement>()
            .HasIndex(x => new { x.EmployeeDomainName, x.TimestampUtc });

        builder.Entity<PolicyAcknowledgement>()
            .HasIndex(x => new { x.PolicyArticleId, x.TimestampUtc });

        builder.Entity<ZendeskSyncLog>()
            .HasIndex(x => x.StartedUtc);

        builder.Entity<ZendeskSyncLog>()
            .HasIndex(x => new { x.Succeeded, x.StartedUtc });

        builder.Entity<ZendeskPolicyArticle>()
            .HasIndex(x => new { x.SectionName, x.Title });

        builder.Entity<ZendeskPolicyArticle>()
            .HasIndex(x => new { x.IsPublished, x.CategoryName, x.SectionName, x.UpdatedAtUtc });
    }

    public static void ConfigureOperationalDomain(this ModelBuilder builder)
    {
        builder.Entity<Announcement>()
            .HasIndex(x => new { x.IsDeleted, x.IsActive, x.IsPinned, x.PublishDateUtc, x.ExpirationDate });

        builder.Entity<AuditLog>()
            .HasIndex(x => x.TimestampUtc);

        builder.Entity<AuditLog>()
            .HasIndex(x => new { x.Actor, x.Action, x.TimestampUtc });

        builder.Entity<AuditLog>()
            .HasIndex(x => new { x.Entity, x.EntityId, x.TimestampUtc });

        builder.Entity<BackgroundJobRun>()
            .HasIndex(x => new { x.JobName, x.StartedUtc });

        builder.Entity<BackgroundJobRun>()
            .HasIndex(x => new { x.Status, x.StartedUtc });

        builder.Entity<EmailOutboxMessage>()
            .HasIndex(x => new { x.Status, x.CreatedUtc });

        builder.Entity<EmailOutboxMessage>()
            .HasMany(x => x.Attachments)
            .WithOne(x => x.Message)
            .HasForeignKey(x => x.EmailOutboxMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

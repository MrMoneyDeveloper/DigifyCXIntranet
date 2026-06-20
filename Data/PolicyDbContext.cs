using DigifyCXIntranet.Models;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Data;

public class PolicyDbContext : DbContext
{
    public PolicyDbContext(DbContextOptions<PolicyDbContext> options)
        : base(options)
    {
    }

    public DbSet<PolicyDocument> PolicyDocuments => Set<PolicyDocument>();
    public DbSet<ZendeskPolicyArticle> ZendeskPolicyArticles => Set<ZendeskPolicyArticle>();
    public DbSet<PolicyAcknowledgement> PolicyAcknowledgements => Set<PolicyAcknowledgement>();
    public DbSet<ZendeskSyncLog> ZendeskSyncLogs => Set<ZendeskSyncLog>();

    public override int SaveChanges()
    {
        EntityValidation.Validate(ChangeTracker);
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        EntityValidation.Validate(ChangeTracker);
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigurePolicyDomain();
    }
}

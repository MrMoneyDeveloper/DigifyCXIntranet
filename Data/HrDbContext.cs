using DigifyCXIntranet.Models;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Data;

public class HrDbContext : DbContext
{
    public HrDbContext(DbContextOptions<HrDbContext> options)
        : base(options)
    {
    }

    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<ReferralInvite> ReferralInvites => Set<ReferralInvite>();
    public DbSet<ExternalApplication> ExternalApplications => Set<ExternalApplication>();
    public DbSet<InternalJobApplication> InternalJobApplications => Set<InternalJobApplication>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ConfigureHrDomain();
    }
}

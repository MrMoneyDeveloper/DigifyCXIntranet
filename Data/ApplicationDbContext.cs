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
    public DbSet<FaqItem> FaqItems => Set<FaqItem>();
    public DbSet<JobPosting> JobPostings => Set<JobPosting>();
    public DbSet<PolicyDocument> PolicyDocuments => Set<PolicyDocument>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<CanteenOrder>()
            .Property(x => x.TotalAmount)
            .HasPrecision(18, 2);
    }
}

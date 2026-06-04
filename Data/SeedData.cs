using DigifyCXIntranet.Models;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Data;

public static class SeedData
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        if (!await context.MenuItems.AnyAsync())
        {
            context.MenuItems.AddRange(
                new MenuItem { Name = "Breakfast Wrap", Price = 42.00m, Emoji = "🌯", MealSlot = MealSlot.Breakfast, DisplayOrder = 1 },
                new MenuItem { Name = "Egg & Toast Combo", Price = 36.00m, Emoji = "🍳", MealSlot = MealSlot.Breakfast, DisplayOrder = 2 },
                new MenuItem { Name = "Burger Meal", Price = 58.50m, Emoji = "🍔", MealSlot = MealSlot.Lunch, DisplayOrder = 1 },
                new MenuItem { Name = "Chicken Salad Bowl", Price = 48.00m, Emoji = "🥗", MealSlot = MealSlot.Lunch, DisplayOrder = 2 });
            await context.SaveChangesAsync();
        }

        if (!await context.Announcements.AnyAsync())
        {
            context.Announcements.AddRange(
                new Announcement
                {
                    Title = "Welcome to the DigifyCX Employee Platform",
                    Summary = "Internal updates, policies, canteen requests, and jobs are centralised here.",
                    Content = "Use the navigation menu to access all employee services and operational updates.",
                    IsPinned = true,
                    IsActive = true,
                    CreatedDateUtc = DateTime.UtcNow.AddHours(-2),
                    PublishDateUtc = DateTime.UtcNow.AddHours(-2),
                    LastUpdatedBy = "System"
                },
                new Announcement
                {
                    Title = "Monthly Townhall - Friday 10:00",
                    Summary = "Leadership update and performance highlights.",
                    Content = "Townhall will be held in the main boardroom and streamed to all departments.",
                    IsPinned = false,
                    IsActive = true,
                    CreatedDateUtc = DateTime.UtcNow.AddDays(-1),
                    PublishDateUtc = DateTime.UtcNow.AddDays(-1),
                    ExpirationDate = DateTime.UtcNow.Date.AddDays(21),
                    LastUpdatedBy = "System"
                });
        }

        if (!await context.JobPostings.AnyAsync())
        {
            context.JobPostings.AddRange(
                new JobPosting
                {
                    Title = "Customer Experience Coach",
                    Department = "Operations",
                    Description = "Support team leads with coaching and quality reviews.",
                    ApplicationRoute = "Apply on this portal",
                    ClosingDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
                    IsExternalReferral = true,
                    UseVisualAd = true,
                    AdHeadline = "Join as Customer Experience Coach",
                    AdSubHeadline = "Drive coaching quality, uplift advisor performance, and mentor future team leaders.",
                    AdBackgroundImagePath = "/images/job-ad-a4-template.svg",
                    IsActive = true,
                    LastUpdatedBy = "System"
                },
                new JobPosting
                {
                    Title = "Data Analyst (Referral)",
                    Department = "Business Intelligence",
                    Description = "Analyze performance metrics and generate reporting insights.",
                    ApplicationRoute = "Apply on this portal",
                    ClosingDate = DateOnly.FromDateTime(DateTime.Today.AddDays(21)),
                    IsExternalReferral = true,
                    UseVisualAd = true,
                    AdHeadline = "Data Analyst Referral Campaign",
                    AdSubHeadline = "Build dashboards, forecast trends, and help leadership make sharper decisions.",
                    AdBackgroundImagePath = "/images/job-ad-a4-template.svg",
                    IsActive = true,
                    LastUpdatedBy = "System"
                });
        }

        if (!await context.FaqItems.AnyAsync())
        {
            context.FaqItems.AddRange(
                new FaqItem
                {
                    Category = "Canteen",
                    Question = "How do I submit a canteen request?",
                    Answer = "Open the Canteen page and select a menu card.",
                    DisplayOrder = 1,
                    IsActive = true
                },
                new FaqItem
                {
                    Category = "Policies",
                    Question = "How do policy acknowledgements work?",
                    Answer = "A new acknowledgement is required whenever a policy version changes.",
                    DisplayOrder = 2,
                    IsActive = true
                });
        }

        /*if (!await context.ZendeskPolicyArticles.AnyAsync())
        {
            context.ZendeskPolicyArticles.Add(new ZendeskPolicyArticle
            {
                ZendeskArticleId = 100001,
                Title = "Sample Policy (Awaiting Zendesk Sync)",
                HtmlUrl = "https://example.zendesk.com/hc/en-us/articles/100001",
                VersionLabel = "v1.0",
                Body = "This is a fallback sample policy record. Configure ZendeskSync.BaseUrl for live sync.",
                IsPublished = true,
                UpdatedAtUtc = DateTime.UtcNow.AddDays(-1),
                SyncedAtUtc = DateTime.UtcNow
            });
        }*/

        if (!await context.CanteenOrders.AnyAsync())
        {
            var menu = await context.MenuItems.OrderBy(x => x.Id).ToListAsync();
            var first = menu.FirstOrDefault();
            var second = menu.Skip(1).FirstOrDefault();
            if (first is not null)
            {
                context.CanteenOrders.Add(new CanteenOrder
                {
                    EmployeeUsername = "moham",
                    MenuItemId = first.Id,
                    ItemSummary = first.Name,
                    MealSlot = first.MealSlot,
                    TotalAmount = first.Price,
                    OrderTimeUtc = DateTime.UtcNow.AddHours(-3),
                    Status = "Submitted"
                });
            }

            if (second is not null)
            {
                context.CanteenOrders.Add(new CanteenOrder
                {
                    EmployeeUsername = "admin",
                    MenuItemId = second.Id,
                    ItemSummary = second.Name,
                    MealSlot = second.MealSlot,
                    TotalAmount = second.Price,
                    OrderTimeUtc = DateTime.UtcNow.AddHours(-1),
                    Status = "Submitted"
                });
            }
        }

        await context.SaveChangesAsync();
    }
}

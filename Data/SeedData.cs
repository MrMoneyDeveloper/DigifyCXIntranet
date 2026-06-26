using DigifyCXIntranet.Models;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Data;

public static class SeedData
{
    public static async Task InitializeAsync(ApplicationDbContext context)
    {
        await SeedMenuAsync(context);
        await SeedAnnouncementsAsync(context);
        await SeedJobsAsync(context);
        await SeedFaqsAsync(context);
        await SeedCanteenOrdersAsync(context);
        await context.SaveChangesAsync();
    }

    private static async Task SeedMenuAsync(ApplicationDbContext context)
    {
        var menuSeeds = new[]
        {
            new MenuItem { Name = "Breakfast Wrap", Price = 42.00m, Emoji = "\U0001F32F", MealSlot = MealSlot.Breakfast, DisplayOrder = 1, ImagePath = "/images/canteen/breakfast-wrap.svg" },
            new MenuItem { Name = "Egg & Toast Combo", Price = 36.00m, Emoji = "\U0001F373", MealSlot = MealSlot.Breakfast, DisplayOrder = 2, ImagePath = "/images/canteen/egg-toast-combo.svg" },
            new MenuItem { Name = "Burger Meal", Price = 58.50m, Emoji = "\U0001F354", MealSlot = MealSlot.Lunch, DisplayOrder = 1, ImagePath = "/images/canteen/burger-meal.svg" },
            new MenuItem { Name = "Chicken Salad Bowl", Price = 48.00m, Emoji = "\U0001F957", MealSlot = MealSlot.Lunch, DisplayOrder = 2, ImagePath = "/images/canteen/chicken-salad-bowl.svg" },
            new MenuItem { Name = "Veggie Bowl", Price = 44.00m, Emoji = "\U0001F96C", MealSlot = MealSlot.Lunch, DisplayOrder = 3, ImagePath = "/images/canteen/veggie-bowl.svg" }
        };

        foreach (var seed in menuSeeds)
        {
            var existing = await context.MenuItems.FirstOrDefaultAsync(x => x.Name == seed.Name);
            if (existing is null)
            {
                context.MenuItems.Add(seed);
                continue;
            }

            if (string.IsNullOrWhiteSpace(existing.ImagePath))
            {
                existing.ImagePath = seed.ImagePath;
            }

            existing.IsDeleted = false;
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedAnnouncementsAsync(ApplicationDbContext context)
    {
        if (await context.Announcements.AnyAsync())
        {
            return;
        }

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

    private static async Task SeedJobsAsync(ApplicationDbContext context)
    {
        if (await context.JobPostings.AnyAsync())
        {
            return;
        }

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
                LastUpdatedBy = "System",
                CreatedDateUtc = DateTime.UtcNow.AddDays(-3),
                UpdatedDateUtc = DateTime.UtcNow.AddDays(-3)
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
                LastUpdatedBy = "System",
                CreatedDateUtc = DateTime.UtcNow.AddDays(-1),
                UpdatedDateUtc = DateTime.UtcNow.AddDays(-1)
            });
    }

    private static async Task SeedFaqsAsync(ApplicationDbContext context)
    {
        if (await context.FaqItems.AnyAsync())
        {
            return;
        }

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

    private static async Task SeedCanteenOrdersAsync(ApplicationDbContext context)
    {
        if (await context.CanteenOrders.CountAsync() >= 8)
        {
            return;
        }

        var menu = await context.MenuItems.OrderBy(x => x.Id).ToListAsync();
        if (menu.Count == 0)
        {
            return;
        }

        var users = new[] { "employee", "moham", "finance", "admin" };
        var orderTime = DateTime.UtcNow.AddDays(-5);
        foreach (var username in users)
        {
            foreach (var item in menu.Take(2))
            {
                var exists = await context.CanteenOrders.AnyAsync(x =>
                    x.EmployeeUsername == username &&
                    x.MenuItemId == item.Id &&
                    x.OrderTimeUtc.Date == orderTime.Date);
                if (exists)
                {
                    continue;
                }

                context.CanteenOrders.Add(new CanteenOrder
                {
                    EmployeeUsername = username,
                    MenuItemId = item.Id,
                    ItemSummary = item.Name,
                    MealSlot = item.MealSlot,
                    TotalAmount = item.Price,
                    OrderTimeUtc = orderTime,
                    Status = "Submitted"
                });

                orderTime = orderTime.AddHours(6);
            }
        }
    }
}

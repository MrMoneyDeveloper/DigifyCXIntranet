using DigifyCXIntranet.Models;
using Microsoft.EntityFrameworkCore;

namespace DigifyCXIntranet.Data;

public static class SeedData
{
    public static async Task InitializeAsync(ApplicationDbContext context, bool resetForTesting = false)
    {
        if (resetForTesting)
        {
            context.CanteenOrders.RemoveRange(context.CanteenOrders);
            context.FaqItems.RemoveRange(context.FaqItems);
            context.JobPostings.RemoveRange(context.JobPostings);
            context.Announcements.RemoveRange(context.Announcements);
            context.PolicyDocuments.RemoveRange(context.PolicyDocuments);
            await context.SaveChangesAsync();
        }

        if (!await context.PolicyDocuments.AnyAsync())
        {
            context.PolicyDocuments.AddRange(
                new PolicyDocument
                {
                    Title = "Code of Conduct",
                    ContentType = PolicyContentType.Policy,
                    VersionLabel = "v1.3",
                    EffectiveDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-20)),
                    Content = "All employees must maintain respectful conduct, data confidentiality, and compliance with approved procedures.",
                    LastUpdatedBy = "System",
                    LastUpdatedUtc = DateTime.UtcNow.AddDays(-20)
                },
                new PolicyDocument
                {
                    Title = "Leave Request Procedure",
                    ContentType = PolicyContentType.Procedure,
                    VersionLabel = "v1.1",
                    EffectiveDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-8)),
                    Content = "Submit leave requests through your line manager at least 48 hours in advance, except emergencies.",
                    LastUpdatedBy = "System",
                    LastUpdatedUtc = DateTime.UtcNow.AddDays(-8)
                });
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
                    PublishDateUtc = DateTime.UtcNow.AddDays(-1),
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
                    ApplicationRoute = "hr@digifycx.example",
                    ClosingDate = DateOnly.FromDateTime(DateTime.Today.AddDays(14)),
                    IsExternalReferral = false,
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
                    ApplicationRoute = "zendesk://hr-referrals",
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
                    Answer = "Open the Canteen page and submit the request form with date and total amount.",
                    DisplayOrder = 1,
                    IsActive = true
                },
                new FaqItem
                {
                    Category = "Policies",
                    Question = "Can I download policies?",
                    Answer = "Policies are presented as read-only content in the platform for controlled distribution.",
                    DisplayOrder = 2,
                    IsActive = true
                },
                new FaqItem
                {
                    Category = "Jobs",
                    Question = "How are referrals handled?",
                    Answer = "External referrals route outside the intranet to approved HR workflows.",
                    DisplayOrder = 3,
                    IsActive = true
                });
        }

        if (!await context.CanteenOrders.AnyAsync())
        {
            var monthStart = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
            context.CanteenOrders.AddRange(
                new CanteenOrder
                {
                    EmployeeUsername = "moham",
                    ItemSummary = "Chicken wrap + juice",
                    OrderDate = monthStart.AddDays(3),
                    TotalAmount = 58.50m,
                    Status = "Paid",
                    EmploymentMonthsAtOrder = 9
                },
                new CanteenOrder
                {
                    EmployeeUsername = "moham",
                    ItemSummary = "Breakfast combo",
                    OrderDate = monthStart.AddDays(8),
                    TotalAmount = 42.00m,
                    Status = "Approved",
                    EmploymentMonthsAtOrder = 9
                },
                new CanteenOrder
                {
                    EmployeeUsername = "test.user",
                    ItemSummary = "Salad bowl",
                    OrderDate = monthStart.AddDays(6),
                    TotalAmount = 36.00m,
                    Status = "Submitted",
                    EmploymentMonthsAtOrder = 5
                });
        }

        await context.SaveChangesAsync();
    }
}

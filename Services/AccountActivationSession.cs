using System.Globalization;
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DigifyCXIntranet.Services;

internal sealed record AccountActivationSession(string UserId, string Username, string ResetToken, string IpAddress)
{
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);
    private static readonly string[] Keys =
    {
        "ActivationUsername", "ActivationIp", "ActivationDisplay", "ActivationUserId",
        "ActivationResetToken", "ActivationVerifiedUtc"
    };

    internal static void Issue(
        ITempDataDictionary tempData,
        ApplicationUser user,
        string resetToken,
        string ipAddress,
        DateTimeOffset now)
    {
        tempData["ActivationUsername"] = user.UserName;
        tempData["ActivationUserId"] = user.Id;
        tempData["ActivationResetToken"] = resetToken;
        tempData["ActivationIp"] = ipAddress;
        tempData["ActivationVerifiedUtc"] = now.ToString("O", CultureInfo.InvariantCulture);
    }

    internal static AccountActivationSession? Read(ITempDataDictionary tempData, DateTimeOffset now)
    {
        var userId = tempData.Peek("ActivationUserId") as string;
        var username = tempData.Peek("ActivationUsername") as string;
        var token = tempData.Peek("ActivationResetToken") as string;
        var verifiedUtc = tempData.Peek("ActivationVerifiedUtc") as string;
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(token) ||
            !DateTimeOffset.TryParseExact(verifiedUtc, "O", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var issuedAt) ||
            issuedAt > now || now - issuedAt >= Lifetime)
        {
            return null;
        }

        return new AccountActivationSession(userId, username, token,
            tempData.Peek("ActivationIp") as string ?? "unknown");
    }

    internal bool Matches(ApplicationUser? user) =>
        user is { IsFirstTimeLogin: true } &&
        string.Equals(UserId, user.Id, StringComparison.Ordinal) &&
        string.Equals(Username, user.UserName, StringComparison.OrdinalIgnoreCase);

    internal static void Clear(ITempDataDictionary tempData)
    {
        foreach (var key in Keys)
        {
            tempData.Remove(key);
        }
    }
}

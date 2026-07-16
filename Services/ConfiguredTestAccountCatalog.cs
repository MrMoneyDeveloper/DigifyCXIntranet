using DigifyCXIntranet.Options;

namespace DigifyCXIntranet.Services;

public sealed record ConfiguredTestAccount(
    string Username,
    string Password,
    string DisplayName,
    string Role);

public static class ConfiguredTestAccountCatalog
{
    public static IReadOnlyList<ConfiguredTestAccount> ValidateAndNormalize(AuthModeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DevelopmentUsers.Count == 0)
        {
            throw new InvalidOperationException(
                "AuthMode:SeedConfiguredTestUsers is enabled, but no configured test users were supplied.");
        }

        var accounts = new List<ConfiguredTestAccount>(options.DevelopmentUsers.Count);
        var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < options.DevelopmentUsers.Count; index++)
        {
            var source = options.DevelopmentUsers[index];
            var username = source.Username.Trim();
            var password = source.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                throw new InvalidOperationException($"Configured test account {index} has no username.");
            }

            if (string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException($"Configured test account {index} has no password.");
            }

            if (!usernames.Add(username))
            {
                throw new InvalidOperationException($"Configured test account {index} duplicates another username.");
            }

            if (!AppRoles.TryNormalize(source.Role, out var role))
            {
                throw new InvalidOperationException($"Configured test account {index} has an unsupported role.");
            }

            accounts.Add(new ConfiguredTestAccount(
                username,
                password,
                string.IsNullOrWhiteSpace(source.DisplayName) ? username : source.DisplayName.Trim(),
                role));
        }

        return accounts;
    }
}

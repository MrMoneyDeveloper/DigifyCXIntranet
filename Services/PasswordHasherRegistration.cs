// This file ensures IPasswordHasher<ApplicationUser> is available in DI
// without registering full ASP.NET Identity (which this project does not use).
// It is called from Program.cs via: builder.Services.AddPasswordHasher();
using DigifyCXIntranet.Models;
using Microsoft.AspNetCore.Identity;

namespace DigifyCXIntranet.Services;

public static class PasswordHasherRegistration
{
    /// <summary>
    /// Registers <see cref="IPasswordHasher{TUser}"/> for <see cref="ApplicationUser"/>
    /// as a standalone service — no full Identity stack required.
    /// </summary>
    public static IServiceCollection AddPasswordHasher(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
        return services;
    }
}

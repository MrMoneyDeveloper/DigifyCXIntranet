using Company.Product.Api.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Company.Product.Api.Authorization;

public static class AuthorizationPolicyBuilderExtensions
{
    public static AuthorizationPolicyBuilder RequireScope(this AuthorizationPolicyBuilder builder, string scope)
    {
        return builder.RequireAssertion(context =>
        {
            var jwtOptions = context.Resource switch
            {
                HttpContext httpContext => httpContext.RequestServices.GetRequiredService<IOptions<JwtOptions>>().Value,
                _ => null
            };

            var scopeClaimType = jwtOptions?.ScopeClaimType ?? "scope";

            var scopes = context.User
                .FindAll(scopeClaimType)
                .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return scopes.Contains(scope);
        });
    }
}

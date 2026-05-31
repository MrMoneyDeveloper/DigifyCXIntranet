namespace Company.Product.Contracts.Errors;

public static class ApiErrorCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string ResourceNotFound = "RESOURCE_NOT_FOUND";
    public const string DomainRuleViolation = "DOMAIN_RULE_VIOLATION";
    public const string Unauthorized = "UNAUTHORIZED";
    public const string Forbidden = "FORBIDDEN";
    public const string Unexpected = "UNEXPECTED_ERROR";
}

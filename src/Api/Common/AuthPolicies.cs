namespace Api.Common;

internal static class AuthPolicies
{
    public const string Customer = nameof(Customer);
    public const string Agent = nameof(Agent);
    public const string LoginRateLimit = "login";
}

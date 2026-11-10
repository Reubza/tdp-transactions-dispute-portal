namespace Domain.Common;

internal static class Guard
{
    public static string NotBlank(string? value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value is required.", paramName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"Value must be at most {maxLength} characters.", paramName);
        }

        return trimmed;
    }

    public static string? Optional(string? value, string paramName, int maxLength) =>
        string.IsNullOrWhiteSpace(value) ? null : NotBlank(value, paramName, maxLength);
}
